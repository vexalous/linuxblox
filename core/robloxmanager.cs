using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace linuxblox.core
{
    public static class RobloxManager
    {
        private static readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions { WriteIndented = true };

        public static (string? Path, string Log) GetClientSettingsPath(string? basePath)
        {
            var log = new StringBuilder();
            log.AppendLine("--- Begin Sober Path Log ---");

            try
            {
                var homeDir = Environment.GetEnvironmentVariable("HOME");
                if (string.IsNullOrEmpty(homeDir))
                {
                    log.AppendLine("[FAIL] Cannot determine HOME directory.");
                    return (null, log.ToString());
                }

                var pathExe = Path.Combine(homeDir, ".var/app/org.vinegarhq.Sober/data/sober/exe/ClientSettings");
                var pathAppData = Path.Combine(homeDir, ".var/app/org.vinegarhq.Sober/data/sober/appData/ClientSettings");

                log.AppendLine(string.Format(CultureInfo.InvariantCulture, $"Checking primary discovered path: '{pathAppData}'"));
                if (Directory.Exists(pathAppData))
                {
                    var finalPath = Path.Combine(pathAppData, "ClientAppSettings.json");
                    log.AppendLine(string.Format(CultureInfo.InvariantCulture, $"[SUCCESS] Found settings directory at AppData path: '{finalPath}'"));
                    return (finalPath, log.ToString());
                }

                log.AppendLine(string.Format(CultureInfo.InvariantCulture, $"Checking secondary discovered path: '{pathExe}'"));
                if (Directory.Exists(pathExe))
                {
                    var finalPath = Path.Combine(pathExe, "ClientAppSettings.json");
                    log.AppendLine(string.Format(CultureInfo.InvariantCulture, $"[SUCCESS] Found settings directory at exe path: '{finalPath}'"));
                    return (finalPath, log.ToString());
                }

                log.AppendLine("[FAIL] Neither of the discovered ClientSettings directories exist. Cannot determine where to save settings.");
                log.AppendLine(string.Format(CultureInfo.InvariantCulture, $"Creating primary target directory: '{pathAppData}'"));
                Directory.CreateDirectory(pathAppData);
                var createdPath = Path.Combine(pathAppData, "ClientAppSettings.json");
                log.AppendLine(string.Format(CultureInfo.InvariantCulture, $"[SUCCESS] Created new settings directory. Final path is: '{createdPath}'"));
                return (createdPath, log.ToString());
            }
            catch (IOException ex)
            {
                log.AppendLine(string.Format(CultureInfo.InvariantCulture, $"[IO FAIL] An IO error occurred while trying to access/create settings directory: {ex.Message}"));
                return (null, log.ToString());
            }
            catch (UnauthorizedAccessException ex)
            {
                log.AppendLine(string.Format(CultureInfo.InvariantCulture, $"[AUTH FAIL] An authorization error occurred while trying to access/create settings directory: {ex.Message}"));
                return (null, log.ToString());
            }
            catch (Exception ex)
            {
                log.AppendLine(string.Format(CultureInfo.InvariantCulture, $"[CRITICAL FAIL] An unexpected error occurred: {ex.Message}"));
                throw; // Rethrowing unexpected exceptions
            }
        }

        public static Dictionary<string, string> ReadFlags(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return new Dictionary<string, string>();
            try
            {
                var jsonString = File.ReadAllText(path);
                var rawFlags = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonString) ?? new();
                return rawFlags.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString() ?? "");
            }
            catch (JsonException)
            {
                return new Dictionary<string, string>();
            }
            catch (IOException)
            {
                return new Dictionary<string, string>();
            }
            catch (Exception)
            {
                throw;
            }
        }

        public static void WriteFlags(string path, Dictionary<string, string> flags)
        {
            if (string.IsNullOrEmpty(path)) return;
            var jsonString = JsonSerializer.Serialize(flags, _jsonSerializerOptions);
            File.WriteAllText(path, jsonString);
        }
    }
}
