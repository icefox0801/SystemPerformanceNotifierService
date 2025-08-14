# System Monitor Service for ESP32

## Project Overview
A Windows service that collects and sends system information (CPU, GPU, RAM usage and temperatures) to an ESP32 development board via USB/Serial connection for custom PC monitoring displays with LCD screens.

## Development History & Changes Made by Claude

### Initial Project Setup (August 13, 2025)
- **Fixed Program.cs**: Converted from basic web app template to proper Windows service configuration
- **Created ISystemInfoCollector Interface**: Added missing interface for dependency injection
- **Fixed Timer Namespace Conflicts**: Resolved ambiguity between System.Windows.Forms.Timer and System.Threading.Timer
- **Updated SystemMonitorWorker**: Fixed logger factory dependency injection
- **Fixed Install Script**: Corrected project path references

### .NET Framework Upgrade
- **Upgraded from .NET 8.0 to .NET 9.0**:
  - Updated `TargetFramework` from `net8.0-windows` to `net9.0-windows`
  - Upgraded all Microsoft package references to version 9.0.0
  - Successfully tested and verified functionality

### Project Structure
```
SystemInfoMonitorService/
├── Models/
│   └── SystemInfo.cs              # Data models for system information
├── Services/
│   ├── ISystemInfoCollector.cs    # Interface for system info collection
│   ├── SystemInfoCollector.cs     # Hardware monitoring implementation
│   └── SerialCommunicator.cs      # ESP32 USB/Serial communication
├── Scripts/
│   └── install-service.bat        # Windows service installation script
├── Properties/
│   └── launchSettings.json        # Development launch settings
├── Program.cs                     # Service host configuration
├── SystemMonitorWorker.cs         # Background service worker
├── appsettings.json              # Production configuration
├── appsettings.Development.json   # Development configuration
└── SystemInfoMonitorService.csproj # Project file
```

## Technical Features

### Hardware Monitoring
- **LibreHardwareMonitorLib**: For CPU/GPU temperatures and usage
- **Performance Counters**: For system memory and processor metrics
- **Real-time Data**: Collects data every 1000ms (configurable)

### ESP32 Communication
- **Auto-detection**: Automatically finds ESP32 devices on USB ports
- **Multiple Chip Support**: CH340, CP2102, FTDI, Prolific USB-to-Serial chips
- **Robust Connection**: Auto-reconnection and error handling
- **JSON Protocol**: Compact JSON format optimized for ESP32 memory

### Windows Service Integration
- **Background Service**: Runs as Windows service with auto-start
- **Service Management**: Install/uninstall via batch scripts
- **Logging**: Comprehensive logging with configurable levels
- **Configuration**: JSON-based configuration system

## Configuration Options

### Serial Communication
- `SerialPort`: "AUTO" for auto-detection or specific port (e.g., "COM3")
- `BaudRate`: Communication speed (default: 115200)
- `AutoDetectESP32`: Enable/disable automatic ESP32 detection
- `ESP32VendorId`/`ESP32ProductId`: USB device identification

### Monitoring Settings
- `TransmissionInterval`: Data transmission frequency in milliseconds
- `ReconnectInterval`: Reconnection attempt frequency

## Build & Deployment

### Development
```bash
cd SystemInfoMonitorService
dotnet restore
dotnet build
dotnet run --environment Development
```

### Production Release
```bash
dotnet publish -c Release -r win-x64 --self-contained
```

### Windows Service Installation
```bash
# Run as Administrator
Scripts\install-service.bat
```

## Current Status
- ✅ **Framework**: .NET 9.0 (latest)
- ✅ **Build Status**: Clean compilation with minor async warning
- ✅ **Hardware Detection**: Successfully detects ESP32 on COM3 (CH340 USB-Serial)
- ✅ **System Monitoring**: CPU, GPU, RAM, and temperature collection working
- ✅ **Serial Communication**: Stable 115200 baud connection
- ✅ **Service Installed**: Running as Windows service (Process ID: 50620)
- ✅ **Auto-Start**: Configured for delayed automatic startup
- ✅ **Data Transmission**: Successfully sending system info to ESP32 every 1 second
- ✅ **Script Management**: Unified PowerShell script for all service operations

### Service Installation Completed (August 14, 2025)
- **Service Status**: RUNNING
- **Memory Usage**: ~90 MB  
- **ESP32 Connection**: COM3 (CH340 chip, VID: 1A86, PID: 7523)
- **Installation Method**: Unified `service-manager.ps1` script
- **Auto-Detection**: Working correctly with VID/PID matching

## Known Issues
- Warning CS1998: `DetectESP32PortAsync` method lacks await operator (harmless - method uses synchronous ManagementObjectSearcher)

## Hardware Requirements
- **Windows**: 10/11 or Server 2019+
- **ESP32**: ESP32-S3-WROOM-1 ESP32-8048S050 development board with USB connection
- **Display**: 20x4 I2C LCD (HD44780 compatible) 
- **Connection**: USB-A to Micro-USB or USB-C cable

## Future Enhancements
- Support for additional LCD sizes
- Web-based configuration interface
- Historical data logging
- Multiple ESP32 device support
- Custom metric selection

## Commit Message Convention

Your task is to help the user to generate a commit message and commit the changes using git.

### Guidelines

- DO NOT add any ads such as "Generated with [Claude Code](https://claude.ai/code)"
- Only generate the message for staged files/changes
- Don't add any files using `git add`. The user will decide what to add.
- Follow the rules below for the commit message.

### Format

```
<type>(<scope>): <emoji> <message title>

<bullet points summarizing what was updated>
```

### Rules

* title is lowercase, no period at the end.
* Title should be a clear summary, max 50 characters.
* Use the body (optional) to explain *why*, not just *what*.
* Bullet points should be concise and high-level.

### Avoid

* Vague titles like: "update", "fix stuff"
* Overly long or unfocused titles
* Excessive detail in bullet points

### Allowed Types

| Type     | Description                           |
| -------- | ------------------------------------- |
| feat     | New feature                           |
| fix      | Bug fix                               |
| chore    | Maintenance (e.g., tooling, deps)     |
| docs     | Documentation changes                 |
| refactor | Code restructure (no behavior change) |
| test     | Adding or refactoring tests           |
| style    | Code formatting (no logic change)     |
| perf     | Performance improvements              |

### Emoji Guidelines

Include an emoji in every commit message to categorize the type of change:

| Emoji | Category | Description |
| ----- | -------- | ----------- |
| 📚 | Documentation | Hardware specs, schematics, user guides |
| ⚙️ | Configuration | VS Code settings, build config, toolchain |
| 💄 | Display/Graphics | LCD drivers, LVGL integration, UI components |
| 🔧 | Hardware/Fixes | GPIO config, peripheral drivers, bug fixes |
| ✅ | Testing | Unit tests, integration tests, validation |
| ⬆️ | Dependencies | Component updates, library management |
| ⚡ | Performance | Optimization, memory management, speed |
| 🎨 | UI/UX | Interface design, user experience |
| 🔒 | Security | Authentication, encryption, secure communication |
| 📱 | Touch/Mobile | Touch interfaces, gesture handling |

### Example Titles

```
feat(auth): 🔑 add JWT login flow
fix(ui): 🐛 handle null pointer in sidebar
refactor(api): ♻️ split user controller logic
docs(readme): 📝 add usage section
```

### Example with Title and Body

```
feat(auth): 🔑 add JWT login flow

- Implemented JWT token validation logic
- Added documentation for the validation component
```

---
*Last updated: August 13, 2025*  
*Claude AI Assistant - Project Setup & Migration*
