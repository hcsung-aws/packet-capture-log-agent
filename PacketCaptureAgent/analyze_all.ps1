# Usage: .\analyze_all.ps1 <protocol.json> <log_or_dir> [log_or_dir ...]
#   .\analyze_all.ps1 ..\protocols\mmorpg_simulator.json ..\captures\archive
#   .\analyze_all.ps1 ..\protocols\mmorpg_simulator.json capture1.log capture2.log

param(
    [Parameter(Mandatory)][string]$Protocol,
    [Parameter(Mandatory, ValueFromRemainingArguments)][string[]]$Paths
)

$exe = Join-Path $PSScriptRoot "bin\Release\net9.0\PacketCaptureAgent.exe"
if (-not (Test-Path $exe)) {
    $exe = Join-Path $PSScriptRoot "bin\Debug\net9.0\PacketCaptureAgent.exe"
}
$count = 0

foreach ($p in $Paths) {
    if (Test-Path $p -PathType Container) {
        Get-ChildItem $p -Filter "*.log" | ForEach-Object {
            $count++
            Write-Host "[$count] Analyzing $($_.FullName)"
            & $exe -p $Protocol --analyze $_.FullName
        }
    } else {
        $count++
        Write-Host "[$count] Analyzing $p"
        & $exe -p $Protocol --analyze $p
    }
}

Write-Host "`nDone: $count log(s) analyzed."
Write-Host "Run --build-behavior to regenerate BT."
