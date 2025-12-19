using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using ClickUpOverlay.Services;

namespace ClickUpOverlay;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private TimerPollingService? _timerPollingService;
    private OverlayWindowManager? _overlayManager;
    private ConfigurationService? _configService;
    private MainWindow? _mainWindow;
    private LogWindow? _logWindow;
    private NotifyIcon? _notifyIcon;
    private bool _isPollingPaused;


    private System.Drawing.Icon CreateTrayIcon()
    {
        // Create a 16x16 bitmap for the icon
        using var bitmap = new System.Drawing.Bitmap(16, 16);
        using var graphics = System.Drawing.Graphics.FromImage(bitmap);
        
        // Use anti-aliasing for smoother rendering
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
        
        // Draw a simple timer/clock icon in red
        using var redBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(255, 255, 0, 0)); // Red
        using var redPen = new System.Drawing.Pen(redBrush, 2);
        
        // Draw a circle (clock face)
        graphics.DrawEllipse(redPen, 2, 2, 12, 12);
        
        // Draw clock hands (simple timer indicator)
        // Hour hand (pointing up)
        graphics.DrawLine(redPen, 8, 8, 8, 4);
        // Minute hand (pointing right)
        graphics.DrawLine(redPen, 8, 8, 12, 8);
        
        // Convert bitmap to icon
        var hIcon = bitmap.GetHicon();
        return System.Drawing.Icon.FromHandle(hIcon);
    }

    private void SetupSystemTray()
    {
        var customIcon = CreateTrayIcon();
        
        _notifyIcon = new NotifyIcon
        {
            Icon = customIcon,
            Text = "ClickUpOverlay",
            Visible = true
        };

        _notifyIcon.DoubleClick += (s, e) =>
        {
            if (_mainWindow != null)
            {
                _mainWindow.Show();
                _mainWindow.WindowState = WindowState.Normal;
                _mainWindow.Activate();
            }
        };

        var contextMenu = new ContextMenuStrip();
        
        var showSettingsItem = new ToolStripMenuItem("Show Settings");
        showSettingsItem.Click += (s, e) =>
        {
            if (_mainWindow != null)
            {
                _mainWindow.Show();
                _mainWindow.WindowState = WindowState.Normal;
                _mainWindow.Activate();
            }
        };
        contextMenu.Items.Add(showSettingsItem);

        var pauseResumeItem = new ToolStripMenuItem("Pause Polling");
        pauseResumeItem.Click += (s, e) =>
        {
            if (_isPollingPaused)
            {
                StartPolling();
                pauseResumeItem.Text = "Pause Polling";
            }
            else
            {
                _timerPollingService?.StopPolling();
                _overlayManager?.HideOverlays();
                _isPollingPaused = true;
                pauseResumeItem.Text = "Resume Polling";
            }
        };
        contextMenu.Items.Add(pauseResumeItem);

        contextMenu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (s, e) => Shutdown();
        contextMenu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = contextMenu;
    }

    private void StartPolling()
    {
        if (_timerPollingService == null || _configService == null)
            return;

        var config = _configService.Config;
        if (string.IsNullOrWhiteSpace(config.ApiToken) || string.IsNullOrWhiteSpace(config.TeamId))
            return;

        _timerPollingService.StartPolling(config.ApiToken, config.TeamId, config.PollIntervalSeconds);
        _isPollingPaused = false;
    }

    private void TimerPollingService_TimerStateChanged(object? sender, TimerStateChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            if (_overlayManager == null || _configService == null)
                return;

            var config = _configService.Config;
            var borderColor = _configService.GetBorderColor();

            if (e.IsRunning)
            {
                _overlayManager.ShowOverlays(borderColor, config.OverlayPosition, e.TaskName, e.StartTime);
            }
            else
            {
                _overlayManager.HideOverlays();
            }
        });
    }

    private void TimerPollingService_ErrorOccurred(object? sender, string message)
    {
        Dispatcher.Invoke(() =>
        {
            System.Windows.MessageBox.Show(message, "ClickUpOverlay Error", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            
            // If polling stopped due to errors, show the configuration window
            if (message.Contains("Polling stopped"))
            {
                if (_mainWindow != null)
                {
                    _mainWindow.Show();
                    _mainWindow.WindowState = WindowState.Normal;
                    _mainWindow.Activate();
                }
            }
        });
    }

    private void TimerPollingService_LogMessage(object? sender, string message)
    {
        Dispatcher.Invoke(() =>
        {
            if (_logWindow != null)
            {
                _logWindow.AddLog(message);
            }
        });
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Hide instead of close, so user can access via system tray
        if (sender is Window window)
        {
            e.Cancel = true;
            window.Hide();
        }
    }

    private void Application_Exit(object sender, System.Windows.ExitEventArgs e)
    {
        // Cleanup
        _timerPollingService?.StopPolling();
        _timerPollingService?.Dispose();
        _notifyIcon?.Dispose();
    }

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // Prevent application from shutting down when all windows are closed
        // We want to keep it running in the system tray
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        
        try
        {
            // Initialize services
            _configService = ConfigurationService.Instance;
            _overlayManager = new OverlayWindowManager();
            _timerPollingService = new TimerPollingService();
            
            // Make overlay manager and polling service accessible from MainWindow
            System.Windows.Application.Current.Properties["OverlayManager"] = _overlayManager;
            System.Windows.Application.Current.Properties["TimerPollingService"] = _timerPollingService;

            // Subscribe to events
            _timerPollingService.TimerStateChanged += TimerPollingService_TimerStateChanged;
            _timerPollingService.ErrorOccurred += TimerPollingService_ErrorOccurred;
            _timerPollingService.LogMessage += TimerPollingService_LogMessage;

            // Create and setup system tray icon
            SetupSystemTray();

            // Create main window but don't show it initially if configured
            _mainWindow = new MainWindow();
            _mainWindow.Closing += MainWindow_Closing;
            
            // Create log window (hidden by default)
            _logWindow = new LogWindow();
            _logWindow.Closing += (s, e) => { e.Cancel = true; _logWindow.Hide(); };
            System.Windows.Application.Current.Properties["LogWindow"] = _logWindow;
            
            // Set as main window so application knows it exists
            MainWindow = _mainWindow;

            // Check if we have valid configuration
            var config = _configService.Config;
            if (!string.IsNullOrWhiteSpace(config.ApiToken) && !string.IsNullOrWhiteSpace(config.TeamId))
            {
                // Start polling automatically
                StartPolling();
            }
            else
            {
                // Show configuration window if not configured
                _mainWindow.Show();
            }

            // Listen for configuration saves
            System.Windows.Application.Current.Properties["ConfigSaved"] = false;
            
            // Check for configuration save events
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            timer.Tick += (s, args) =>
            {
                if (System.Windows.Application.Current.Properties.Contains("ConfigSaved"))
                {
                    var configSavedObj = System.Windows.Application.Current.Properties["ConfigSaved"];
                    if (configSavedObj is bool saved && saved)
                    {
                        System.Windows.Application.Current.Properties["ConfigSaved"] = false;
                        
                        // Restart polling with new configuration
                        _timerPollingService?.StopPolling();
                        StartPolling();
                        
                        // Update overlay style if visible
                        if (_overlayManager != null && _configService != null)
                        {
                            var config = _configService.Config;
                            var borderColor = _configService.GetBorderColor();
                            _overlayManager.UpdateBorderStyle(borderColor, config.OverlayPosition);
                        }
                    }
                }
            };
            timer.Start();
        }
        catch (Exception ex)
        {
            var errorMessage = $"Error starting application: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}";
            System.Windows.MessageBox.Show(errorMessage, "Startup Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }
}

