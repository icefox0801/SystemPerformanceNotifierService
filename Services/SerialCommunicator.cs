using System.IO.Ports;
using System.Management;
using System.Text;
using System.Text.Json;
using SystemMonitorService.Models;

namespace SystemMonitorService.Services;

public class SerialCommunicator : IDisposable
{
    private readonly ILogger<SerialCommunicator> _logger;
    private readonly IConfiguration _configuration;
    private SerialPort? _serialPort;
    private string? _currentPortName;
    private bool _disposed = false;
    private readonly System.Threading.Timer _reconnectTimer;

    public SerialCommunicator(ILogger<SerialCommunicator> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _reconnectTimer = new System.Threading.Timer(CheckConnection, null, Timeout.Infinite, Timeout.Infinite);
    }

    public async Task InitializeAsync()
    {
        var configuredPort = _configuration["SystemMonitor:SerialPort"] ?? "AUTO";
        var baudRate = _configuration.GetValue<int>("SystemMonitor:BaudRate", 115200);
        var autoDetect = _configuration.GetValue<bool>("SystemMonitor:AutoDetectESP32", true);

        if (configuredPort == "AUTO" && autoDetect)
        {
            _currentPortName = await DetectESP32PortAsync();
        }
        else
        {
            _currentPortName = configuredPort;
        }

        if (!string.IsNullOrEmpty(_currentPortName) && _currentPortName != "AUTO")
        {
            await ConnectToPortAsync(_currentPortName, baudRate);
        }

        // Start reconnection monitoring
        var reconnectInterval = _configuration.GetValue<int>("SystemMonitor:ReconnectInterval", 5000);
        _reconnectTimer.Change(reconnectInterval, reconnectInterval);
    }

    private async Task<string?> DetectESP32PortAsync()
    {
        try
        {
            var vendorId = _configuration["SystemMonitor:ESP32VendorId"] ?? "1A86";
            var productId = _configuration["SystemMonitor:ESP32ProductId"] ?? "7523";

            _logger.LogInformation("Auto-detecting ESP32 on USB ports...");

            // Check for common ESP32 development board USB-to-Serial chips
            var commonVendorIds = new[] { "1A86", "10C4", "0403", "067B" }; // CH340, CP2102, FTDI, Prolific
            
            using (var searcher = new ManagementObjectSearcher(
                "SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%COM%'"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    var name = obj["Name"]?.ToString() ?? "";
                    var deviceId = obj["DeviceID"]?.ToString() ?? "";
                    
                    // Extract COM port number
                    var match = System.Text.RegularExpressions.Regex.Match(name, @"COM(\d+)");
                    if (match.Success)
                    {
                        var portName = $"COM{match.Groups[1].Value}";
                        
                        // Check if it matches ESP32 patterns
                        if (deviceId.Contains(vendorId) || 
                            commonVendorIds.Any(vid => deviceId.Contains(vid)) ||
                            name.ToLower().Contains("ch340") ||
                            name.ToLower().Contains("cp210") ||
                            name.ToLower().Contains("esp32"))
                        {
                            _logger.LogInformation("Found potential ESP32 device on {Port}: {Name}", portName, name);
                            return portName;
                        }
                    }
                }
            }

            // Fallback: try common ESP32 ports
            var commonPorts = new[] { "COM3", "COM4", "COM5", "COM6", "COM7", "COM8" };
            foreach (var port in commonPorts)
            {
                if (SerialPort.GetPortNames().Contains(port))
                {
                    _logger.LogInformation("Trying common ESP32 port: {Port}", port);
                    return port;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during ESP32 port detection");
        }

        return null;
    }

    private async Task ConnectToPortAsync(string portName, int baudRate)
    {
        try
        {
            _serialPort?.Close();
            _serialPort?.Dispose();

            _serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
            {
                ReadTimeout = 1000,
                WriteTimeout = 1000,
                DtrEnable = true,
                RtsEnable = true
            };

            _serialPort.Open();
            
            // Send initial handshake
            await Task.Delay(100); // Allow ESP32 to reset if needed
            await SendHandshakeAsync();

            _logger.LogInformation("Connected to ESP32 on {Port} at {BaudRate} baud", portName, baudRate);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError("Port {Port} is already in use (possibly by serial monitor). Please close other applications using this port.", portName);
            _serialPort?.Dispose();
            _serialPort = null;
            throw new InvalidOperationException($"Port {portName} is already in use by another application", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to port {Port}", portName);
            _serialPort?.Dispose();
            _serialPort = null;
            throw;
        }
    }

    private async Task SendHandshakeAsync()
    {
        if (_serialPort?.IsOpen == true)
        {
            try
            {
                var handshake = "{\"type\":\"handshake\",\"service\":\"SystemMonitor\",\"version\":\"1.0\"}\n";
                var data = Encoding.UTF8.GetBytes(handshake);
                await _serialPort.BaseStream.WriteAsync(data, 0, data.Length);
                await _serialPort.BaseStream.FlushAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send handshake");
            }
        }
    }

    public async Task SendSystemInfoAsync(SystemInfo systemInfo)
    {
        if (_serialPort?.IsOpen != true)
        {
            _logger.LogWarning("Serial port not connected, skipping transmission");
            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(systemInfo, new JsonSerializerOptions 
            { 
                WriteIndented = false 
            });
            
            // Add newline delimiter for ESP32 parsing
            json += "\n";
            
            var data = Encoding.UTF8.GetBytes(json);
            
            await _serialPort.BaseStream.WriteAsync(data, 0, data.Length);
            await _serialPort.BaseStream.FlushAsync();
            
            _logger.LogInformation("Sent to ESP32: {Json}", json.TrimEnd());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send system information to ESP32");
            
            // Try to reconnect on next cycle
            _serialPort?.Close();
        }
    }

    private async void CheckConnection(object? state)
    {
        if (_serialPort?.IsOpen != true && !string.IsNullOrEmpty(_currentPortName))
        {
            try
            {
                _logger.LogInformation("Attempting to reconnect to ESP32...");
                var baudRate = _configuration.GetValue<int>("SystemMonitor:BaudRate", 115200);
                await ConnectToPortAsync(_currentPortName, baudRate);
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Reconnection attempt failed: {Error}", ex.Message);
                
                // Try auto-detection again
                if (_configuration.GetValue<bool>("SystemMonitor:AutoDetectESP32", true))
                {
                    var newPort = await DetectESP32PortAsync();
                    if (!string.IsNullOrEmpty(newPort) && newPort != _currentPortName)
                    {
                        _currentPortName = newPort;
                        _logger.LogInformation("Detected ESP32 on different port: {Port}", newPort);
                    }
                }
            }
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _reconnectTimer?.Dispose();
            _serialPort?.Close();
            _serialPort?.Dispose();
            _disposed = true;
        }
    }
}