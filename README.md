# System Monitor Service for ESP32

A Windows service that collects and sends system information (CPU, GPU, RAM usage and temperatures) to an ESP32 development board via USB/Serial connection. Perfect for creating custom PC monitoring displays with LCD screens.

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

### Display Layout
```
┌─────────── System Monitor - ESP32-S3 ───────────┐
│ 📶 Connection Status        Last Update: Live   │
├─────────────────┬───────────────────────────────┤
│      CPU        │           GPU                 │
│ Intel Core...   │   NVIDIA GeForce...           │
│                 │                               │
│ Usage    Temp   │ Usage   Memory    Temp        │
│  ##%     ##°C   │  ##%     ##%      ##°C        │
│         Fan     │                               │
│       -- RPM    │                               │
├─────────────────┴───────────────────────────────┤
│              System Memory                      │
│ Usage: ##%           (##.# GB / ##.# GB)        │
└─────────────────────────────────────────────────┘
```

## Quick Start

### 1. Setup Windows Service

#### Install Dependencies
```bash
# Clone or create the project
git clone https://github.com/icefox0801/SystemMonitorService
cd SystemMonitorService

# Restore NuGet packages
dotnet restore
```

#### Build and Install Service
```bash
# Build the project
dotnet build -c Release

# Install as Windows service (run as Administrator)
Scripts\install-service.bat
```

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
3. The integrated display will show the system monitoring interface

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
SystemMonitorService/
├── Program.cs                 # Main application entry point
├── SystemMonitorWorker.cs     # Background service worker
├── Models/
│   └── SystemInfo.cs         # Data transfer objects
├── Services/
│   ├── ISystemInfoCollector.cs
│   ├── SystemInfoCollector.cs # Hardware monitoring logic
│   └── SerialCommunicator.cs  # ESP32 communication
├── Scripts/
│   └── install-service.bat   # Service installation script
└── README.md                 # This documentation
```

## Troubleshooting

### Service Issues
- Check Windows Event Log for service errors
- Verify .NET 8.0 Runtime is installed
- Run service installation as Administrator

### ESP32 Connection Issues
- Check USB cable and port
- Verify ESP32 drivers are installed
- Monitor serial output for connection messages

### LCD Display Issues
- Verify I2C wiring connections
- Check LCD I2C address (default 0x27)
- Test LCD with simple Arduino sketch first

## Development

### Building from Source
```bash
# Clone repository
git clone https://github.com/icefox0801/SystemMonitorService
cd SystemMonitorService

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
