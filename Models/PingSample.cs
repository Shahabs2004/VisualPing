using System.Net.NetworkInformation;

namespace VisualPing.Models;

public sealed record PingSample(DateTimeOffset Timestamp, long? RoundtripTime, IPStatus Status, string? ErrorMessage = null)
{
    public bool IsSuccess => RoundtripTime.HasValue;
}
