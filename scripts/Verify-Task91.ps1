param(
    [string]$BaseUrl = 'http://127.0.0.1:7304',
    [ValidateSet('Mock','MissingKey','InvalidConfig')][string]$Mode = 'Mock'
)
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '../WarehouseManagement.csproj'
$checks = 0
function Assert-Check($value, [string]$label) {
    if (!$value) { throw "FAIL: $label" }
    $script:checks++
    Write-Host "PASS: $label"
}
function Get-Page([string]$path, $session) {
    Invoke-WebRequest ($BaseUrl+$path) -WebSession $session -MaximumRedirection 0 -SkipHttpErrorCheck -ErrorAction SilentlyContinue
}
function Post-Page([hashtable]$body, $session) {
    Invoke-WebRequest "$BaseUrl/AiAnalysis/Generate" -Method Post -Body $body -WebSession $session -MaximumRedirection 0 -SkipHttpErrorCheck -ErrorAction SilentlyContinue
}
function Token([string]$html) {
    [regex]::Match($html,'name="__RequestVerificationToken"[^>]*value="([^"]+)"').Groups[1].Value
}
function Metric([string]$html, [string]$name) {
    [regex]::Match($html,('data-metric="'+$name+'">([^<]+)</dd>')).Groups[1].Value
}
$secrets=@{}
dotnet user-secrets list --project $project | ForEach-Object {
    $parts=$_ -split ' = ',2
    if ($parts.Count -eq 2) { $secrets[$parts[0]]=$parts[1] }
}
$anonymous=New-Object Microsoft.PowerShell.Commands.WebRequestSession
foreach ($response in @((Get-Page '/AiAnalysis/Index' $anonymous),(Post-Page @{} $anonymous))) {
    Assert-Check ($response.StatusCode -eq 302 -and $response.Headers.Location -match '/Account/Login') 'Anonymous AI GET/POST requires login'
}
foreach ($role in @('WarehouseStaff','Accountant')) {
    $session=New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $login=Get-Page '/Account/Login' $session
    $result=Invoke-WebRequest "$BaseUrl/Account/Login" -WebSession $session -Method Post -Body @{
        LoginIdentifier=$secrets["TestAccountSeed:${role}:UserName"]
        Password=$secrets["TestAccountSeed:${role}:Password"]
        __RequestVerificationToken=(Token $login.Content)
    } -MaximumRedirection 0 -SkipHttpErrorCheck -ErrorAction SilentlyContinue
    Assert-Check ($result.StatusCode -eq 302) "$role login"
    if ($role -eq 'WarehouseStaff') {
        foreach ($response in @((Get-Page '/AiAnalysis/Index' $session),(Post-Page @{} $session))) {
            Assert-Check ($response.StatusCode -eq 302 -and $response.Headers.Location -match '/Account/AccessDenied') 'Staff AI GET/POST denied by ViewReports'
        }
        continue
    }
    $page=Get-Page '/AiAnalysis/Index' $session
    Assert-Check ($page.StatusCode -eq 200) 'Accountant preview HTTP 200'
    Assert-Check ((Metric $page.Content 'import') -eq '44.125' -and (Metric $page.Content 'export') -eq '14.750' -and (Metric $page.Content 'current') -eq '29.375') 'Official preview totals equal SQL snapshot'
    Assert-Check ($page.Headers.'Cache-Control' -match 'no-store') 'Analysis page is not cached'
    Assert-Check (!$page.Content.Contains('name="ApiKey"') -and !$page.Content.Contains('name="Model"') -and !$page.Content.Contains('name="Provider"')) 'No client credential/provider form'
    foreach ($query in @('fromDate=bad','fromDate=2026-09-23&toDate=2026-09-22','toDate=9999-12-31')) {
        $invalid=Get-Page ("/AiAnalysis/Index?"+$query) $session
        Assert-Check ($invalid.StatusCode -eq 200 -and $invalid.Content.Contains('validation-summary-errors') -and !(Metric $invalid.Content 'import')) 'Invalid preview dates rejected'
    }
    $empty=Get-Page '/AiAnalysis/Index?fromDate=2026-09-23&toDate=2026-09-23' $session
    Assert-Check ((Metric $empty.Content 'import') -eq '0.000' -and (Metric $empty.Content 'export') -eq '0.000' -and (Metric $empty.Content 'opening') -eq '29.375') 'No movements period preserves opening stock'
    if ($Mode -ne 'Mock') {
        $expectedError = if ($Mode -eq 'MissingKey') { 'API key chưa được cấu hình' } else { 'Cấu hình AI không hợp lệ trên máy chủ' }
        $html=[Net.WebUtility]::HtmlDecode($page.Content)
        Assert-Check ($html.Contains($expectedError) -and !$html.Contains('id="ai-generate"')) 'Configuration error visible; generation button absent'
        # Login form token is valid for the same authenticated session after fetching it again.
        $tokenPage=Get-Page '/Reports/Index' $session
        $result=Post-Page @{__RequestVerificationToken=(Token $tokenPage.Content)} $session
        Assert-Check ($result.StatusCode -eq 200 -and [Net.WebUtility]::HtmlDecode($result.Content).Contains($expectedError) -and !$result.Content.Contains('ai-analysis-text') -and !$result.Content.Contains('not-a-number') -and !$result.Content.Contains('System.InvalidOperationException')) 'Direct POST with invalid configuration fails safely without raw values or stack trace'
        $old=Get-Page '/Reports/Dashboard' $session
        Assert-Check ($old.StatusCode -eq 200 -and $old.Content.Contains('44.125')) 'Invalid AI configuration does not break warehouse dashboard'
        continue
    }
    $token=Token $page.Content
    Assert-Check ($token.Length -gt 0) 'Generate form has antiforgery token'
    $withoutToken=Post-Page @{} $session
    Assert-Check ($withoutToken.StatusCode -eq 400) 'POST without antiforgery token rejected'
    $success=Post-Page @{__RequestVerificationToken=$token} $session
    $html=[Net.WebUtility]::HtmlDecode($success.Content)
    Assert-Check ($success.StatusCode -eq 200 -and $html.Contains('BẢN KIỂM THỬ MOCK') -and $html.Contains('ai-analysis-text')) 'End-to-end mock report succeeds and is labeled'
    Assert-Check ($html.Contains('44.125') -and $html.Contains('14.750') -and $html.Contains('29.375')) 'Mock narrative uses official totals'
    Assert-Check ($html.Contains('Tóm tắt biến động kho') -and (Metric $html 'net-change') -eq '29.375') 'Movement section retains official net change and full report'
    $belowCount=[regex]::Match($html,'data-metric="below-minimum">([^<]+)</strong>').Groups[1].Value
    Assert-Check ($html.Contains('Đề xuất nhập thêm hàng') -and $belowCount -eq '20' -and $html.Contains('không tự động tạo phiếu nhập hoặc thay đổi tồn kho')) 'Replenishment section has official count and advisory-only notice'
    $candidateTable=[regex]::Match($html,'id="replenishment-table".*?<tbody>(.*?)</tbody>','Singleline').Groups[1].Value
    Assert-Check ([regex]::Matches($candidateTable,'<tr>').Count -eq 20) 'All 20 replenishment candidates shown, not truncated to 10'
    $emptyResult=Post-Page @{fromDate='2026-09-23';toDate='2026-09-23';__RequestVerificationToken=$token} $session
    $html=[Net.WebUtility]::HtmlDecode($emptyResult.Content)
    Assert-Check ($emptyResult.StatusCode -eq 200 -and $html.Contains('Không có giao dịch nhập/xuất Posted trong kỳ') -and (Metric $html 'import') -eq '0.000') 'Mock states limitation for empty period'
    Assert-Check ($html.Contains('Không có biến động nhập/xuất Posted trong kỳ') -and (Metric $html 'net-change') -eq '0.000' -and (Metric $html 'current') -eq '29.375') 'Empty-period movement summary preserves current stock and zero net change'
    $invalidResult=Post-Page @{fromDate='bad';__RequestVerificationToken=$token} $session
    Assert-Check ($invalidResult.StatusCode -eq 200 -and $invalidResult.Content.Contains('validation-summary-errors') -and !$invalidResult.Content.Contains('ai-analysis-text')) 'Invalid POST dates cannot generate report'
    $forged=Post-Page @{ImportedQuantity='999999';CurrentQuantity='999999';Provider='OpenAI';ApiKey='form-input-must-be-ignored';__RequestVerificationToken=$token} $session
    $html=[Net.WebUtility]::HtmlDecode($forged.Content)
    Assert-Check ($forged.StatusCode -eq 200 -and (Metric $html 'import') -eq '44.125' -and !$html.Contains('999999') -and !$html.Contains('form-input-must-be-ignored') -and $html.Contains('BẢN KIỂM THỬ MOCK')) 'Forged totals/provider/key ignored; backend recalculates'
    $limited=Post-Page @{__RequestVerificationToken=$token} $session
    Assert-Check ($limited.StatusCode -eq 429) 'Sixth POST in window rate-limited'
}
Write-Output "Completed $checks HTTP checks ($Mode). ADMIN login and real provider remain unverified."
