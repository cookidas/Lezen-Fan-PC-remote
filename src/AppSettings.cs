using System.Text.Json;

namespace LezenTray;

public sealed record FanDevice(string Id, string Name);

public sealed class AppSettings
{
    public List<FanDevice> Devices { get; set; } = [];
    public string? SelectedId { get; set; }
    public string Language { get; set; } = Strings.DefaultLanguage;
    // The prototype's scanned Bluetooth addresses are not fan registration IDs.
    internal static string StoragePath => Path.Combine(AppContext.BaseDirectory, "data", "fans.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        if (!File.Exists(StoragePath))
        {
            var fresh = new AppSettings();
            Strings.Language = fresh.Language;
            return fresh;
        }
        var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(StoragePath))
            ?? throw new InvalidDataException(Strings.T("err.settings_unreadable"));
        settings.Validate();
        Strings.Language = settings.Language;
        return settings;
    }

    internal void Validate()
    {
        if (Devices is null || Devices.Any(d => d is null || !FanProtocol.IsValidId(d.Id)
            || string.IsNullOrWhiteSpace(d.Name) || d.Name.Length > 60)
            || Devices.Select(d => d.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Devices.Count)
            throw new InvalidDataException(Strings.T("err.settings_invalid"));
        Devices = Devices.Select(d => d with { Id = d.Id.ToUpperInvariant() }).ToList();
        SelectedId = SelectedId?.ToUpperInvariant();
        if (SelectedId is not null && !Devices.Any(d => d.Id == SelectedId))
            SelectedId = null;
        if (Language != "ko") Language = "en";
    }

    public void Save()
    {
        Validate();
        Directory.CreateDirectory(Path.GetDirectoryName(StoragePath)!);
        var temp = StoragePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, JsonOptions));
        File.Move(temp, StoragePath, true);
    }
}
