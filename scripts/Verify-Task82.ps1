param([string]$BaseUrl = 'http://127.0.0.1:7303')
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '../WarehouseManagement.csproj'
$outputDirectory = Join-Path $PSScriptRoot '../bin/Task82Evidence'
$null = New-Item -ItemType Directory -Force $outputDirectory
$checks = 0
function Assert-Check($value, [string]$message) {
    if (!$value) { throw "FAIL: $message" }
    $script:checks++
    Write-Host "PASS: $message"
}
function Read-Snapshot {
    $json = sqlcmd -S THI-DIEU -E -d WarehouseManagementDb -b -y 0 -w 65535 -Q @'
SET NOCOUNT ON;
SELECT p.Id,p.Code,p.Name,c.Name CategoryName,p.Unit,p.IsActive,p.CurrentQuantity,p.MinimumStockLevel,p.CategoryId
FROM Products p JOIN Categories c ON c.Id=p.CategoryId ORDER BY p.Id FOR JSON PATH;
'@
    if ($LASTEXITCODE -ne 0) { throw 'SQL snapshot failed' }
    return @(($json -join '') | ConvertFrom-Json)
}
function Request([string]$path, $session) {
    Invoke-WebRequest ($BaseUrl + $path) -WebSession $session -SkipHttpErrorCheck -MaximumRedirection 0 -ErrorAction SilentlyContinue
}
function Rows([string]$html) {
    foreach ($row in [regex]::Matches($html, '<tr data-product-id="(\d+)">(.*?)</tr>', 'Singleline')) {
        $cells = @([regex]::Matches($row.Groups[2].Value, '<td[^>]*>(.*?)</td>', 'Singleline') | ForEach-Object {
            [System.Net.WebUtility]::HtmlDecode(([regex]::Replace($_.Groups[1].Value, '<[^>]*>', '')).Trim())
        })
        [pscustomobject]@{ Id = [int]$row.Groups[1].Value; Cells = $cells }
    }
}
$before = @(Read-Snapshot)
$alerts = @($before | Where-Object { $_.IsActive -and $_.CurrentQuantity -le $_.MinimumStockLevel } |
    Sort-Object @{Expression={if ($_.CurrentQuantity -eq 0) {0} else {1}}},Code,Id)
