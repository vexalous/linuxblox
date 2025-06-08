using Avalonia.Threading;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
            var homeDir = Environment.GetEnvironmentVariable("HOME");
            if (!string.IsNullOrEmpty(homeDir))
            {
                _soberConfigPath = Path.Combine(homeDir, ".var", "app", "org.vinegarhq.Sober", "config", "sober", "config.json");
            }

            PopulateDefaultFlags();

            InitializeCommand = ReactiveCommand.CreateFromTask(LoadSettingsFromFileAsync);

            var canExecuteMainCommands = this.WhenAnyObservable(x => x.InitializeCommand.IsExecuting)
                                             .Select(isInitializing => !isInitializing && !string.IsNullOrEmpty(_soberConfigPath))
                                             .StartWith(false)
                                             .DistinctUntilChanged()
                                             .ObserveOn(RxApp.MainThreadScheduler);

            PlayCommand = ReactiveCommand.Create(PlayRoblox, canExecuteMainCommands);
            SaveFlagsCommand = ReactiveCommand.CreateFromTask(SaveChangesAsync, canExecuteMainCommands);

            _statusMessage = CreateStatusMessageObservable(InitializeCommand, SaveFlagsCommand, PlayCommand)
                             .ToProperty(this, vm => vm.StatusMessage, "Awaiting initialization...");

            InitializeCommand.Execute().Subscribe();
        }

        private static IObservable<string> CreateStatusMessageObservable(
            ReactiveCommand<Unit, string> initialize,
            ReactiveCommand<Unit, Unit> save,
            ReactiveCommand<Unit, Unit> play)
        {
            var execution = Observable.Merge(
                initialize.IsExecuting.Where(isExecuting => isExecuting).Select(_ => "Initializing..."),
                save.IsExecuting.Where(isExecuting => isExecuting).Select(_ => "Saving flags..."),
                play.IsExecuting.Where(isExecuting => isExecuting).Select(_ => "Launching Roblox via Sober...")
            );

            var results = Observable.Merge(
                initialize,
                save.Select(_ => "Flags saved successfully to Sober config!")
            );

            var errors = Observable.Merge(
                initialize.ThrownExceptions,
                save.ThrownExceptions,
                play.ThrownExceptions)
                .Select(ex => ex is Win32Exception or FileNotFoundException
                    ? $"Launch failed. Is 'flatpak' installed & Sober available? Error: {ex.Message}"
                    : $"Error: {ex.Message}");

            return Observable.Merge(execution, results, errors).ObserveOn(RxApp.MainThreadScheduler);
        }

        private void PopulateDefaultFlags()
        {
            Flags.Clear();
            Flags.Add(new InputFlagViewModel { Name = "DFIntTaskSchedulerTargetFps", Description = "FPS Limit", Value = "120" });
            Flags.Add(new ToggleFlagViewModel { Name = "FFlagDebugGraphicsPreferVulkan", Description = "Prefer Vulkan Renderer", IsOn = true });
            Flags.Add(new ToggleFlagViewModel { Name = "FFlagDebugGraphicsDisablePostFX", Description = "Disable Post-Processing Effects", IsOn = false });
            Flags.Add(new InputFlagViewModel { Name = "DFIntPostEffectQualityLevel", Description = "Post Effect Quality (0-4)", Value = "4" });
            Flags.Add(new InputFlagViewModel { Name = "DFIntCanHideGuiGroupId", Description = "Set to a Group ID to enable visibility toggles (Ctrl+Shift+G, etc). Set to 0 to disable.", Value = "0" });
        }

        // --- MODIFIED METHOD ---
        // This method now performs file I/O on a background thread and then safely
        // dispatches the UI updates to the main thread to prevent cross-thread exceptions.
        private async Task<string> LoadSettingsFromFileAsync()
        {
            if (string.IsNullOrEmpty(_soberConfigPath)) return "Could not determine HOME directory. Cannot find Sober config.";
            if (!File.Exists(_soberConfigPath)) return "Sober config not found. You can save changes to create it.";

            try
            {
                // 1. Do all file I/O and parsing on the background thread.
                string jsonString = await File.ReadAllTextAsync(_soberConfigPath);
                if (string.IsNullOrWhiteSpace(jsonString)) return "Sober config is empty. Ready to save new settings.";

                JsonNode? configNode = JsonNode.Parse(jsonString);
                if (configNode?["FFlags"] is not JsonObject fflags) return "Sober config loaded, but no flags are present.";

                // 2. Prepare the data in a temporary, non-UI object.
                var loadedFlagsData = fflags.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value?.ToString()
                );

                // 3. Switch to the UI thread to safely update the UI-bound collection.
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    foreach (var flag in Flags)
                    {
                        if (loadedFlagsData.TryGetValue(flag.Name, out var value) && value != null)
                        {
                            flag.IsEnabled = true;
                            if (flag is ToggleFlagViewModel toggleFlag)
                            {
                                toggleFlag.IsOn = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                            }
                            else if (flag is InputFlagViewModel inputFlag)
                            {
                                inputFlag.Value = value;
                            }
                        }
                        else
                        {
                            flag.IsEnabled = false;
                        }
                    }
                });

                return "Sober config file loaded successfully.";
            }
            catch (JsonException ex) { return $"Sober config is corrupt. Save to overwrite it. Error: {ex.Message}"; }
            catch (Exception ex) { return $"Error reading Sober config file: {ex.Message}"; }
        }

        private void PlayRoblox()
        {
            try
            {
                 Process.Start(new ProcessStartInfo("flatpak", "run org.vinegarhq.Sober") { UseShellExecute = false });
            }
            catch (Exception)
            {
                // Re-throwing the exception will allow the ReactiveCommand's ThrownExceptions observable to catch it.
                throw;
            }
        }

        private async Task<JsonNode> LoadOrCreateConfigNodeAsync()
        {
            if (string.IsNullOrEmpty(_soberConfigPath) || !File.Exists(_soberConfigPath)) return new JsonObject();

            try
            {
                var json = await File.ReadAllTextAsync(_soberConfigPath);
                return string.IsNullOrWhiteSpace(json)
                    ? new JsonObject()
                    : JsonNode.Parse(json) ?? new JsonObject();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Could not read or parse config file: {_soberConfigPath}", ex);
            }
        }

        private async Task SaveChangesAsync()
        {
            if (string.IsNullOrEmpty(_soberConfigPath))
                throw new InvalidOperationException("Cannot save: Sober config path is not set.");

            var configNode = await LoadOrCreateConfigNodeAsync();

            if (configNode["FFlags"] is not JsonObject fflags)
            {
                fflags = new JsonObject();
                configNode["FFlags"] = fflags;
            }

            foreach (var flag in Flags.Where(f => f.IsEnabled))
            {
                if (flag is ToggleFlagViewModel toggle)
                    fflags[flag.Name] = toggle.IsOn;
                else if (flag is InputFlagViewModel input)
                    fflags[flag.Name] = int.TryParse(input.Value, out int intValue) ? JsonValue.Create(intValue) : JsonValue.Create(input.Value);
            }

            foreach (var flag in Flags.Where(f => !f.IsEnabled))
            {
                fflags.Remove(flag.Name);
            }

            var configDir = Path.GetDirectoryName(_soberConfigPath);
            if (!string.IsNullOrEmpty(configDir))
                Directory.CreateDirectory(configDir);

            var options = new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
            await File.WriteAllTextAsync(_soberConfigPath, configNode.ToJsonString(options));
        }
    }

    // NOTE: It is assumed FlagViewModel, InputFlagViewModel, and ToggleFlagViewModel
    // are defined in another file and are also inheriting from ReactiveObject
    // for bindings to work correctly. For example:
    //
    // public abstract class FlagViewModel : ReactiveObject
    // {
    //     [Reactive] public bool IsEnabled { get; set; }
    //     public string Name { get; set; }
    //     public string Description { get; set; }
    // }
}
