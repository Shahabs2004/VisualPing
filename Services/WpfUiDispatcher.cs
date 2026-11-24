using System.Windows.Threading;

namespace VisualPing.Services;

public sealed class WpfUiDispatcher : IUiDispatcher
{
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;

    public void Invoke(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        if (_dispatcher.CheckAccess())
            callback();
        else
            _ = _dispatcher.BeginInvoke(callback);
    }
}
