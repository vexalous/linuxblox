using System;
using System.IO;
using System.Text.Json;

namespace linuxblox.core
{
    public record AppSettings(bool IsRobloxInstalled, string? RobloxBasePath);

    public static class SettingsService
    {
        private static readonly string _configDirectory;
        private static readonly string _settingsFilePath;

        static SettingsService()
        {
            string configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") 
                                ?? Path.Combine(Environment.GetEnvironmentVariable("HOME")!, ".config");

            _configDirectory = Path.Combine(configHome, "linuxblox");
            _settingsFilePath = Path.Combine(_configDirectory, "settings.json");
        }

        public static AppSettings LoadSettings()
        {
            if (!File.Exists(_settingsFilePath))
            {
                return new AppSettings(false, null);
            }
            try
            {
                var json = File.ReadAllText(_settingsFilePath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings(false, null);
            }
            catch { return new AppSettings(false, null); }
        }

        public static void SaveSettings(AppSettings settings)
        {
            Directory.CreateDirectory(_configDirectory);
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsFilePath, json);
        }
    }
}
