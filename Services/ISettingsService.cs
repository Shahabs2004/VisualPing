using VisualPing.Models;

namespace VisualPing.Services;

public interface ISettingsService
{
    Task<bool> SaveAsync(PingSettings settings);
    Task<PingSettings?> LoadAsync();
}
