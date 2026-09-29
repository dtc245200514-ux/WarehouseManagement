param([string]$BaseUrl='http://127.0.0.1:7324')
$ErrorActionPreference='Stop'
$secrets=@{}
dotnet user-secrets list --project (Join-Path $PSScriptRoot '../WarehouseManagement.csproj') | ForEach-Object {
    $parts=$_ -split ' = ',2;if($parts.Count -eq 2){$secrets[$parts[0]]=$parts[1]}
}
$session=[Microsoft.PowerShell.Commands.WebRequestSession]::new()
$login=Invoke-WebRequest "$BaseUrl/Account/Login" -WebSession $session
$token=[regex]::Match($login.Content,'name="__RequestVerificationToken"[^>]*value="([^"]+)"').Groups[1].Value
$signed=Invoke-WebRequest "$BaseUrl/Account/Login" -WebSession $session -Method Post -Body @{
    LoginIdentifier=$secrets['TestAccountSeed:Accountant:UserName'];Password=$secrets['TestAccountSeed:Accountant:Password'];__RequestVerificationToken=$token
} -MaximumRedirection 0 -SkipHttpErrorCheck -ErrorAction SilentlyContinue
$secrets.Clear()
if($signed.StatusCode -ne 302){throw 'Login failed'}
$page=Invoke-WebRequest "$BaseUrl/AiAnalysis" -WebSession $session
$table=[regex]::Match($page.Content,'id="replenishment-table".*?<tbody>(.*?)</tbody>','Singleline').Groups[1].Value
$rows=@{}
foreach($row in [regex]::Matches($table,'<tr>(.*?)</tr>','Singleline')) {
    $cells=@([regex]::Matches($row.Groups[1].Value,'<td>(.*?)</td>','Singleline') | ForEach-Object{[Net.WebUtility]::HtmlDecode($_.Groups[1].Value)})
    if($cells.Count -ne 6 -or $rows.ContainsKey($cells[0])){throw 'Invalid or duplicate UI row'}
    $rows[$cells[0]]=$cells
}
$connection=[System.Data.SqlClient.SqlConnection]::new('Server=THI-DIEU;Database=WarehouseManagementDb;Integrated Security=True;TrustServerCertificate=True')
try {
    $connection.Open();$command=$connection.CreateCommand()
    $command.CommandText='SELECT (SELECT Code,Name,Unit,CurrentQuantity,MinimumStockLevel,MinimumStockLevel-CurrentQuantity AS Gap FROM Products WHERE IsActive=1 AND CurrentQuantity<MinimumStockLevel ORDER BY Code FOR JSON PATH)'
    $expected=@(([string]$command.ExecuteScalar()) | ConvertFrom-Json)
}finally{$connection.Dispose()}
if($rows.Count -ne $expected.Count){throw 'UI count differs from SQL'}
Write-Host 'PASS: UI includes exactly the SQL eligible set'
foreach($product in $expected) {
    $cells=$rows[$product.Code]
    if(!$cells -or $cells[1] -cne $product.Name -or $cells[2] -cne $product.Unit){throw 'UI identity/unit differs from SQL'}
    $values=@($product.CurrentQuantity,$product.MinimumStockLevel,$product.Gap)
    for($i=0;$i -lt 3;$i++) {
        $actual=[decimal]::Parse($cells[$i+3],[Globalization.CultureInfo]::InvariantCulture)
        if($actual -ne $values[$i]){throw 'UI quantity/threshold/gap differs from SQL'}
    }
    Write-Host 'PASS: UI product code/name/unit/current/minimum/gap match SQL'
}
Write-Host "Completed $($expected.Count+1) replenishment UI/SQL checks; read only."
