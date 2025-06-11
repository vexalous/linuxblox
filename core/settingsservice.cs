// FILE: core/settingsservice.cs

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static LinuxBlox.Core.AppSettings;

namespace LinuxBlox.Core;

public record AppSettings(bool IsRobloxInstalled, string? RobloxBasePath)
{
    public static readonly AppSettings Default = new(false, null);
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class AppSettingsContext : JsonSerializerContext
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
            return Path.Combine(configHome, "linuxblox"); // Note: This could be a future analyzer warning, but won't break the build.

        string? home = Environment.GetEnvironmentVariable("HOME");
        if (!string.IsNullOrEmpty(home))
            return Path.Combine(home, ".config", "linuxblox"); // Same as above.
        
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
            var stream = new FileStream(_settingsFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            try
            {
                return await JsonSerializer.DeserializeAsync<AppSettings>(stream, AppSettingsContext.Default.AppSettings).ConfigureAwait(false) ?? Default;
            }
            finally
            {
                await stream.DisposeAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return Default;
        }
    }

    public static async Task SaveSettingsAsync(AppSettings settings)
    {
        Directory.CreateDirectory(_configDirectory);
        var stream = new FileStream(_settingsFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
        try
        {
            await JsonSerializer.SerializeAsync(stream, settings, AppSettingsContext.Default.AppSettings).ConfigureAwait(false);
        }
        finally
        {
            await stream.DisposeAsync().ConfigureAwait(false);
        }
    }
}
