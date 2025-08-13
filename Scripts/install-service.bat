@echo off
echo Installing System Monitor Service for ESP32...

REM Stop service if it exists
sc stop SystemMonitorService >nul 2>&1

REM Delete service if it exists
sc delete SystemMonitorService >nul 2>&1

REM Build the project
echo Building project...
dotnet publish ..\SystemInfoMonitorService.csproj -c Release -o ..\bin\Release

REM Create the service
sc create SystemInfoMonitorService binPath= "%~dp0..\bin\Release\SystemInfoMonitorService.exe" start= auto
sc description SystemMonitorService "Sends system information (CPU, GPU, RAM) to ESP32 via USB/Serial connection"

REM Start the service
sc start SystemMonitorService

echo.
echo Service installed and started successfully!
echo.
echo Next steps:
echo 1. Connect your ESP32 to a USB port
echo 2. Upload the ESP32Code/SystemMonitor.ino to your ESP32
echo 3. Connect an I2C LCD (20x4) to your ESP32
echo 4. The service will auto-detect the ESP32 COM port
echo.
pause