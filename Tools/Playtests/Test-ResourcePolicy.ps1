$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ResourcePolicy.ps1')
$checks = 0
function Assert-Result([bool]$Condition, [string]$Name) {
    if (-not $Condition) { throw "FAIL: $Name" }
    $script:checks++
}
foreach ($metric in @('Cpu','Ram','Gpu')) {
    $state = New-ResourcePolicyState
    $sample = @{State=$state;Cpu=20;Ram=70;Gpu=20;FreeBytes=10GB;Seconds=0}
    $sample[$metric] = 99
    Assert-Result ((Get-ResourceStopReason @sample) -eq '') "$metric transient allowed"
    $sample.Seconds=9.9; Assert-Result ((Get-ResourceStopReason @sample) -eq '') "$metric below duration"
    $sample.Seconds=10; Assert-Result ((Get-ResourceStopReason @sample) -ne '') "$metric sustained abort"
    $sample[$metric]=20; $sample.Seconds=11; Assert-Result ((Get-ResourceStopReason @sample) -eq '') "$metric recovery resets duration"
    $sample[$metric]=99; $sample.Seconds=12; Assert-Result ((Get-ResourceStopReason @sample) -eq '') "$metric new spike starts a new interval"
}
$state = New-ResourcePolicyState
Assert-Result ((Get-ResourceStopReason -State $state -Cpu 10 -Ram 40 -Gpu 20 -FreeBytes 4GB -Seconds 0) -ne '') 'Disk stop is immediate'
Assert-Result ((Get-ResourceStopReason -State $state -Cpu 10 -Ram 40 -Gpu -1 -FreeBytes 10GB -Seconds 0) -ne '') 'Missing GPU stops instead of treating unknown as idle'
Assert-Result ((Get-ResourceStopReason -State $state -Cpu ([double]::NaN) -Ram 40 -Gpu 20 -FreeBytes 10GB -Seconds 0) -ne '') 'Invalid CPU measurement stops'
Assert-Result ((Get-ResourceStopReason -State $state -Cpu 10 -Ram ([double]::PositiveInfinity) -Gpu 20 -FreeBytes 10GB -Seconds 0) -ne '') 'Invalid RAM measurement stops'
"PASS: $checks resource policy assertions"
