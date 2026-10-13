using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;

namespace NexusStrap
{
    /// <summary>
    /// Measures another process's real presented FPS by counting DXGI Present
    /// events over ETW (the same technique PresentMon uses). No code is
    /// injected into the target process.
    ///
    /// Creating an ETW real-time session requires elevated privileges. When
    /// the session cannot be created (standard user), <see cref="TryStart"/>
    /// returns false and <see cref="IsAvailable"/> stays false — callers must
    /// fall back to showing no FPS value instead of a fake one.
    /// </summary>
    public sealed class FpsMonitor : IDisposable
    {
        private const string LOG_IDENT = "FpsMonitor";

        private TraceEventSession? _session;
        private Thread? _pumpThread;
        private readonly object _gate = new();
        private readonly Queue<long> _presentTicks = new();
        private int _targetPid;
        private bool _disposed;
        private bool _loggedFirstPresent;

        public bool IsAvailable => _session is not null;

        /// <summary>
        /// Frames presented by the target process in the trailing 1 second.
        /// Returns 0 when unavailable.
        /// </summary>
        public int CurrentFps
        {
            get
            {
                lock (_gate)
                {
                    if (_session is null || _disposed)
                        return 0;

                    PruneLocked(Stopwatch.GetTimestamp());
                    return _presentTicks.Count;
                }
            }
        }

        public bool TryStart(int processId)
        {
            const string LOG_START = "FpsMonitor::TryStart";

            _targetPid = processId;

            try
            {
                // Session names are system-global, so include both PIDs to stay
                // unique when several watchers (multi-instance) run at once.
                string sessionName = $"NexusStrapFPS-{Environment.ProcessId}-{processId}";
                var session = new TraceEventSession(sessionName);

                session.EnableProvider("Microsoft-Windows-DXGI");
                session.Source.Dynamic.AddCallbackForProviderEvents(
                    (providerName, eventName) => EventFilterResponse.AcceptEvent,
                    OnDxgiEvent);

                _session = session;
                _pumpThread = new Thread(Pump)
                {
                    IsBackground = true,
                    Name = $"NexusStrapFPS-{processId}"
                };
                _pumpThread.Start();

                App.Logger.WriteLine(LOG_START, $"ETW Present monitoring started for PID {processId}");
                return true;
            }
            catch (Exception ex)
            {
                // Expected for standard users: creating a real-time ETW session
                // needs elevation. Never let this break the overlay.
                App.Logger.WriteLine(LOG_START, $"ETW session unavailable ({ex.GetType().Name}), FPS will be hidden: {ex.Message}");
                try { _session?.Dispose(); } catch { }
                _session = null;
                return false;
            }
        }

        public void SetProcessId(int processId)
        {
            lock (_gate)
            {
                _targetPid = processId;
                _presentTicks.Clear();
                _loggedFirstPresent = false;
            }
        }

        private void Pump()
        {
            try
            {
                _session?.Source.Process();
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }

        private void OnDxgiEvent(TraceEvent data)
        {
            try
            {
                if (_disposed || data.ProcessID != _targetPid)
                    return;

                // DXGI manifest names Present completions "Present/Stop".
                // Match loosely (prefix + Stop opcode) so minor manifest
                // differences between Windows builds cannot break counting.
                if (data.Opcode != TraceEventOpcode.Stop)
                    return;

                string name = data.EventName ?? "";
                if (!name.StartsWith("Present", StringComparison.OrdinalIgnoreCase))
                    return;

                lock (_gate)
                {
                    if (_disposed || data.ProcessID != _targetPid)
                        return;

                    long now = Stopwatch.GetTimestamp();
                    _presentTicks.Enqueue(now);
                    PruneLocked(now);

                    if (!_loggedFirstPresent)
                    {
                        _loggedFirstPresent = true;
                        App.Logger.WriteLine(LOG_IDENT, $"First Present seen for PID {_targetPid} (event {name}), FPS is live");
                    }
                }
            }
            catch
            {
                // ETW callbacks must never throw back into the pump.
            }
        }

        private void PruneLocked(long now)
        {
            long cutoff = now - Stopwatch.Frequency;
            while (_presentTicks.Count > 0 && _presentTicks.Peek() < cutoff)
                _presentTicks.Dequeue();
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                    return;
                _disposed = true;
            }

            try { _session?.Stop(); } catch { }

            try { _pumpThread?.Join(TimeSpan.FromSeconds(2)); } catch { }

            try { _session?.Dispose(); } catch { }
            _session = null;

            GC.SuppressFinalize(this);
        }
    }
}
