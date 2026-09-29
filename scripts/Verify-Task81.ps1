param([string]$BaseUrl = 'http://127.0.0.1:7302')
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '../WarehouseManagement.csproj'
$checks = 0
function Assert-Check($condition, [string]$label) {
    if (!$condition) { throw "FAIL: $label" }
    $script:checks++
    Write-Host "PASS: $label"
}
function Get-Page([string]$path, $session) {
    $response = Invoke-WebRequest ($BaseUrl + $path) -WebSession $session -SkipHttpErrorCheck -MaximumRedirection 0 -ErrorAction SilentlyContinue
    Assert-Check ($response.StatusCode -eq 200) "HTTP 200 $path"
    return [System.Net.WebUtility]::HtmlDecode($response.Content)
}
function Get-Rows([string]$html) {
    foreach ($match in [regex]::Matches($html, '<tbody>(.*?)</tbody>', 'Singleline')) {
        foreach ($row in [regex]::Matches($match.Groups[1].Value, '<tr>(.*?)</tr>', 'Singleline')) {
            $cells = @([regex]::Matches($row.Groups[1].Value, '<td[^>]*>(.*?)</td>', 'Singleline') | ForEach-Object {
                ([regex]::Replace($_.Groups[1].Value, '<[^>]+>', '')).Trim()
            })
            if ($cells.Count -eq 13) { ,$cells }
        }
    }
}
$secrets = @{}
dotnet user-secrets list --project $project | ForEach-Object {
    $parts = $_ -split ' = ', 2
    if ($parts.Count -eq 2) { $secrets[$parts[0]] = $parts[1] }
}
$anonymous = Invoke-WebRequest "$BaseUrl/Reports/Dashboard" -MaximumRedirection 0 -SkipHttpErrorCheck -ErrorAction SilentlyContinue
Assert-Check ($anonymous.StatusCode -eq 302 -and $anonymous.Headers.Location -match '/Account/Login') 'Anonymous redirected to login'
$anonymousReport = Invoke-WebRequest "$BaseUrl/Reports/Index" -MaximumRedirection 0 -SkipHttpErrorCheck -ErrorAction SilentlyContinue
Assert-Check ($anonymousReport.StatusCode -eq 302 -and $anonymousReport.Headers.Location -match '/Account/Login') 'Anonymous report redirected to login'
foreach ($role in @('WarehouseStaff', 'Accountant')) {
    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $login = Invoke-WebRequest "$BaseUrl/Account/Login" -WebSession $session
    $token = [regex]::Match($login.Content, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"').Groups[1].Value
    $result = Invoke-WebRequest "$BaseUrl/Account/Login" -WebSession $session -Method Post -Body @{
        LoginIdentifier = $secrets["TestAccountSeed:${role}:UserName"]
        Password = $secrets["TestAccountSeed:${role}:Password"]
        __RequestVerificationToken = $token
    } -MaximumRedirection 0 -SkipHttpErrorCheck -ErrorAction SilentlyContinue
    Assert-Check ($result.StatusCode -eq 302 -and $result.Headers.Location -notmatch '/Account/Login') "$role login"
    $html = Get-Page '/Reports/Dashboard' $session
    Assert-Check ($html.Contains('44.125') -and $html.Contains('14.750') -and $html.Contains('29.375')) "$role dashboard matches SQL totals"
    if ($role -eq 'WarehouseStaff') {
        $denied = Invoke-WebRequest "$BaseUrl/Reports/Index" -WebSession $session -MaximumRedirection 0 -SkipHttpErrorCheck -ErrorAction SilentlyContinue
        Assert-Check ($denied.StatusCode -eq 302 -and $denied.Headers.Location -match '/Account/AccessDenied') 'Staff report access denied by existing policy'
        continue
    }
    $html = Get-Page '/Reports/Index' $session
    $rows = @(Get-Rows $html)
    Assert-Check ($rows.Count -eq 20) 'All 20 real products included, including no-movement products'
    $firstPageCodes = ($rows | ForEach-Object { $_[1] }) -join ','
    foreach ($page in @(2, 2147483647)) {
        $pagedHtml = Get-Page "/Reports/Index?page=$page" $session
        $pagedRows = @(Get-Rows $pagedHtml)
        Assert-Check ($pagedRows.Count -eq 20 -and (($pagedRows | ForEach-Object { $_[1] }) -join ',') -eq $firstPageCodes -and $pagedRows[0][0] -eq '1') "Out-of-range page $page clamps to the only real page"
        Assert-Check (!$pagedHtml.Contains('aria-label="Phân trang báo cáo"')) 'Single page does not show next/previous controls'
    }
    foreach ($row in $rows) {
        $opening = [decimal]::Parse($row[5], [cultureinfo]::InvariantCulture)
        $imported = [decimal]::Parse($row[6], [cultureinfo]::InvariantCulture)
        $exported = [decimal]::Parse($row[7], [cultureinfo]::InvariantCulture)
        $other = [decimal]::Parse($row[8], [cultureinfo]::InvariantCulture)
        $closing = [decimal]::Parse($row[9], [cultureinfo]::InvariantCulture)
        $current = [decimal]::Parse($row[10], [cultureinfo]::InvariantCulture)
        Assert-Check ($closing -eq $opening + $imported - $exported + $other -and $closing -eq $current) "Stock equation/current stock $($row[1])"
    }
    $row = @(Get-Rows (Get-Page '/Reports/Index?productId=2' $session))
    Assert-Check ($row.Count -eq 1 -and $row[0][6] -eq '15.000' -and $row[0][7] -eq '13.750' -and $row[0][9] -eq '1.250') 'HH002 matches independent SQL'
    $row = @(Get-Rows (Get-Page '/Reports/Index?productId=5' $session))
    Assert-Check ($row.Count -eq 1 -and $row[0][6] -eq '0.000' -and $row[0][9] -eq '0.000') 'Product without transactions'
    $row = @(Get-Rows (Get-Page '/Reports/Index?productId=2&fromDate=2026-09-23&toDate=2026-09-23' $session))
    Assert-Check ($row[0][5] -eq '1.250' -and $row[0][6] -eq '0.000' -and $row[0][7] -eq '0.000' -and $row[0][9] -eq '1.250') 'Opening carried forward into empty period'
    $row = @(Get-Rows (Get-Page '/Reports/Index?productId=2&toDate=2026-09-21' $session))
    Assert-Check ($row[0][9] -eq '0.000' -and $row[0][10] -eq '1.250') 'Historical closing distinct from current stock'
    $rows = @(Get-Rows (Get-Page '/Reports/Index?categoryId=3' $session))
    Assert-Check ($rows.Count -eq 4) 'Category filter matches SQL'
    $rows = @(Get-Rows (Get-Page '/Reports/Index?searchTerm=HH002' $session))
    Assert-Check ($rows.Count -eq 2) 'Code search'
    foreach ($sort in @('import','export')) {
        $rows = @(Get-Rows (Get-Page "/Reports/Index?sortBy=$sort" $session))
        Assert-Check ($rows[0][1] -eq 'HH002') "SQL sorting $sort"
    }
    foreach ($query in @('fromDate=bad-date','fromDate=2026-09-23&toDate=2026-09-22','toDate=9999-12-31','productId=999999','categoryId=999999','isActive=bad','sortBy=bad','page=0','productId=abc')) {
        $html = Get-Page "/Reports/Index?$query" $session
        Assert-Check ($html.Contains('validation-summary-errors') -and @(Get-Rows $html).Count -eq 0) "Invalid filter rejected: $query"
    }
    foreach ($query in @('searchTerm=does-not-exist','isActive=false')) {
        $html = Get-Page "/Reports/Index?$query" $session
        Assert-Check ($html.Contains('Không có hàng hóa phù hợp')) "Empty result: $query"
    }
    $html = Get-Page '/Reports/Dashboard?isActive=false' $session
    $cards = @([regex]::Matches($html, '<div class="h3 mb-0[^"]*">([^<]+)</div>') | ForEach-Object { $_.Groups[1].Value })
    Assert-Check ($cards.Count -eq 6 -and $cards[0] -eq '0' -and $cards[2] -eq '0.000' -and $cards[3] -eq '0.000' -and $cards[4] -eq '0.000' -and $cards[5] -eq '0') 'Empty matching products: stock, import, export, low-stock and active-product cards are zero'
    Assert-Check ($html.Contains('Không có giao dịch đã ghi sổ') -and !$html.Contains('class="report-bar report-bar-')) 'Empty dashboard does not render misleading chart bars'
    $emptyPage = Get-Page '/Reports/Index?isActive=false&page=2' $session
    Assert-Check (@(Get-Rows $emptyPage).Count -eq 0 -and !$emptyPage.Contains('aria-label="Phân trang báo cáo"')) 'Empty report with page=2 stays empty without pagination'
    foreach ($query in @('fromDate=2026-09-22&toDate=2026-09-22','groupBy=month')) {
        $html = Get-Page "/Reports/Dashboard?$query" $session
        Assert-Check ($html.Contains('44.125') -and $html.Contains('14.750') -and $html -match '<td class="text-end">5</td>' -and $html -match '<td class="text-end">6</td>') "Timeline quantity and distinct receipt counts: $query"
    }
    $html = Get-Page '/Reports/Dashboard?fromDate=2026-09-23&toDate=2026-09-23' $session
    Assert-Check ($html.Contains('Không có giao dịch đã ghi sổ')) 'Empty timeline'
    $html = Get-Page '/Reports/Dashboard?toDate=9999-12-31' $session
    Assert-Check ($html.Contains('Ngày kết thúc vượt phạm vi')) 'Maximum date dashboard regression'
    $html = Get-Page '/Reports/Index?fromDate=2026-09-22&toDate=2026-09-22&productId=2' $session
    Assert-Check ($html -match '/Inventory/Transactions\?[^" ]*fromDate=2026-09-22[^" ]*toDate=2026-09-22') 'History link preserves reporting period'
}
Write-Output "Completed $checks checks. Real warehouse data was only read; no receipts or stock were modified."
