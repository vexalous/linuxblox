using Avalonia.Threading;
using linuxblox.core;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Input;

namespace linuxblox.viewmodels
{
    public class MainWindowViewModel : ReactiveObject
    {
        private string _statusMessage = "Initializing...";
        public string StatusMessage { get => _statusMessage; set => this.RaiseAndSetIfChanged(ref _statusMessage, value); }

        public string ConfigFilePathText => $"Sober Config File: {_soberConfigPath}";

        private bool _isPlayButtonEnabled;
        public bool IsPlayButtonEnabled { get => _isPlayButtonEnabled; set => this.RaiseAndSetIfChanged(ref _isPlayButtonEnabled, value); }
        
        private bool _isSaveButtonEnabled;
        public bool IsSaveButtonEnabled { get => _isSaveButtonEnabled; set => this.RaiseAndSetIfChanged(ref _isSaveButtonEnabled, value); }

        public ObservableCollection<FlagViewModel> Flags { get; }
        public ICommand PlayCommand { get; }
        public ICommand SaveFlagsCommand { get; }

        private readonly string _soberConfigPath;

        public MainWindowViewModel()
        {
            Flags = new ObservableCollection<FlagViewModel>();
            
            string homeDir = Environment.GetEnvironmentVariable("HOME")!;
            _soberConfigPath = Path.Combine(homeDir, ".var", "app", "org.vinegarhq.Sober", "config", "sober", "config.json");

            PlayCommand = new DelegateCommand(PlayRoblox);
            SaveFlagsCommand = new DelegateCommand(SaveChanges);
            
            Initialize();
        }

        private void Initialize()
        {
            PopulateDefaultFlags();
            LoadSettingsFromFile();
        }

        private void PopulateDefaultFlags()
        {
            Flags.Clear();
            Flags.Add(new InputFlagViewModel { Name = "DFIntTaskSchedulerTargetFps", Description = "FPS Limit", Value = "120" });
            Flags.Add(new ToggleFlagViewModel { Name = "FFlagDebugGraphicsPreferVulkan", Description = "Prefer Vulkan Renderer", IsOn = true });
            Flags.Add(new ToggleFlagViewModel { Name = "FFlagDebugGraphicsDisablePostFX", Description = "Disable Post-Processing Effects", IsOn = false });
            Flags.Add(new InputFlagViewModel {Name = "DFIntPostEffectQualityLevel", Description = "Post Effect Quality (0-4)", Value = "4"});
        }

        private void LoadSettingsFromFile()
        {
            Task.Run(() =>
            {
                if (!File.Exists(_soberConfigPath))
                {
                    Dispatcher.UIThread.Post(() => {
                        StatusMessage = "Sober config not found. Please run Sober once to create it.";
                        IsPlayButtonEnabled = true;
                        IsSaveButtonEnabled = false;
                    });
                    return;
                }
                
                Dispatcher.UIThread.Post(() => {
                    StatusMessage = "Sober config file found! Ready.";
                    IsPlayButtonEnabled = true;
                    IsSaveButtonEnabled = true;
                });

                try
                {
                    var jsonString = File.ReadAllText(_soberConfigPath);
                    var configNode = JsonNode.Parse(jsonString);
                    if (configNode?["FFlags"] is not JsonObject fflags)
                    {
                        return;
                    }

                    Dispatcher.UIThread.Post(() =>
                    {
                        foreach (var flag in Flags)
                        {
                            if (fflags.ContainsKey(flag.Name) && fflags[flag.Name] != null)
                            {
                                flag.IsEnabled = true;
                                var value = fflags[flag.Name]!.ToString();
                                
                                if (flag is ToggleFlagViewModel toggleFlag) { toggleFlag.IsOn = value.Equals("true", StringComparison.OrdinalIgnoreCase); }
                                else if (flag is InputFlagViewModel inputFlag) { inputFlag.Value = value; }
                            }
                        }
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.UIThread.Post(() => StatusMessage = $"Error reading Sober config: {ex.Message}");
                }
            });
        }
        
        private void PlayRoblox()
        {
            StatusMessage = "Launching Roblox via Sober...";
            try { Process.Start(new ProcessStartInfo("flatpak", "run org.vinegarhq.Sober") { UseShellExecute = false }); }
            catch (Exception ex) { StatusMessage = $"Launch failed. Is the Sober Flatpak installed correctly? Error: {ex.Message}"; }
        }
        
        private void SaveChanges()
        {
            try
            {
                var jsonString = File.Exists(_soberConfigPath) ? File.ReadAllText(_soberConfigPath) : "{}";
                var configNode = JsonNode.Parse(jsonString) ?? new JsonObject();
                
                if (configNode["FFlags"] is not JsonObject fflags)
                {
                    fflags = new JsonObject();
                    configNode["FFlags"] = fflags;
                }
                
                foreach (var flag in Flags)
                {
                    if (flag.IsEnabled)
                    {
                        if (flag is ToggleFlagViewModel toggle) fflags[flag.Name] = JsonValue.Create(toggle.IsOn);
                        else if (flag is InputFlagViewModel input) fflags[flag.Name] = JsonValue.Create(input.Value);
                    }
                    else
                    {
                        fflags.Remove(flag.Name);
                    }
                }

                var configDir = Path.GetDirectoryName(_soberConfigPath);
                if (!string.IsNullOrEmpty(configDir)) Directory.CreateDirectory(configDir);

                File.WriteAllText(_soberConfigPath, configNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                StatusMessage = "Flags saved successfully to Sober config!";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error saving flags: {ex.Message}";
            }
        }
    }
}
