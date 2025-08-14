# System Monitor Service Manager
# Unified script for all service management operations
# Author: System Monitor Service Team
# Version: 2.0

[CmdletBinding()]
param(
  [Parameter(Position=0)]
  [ValidateSet('install', 'uninstall', 'restart', 'status', 'logs', 'diagnostics', 'menu', 'help')]
  [string]$Command = 'menu',
  
  [switch]$Force,
  [switch]$SkipBuild,
  [switch]$Detailed,
  [switch]$Live,
  [switch]$Quick,
  [switch]$ExportReport,
  [int]$Hours = 24
)

# Global variables
$script:ServiceName = "SystemMonitorService"
$script:ErrorCount = 0
$script:WarningCount = 0
$script:ProjectRoot = Split-Path $PSScriptRoot -Parent
$script:ProjectPath = Join-Path $script:ProjectRoot "SystemInfoMonitorService.csproj"
$script:ExePath = Join-Path $script:ProjectRoot "bin\Release\SystemInfoMonitorService.exe"

#region Utility Functions

function Test-Administrator {
  $currentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
  $principal = New-Object Security.Principal.WindowsPrincipal($currentUser)
  return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Request-Elevation {
  param($CommandArgs = "")
  
  if ($Force) {
    Write-Error "This operation requires administrator privileges. Run as administrator."
    exit 1
  }
  
  Write-Host "Requesting administrator privileges..." -ForegroundColor Yellow
  $arguments = "-ExecutionPolicy Bypass -File `"$PSCommandPath`" $CommandArgs -Force"
  Start-Process PowerShell -Verb RunAs -ArgumentList $arguments
  exit
}

function Write-Status {
  param($Message, $Level = "INFO")
  
  $color = switch ($Level) {
    "ERROR" { $script:ErrorCount++; "Red" }
    "WARN" { $script:WarningCount++; "Yellow" }  
    "SUCCESS" { "Green" }
    default { "White" }
  }
  
  $symbol = switch ($Level) {
    "ERROR" { "[X]" }
    "WARN" { "[!]" }
    "SUCCESS" { "[OK]" }
    default { "[i]" }
  }
  
  Write-Host "$symbol $Message" -ForegroundColor $color
}

function Get-ServiceInfo {
  return Get-Service -Name $script:ServiceName -ErrorAction SilentlyContinue
}

function Wait-ForKeyPress {
  param($Message = "Press any key to continue...")
  Write-Host ""
  Write-Host $Message -ForegroundColor Gray
  $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}

#endregion

#region Service Operations

function Install-SystemService {
  param([switch]$RequireAdmin = $true)
  
  if ($RequireAdmin -and -not (Test-Administrator)) {
    Request-Elevation "install $(if($SkipBuild){'-SkipBuild'})"
  }
  
  Write-Host "======================================================" -ForegroundColor Cyan
  Write-Host "  System Monitor Service Installer" -ForegroundColor Cyan  
  Write-Host "======================================================" -ForegroundColor Cyan
  Write-Host ""

  try {
    # Check .NET installation
    Write-Host "[CHECK] Verifying .NET installation..." -ForegroundColor Yellow
    try {
      $dotnetVersion = & dotnet --version 2>&1
      if ($LASTEXITCODE -eq 0) {
        Write-Status ".NET Version: $dotnetVersion" "SUCCESS"
      } else {
        throw "dotnet command failed"
      }
    } catch {
      Write-Status ".NET runtime not found! Install from: https://dotnet.microsoft.com/download" "ERROR"
      return $false
    }

    # Stop existing service
    Write-Host "[STEP 1/6] Stopping existing service..." -ForegroundColor Yellow
    $service = Get-ServiceInfo
    if ($service -and $service.Status -eq 'Running') {
      Stop-Service -Name $script:ServiceName -Force
      Write-Status "Service stopped" "SUCCESS"
      Start-Sleep 3
    } else {
      Write-Status "No running service found" "SUCCESS"
    }

    # Remove existing service  
    Write-Host "[STEP 2/6] Removing existing service..." -ForegroundColor Yellow
    if ($service) {
      & sc.exe delete $script:ServiceName | Out-Null
      Write-Status "Service removed" "SUCCESS"
    } else {
      Write-Status "No existing service to remove" "SUCCESS"
    }
    Start-Sleep 2

    # Build project
    if (-not $SkipBuild) {
      Write-Host "[STEP 3/6] Building project..." -ForegroundColor Yellow
      $outputPath = Join-Path $script:ProjectRoot "bin\Release"
      
      $buildResult = & dotnet publish $script:ProjectPath -c Release -o $outputPath --self-contained false 2>&1
      if ($LASTEXITCODE -ne 0) {
        Write-Status "Build failed: $buildResult" "ERROR"
        return $false
      }
      Write-Status "Build completed" "SUCCESS"
    } else {
      Write-Host "[STEP 3/6] Skipping build (using existing executable)..." -ForegroundColor Yellow
    }

    # Verify executable
    if (-not (Test-Path $script:ExePath)) {
      Write-Status "Service executable not found: $script:ExePath" "ERROR"
      return $false
    }

    # Create service
    Write-Host "[STEP 4/6] Installing service..." -ForegroundColor Yellow
    $createResult = & sc.exe create $script:ServiceName binPath= $script:ExePath start= delayed-auto depend= "Tcpip" 2>&1
    if ($LASTEXITCODE -ne 0) {
      # Fallback without dependencies
      $createResult = & sc.exe create $script:ServiceName binPath= $script:ExePath start= delayed-auto 2>&1
      if ($LASTEXITCODE -ne 0) {
        Write-Status "Service creation failed: $createResult" "ERROR"
        return $false
      }
    }
    Write-Status "Service installed" "SUCCESS"

    # Configure service
    Write-Host "[STEP 5/6] Configuring service..." -ForegroundColor Yellow
    & sc.exe description $script:ServiceName "System Info Monitor - Sends CPU/GPU/RAM data to ESP32. Auto-starts with Windows." | Out-Null
    & sc.exe failure $script:ServiceName reset= 86400 actions= restart/5000/restart/10000/restart/30000 | Out-Null
    Write-Status "Service configured (auto-start with failure recovery)" "SUCCESS"

    # Start service
    Write-Host "[STEP 6/6] Starting service..." -ForegroundColor Yellow
    & sc.exe start $script:ServiceName | Out-Null
    Start-Sleep 3

    # Verify installation
    $service = Get-ServiceInfo
    if ($service -and $service.Status -eq 'Running') {
      Write-Status "Service is running" "SUCCESS"
    } else {
      Write-Status "Service installed but may not be running" "WARN"
    }

    Write-Host ""
    Write-Host "======================================================" -ForegroundColor Green
    Write-Host "  Installation Complete!" -ForegroundColor Green
    Write-Host "======================================================" -ForegroundColor Green
    Write-Status "Service: $script:ServiceName" "SUCCESS"
    Write-Status "Auto-start: Enabled (delayed)" "SUCCESS"
    Write-Status "Failure recovery: Enabled" "SUCCESS"
    
    return $true

  } catch {
    Write-Status "Installation failed: $_" "ERROR"
    return $false
  }
}

function Uninstall-SystemService {
  if (-not (Test-Administrator)) {
    Request-Elevation "uninstall"
  }
  
  Write-Host "======================================================" -ForegroundColor Cyan
  Write-Host "  System Monitor Service Uninstaller" -ForegroundColor Cyan
  Write-Host "======================================================" -ForegroundColor Cyan
  Write-Host ""

  try {
    $service = Get-ServiceInfo
    if (-not $service) {
      Write-Status "$script:ServiceName is not installed" "SUCCESS"
      return $true
    }

    Write-Host "[STEP 1/3] Stopping service..." -ForegroundColor Yellow
    if ($service.Status -eq 'Running') {
      Stop-Service -Name $script:ServiceName -Force
      
      # Wait for service to stop
      $timeout = 0
      do {
        Start-Sleep 1
        $service = Get-ServiceInfo
        $timeout++
      } while ($service.Status -ne 'Stopped' -and $timeout -lt 10)
      
      if ($service.Status -eq 'Stopped') {
        Write-Status "Service stopped" "SUCCESS"
      } else {
        Write-Status "Service stop timeout (forcing removal)" "WARN"
      }
    } else {
      Write-Status "Service was not running" "SUCCESS"
    }

    Write-Host "[STEP 2/3] Removing service..." -ForegroundColor Yellow
    $result = & sc.exe delete $script:ServiceName 2>&1
    if ($LASTEXITCODE -eq 0) {
      Write-Status "Service removed successfully" "SUCCESS"
    } else {
      Write-Status "Service removal warning: $result" "WARN"
    }

    Start-Sleep 2

    Write-Host "[STEP 3/3] Verifying removal..." -ForegroundColor Yellow
    $service = Get-ServiceInfo
    if (-not $service) {
      Write-Status "Service successfully uninstalled" "SUCCESS"
    } else {
      Write-Status "Service may still be visible (Windows processing removal)" "WARN"
    }

    Write-Host ""
    Write-Host "======================================================" -ForegroundColor Green
    Write-Host "  Uninstallation Complete!" -ForegroundColor Green
    Write-Host "======================================================" -ForegroundColor Green
    
    return $true

  } catch {
    Write-Status "Uninstallation failed: $_" "ERROR"
    return $false
  }
}

function Restart-SystemService {
  if (-not (Test-Administrator)) {
    Request-Elevation "restart"
  }
  
  Write-Host "======================================================" -ForegroundColor Cyan
  Write-Host "  System Monitor Service Restart" -ForegroundColor Cyan
  Write-Host "======================================================" -ForegroundColor Cyan
  Write-Host ""

  try {
    $service = Get-ServiceInfo
    if (-not $service) {
      Write-Status "$script:ServiceName is not installed!" "ERROR"
      Write-Host "Run '$PSCommandPath install' to install the service first." -ForegroundColor Yellow
      return $false
    }

    Write-Host "Current Status: $($service.Status)" -ForegroundColor Cyan
    Write-Host ""

    # Stop the service
    Write-Host "[STEP 1/2] Stopping service..." -ForegroundColor Yellow
    if ($service.Status -eq 'Running') {
      Stop-Service -Name $script:ServiceName -Force
      
      # Wait for service to stop with timeout
      $timeout = 0
      do {
        Start-Sleep 1
        $service = Get-ServiceInfo
        $timeout++
        if ($timeout -le 10) {
          Write-Host "  Waiting for stop... ($timeout/10)" -ForegroundColor Gray
        }
      } while ($service.Status -ne 'Stopped' -and $timeout -lt 10)
      
      if ($service.Status -eq 'Stopped') {
        Write-Status "Service stopped successfully" "SUCCESS"
      } else {
        Write-Status "Service stop timeout - attempting restart anyway" "WARN"
      }
    } else {
      Write-Status "Service was not running" "SUCCESS"
    }

    # Start the service
    Write-Host "[STEP 2/2] Starting service..." -ForegroundColor Yellow
    Start-Service -Name $script:ServiceName
    
    # Wait for service to start
    Start-Sleep 3
    $service = Get-ServiceInfo
    
    if ($service.Status -eq 'Running') {
      Write-Status "Service started successfully" "SUCCESS"
      
      # Get process information
      try {
        $process = Get-Process -Name "SystemInfoMonitorService" -ErrorAction Stop
        Write-Status "Process ID: $($process.Id)" "SUCCESS"
        Write-Status "Memory Usage: $([math]::Round($process.WorkingSet64 / 1MB, 1)) MB" "SUCCESS"
      } catch {
        Write-Status "Process details unavailable" "WARN"
      }
    } else {
      Write-Status "Service restart failed - Status: $($service.Status)" "ERROR"
      Write-Host "Check Windows Event Log for details" -ForegroundColor Yellow
    }

    Write-Host ""
    Write-Host "======================================================" -ForegroundColor Green
    Write-Host "  Restart Complete!" -ForegroundColor Green
    Write-Host "======================================================" -ForegroundColor Green
    
    return $service.Status -eq 'Running'

  } catch {
    Write-Status "Restart failed: $_" "ERROR"
    return $false
  }
}

#endregion

#region Information Functions

function Show-ServiceStatus {
  Write-Host "======================================================" -ForegroundColor Cyan
  Write-Host "  System Monitor Service Status" -ForegroundColor Cyan
  Write-Host "======================================================" -ForegroundColor Cyan
  Write-Host ""

  $service = Get-ServiceInfo
  
  if (-not $service) {
    Write-Status "Service Status: NOT INSTALLED" "ERROR"
    Write-Host ""
    Write-Host "To install the service:" -ForegroundColor Yellow
    Write-Host "  $PSCommandPath install" -ForegroundColor White
    return $false
  }

  # Service status
  $statusColor = switch ($service.Status) {
    'Running' { 'Green' }
    'Stopped' { 'Red' }
    default { 'Yellow' }
  }
  Write-Host "[OK] Service Status: $($service.Status.ToString().ToUpper())" -ForegroundColor $statusColor

  # Get service configuration
  try {
    $config = & sc.exe qc $script:ServiceName 2>&1 | Out-String
    $startType = ($config | Select-String "START_TYPE").ToString().Split(":")[1].Trim()
    Write-Host "[OK] Start Type: $startType" -ForegroundColor Green
    
    # Check dependencies
    if ($config -match "DEPENDENCIES") {
      $deps = ($config | Select-String "DEPENDENCIES").ToString().Split(":")[1].Trim()
      Write-Host "[OK] Dependencies: $deps" -ForegroundColor Green
    }
  } catch {
    Write-Host "[!] Unable to get service configuration" -ForegroundColor Yellow
  }

  # Process information
  if ($service.Status -eq 'Running') {
    try {
      $process = Get-Process -Name "SystemInfoMonitorService" -ErrorAction Stop
      Write-Host "[OK] Process ID: $($process.Id)" -ForegroundColor Green
      Write-Host "[OK] Memory Usage: $([math]::Round($process.WorkingSet64 / 1MB, 1)) MB" -ForegroundColor Green
      Write-Host "[OK] CPU Time: $($process.TotalProcessorTime.ToString('hh\:mm\:ss'))" -ForegroundColor Green
      Write-Host "[OK] Start Time: $($process.StartTime)" -ForegroundColor Green
    } catch {
      Write-Host "[!] Process information unavailable" -ForegroundColor Yellow
    }
  }

  # ESP32 Device Detection
  Write-Host ""
  Write-Host "ESP32 Device Detection:" -ForegroundColor Cyan
  Write-Host "======================" -ForegroundColor Cyan
  
  try {
    $serialPorts = Get-WmiObject -Class Win32_SerialPort | Where-Object { 
      $_.Description -match "USB|CH340|CP210|ESP32|Arduino" 
    }
    
    if ($serialPorts) {
      foreach ($port in $serialPorts) {
        Write-Host "[OK] Found: $($port.Name) - $($port.Description)" -ForegroundColor Green
      }
    } else {
      Write-Host "[!] No ESP32/USB serial devices detected" -ForegroundColor Yellow
      Write-Host "  Make sure ESP32 is connected via USB" -ForegroundColor Gray
    }
  } catch {
    Write-Host "[X] Unable to check serial ports" -ForegroundColor Red
  }

  # Show recent events if detailed mode
  if ($Detailed) {
    Write-Host ""
    Write-Host "Recent Service Events:" -ForegroundColor Cyan
    Write-Host "=====================" -ForegroundColor Cyan
    
    try {
      $events = Get-WinEvent -FilterHashtable @{
        LogName = 'System'
        ProviderName = 'Service Control Manager'
        StartTime = (Get-Date).AddDays(-1)
      } -MaxEvents 10 -ErrorAction Stop | Where-Object {
        $_.Message -match $script:ServiceName
      }
      
      if ($events) {
        foreach ($event in $events) {
          $level = switch ($event.LevelDisplayName) {
            'Information' { 'Green' }
            'Warning' { 'Yellow' }
            'Error' { 'Red' }
            default { 'White' }
          }
          Write-Host "$($event.TimeCreated.ToString('MM-dd HH:mm:ss')) - $($event.LevelDisplayName): $($event.Message -replace $script:ServiceName, 'Service')" -ForegroundColor $level
        }
      } else {
        Write-Host "[OK] No recent service events (service is stable)" -ForegroundColor Green
      }
    } catch {
      Write-Host "[!] Unable to access event log" -ForegroundColor Yellow
    }
  }

  return $true
}

function Show-ServiceLogs {
  Write-Host "======================================================" -ForegroundColor Cyan
  Write-Host "  System Monitor Service Logs" -ForegroundColor Cyan
  if ($Live) {
    Write-Host "  Live Monitoring Mode" -ForegroundColor Yellow
  } else {
    Write-Host "  Last $Hours hours" -ForegroundColor Yellow
  }
  Write-Host "======================================================" -ForegroundColor Cyan

  if ($Live) {
    Write-Host ""
    Write-Host "Starting live monitoring (Press Ctrl+C to stop)..." -ForegroundColor Yellow
    Write-Host "Monitoring service events in real-time..." -ForegroundColor Gray
    Write-Host ""
    
    try {
      $lastEventTime = Get-Date
      while ($true) {
        Start-Sleep 5
        
        $newEvents = Get-WinEvent -FilterHashtable @{
          LogName = 'System'
          ProviderName = 'Service Control Manager'
          StartTime = $lastEventTime
        } -MaxEvents 10 -ErrorAction SilentlyContinue | Where-Object {
          $_.Message -match $script:ServiceName
        }
        
        foreach ($event in $newEvents) {
          $time = $event.TimeCreated.ToString('HH:mm:ss')
          $color = switch ($event.LevelDisplayName) {
            'Error' { 'Red' }
            'Warning' { 'Yellow' }
            default { 'Green' }
          }
          Write-Host "$time [LIVE] $($event.Message)" -ForegroundColor $color
        }
        
        $lastEventTime = Get-Date
      }
    } catch {
      Write-Host "Live monitoring stopped: $_" -ForegroundColor Yellow
    }
    return
  }

  # Show current service status
  $service = Get-ServiceInfo
  if ($service) {
    $statusColor = switch ($service.Status) {
      'Running' { 'Green' }
      'Stopped' { 'Red' }
      default { 'Yellow' }
    }
    Write-Host ""
    Write-Host "Current Status: $($service.Status.ToString().ToUpper())" -ForegroundColor $statusColor
    
    if ($service.Status -eq 'Running') {
      try {
        $process = Get-Process -Name "SystemInfoMonitorService" -ErrorAction Stop
        Write-Host "Memory Usage: $([math]::Round($process.WorkingSet64 / 1MB, 1)) MB" -ForegroundColor Green
        Write-Host "Start Time: $($process.StartTime)" -ForegroundColor Green
      } catch {}
    }
  } else {
    Write-Host ""
    Write-Host "Service is not installed" -ForegroundColor Red
  }

  # Show system events
  Write-Host ""
  Write-Host "System Events (Last $Hours hours):" -ForegroundColor Cyan
  Write-Host "=" * 35 -ForegroundColor Cyan
  
  try {
    $startTime = (Get-Date).AddHours(-$Hours)
    $events = Get-WinEvent -FilterHashtable @{
      LogName = 'System'
      StartTime = $startTime
    } -MaxEvents 50 -ErrorAction Stop | Where-Object {
      $_.Message -match $script:ServiceName -or
      ($_.ProviderName -eq 'Service Control Manager' -and $_.Message -match $script:ServiceName)
    } | Sort-Object TimeCreated -Descending
    
    if ($events) {
      foreach ($event in $events) {
        $color = switch ($event.LevelDisplayName) {
          'Error' { 'Red' }
          'Warning' { 'Yellow' }
          'Information' { 'Green' }
          default { 'White' }
        }
        
        $time = $event.TimeCreated.ToString('MM-dd HH:mm:ss')
        Write-Host "$time [$($event.LevelDisplayName.PadRight(11))] $($event.Message.Split("`n")[0])" -ForegroundColor $color
      }
    } else {
      Write-Host "✓ No relevant events found (service is stable)" -ForegroundColor Green
    }
  } catch {
    Write-Host "⚠ Unable to access event log: $_" -ForegroundColor Yellow
  }

  # Show serial ports
  Write-Host ""
  Write-Host "Serial Port Status:" -ForegroundColor Cyan
  Write-Host "==================" -ForegroundColor Cyan
  
  try {
    $ports = Get-WmiObject -Class Win32_SerialPort | Where-Object { 
      $_.Description -match "USB|CH340|CP210|ESP32|Arduino|Serial" 
    }
    
    if ($ports) {
      foreach ($port in $ports) {
        Write-Host "[OK] $($port.Name): $($port.Description)" -ForegroundColor Green
      }
    } else {
      Write-Host "[!] No USB serial devices detected" -ForegroundColor Yellow
    }
  } catch {
    Write-Host "[X] Unable to check serial ports: $_" -ForegroundColor Red
  }
}

