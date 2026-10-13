using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace NexusStrap.UI.ViewModels.Settings
{
    public class BehaviourViewModel : NotifyPropertyChangedViewModel
    {
        public BehaviourViewModel()
        {
            App.Cookies.StateChanged += (object? _, CookieState state) => CookieLoadingFailed = state != CookieState.Success && state != CookieState.Unknown;
            BeastBoostCommand = new RelayCommand(ApplyBeastBoost);
        }

        public ObservableCollection<ProcessPriorityOption> ProcessPriorityOptions { get; } = new ObservableCollection<ProcessPriorityOption>(Enum.GetValues(typeof(ProcessPriorityOption)).Cast<ProcessPriorityOption>());

        public ProcessPriorityOption SelectedPriority
        {
            get => App.Settings.Prop.SelectedProcessPriority;
            set => App.Settings.Prop.SelectedProcessPriority = value;
        }

        public bool MultiInstances
        {
            get => App.Settings.Prop.MultiInstanceLaunching;
            set
            {
                if (value)
                {
                    var result = Frontend.ShowMessageBox(
                        "Roblox stated that multi-instance launching is considered an exploit, but it isn't bannable.\n\n" +
                        "Are you sure you want to enable multi-instance launching?",
                        MessageBoxImage.Warning,
                        MessageBoxButton.YesNo
                    );

                    if (result != MessageBoxResult.Yes)
                    {
                        return;
                    }
                }

                App.Settings.Prop.MultiInstanceLaunching = value;

                if (!value)
                {
                    Error773Fix = false;
                    OnPropertyChanged(nameof(Error773Fix));
                }

                OnPropertyChanged(nameof(MultiInstances));
            }
        }

        public bool FramerateUncap
        {
            get
            {
                string? value = App.GlobalSettings.GetPresets("Rendering.FramerateCap");
                return int.TryParse(value, out int framerate) && framerate > 240;
            }
            set
            {
                // -1 is invalid and Roblox clamps it to 20 FPS (the bug you saw).
                // Use 60 (stock) when capping, 9999 when uncapping. The actual
                // uncap is the FFlag; GBS is just kept sane.
                App.GlobalSettings.SetPresets("Rendering.FramerateCap", value ? "9999" : "60");
                App.GlobalSettings.Save();

                // Uncap via FFlag so the client ignores the GBS cap instead of
                // locking GlobalBasicSettings_13.xml read-only. A read-only lock
                // blocks Roblox from persisting ANY in-game change (volume,
                // shift lock, quality), which wiped settings on every rejoin.
                if (value)
                    App.FastFlags.SetValue("FFlagGameBasicSettingsFramerateCap", "False");
                else
                    App.FastFlags.SetValue("FFlagGameBasicSettingsFramerateCap", null);
                App.FastFlags.Save();

                // Release any lock left by the old read-only approach (or the
                // manual toggle era) so in-game settings save again.
                App.GlobalSettings.SetReadOnly(false);

                OnPropertyChanged(nameof(FramerateUncap));
            }
        }

        public bool Error773Fix
        {
            get => App.Settings.Prop.Error773Fix;
            set => App.Settings.Prop.Error773Fix = value;
        }

        public bool LogViewEnabled
        {
            get => App.Settings.Prop.ShowFpsOverlay;
            set
            {
                App.Settings.Prop.ShowFpsOverlay = value;
                OnPropertyChanged(nameof(LogViewEnabled));
            }
        }

        public bool BackgroundUpdates
        {
            get => App.Settings.Prop.BackgroundUpdatesEnabled;
            set => App.Settings.Prop.BackgroundUpdatesEnabled = value;
        }

        public bool CloseCrashHandler
        {
            get => App.Settings.Prop.AutoCloseCrashHandler;
            set => App.Settings.Prop.AutoCloseCrashHandler = value;
        }

        public bool ConfirmLaunches
        {
            get => App.Settings.Prop.ConfirmLaunches;
            set => App.Settings.Prop.ConfirmLaunches = value;
        }

        public bool CookieLoadingFinished => true;

        public bool CookieAccess
        {
            get => App.Settings.Prop.AllowCookieAccess;
            set
            {
                App.Settings.Prop.AllowCookieAccess = value;
                if (value)
                    Task.Run(App.Cookies.LoadCookies);

                OnPropertyChanged(nameof(CookieAccess));
            }
        }

        private bool _cookieLoadingFailed;
        public bool CookieLoadingFailed
        {
            get => _cookieLoadingFailed;
            set
            {
                _cookieLoadingFailed = value;
                OnPropertyChanged(nameof(CookieLoadingFailed));
            }
        }

        public CleanerOptions SelectedCleanUpMode
        {
            get => App.Settings.Prop.CleanerOptions;
            set => App.Settings.Prop.CleanerOptions = value;
        }

        public IEnumerable<CleanerOptions> CleanerOptions { get; } = CleanerOptionsEx.Selections;

        public CleanerOptions CleanerOption
        {
            get => App.Settings.Prop.CleanerOptions;
            set
            {
                App.Settings.Prop.CleanerOptions = value;
            }
        }

        private List<string> CleanerItems = App.Settings.Prop.CleanerDirectories;

        public bool CleanerLogs
        {
            get => CleanerItems.Contains("RobloxLogs");
            set
            {
                if (value)
                    CleanerItems.Add("RobloxLogs");
                else
                    CleanerItems.Remove("RobloxLogs");
            }
        }

        public bool CleanerCache
        {
            get => CleanerItems.Contains("RobloxCache");
            set
            {
                if (value)
                    CleanerItems.Add("RobloxCache");
                else
                    CleanerItems.Remove("RobloxCache");
            }
        }

        public bool CleanerNexusStrap
        {
            get => CleanerItems.Contains("NexusStrapLogs");
            set
            {
                if (value)
                    CleanerItems.Add("NexusStrapLogs");
                else
                    CleanerItems.Remove("NexusStrapLogs");
            }
        }

        public bool LaunchSoundEnabled
        {
            get => App.Settings.Prop.EnableLaunchSound;
            set => App.Settings.Prop.EnableLaunchSound = value;
        }

        public string? LaunchSoundPath
        {
            get => App.Settings.Prop.LaunchSoundPath;
            set
            {
                App.Settings.Prop.LaunchSoundPath = value;
                OnPropertyChanged(nameof(LaunchSoundPath));
                OnPropertyChanged(nameof(LaunchSoundDisplay));
            }
        }

        public string LaunchSoundDisplay => string.IsNullOrEmpty(LaunchSoundPath)
            ? "Select a .mp3, .wav, or .ogg file to play on launch."
            : $"Selected: {System.IO.Path.GetFileName(LaunchSoundPath)}";

        public double LaunchSoundVolume
        {
            get => App.Settings.Prop.LaunchSoundVolume;
            set => App.Settings.Prop.LaunchSoundVolume = value;
        }

        public ICommand BeastBoostCommand { get; }

        private bool _isApplyingBoost;

        private void ApplyBeastBoost()
        {
            if (_isApplyingBoost)
                return;

            _isApplyingBoost = true;

            try
            {
                App.FastFlags.suspendUndoSnapshot = true;
                App.FastFlags.SaveUndoSnapshot();
                foreach (var kv in BeastFlags)
                    App.FastFlags.SetValue(kv.Key, kv.Value);
                App.FastFlags.suspendUndoSnapshot = false;
                App.FastFlags.Save();

                // Also set CPU priority to AboveNormal for max power
                App.Settings.Prop.SelectedProcessPriority = ProcessPriorityOption.AboveNormal;
                OnPropertyChanged(nameof(SelectedPriority));

                // Disable RAM cleaner lag source
                App.Settings.Prop.EnableRamCleaner = false;
                App.Settings.Save();

                Frontend.ShowMessageBox(
                    "Performance Boost applied and saved!",
                    MessageBoxImage.Information);
            }
            finally
            {
                _isApplyingBoost = false;
            }
        }

        // BEAST: uncapped + threading + network + telemetry + render killer (excludes PauseVoxelizer/ForceVoxel)
        private static readonly Dictionary<string, string> BeastFlags = new()
        {
            { "DFIntTaskSchedulerTargetFps", "2147483647" },
            { "FFlagTaskSchedulerLimitTargetFpsTo2402", "False" },
            { "DFIntTaskSchedulerJobInitThreads", "12" },
            { "DFIntTaskSchedulerJobInGameThreads", "12" },
            { "FIntRuntimeMaxNumOfThreads", "1000000" },
            { "DFIntMegaReplicatorNumParallelTasks", "12" },
            { "DFIntReplicationDataCacheNumParallelTasks", "12" },
            { "DFIntPhysicsReceiveNumParallelTasks", "12" },
            { "DFIntClientPacketMaxDelayMs", "1" },
            { "DFIntRakNetResendRttMultiple", "1" },
            { "DFIntRakNetNakResendDelayMs", "1" },
            { "DFIntRakNetLoopMs", "1" },
            { "FFlagDebugDisableTelemetryV2Stat", "True" },
            { "FFlagDebugDisableTelemetryV2Counter", "True" },
            { "FFlagDebugDisableTelemetryEphemeralCounter", "True" },
            { "FFlagDebugDisableTelemetryPoint", "True" },
            { "FFlagDisablePostFx", "True" },
            { "FIntRenderShadowIntensity", "0" },
            { "FIntRenderShadowmapBias", "0" },
            { "FIntSSAOMipLevels", "0" },
            { "FFlagGlobalWindActivated", "False" },
            { "FFlagAdServiceEnabled", "False" },
            { "FIntFRMMinGrassDistance", "0" },
            { "FIntFRMMaxGrassDistance", "0" },
            { "FIntRenderGrassDetail", "0" },
            { "FIntRomarkStartWithGraphicQualityLevel", "1" },
            { "FFlagDebugSkyGray", "True" },
            { "FIntFullscreenTitleBarTriggerDelayMillis", "2147483647" },
            { "DFFlagEnableRequestAsyncCompression", "True" },
            { "FFlagMSRefactor5", "False" },
        };
    }
}