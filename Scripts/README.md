# Service Management Scripts

This folder contains a unified PowerShell script for managing the System Performance Notifier Service.

## Unified Management Script

**`service-manager.ps1`** - Complete service management solution with all functionality in one script.

## Quick Start

The unified script provides both interactive menu and command-line interfaces:

```powershell
# Interactive menu (default)
.\service-manager.ps1

# Or simply
.\service-manager.ps1 menu
```

This will show an interactive menu with all available operations and real-time status display.

## Command Line Usage

The script supports all operations via command-line parameters:

### Service Operations
```powershell
# Install service with auto-start
.\service-manager.ps1 install

# Install without building (use existing executable)
.\service-manager.ps1 install -SkipBuild

# Uninstall service completely
.\service-manager.ps1 uninstall

# Restart service
.\service-manager.ps1 restart
```

### Information & Monitoring
```powershell
# Basic service status
.\service-manager.ps1 status

# Detailed status with recent events
.\service-manager.ps1 status -Detailed

# View logs (last 24 hours)
.\service-manager.ps1 logs

# Live monitoring mode
.\service-manager.ps1 logs -Live

# View logs for specific time period
.\service-manager.ps1 logs -Hours 48
```

### Diagnostics
```powershell
# Full system diagnostics
.\service-manager.ps1 diagnostics

# Quick diagnostics (essential checks only)
.\service-manager.ps1 diagnostics -Quick

# Export diagnostic report
.\service-manager.ps1 diagnostics -ExportReport
```

### Help
```powershell
# Show help information
.\service-manager.ps1 help
```

## Interactive Menu Features

The interactive menu adapts based on the current service state:

### When Service is NOT Installed:
- Install Service
- Build Project Only
- Status & Diagnostics

### When Service is Installed:
- Restart/Start/Stop Service
- Reinstall Service
- Uninstall Service
- Live monitoring and logs

## Features

### Automatic Elevation
The script automatically requests administrator privileges when needed. You don't need to run PowerShell as Administrator manually.

### Comprehensive Error Handling
- Detailed error messages with suggested solutions
- Graceful failure handling with recovery options
- Timeout management for all operations

### Hardware Detection
- Automatic ESP32 device detection
- Serial port enumeration and validation
- USB device driver status checking

### Real-time Monitoring
- Live event monitoring with color-coded status
- Memory usage and performance tracking
- Service uptime and process information

### Smart Service Management
- Delayed auto-start configuration
- Automatic failure recovery setup
- Dependency management (network services)
- Proper service lifecycle handling

## Prerequisites

- **PowerShell 5.1 or later**
- **Administrator privileges** (requested automatically)
- **.NET 8.0 Runtime or later**
- **ESP32 device** connected via USB (for full functionality)

## Service Configuration

The installed service includes:
- **Auto-start**: Delayed automatic startup with Windows
- **Failure Recovery**: Automatic restart on crashes (3 attempts)
- **Dependencies**: Network services (Tcpip) for communication
- **Description**: User-friendly description in Services.msc

## Troubleshooting

### Service Won't Start
1. Run `.\service-manager.ps1 diagnostics` to identify issues
2. Check ESP32 USB connection
3. Verify .NET runtime installation
4. Review Windows Event Log for detailed errors

### Permission Errors
1. Script automatically requests administrator privileges
2. If elevation fails, run PowerShell as Administrator manually
3. Check Windows UAC settings

### ESP32 Not Detected
1. Connect ESP32 via USB with data cable
2. Install ESP32/CH340/CP210x drivers if needed
3. Check Device Manager for COM port assignments
4. Run `.\service-manager.ps1 status -Detailed` to verify detection

### Build Failures
1. Ensure .NET SDK is installed (not just runtime)
2. Check project file exists and is valid
3. Try manual build: `dotnet build SystemPerformanceNotifierService.csproj`

## Advanced Usage

### Automation & CI/CD
Use the `-Force` parameter to skip interactive prompts for automation:

```powershell
.\service-manager.ps1 install -Force -SkipBuild
.\service-manager.ps1 diagnostics -Force -Quick
```

### Scripted Operations
Chain operations for complex workflows:

```powershell
# Complete rebuild and reinstall
.\service-manager.ps1 uninstall -Force
.\service-manager.ps1 install -Force

# Health check with logging
.\service-manager.ps1 diagnostics -Quick -Force > "logs\health-check-$(Get-Date -Format 'yyyyMMdd').log"
```

## Migration from Previous Versions

If you're upgrading from a version with multiple scripts, the unified script provides all the same functionality:

### Command Mapping
```powershell
# Old approach → New unified approach
install-service.ps1           → service-manager.ps1 install
uninstall-service.ps1         → service-manager.ps1 uninstall  
restart-service.ps1           → service-manager.ps1 restart
service-status.ps1            → service-manager.ps1 status
view-logs.ps1                 → service-manager.ps1 logs
diagnostics.ps1               → service-manager.ps1 diagnostics
manage-service.ps1            → service-manager.ps1 (interactive menu)
```

### Benefits of Migration
- **Single file to maintain** instead of 7+ separate scripts
- **Enhanced error handling** and user guidance
- **Automatic privilege elevation** when needed
- **Live monitoring capabilities** for real-time troubleshooting
- **Context-sensitive menus** that adapt to service state
- **Comprehensive diagnostics** with export capabilities
- Dependency on TCP/IP services for proper network operation

### Error Handling
- Administrator privilege checking with automatic elevation
- Comprehensive error messages and troubleshooting guidance
- Graceful handling of missing dependencies and files

### Monitoring Capabilities
- Real-time service status checking
- ESP32 device detection and serial port monitoring
- Windows Event Log integration for detailed diagnostics
- System resource monitoring (CPU, memory usage)

## Requirements

- **PowerShell 5.1 or later** (Windows PowerShell or PowerShell Core)
- **Administrator privileges** for service installation/removal operations
- **.NET 8.0 Runtime** for building and running the service
- **ESP32 device** connected via USB for full functionality

## Troubleshooting

If you encounter issues:

1. **Run diagnostics first:**
   ```powershell
   .\diagnostics.ps1
   ```

2. **Check recent events:**
   ```powershell
   .\view-logs.ps1
   ```

3. **Verify service status:**
   ```powershell
   .\service-status.ps1 -Detailed
   ```

4. **Use management console for guided operations:**
   ```powershell
   .\manage-service.ps1
   ```

## Common Issues

### "Execution Policy" Error
```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

### "Access Denied" Error
- Right-click PowerShell and "Run as Administrator"
- Or use the management console which handles elevation automatically

### Service Won't Start
1. Run `.\diagnostics.ps1` to identify issues
2. Check if .NET runtime is installed: `dotnet --version`
3. Verify ESP32 is connected: `.\service-status.ps1`
4. Check Windows Event Log for detailed error messages

### ESP32 Not Detected
- Ensure ESP32 is connected via USB cable
- Install ESP32 USB drivers (CH340/CP210x)
- Check Device Manager for COM port assignment
- Run `.\service-status.ps1` to see detected devices

## Script Hierarchy

```
manage-service.ps1          # Main entry point (interactive menu)
├── install-service.ps1     # Service installation
├── uninstall-service.ps1   # Service removal
├── restart-service.ps1     # Service restart
├── service-status.ps1      # Status checking
├── view-logs.ps1          # Log viewing
└── diagnostics.ps1        # Health diagnostics
```

Each script can be run independently, but `manage-service.ps1` provides the most user-friendly experience with guided operations and comprehensive status information.