function Start-SystemDiagnostics {
  Write-Host "======================================================" -ForegroundColor Cyan
  Write-Host "  System Monitor Service Diagnostics" -ForegroundColor Cyan
  if ($Quick) {
    Write-Host "  Quick Mode - Essential checks only" -ForegroundColor Yellow
  }
  Write-Host "======================================================" -ForegroundColor Cyan
  Write-Host ""

  $script:ErrorCount = 0
  $script:WarningCount = 0

  # Environment Check
  Write-Host "Environment Check:" -ForegroundColor Cyan
  Write-Host "=================" -ForegroundColor Cyan
  
  # .NET Runtime
  try {
    $dotnetVersion = & dotnet --version 2>&1
    if ($LASTEXITCODE -eq 0) {
      Write-Status ".NET Runtime: $dotnetVersion" "SUCCESS"
    } else {
      Write-Status ".NET Runtime: Not Found" "ERROR"
    }
  } catch {
    Write-Status ".NET Runtime: Not Found" "ERROR"
  }
  
  # Admin privileges
  if (Test-Administrator) {
    Write-Status "Admin Rights: Available" "SUCCESS"
  } else {
    Write-Status "Admin Rights: Not Running as Admin (some operations may fail)" "WARN"
  }
  
  # Project files
  if (Test-Path $script:ProjectPath) {
    Write-Status "Project File: Found" "SUCCESS"
  } else {
    Write-Status "Project File: Missing ($script:ProjectPath)" "ERROR"
  }
  
  # Service executable
  if (Test-Path $script:ExePath) {
    try {
      $version = (Get-Item $script:ExePath).VersionInfo.FileVersion
      Write-Status "Service Executable: Found (v$version)" "SUCCESS"
    } catch {
      Write-Status "Service Executable: Found" "SUCCESS"
    }
  } else {
    Write-Status "Service Executable: Missing (run build or install)" "WARN"
  }

  # Service Status Check
  Write-Host ""
  Write-Host "Service Status Check:" -ForegroundColor Cyan
  Write-Host "====================" -ForegroundColor Cyan
  
  $service = Get-ServiceInfo
  
  if (-not $service) {
    Write-Status "Installation: Not Installed" "ERROR"
  } else {
    Write-Status "Installation: Installed" "SUCCESS"
    
    switch ($service.Status) {
      'Running' { 
        Write-Status "Status: Running" "SUCCESS"
        
        try {
          $process = Get-Process -Name "SystemInfoMonitorService" -ErrorAction Stop
          Write-Status "Process ID: $($process.Id)" "SUCCESS"
          Write-Status "Memory Usage: $([math]::Round($process.WorkingSet64 / 1MB, 1)) MB" "SUCCESS"
        } catch {
          Write-Status "Process Info: Unavailable" "WARN"
        }
      }
      'Stopped' { Write-Status "Status: Stopped (use restart command)" "WARN" }
      default { Write-Status "Status: $($service.Status) (check configuration)" "WARN" }
    }
    
    # Service configuration
    try {
      $config = & sc.exe qc $script:ServiceName 2>&1 | Out-String
      if ($config -match "AUTO_START|DELAYED") {
        Write-Status "Auto-Start: Enabled" "SUCCESS"
      } else {
        Write-Status "Auto-Start: Not Configured" "WARN"
      }
    } catch {
      Write-Status "Configuration: Unable to check" "WARN"
    }
  }

  # Hardware Detection
  Write-Host ""
  Write-Host "Hardware Detection:" -ForegroundColor Cyan
  Write-Host "==================" -ForegroundColor Cyan
  
  try {
    $serialPorts = Get-WmiObject -Class Win32_SerialPort -ErrorAction Stop | Where-Object { 
      $_.Description -match "USB|CH340|CP210|ESP32|Arduino" 
    }
    
    if ($serialPorts) {
      foreach ($port in $serialPorts) {
        Write-Status "ESP32 Device: $($port.Name) ($($port.Description))" "SUCCESS"
      }
    } else {
      Write-Status "ESP32 Device: Not Detected (connect ESP32 via USB)" "WARN"
    }
  } catch {
    Write-Status "Serial Ports: Check Failed" "ERROR"
  }
  
  # System resources (if not quick mode)
  if (-not $Quick) {
    try {
      $cpu = Get-WmiObject -Class Win32_Processor | Measure-Object -Property LoadPercentage -Average
      $cpuUsage = [math]::Round($cpu.Average, 1)
      Write-Status "CPU Usage: $cpuUsage%" "SUCCESS"
      
      $memory = Get-WmiObject -Class Win32_OperatingSystem
      $memUsed = ($memory.TotalVisibleMemorySize - $memory.FreePhysicalMemory) / 1MB
      $memTotal = $memory.TotalVisibleMemorySize / 1MB
      $memPercent = [math]::Round(($memUsed / $memTotal) * 100, 1)
      Write-Status "Memory Usage: $memPercent% ($([math]::Round($memUsed, 1))/$([math]::Round($memTotal, 1)) GB)" "SUCCESS"
    } catch {
      Write-Status "Resource Check: Failed" "WARN"
    }
    
    # Dependencies Check
    Write-Host ""
    Write-Host "Dependencies Check:" -ForegroundColor Cyan
    Write-Host "==================" -ForegroundColor Cyan
    
    $requiredServices = @("Tcpip", "AFD")
    foreach ($svcName in $requiredServices) {
      try {
        $svc = Get-Service -Name $svcName -ErrorAction Stop
        if ($svc.Status -eq 'Running') {
          Write-Status "$svcName Service: Running" "SUCCESS"
        } else {
          Write-Status "$svcName Service: $($svc.Status) (may affect service startup)" "WARN"
        }
      } catch {
        Write-Status "$svcName Service: Not Found" "ERROR"
      }
    }
  }

  # Summary
  Write-Host ""
  Write-Host "======================================================" -ForegroundColor Cyan
  Write-Host "  Diagnostic Summary" -ForegroundColor Cyan
  Write-Host "======================================================" -ForegroundColor Cyan
  
  if ($script:ErrorCount -eq 0 -and $script:WarningCount -eq 0) {
    Write-Host "[SUCCESS] ALL CHECKS PASSED - System is healthy!" -ForegroundColor Green
  } else {
    if ($script:ErrorCount -gt 0) {
      Write-Host "[X] ERRORS FOUND: $script:ErrorCount" -ForegroundColor Red
    }
    if ($script:WarningCount -gt 0) {
      Write-Host "[!] WARNINGS: $script:WarningCount" -ForegroundColor Yellow  
    }
    
    Write-Host ""
    Write-Host "Recommended Actions:" -ForegroundColor Cyan
    if ($script:ErrorCount -gt 0) {
      Write-Host "1. Fix all errors listed above" -ForegroundColor White
      Write-Host "2. Run '$PSCommandPath install' as Administrator" -ForegroundColor White
      Write-Host "3. Re-run diagnostics to verify fixes" -ForegroundColor White
    }
    if ($script:WarningCount -gt 0) {
      Write-Host "1. Connect ESP32 device via USB if needed" -ForegroundColor White
      Write-Host "2. Check service status: '$PSCommandPath status'" -ForegroundColor White
    }
  }
}

