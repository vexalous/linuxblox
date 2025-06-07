using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace linuxblox.viewmodels
{
    public class MainWindowViewModel : ReactiveObject
    {
        private readonly ObservableAsPropertyHelper<string> _statusMessage;
        public string StatusMessage => _statusMessage.Value;

        private readonly string? _soberConfigPath;
        public string ConfigFilePathText => $"Sober Config File: {_soberConfigPath ?? "Path not found"}";

        public ObservableCollection<FlagViewModel> Flags { get; } = new();

        public ReactiveCommand<Unit, string> InitializeCommand { get; }
        public ReactiveCommand<Unit, Unit> PlayCommand { get; }
        public ReactiveCommand<Unit, Unit> SaveFlagsCommand { get; }

        public MainWindowViewModel()
        {
            string? homeDir = Environment.GetEnvironmentVariable("HOME");
            if (!string.IsNullOrEmpty(homeDir))
            {
                _soberConfigPath = Path.Combine(homeDir, ".var", "app", "org.vinegarhq.Sober", "config", "sober", "config.json");
            }
            
            PopulateDefaultFlags();
            
            InitializeCommand = ReactiveCommand.CreateFromTask(LoadSettingsFromFileAsync);

            var canExecuteMainCommands = this.WhenAnyObservable(x => x.InitializeCommand.IsExecuting)
                                             .Select(isInitializing => !isInitializing && !string.IsNullOrEmpty(_soberConfigPath));

            PlayCommand = ReactiveCommand.Create(PlayRoblox, canExecuteMainCommands);
            SaveFlagsCommand = ReactiveCommand.CreateFromTask(SaveChangesAsync, canExecuteMainCommands);

            var initStarting = InitializeCommand.IsExecuting
                .Where(isExecuting => isExecuting)
                .Select(_ => "Initializing...");

            var saveStarting = SaveFlagsCommand.IsExecuting
                .Where(isExecuting => isExecuting)
                .Select(_ => "Saving...");
            
            var playStarting = PlayCommand.IsExecuting
                .Where(isExecuting => isExecuting)
                .Select(_ => "Launching Roblox via Sober...");

            var saveCompleted = SaveFlagsCommand
                .Select(_ => "Flags saved successfully to Sober config!");

            var errorMessages = Observable.Merge(
                InitializeCommand.ThrownExceptions,
                SaveFlagsCommand.ThrownExceptions,
                PlayCommand.ThrownExceptions)
                .Select(ex => $"Error: {ex.Message}");

            _statusMessage = Observable.Merge(
                    initStarting,
                    InitializeCommand,
                    saveStarting,
                    playStarting,
                    saveCompleted,
                    errorMessages)
                .ToProperty(this, vm => vm.StatusMessage, "Awaiting initialization...");

            InitializeCommand.Execute().Subscribe();
        }

        private void PopulateDefaultFlags()
        {
            Flags.Clear();
            Flags.Add(new InputFlagViewModel { Name = "DFIntTaskSchedulerTargetFps", Description = "FPS Limit", Value = "120" });
            Flags.Add(new ToggleFlagViewModel { Name = "FFlagDebugGraphicsPreferVulkan", Description = "Prefer Vulkan Renderer", IsOn = true });
            Flags.Add(new ToggleFlagViewModel { Name = "FFlagDebugGraphicsDisablePostFX", Description = "Disable Post-Processing Effects", IsOn = false });
            Flags.Add(new InputFlagViewModel { Name = "DFIntPostEffectQualityLevel", Description = "Post Effect Quality (0-4)", Value = "4" });
        }

        private async Task<string> LoadSettingsFromFileAsync()
        {
            if (string.IsNullOrEmpty(_soberConfigPath))
            {
                return "Could not determine HOME directory. Cannot find Sober config.";
            }

            if (!File.Exists(_soberConfigPath))
            {
                return "Sober config not found. You can still save changes to create it.";
            }

            try
            {
                string jsonString = await File.ReadAllTextAsync(_soberConfigPath);
                if (string.IsNullOrWhiteSpace(jsonString))
                {
                    return "Sober config is empty. Ready to save new settings.";
                }

                JsonNode? configNode = JsonNode.Parse(jsonString);

                if (configNode?["FFlags"] is JsonObject fflags)
                {
                    foreach (var flag in Flags)
                    {
                        if (fflags.TryGetValue(flag.Name, out var flagNode) && flagNode is not null)
                        {
                            flag.IsEnabled = true;
                            string value = flagNode.ToString();

                            if (flag is ToggleFlagViewModel toggleFlag)
                            {
                                toggleFlag.IsOn = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                            }
                            else if (flag is InputFlagViewModel inputFlag)
                            {
                                inputFlag.Value = value;
                            }
                        }
                    }
                }
                return "Sober config file loaded! Ready.";
            }
            catch (JsonException ex)
            {
                return $"Sober config is corrupt. You can save to overwrite it. Error: {ex.Message}";
            }
        }
        
        private void PlayRoblox()
        {
            Process.Start(new ProcessStartInfo("flatpak", "run org.vinegarhq.Sober") { UseShellExecute = false });
        }
        
        private async Task SaveChangesAsync()
        {
            if (string.IsNullOrEmpty(_soberConfigPath))
            {
                throw new InvalidOperationException("Cannot save: Sober config path is not set.");
            }

            JsonNode configNode;
            if (File.Exists(_soberConfigPath))
            {
                var existingJson = await File.ReadAllTextAsync(_soberConfigPath);
                configNode = string.IsNullOrWhiteSpace(existingJson) 
                    ? new JsonObject() 
                    : JsonNode.Parse(existingJson) ?? new JsonObject();
            }
            else
            {
                configNode = new JsonObject();
            }
            
            if (configNode["FFlags"] is not JsonObject fflags)
            {
                fflags = new JsonObject();
                configNode["FFlags"] = fflags;
            }
            
            foreach (var flag in Flags)
            {
                if (flag.IsEnabled)
                {
                    if (flag is ToggleFlagViewModel toggle)
                    {
                        fflags[flag.Name] = toggle.IsOn;
                    }
                    else if (flag is InputFlagViewModel input)
                    {
                        fflags[flag.Name] = int.TryParse(input.Value, out int intValue)
                            ? JsonValue.Create(intValue)
                            : JsonValue.Create(input.Value);
                    }
                }
                else
                {
                    fflags.Remove(flag.Name);
                }
            }

            var configDir = Path.GetDirectoryName(_soberConfigPath);
            if (!string.IsNullOrEmpty(configDir))
            {
                Directory.CreateDirectory(configDir);
            }

            var options = new JsonSerializerOptions { WriteIndented = true, };
            await File.WriteAllTextAsync(_soberConfigPath, configNode.ToJsonString(options));
        }
    }
}
