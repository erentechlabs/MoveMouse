using MoveMouse.Models;

namespace MoveMouse.Services;

/// <summary>Loads and saves <see cref="AppSettings"/> to local application data.</summary>
public interface ISettingsService
{
    /// <summary>
    /// Loads settings from disk, returning fresh defaults if no settings
    /// file exists yet or if the existing file could not be read.
    /// </summary>
    AppSettings Load();

    /// <summary>Persists the given settings to disk, overwriting any previous file.</summary>
    void Save(AppSettings settings);
}
