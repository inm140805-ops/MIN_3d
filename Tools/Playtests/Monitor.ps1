param([Parameter(Mandatory=$true)][string]$SessionDir, [int]$EditorPid, [string]$EditorStart)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\QAOutput\Playtests'))
$session = [IO.Path]::GetFullPath($SessionDir)
if (-not $session.StartsWith($root + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid QA directory' }
for ($item = Get-Item -LiteralPath $session; $null -ne $item; $item = $item.Parent) {
    if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Reparse point is not allowed' }
}
if ((Get-Content -LiteralPath (Join-Path $session '.qa-owned') -Raw) -ne 'LaundryQA-v1') { throw 'Unknown directory owner' }
$watch = [Diagnostics.Stopwatch]::StartNew()
. (Join-Path $PSScriptRoot 'ResourcePolicy.ps1')
$policyState = New-ResourcePolicyState
while ($watch.Elapsed.TotalSeconds -lt 360) {
    if (Test-Path -LiteralPath (Join-Path $session 'monitor.stop')) { break }
    try {
        $editor = Get-Process -Id $EditorPid -ErrorAction Stop
        if ($editor.StartTime.ToUniversalTime().ToString('o') -ne $EditorStart) { break }
        $cpuSample = Get-CimInstance Win32_PerfFormattedData_PerfOS_Processor -Filter "Name='_Total'"
        if ($null -eq $cpuSample) { throw 'CPU utilization unavailable' }
        $cpu = [double]$cpuSample.PercentProcessorTime
        $os = Get-CimInstance Win32_OperatingSystem
        $ram = 100.0 * (1.0 - $os.FreePhysicalMemory / $os.TotalVisibleMemorySize)
        $drive = [IO.DriveInfo]::new([IO.Path]::GetPathRoot($session))
        $gpu = -1.0
        $engines = @(Get-CimInstance Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine -ErrorAction SilentlyContinue)
        if ($engines.Count -gt 0) {
            # Sum clients sharing each physical engine, then use the busiest engine.
            $loads = $engines | Group-Object { $_.Name -replace '^pid_\d+_', '' } | ForEach-Object {
                [double](($_.Group | Measure-Object -Property UtilizationPercentage -Sum).Sum)
            }
            $gpu = [math]::Min(100.0, [double](($loads | Measure-Object -Maximum).Maximum))
        }
        $now = $watch.Elapsed.TotalSeconds
        $reason = Get-ResourceStopReason -State $policyState -Cpu $cpu -Ram $ram -Gpu $gpu -FreeBytes $drive.AvailableFreeSpace -Seconds $now
        if (-not $editor.Responding) { $reason = 'Editor is not responding; request stop, never terminate Editor' }
        $metrics = @{ utc=[DateTime]::UtcNow.ToString('o'); cpu=$cpu; ram=$ram; gpu=$gpu; freeBytes=$drive.AvailableFreeSpace; reason=$reason }
        $json = $metrics | ConvertTo-Json -Compress
        $temporary = Join-Path $session 'metrics.tmp'
        [IO.File]::WriteAllText($temporary, $json)
        Move-Item -LiteralPath $temporary -Destination (Join-Path $session 'metrics.json') -Force
        if ($reason) { [IO.File]::WriteAllText((Join-Path $session 'stop.request'), $reason); break }
    } catch {
        [IO.File]::WriteAllText((Join-Path $session 'stop.request'), ('Resource monitor failed: ' + $_.Exception.Message))
        break
    }
    Start-Sleep -Milliseconds 1500
}
