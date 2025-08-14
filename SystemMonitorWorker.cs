using SystemPerformanceNotifierService.Services;

namespace SystemPerformanceNotifierService;

public class SystemMonitorWorker : BackgroundService
{
  private readonly ILogger<SystemMonitorWorker> _logger;
  private readonly IConfiguration _configuration;
  private readonly ISystemInfoCollector _systemInfoCollector;
  private readonly ILoggerFactory _loggerFactory;
  private SerialCommunicator? _serialCommunicator;

  public SystemMonitorWorker(
      ILogger<SystemMonitorWorker> logger,
      IConfiguration configuration,
      ISystemInfoCollector systemInfoCollector,
      ILoggerFactory loggerFactory)
  {
    _logger = logger;
    _configuration = configuration;
    _systemInfoCollector = systemInfoCollector;
    _loggerFactory = loggerFactory;
  }

  public override async Task StartAsync(CancellationToken cancellationToken)
  {
    _logger.LogInformation("System Monitor Service for ESP32 starting...");

    try
    {
      await _systemInfoCollector.InitializeAsync();

      _serialCommunicator = new SerialCommunicator(
          _loggerFactory.CreateLogger<SerialCommunicator>(),
          _configuration);

      await _serialCommunicator.InitializeAsync();

      _logger.LogInformation("System Monitor Service for ESP32 started successfully");
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("already in use"))
    {
      _logger.LogWarning("Serial port is in use by another application. Service will continue without serial output.");
      _logger.LogInformation("System Monitor Service started in monitoring-only mode");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to start System Monitor Service");
      throw;
    }

    await base.StartAsync(cancellationToken);
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    var interval = _configuration.GetValue<int>("SystemMonitor:TransmissionInterval", 1000);

    _logger.LogInformation("Starting system monitoring loop with {Interval}ms interval", interval);

    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        var systemInfo = await _systemInfoCollector.CollectAsync();

        if (_serialCommunicator != null)
        {
          await _serialCommunicator.SendSystemInfoAsync(systemInfo);
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error in monitoring loop");
      }

      await Task.Delay(interval, stoppingToken);
    }
  }

  public override async Task StopAsync(CancellationToken cancellationToken)
  {
    _logger.LogInformation("System Monitor Service stopping...");

    _serialCommunicator?.Dispose();
    _systemInfoCollector?.Dispose();

    await base.StopAsync(cancellationToken);

    _logger.LogInformation("System Monitor Service stopped");
  }
}