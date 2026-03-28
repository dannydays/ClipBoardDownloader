using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CBDownloader.Services;
using CBDownloader.ViewModels;
using CBDownloader.Views;
using Forms = System.Windows.Forms;

namespace CBDownloader
{
    public partial class App : System.Windows.Application
    {
        private ClipboardMonitorService? _clipboardMonitor;
        private MainWindow? _mainWindow;
        private MainViewModel? _mainViewModel;
        private Forms.NotifyIcon? _notifyIcon;
        private EventWaitHandle? _showEvent;

        protected override void OnStartup(StartupEventArgs e)
        {
            try 
            {
                var existingEvent = EventWaitHandle.OpenExisting("CBDownloaderShowEvent");
                existingEvent.Set();
                Environment.Exit(0);
                return;
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "CBDownloaderShowEvent");
                Task.Run(() =>
                {
                    while (true)
                    {
                        _showEvent.WaitOne();
                        Dispatcher.Invoke(() =>
                        {
                            _mainWindow?.Show();
                            if (_mainWindow?.WindowState == WindowState.Minimized)
                                _mainWindow.WindowState = WindowState.Normal;
                            _mainWindow?.Activate();
                        });
                    }
                });
            }

            base.OnStartup(e);

            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            _mainViewModel = new MainViewModel();
            _mainWindow = new MainWindow
            {
                DataContext = _mainViewModel,
                Topmost = SettingsService.Current.AlwaysOnTop
            };

            ThemeManager.Initialize();

            SetupNotifyIcon();

            _clipboardMonitor = new ClipboardMonitorService();
            _clipboardMonitor.ClipboardUrlCopied += async (s, url) =>
            {
                if (_mainViewModel != null && _mainViewModel.IsBusy)
                {
                    return;
                }

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    _mainWindow?.Show();
                    _mainWindow?.Activate();
                    if (_mainViewModel != null) await _mainViewModel.InitializeAndFetchMetadata(url);
                });
            };

            var hwnd = new System.Windows.Interop.WindowInteropHelper(_mainWindow).EnsureHandle();
            _clipboardMonitor.StartMonitoring(_mainWindow);
        }

        private void SetupNotifyIcon()
        {
            _notifyIcon = new Forms.NotifyIcon();
            
            try
            {
                var iconUri = new Uri("pack://application:,,,/Assets/icon.png");
                var streamInfo = System.Windows.Application.GetResourceStream(iconUri);
                if (streamInfo != null)
                {
                    using (var bmp = new Bitmap(streamInfo.Stream))
                    {
                        var hIcon = bmp.GetHicon();
                        _notifyIcon.Icon = System.Drawing.Icon.FromHandle(hIcon);
                    }
                }
                else
                {
                    _notifyIcon.Icon = SystemIcons.Application;
                }
            }
            catch
            {
                _notifyIcon.Icon = SystemIcons.Application;
            }

            _notifyIcon.Visible = true;
            _notifyIcon.Text = "CBDownloader";
            _notifyIcon.DoubleClick += (s, args) =>
            {
                _mainWindow?.Show();
                _mainWindow?.Activate();
            };

            var contextMenu = new Forms.ContextMenuStrip();
            contextMenu.Items.Add("Show", null, (s, args) =>
            {
                _mainWindow?.Show();
                _mainWindow?.Activate();
            });
            contextMenu.Items.Add("Settings", null, (s, args) =>
            {
                ShowSettings();
            });
            contextMenu.Items.Add("Exit", null, (s, args) =>
            {
                _notifyIcon.Visible = false;
                System.Windows.Application.Current.Shutdown();
            });

            _notifyIcon.ContextMenuStrip = contextMenu;
        }

        internal void ShowNotification(string title, string message)
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.ShowBalloonTip(3000, title, message, Forms.ToolTipIcon.Info);
            }
        }

        private SettingsWindow? _settingsWindow;
        internal void ShowSettings()
        {
            Dispatcher.Invoke(() => 
            {
                if (_settingsWindow != null)
                {
                    _settingsWindow.Activate();
                    return;
                }

                _settingsWindow = new SettingsWindow
                {
                    Owner = _mainWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                
                _settingsWindow.Closed += (s, e) => _settingsWindow = null;
                _settingsWindow.ShowDialog();
            });
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _clipboardMonitor?.StopMonitoring();
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            base.OnExit(e);
        }
    }
}
