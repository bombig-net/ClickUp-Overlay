using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using ClickUpOverlay.Win32;

namespace ClickUpOverlay.Services;

public class OverlayWindowManager
{
    private readonly List<Window> _overlayWindows = new();
    private readonly object _lock = new();
    private string? _currentTaskName;
    private DateTime? _startTime;
    private DispatcherTimer? _updateTimer;

    public void ShowOverlays(System.Windows.Media.Color borderColor, string overlayPosition, string? taskName = null, DateTime? startTime = null)
    {
        lock (_lock)
        {
            _currentTaskName = taskName;
            _startTime = startTime;
            
            if (_overlayWindows.Count > 0)
            {
                // Update existing overlays
                UpdateOverlayContent(borderColor, overlayPosition);
                return;
            }

            // Create overlay for each monitor
            foreach (var screen in Screen.AllScreens)
            {
                var overlayWindow = CreateOverlayWindow(screen, borderColor, overlayPosition);
                _overlayWindows.Add(overlayWindow);
                
                // Add fade-in animation
                overlayWindow.Opacity = 0;
                overlayWindow.Show();
                
                var fadeIn = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = new Duration(TimeSpan.FromMilliseconds(300))
                };
                overlayWindow.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            }
            
            // Start timer to update elapsed time
            StartUpdateTimer();
        }
    }

    public void HideOverlays()
    {
        lock (_lock)
        {
            StopUpdateTimer();
            foreach (var window in _overlayWindows)
            {
                window.Close();
            }
            _overlayWindows.Clear();
            _currentTaskName = null;
            _startTime = null;
        }
    }

    public void UpdateOverlayInfo(string? taskName, DateTime? startTime)
    {
        lock (_lock)
        {
            _currentTaskName = taskName;
            _startTime = startTime;
            UpdateOverlayContent(System.Windows.Media.Colors.Red, "TopRight"); // Use default values, will be updated
        }
    }

    private void UpdateOverlayContent(System.Windows.Media.Color borderColor, string overlayPosition)
    {
        var cornerThickness = 3.5; // Fixed thickness for corner indicator
        
        foreach (var window in _overlayWindows)
        {
            if (window.Content is System.Windows.Controls.Grid grid)
            {
                // Update corner indicator lines
                foreach (var child in grid.Children.OfType<Line>())
                {
                    child.Stroke = new SolidColorBrush(borderColor);
                    child.StrokeThickness = cornerThickness;
                    // Update glow effect color
                    if (child.Effect is System.Windows.Media.Effects.DropShadowEffect shadow)
                    {
                        shadow.Color = borderColor;
                    }
                }
                
                // Update badge
                var badge = grid.Children.OfType<System.Windows.Controls.Border>().FirstOrDefault();
                if (badge != null)
                {
                    // Update badge border color
                    badge.BorderBrush = new SolidColorBrush(borderColor);
                    
                    // Find text blocks inside badge
                    if (badge.Child is System.Windows.Controls.StackPanel stackPanel)
                    {
                        var textBlocks = stackPanel.Children.OfType<System.Windows.Controls.TextBlock>().ToList();
                        if (textBlocks.Count >= 2)
                        {
                            var taskBlock = textBlocks[0];
                            var timeBlock = textBlocks[1];
                            
                            // Update task name color
                            taskBlock.Foreground = new SolidColorBrush(borderColor);
                            UpdateBadgeText(taskBlock, timeBlock);
                        }
                    }
                }
            }
        }
    }

    public void UpdateBorderStyle(System.Windows.Media.Color borderColor, string overlayPosition)
    {
        lock (_lock)
        {
            UpdateOverlayContent(borderColor, overlayPosition);
        }
    }

    private Window CreateOverlayWindow(Screen screen, System.Windows.Media.Color borderColor, string overlayPosition)
    {
        var window = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
            Topmost = true,
            ShowInTaskbar = false,
            ResizeMode = ResizeMode.NoResize,
            Left = screen.Bounds.Left,
            Top = screen.Bounds.Top,
            Width = screen.Bounds.Width,
            Height = screen.Bounds.Height,
            WindowState = WindowState.Normal
        };

        var grid = new System.Windows.Controls.Grid
        {
            Margin = new Thickness(0)
        };

        // Create corner bracket indicator based on position
        var cornerLength = 110.0; // Length of corner bracket sides
        var cornerMargin = 25.0; // Margin from edges
        var cornerThickness = 3.5; // Fixed thickness

        var (cornerX, cornerY, horizontalX1, horizontalX2, horizontalY, verticalX, verticalY1, verticalY2) = 
            CalculateCornerPosition(screen.Bounds.Width, screen.Bounds.Height, overlayPosition, cornerLength, cornerMargin);

        // Horizontal line (top/bottom part of bracket)
        var cornerHorizontal = new Line
        {
            X1 = horizontalX1,
            Y1 = horizontalY,
            X2 = horizontalX2,
            Y2 = horizontalY,
            Stroke = new SolidColorBrush(borderColor),
            StrokeThickness = cornerThickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = borderColor,
                Direction = 315,
                ShadowDepth = 0,
                BlurRadius = 8,
                Opacity = 0.4
            }
        };
        
        // Vertical line (left/right part of bracket)
        var cornerVertical = new Line
        {
            X1 = verticalX,
            Y1 = verticalY1,
            X2 = verticalX,
            Y2 = verticalY2,
            Stroke = new SolidColorBrush(borderColor),
            StrokeThickness = cornerThickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = borderColor,
                Direction = 315,
                ShadowDepth = 0,
                BlurRadius = 8,
                Opacity = 0.4
            }
        };
        
        grid.Children.Add(cornerHorizontal);
        grid.Children.Add(cornerVertical);

        // Create task badge based on position
        var badge = CreateTaskBadge(borderColor, screen.Bounds.Width, screen.Bounds.Height, overlayPosition);
        grid.Children.Add(badge);

        window.Content = grid;

        // Apply Win32 styles for click-through and always-on-top
        Win32Interop.MakeWindowClickThrough(window);
        Win32Interop.MakeWindowAlwaysOnTop(window);

        return window;
    }

    private (System.Windows.HorizontalAlignment hAlign, System.Windows.VerticalAlignment vAlign, Thickness margin) 
        CalculateBadgePosition(string position, double screenWidth, double screenHeight)
    {
        var badgeMargin = 35.0; // Distance from edge
        var badgeTopOffset = 50.0; // Distance below corner indicator for corner positions
        
        return position switch
        {
            "TopRight" => (System.Windows.HorizontalAlignment.Right, System.Windows.VerticalAlignment.Top, new Thickness(0, badgeTopOffset, badgeMargin, 0)),
            "TopLeft" => (System.Windows.HorizontalAlignment.Left, System.Windows.VerticalAlignment.Top, new Thickness(badgeMargin, badgeTopOffset, 0, 0)),
            "BottomRight" => (System.Windows.HorizontalAlignment.Right, System.Windows.VerticalAlignment.Bottom, new Thickness(0, 0, badgeMargin, badgeTopOffset)),
            "BottomLeft" => (System.Windows.HorizontalAlignment.Left, System.Windows.VerticalAlignment.Bottom, new Thickness(badgeMargin, 0, 0, badgeTopOffset)),
            "Top" => (System.Windows.HorizontalAlignment.Center, System.Windows.VerticalAlignment.Top, new Thickness(0, badgeTopOffset, 0, 0)),
            "Bottom" => (System.Windows.HorizontalAlignment.Center, System.Windows.VerticalAlignment.Bottom, new Thickness(0, 0, 0, badgeTopOffset)),
            "Right" => (System.Windows.HorizontalAlignment.Right, System.Windows.VerticalAlignment.Center, new Thickness(0, 0, badgeMargin, 0)),
            "Left" => (System.Windows.HorizontalAlignment.Left, System.Windows.VerticalAlignment.Center, new Thickness(badgeMargin, 0, 0, 0)),
            _ => (System.Windows.HorizontalAlignment.Right, System.Windows.VerticalAlignment.Top, new Thickness(0, badgeTopOffset, badgeMargin, 0)) // Default to TopRight
        };
    }

    private (double cornerX, double cornerY, double hX1, double hX2, double hY, double vX, double vY1, double vY2) 
        CalculateCornerPosition(double screenWidth, double screenHeight, string position, double cornerLength, double margin)
    {
        return position switch
        {
            "TopRight" => (screenWidth - margin, margin, screenWidth - margin - cornerLength, screenWidth - margin, margin, screenWidth - margin, margin, margin + cornerLength),
            "TopLeft" => (margin, margin, margin, margin + cornerLength, margin, margin, margin, margin + cornerLength),
            "BottomRight" => (screenWidth - margin, screenHeight - margin, screenWidth - margin - cornerLength, screenWidth - margin, screenHeight - margin, screenWidth - margin, screenHeight - margin - cornerLength, screenHeight - margin),
            "BottomLeft" => (margin, screenHeight - margin, margin, margin + cornerLength, screenHeight - margin, margin, screenHeight - margin - cornerLength, screenHeight - margin),
            "Top" => (screenWidth / 2, margin, screenWidth / 2 - cornerLength / 2, screenWidth / 2 + cornerLength / 2, margin, screenWidth / 2, margin, margin + cornerLength),
            "Bottom" => (screenWidth / 2, screenHeight - margin, screenWidth / 2 - cornerLength / 2, screenWidth / 2 + cornerLength / 2, screenHeight - margin, screenWidth / 2, screenHeight - margin - cornerLength, screenHeight - margin),
            "Right" => (screenWidth - margin, screenHeight / 2, screenWidth - margin - cornerLength, screenWidth - margin, screenHeight / 2, screenWidth - margin, screenHeight / 2 - cornerLength / 2, screenHeight / 2 + cornerLength / 2),
            "Left" => (margin, screenHeight / 2, margin, margin + cornerLength, screenHeight / 2, margin, screenHeight / 2 - cornerLength / 2, screenHeight / 2 + cornerLength / 2),
            _ => (screenWidth - margin, margin, screenWidth - margin - cornerLength, screenWidth - margin, margin, screenWidth - margin, margin, margin + cornerLength) // Default to TopRight
        };
    }

    private System.Windows.Controls.Border CreateTaskBadge(System.Windows.Media.Color accentColor, double screenWidth, double screenHeight, string position)
    {
        // Calculate badge position and alignment based on overlay position
        var (horizontalAlign, verticalAlign, margin) = CalculateBadgePosition(position, screenWidth, screenHeight);
        
        // Create badge container with semi-transparent background
        var badge = new System.Windows.Controls.Border
        {
            Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(230, 20, 20, 20)), // ~90% opacity dark background for better contrast
            BorderBrush = new SolidColorBrush(accentColor),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 14, 16, 14),
            MaxWidth = 360,
            HorizontalAlignment = horizontalAlign,
            VerticalAlignment = verticalAlign,
            Margin = margin,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = System.Windows.Media.Colors.Black,
                Direction = 315,
                ShadowDepth = 6,
                BlurRadius = 16,
                Opacity = 0.6
            }
        };

        // Create vertical stack for task name and elapsed time
        var stackPanel = new System.Windows.Controls.StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Vertical
        };

        // Task name text block
        var taskNameBlock = new System.Windows.Controls.TextBlock
        {
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
            FontSize = 17,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(accentColor),
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 320,
            LineHeight = 22
        };
        
        // Elapsed time text block
        var timeBlock = new System.Windows.Controls.TextBlock
        {
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
            FontSize = 15,
            FontWeight = FontWeights.Regular,
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(224, 224, 224)), // #E0E0E0
            Margin = new Thickness(0, 5, 0, 0),
            LineHeight = 20
        };

        // Update text content
        UpdateBadgeText(taskNameBlock, timeBlock);

        stackPanel.Children.Add(taskNameBlock);
        stackPanel.Children.Add(timeBlock);
        badge.Child = stackPanel;

        return badge;
    }

    private void UpdateBadgeText(System.Windows.Controls.TextBlock taskNameBlock, System.Windows.Controls.TextBlock timeBlock)
    {
        // Update task name
        if (!string.IsNullOrWhiteSpace(_currentTaskName))
        {
            taskNameBlock.Text = _currentTaskName;
        }
        else
        {
            taskNameBlock.Text = "Timer Running";
        }

        // Update elapsed time
        if (_startTime.HasValue)
        {
            var elapsed = DateTime.Now - _startTime.Value;
            if (elapsed.TotalSeconds < 0)
            {
                elapsed = TimeSpan.Zero;
            }
            timeBlock.Text = FormatElapsedTime(elapsed);
        }
        else
        {
            timeBlock.Text = "0s";
        }
    }


    private string FormatElapsedTime(TimeSpan elapsed)
    {
        if (elapsed.TotalHours >= 1)
        {
            return $"{(int)elapsed.TotalHours}h {elapsed.Minutes:D2}m {elapsed.Seconds:D2}s";
        }
        else if (elapsed.TotalMinutes >= 1)
        {
            return $"{elapsed.Minutes}m {elapsed.Seconds:D2}s";
        }
        else
        {
            return $"{elapsed.Seconds}s";
        }
    }

    private void StartUpdateTimer()
    {
        StopUpdateTimer();
        
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _updateTimer.Tick += (s, e) =>
        {
            lock (_lock)
            {
                foreach (var window in _overlayWindows)
                {
                    if (window.Content is System.Windows.Controls.Grid grid)
                    {
                        // Update badge elapsed time
                        var badge = grid.Children.OfType<System.Windows.Controls.Border>().FirstOrDefault();
                        if (badge != null && badge.Child is System.Windows.Controls.StackPanel stackPanel)
                        {
                            var textBlocks = stackPanel.Children.OfType<System.Windows.Controls.TextBlock>().ToList();
                            if (textBlocks.Count >= 2)
                            {
                                UpdateBadgeText(textBlocks[0], textBlocks[1]);
                            }
                        }
                    }
                }
            }
        };
        _updateTimer.Start();
    }

    private void StopUpdateTimer()
    {
        if (_updateTimer != null)
        {
            _updateTimer.Stop();
            _updateTimer = null;
        }
    }

    public void RefreshMonitors(System.Windows.Media.Color borderColor, int borderThickness)
    {
        lock (_lock)
        {
            // Close existing windows
            foreach (var window in _overlayWindows)
            {
                window.Close();
            }
            _overlayWindows.Clear();

            // Recreate for current monitor configuration
            if (_overlayWindows.Count == 0)
            {
                // Only recreate if we were showing overlays
                // This method is called when monitors change, but we need to know if overlays should be visible
                // For now, we'll just clear - the ShowOverlays will be called again if needed
            }
        }
    }
}