#endregion

#region Menu System

function Show-MainMenu {
  Clear-Host
  Write-Host "======================================================" -ForegroundColor Cyan
  Write-Host "  System Monitor Service Manager v2.0" -ForegroundColor Cyan
  Write-Host "  Unified Management Console" -ForegroundColor Cyan
  Write-Host "======================================================" -ForegroundColor Cyan
  Write-Host ""
  
  # Show current status
  $service = Get-ServiceInfo
  if ($service) {
    $statusColor = switch ($service.Status) {
      'Running' { 'Green' }
      'Stopped' { 'Red' }
      default { 'Yellow' }
    }
    Write-Host "Current Status: $($service.Status.ToString().ToUpper())" -ForegroundColor $statusColor
    
    if ($service.Status -eq 'Running') {
      try {
        $process = Get-Process -Name "SystemInfoMonitorService" -ErrorAction Stop
        Write-Host "Memory Usage: $([math]::Round($process.WorkingSet64 / 1MB, 1)) MB" -ForegroundColor Green
        Write-Host "Uptime: $(((Get-Date) - $process.StartTime).ToString('d\.hh\:mm\:ss'))" -ForegroundColor Green
      } catch {}
    }
  } else {
    Write-Host "Current Status: NOT INSTALLED" -ForegroundColor Red
  }
  
  Write-Host ""
  Write-Host "Available Commands:" -ForegroundColor Yellow
  Write-Host ""
  
  if (-not $service) {
    Write-Host "  [1] Install Service" -ForegroundColor White
    Write-Host "  [2] Build Project Only" -ForegroundColor White
  } else {
    Write-Host "  [1] Restart Service" -ForegroundColor White
    Write-Host "  [2] Stop Service" -ForegroundColor White
    Write-Host "  [3] Start Service" -ForegroundColor White
    Write-Host "  [4] Reinstall Service" -ForegroundColor White
    Write-Host "  [5] Uninstall Service" -ForegroundColor White
  }
  
  Write-Host ""
  Write-Host "Information & Diagnostics:" -ForegroundColor Yellow
  Write-Host "  [S] Service Status (detailed)" -ForegroundColor White
  Write-Host "  [L] View Logs" -ForegroundColor White
  Write-Host "  [D] Run Diagnostics" -ForegroundColor White
  Write-Host "  [M] Live Monitoring" -ForegroundColor White
  
  Write-Host ""
  Write-Host "Other Options:" -ForegroundColor Yellow
  Write-Host "  [H] Help & Commands" -ForegroundColor White
  Write-Host "  [Q] Quit" -ForegroundColor White
  
  Write-Host ""
}

