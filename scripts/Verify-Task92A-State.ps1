param([ValidateSet('Before','After')][string]$Phase = 'Before', [string]$EvidenceDirectory = '../bin/Task92AEvidence')
$ErrorActionPreference='Stop'
$directory=Join-Path $PSScriptRoot $EvidenceDirectory
$null=New-Item -ItemType Directory -Force $directory
$connection=[System.Data.SqlClient.SqlConnection]::new('Server=THI-DIEU;Database=WarehouseManagementDb;Integrated Security=True;TrustServerCertificate=True')
$snapshot=@{}
try {
    $connection.Open()
    $transaction=$connection.BeginTransaction([System.Data.IsolationLevel]::Serializable)
    foreach($table in @('Categories','Products','Suppliers','ImportReceipts','ImportReceiptDetails','ExportReceipts','ExportReceiptDetails','InventoryTransactions')) {
        $command=$connection.CreateCommand();$command.Transaction=$transaction
        $command.CommandText="SELECT (SELECT * FROM [$table] ORDER BY Id FOR JSON PATH, INCLUDE_NULL_VALUES)"
        $json=[string]$command.ExecuteScalar()
        $snapshot[$table]=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($json)))
    }
    $transaction.Commit()
} finally { $connection.Dispose() }
$path=Join-Path $directory 'business-before.json'
if($Phase -eq 'Before') { $snapshot|ConvertTo-Json|Set-Content $path; Write-Host 'Captured hashes for 8 business tables; no row data written.' }
else {
    $before=Get-Content $path -Raw|ConvertFrom-Json -AsHashtable
    foreach($table in $snapshot.Keys) { if($before[$table] -ne $snapshot[$table]) {throw "FAIL: business table changed: $table"};Write-Host "PASS: unchanged $table" }
    Write-Host 'Completed 8 business-state checks.'
}
