using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Net.NetworkInformation;
using Microsoft.Extensions.Logging;
using VisualPing.Models;
using VisualPing.Services;

namespace VisualPing.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject, IDisposable
{
    public static IReadOnlyList<int> Intervals { get; } = new[] { 200, 500, 1000, 2000, 5000, 10000 };
    public static IReadOnlyList<int> Timeouts { get; } = new[] { 100, 200, 500, 1000, 2000 };

    private readonly IPingMonitorService _pingMonitor;
    private readonly ISettingsService _settingsService;
    private readonly IApplicationService _applicationService;
    private readonly IUiDispatcher _dispatcher;
    private readonly ILogger<MainWindowViewModel> _logger;
    private bool _disposed;

    [ObservableProperty] private string _address = "10.7.1.1";
    [ObservableProperty] private int _intervalMilliseconds = 1000;
    [ObservableProperty] private int _timeoutMilliseconds = 1000;
    [ObservableProperty] private bool _autoStart;
    [ObservableProperty] private bool _showGrid = true;
    [ObservableProperty] private bool _alwaysOnTop;
    [ObservableProperty] private bool _isPureDisplay;
    [ObservableProperty] private int _historyCapacity = 50000;
    [ObservableProperty] private string _historyCapacityText = "50000";
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _pingButtonText = "Start Ping";
    [ObservableProperty] private string _currentPing = "-- ms";
    [ObservableProperty] private string _averagePing = "-- ms";
    [ObservableProperty] private string _packetLoss = "0%";
    [ObservableProperty] private string _duration = "00:00";
    [ObservableProperty] private string _pingCount = "--";
    [ObservableProperty] private string _historySummary = string.Empty;
    [ObservableProperty] private long _historyVersion;
    [ObservableProperty] private string _statusMessage = "Ready";
    [ObservableProperty] private bool _hasStatusError;

    public MainWindowViewModel(
        IPingMonitorService pingMonitor,
        ISettingsService settingsService,
        IApplicationService applicationService,
        IUiDispatcher dispatcher,
        ILogger<MainWindowViewModel> logger)
    {
        _pingMonitor = pingMonitor ?? throw new ArgumentNullException(nameof(pingMonitor));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _applicationService = applicationService ?? throw new ArgumentNullException(nameof(applicationService));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _pingMonitor.SampleReceived += OnSampleReceived;
        _pingMonitor.StateChanged += OnStateChanged;
        UpdateStatistics();
    }

    public IPingHistory History => _pingMonitor.History;

    public Task InitializeAsync() => AutoStart ? TogglePingAsync() : Task.CompletedTask;

    partial void OnIntervalMillisecondsChanged(int value)
    {
        if (value > 0 && _timeoutMilliseconds > 0)
            _pingMonitor.UpdateTiming(_timeoutMilliseconds, value);
    }

    partial void OnTimeoutMillisecondsChanged(int value)
    {
        if (value > 0 && _intervalMilliseconds > 0)
            _pingMonitor.UpdateTiming(value, _intervalMilliseconds);
    }

    [RelayCommand]
    private void TogglePureDisplay() => IsPureDisplay = !IsPureDisplay;

    [RelayCommand]
    private async Task TogglePingAsync()
    {
        if (string.IsNullOrWhiteSpace(Address))
        {
            StatusMessage = "Enter a host name or IP address.";
            HasStatusError = true;
            return;
        }

        if (!Intervals.Contains(IntervalMilliseconds) || !Timeouts.Contains(TimeoutMilliseconds))
        {
            StatusMessage = "Select a valid ping interval and timeout.";
            HasStatusError = true;
            return;
        }

        try
        {
            if (_pingMonitor.IsRunning)
            {
                await _pingMonitor.StopAsync();
                return;
            }

            await _pingMonitor.StartAsync(Address, TimeoutMilliseconds, IntervalMilliseconds);
            StatusMessage = $"Monitoring {Address.Trim()}";
            HasStatusError = false;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to change ping monitoring state.");
            StatusMessage = "Ping monitoring could not be started or stopped.";
            HasStatusError = true;
        }
    }

    [RelayCommand]
    private void ApplyHistoryCapacity(string? text)
    {
        if (!int.TryParse(text?.Trim(), out var capacity) || capacity is < 100 or > 50000)
        {
            StatusMessage = "History capacity must be between 100 and 50,000.";
            HasStatusError = true;
            return;
        }

        ResizeHistory(capacity);
        StatusMessage = $"History capacity set to {capacity:N0} samples.";
        HasStatusError = false;
    }

