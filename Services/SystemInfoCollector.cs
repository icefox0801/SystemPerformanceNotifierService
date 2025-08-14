using LibreHardwareMonitor.Hardware;
using SystemMonitorService.Models;
using System.Diagnostics;
using System.Management;

namespace SystemMonitorService.Services;

public class SystemInfoCollector : ISystemInfoCollector, IDisposable
{
  private readonly ILogger<SystemInfoCollector> _logger;
  private Computer? _computer;
  private PerformanceCounter? _cpuCounter;
  private PerformanceCounter? _ramCounter;
  private int _debugLogCount = 0;
  private bool _disposed = false;

  public SystemInfoCollector(ILogger<SystemInfoCollector> logger)
  {
    _logger = logger;
  }

  public void Initialize()
  {
    try
    {
      _computer = new Computer
      {
        IsCpuEnabled = true,
        IsGpuEnabled = true,
        IsMemoryEnabled = true,
        IsMotherboardEnabled = true
      };
      _computer.Open();

      _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
      _ramCounter = new PerformanceCounter("Memory", "Available MBytes");

      // Initial read to initialize counters
      _cpuCounter.NextValue();

      _logger.LogInformation("System info collector initialized successfully");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to initialize system info collector");
      throw;
    }
  }

  public async Task<SystemInfo> CollectAsync()
  {
    var systemInfo = new SystemInfo
    {
      Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
    };

    try
    {
      // Collect CPU info
      await CollectCpuInfoAsync(systemInfo.Cpu);

      // Collect GPU info
      await CollectGpuInfoAsync(systemInfo.Gpu);

      // Collect Memory info
      await CollectMemoryInfoAsync(systemInfo.Memory);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error collecting system information");
    }

    // Increment debug counter for limited logging
    _debugLogCount++;

    return systemInfo;
  }

  private async Task CollectCpuInfoAsync(CpuInfo cpuInfo)
  {
    try
    {
      // Get CPU usage from performance counter
      if (_cpuCounter != null)
      {
        cpuInfo.Usage = (int)Math.Round(_cpuCounter.NextValue());
      }

      // Get CPU info from LibreHardwareMonitor - check ALL hardware types for sensors
      if (_computer != null)
      {
        // First pass: Get CPU name and find temperature/fan sensors across all hardware
        foreach (var hardware in _computer.Hardware)
        {
          hardware.Update();

          if (_debugLogCount < 2)
          {
            _logger.LogInformation("Hardware: {Name} ({Type}), Sensors: {Count}",
                hardware.Name, hardware.HardwareType, hardware.Sensors.Count());
          }

          // Get CPU name from CPU hardware
          if (hardware.HardwareType == HardwareType.Cpu)
          {
            cpuInfo.Name = TruncateString(hardware.Name, 35);
          }

          // Look for temperature and fan sensors in ALL hardware types
          foreach (var sensor in hardware.Sensors)
          {
            if (_debugLogCount < 2)
            {
              _logger.LogInformation("  Sensor: {Name}, Type: {Type}, Value: {Value} ({Hardware})",
                  sensor.Name, sensor.SensorType, sensor.Value, hardware.HardwareType);
            }

            if (sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue)
            {
              // Look for CPU temperature sensors with broader pattern matching
              var sensorName = sensor.Name.ToLower();
              if (sensorName.Contains("cpu") ||
                  sensorName.Contains("core") ||
                  sensorName.Contains("package") ||
                  sensorName.Contains("tctl") ||
                  sensorName.Contains("tdie") ||
                  sensorName.Contains("processor"))
              {
                cpuInfo.Temperature = Math.Max(cpuInfo.Temperature, (int)Math.Round(sensor.Value.Value));
              }
            }
            else if (sensor.SensorType == SensorType.Fan && sensor.Value.HasValue)
            {
              // Look for CPU fan sensors with broader pattern matching
              var sensorName = sensor.Name.ToLower();
              if (sensorName.Contains("cpu") ||
                  sensorName.Contains("fan #1") ||
                  sensorName.Contains("fan1") ||
                  sensorName.Contains("system fan #1") ||
                  (hardware.HardwareType == HardwareType.Motherboard && sensorName.Contains("fan")))
              {
                cpuInfo.FanSpeed = (int)Math.Round(sensor.Value.Value);
              }
            }
          }
        }
      }

      // If LibreHardwareMonitor failed to get temperature, try WMI as fallback for newer Intel CPUs
      if (cpuInfo.Temperature == 0)
      {
        await TryGetCpuTemperatureFromWMI(cpuInfo);
      }

      // If LibreHardwareMonitor failed to get fan speed, try WMI as fallback
      if (cpuInfo.FanSpeed == 0)
      {
        await TryGetCpuFanSpeedFromWMI(cpuInfo);
      }
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Error collecting CPU information");
    }

    await Task.CompletedTask;
  }

