param([string]$BaseUrl='http://127.0.0.1:7323', [switch]$Format, [switch]$Security,
    [string]$EvidenceDirectory='../bin/Task92DEvidence')
$ErrorActionPreference='Stop'
$checks=0
function Check($ok,[string]$label) { if(!$ok){throw "FAIL: $label"};$script:checks++;Write-Host "PASS: $label" }
function Token($html) { [regex]::Match($html,'name="__RequestVerificationToken"[^>]*value="([^"]+)"').Groups[1].Value }
function Metric($html,$name) { [regex]::Match($html,('data-metric="'+$name+'">([^<]+)</dd>')).Groups[1].Value }
$secrets=@{}
dotnet user-secrets list --project (Join-Path $PSScriptRoot '../WarehouseManagement.csproj') | ForEach-Object {
    $parts=$_ -split ' = ',2; if($parts.Count -eq 2){$secrets[$parts[0]]=$parts[1]}
}
$session=[Microsoft.PowerShell.Commands.WebRequestSession]::new()
$login=Invoke-WebRequest "$BaseUrl/Account/Login" -WebSession $session
$signed=Invoke-WebRequest "$BaseUrl/Account/Login" -WebSession $session -Method Post -Body @{
    LoginIdentifier=$secrets['TestAccountSeed:Accountant:UserName'];Password=$secrets['TestAccountSeed:Accountant:Password'];__RequestVerificationToken=(Token $login.Content)
} -MaximumRedirection 0 -SkipHttpErrorCheck -ErrorAction SilentlyContinue
$accountName=$secrets['TestAccountSeed:Accountant:UserName']
$accountPassword=$secrets['TestAccountSeed:Accountant:Password']
$secrets.Clear()
Check ($signed.StatusCode -eq 302) 'Existing Accountant authenticated; no accounts created'
$page=Invoke-WebRequest "$BaseUrl/AiAnalysis" -WebSession $session
$csrf=Token $page.Content
Check ($csrf.Length -gt 0) 'Real MVC antiforgery form available'
$cases=@('Success','Timeout','400','401','403','404','429','500','502','503','Network','IO','Socket','InvalidKey','Unexpected','Empty','MaxTokens')
if($Format){$cases=@('ValidJson','InvalidJson','MissingRequiredField','WrongPartsType','WrongCandidatesType','NullText','NullResponse','EmptyResponse','WhitespaceResponse','PlainText','MarkdownJson','TruncatedJson','Incomplete','WrongNumberType','DuplicateField','MixedToolPart','WrongThoughtType','ValidSplitText','EmptyObject','NumericText')}
if($Security){$cases=@('KeyResponse','PasswordResponse','AccountResponse','CookieResponse','HeaderResponse');$Format=$true}
for($i=0;$i -lt $cases.Count;$i++) {
    # Keep the application's actual five-per-minute limiter. No policy override in test host.
    if($i -gt 0 -and $i%5 -eq 0){Write-Host 'Waiting for actual rate-limit window (two 31-second waits).';Start-Sleep 31;Start-Sleep 31}
    $case=$cases[$i]
    $body=@{__RequestVerificationToken=$csrf}
    if($i%2 -eq 1){$body.fromDate='2026-09-23';$body.toDate='2026-09-25'}
    $response=Invoke-WebRequest "$BaseUrl/AiAnalysis/Generate" -WebSession $session -Method Post -Body $body -SkipHttpErrorCheck
    $html=[Net.WebUtility]::HtmlDecode($response.Content)
    Check ($response.StatusCode -eq 200) "$case returns MVC HTTP 200 without crash"
    $expectedImport=if($i%2 -eq 1){'0.000'}else{'44.125'}
    $expectedExport=if($i%2 -eq 1){'0.000'}else{'14.750'}
    Check ((Metric $html 'import') -eq $expectedImport -and (Metric $html 'export') -eq $expectedExport -and (Metric $html 'current') -eq '29.375') "$case preserves official SQL totals"
    $table=[regex]::Match($html,'id="replenishment-table".*?<tbody>(.*?)</tbody>','Singleline').Groups[1].Value
    $count=[regex]::Match($html,'data-metric="below-minimum">([^<]+)</strong>').Groups[1].Value
    Check ($count -eq '20' -and [regex]::Matches($table,'<tr>').Count -eq 20) "$case retains all replenishment rows and strong selector"
    Check ($html -notmatch 'ui-fixture-not-a-real-key|x-goog-api-key|Authorization|provider-private-body|private-fixture-|private@example.invalid|System\.(Net|IO)|StackTrace|TaskCanceledException|HttpRequestException|SocketException|partial-must-not-display') "$case HTML contains no fixture secret, headers, raw body or stack trace"
    if($Security){Check ((!$accountName -or !$html.Contains($accountName)) -and (!$accountPassword -or !$html.Contains($accountPassword))) "$case HTML excludes actual account name and password"}
    if($Format) {
        if($case -in 'ValidJson','ValidSplitText') {
            Check ($html.Contains('ai-analysis-text') -and $html.Contains('format-fixture-report')) "$case accepted report rendered"
        }else{
            Check (!$html.Contains('ai-analysis-text') -and !$html.Contains('format-fixture-report') -and $html.Contains('AI chưa thể tạo phần phân tích')) "$case invalid report withheld; friendly error shown"
        }
        & (Join-Path $PSScriptRoot 'Verify-Task92A-State.ps1') -Phase After -EvidenceDirectory $EvidenceDirectory
        $script:checks+=8
    } elseif($case -eq 'Success') {
        Check ($html.Contains('ai-analysis-text') -and $html.Contains('Báo cáo fixture Gemini HTTP')) 'Success renders accepted Gemini adapter response (simulated transport)'
    } else {
        $message=switch($case){
            '404' {'Mô hình AI hiện không khả dụng. Vui lòng thử lại sau.'}
            '429' {'Dịch vụ AI đang bị giới hạn yêu cầu. Vui lòng thử lại sau.'}
            {$_ -in '400','500','502','503'} {'Dịch vụ AI hiện không khả dụng. Vui lòng thử lại sau.'}
            'Empty' {'Gemini trả về nội dung rỗng hoặc không đúng định dạng.'}
            'MaxTokens' {'MAX_TOKENS'}
            default {'Không thể kết nối dịch vụ AI. Vui lòng thử lại sau.'}
        }
        Check ($html.Contains($message) -and !$html.Contains('ai-analysis-text') -and $html.Contains('AI chưa thể tạo phần phân tích')) "$case friendly error; no report rendered"
    }
}
Write-Host "Completed $checks HTTP/UI/state checks; $($cases.Count) simulated Gemini scenarios, no real provider call."
