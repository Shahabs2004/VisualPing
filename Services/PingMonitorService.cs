using System.Diagnostics;
using System.Net.NetworkInformation;
using Microsoft.Extensions.Logging;
using VisualPing.Models;

namespace VisualPing.Services;

public sealed class PingMonitorService(IPingHistory history, ILogger<PingMonitorService> logger) : IPingMonitorService
{
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private CancellationTokenSource? _cancellation;
    private Task _runTask = Task.CompletedTask;
    private Ping? _pinger;
    private int _timeoutMilliseconds;
    private int _intervalMilliseconds;
    private bool _disposed;

    public IPingHistory History { get; } = history ?? throw new ArgumentNullException(nameof(history));
    public bool IsRunning { get; private set; }

    public event EventHandler<PingSample>? SampleReceived;
    public event EventHandler? StateChanged;

    public async Task StartAsync(string address, int timeoutMilliseconds, int intervalMilliseconds)
    {
        if (string.IsNullOrWhiteSpace(address))
            throw new ArgumentException("A ping address is required.", nameof(address));
        if (timeoutMilliseconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds));
        if (intervalMilliseconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(intervalMilliseconds));

        await _lifecycleLock.WaitAsync().ConfigureAwait(false);

        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (IsRunning)
                return;

            UpdateTiming(timeoutMilliseconds, intervalMilliseconds);

            var cancellation = new CancellationTokenSource();
            var pinger = new Ping();
            _cancellation = cancellation;
            _pinger = pinger;
            SetRunning(true);
            _runTask = RunAsync(address.Trim(), pinger, cancellation.Token);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task StopAsync()
    {
        await _lifecycleLock.WaitAsync().ConfigureAwait(false);

        try
        {
            var cancellation = _cancellation;
            _cancellation = null;
            cancellation?.Cancel();

            if (!_runTask.IsCompleted)
                await _runTask.ConfigureAwait(false);

            _runTask = Task.CompletedTask;
            _pinger?.Dispose();
            _pinger = null;
            cancellation?.Dispose();
            SetRunning(false);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public void UpdateTiming(int timeoutMilliseconds, int intervalMilliseconds)
    {
        if (timeoutMilliseconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds));
        if (intervalMilliseconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(intervalMilliseconds));

        Volatile.Write(ref _timeoutMilliseconds, timeoutMilliseconds);
        Volatile.Write(ref _intervalMilliseconds, intervalMilliseconds);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        StopAsync().GetAwaiter().GetResult();
        _lifecycleLock.Dispose();
    }

    private async Task RunAsync(string address, Ping pinger, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var startedAt = Stopwatch.GetTimestamp();
                PingSample sample;

                try
                {
                    var reply = await pinger.SendPingAsync(address, Volatile.Read(ref _timeoutMilliseconds)).ConfigureAwait(false);
                    sample = reply.Status == IPStatus.Success
                        ? new PingSample(DateTimeOffset.UtcNow, reply.RoundtripTime, reply.Status)
                        : new PingSample(DateTimeOffset.UtcNow, null, reply.Status);
                }
                catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
                {
                    logger.LogWarning(exception, "Ping request to {Address} failed.", address);
                    sample = new PingSample(DateTimeOffset.UtcNow, null, IPStatus.Unknown, exception.Message);
                }

                History.Add(sample);
                SampleReceived?.Invoke(this, sample);

                var elapsedMilliseconds = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
                var delayMilliseconds = Math.Max(0, Volatile.Read(ref _intervalMilliseconds) - elapsedMilliseconds);

                if (delayMilliseconds > 0)
                    await Task.Delay(TimeSpan.FromMilliseconds(delayMilliseconds), cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Ping monitoring stopped unexpectedly.");
        }
        finally
        {
            SetRunning(false);
        }
    }

    private void SetRunning(bool isRunning)
    {
        if (IsRunning == isRunning)
            return;

        IsRunning = isRunning;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
