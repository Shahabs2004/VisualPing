using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Media;
using VisualPing.Models;

namespace VisualPing.Controls;

public sealed class PingGraphControl : FrameworkElement
{
    private const double PixelSize = 4;
    private const double GridSpacing = 4;
    private readonly Dictionary<Color, SolidColorBrush> _brushes = new();
    private DrawingGroup? _drawing;
    private double _renderedWidth;
    private double _renderedHeight;

    private static readonly SolidColorBrush BackgroundBrush = CreateBrush(Colors.Black);
    private static readonly Pen GridPen = CreateGridPen();

    public static readonly DependencyProperty HistoryProperty = DependencyProperty.Register(
        nameof(History), typeof(IPingHistory), typeof(PingGraphControl),
        new FrameworkPropertyMetadata(null, OnRenderingPropertyChanged));

    public static readonly DependencyProperty RefreshTriggerProperty = DependencyProperty.Register(
        nameof(RefreshTrigger), typeof(long), typeof(PingGraphControl),
        new FrameworkPropertyMetadata(0L, OnRenderingPropertyChanged));

    public static readonly DependencyProperty ShowGridProperty = DependencyProperty.Register(
        nameof(ShowGrid), typeof(bool), typeof(PingGraphControl),
        new FrameworkPropertyMetadata(true, OnRenderingPropertyChanged));

    public static readonly DependencyProperty TimeoutMillisecondsProperty = DependencyProperty.Register(
        nameof(TimeoutMilliseconds), typeof(int), typeof(PingGraphControl),
        new FrameworkPropertyMetadata(1000, OnRenderingPropertyChanged));

    public IPingHistory? History
    {
        get => (IPingHistory?)GetValue(HistoryProperty);
        set => SetValue(HistoryProperty, value);
    }

    public long RefreshTrigger
    {
        get => (long)GetValue(RefreshTriggerProperty);
        set => SetValue(RefreshTriggerProperty, value);
    }

    public bool ShowGrid
    {
        get => (bool)GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }

    public int TimeoutMilliseconds
    {
        get => (int)GetValue(TimeoutMillisecondsProperty);
        set => SetValue(TimeoutMillisecondsProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        if (_drawing is null || _renderedWidth != ActualWidth || _renderedHeight != ActualHeight)
        {
            _drawing = BuildDrawing();
            _renderedWidth = ActualWidth;
            _renderedHeight = ActualHeight;
        }

        drawingContext.DrawDrawing(_drawing);
    }

    private DrawingGroup BuildDrawing()
    {
        var drawing = new DrawingGroup();

        using (var context = drawing.Open())
        {
            var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
            context.DrawRectangle(BackgroundBrush, null, bounds);

            if (ShowGrid && bounds.Width > 0 && bounds.Height > 0)
                context.DrawGeometry(null, GridPen, BuildGridGeometry(bounds));

            var columns = (int)Math.Floor(bounds.Width / PixelSize);
            var rows = (int)Math.Floor(bounds.Height / PixelSize);
            var visibleCapacity = (int)Math.Min(int.MaxValue, (long)columns * rows);
            var samples = History?.GetLatest(visibleCapacity) ?? Array.Empty<PingSample>();

            for (var index = 0; index < samples.Length; index++)
            {
                var x = index % columns * PixelSize;
                var y = index / columns * PixelSize;
                context.DrawRectangle(GetBrush(GetSampleColor(samples[index])), null, new Rect(x, y, PixelSize, PixelSize));
            }
        }

        drawing.Freeze();
        return drawing;
    }

    private StreamGeometry BuildGridGeometry(Rect bounds)
    {
        var geometry = new StreamGeometry();

        using (var context = geometry.Open())
        {
            for (var x = 0.0; x < bounds.Width; x += GridSpacing)
            {
                context.BeginFigure(new Point(x, 0), false, false);
                context.LineTo(new Point(x, bounds.Height), true, false);
            }

            for (var y = 0.0; y < bounds.Height; y += GridSpacing)
            {
                context.BeginFigure(new Point(0, y), false, false);
                context.LineTo(new Point(bounds.Width, y), true, false);
            }
        }

        geometry.Freeze();
        return geometry;
    }

    private Color GetSampleColor(PingSample sample)
    {
        if (sample.IsSuccess)
        {
            var latency = sample.RoundtripTime!.Value;
            var green = latency <= 10
                ? 255
                : (int)Math.Clamp(255.0 - latency * 255.0 / Math.Max(1, TimeoutMilliseconds), 0, 255);

            return Color.FromRgb(0, (byte)green, 0);
        }

        return sample.Status switch
        {
            IPStatus.DestinationNetworkUnreachable => Colors.CadetBlue,
            IPStatus.DestinationHostUnreachable => Colors.Magenta,
            IPStatus.TimedOut => Colors.Red,
            IPStatus.Unknown => Colors.Red,
            _ => Colors.Black
        };
    }

    private SolidColorBrush GetBrush(Color color)
    {
        if (!_brushes.TryGetValue(color, out var brush))
        {
            brush = CreateBrush(color);
            _brushes.Add(color, brush);
        }

        return brush;
    }

    private static SolidColorBrush CreateBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Pen CreateGridPen()
    {
        var pen = new Pen(CreateBrush(Color.FromArgb(40, 255, 255, 255)), 1);
        pen.Freeze();
        return pen;
    }

    private static void OnRenderingPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        var graph = (PingGraphControl)dependencyObject;
        graph._drawing = null;
        graph.InvalidateVisual();
    }
}
