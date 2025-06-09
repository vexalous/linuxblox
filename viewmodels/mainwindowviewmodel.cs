using Avalonia.ReactiveUI;
using Avalonia.Threading;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace linuxblox.viewmodels
{
    public class MainWindowViewModel : ReactiveObject, IActivatableViewModel
    {
        public ViewModelActivator Activator { get; }

        private readonly ObservableAsPropertyHelper<bool> _isInitialized;
        public bool IsInitialized => _isInitialized.Value;

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
            Activator = new ViewModelActivator();

            var homeDir = Environment.GetEnvironmentVariable("HOME");
            if (!string.IsNullOrEmpty(homeDir))
            {
                _soberConfigPath = Path.Combine(homeDir, ".var", "app", "org.vinegarhq.Sober", "config", "sober", "config.json");
            }

            PopulateDefaultFlags();

            InitializeCommand = ReactiveCommand.CreateFromTask(LoadSettingsFromFileAsync, outputScheduler: AvaloniaScheduler.Instance);

            _isInitialized = InitializeCommand.Select(_ => true)
                                              .StartWith(false)
                                              .DistinctUntilChanged()
                                              .ToProperty(this, x => x.IsInitialized);

            var canExecuteMainCommands = this.WhenAnyValue(x => x.IsInitialized)
                                             .Select(isInitialized => isInitialized && !string.IsNullOrEmpty(_soberConfigPath))
                                             .ObserveOn(AvaloniaScheduler.Instance);

            PlayCommand = ReactiveCommand.Create(PlayRoblox, canExecuteMainCommands, outputScheduler: AvaloniaScheduler.Instance);
            SaveFlagsCommand = ReactiveCommand.CreateFromTask(SaveChangesAsync, canExecuteMainCommands, outputScheduler: AvaloniaScheduler.Instance);

            _statusMessage = CreateStatusMessageObservable(InitializeCommand, SaveFlagsCommand, PlayCommand)
                             .ToProperty(this, vm => vm.StatusMessage, "Awaiting initialization...");

            this.WhenActivated(disposables =>
            {
                InitializeCommand.Execute()
                                 .Subscribe()
                                 .DisposeWith(disposables);
            });
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

            return Observable.Merge(execution, results, errors).ObserveOn(AvaloniaScheduler.Instance);
        }

        private void PopulateDefaultFlags()
        {
            Flags.Clear();
            Flags.Add(new InputFlagViewModel { Name = "DFIntTaskSchedulerTargetFps", Description = "FPS Limit", Value = "144" });
            Flags.Add(new ToggleFlagViewModel { Name = "FFlagDebugGraphicsPreferVulkan", Description = "Prefer Vulkan Renderer", IsOn = true });
            Flags.Add(new ToggleFlagViewModel { Name = "FFlagDebugGraphicsDisablePostFX", Description = "Disable Post-Processing Effects", IsOn = false });
            Flags.Add(new InputFlagViewModel { Name = "DFIntPostEffectQualityLevel", Description = "Post Effect Quality (0-4)", Value = "4" });
            Flags.Add(new InputFlagViewModel { Name = "DFIntCanHideGuiGroupId", Description = "Set to a Group ID to enable visibility toggles (Ctrl+Shift+G, etc). Set to 0 to disable.", Value = "0" });
            Flags.Add(new ToggleFlagViewModel { Name = "FFlagDisableNewIGMinDUA", Description = "Disable New In-Game Menu (Reverts to Old Menu)", IsOn = false });
            Flags.Add(new ToggleFlagViewModel { Name = "FFlagEnableInGameMenuControls", Description = "Enable 'Controls' Button in In-Game Menu", IsOn = false });
        }

        private async Task<string> LoadSettingsFromFileAsync()
        {
            if (string.IsNullOrEmpty(_soberConfigPath) || !File.Exists(_soberConfigPath)) return "Sober config not found.";

            try
            {
                File.SetAttributes(_soberConfigPath, FileAttributes.Normal);
                string jsonString = await File.ReadAllTextAsync(_soberConfigPath);
                if (string.IsNullOrWhiteSpace(jsonString)) return "Sober config is empty.";

                JsonNode? configNode = JsonNode.Parse(jsonString);
                if (configNode?["fflags"] is not JsonObject fflags) return "Sober config loaded, but no 'fflags' section found.";

                var loadedFlagsData = fflags.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.ToString());

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    foreach (var flag in Flags)
                    {
                        if (loadedFlagsData.TryGetValue(flag.Name, out var value) && value != null)
                        {
                            flag.IsEnabled = true;
                            if (flag is ToggleFlagViewModel toggleFlag)
                                toggleFlag.IsOn = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                            else if (flag is InputFlagViewModel inputFlag)
                                inputFlag.Value = value;
                        } else { flag.IsEnabled = false; }
                    }
                });
                return "Sober config file loaded successfully.";
            }
            catch (Exception ex) { return $"Error reading Sober config: {ex.Message}"; }
        }

        private void PlayRoblox()
        {
            try { Process.Start(new ProcessStartInfo("flatpak", "run org.vinegarhq.Sober") { UseShellExecute = false }); }
            catch (Exception) { throw; }
        }

        private async Task<JsonNode> LoadOrCreateConfigNodeAsync()
        {
            if (string.IsNullOrEmpty(_soberConfigPath)) return new JsonObject();
            if (!File.Exists(_soberConfigPath)) return new JsonObject();
            try
            {
                File.SetAttributes(_soberConfigPath, FileAttributes.Normal);
                var json = await File.ReadAllTextAsync(_soberConfigPath);
                return string.IsNullOrWhiteSpace(json) ? new JsonObject() : JsonNode.Parse(json) ?? new JsonObject();
            }
            catch (Exception ex) { throw new InvalidOperationException($"Could not read config: {_soberConfigPath}", ex); }
        }

        private async Task SaveChangesAsync()
        {
            if (string.IsNullOrEmpty(_soberConfigPath)) throw new InvalidOperationException("Config path not set.");

            var configNode = await LoadOrCreateConfigNodeAsync();

            if (configNode is JsonObject obj)
            {
                obj.Remove("FFlags");
            }
            
            if (configNode["fflags"] is not JsonObject fflags)
            {
                fflags = new JsonObject();
                configNode["fflags"] = fflags;
            }

            foreach (var flag in Flags.Where(f => f.IsEnabled))
            {
                if (flag is ToggleFlagViewModel toggle)
                    fflags[flag.Name] = toggle.IsOn.ToString().ToLower();
                else if (flag is InputFlagViewModel input)
                    fflags[flag.Name] = input.Value;
            }

            foreach (var flag in Flags.Where(f => !f.IsEnabled))
            {
                fflags.Remove(flag.Name);
            }

            var configDir = Path.GetDirectoryName(_soberConfigPath);
            if (!string.IsNullOrEmpty(configDir)) Directory.CreateDirectory(configDir);

            var options = new JsonSerializerOptions { WriteIndented = true };
            await File.WriteAllTextAsync(_soberConfigPath, configNode.ToJsonString(options));
        }
    }
}
