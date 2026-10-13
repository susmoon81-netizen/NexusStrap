using NexusStrap.AppData;
using NexusStrap.Integrations;
using NexusStrap.UI.Elements.Overlay;
using System.Windows;

namespace NexusStrap
{
    public class Watcher : IDisposable
    {
        private readonly InterProcessLock _lock = new("Watcher");

        private readonly WatcherData? _watcherData;

        private readonly NotifyIconWrapper? _notifyIcon;

        public readonly ActivityWatcher? ActivityWatcher;

        public readonly IntegrationWatcher? IntegrationWatcher;

        public readonly PlayerDiscordRichPresence? PlayerRichPresence;
        public readonly StudioDiscordRichPresence? StudioRichPresence;

        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private bool _isDisposed = false;

        private static readonly string PlaytimePath = Path.Combine(Paths.Base, "Playtime.json");

        private readonly Stopwatch _playtimeStopwatch = new();
        private TimeSpan _accumulatedPlaytime = TimeSpan.Zero;
        private bool _playtimeTracking = false;

        private PerformanceOverlay? _fpsOverlay;

        /// <summary>
        /// Cumulative Roblox playtime: everything recorded by previous sessions plus
        /// whatever this watcher process has been alive for.
        /// </summary>
        public TimeSpan TotalPlaytime => _accumulatedPlaytime + _playtimeStopwatch.Elapsed;

        public Watcher()
        {
            const string LOG_IDENT = "Watcher";

            if (!_lock.IsAcquired)
            {
                App.Logger.WriteLine(LOG_IDENT, "Watcher instance already exists");
                return;
            }

            string? watcherDataArg = App.LaunchSettings.WatcherFlag.Data;

            if (String.IsNullOrEmpty(watcherDataArg))
            {
#if DEBUG
                string path = new RobloxPlayerData().ExecutablePath;
                if (!File.Exists(path))
                    throw new ApplicationException("Roblox player is not been installed");

                using var gameClientProcess = Process.Start(path);

                _watcherData = new() { ProcessId = gameClientProcess.Id };
#else
                throw new Exception("Watcher data not specified");
#endif
            }
            else
            {
                _watcherData = JsonSerializer.Deserialize<WatcherData>(Encoding.UTF8.GetString(Convert.FromBase64String(watcherDataArg)));
            }

            if (_watcherData is null)
                throw new Exception("Watcher data is invalid");

            if (App.Settings.Prop.EnableActivityTracking)
            {
                ActivityWatcher = new(_watcherData.LogFile, _watcherData.LaunchMode, _watcherData.ProcessId);

                if (App.Settings.Prop.UseDisableAppPatch)
                {
                    ActivityWatcher.OnAppClose += delegate
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Received desktop app exit, closing Roblox");
                        using var process = Process.GetProcessById(_watcherData.ProcessId);
                        process.CloseMainWindow();
                    };
                }

                if ((_watcherData.LaunchMode == LaunchMode.Studio || _watcherData.LaunchMode == LaunchMode.StudioAuth) && App.Settings.Prop.StudioRPC)
                    StudioRichPresence = new(ActivityWatcher);
                else if (_watcherData.LaunchMode == LaunchMode.Player && App.Settings.Prop.UseDiscordRichPresence)
                    PlayerRichPresence = new(ActivityWatcher);
            }

            _notifyIcon = new(this);

            if (App.Settings.Prop.PlaytimeCounter)
                StartPlaytimeTracking();
        }

        private void StartPlaytimeTracking()
        {
            const string LOG_IDENT = "Watcher::StartPlaytimeTracking";

            try
            {
                if (File.Exists(PlaytimePath))
                {
                    var saved = JsonSerializer.Deserialize<PlaytimeRecord>(File.ReadAllText(PlaytimePath));

                    if (saved is not null && saved.Total > TimeSpan.Zero)
                        _accumulatedPlaytime = saved.Total;
                }
            }
            catch (Exception ex)
            {
                // a corrupt file must never stop playtime tracking
                App.Logger.WriteException(LOG_IDENT, ex);
                _accumulatedPlaytime = TimeSpan.Zero;
            }

            _playtimeTracking = true;
            _playtimeStopwatch.Start();

            App.Logger.WriteLine(LOG_IDENT, $"Resumed with {_accumulatedPlaytime} of previously recorded playtime");
        }

