using SystemPerformanceNotifierService;
using SystemPerformanceNotifierService.Services;

var builder = Host.CreateApplicationBuilder(args);

// Configure services
builder.Services.AddWindowsService(options =>
{
  options.ServiceName = "SystemPerformanceNotifier";
});

// Register application services
builder.Services.AddSingleton<ISystemInfoCollector, SystemInfoCollector>();
builder.Services.AddHostedService<SystemMonitorWorker>();

// Configure logging
builder.Services.AddLogging(logging =>
{
  logging.ClearProviders();
  logging.AddConsole();
  logging.AddEventLog();
});

var host = builder.Build();

await host.RunAsync();
