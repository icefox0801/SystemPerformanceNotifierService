using System.IO.Ports;
using System.Management;
using System.Text;
using System.Text.Json;
using SystemPerformanceNotifierService.Models;

namespace SystemPerformanceNotifierService.Services;

public class SerialCommunicator : IDisposable
{
  private readonly ILogger<SerialCommunicator> _logger;
  private readonly IConfiguration _configuration;
  private SerialPort? _serialPort;
  private string? _currentPortName;
  private bool _disposed = false;
  private readonly System.Threading.Timer _reconnectTimer;
  private readonly CancellationTokenSource _cancellationTokenSource;
  private Task? _readTask;

  public SerialCommunicator(ILogger<SerialCommunicator> logger, IConfiguration configuration)
  {
    _logger = logger;
    _configuration = configuration;
    _cancellationTokenSource = new CancellationTokenSource();
    _reconnectTimer = new System.Threading.Timer(CheckConnection, null, Timeout.Infinite, Timeout.Infinite);
  }

  public async Task InitializeAsync()
  {
    var configuredPort = _configuration["SystemPerformanceNotifier:SerialPort"] ?? "AUTO";
    var baudRate = _configuration.GetValue<int>("SystemPerformanceNotifier:BaudRate", 115200);
    var autoDetect = _configuration.GetValue<bool>("SystemPerformanceNotifier:AutoDetectESP32", true);

    if (configuredPort == "AUTO" && autoDetect)
    {
      _currentPortName = DetectESP32Port();
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
    var reconnectInterval = _configuration.GetValue<int>("SystemPerformanceNotifier:ReconnectInterval", 5000);
    _reconnectTimer.Change(reconnectInterval, reconnectInterval);
  }

  private string? DetectESP32Port()
  {
    try
    {
      var vendorId = _configuration["SystemPerformanceNotifier:ESP32VendorId"] ?? "1A86";
      var productId = _configuration["SystemPerformanceNotifier:ESP32ProductId"] ?? "7523";

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
      // Check if we're already connected to this port
      if (_serialPort?.IsOpen == true && _currentPortName == portName)
      {
        _logger.LogDebug("Already connected to {Port}, skipping reconnection", portName);
        return;
      }

      // Gracefully close existing connection without triggering reset
      if (_serialPort?.IsOpen == true)
      {
        try
        {
          // Don't change DTR/RTS states during close to prevent reset
          _serialPort.Close();
        }
        catch (Exception ex)
        {
          _logger.LogDebug("Error closing serial port: {Error}", ex.Message);
        }
      }
      _serialPort?.Dispose();

      // Create port with anti-reset configuration
      _serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
      {
        ReadTimeout = 1000,
        WriteTimeout = 1000,
        DtrEnable = false,  // Critical: Prevent ESP32 reset on connection
        RtsEnable = false,  // Critical: Prevent ESP32 reset on connection
        Handshake = Handshake.None,  // Disable hardware handshaking
        ReceivedBytesThreshold = 1   // Trigger events on single byte
      };

      // Use gentle connection approach - check if port is already in use
      await ConnectWithRetryAsync(portName);

      // Start reading incoming data from ESP32
      StartReadingIncomingData();

      // Wait for ESP32 to stabilize (configurable delay)
      var stabilizationDelay = _configuration.GetValue<int>("SystemPerformanceNotifier:ConnectionStabilizationDelay", 1000);
      await Task.Delay(stabilizationDelay);
      await SendHandshakeAsync();

      _logger.LogInformation("Connected to ESP32 on {Port} at {BaudRate} baud", portName, baudRate);
    }
    catch (UnauthorizedAccessException)
    {
      _logger.LogWarning("Port {Port} is in use by another process. Will retry when available.", portName);
      _serialPort?.Dispose();
      _serialPort = null;

      // Don't throw exception - let the reconnect timer handle retry
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to connect to port {Port}", portName);
      _serialPort?.Dispose();
      _serialPort = null;
      throw;
    }
  }

  private async Task ConnectWithRetryAsync(string portName)
  {
    const int maxRetries = 3;
    const int retryDelayMs = 100;

    if (_serialPort == null)
    {
      throw new InvalidOperationException("Serial port not initialized");
    }

    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
      try
      {
        // Check if another process is using the port
        var existingPorts = SerialPort.GetPortNames();
        if (!existingPorts.Contains(portName))
        {
          throw new InvalidOperationException($"Port {portName} not available");
        }

        // Attempt to open with minimal disturbance
        _serialPort!.Open();

        // Explicitly ensure DTR/RTS are false after opening
        await Task.Delay(50); // Small delay for port stabilization

        try
        {
          _serialPort.DtrEnable = false;
          _serialPort.RtsEnable = false;
        }
        catch (Exception ex)
        {
          _logger.LogDebug("Could not explicitly set DTR/RTS: {Error}", ex.Message);
        }

        _logger.LogDebug("Successfully connected to {Port} on attempt {Attempt}", portName, attempt);
        return; // Success!
      }
      catch (Exception ex) when (attempt < maxRetries)
      {
        _logger.LogDebug("Connection attempt {Attempt} failed: {Error}. Retrying...", attempt, ex.Message);

        // Close and dispose if partially opened
        try
        {
          _serialPort?.Close();
        }
        catch { }

        // Wait before retry
        await Task.Delay(retryDelayMs * attempt);
      }
    }

    // Final attempt - let exceptions bubble up
    _serialPort!.Open();

    await Task.Delay(50);
    try
    {
      _serialPort.DtrEnable = false;
      _serialPort.RtsEnable = false;
    }
    catch (Exception ex)
    {
      _logger.LogDebug("Could not explicitly set DTR/RTS on final attempt: {Error}", ex.Message);
    }
  }

