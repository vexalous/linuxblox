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
            PopulateCoreFlags();
            PopulateRenderingApiFlags();
            PopulateLightningTechFlags();
            PopulateGraphicalSettingsFlags();
            PopulateMenuSettingsFlags();
            PopulateUserInterfaceFlags();
            // Add calls to other categories here if they are created (e.g., Network, Animation, Audio)
        }

        private void PopulateCoreFlags()
        {
            Flags.Add(new InputFlagViewModel { Name = "DFIntTaskSchedulerTargetFps", Description = "FPS Limit", Value = "120" });
            Flags.Add(new ToggleFlagViewModel { Name = "FFlagDebugGraphicsPreferVulkan", Description = "Prefer Vulkan Renderer", IsOn = true });
            Flags.Add(new InputFlagViewModel { Name = "DFIntPostEffectQualityLevel", Description = "Post Effect Quality (0-4)", Value = "4" });
            Flags.Add(new InputFlagViewModel { Name = "DFIntCanHideGuiGroupId", Description = "Set to a Group ID to enable visibility toggles (Ctrl+Shift+G, etc). Set to 0 to disable.", Value = "0" });
        }

        private void PopulateRenderingApiFlags()
        {
            Flags.Add(new ToggleFlagViewModel { Name = "FFlagDebugGraphicsDisableDirect3D11", Description = "Direct3D 11: Disable Direct3D 11", IsOn = false });
            Flags.Add(new ToggleFlagViewModel { Name = "FFlagDebugGraphicsPreferD3D11", Description = "Direct3D 11: Prefer Direct3D 11", IsOn = true });
            Flags.Add(new ToggleFlagViewModel { Name = "FFlagDebugGraphicsPreferD3D11FL10", Description = "Direct3D 10: Prefer D3D11 FL10", IsOn = true });
            Flags.Add(new ToggleFlagViewModel { Name = "FFlagGraphicsEnableD3D10Compute", Description = "Direct3D 10: Enable D3D10 Compute", IsOn = true });
            Flags.Add(new ToggleFlagViewModel { Name = "FFlagRenderVulkanFixMinimizeWindow", Description = "Vulkan: Fix Minimize Window", IsOn = true });
            Flags.Add(new ToggleFlagViewModel { Name = "FFlagDebugGraphicsPreferOpenGL", Description = "OpenGL: Prefer OpenGL", IsOn = true });
            Flags.Add(new ToggleFlagViewModel { Name = "FFlagDebugGraphicsPreferMetal", Description = "Metal (MacOS Only): Prefer Metal", IsOn = true });
        }

        private void PopulateLightningTechFlags()
        {
            Flags.Add(new ToggleFlagViewModel { Name = "DFFlagDebugRenderForceTechnologyVoxel", Description = "Voxel (Phase 1) Lighting", IsOn = true });
            Flags.Add(new ToggleFlagViewModel { Name = "FFlagDebugForceFutureIsBrightPhase2", Description = "Shadow Map (Phase 2) Lighting", IsOn = true });
            Flags.Add(new ToggleFlagViewModel { Name = "FFlagDebugForceFutureIsBrightPhase3", Description = "Future (Phase 3) Lighting", IsOn = true });
        }

        private void PopulateGraphicalSettingsFlags()
        {
            Flags.Add(new ToggleFlagViewModel { Name = "DFFlagDebugPerfMode", Description = "Performance Fast Flag", IsOn = true });
            Flags.Add(new InputFlagViewModel { Name = "FIntDebugForceMSAASamples", Description = "Force MSAA Samples (0,1,2,4,8)", Value = "1" });
            Flags.Add(new ToggleFlagViewModel { Name = "DFFlagDebugOverrideDPIScale", Description = "Preserve Rendering Quality With Display Scaling", IsOn = true });
            Flags.Add(new ToggleFlagViewModel { Name = "DFFlagDisableDPIScale", Description = "Disable Display Scaling (Alternative to Override)", IsOn = false });
            // Note: The original list had FFlagDisablePostFx. The existing code has FFlagDebugGraphicsDisablePostFX.
            // We are keeping the existing FFlagDebugGraphicsDisablePostFX and adding FFlagDisablePostFx as a new, distinct flag if its behavior is different.
            // If FFlagDisablePostFx is intended to replace FFlagDebugGraphicsDisablePostFX, manual deduplication might be needed later if they are exact synonyms.
            // For now, adding the one from the list:
            Flags.Add(new ToggleFlagViewModel { Name = "FFlagDisablePostFx", Description = "Disable Unnecessary Effects (PostFx)", IsOn = true });
            Flags.Add(new ToggleFlagViewModel { Name = "FFlagUserHideCharacterParticlesInFirstPerson", Description = "Reduced Avatar Item Particle in FP", IsOn = true });
            Flags.Add(new ToggleFlagViewModel { Name = "FFlagDebugDeterministicParticles", Description = "Max Graphics Quality Particles (May speed up)", IsOn = true });
            Flags.Add(new InputFlagViewModel { Name = "FIntRenderShadowIntensity", Description = "Disable Player Shadows (Set to 0)", Value = "0" });
            Flags.Add(new InputFlagViewModel { Name = "FIntRenderShadowmapBias", Description = "ShadowMap Bias (Future & ShadowMap only)", Value = "75" });
            Flags.Add(new InputFlagViewModel { Name = "DFIntCullFactorPixelThresholdShadowMapHighQuality", Description = "Reduce Shadows (High Quality Threshold)", Value = "2147483647" });
            Flags.Add(new InputFlagViewModel { Name = "DFIntCullFactorPixelThresholdShadowMapLowQuality", Description = "Reduce Shadows (Low Quality Threshold)", Value = "2147483647" });
            Flags.Add(new ToggleFlagViewModel { Name = "DFFlagDebugPauseVoxelizer", Description = "Pause Voxelizer/Disable Baked Shadows (Voxel Only)", IsOn = true });
            Flags.Add(new InputFlagViewModel { Name = "FIntFRMMinGrassDistance", Description = "Remove Grass (Min Distance)", Value = "0" });
            Flags.Add(new InputFlagViewModel { Name = "FIntFRMMaxGrassDistance", Description = "Remove Grass (Max Distance)", Value = "0" });
            Flags.Add(new InputFlagViewModel { Name = "FIntRenderGrassDetailStrands", Description = "Remove Grass (Detail Strands)", Value = "0" });
            Flags.Add(new InputFlagViewModel { Name = "FIntTerrainArraySliceSize", Description = "Low Quality Terrain Textures (0-4 Low, 16-64 High)", Value = "0" });
            Flags.Add(new ToggleFlagViewModel { Name = "DFFlagTextureQualityOverrideEnabled", Description = "Force Texture Quality: Enabled", IsOn = true });
            Flags.Add(new InputFlagViewModel { Name = "DFIntTextureQualityOverride", Description = "Force Texture Quality: Value (0-3)", Value = "3" });
        }

        private void PopulateMenuSettingsFlags()
        {
            Flags.Add(new ToggleFlagViewModel { Name = "FFlagHandleAltEnterFullscreenManually", Description = "Exclusive Fullscreen (Alt+Delete)", IsOn = false });
            Flags.Add(new InputFlagViewModel { Name = "FIntFullscreenTitleBarTriggerDelayMillis", Description = "Disable Fullscreen Title Bar (Delay ms)", Value = "3600000" });
            Flags.Add(new ToggleFlagViewModel { Name = "FFlagUserShowGuiHideToggles", Description = "GUI Hiding Toggles", IsOn = true });
            Flags.Add(new ToggleFlagViewModel { Name = "FFlagGuiHidingApiSupport2", Description = "GUI Hiding API Support", IsOn = true });
            Flags.Add(new ToggleFlagViewModel { Name = "FFlagGameBasicSettingsFramerateCap5", Description = "FPS Unlocker In Roblox Menu Settings", IsOn = true });
            Flags.Add(new ToggleFlagViewModel { Name = "FFlagTaskSchedulerLimitTargetFpsTo2402", Description = "Disable >240 FPS Limit for Unlocker", IsOn = false });
            Flags.Add(new ToggleFlagViewModel { Name = "FFlagFixSensitivityTextPrecision", Description = "5 Decimal Sensitivity Precision", IsOn = false });
            Flags.Add(new ToggleFlagViewModel { Name = "FFlagChatTranslationEnableSystemMessage", Description = "Removes Translated Supported Message On Join", IsOn = false }); // Value was 'false' (lowercase)
            Flags.Add(new InputFlagViewModel { Name = "FStringChatTranslationEnabledLocales", Description = "Customize Language Translation (comma-sep list)", Value = "es_es,fr_fr,pt_br,de_de,it_it,ja_jp,ko_kr,id_id,tr_tr,zh_cn,zh_tw,th_th,pl_pl,vi_vn,ru_ru," });
            Flags.Add(new InputFlagViewModel { Name = "FIntV1MenuLanguageSelectionFeaturePerMillageRollout", Description = "Opt-Out Experience Language (0 to disable)", Value = "0" });
            Flags.Add(new ToggleFlagViewModel { Name = "FFlagChatTranslationSettingEnabled3", Description = "Disable New Chat Translation Settings", IsOn = false });
            Flags.Add(new ToggleFlagViewModel { Name = "FFlagNewCameraControls", Description = "New Camera Mode", IsOn = true });
        }

        private void PopulateUserInterfaceFlags()
        {
            Flags.Add(new ToggleFlagViewModel { Name = "FFlagDebugDisplayFPS", Description = "Display FPS Counter", IsOn = true });
            Flags.Add(new ToggleFlagViewModel { Name = "FFlagAdServiceEnabled", Description = "Disable In-Game Advertisements", IsOn = false });
            Flags.Add(new ToggleFlagViewModel { Name = "FFlagDebugDisableTelemetryEphemeralCounter", Description = "Disable Telemetry (Ephemeral Counter)", IsOn = true });
            Flags.Add(new ToggleFlagViewModel { Name = "FFlagDebugDisableTelemetryEphemeralStat", Description = "Disable Telemetry (Ephemeral Stat)", IsOn = true });
            Flags.Add(new ToggleFlagViewModel { Name = "FFlagDebugDisableTelemetryEventIngest", Description = "Disable Telemetry (Event Ingest)", IsOn = true });
            Flags.Add(new ToggleFlagViewModel { Name = "FFlagDebugDisableTelemetryPoint", Description = "Disable Telemetry (Point)", IsOn = true });
            Flags.Add(new ToggleFlagViewModel { Name = "FFlagDebugDisableTelemetryV2Counter", Description = "Disable Telemetry (V2 Counter)", IsOn = true });
            Flags.Add(new ToggleFlagViewModel { Name = "FFlagDebugDisableTelemetryV2Event", Description = "Disable Telemetry (V2 Event)", IsOn = true });
            Flags.Add(new ToggleFlagViewModel { Name = "FFlagDebugDisableTelemetryV2Stat", Description = "Disable Telemetry (V2 Stat)", IsOn = true });
            Flags.Add(new ToggleFlagViewModel { Name = "DFFlagDebugDisableTimeoutDisconnect", Description = "No Internet Disconnect Message (still kicked)", IsOn = true });
            Flags.Add(new ToggleFlagViewModel { Name = "FFlagEnableQuickGameLaunch", Description = "Quick Game Launch (Can cause bugs)", IsOn = true });
            Flags.Add(new InputFlagViewModel { Name = "DFIntNumAssetsMaxToPreload", Description = "Increased Asset Preloading Count (Default: 1000)", Value = "1000" });
            Flags.Add(new ToggleFlagViewModel { Name = "DFFlagOrder66", Description = "Disable In-Game Purchases", IsOn = true });
            Flags.Add(new ToggleFlagViewModel { Name = "FFlagDebugDefaultChannelStartMuted", Description = "Unmute Mic Automatically When Joining (VC)", IsOn = false });
        }

        private async Task<string> LoadSettingsFromFileAsync()
        {
            if (string.IsNullOrEmpty(_soberConfigPath)) return "Could not determine HOME directory. Cannot find Sober config.";
            if (!File.Exists(_soberConfigPath)) return "Sober config not found. You can save changes to create it.";

            try
            {
                string jsonString = await File.ReadAllTextAsync(_soberConfigPath);
                if (string.IsNullOrWhiteSpace(jsonString)) return "Sober config is empty. Ready to save new settings.";

                JsonNode? configNode = JsonNode.Parse(jsonString);
                if (configNode?["FFlags"] is not JsonObject fflags) return "Sober config loaded, but no flags are present.";
                
                foreach (var flag in Flags) flag.IsEnabled = false;

                foreach (var flag in Flags)
                {
                    if (fflags.TryGetValue(flag.Name, out var flagNode) && flagNode is not null)
                    {
                        flag.IsEnabled = true;
                        string value = flagNode.ToString();

                        if (flag is ToggleFlagViewModel toggleFlag)
                            toggleFlag.IsOn = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                        else if (flag is InputFlagViewModel inputFlag)
                            inputFlag.Value = value;
                    }
                }
                return "Sober config file loaded successfully.";
            }
            catch (JsonException ex) { return $"Sober config is corrupt. Save to overwrite it. Error: {ex.Message}"; }
            catch (Exception ex) { return $"Error reading Sober config file: {ex.Message}"; }
        }
        
        private void PlayRoblox()
        {
            Process.Start(new ProcessStartInfo("flatpak", "run org.vinegarhq.Sober") { UseShellExecute = false });
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
            }
            
            var configDir = Path.GetDirectoryName(_soberConfigPath);
            if (!string.IsNullOrEmpty(configDir))
                Directory.CreateDirectory(configDir);

            var options = new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
            await File.WriteAllTextAsync(_soberConfigPath, configNode.ToJsonString(options));
        }
    }
}