    [RelayCommand]
    private void ClearHistory()
    {
        _pingMonitor.History.Clear();
        HistoryVersion++;
        CurrentPing = "-- ms";
        StatusMessage = "History cleared. Ping data is kept in memory only.";
        HasStatusError = false;
        UpdateStatistics();
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        var settings = new PingSettings
        {
            Address = Address,
            IntervalMilliseconds = IntervalMilliseconds,
            TimeoutMilliseconds = TimeoutMilliseconds,
            HistoryCapacity = HistoryCapacity,
            AutoStart = AutoStart,
            ShowGrid = ShowGrid,
            AlwaysOnTop = AlwaysOnTop
        };

        try
        {
            var saved = await _settingsService.SaveAsync(settings);
            if (saved)
            {
                StatusMessage = "Settings saved.";
                HasStatusError = false;
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to save settings.");
            StatusMessage = "Settings could not be saved.";
            HasStatusError = true;
        }
    }

    [RelayCommand]
    private async Task LoadSettingsAsync()
    {
        try
        {
            var settings = await _settingsService.LoadAsync();
            if (settings is null)
                return;

            Address = string.IsNullOrWhiteSpace(settings.Address) ? "10.7.1.1" : settings.Address.Trim();
            IntervalMilliseconds = Intervals.Contains(settings.IntervalMilliseconds) ? settings.IntervalMilliseconds : 1000;
            TimeoutMilliseconds = Timeouts.Contains(settings.TimeoutMilliseconds) ? settings.TimeoutMilliseconds : 1000;
            ResizeHistory(settings.HistoryCapacity is >= 100 and <= 50000 ? settings.HistoryCapacity : HistoryCapacity);
            AutoStart = settings.AutoStart;
            ShowGrid = settings.ShowGrid;
            AlwaysOnTop = settings.AlwaysOnTop;
            StatusMessage = "Settings loaded.";
            HasStatusError = false;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to load settings.");
            StatusMessage = "The selected settings file is invalid or could not be read.";
            HasStatusError = true;
        }
    }

    [RelayCommand]
    private void Exit() => _applicationService.Shutdown();

    private void OnSampleReceived(object? sender, PingSample sample)
    {
        _dispatcher.Invoke(() =>
        {
            if (_disposed)
                return;

            HistoryVersion++;
            CurrentPing = FormatCurrentPing(sample);
            HasStatusError = !sample.IsSuccess;
            StatusMessage = sample.IsSuccess ? $"Monitoring {Address.Trim()}" : CurrentPing;
            UpdateStatistics();
        });
    }

    private void OnStateChanged(object? sender, EventArgs eventArgs)
    {
        _dispatcher.Invoke(() =>
        {
            if (_disposed)
                return;

            IsRunning = _pingMonitor.IsRunning;
            PingButtonText = IsRunning ? "Stop Ping" : "Start Ping";

            if (!HasStatusError)
                StatusMessage = IsRunning ? $"Monitoring {Address.Trim()}" : "Ready";
        });
    }

    private void ResizeHistory(int capacity)
    {
        _pingMonitor.History.Resize(capacity);
        HistoryCapacity = capacity;
        HistoryCapacityText = capacity.ToString();
        HistoryVersion++;
        UpdateStatistics();
    }

    private void UpdateStatistics()
    {
        var statistics = _pingMonitor.History.GetStatistics();
        PingCount = statistics.SuccessfulCount > 0 ? statistics.SuccessfulCount.ToString("N0") : "--";
        AveragePing = statistics.SuccessfulCount > 0 ? $"{statistics.AverageLatency:F1} ms" : "-- ms";
        PacketLoss = $"{statistics.PacketLossPercent:F1}%";
        Duration = statistics.Duration.TotalHours >= 1
            ? statistics.Duration.ToString(@"hh\:mm\:ss")
            : statistics.Duration.ToString(@"mm\:ss");
        HistorySummary = $"{statistics.Count:N0} / {statistics.Capacity:N0} samples in memory";
    }

    private static string FormatCurrentPing(PingSample sample)
    {
        if (sample.IsSuccess)
            return $"{sample.RoundtripTime} ms";

        return sample.Status switch
        {
            IPStatus.TimedOut => "Timed out",
            IPStatus.Unknown => "Error",
            _ => "Failed"
        };
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _pingMonitor.SampleReceived -= OnSampleReceived;
        _pingMonitor.StateChanged -= OnStateChanged;
        _pingMonitor.StopAsync().GetAwaiter().GetResult();
    }
}
