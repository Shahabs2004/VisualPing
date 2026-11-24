using VisualPing.Models;

namespace VisualPing.Services;

public interface IPingMonitorService : IDisposable
{
    IPingHistory History { get; }
    bool IsRunning { get; }
    event EventHandler<PingSample>? SampleReceived;
    event EventHandler? StateChanged;
    Task StartAsync(string address, int timeoutMilliseconds, int intervalMilliseconds);
    Task StopAsync();
    void UpdateTiming(int timeoutMilliseconds, int intervalMilliseconds);
}
