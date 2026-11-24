namespace VisualPing.Models;

public sealed record PingHistoryStatistics(int Capacity, int Count, int SuccessfulCount, double AverageLatency, double PacketLossPercent, TimeSpan Duration)
{
    public int FailedCount => Count - SuccessfulCount;
}
