$ErrorActionPreference = "Stop"

# Stop existing process if running
Get-Process | Where-Object { $_.ProcessName -like "*SystemPerformanceNotifierService*" } | Stop-Process -Force -ErrorAction SilentlyContinue

Write-Host "Building project..."
dotnet build -c Debug

$exePath = Join-Path $PWD "bin\Debug\net9.0-windows\win-x64\SystemPerformanceNotifierService.exe"

if (-not (Test-Path $exePath)) {
  Write-Error "Executable not found at $exePath"
  exit 1
}

Write-Host "Starting application with Administrator privileges..."
Write-Host "Please accept the UAC prompt."

# Use cmd /k to keep the window open if the application crashes
Start-Process -FilePath "cmd.exe" -Verb RunAs -ArgumentList "/k `"$exePath`" --environment Development"
