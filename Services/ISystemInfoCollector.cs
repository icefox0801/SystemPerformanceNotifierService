using SystemPerformanceNotifierService.Models;

namespace SystemPerformanceNotifierService.Services;

public interface ISystemInfoCollector : IDisposable
{
  Task InitializeAsync();
  Task<SystemInfo> CollectAsync();
}