  private async Task SendHandshakeAsync()
  {
    if (_serialPort?.IsOpen == true)
    {
      try
      {
        var handshake = "{\"type\":\"handshake\",\"service\":\"SystemPerformanceNotifier\",\"version\":\"1.0\"}\n";
        var data = Encoding.UTF8.GetBytes(handshake);
        await _serialPort.BaseStream.WriteAsync(data, 0, data.Length);
        await _serialPort.BaseStream.FlushAsync();
        _logger.LogDebug("Sent handshake to ESP32");
      }
      catch (Exception ex)
      {
        _logger.LogWarning(ex, "Failed to send handshake");
      }
    }
  }

  private void StartReadingIncomingData()
  {
    if (_serialPort?.IsOpen == true && _readTask?.IsCompleted != false)
    {
      _readTask = Task.Run(async () => await ReadIncomingDataAsync(_cancellationTokenSource.Token));
    }
  }

  private async Task ReadIncomingDataAsync(CancellationToken cancellationToken)
  {
    var buffer = new byte[4096];
    var stringBuilder = new StringBuilder();

    while (!cancellationToken.IsCancellationRequested && _serialPort?.IsOpen == true)
    {
      try
      {
        if (_serialPort.BytesToRead > 0)
        {
          var bytesRead = await _serialPort.BaseStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
          if (bytesRead > 0)
          {
            var receivedData = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            stringBuilder.Append(receivedData);

            // Process complete lines (messages ending with newline)
            string accumulated = stringBuilder.ToString();
            var lines = accumulated.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            // Keep the last incomplete line in buffer
            if (accumulated.EndsWith('\n') || accumulated.EndsWith('\r'))
            {
              stringBuilder.Clear();
              // Process all lines
              foreach (var line in lines)
              {
                ProcessIncomingMessage(line.Trim());
              }
            }
            else
            {
              // Keep the last line as it might be incomplete
              stringBuilder.Clear();
              for (int i = 0; i < lines.Length - 1; i++)
              {
                ProcessIncomingMessage(lines[i].Trim());
              }
              if (lines.Length > 0)
              {
                stringBuilder.Append(lines[^1]); // Keep the last incomplete line
              }
            }
          }
        }

        await Task.Delay(50, cancellationToken); // Small delay to prevent excessive CPU usage
      }
      catch (OperationCanceledException)
      {
        break;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error reading from serial port");
        await Task.Delay(1000, cancellationToken); // Wait before retrying
      }
    }
  }

  private void ProcessIncomingMessage(string message)
  {
    if (string.IsNullOrWhiteSpace(message))
      return;

    try
    {
      // Try to parse as JSON first
      if (message.StartsWith('{') && message.EndsWith('}'))
      {
        using var doc = JsonDocument.Parse(message);
        var root = doc.RootElement;

        if (root.TryGetProperty("type", out var typeElement))
        {
          var messageType = typeElement.GetString();
          switch (messageType?.ToLower())
          {
            case "debug":
              HandleDebugMessage(root);
              break;
            case "status":
              HandleStatusMessage(root);
              break;
            case "error":
              HandleErrorMessage(root);
              break;
            default:
              _logger.LogInformation("[ESP32 JSON] {Message}", message);
              break;
          }
        }
        else
        {
          _logger.LogInformation("[ESP32 JSON] {Message}", message);
        }
      }
      else
      {
        // Plain text debug output
        _logger.LogInformation("[ESP32 DEBUG] {Message}", message);
      }
    }
    catch (JsonException)
    {
      // Not valid JSON, treat as plain debug message
      _logger.LogInformation("[ESP32 DEBUG] {Message}", message);
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Error processing incoming message: {Message}", message);
    }
  }

