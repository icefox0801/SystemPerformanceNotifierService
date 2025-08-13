using SystemMonitorService.Models;

namespace SystemMonitorService.Services;

public interface ISystemInfoCollector : IDisposable
{
    void Initialize();
    Task<SystemInfo> CollectAsync();
}
