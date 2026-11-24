using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VisualPing.ViewModels;

namespace VisualPing;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private Point _dragStart;
    private bool _isDragCandidate;

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        pingGraph.MouseLeftButtonDown += PingGraph_MouseLeftButtonDown;
        pingGraph.MouseMove += PingGraph_MouseMove;
        pingGraph.MouseLeftButtonUp += PingGraph_MouseLeftButtonUp;
        UpdateLayout(Width);
    }

    private async void Window_Loaded(object sender, RoutedEventArgs eventArguments)
    {
        await _viewModel.InitializeAsync();
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs eventArguments)
    {
        UpdateLayout(eventArguments.NewSize.Width);
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs eventArguments)
    {
        if (eventArguments.PropertyName != nameof(MainWindowViewModel.IsPureDisplay))
            return;

        _isDragCandidate = false;
        if (pingGraph.IsMouseCaptured)
            pingGraph.ReleaseMouseCapture();

        UpdateLayout(Width);
    }

    private void PingGraph_MouseLeftButtonDown(object sender, MouseButtonEventArgs eventArguments)
    {
        if (eventArguments.ClickCount == 2)
        {
            _viewModel.TogglePureDisplayCommand.Execute(null);
            eventArguments.Handled = true;
            return;
        }

        if (!_viewModel.IsPureDisplay)
            return;

        _dragStart = eventArguments.GetPosition(this);
        _isDragCandidate = true;
        pingGraph.CaptureMouse();
    }

    private void PingGraph_MouseMove(object sender, MouseEventArgs eventArguments)
    {
        if (!_viewModel.IsPureDisplay || !_isDragCandidate || eventArguments.LeftButton != MouseButtonState.Pressed)
            return;

        var position = eventArguments.GetPosition(this);
        if (Math.Abs(position.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(position.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        _isDragCandidate = false;
        pingGraph.ReleaseMouseCapture();
        DragMove();
    }

    private void PingGraph_MouseLeftButtonUp(object sender, MouseButtonEventArgs eventArguments)
    {
        _isDragCandidate = false;
        if (pingGraph.IsMouseCaptured)
            pingGraph.ReleaseMouseCapture();
    }

    private void UpdateLayout(double width)
    {
        var isPureDisplay = _viewModel.IsPureDisplay;
        var isCompact = width < 900;
        var chromeVisibility = isPureDisplay ? Visibility.Collapsed : Visibility.Visible;

        MinWidth = isPureDisplay ? 200 : 420;
        MinHeight = isPureDisplay ? 160 : 480;
        headerBorder.Visibility = chromeVisibility;
        pingControlsBorder.Visibility = chromeVisibility;
        statusBorder.Visibility = chromeVisibility;
        windowLayout.RowDefinitions[0].Height = isPureDisplay ? new GridLength(0) : GridLength.Auto;
        windowLayout.RowDefinitions[1].Height = new GridLength(1, GridUnitType.Star);
        windowLayout.RowDefinitions[2].Height = isPureDisplay ? new GridLength(0) : GridLength.Auto;
        windowLayout.RowDefinitions[3].Height = isPureDisplay ? new GridLength(0) : GridLength.Auto;

        contentScroll.VerticalScrollBarVisibility = !isPureDisplay && isCompact
            ? ScrollBarVisibility.Auto
            : ScrollBarVisibility.Disabled;
        contentGrid.Margin = isPureDisplay ? new Thickness(0) : new Thickness(20);
        contentGrid.ColumnDefinitions[1].Width = !isPureDisplay && !isCompact ? new GridLength(240) : new GridLength(0);
        contentGrid.RowDefinitions[0].Height = !isPureDisplay && isCompact
            ? GridLength.Auto
            : new GridLength(1, GridUnitType.Star);
        contentGrid.RowDefinitions[1].Height = !isPureDisplay && isCompact ? GridLength.Auto : new GridLength(0);

        Grid.SetColumn(graphHost, 0);
        Grid.SetColumnSpan(graphHost, isPureDisplay || isCompact ? 2 : 1);
        graphHost.Height = !isPureDisplay && isCompact ? 260 : double.NaN;
        graphHost.MinHeight = isPureDisplay ? 0 : 160;
        graphHost.Margin = isPureDisplay ? new Thickness(0) : new Thickness(0, 0, 10, 0);
        graphHost.BorderThickness = isPureDisplay ? new Thickness(0) : new Thickness(1);
        graphHost.CornerRadius = new CornerRadius(isPureDisplay ? 0 : 10);

        sidePanel.Visibility = isPureDisplay ? Visibility.Collapsed : Visibility.Visible;
        Grid.SetRow(sidePanel, !isPureDisplay && isCompact ? 1 : 0);
        Grid.SetColumn(sidePanel, !isPureDisplay && isCompact ? 0 : 1);
        Grid.SetColumnSpan(sidePanel, !isPureDisplay && isCompact ? 2 : 1);
        sidePanel.Width = !isPureDisplay && !isCompact ? 240 : double.NaN;
        sidePanel.Orientation = !isPureDisplay && isCompact && width >= 700 ? Orientation.Horizontal : Orientation.Vertical;
        sidePanel.Margin = !isPureDisplay && isCompact ? new Thickness(0, 12, 0, 0) : new Thickness(0);

        historyOverlay.Visibility = isPureDisplay ? Visibility.Collapsed : Visibility.Visible;
        historySummaryText.Visibility = !isPureDisplay && width < 700 ? Visibility.Collapsed : Visibility.Visible;
        pingGraph.Cursor = isPureDisplay ? Cursors.SizeAll : Cursors.Arrow;
    }

    protected override void OnClosed(EventArgs eventArguments)
    {
        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _viewModel.Dispose();
        base.OnClosed(eventArguments);
    }
}
