namespace VisualPing.Models;

public sealed class PingSettings
{
    public string Address { get; set; } = "10.7.1.1";
    public int IntervalMilliseconds { get; set; } = 1000;
    public int TimeoutMilliseconds { get; set; } = 1000;
    public int HistoryCapacity { get; set; } = 50000;
    public bool AutoStart { get; set; }
    public bool ShowGrid { get; set; } = true;
    public bool AlwaysOnTop { get; set; }
}