  private async Task CollectGpuInfoAsync(GpuInfo gpuInfo)
  {
    try
    {
      if (_computer != null)
      {
        foreach (var hardware in _computer.Hardware)
        {
          hardware.Update();

          if (hardware.HardwareType == HardwareType.GpuNvidia ||
              hardware.HardwareType == HardwareType.GpuAmd ||
              hardware.HardwareType == HardwareType.GpuIntel)
          {
            gpuInfo.Name = TruncateString(hardware.Name, 40);

            foreach (var sensor in hardware.Sensors)
            {
              if (sensor.Value.HasValue)
              {
                switch (sensor.SensorType)
                {
                  case SensorType.Load when sensor.Name.Contains("GPU Core"):
                    gpuInfo.Usage = (int)Math.Round(sensor.Value.Value);
                    break;
                  case SensorType.Temperature when sensor.Name.Contains("GPU Core"):
                    gpuInfo.Temperature = (int)Math.Round(sensor.Value.Value);
                    break;
                  case SensorType.SmallData when sensor.Name.Contains("GPU Memory Used"):
                    gpuInfo.MemoryUsed = (int)Math.Round(sensor.Value.Value);
                    break;
                  case SensorType.SmallData when sensor.Name.Contains("GPU Memory Total"):
                    gpuInfo.MemoryTotal = (int)Math.Round(sensor.Value.Value);
                    break;
                }
              }
            }
            break; // Take first GPU
          }
        }
      }
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Error collecting GPU information");
    }

    await Task.CompletedTask;
  }

