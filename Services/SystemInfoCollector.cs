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

            // Get CPU info from LibreHardwareMonitor
            if (_computer != null)
            {
                foreach (var hardware in _computer.Hardware)
                {
                    hardware.Update();
                    
                    if (hardware.HardwareType == HardwareType.Cpu)
                    {
                        // Truncate name for ESP32 memory constraints
                        cpuInfo.Name = TruncateString(hardware.Name, 20);
                        
                        foreach (var sensor in hardware.Sensors)
                        {
                            if (sensor.SensorType == SensorType.Temperature && 
                                sensor.Name.Contains("Core") && sensor.Value.HasValue)
                            {
                                cpuInfo.Temperature = Math.Max(cpuInfo.Temperature, (int)Math.Round(sensor.Value.Value));
                            }
                        }
                        break;
                    }
                }
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
                        gpuInfo.Name = TruncateString(hardware.Name, 15);
                        
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