        private void PersistPlaytime()
        {
            const string LOG_IDENT = "Watcher::PersistPlaytime";

            if (!_playtimeTracking)
                return;

            _playtimeTracking = false;
            _playtimeStopwatch.Stop();

            _accumulatedPlaytime += _playtimeStopwatch.Elapsed;

            try
            {
                string json = JsonSerializer.Serialize(
                    new PlaytimeRecord { Total = _accumulatedPlaytime },
                    new JsonSerializerOptions { WriteIndented = true });

                File.WriteAllText(PlaytimePath, json);
                App.Logger.WriteLine(LOG_IDENT, $"Recorded total playtime of {_accumulatedPlaytime}");
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }

        private class PlaytimeRecord
        {
            public TimeSpan Total { get; set; }
        }

        public void KillRobloxProcess() => CloseProcess(_watcherData!.ProcessId, true);

        private void ShowLogViewOverlay()
        {
            const string LOG_IDENT = "Watcher::ShowLogViewOverlay";

            try
            {
                if (!App.Settings.Prop.ShowStatsOverlay)
                    return;

                if (_watcherData?.LaunchMode != LaunchMode.Player && _watcherData?.LaunchMode != LaunchMode.Studio)
                    return;

                int pid = _watcherData.ProcessId;

                Application.Current?.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        CloseLogViewOverlay();
                        _fpsOverlay = new PerformanceOverlay(pid);
                        _fpsOverlay.Show();
                        App.Logger.WriteLine(LOG_IDENT, $"Stats overlay shown for PID {pid}");
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteException(LOG_IDENT, ex);
                    }
                });
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }

        private void CloseLogViewOverlay()
        {
            const string LOG_IDENT = "Watcher::CloseLogViewOverlay";

            try
            {
                if (_fpsOverlay is null)
                    return;

                var overlay = _fpsOverlay;
                _fpsOverlay = null;

                if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.HasShutdownStarted)
                {
                    dispatcher.Invoke(() =>
                    {
                        try { overlay.Close(); }
                        catch (Exception ex) { App.Logger.WriteException(LOG_IDENT, ex); }
                    });
                }
                else
                {
                    try { overlay.Close(); } catch { }
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }

        public void CloseProcess(int pid, bool force = false)
        {
            const string LOG_IDENT = "Watcher::CloseProcess";

            try
            {
                using var process = Process.GetProcessById(pid);

                App.Logger.WriteLine(LOG_IDENT, $"Killing process '{process.ProcessName}' (pid={pid}, force={force})");

                if (process.HasExited)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"PID {pid} has already exited");
                    return;
                }

                if (force)
                    process.Kill();
                else
                    process.CloseMainWindow();
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"PID {pid} could not be closed");
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }

        public async Task Run()
        {
            if (!_lock.IsAcquired || _watcherData is null)
                return;

            if (ActivityWatcher is not null)
                _ = ActivityWatcher.Start();

            ShowLogViewOverlay();

            int? exitCode = null;

            try
            {
                // Keep the Process instance until it exits. Looking it up after it has
                // exited throws, which previously made crash recovery silently unreachable.
                using var process = Process.GetProcessById(_watcherData.ProcessId);
                await process.WaitForExitAsync(_cancellationTokenSource.Token);
                exitCode = process.ExitCode;
            }
            catch (OperationCanceledException)
            {
                App.Logger.WriteLine("Watcher::Run", "Watcher was cancelled");
                CloseLogViewOverlay();
                return;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("Watcher::Run", ex);
            }

            CloseLogViewOverlay();

            if (_cancellationTokenSource.Token.IsCancellationRequested)
                return;

            // Crash Recovery: only if still in-game (not graceful leave via menu/Alt+F4 -> InGame false)
            // Alt+F4 / Leave logs "Time to disconnect..." and clears InGame before process exit,
            // so we treat !InGame as user-initiated and skip recovery (fixes rejoin loop).
            if (App.Settings.Prop.CrashRecoveryEnabled && _watcherData.LaunchMode == LaunchMode.Player)
            {
                bool wasInGame = ActivityWatcher?.InGame == true;
                if (!wasInGame)
                    App.Logger.WriteLine("Watcher::Run", $"Exit code {exitCode} but not in-game (graceful leave), skipping crash recovery.");

                if (wasInGame && exitCode is int code && code != 0)
                {
                    // Filter non-crash codes: 0 = clean, 1 = WM_CLOSE/Alt+F4 on some builds
                    bool isCrashCode = code < 0 || code > 1000;
                    if (!isCrashCode)
                    {
                        App.Logger.WriteLine("Watcher::Run", $"Exit code {code} not considered crash, skipping recovery.");
                    }
                    else
                    {
                        App.Logger.WriteLine("Watcher::Run", $"Roblox crashed with exit code {code}. Crash recovery triggered.");
                        for (int attempt = 1; attempt <= App.Settings.Prop.CrashRecoveryMaxRetries; attempt++)
                        {
                            App.Logger.WriteLine("Watcher::Run", $"Crash recovery attempt {attempt}/{App.Settings.Prop.CrashRecoveryMaxRetries}");
                            try
                            {
                                await Task.Delay(App.Settings.Prop.CrashRecoveryDelayMs, _cancellationTokenSource.Token);
                            }
                            catch (OperationCanceledException)
                            {
                                return;
                            }

                            try
                            {
                                Process.Start(Paths.Process, "-player");
                                App.Logger.WriteLine("Watcher::Run", "Crash recovery: relaunched Roblox successfully");
                                return;
                            }
                            catch (Exception ex)
                            {
                                App.Logger.WriteException("Watcher::Run", ex);
                            }
                        }
                        App.Logger.WriteLine("Watcher::Run", "Crash recovery: all retry attempts exhausted");
                    }
                }
            }

            if (_watcherData.AutoclosePids is not null)
            {
                foreach (int pid in _watcherData.AutoclosePids)
                    CloseProcess(pid);
            }

            if (App.LaunchSettings.TestModeFlag.Active)
                Process.Start(Paths.Process, "-settings -testmode");
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            App.Logger.WriteLine("Watcher::Dispose", "Disposing Watcher");

            PersistPlaytime();

            _cancellationTokenSource.Cancel();

            CloseLogViewOverlay();

            if (App.Settings.Prop.MultiInstanceLaunching)
            {
                App.Logger.WriteLine("Watcher::Dispose", "Starting multi-instance cleanup");
                App.Bootstrapper?.CleanupMultiInstanceResources();
            }

            IntegrationWatcher?.Dispose();
            _notifyIcon?.Dispose();
            PlayerRichPresence?.Dispose();
            StudioRichPresence?.Dispose();
            _cancellationTokenSource.Dispose();

            _isDisposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
