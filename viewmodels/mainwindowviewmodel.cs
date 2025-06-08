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
using System.Text.Json.Serialization;
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
            
            InitializeCommand = ReactiveCommand.CreateFromTask(LoadSettingsFromFileAsync, scheduler: RxApp.TaskpoolScheduler);

            var canExecuteMainCommands = this.WhenAnyObservable(x => x.InitializeCommand.IsExecuting)
                                             .Select(isInitializing => !isInitializing && !string.IsNullOrEmpty(_soberConfigPath))
                                             .StartWith(false)
                                             .DistinctUntilChanged()
                                             .ObserveOn(RxApp.MainThreadScheduler);

            PlayCommand = ReactiveCommand.Create(PlayRoblox, canExecuteMainCommands);
            SaveFlagsCommand = ReactiveCommand.CreateFromTask(SaveChangesAsync, canExecuteMainCommands);

            var commandExecutionMessages = Observable.Merge(
                InitializeCommand.IsExecuting.Where(isExecuting => isExecuting).Select(_ => "Initializing..."),
                SaveFlagsCommand.IsExecuting.Where(isExecuting => isExecuting).Select(_ => "Saving..."),
                PlayCommand.IsExecuting.Where(isExecuting => isExecuting).Select(_ => "Launching Roblox via Sober...")
            );

            var commandResultMessages = Observable.Merge(
                InitializeCommand,
                SaveFlagsCommand.Select(_ => "Flags saved successfully to Sober config!")
            );

            var errorMessages = Observable.Merge(
                InitializeCommand.ThrownExceptions,
                SaveFlagsCommand.ThrownExceptions,
                PlayCommand.ThrownExceptions)
                .Select(ex => ex is System.ComponentModel.Win32Exception or FileNotFoundException 
                    ? $"Launch failed. Is 'flatpak' installed & Sober available? Error: {ex.Message}"
                    : $"Error: {ex.Message}");

            _statusMessage = Observable.Merge(commandExecutionMessages, commandResultMessages, errorMessages)
                .ObserveOn(RxApp.MainThreadScheduler)
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
            Flags.Add(new InputFlagViewModel { Name = "DFIntCanHideGuiGroupId", Description = "Set to a Group ID. If the user is a member of this group, enables: Ctrl+Shift+G (CoreGui), C (DevUI), B (3D GUI), N (Nameplates) to toggle visibility. Set to 0 to disable.", Value = "0" });
        }

        private async Task<string> LoadSettingsFromFileAsync()
        {
            if (string.IsNullOrEmpty(_soberConfigPath))
                return "Could not determine HOME directory. Cannot find Sober config.";

            if (!File.Exists(_soberConfigPath))
                return "Sober config not found. You can save changes to create it.";

            try
            {
                string jsonString = await File.ReadAllTextAsync(_soberConfigPath);
                if (string.IsNullOrWhiteSpace(jsonString))
                    return "Sober config is empty. Ready to save new settings.";

                JsonNode? configNode = JsonNode.Parse(jsonString);

                foreach (var flag in Flags) flag.IsEnabled = false;

                if (configNode?["FFlags"] is JsonObject fflags)
                {
                    foreach (var flag in Flags)
                    {
                        if (fflags.ContainsKey(flag.Name) && fflags[flag.Name] is { } flagNode)
                        {
                            flag.IsEnabled = true;
                            string value = flagNode.ToString();

                            if (flag is ToggleFlagViewModel toggleFlag)
                                toggleFlag.IsOn = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                            else if (flag is InputFlagViewModel inputFlag)
                                inputFlag.Value = value;
                        }
                    }
                }
                return "Sober config file loaded! Ready.";
            }
            catch (JsonException ex)
            {
                return $"Sober config is corrupt. You can save to overwrite it. Error: {ex.Message}";
            }
            catch (Exception ex)
            {
                return $"Error reading file: {ex.Message}";
            }
        }
        
        private void PlayRoblox()
        {
            Process.Start(new ProcessStartInfo("flatpak", "run org.vinegarhq.Sober") { UseShellExecute = false });
        }
        
        private async Task SaveChangesAsync()
        {
            if (string.IsNullOrEmpty(_soberConfigPath))
                throw new InvalidOperationException("Cannot save: Sober config path is not set.");

            JsonNode configNode;
            try
            {
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
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Could not read or parse existing config: {ex.Message}", ex);
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
                        fflags[flag.Name] = toggle.IsOn;
                    else if (flag is InputFlagViewModel input)
                        fflags[flag.Name] = int.TryParse(input.Value, out int intValue) ? JsonValue.Create(intValue) : JsonValue.Create(input.Value);
                }
                else
                {
                    fflags.Remove(flag.Name);
                }
            }
            
            var configDir = Path.GetDirectoryName(_soberConfigPath);
            if (!string.IsNullOrEmpty(configDir))
                Directory.CreateDirectory(configDir);

            var options = new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
            await File.WriteAllTextAsync(_soberConfigPath, configNode.ToJsonString(options));
        }
    }
}
