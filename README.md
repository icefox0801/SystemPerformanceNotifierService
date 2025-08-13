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
- ESP32 development board (ESP32-WROOM-32, NodeMCU-32S, etc.)
- 20x4 I2C LCD display (HD44780 compatible)
- USB cable (USB-A to Micro-USB or USB-C)
- Jumper wires for LCD connection

## Hardware Setup

### ESP32 to LCD Connections (I2C)
```
ESP32    ->    20x4 LCD (I2C)
GND      ->    GND
3.3V     ->    VCC
GPIO21   ->    SDA
GPIO22   ->    SCL
```

### LCD Display Layout
```
Line 1: CPU:[████████  ] 75%
Line 2: 45C GPU:[████      ] 60%
Line 3: RAM:[██████████] 85%  
Line 4: 12.5/16.0GB G:68C
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

### 2. Setup ESP32

#### Install Required Arduino Libraries
- ArduinoJson (by Benoit Blanchon)
- LiquidCrystal I2C (by Frank de Brabander)

#### Upload ESP32 Code
1. Open `ESP32Code/SystemMonitor.ino` in Arduino IDE
2. Select your ESP32 board and COM port
3. Upload the code
4. Connect the LCD according to the wiring diagram

### 3. Connect Hardware
1. Connect ESP32 to PC via USB
2. Connect LCD to ESP32 using I2C
3. The service will auto-detect the ESP32 and start sending data

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
    "ReconnectInterval": 5000      // Reconnection attempt