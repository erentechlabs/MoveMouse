using System.IO;
using System.Text.Json;
using MoveMouse.Models;

namespace MoveMouse.Services;

/// <summary>
/// JSON-file-backed <see cref="ISettingsService"/>. Settings live under
/// <c>%LOCALAPPDATA%\MoveMouse\settings.json</c>. Plain file I/O is used
/// (rather than the WinRT <c>ApplicationData</c> API) because Move Mouse
/// runs unpackaged and does not have an MSIX package identity.
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private readonly string _settingsFilePath;

    public SettingsService()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MoveMouse");

        Directory.CreateDirectory(folder);
        _settingsFilePath = Path.Combine(folder, "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(_settingsFilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions);
            return settings ?? new AppSettings();
        }
        catch (Exception)
        {
            // A corrupt or unreadable settings file should never prevent the
            // application from starting; fall back to defaults instead.
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, SerializerOptions);
            File.WriteAllText(_settingsFilePath, json);
        }
        catch (Exception)
        {
            // Persistence is best-effort: if the disk is unavailable or the
            // file is locked, the app should keep running with in-memory settings.
        }
    }
}