$secrets = @{}
dotnet user-secrets list --project $project | ForEach-Object {
    $parts = $_ -split ' = ',2
    if ($parts.Count -eq 2) { $secrets[$parts[0]] = $parts[1] }
}
$anonymousSession = New-Object Microsoft.PowerShell.Commands.WebRequestSession
foreach ($path in @('/Inventory/LowStock','/Inventory/ExportLowStock')) {
    $response = Request $path $anonymousSession
    Assert-Check ($response.StatusCode -eq 302 -and $response.Headers.Location -match '/Account/Login') "Anonymous blocked $path"
}
foreach ($role in @('WarehouseStaff','Accountant')) {
    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $login = Request '/Account/Login' $session
    $token = [regex]::Match($login.Content, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"').Groups[1].Value
    $loginResult = Invoke-WebRequest "$BaseUrl/Account/Login" -WebSession $session -Method Post -Body @{
        LoginIdentifier = $secrets["TestAccountSeed:${role}:UserName"]
        Password = $secrets["TestAccountSeed:${role}:Password"]
        __RequestVerificationToken = $token
    } -MaximumRedirection 0 -SkipHttpErrorCheck -ErrorAction SilentlyContinue
    Assert-Check ($loginResult.StatusCode -eq 302) "$role login"
    $page = Request '/Inventory/LowStock' $session
    Assert-Check ($page.StatusCode -eq 200) "$role low-stock access"
    if ($role -eq 'WarehouseStaff') {
        $export = Request '/Inventory/ExportLowStock' $session
        Assert-Check ($export.StatusCode -eq 302 -and $export.Headers.Location -match '/Account/AccessDenied') 'Staff export denied by ViewReports'
        Assert-Check (!$page.Content.Contains('/Inventory/ExportLowStock')) 'Staff export button hidden'
        continue
    }
    Assert-Check ($page.Content.Contains('/Inventory/ExportLowStock')) 'Accountant export button visible'
    $cases = @(
        @{ Query=''; Expected=$alerts },
        @{ Query='stockStatus=OutOfStock'; Expected=@($alerts | Where-Object CurrentQuantity -eq 0) },
        @{ Query='stockStatus=LowStock'; Expected=@($alerts | Where-Object CurrentQuantity -gt 0) },
        @{ Query='categoryId=3'; Expected=@($alerts | Where-Object CategoryId -eq 3) },
        @{ Query='searchTerm=HH002'; Expected=@($alerts | Where-Object Code -like '*HH002*') },
        @{ Query=('searchTerm='+[Uri]::EscapeDataString('Nước mắm')); Expected=@($alerts | Where-Object Name -eq 'Nước mắm') },
        @{ Query='categoryId=3&stockStatus=OutOfStock'; Expected=@($alerts | Where-Object { $_.CategoryId -eq 3 -and $_.CurrentQuantity -eq 0 }) },
        @{ Query='searchTerm=does-not-exist'; Expected=@() },
        @{ Query='page=2'; Expected=$alerts },
        @{ Query='page=2147483647'; Expected=$alerts }
    )
    $caseIndex = 0
    foreach ($case in $cases) {
        $caseIndex++
        $response = Request ("/Inventory/LowStock?" + $case.Query) $session
        Assert-Check ($response.StatusCode -eq 200) "List HTTP 200 $($case.Query)"
        $html = [System.Net.WebUtility]::HtmlDecode($response.Content)
        $rows = @(Rows $response.Content)
        $expected = @($case.Expected)
        Assert-Check ($rows.Count -eq [Math]::Min(20,$expected.Count)) "SQL row count $($case.Query)"
        Assert-Check ($html.Contains(('data-count="total">'+$expected.Count+'</strong>'))) 'Filtered total count'
        $outCount = @($expected | Where-Object CurrentQuantity -eq 0).Count
        Assert-Check ($html.Contains(('data-count="out">'+$outCount+'</strong>')) -and $html.Contains(('data-count="low">'+($expected.Count-$outCount)+'</strong>'))) 'Filtered out/low counts'
        for ($i=0; $i -lt $rows.Count; $i++) {
            $actual=$rows[$i]; $e=$expected[$i]
            $label=if ($e.CurrentQuantity -eq 0) {'Hết hàng'} else {'Chạm hoặc dưới mức tối thiểu'}
            Assert-Check ($actual.Id -eq $e.Id -and $actual.Cells[0] -eq $e.Code -and $actual.Cells[1] -eq $e.Name -and $actual.Cells[2] -eq $e.CategoryName -and $actual.Cells[3] -eq $e.Unit -and [decimal]::Parse($actual.Cells[4],[cultureinfo]::InvariantCulture) -eq $e.CurrentQuantity -and [decimal]::Parse($actual.Cells[5],[cultureinfo]::InvariantCulture) -eq $e.MinimumStockLevel -and [decimal]::Parse($actual.Cells[6],[cultureinfo]::InvariantCulture) -eq ($e.CurrentQuantity-$e.MinimumStockLevel) -and $actual.Cells[7] -eq $label) "SQL row fields $($e.Code)"
        }
        if ($expected.Count -eq 0) { Assert-Check ($html.Contains('Không có hàng hóa cảnh báo phù hợp')) 'Empty state' }
        Assert-Check ($response.Content -match 'href="/Inventory/LowStock"[^>]*>Xóa lọc') 'Clear filter link has no parameters'
        $csvPath = Join-Path $outputDirectory "export-$caseIndex.csv"
        $download = Invoke-WebRequest ("$BaseUrl/Inventory/ExportLowStock?"+$case.Query) -WebSession $session -OutFile $csvPath -PassThru -SkipHttpErrorCheck
        Assert-Check ($download.StatusCode -eq 200 -and $download.Headers.'Content-Type' -match 'text/csv' -and $download.Headers.'Content-Disposition' -match 'canh-bao-ton-kho-.*\.csv') 'CSV download headers'
        $bytes=[IO.File]::ReadAllBytes($csvPath)
        Assert-Check ($bytes.Length -ge 3 -and $bytes[0] -eq 239 -and $bytes[1] -eq 187 -and $bytes[2] -eq 191) 'CSV UTF-8 BOM'
        $csv=@(Import-Csv -LiteralPath $csvPath -Encoding utf8)
        Assert-Check ($csv.Count -eq $expected.Count) 'CSV exports entire filtered result'
        for ($i=0; $i -lt $csv.Count; $i++) {
            $e=$expected[$i]; $actual=$csv[$i]
            $label=if ($e.CurrentQuantity -eq 0) {'Hết hàng'} else {'Chạm hoặc dưới mức tối thiểu'}
            Assert-Check ($actual.'Mã hàng hóa' -eq $e.Code -and $actual.'Tên hàng hóa' -eq $e.Name -and $actual.'Danh mục' -eq $e.CategoryName -and $actual.'Đơn vị tính' -eq $e.Unit -and [decimal]::Parse($actual.'Tồn hiện tại',[cultureinfo]::InvariantCulture) -eq $e.CurrentQuantity -and [decimal]::Parse($actual.'Mức tồn tối thiểu',[cultureinfo]::InvariantCulture) -eq $e.MinimumStockLevel -and [decimal]::Parse($actual.'Chênh lệch',[cultureinfo]::InvariantCulture) -eq ($e.CurrentQuantity-$e.MinimumStockLevel) -and $actual.'Trạng thái cảnh báo' -eq $label) "CSV row equals SQL $($e.Code)"
        }
    }
    foreach ($query in @('categoryId=999999','categoryId=abc','stockStatus=InStock','stockStatus=999',('searchTerm='+('x'*201)))) {
        $response=Request ("/Inventory/LowStock?"+$query) $session
        Assert-Check ($response.StatusCode -eq 200 -and $response.Content.Contains('validation-summary-errors') -and @(Rows $response.Content).Count -eq 0) "Invalid list filter $query"
        $response=Request ("/Inventory/ExportLowStock?"+$query) $session
        Assert-Check ($response.StatusCode -eq 400 -and $response.Headers.'Content-Type' -notmatch 'text/csv') 'Invalid export rejected, no unfiltered download'
    }
    foreach ($query in @('page=0','page=abc')) {
        $response=Request ("/Inventory/LowStock?"+$query) $session
        Assert-Check ($response.StatusCode -eq 200 -and $response.Content.Contains('validation-summary-errors')) 'Invalid page rejected'
    }
    $detail=Request ("/Inventory/Details/"+$alerts[0].Id) $session
    Assert-Check ($detail.StatusCode -eq 200 -and $detail.Content.Contains($alerts[0].Code)) 'Existing stock details reused'
    foreach ($status in @('OutOfStock','LowStock','InStock')) {
        $inventory = Request ("/Inventory/Index?stockStatus="+$status) $session
        $expectedCount = @($before | Where-Object {
            if ($status -eq 'OutOfStock') { $_.CurrentQuantity -eq 0 }
            elseif ($status -eq 'LowStock') { $_.CurrentQuantity -gt 0 -and $_.CurrentQuantity -le $_.MinimumStockLevel }
            else { $_.CurrentQuantity -gt $_.MinimumStockLevel }
        }).Count
        $detailLinks = [regex]::Matches($inventory.Content, 'href="/Inventory/Details/\d+"').Count
        Assert-Check ($inventory.StatusCode -eq 200 -and $detailLinks -eq [Math]::Min(20,$expectedCount)) "Existing inventory filter regression $status"
    }
}
$after=@(Read-Snapshot)
Assert-Check (($before | ConvertTo-Json -Compress) -eq ($after | ConvertTo-Json -Compress)) 'Product snapshot unchanged before/after'
Write-Output "Completed $checks HTTP/CSV/SQL checks. ADMIN login not tested; no configured password."
