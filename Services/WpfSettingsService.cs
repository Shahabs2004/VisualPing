using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.Win32;
using VisualPing.Models;

namespace VisualPing.Services;

public sealed class WpfSettingsService : ISettingsService
{
    private const int MaximumSettingsFileBytes = 1_000_000;

    public async Task<bool> SaveAsync(PingSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var dialog = new SaveFileDialog
        {
            Filter = "XML settings (*.xml)|*.xml",
            DefaultExt = "xml",
            FileName = "VisualPing-settings.xml"
        };

        if (dialog.ShowDialog() != true)
            return false;

        var serializer = new XmlSerializer(typeof(PingSettings));
        using var writer = new StringWriter();
        serializer.Serialize(writer, settings);
        await File.WriteAllTextAsync(dialog.FileName, writer.ToString(), Encoding.UTF8);
        return true;
    }

    public async Task<PingSettings?> LoadAsync()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "XML settings (*.xml)|*.xml"
        };

        if (dialog.ShowDialog() != true)
            return null;

        await using var stream = File.OpenRead(dialog.FileName);
        if (stream.Length > MaximumSettingsFileBytes)
            throw new InvalidDataException("The settings file is too large.");

        using var reader = new StreamReader(stream);
        using var xmlReader = XmlReader.Create(reader, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        });

        var serializer = new XmlSerializer(typeof(PingSettings));
        return serializer.Deserialize(xmlReader) as PingSettings
            ?? throw new InvalidDataException("The settings file is empty or invalid.");
    }
}
