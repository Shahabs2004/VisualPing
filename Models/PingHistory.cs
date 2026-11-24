namespace VisualPing.Models;

public sealed class PingHistory : IPingHistory
{
    private PingSample?[] _samples;
    private readonly object _sync = new();
    private int _start;
    private int _count;
    private int _successfulCount;
    private long _totalLatency;

    public PingHistory(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        Capacity = capacity;
        _samples = new PingSample[capacity];
    }

    public int Capacity { get; private set; }

    public int Count
    {
        get
        {
            lock (_sync)
                return _count;
        }
    }

    public void Add(PingSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);

        lock (_sync)
        {
            var writeIndex = (_start + _count) % Capacity;
            var overwritten = _samples[writeIndex];

            if (overwritten is not null)
            {
                if (overwritten.IsSuccess)
                {
                    _successfulCount--;
                    _totalLatency -= overwritten.RoundtripTime!.Value;
                }

                _start = (_start + 1) % Capacity;
            }
            else
            {
                _count++;
            }

            _samples[writeIndex] = sample;

            if (sample.IsSuccess)
            {
                _successfulCount++;
                _totalLatency += sample.RoundtripTime!.Value;
            }
        }
    }

    public void Resize(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        lock (_sync)
        {
            if (capacity == Capacity)
                return;

            var resizedSamples = new PingSample?[capacity];
            var retainedCount = Math.Min(_count, capacity);
            var skip = _count - retainedCount;
            _successfulCount = 0;
            _totalLatency = 0;

            for (var i = 0; i < retainedCount; i++)
            {
                var sample = _samples[(_start + skip + i) % Capacity]!;
                resizedSamples[i] = sample;

                if (sample.IsSuccess)
                {
                    _successfulCount++;
                    _totalLatency += sample.RoundtripTime!.Value;
                }
            }

            _samples = resizedSamples;
            Capacity = capacity;
            _start = 0;
            _count = retainedCount;
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            Array.Clear(_samples);
            _start = 0;
            _count = 0;
            _successfulCount = 0;
            _totalLatency = 0;
        }
    }

    public PingSample[] GetLatest(int maximumCount)
    {
        if (maximumCount <= 0)
            return Array.Empty<PingSample>();

        lock (_sync)
        {
            var take = Math.Min(maximumCount, _count);
            var result = new PingSample[take];
            var skip = _count - take;

            for (var i = 0; i < take; i++)
                result[i] = _samples[(_start + skip + i) % Capacity]!;

            return result;
        }
    }

    public PingHistoryStatistics GetStatistics()
    {
        lock (_sync)
        {
            var duration = _count > 0
                ? _samples[(_start + _count - 1) % Capacity]!.Timestamp - _samples[_start]!.Timestamp
                : TimeSpan.Zero;

            return new PingHistoryStatistics(
                Capacity,
                _count,
                _successfulCount,
                _successfulCount > 0 ? (double)_totalLatency / _successfulCount : 0,
                _count > 0 ? (Count - _successfulCount) * 100.0 / _count : 0,
                duration);
        }
    }

    public IEnumerator<PingSample> GetEnumerator() => ((IEnumerable<PingSample>)GetLatest(Count)).GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
