# System Performance Notifier Service for ESP32

> **Latest Update**: Service management has been simplified with a unified PowerShell script (`Scripts/service-manager.ps1`) that consolidates all operations into a single, user-friendly interface with both interactive menu and command-line support.

A Windows service that collects and sends system information (CPU, GPU, RAM usage and temperatures) to an ESP32 development board via USB/Serial connection. Perfect for creating custom PC monitoring displays with LCD screens.

## Table of Contents

1. [Features](#features)
2. [System Requirements](#system-requirements)
3. [Hardware Setup](#hardware-setup)
4. [Quick Start](#quick-start)
5. [Service Management](#service-management)
6. [ESP32 Arduino Code](#esp32-arduino-code)
7. [Data Format](#data-format)
8. [Troubleshooting](#troubleshooting)
9. [Project Structure](#project-structure)
10. [Script Management](#script-management)
11. [Building from Source](#building-from-source)

## Features

- **Real-time System Monitoring**: CPU usage, GPU usage, temperatures, and RAM usage
- **USB/Serial Communication**: Automatic ESP32 detection and connection
- **ESP32 LCD Display**: Complete Arduino code for 20x4 LCD display
- **Windows Service**: Runs in background, starts automatically with Windows
- **Auto-reconnection**: Handles USB disconnection/reconnection gracefully
- **Compact JSON Format**: Optimized for ESP32 memory constraints
- **Progress Bars**: Visual LCD progress bars for usage percentages

## System Requirements

### Windows PC
- Windows 10/11 or Windows Server 2019+
- .NET 8.0 Runtime
- Administrator privileges for service installation
- Available USB port for ESP32

### ESP32 Hardware
- ESP32-S3-WROOM-1 ESP32-8048S050 development board with integrated display
- USB cable (USB-A to USB-C)
- No additional wiring required (integrated display)

## Hardware Setup

### ESP32-S3 Display Interface
The ESP32-S3-WROOM-1 ESP32-8048S050 features an integrated display that connects via internal interface (no external wiring required).

## Quick Start

### 1. Setup Windows Service

#### Install Dependencies
```bash
# Clone or create the project
git clone https://github.com/icefox0801/SystemPerformanceNotifierService
cd SystemPerformanceNotifierService

# Restore NuGet packages
dotnet restore
```

#### Build and Install Service
```powershell
# Build the project
dotnet build -c Release

# Install service using the unified management script (recommended)
Scripts\service-manager.ps1 install

# Or use the interactive management console
Scripts\service-manager.ps1

# Alternative: Manual installation commands (run as Administrator)
dotnet publish SystemPerformanceNotifierService.csproj -c Release -o bin\Release
sc create SystemPerformanceNotifier binPath="bin\Release\SystemPerformanceNotifierService.exe" start=delayed-auto
sc description SystemPerformanceNotifier "System Performance Notifier Service - Monitors and transmits system performance data"
sc failure SystemPerformanceNotifier reset=86400 actions=restart/5000/restart/10000/restart/30000
sc start SystemPerformanceNotifier
```

## Service Management

The service is managed through a **unified PowerShell script** located in the `Scripts` folder:

### Interactive Management (Recommended)
```powershell
# Launch the interactive management console
cd Scripts
.\service-manager.ps1
```

This opens an interactive menu system with:
- Real-time service status display
- Context-sensitive menu options
- Guided troubleshooting
- Live monitoring capabilities

### Command Line Operations
```powershell
# Service lifecycle management
.\service-manager.ps1 install      # Install service with auto-start
.\service-manager.ps1 uninstall    # Remove service completely
.\service-manager.ps1 restart      # Restart the service
.\service-manager.ps1 status       # Show detailed service status

# Monitoring and diagnostics  
.\service-manager.ps1 logs          # View service logs (last 24 hours)
.\service-manager.ps1 logs -Live    # Live event monitoring
.\service-manager.ps1 diagnostics   # Run comprehensive system checks
.\service-manager.ps1 diagnostics -Quick  # Quick essential checks only

# Help and information
.\service-manager.ps1 help          # Show all available commands and options
```

### Advanced Options
```powershell
# Installation options
.\service-manager.ps1 install -SkipBuild    # Use existing build, skip compilation
.\service-manager.ps1 install -Force       # Skip interactive prompts

# Monitoring options  
.\service-manager.ps1 status -Detailed     # Include recent events and hardware info
.\service-manager.ps1 logs -Hours 48       # View logs from last 48 hours
.\service-manager.ps1 diagnostics -ExportReport  # Export diagnostics to JSON
```

### Service Features
The unified management script provides:
- **Automatic elevation** - Requests admin privileges when needed
- **ESP32 auto-detection** - Automatically finds connected ESP32 devices
- **Comprehensive diagnostics** - System health checks and troubleshooting
- **Live monitoring** - Real-time event and log monitoring  
- **Error recovery** - Automatic service restart on failure
- **Smart configuration** - Delayed startup and dependency management

#### Auto-Start Configuration
The service is configured to:
- ✅ **Auto-start with Windows** (delayed start for better boot performance)
- ✅ **Restart on failure** (with increasing delays: 5s, 10s, 30s)
- ✅ **Start after network services** (depends on TCP/IP services)
- ✅ **Comprehensive monitoring** with event logging and diagnostics

### 2. Setup ESP32-S3

#### Install Required Arduino Libraries
- ArduinoJson (by Benoit Blanchon)
- ESP32-specific display libraries (LVGL or similar for GUI)

#### Upload ESP32-S3 Code
1. Open ESP32 code in Arduino IDE or PlatformIO
2. Select ESP32-S3 board and COM port
3. Upload the code to the ESP32-S3-WROOM-1 ESP32-8048S050
4. The integrated display will automatically initialize

### 3. Connect Hardware
1. Connect ESP32-S3 to PC via USB-C
2. The service will auto-detect the ESP32-S3 and start sending data
3. The integrated display will show the system performance interface

## Configuration

### Windows Service Configuration
Edit `appsettings.json`:

```json
{
  "SystemMonitor": {
    "SerialPort": "AUTO",           // Auto-detect or specific port (e.g., "COM3")
    "BaudRate": 115200,             // Serial communication speed
    "TransmissionInterval": 1000,   // Milliseconds between updates
    "ESP32VendorId": "1A86",       // USB Vendor ID for detection
    "ESP32ProductId": "7523",      // USB Product ID for detection
    "AutoDetectESP32": true,       // Enable automatic ESP32 detection
    "ReconnectInterval": 5000      // Reconnection attempt    "ReconnectInterval": 5000      // Reconnection attempt interval
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}
```

### ESP32 Configuration
The ESP32-S3 code automatically configures:
- Integrated display interface (no external wiring required)
- UART on Serial0 at 115200 baud
- JSON parsing and GUI display updates
- Touch interface for interaction (if supported)

## Example JSON Output

The service sends JSON data to the ESP32 every second. Here's an example of the data format:

```json
{
  "ts": 1755143472,
  "cpu": {
    "usage": 11,
    "temp": 36,
    "fan": 932,
    "name": "Intel Core Ultra 7 265K"
  },
  "gpu": {
    "usage": 2,
    "temp": 36,
    "name": "NVIDIA GeForce RTX 5070 Ti",
    "mem_used": 4008,
    "mem_total": 16303
  },
  "mem": {
    "usage": 42,
    "used": 26.378284,
    "total": 63.409534,
    "avail": 37.03125
  }
}
```

### Field Descriptions

| Field | Type | Description | Unit |
|-------|------|-------------|------|
| `ts` | number | Unix timestamp | seconds |
| `cpu.usage` | number | CPU usage percentage | % |
| `cpu.temp` | number | CPU temperature | °C |
| `cpu.fan` | number | CPU fan speed | RPM |
| `cpu.name` | string | CPU model name | - |
| `gpu.usage` | number | GPU usage percentage | % |
| `gpu.temp` | number | GPU temperature | °C |
| `gpu.name` | string | GPU model name | - |
| `gpu.mem_used` | number | GPU memory used | MB |
| `gpu.mem_total` | number | GPU memory total | MB |
| `mem.usage` | number | RAM usage percentage | % |
| `mem.used` | number | RAM used | GB |
| `mem.total` | number | RAM total | GB |
| `mem.avail` | number | RAM available | GB |

## Project Structure

```
SystemPerformanceNotifierService/
├── Program.cs                 # Main application entry point
├── SystemMonitorWorker.cs     # Background service worker
├── Models/
│   └── SystemInfo.cs         # Data transfer objects
├── Services/
│   ├── ISystemInfoCollector.cs
│   ├── SystemInfoCollector.cs # Hardware monitoring logic
│   └── SerialCommunicator.cs  # ESP32 communication
├── Scripts/
│   ├── service-manager.ps1   # Unified service management script
│   └── README.md             # Script documentation
└── README.md                 # This documentation
```

## Troubleshooting

### Service Issues
- **Installation**: Use `Scripts\service-manager.ps1 install` with automatic admin elevation
- **Auto-start**: Service uses delayed auto-start (starts after Windows boot completes)
- **Dependencies**: Service waits for TCP/IP services to start first
- **Failure Recovery**: Service automatically restarts if it crashes (5s, 10s, 30s delays)
- **Health Monitoring**: Use `Scripts\service-manager.ps1 diagnostics` for comprehensive system checks
- **Event Logs**: Use `Scripts\service-manager.ps1 logs` to view detailed service events
- **Interactive Management**: Use `Scripts\service-manager.ps1` for guided troubleshooting

### Quick Diagnostics
```powershell
# Run comprehensive health check
Scripts\service-manager.ps1 diagnostics

# Quick essential checks only
Scripts\service-manager.ps1 diagnostics -Quick  

# Check current status with details
Scripts\service-manager.ps1 status -Detailed

# View recent service events
Scripts\service-manager.ps1 logs

# Live event monitoring
Scripts\service-manager.ps1 logs -Live

# Interactive management console
Scripts\service-manager.ps1
```

### Auto-Start Verification
```powershell
# Check service configuration
Scripts\service-manager.ps1 status

# Verify auto-start settings
sc qc SystemPerformanceNotifier | findstr "START_TYPE"

# Check service dependencies  
sc qc SystemPerformanceNotifier | findstr "DEPENDENCIES"
```

### ESP32 Connection Issues
- Check USB cable and port
- Verify ESP32 drivers are installed
- Monitor serial output for connection messages

### LCD Display Issues
- Verify I2C wiring connections
- Check LCD I2C address (default 0x27)
- Test LCD with simple Arduino sketch first

## Development

### Script Management

The `Scripts` folder contains a **single unified PowerShell script** that handles all service management operations:

#### Why Unified?
- **Simplicity**: One script instead of multiple files
- **Consistency**: Unified error handling and user experience
- **Maintainability**: Single file to update and maintain
- **User-friendly**: Interactive menu system for easy navigation
- **Automation-ready**: Complete command-line interface for CI/CD

#### Migration from Multiple Scripts
Previous versions used multiple separate scripts. The new unified approach:
- Consolidates all functionality into `service-manager.ps1`
- Maintains backward compatibility for all operations
- Adds enhanced features like live monitoring and comprehensive diagnostics
- Provides both interactive and command-line interfaces

### Building from Source
```bash
# Clone repository
git clone https://github.com/icefox0801/SystemPerformanceNotifierService
cd SystemPerformanceNotifierService

# Build in development mode
dotnet build

# Run in development mode
dotnet run --environment Development
```

### Hardware Compatibility
- **Tested CPUs**: Intel Core Ultra 7 265K, AMD Ryzen series
- **Tested GPUs**: NVIDIA RTX 5070 Ti, RTX 40 series
- **Tested ESP32**: ESP32-S3-WROOM-1 ESP32-8048S050 (integrated display), ESP32-WROOM-32, ESP32-S3
- **Display Compatibility**: Integrated ESP32-S3 display, 20x4 I2C LCD with PCF8574 backpack

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Contributing

1. Fork the repository
2. Create a feature branch
3. Commit your changes
4. Push to the branch
5. Create a Pull Request

## Support

For issues and questions:
- Open an issue on GitHub
- Check existing documentation
- Verify hardware setup and connections