function Show-HelpInfo {
  Clear-Host
  Write-Host "======================================================" -ForegroundColor Cyan
  Write-Host "  System Monitor Service Manager Help" -ForegroundColor Cyan
  Write-Host "======================================================" -ForegroundColor Cyan
  Write-Host ""
  
  Write-Host "Command Line Usage:" -ForegroundColor Yellow
  Write-Host "  $PSCommandPath <command> [options]" -ForegroundColor White
  Write-Host ""
  
  Write-Host "Commands:" -ForegroundColor Yellow
  Write-Host "  install       Install service with auto-start" -ForegroundColor White
  Write-Host "  uninstall     Remove service completely" -ForegroundColor White
  Write-Host "  restart       Restart the service" -ForegroundColor White
  Write-Host "  status        Show detailed service status" -ForegroundColor White
  Write-Host "  logs          View service logs and events" -ForegroundColor White
  Write-Host "  diagnostics   Run comprehensive system diagnostics" -ForegroundColor White
  Write-Host "  menu          Show interactive menu (default)" -ForegroundColor White
  Write-Host "  help          Show this help information" -ForegroundColor White
  Write-Host ""
  
  Write-Host "Options:" -ForegroundColor Yellow
  Write-Host "  -SkipBuild    Skip building when installing" -ForegroundColor White
  Write-Host "  -Detailed     Show detailed information" -ForegroundColor White
  Write-Host "  -Live         Live monitoring mode" -ForegroundColor White
  Write-Host "  -Quick        Quick diagnostics mode" -ForegroundColor White
  Write-Host "  -Hours <n>    Show logs for last n hours (default: 24)" -ForegroundColor White
  Write-Host "  -Force        Force operation without prompts" -ForegroundColor White
  Write-Host ""
  
  Write-Host "Examples:" -ForegroundColor Yellow
  Write-Host "  $PSCommandPath install" -ForegroundColor Green
  Write-Host "  $PSCommandPath status -Detailed" -ForegroundColor Green
  Write-Host "  $PSCommandPath logs -Live" -ForegroundColor Green
  Write-Host "  $PSCommandPath diagnostics -Quick" -ForegroundColor Green
  Write-Host ""
  
  Write-Host "Service Features:" -ForegroundColor Yellow
  Write-Host "  [OK] Auto-start with Windows (delayed start)" -ForegroundColor Green
  Write-Host "  [OK] Automatic restart on failure" -ForegroundColor Green
  Write-Host "  [OK] ESP32 device auto-detection" -ForegroundColor Green
  Write-Host "  [OK] Real-time system monitoring (CPU, GPU, RAM)" -ForegroundColor Green
  Write-Host "  [OK] JSON data transmission to ESP32" -ForegroundColor Green
  
  Write-Host ""
  Write-Host "Requirements:" -ForegroundColor Yellow
  Write-Host "  - .NET 8.0 Runtime or later" -ForegroundColor White
  Write-Host "  - Administrator privileges (for service operations)" -ForegroundColor White
  Write-Host "  - ESP32 connected via USB" -ForegroundColor White
  
  if ($Command -eq 'menu') {
    Write-Host ""
    Write-Host "Press any key to return to menu..." -ForegroundColor Gray
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
  }
}

