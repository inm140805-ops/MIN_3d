function New-ResourcePolicyState { @{ CpuSince = -1.0; RamSince = -1.0; GpuSince = -1.0 } }
function Get-ResourceStopReason {
    param([hashtable]$State, [double]$Cpu, [double]$Ram, [double]$Gpu, [double]$FreeBytes, [double]$Seconds)
    foreach ($value in @($Cpu, $Ram, $Gpu, $FreeBytes)) {
        if ([double]::IsNaN($value) -or [double]::IsInfinity($value) -or $value -lt 0) { return 'Required resource metric unavailable' }
    }
    foreach ($entry in @(@('CpuSince',$Cpu,90), @('RamSince',$Ram,97), @('GpuSince',$Gpu,95))) {
        $key = $entry[0]
        if ($entry[1] -ge $entry[2]) { if ($State[$key] -lt 0) { $State[$key] = $Seconds } }
        else { $State[$key] = -1.0 }
    }
    if ($FreeBytes -lt 5GB) { return 'Free disk below 5 GB' }
    if ($State.CpuSince -ge 0 -and $Seconds - $State.CpuSince -ge 10) { return 'CPU >= 90% for 10 seconds' }
    if ($State.RamSince -ge 0 -and $Seconds - $State.RamSince -ge 10) { return 'RAM >= 97% for 10 seconds' }
    if ($State.GpuSince -ge 0 -and $Seconds - $State.GpuSince -ge 10) { return 'GPU >= 95% for 10 seconds' }
    return ''
}
