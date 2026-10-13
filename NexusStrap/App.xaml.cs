using NexusStrap.Integrations;
using Microsoft.Win32;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wpf.Ui.Hardware;

namespace NexusStrap
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
#if QA_BUILD
        public const string ProjectName = "NexusStrap-QA";
#else
        public const string ProjectName = "NexusStrap";
#endif
        public const string ProjectOwner = "Nexus";
        public const string DiscordInviteLink = "https://discord.gg/R47Pwf7s9q";
        public const string ProjectWebsiteLink = "https://nexusstrap.pages.dev";

        // There is no code host anymore, so downloads and support live on the site.
        // Change ProjectDownloadLink if the exe is ever served from a different path.
        public const string ProjectDownloadLink = ProjectWebsiteLink + "/NexusStrap.exe";

        /// <summary>
        /// Documentation lives on the website rather than a GitHub wiki, so every help
        /// link in the app points at the site. Anchors must match an id on wiki.html.
        /// </summary>
        public const string ProjectWikiLink = ProjectWebsiteLink + "/wiki.html";
        public const string ProjectHelpLink = ProjectWebsiteLink + "/wiki.html#faq";
        public const string ProjectSupportLink = DiscordInviteLink;

        public const string RobloxPlayerAppName = "RobloxPlayerBeta.exe";
        public const string RobloxStudioAppName = "RobloxStudioBeta.exe";

        // simple shorthand for extremely frequently used and long string - this goes under HKCU
        public const string UninstallKey = $@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{ProjectName}";

        public const string ApisKey = $"Software\\{ProjectName}";
        public static LaunchSettings LaunchSettings { get; private set; } = null!;

        public static BuildMetadataAttribute BuildMetadata = Assembly.GetExecutingAssembly().GetCustomAttribute<BuildMetadataAttribute>()!;

        public static string Version = Assembly.GetExecutingAssembly().GetName().Version!.ToString()[..^2];

        public static Bootstrapper? Bootstrapper { get; set; } = null!;

        public static Watcher? WatcherInstance { get; set; } = null;

        public NexusStrapRichPresence RichPresenceInstance { get; private set; } = null!;

        public static readonly EventWaitHandle PlayerLaunchSignal = new(false, EventResetMode.AutoReset, "NexusStrap-PlayerLaunch");

        public static bool IsActionBuild => !String.IsNullOrEmpty(BuildMetadata.CommitRef);

        public static bool IsProductionBuild => IsActionBuild && BuildMetadata.CommitRef.StartsWith("tag", StringComparison.Ordinal);

        public static bool IsPlayerInstalled => App.PlayerState.IsSaved && !String.IsNullOrEmpty(App.PlayerState.Prop.VersionGuid);

        public static bool IsStudioInstalled => App.StudioState.IsSaved && !String.IsNullOrEmpty(App.StudioState.Prop.VersionGuid);

        public static readonly MD5 MD5Provider = MD5.Create();

        public static readonly Logger Logger = new();

        public static readonly Dictionary<string, BaseTask> PendingSettingTasks = new();

        // use the persistable Settings (NexusStrap.Models.Persistable.Settings),
        // NOT that boring auto-made Properties.Settings - it doesn't even have the clicker fields
        public static readonly JsonManager<Settings> Settings = new();

        public static readonly JsonManager<State> State = new();

        public static readonly LazyJsonManager<DistributionState> PlayerState = new(nameof(PlayerState));

        public static readonly LazyJsonManager<DistributionState> StudioState = new(nameof(StudioState));

        public static readonly RemoteDataManager RemoteData = new();

        public static readonly FastFlagManager FastFlags = new();

        public static readonly GBSEditor GlobalSettings = new();

        public static readonly CookiesManager Cookies = new();

        public static readonly HttpClient HttpClient = new(new HttpClientLoggingHandler(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All }));


        private static bool _showingExceptionDialog = false;

        public static void Terminate(ErrorCode exitCode = ErrorCode.ERROR_SUCCESS)
        {
            int exitCodeNum = (int)exitCode;

            Logger.WriteLine("App::Terminate", $"Terminating with exit code {exitCodeNum} ({exitCode})");

            Environment.Exit(exitCodeNum);
        }

        public static void SoftTerminate(ErrorCode exitCode = ErrorCode.ERROR_SUCCESS)
        {
            int exitCodeNum = (int)exitCode;

            Logger.WriteLine("App::SoftTerminate", $"Terminating with exit code {exitCodeNum} ({exitCode})");

            Current.Dispatcher.Invoke(() => Current.Shutdown(exitCodeNum));
        }

        public static void DeferredTerminate()
        {
            Current.Dispatcher.BeginInvoke(() =>
            {
                Logger.WriteLine("App::DeferredTerminate", "Terminating after launch");
                Current.Shutdown();
            });
        }

        void GlobalExceptionHandler(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;

            Logger.WriteLine("App::GlobalExceptionHandler", "An exception occurred");

            FinalizeExceptionHandling(e.Exception);
        }

        public static void FinalizeExceptionHandling(AggregateException ex)
        {
            foreach (var innerEx in ex.InnerExceptions)
                Logger.WriteException("App::FinalizeExceptionHandling", innerEx);

            FinalizeExceptionHandling(ex.GetBaseException(), false);
        }

        public static void FinalizeExceptionHandling(Exception ex, bool log = true)
        {
            if (log)
                Logger.WriteException("App::FinalizeExceptionHandling", ex);

            if (_showingExceptionDialog)
                return;

            _showingExceptionDialog = true;

            if (Bootstrapper?.Dialog != null)
            {
                if (Bootstrapper.Dialog.TaskbarProgressValue == 0)
                    Bootstrapper.Dialog.TaskbarProgressValue = 1; // make sure it's visible

                Bootstrapper.Dialog.TaskbarProgressState = TaskbarItemProgressState.Error;
            }

            Frontend.ShowExceptionDialog(ex);

            Terminate(ErrorCode.ERROR_INSTALL_FAILURE);
        }

        public static NexusStrapRichPresence? RichPresence
        {
            get => (Current as App)?.RichPresenceInstance;
            set
            {
                if (Current is App app)
                    app.RichPresenceInstance = value!;
            }
        }

        public static void WindowsBackdrop()
        {
            Current.Dispatcher.Invoke(() =>
            {
                var backdropType = Settings.Prop.SelectedBackdrop;
                ApplyBackdropToAllWindows(backdropType);
            });
        }

        private static void ApplyBackdropToAllWindows(WindowsBackdrops backdropType)
        {
            var wpfBackdrop = backdropType switch
            {
                WindowsBackdrops.None => BackgroundType.None,
                WindowsBackdrops.Mica => BackgroundType.Mica,
                WindowsBackdrops.Acrylic => BackgroundType.Acrylic,
                WindowsBackdrops.Aero => BackgroundType.Aero,
                _ => BackgroundType.None
            };

            foreach (Window window in Current.Windows)
            {
                if (window is UiWindow uiWindow)
                {
                    bool isTransparentBackdrop = (wpfBackdrop == BackgroundType.Acrylic || wpfBackdrop == BackgroundType.Aero);

                    uiWindow.AllowsTransparency = isTransparentBackdrop;

                    uiWindow.WindowStyle = isTransparentBackdrop
                        ? WindowStyle.None
                        : WindowStyle.SingleBorderWindow;

                    uiWindow.WindowBackdropType = wpfBackdrop;
                }
            }
        }

        public void ApplyCustomFontToWindow(Window window)
        {
            var fontPath = Settings.Prop.CustomFontPath;
            if (string.IsNullOrWhiteSpace(fontPath) || !File.Exists(fontPath))
                return;

            var font = FontManager.LoadFontFromFile(fontPath);
            if (font != null)
            {
                window.FontFamily = font;
            }
        }

        /// <summary>
        /// GitHub repo backing releases (update checks, studio plugin sync).
        /// </summary>
        public const string GithubRepo = "susmoon81-netizen/NexusStrap";

        /// <summary>
        /// Polls the GitHub releases feed. With <paramref name="includePreRelease"/>
        /// true the newest release of any kind is returned, otherwise the latest
        /// stable release. Null when offline or on any failure — callers treat
        /// that as "no update available", never as an error.
        /// </summary>
        public static async Task<GithubRelease?> GetLatestRelease(bool includePreRelease = false)
        {
            const string LOG_IDENT = "App::GetLatestRelease";

            try
            {
                if (includePreRelease)
                {
                    string json = await HttpClient.GetStringAsync($"https://api.github.com/repos/{GithubRepo}/releases");
                    return JsonSerializer.Deserialize<List<GithubRelease>>(json)?.FirstOrDefault();
                }

                string stableJson = await HttpClient.GetStringAsync($"https://api.github.com/repos/{GithubRepo}/releases/latest");
                return JsonSerializer.Deserialize<GithubRelease>(stableJson);
            }
            catch (Exception ex)
            {
                Logger.WriteLine(LOG_IDENT, "Release feed unreachable, skipping update check.");
                Logger.WriteException(LOG_IDENT, ex);
                return null;
            }
        }

        public static void AssertWindowsOSVersion()
        {
            const string LOG_IDENT = "App::AssertWindowsOSVersion";

            int major = Environment.OSVersion.Version.Major;
            if (major < 10) // Windows 10 and newer only
            {
                Logger.WriteLine(LOG_IDENT, $"Detected unsupported Windows version ({Environment.OSVersion.Version}).");

                if (!LaunchSettings.QuietFlag.Active)
                    Frontend.ShowMessageBox(Strings.App_OSDeprecation_Win7_81, MessageBoxImage.Error);

                Terminate(ErrorCode.ERROR_INVALID_FUNCTION);
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            const string LOG_IDENT = "App::OnStartup";

            Locale.Initialize();

            base.OnStartup(e);

            LaunchSettings = new LaunchSettings(e.Args);

            UI.RoundedWindowChrome.Install();

            bool fontApplied = FontManager.ApplySavedCustomFont();

            if (fontApplied)
                Logger.WriteLine(LOG_IDENT, "Custom font applied at startup.");

            foreach (Window window in Application.Current.Windows)
            {
                ApplyCustomFontToWindow(window);
            }

            Logger.WriteLine(LOG_IDENT, $"Starting {ProjectName} v{Version}");

            var userAgent = new StringBuilder($"{ProjectName}/{Version}");

            if (IsActionBuild)
            {
                Logger.WriteLine(LOG_IDENT, $"Compiled {BuildMetadata.Timestamp.ToFriendlyString()} from commit {BuildMetadata.CommitHash} ({BuildMetadata.CommitRef})");

                if (IsProductionBuild)
                    userAgent.Append(" (Production)");
                else
                    userAgent.Append($" (Artifact {BuildMetadata.CommitHash}, {BuildMetadata.CommitRef})");
            }
            else
            {
                Logger.WriteLine(LOG_IDENT, $"Compiled {BuildMetadata.Timestamp.ToFriendlyString()} from {BuildMetadata.Machine}");

#if QA_BUILD
                userAgent.Append(" (QA)");
#else
                userAgent.Append($" (Build {Convert.ToBase64String(Encoding.UTF8.GetBytes(BuildMetadata.Machine))})");
#endif
            }

            Logger.WriteLine(LOG_IDENT, $"OSVersion: {Environment.OSVersion}");
            Logger.WriteLine(LOG_IDENT, $"Loaded from {Paths.Process}");
            Logger.WriteLine(LOG_IDENT, $"Temp path is {Paths.Temp}");
            Logger.WriteLine(LOG_IDENT, $"WindowsStartMenu path is {Paths.WindowsStartMenu}");

            ApplicationConfiguration.Initialize();

            HttpClient.Timeout = TimeSpan.FromSeconds(60);

            if (!HttpClient.DefaultRequestHeaders.UserAgent.Any())
                HttpClient.DefaultRequestHeaders.Add("User-Agent", userAgent.ToString());

            using var uninstallKey = Registry.CurrentUser.OpenSubKey(UninstallKey);
            string? installLocation = null;
            bool fixInstallLocation = false;

            if (uninstallKey?.GetValue("InstallLocation") is string installLocValue)
            {
                if (Directory.Exists(installLocValue))
                {
                    installLocation = installLocValue;
                }
                else
                {
                    var match = Regex.Match(installLocValue, @"^[a-zA-Z]:\\Users\\([^\\]+)", RegexOptions.IgnoreCase);

                    if (match.Success)
                    {
                        string newLocation = installLocValue.Replace(match.Value, Paths.UserProfile, StringComparison.InvariantCultureIgnoreCase);

                        if (Directory.Exists(newLocation))
                        {
                            installLocation = newLocation;
                            fixInstallLocation = true;
                        }
                    }
                }
            }

            if (installLocation == null && Directory.GetParent(Paths.Process)?.FullName is string processDir)
            {
                var files = Directory.GetFiles(processDir).Select(Path.GetFileName).ToArray();

                if (files.Length <= 3 && files.Contains("Settings.json") && files.Contains("State.json"))
                {
                    installLocation = processDir;
                    fixInstallLocation = true;
                }
            }

            if (fixInstallLocation && installLocation != null)
            {
                var installer = new Installer
                {
                    InstallLocation = installLocation,
                    IsImplicitInstall = true
                };

                if (installer.CheckInstallLocation())
                {
                    Logger.WriteLine(LOG_IDENT, $"Changing install location to '{installLocation}'");
                    installer.DoInstall();
                }
                else
                {
                    installLocation = null; // force reinstall
                }
            }

            if (installLocation == null)
            {
                Logger.Initialize(true);
                AssertWindowsOSVersion();
                Logger.WriteLine(LOG_IDENT, "Not installed, launching the installer");
                AssertWindowsOSVersion();
                LaunchHandler.LaunchInstaller();
            }
            else
            {
                Paths.Initialize(installLocation);

                // the installed copy is a framework-dependent single-file build, so the
                // whole payload is just the exe (managed DLLs and runtime config are
                // embedded in it). if it is missing, redeploy it from the currently
                // running process directory. when the .NET 10 Desktop Runtime is
                // missing, the apphost itself prompts the user to download it —
                // nothing is bundled.
                if (Paths.Process != Paths.Application
                    && !File.Exists(Paths.Application))
                {
                    var selfHealInstaller = new Installer
                    {
                        InstallLocation = installLocation,
                        IsImplicitInstall = true
                    };

                    selfHealInstaller.DeployApplicationPayload();
                }

                Logger.Initialize(LaunchSettings.UninstallFlag.Active);

                if (!Logger.Initialized && !Logger.NoWriteMode)
                {
                    Logger.WriteLine(LOG_IDENT, "Possible duplicate launch detected, terminating.");
                    Terminate();
                }

                Task.Run(RemoteData.LoadData); // ok

                Settings.Load();
                State.Load();
                FastFlags.Load();
                GlobalSettings.Load();

                // Repair: older versions locked GlobalBasicSettings_13.xml read-only
                // for the FPS uncap, which silently blocked Roblox from saving
                // in-game settings (volume, shift lock, camera). The uncap is now
                // done via FFlag, so any leftover lock is always cleared so
                // settings persist between sessions.
                // Also fixes legacy FramerateCap=-1 which Roblox clamps to 20 FPS.
                try
                {
                    string? framerateCap = GlobalSettings.GetPresets("Rendering.FramerateCap");
                    if (framerateCap == "-1")
                    {
                        Logger.WriteLine(LOG_IDENT, "Fixing legacy FramerateCap=-1 -> 60 (was clamping to 20 FPS)");
                        GlobalSettings.SetPresets("Rendering.FramerateCap", "60");
                        GlobalSettings.Save();
                    }

                    if (GlobalSettings.GetReadOnly())
                    {
                        bool uncapped = int.TryParse(framerateCap, out int cap) && cap > 240;
                        Logger.WriteLine(LOG_IDENT, "Clearing stale read-only flag on GlobalBasicSettings_13.xml so in-game settings save" + (uncapped ? " (migrating toggle-era FPS uncap lock to FFlag)" : ""));
                        GlobalSettings.SetReadOnly(false);
                    }
                }
                catch (Exception ex)
                {
                    Logger.WriteException(LOG_IDENT, ex);
                }

                Integrations.PresenceHeartbeat.Start();

                // make sure the desktop and start menu shortcuts exist from the very
                // first open, even before Roblox itself has been installed
                new Installer { IsImplicitInstall = true }.EnsureShortcuts();

                // to fix error System.IO.IOException: No se encuentra el recurso 'ui/style/.xaml'.
                // when i put in installer dosent work
                // if i try to fix in wpfuiwindow also dosent work
                if (Settings.Prop.Theme > Enums.Theme.Custom)
                {
                    Settings.Prop.Theme = Enums.Theme.Dark;
                    Settings.Save();
                }

                if (Settings.Prop.AllowCookieAccess)
                    Task.Run(Cookies.LoadCookies);

                if (!Locale.SupportedLocales.ContainsKey(Settings.Prop.Locale))
                {
                    Settings.Prop.Locale = "nil";
                    Settings.Save();
                }

                Locale.Set(Settings.Prop.Locale);

                // BEAST: always max power — AboveNormal priority every launch
                if (Settings.Prop.SelectedProcessPriority != ProcessPriorityOption.AboveNormal)
                {
                    Settings.Prop.SelectedProcessPriority = ProcessPriorityOption.AboveNormal;
                    Settings.Save();
                }
                // Kill lag sources
                if (Settings.Prop.EnableRamCleaner)
                {
                    Settings.Prop.EnableRamCleaner = false;
                    Settings.Save();
                }

                if (!LaunchSettings.BypassUpdateCheck)
                    Installer.HandleUpgrade();

                WindowsRegistry.RegisterApis();

                MemoryManager.Start();

                LaunchHandler.ProcessLaunchArgs();
            }

        }

        protected override void OnExit(ExitEventArgs e)
        {
            Integrations.PresenceHeartbeat.Stop();
            MemoryManager.Stop();
            AccountManager.Shared.SaveAccounts();
            RichPresence?.Dispose();
            WatcherInstance?.Dispose();
            Logger.Dispose();
            base.OnExit(e);
        }
    }
}