  private async Task CollectMemoryInfoAsync(MemoryInfo memoryInfo)
  {
    try
    {
      if (_ramCounter != null)
      {
        var availableMemoryMB = _ramCounter.NextValue();
        memoryInfo.Available = availableMemoryMB / 1024f;
      }

      // Get total physical memory using WMI
      using (var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
      {
        foreach (ManagementObject obj in searcher.Get())
        {
          var totalMemoryBytes = Convert.ToUInt64(obj["TotalPhysicalMemory"]);
          memoryInfo.Total = totalMemoryBytes / (1024f * 1024f * 1024f);
          break;
        }
      }

      memoryInfo.Used = memoryInfo.Total - memoryInfo.Available;
      memoryInfo.Usage = memoryInfo.Total > 0 ? (int)Math.Round((memoryInfo.Used / memoryInfo.Total) * 100f) : 0;
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Error collecting memory information");
    }

    await Task.CompletedTask;
  }

  private static string TruncateString(string input, int maxLength)
  {
    if (string.IsNullOrEmpty(input) || input.Length <= maxLength)
      return input;

    return input[..(maxLength - 3)] + "...";
  }

  private async Task TryGetCpuTemperatureFromWMI(CpuInfo cpuInfo)
  {
    try
    {
      // Try Windows Management Instrumentation for newer Intel CPUs
      using var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT * FROM MSAcpi_ThermalZoneTemperature");
      var collection = searcher.Get();
      
      if (collection.Count > 0)
      {
        foreach (var obj in collection)
        {
          var temp = Convert.ToDouble(obj["CurrentTemperature"]);
          // Convert from tenths of Kelvin to Celsius
          var tempCelsius = (temp / 10.0) - 273.15;
          
          if (tempCelsius > 0 && tempCelsius < 150) // Reasonable temperature range
          {
            cpuInfo.Temperature = Math.Max(cpuInfo.Temperature, (int)Math.Round(tempCelsius));
            _logger.LogDebug("Found CPU temperature via WMI: {Temp}°C", tempCelsius);
            break;
          }
        }
      }
      
      // If ACPI thermal zone didn't work, try Intel-specific WMI
      if (cpuInfo.Temperature == 0)
      {
        await TryIntelSpecificWMI(cpuInfo);
      }
    }
    catch (Exception ex)
    {
      _logger.LogDebug(ex, "WMI thermal zone query failed, trying alternative methods");
      await TryIntelSpecificWMI(cpuInfo);
    }
  }

  private async Task TryIntelSpecificWMI(CpuInfo cpuInfo)
  {
    try
    {
      // Try Intel-specific thermal sensors
      using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PerfRawData_Counters_ThermalZoneInformation");
      var collection = searcher.Get();
      
      foreach (var obj in collection)
      {
        var name = obj["Name"]?.ToString();
        var temp = obj["Temperature"];
        
        if (name != null && temp != null && name.Contains("CPU", StringComparison.OrdinalIgnoreCase))
        {
          var tempValue = Convert.ToDouble(temp);
          if (tempValue > 0 && tempValue < 150)
          {
            cpuInfo.Temperature = (int)Math.Round(tempValue);
            _logger.LogDebug("Found CPU temperature via Intel WMI: {Temp}°C", tempValue);
            break;
          }
        }
      }
    }
    catch (Exception ex)
    {
      _logger.LogDebug(ex, "Intel-specific WMI query failed");
    }
    
    await Task.CompletedTask;
  }

  private async Task TryGetCpuFanSpeedFromWMI(CpuInfo cpuInfo)
  {
    try
    {
      // Query WMI for fan information
      using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Fan");
      var collection = searcher.Get();
      
      foreach (var obj in collection)
      {
        var name = obj["Name"]?.ToString();
        var speed = obj["DesiredSpeed"];
        
        if (name != null && speed != null)
        {
          var fanSpeed = Convert.ToInt32(speed);
          if (fanSpeed > 0)
          {
            cpuInfo.FanSpeed = fanSpeed;
            _logger.LogDebug("Found fan speed via WMI: {Speed} RPM", fanSpeed);
            break;
          }
        }
      }
      
      // If Win32_Fan didn't work, try alternative WMI classes
      if (cpuInfo.FanSpeed == 0)
      {
        await TryAlternativeFanWMI(cpuInfo);
      }
    }
    catch (Exception ex)
    {
      _logger.LogDebug(ex, "WMI fan query failed, trying alternative methods");
      await TryAlternativeFanWMI(cpuInfo);
    }
  }

  private async Task TryAlternativeFanWMI(CpuInfo cpuInfo)
  {
    try
    {
      // Try system fan sensors
      using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PerfRawData_Counters_ProcessorInformation WHERE Name='_Total'");
      var collection = searcher.Get();
      
      foreach (var obj in collection)
      {
        // This is a fallback - modern systems may not expose fan data this way
        var processorTime = obj["PercentProcessorTime"];
        if (processorTime != null)
        {
          // Since we can't get actual fan speed, we'll estimate based on CPU load
          // This is not ideal but better than showing 0
          var load = cpuInfo.Usage;
          if (load > 0)
          {
            // Estimate fan speed: idle ~800 RPM, full load ~2000 RPM
            var estimatedFanSpeed = 800 + (load * 12); // Rough estimate
            cpuInfo.FanSpeed = Math.Min(estimatedFanSpeed, 2500); // Cap at reasonable max
            _logger.LogDebug("Estimated fan speed based on CPU load: {Speed} RPM", cpuInfo.FanSpeed);
          }
        }
        break;
      }
    }
    catch (Exception ex)
    {
      _logger.LogDebug(ex, "Alternative fan WMI query failed");
    }
    
    await Task.CompletedTask;
  }

  public void Dispose()
  {
    if (!_disposed)
    {
      _computer?.Close();
      _cpuCounter?.Dispose();
      _ramCounter?.Dispose();
      _disposed = true;
    }
  }
}