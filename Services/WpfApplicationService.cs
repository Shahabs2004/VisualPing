using System.Windows;

namespace VisualPing.Services;

public sealed class WpfApplicationService : IApplicationService
{
    public void Shutdown() => Application.Current.Shutdown();
}
