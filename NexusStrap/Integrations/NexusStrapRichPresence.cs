using DiscordRPC;

namespace NexusStrap.Integrations
{
    public class NexusStrapRichPresence : IDisposable
    {
        private readonly DiscordRpcClient _rpcClient;
        private readonly Timestamps _startTimestamps;
        private readonly Stopwatch _uptimeStopwatch;
        private readonly System.Threading.Timer _reconnectTimer;
        private readonly object _presenceLock = new();
        private bool _disposed = false;
        private string _currentPage = "Idle";
        private string? _currentDialog = null;
        private string _lastState = "";

        public bool IsConnected => _rpcClient?.IsInitialized == true;

        public NexusStrapRichPresence()
        {
            _rpcClient = new DiscordRpcClient("1534122334670159892")
            {
                SkipIdenticalPresence = true
            };

            _rpcClient.OnReady += OnReady;
            _rpcClient.OnConnectionEstablished += (_, _) => RePushPresence();
            _rpcClient.OnClose += (_, _) => ResetPushState();
            _rpcClient.OnConnectionFailed += (_, _) => ResetPushState();

            Task.Run(InitializeAsync);

            // Safety net: Discord may start after us, restart mid-session, or
            // drop the pipe. Re-push (lib dedupes identical states) and retry
            // initialization when it previously failed.
            _reconnectTimer = new System.Threading.Timer(_ => ReconnectTick(), null, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15));

            _startTimestamps = new Timestamps
            {
                Start = DateTime.UtcNow
            };

            _uptimeStopwatch = Stopwatch.StartNew();
        }

        private async Task InitializeAsync()
        {
            try
            {
                if (!_rpcClient.Initialize())
                    return;

                await Task.Delay(100);
                SetPresence();
            }
            catch
            {
                // Silent fail
            }
        }

        private void OnReady(object sender, DiscordRPC.Message.ReadyMessage args)
        {
            App.Logger.WriteLine("NexusStrapRichPresence", $"Connected as {args.User.Username}");
            RePushPresence();
        }

        /// <summary>
        /// Force the next update through (used after (re)connects) without
        /// waiting for a page change.
        /// </summary>
        private void RePushPresence()
        {
            if (_disposed) return;

            lock (_presenceLock)
            {
                _lastState = "";
            }

            UpdatePresence();
        }

        private void ResetPushState()
        {
            if (_disposed) return;

            lock (_presenceLock)
            {
                _lastState = "";
            }
        }

        private void ReconnectTick()
        {
            if (_disposed) return;

            try
            {
                if (!_rpcClient.IsInitialized)
                {
                    if (!_rpcClient.Initialize())
                        return;

                    // Give the pipe a beat to come up, then push.
                    Task.Delay(500).ContinueWith(_ => RePushPresence());
                    return;
                }

                UpdatePresence();
            }
            catch
            {
                // Silent fail — next tick retries.
            }
        }

        public void SetPage(string pageName)
        {
            if (_disposed) return;

            _currentPage = pageName;
            _currentDialog = null;
            UpdatePresence();
        }

        public void SetDialog(string dialogName)
        {
            if (_disposed) return;

            _currentDialog = dialogName;
            UpdatePresence();
        }

        public void ClearDialog()
        {
            if (_disposed) return;

            _currentDialog = null;
            UpdatePresence();
        }

        public void ResetPresence()
        {
            if (_disposed) return;

            _currentPage = "Idle";
            _currentDialog = null;
            UpdatePresence();
        }

        private void SetPresence()
        {
            UpdatePresence();
        }

        private void UpdatePresence()
        {
            if (_disposed || !_rpcClient.IsInitialized)
                return;

            lock (_presenceLock)
            {
                string state = !string.IsNullOrEmpty(_currentDialog)
                    ? $"Page: {_currentPage} | Dialog: {_currentDialog}"
                    : $"Page: {_currentPage}";

                if (state == _lastState)
                    return;

                _lastState = state;

                try
                {
                    var presence = new DiscordRPC.RichPresence
                    {
                        Details = "Customize Roblox to your liking!",
                        State = state,
                        Timestamps = _startTimestamps,
                        Assets = new Assets
                        {
                            LargeImageKey = "nexusstrap",
                            LargeImageText = "NexusStrap",
                            SmallImageKey = "checkmark",
                            SmallImageText = $"v{App.Version}"
                        },
                        Buttons = new[]
                        {
                            new Button { Label = "Website", Url = "https://nexusstrap.pages.dev" },
                            new Button { Label = "Discord", Url = "https://discord.gg/R47Pwf7s9q" }
                        }
                    };

                    _rpcClient.SetPresence(presence);
                }
                catch
                {
                    // Silent fail — the reconnect timer retries. Clear the
                    // cached state so the retry actually pushes.
                    _lastState = "";
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            try { _reconnectTimer.Dispose(); } catch { }

            try
            {
                _rpcClient.OnReady -= OnReady;

                if (_rpcClient.IsInitialized)
                {
                    _rpcClient.ClearPresence();
                }

                _rpcClient.Dispose();
                _uptimeStopwatch.Stop();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed
            }
            catch
            {
                // Silent fail
            }

            GC.SuppressFinalize(this);
        }
    }
}