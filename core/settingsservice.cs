using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace linuxblox.core;

public record AppSettings(bool IsRobloxInstalled, string? RobloxBasePath);

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
        string? home = Environment.GetEnvironmentVariable("HOME");
        string? configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");

        if (!string.IsNullOrEmpty(configHome))
        {
            return Path.Combine(configHome, "linuxblox");
        }
        
        if (!string.IsNullOrEmpty(home))
        {
            return Path.Combine(home, ".config", "linuxblox");
        }

        throw new NotSupportedException("Could not determine configuration directory. Set the XDG_CONFIG_HOME or HOME environment variables.");
    }

    public static AppSettings LoadSettings()
    {
        try
        {
            var json = File.ReadAllText(_settingsFilePath);
            return JsonSerializer.Deserialize(json, AppSettingsContext.Default.AppSettings) ?? default;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return default;
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
            var json = await File.ReadAllTextAsync(_settingsFilePath);
            return JsonSerializer.Deserialize(json, AppSettingsContext.Default.AppSettings) ?? default;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return default;
        }
    }

    public static async Task SaveSettingsAsync(AppSettings settings)
    {
        Directory.CreateDirectory(_configDirectory);
        await using var fileStream = new FileStream(_settingsFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
        await JsonSerializer.SerializeAsync(fileStream, settings, AppSettingsContext.Default.AppSettings);
    }
}
