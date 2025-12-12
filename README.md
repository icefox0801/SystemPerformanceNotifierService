# System Performance Notifier Service

A lightweight Windows service that monitors system performance (CPU, GPU, RAM, Temperatures) and transmits real-time data to an ESP32 display via USB/Serial.

## 🚀 Features

- **Real-time Monitoring**: CPU/GPU usage & temps, RAM utilization.
- **Hardware Support**: Supports modern hardware (Intel Core Ultra, RTX 40/50 series) via AIDA64.
- **Plug & Play**: Auto-detects ESP32 devices and handles reconnections.
- **Background Service**: Runs silently as a Windows Service.

## 📋 Requirements

- **OS**: Windows 10 / 11 (64-bit)
- **Software**: 
  - .NET 9.0 Runtime
  - [AIDA64 Extreme/Engineer](https://www.aida64.com/) (Recommended for sensor data)
- **Hardware**: ESP32-S3 Development Board with Display (e.g., ESP32-8048S050)

## 🛠️ Installation & Setup

### 1. Configure Sensors (AIDA64)
For the most reliable temperature readings (especially on newer motherboards like ASUS Z890), we use AIDA64's Shared Memory feature.

1.  Open **AIDA64**.
2.  Go to **File** > **Preferences** > **Hardware Monitoring** > **External Applications**.
3.  ✅ Check **"Enable Shared Memory"**.
4.  Click **OK**.

### 2. Install the Service
The included PowerShell script handles building, installing, and starting the service.

1.  Open PowerShell as **Administrator**.
2.  Navigate to the `Scripts` folder.
3.  Run the installer:
    ```powershell
    cd Scripts
    .\service-manager.ps1 install
    ```

### 3. ESP32 Setup
1.  Flash your ESP32 with the compatible firmware (Arduino/PlatformIO project).
2.  Connect the ESP32 to your PC via USB.
3.  The service will automatically detect the device (looking for USB Vendor ID `1A86` by default).

## 🎮 Management

You can manage the service using the interactive menu:

```powershell
.\Scripts\service-manager.ps1
```

Or use simple commands:

- **Check Status**: `.\Scripts\service-manager.ps1 status`
- **View Logs**: `.\Scripts\service-manager.ps1 logs -Live`
- **Restart**: `.\Scripts\service-manager.ps1 restart`
- **Uninstall**: `.\Scripts\service-manager.ps1 uninstall`

## ⚙️ Configuration (`appsettings.json`)

Located in the installation directory (usually `bin\Release\net9.0-windows\win-x64`).

```json
{
  "SystemMonitor": {
    "SerialPort": "AUTO",          // "AUTO" or specific port like "COM3"
    "BaudRate": 115200,
    "TransmissionInterval": 1000,  // Update every 1 second
    "ESP32VendorId": "1A86",       // Change if using a different ESP32 board
    "ESP32ProductId": "7523"
  }
}
```

## 🏗️ Architecture

```mermaid
graph LR
    A[Hardware Sensors] -->|Shared Memory| B(AIDA64)
    B -->|Memory Map| C[Windows Service]
    C -->|JSON / Serial| D[ESP32 Display]
```

1.  **Data Collection**: The service reads data from AIDA64 Shared Memory (or WMI/Performance Counters as fallback).
2.  **Processing**: Data is formatted into a compact JSON object.
3.  **Transmission**: JSON is sent over USB Serial to the ESP32.
4.  **Display**: ESP32 parses JSON and updates the UI.

## ❓ Troubleshooting

**"CPU Temp is 0°C"**
- Ensure AIDA64 is running.
- Ensure "Enable Shared Memory" is checked in AIDA64 preferences.
- Check logs: `.\Scripts\service-manager.ps1 logs`

**"ESP32 Not Detected"**
- Check USB cable connection.
- Verify the Vendor ID in `appsettings.json` matches your device (Check Device Manager > Ports > Details > Hardware Ids).

## 📜 License
MIT License