function Start-InteractiveMenu {
  while ($true) {
    Show-MainMenu
    Write-Host "Select option: " -ForegroundColor Cyan -NoNewline
    $choice = Read-Host
    $choice = $choice.ToUpper()
    
    $service = Get-ServiceInfo
    
    switch ($choice) {
      "1" {
        if (-not $service) {
          Install-SystemService
        } else {
          Restart-SystemService
        }
        Wait-ForKeyPress
      }
      
      "2" {
        if (-not $service) {
          Write-Host ""
          Write-Host "Building project..." -ForegroundColor Yellow
          & dotnet publish $script:ProjectPath -c Release -o (Join-Path $script:ProjectRoot "bin\Release") --self-contained false
          if ($LASTEXITCODE -eq 0) {
            Write-Status "Build completed successfully" "SUCCESS"
          } else {
            Write-Status "Build failed" "ERROR"
          }
        } else {
          Write-Host ""
          Write-Host "Stopping service..." -ForegroundColor Yellow
          Stop-Service -Name $script:ServiceName -Force
          Write-Status "Service stopped" "SUCCESS"
        }
        Wait-ForKeyPress
      }
      
      "3" {
        if ($service) {
          Write-Host ""
          Write-Host "Starting service..." -ForegroundColor Yellow
          Start-Service -Name $script:ServiceName
          Write-Status "Service started" "SUCCESS"
          Wait-ForKeyPress
        }
      }
      
      "4" {
        if ($service) {
          Install-SystemService
          Wait-ForKeyPress
        }
      }
      
      "5" {
        if ($service) {
          Uninstall-SystemService
          Wait-ForKeyPress
        }
      }
      
      "S" { 
        Show-ServiceStatus
        if ($service) {
          Write-Host ""
          Write-Host "Run '$PSCommandPath status -Detailed' for more information" -ForegroundColor Gray
        }
        Wait-ForKeyPress
      }
      
      "L" { 
        Show-ServiceLogs
        Write-Host ""
        Write-Host "Run '$PSCommandPath logs -Live' for real-time monitoring" -ForegroundColor Gray
        Wait-ForKeyPress
      }
      
      "D" { 
        Start-SystemDiagnostics
        Write-Host ""
        Write-Host "Run '$PSCommandPath diagnostics -Quick' for faster checks" -ForegroundColor Gray
        Wait-ForKeyPress
      }
      
      "M" {
        Write-Host ""
        Write-Host "Starting live monitoring (Press Ctrl+C to stop)..." -ForegroundColor Yellow
        Show-ServiceLogs
      }
      
      "H" { Show-HelpInfo }
      
      "Q" {
        Write-Host ""
        Write-Host "Goodbye!" -ForegroundColor Green
        exit 0
      }
      
      default {
        Write-Host ""
        Write-Host "Invalid option. Please try again." -ForegroundColor Red
        Start-Sleep 2
      }
    }
  }
}

#endregion

#region Main Execution

# Main execution logic
switch ($Command.ToLower()) {
  'install' {
    $result = Install-SystemService
    if ($Force -and $result) {
      Wait-ForKeyPress
    }
  }
  
  'uninstall' {
    $result = Uninstall-SystemService
    if ($Force -and $result) {
      Wait-ForKeyPress
    }
  }
  
  'restart' {
    $result = Restart-SystemService
    if ($Force -and $result) {
      Wait-ForKeyPress
    }
  }
  
  'status' {
    Show-ServiceStatus
    if ($Force) {
      Wait-ForKeyPress
    }
  }
  
  'logs' {
    Show-ServiceLogs
    if ($Force -and -not $Live) {
      Wait-ForKeyPress
    }
  }
  
  'diagnostics' {
    Start-SystemDiagnostics
    if ($Force) {
      Wait-ForKeyPress
    }
  }
  
  'help' {
    Show-HelpInfo
  }
  
  'menu' {
    Start-InteractiveMenu
  }
  
  default {
    Start-InteractiveMenu
  }
}

#endregion
