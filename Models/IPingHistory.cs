namespace VisualPing.Models;

public interface IPingHistory : IReadOnlyCollection<PingSample>
{
    int Capacity { get; }
    void Add(PingSample sample);
    void Resize(int capacity);
    void Clear();
    PingSample[] GetLatest(int maximumCount);
    PingHistoryStatistics GetStatistics();
}