  private void HandleDebugMessage(JsonElement root)
  {
    var level = "INFO";
    var message = "";
    var timestamp = "";

    if (root.TryGetProperty("level", out var levelElement))
      level = levelElement.GetString() ?? "INFO";

    if (root.TryGetProperty("message", out var msgElement))
      message = msgElement.GetString() ?? "";

    if (root.TryGetProperty("timestamp", out var tsElement))
      timestamp = tsElement.GetString() ?? "";

    var logLevel = level.ToUpper() switch
    {
      "ERROR" => LogLevel.Error,
      "WARN" or "WARNING" => LogLevel.Warning,
      "DEBUG" => LogLevel.Debug,
      "TRACE" => LogLevel.Trace,
      _ => LogLevel.Information
    };

    _logger.Log(logLevel, "[ESP32 {Level}]{Timestamp} {Message}",
        level, string.IsNullOrEmpty(timestamp) ? "" : $" [{timestamp}]", message);
  }

  private void HandleStatusMessage(JsonElement root)
  {
    if (root.TryGetProperty("message", out var msgElement))
    {
      var message = msgElement.GetString();
      _logger.LogInformation("[ESP32 STATUS] {Message}", message);
    }
  }

  private void HandleErrorMessage(JsonElement root)
  {
    if (root.TryGetProperty("message", out var msgElement))
    {
      var message = msgElement.GetString();
      _logger.LogError("[ESP32 ERROR] {Message}", message);
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
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
      });

      // Use newline as end marker - standard for serial communication
      var message = $"{json}\n";
      var data = Encoding.UTF8.GetBytes(message);

      // Send all data at once
      await _serialPort.BaseStream.WriteAsync(data, 0, data.Length);
      await _serialPort.BaseStream.FlushAsync();

      _logger.LogInformation("Sent to ESP32: {Json}", json);
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
    // Check if we're not connected and have a port to connect to
    if (_serialPort?.IsOpen != true && !string.IsNullOrEmpty(_currentPortName))
    {
      try
      {
        _logger.LogInformation("Attempting to reconnect to ESP32...");
        var baudRate = _configuration.GetValue<int>("SystemPerformanceNotifier:BaudRate", 115200);
        await ConnectToPortAsync(_currentPortName, baudRate);
      }
      catch (Exception ex)
      {
        _logger.LogDebug("Reconnection attempt failed: {Error}", ex.Message);

        // Try auto-detection again
        if (_configuration.GetValue<bool>("SystemPerformanceNotifier:AutoDetectESP32", true))
        {
          var newPort = DetectESP32Port();
          if (!string.IsNullOrEmpty(newPort) && newPort != _currentPortName)
          {
            _currentPortName = newPort;
            _logger.LogInformation("Detected ESP32 on different port: {Port}", newPort);
          }
        }
      }
    }
    else if (_serialPort?.IsOpen == true)
    {
      // Connection is active - perform health checks

      // Restart reading task if it completed unexpectedly
      if (_readTask?.IsCompleted == true || _readTask == null)
      {
        _logger.LogDebug("Restarting ESP32 data reading task");
        StartReadingIncomingData();
      }
    }
  }

  public void Dispose()
  {
    if (!_disposed)
    {
      _cancellationTokenSource.Cancel();
      _readTask?.Wait(TimeSpan.FromSeconds(2)); // Wait for read task to complete

      _reconnectTimer?.Dispose();

      // Gracefully close serial port without triggering reset
      if (_serialPort?.IsOpen == true)
      {
        try
        {
          // Ensure DTR/RTS remain false during close
          _serialPort.DtrEnable = false;
          _serialPort.RtsEnable = false;
          _serialPort.Close();
        }
        catch (Exception)
        {
          // Ignore close errors to prevent reset
        }
      }
      _serialPort?.Dispose();
      _cancellationTokenSource?.Dispose();
      _disposed = true;
    }
  }
}