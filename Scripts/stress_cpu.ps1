$numberOfCores = $env:NUMBER_OF_PROCESSORS
Write-Host "Starting stress test on $numberOfCores cores for 30 seconds..."

$jobs = @()
for ($i = 0; $i -lt $numberOfCores; $i++) {
  $jobs += Start-Job -ScriptBlock {
    $result = 0
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    while ($sw.Elapsed.TotalSeconds -lt 30) {
      # Simple arithmetic to consume CPU cycles
      $result = [Math]::Sqrt($result + 1)
    }
  }
}

Write-Host "Stress test running. Check your display for temperature increase."
Write-Host "Press Ctrl+C to stop early."

Wait-Job -Job $jobs
Receive-Job -Job $jobs | Out-Null
Remove-Job -Job $jobs
Write-Host "Stress test completed."