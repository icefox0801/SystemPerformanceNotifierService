using SystemMonitorService.Models;

namespace SystemMonitorService.Services;

public interface ISystemInfoCollector : IDisposable
{
  Task InitializeAsync();
  Task<SystemInfo> CollectAsync();
}
