using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static linuxblox.core.AppSettings;

namespace linuxblox.core;

public record AppSettings(bool IsRobloxInstalled, string? RobloxBasePath)
{
    public static readonly AppSettings Default = new(false, null);
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
internal partial class AppSettingsContext : JsonSerializerContext
{
}

public static class SettingsService
{
    private static readonly string _configDirectory = GetConfigDirectory();
    private static readonly string _settingsFilePath = Path.Combine(_configDirectory, "settings.json");

    private static string GetConfigDirectory()
    {
        string? configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (!string.IsNullOrEmpty(configHome))
            return Path.Combine(configHome, "linuxblox");

        string? home = Environment.GetEnvironmentVariable("HOME");
        if (!string.IsNullOrEmpty(home))
            return Path.Combine(home, ".config", "linuxblox");
        
        throw new NotSupportedException("Could not determine configuration directory. Set XDG_CONFIG_HOME or HOME.");
    }

    public static AppSettings LoadSettings()
    {
        try
        {
            var json = File.ReadAllText(_settingsFilePath);
            return JsonSerializer.Deserialize(json, AppSettingsContext.Default.AppSettings) ?? Default;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return Default;
        }
    }

    public static void SaveSettings(AppSettings settings)
    {
        Directory.CreateDirectory(_configDirectory);
        var json = JsonSerializer.Serialize(settings, AppSettingsContext.Default.AppSettings);
        File.WriteAllText(_settingsFilePath, json);
    }

    public static async Task<AppSettings> LoadSettingsAsync()
    {
        try
        {
            await using var stream = new FileStream(_settingsFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            return await JsonSerializer.DeserializeAsync(stream, AppSettingsContext.Default.AppSettings) ?? Default;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return Default;
        }
    }

    public static async Task SaveSettingsAsync(AppSettings settings)
    {
        Directory.CreateDirectory(_configDirectory);
        await using var stream = new FileStream(_settingsFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
        await JsonSerializer.SerializeAsync(stream, settings, AppSettingsContext.Default.AppSettings);
    }
}
