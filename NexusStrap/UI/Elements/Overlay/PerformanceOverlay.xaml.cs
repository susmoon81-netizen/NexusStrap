using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace NexusStrap.UI.Elements.Overlay
{
    /// <summary>
    /// Borderless click-through overlay pinned to the top-right of the screen.
    /// Shows the tracked Roblox process's real presented FPS (ETW, when
    /// available) plus its CPU and RAM usage as percentages. No background —
    /// green text only.
    /// </summary>
    public partial class PerformanceOverlay : Window
    {
        private readonly DispatcherTimer _updateTimer;
        private readonly Stopwatch _sampleStopwatch = Stopwatch.StartNew();
        private readonly FpsMonitor _fpsMonitor = new();
        private readonly long _totalPhysicalBytes;
        private Process? _robloxProcess;
        private TimeSpan _lastProcessorTime;
        private TimeSpan _lastSampleElapsed;
        private bool _hasCpuSample;

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(nint hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(nint hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern nint GetForegroundWindow();

        public PerformanceOverlay(int processId)
        {
            InitializeComponent();

            var workingArea = SystemParameters.WorkArea;
            Left = workingArea.Right - Width - 12;
            // Clears the in-game top-right buttons (leaderboard/menu).
            Top = workingArea.Top + 100;

            _totalPhysicalBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;

            try
            {
                _robloxProcess = Process.GetProcessById(processId);
                ResetCpuSampling();
            }
            catch
            {
                _robloxProcess = null;
            }

            _fpsMonitor.TryStart(processId);

            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _updateTimer.Tick += UpdateStats;
            _updateTimer.Start();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Click-through so the overlay never steals input from the game,
            // no Alt+Tab entry, never activates on click.
            try
            {
                nint hwnd = new WindowInteropHelper(this).Handle;
                int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("PerformanceOverlay::ClickThrough", ex);
            }
        }

        private void UpdateStats(object? sender, EventArgs e)
        {
            try
            {
                // Always drain the FPS queue so it cannot grow while hidden.
                int fps = _fpsMonitor.CurrentFps;

                if (_robloxProcess is null || _robloxProcess.HasExited)
                {
                    Visibility = Visibility.Hidden;
                    return;
                }

                _robloxProcess.Refresh();

                // Only float over the game itself: hide the moment any other
                // window (browser, Discord, desktop) takes focus.
                nint gameWindow = _robloxProcess.MainWindowHandle;
                if (gameWindow == 0 || GetForegroundWindow() != gameWindow)
                {
                    Visibility = Visibility.Hidden;
                    return;
                }

                if (Visibility != Visibility.Visible)
                    Visibility = Visibility.Visible;

                FpsText.Text = _fpsMonitor.IsAvailable ? $"FPS {fps}" : "FPS --";

                // CPU: process share across all cores, as a percentage.
                var elapsed = _sampleStopwatch.Elapsed;
                var processorTime = _robloxProcess.TotalProcessorTime;

                if (_hasCpuSample)
                {
                    double elapsedMs = (elapsed - _lastSampleElapsed).TotalMilliseconds;
                    double cpuMs = (processorTime - _lastProcessorTime).TotalMilliseconds;

                    if (elapsedMs > 0)
                    {
                        double cpu = cpuMs / (elapsedMs * Environment.ProcessorCount) * 100;
                        CpuText.Text = $"CPU {Math.Clamp(cpu, 0, 100):F0}%";
                    }
                }
                else
                {
                    _hasCpuSample = true;
                }

                _lastProcessorTime = processorTime;
                _lastSampleElapsed = elapsed;

                // RAM: process working set as a percentage of total system RAM.
                if (_totalPhysicalBytes > 0)
                {
                    double ramPct = (double)_robloxProcess.WorkingSet64 / _totalPhysicalBytes * 100;
                    RamText.Text = $"RAM {Math.Clamp(ramPct, 0, 100):F0}%";
                }
                else
                {
                    RamText.Text = "RAM --";
                }
            }
            catch
            {
                FpsText.Text = "FPS --";
                CpuText.Text = "CPU --";
                RamText.Text = "RAM --";
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _updateTimer.Stop();
            _fpsMonitor.Dispose();
            _robloxProcess?.Dispose();
            base.OnClosed(e);
        }

        private void ResetCpuSampling()
        {
            _hasCpuSample = false;
            _lastProcessorTime = TimeSpan.Zero;
            _lastSampleElapsed = _sampleStopwatch.Elapsed;
        }
    }
}
