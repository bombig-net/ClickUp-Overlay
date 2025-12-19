using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using ClickUpOverlay.Services;

namespace ClickUpOverlay;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly ConfigurationService _configService;

    public MainWindow()
    {
        InitializeComponent();
        _configService = ConfigurationService.Instance;
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        LoadConfiguration();
    }

    private void LoadConfiguration()
    {
        try
        {
            var config = _configService.Config;
            
            // Temporarily remove event handlers to prevent events during initialization
            if (PollIntervalSlider != null)
                PollIntervalSlider.ValueChanged -= PollIntervalSlider_ValueChanged;
            if (BorderColorBox != null)
                BorderColorBox.TextChanged -= BorderColorBox_TextChanged;
            
            // Note: PasswordBox doesn't support binding well, so we'll leave it empty for security
            // User will need to re-enter if they want to change it
            if (TeamIdBox != null)
                TeamIdBox.Text = config.TeamId;
            if (PollIntervalSlider != null)
            {
                PollIntervalSlider.Value = config.PollIntervalSeconds;
                if (PollIntervalValue != null)
                    PollIntervalValue.Text = config.PollIntervalSeconds.ToString();
            }
            if (BorderColorBox != null)
                BorderColorBox.Text = config.BorderColor;
            
            // Set overlay position
            SetPositionRadioButton(config.OverlayPosition);
            
            // Re-attach event handlers
            if (PollIntervalSlider != null)
                PollIntervalSlider.ValueChanged += PollIntervalSlider_ValueChanged;
            if (BorderColorBox != null)
                BorderColorBox.TextChanged += BorderColorBox_TextChanged;
            
            UpdateColorPreview();
            UpdateStatus();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error loading configuration: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApiTokenBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        // Skip if not fully loaded yet
        if (!IsLoaded)
            return;
        UpdateStatus();
    }

    private void TeamIdBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        // Skip if not fully loaded yet
        if (!IsLoaded)
            return;
        UpdateStatus();
    }

    private void PollIntervalSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (PollIntervalValue == null)
            return;
            
        var value = (int)e.NewValue;
        PollIntervalValue.Text = value.ToString();
        UpdateStatus();
    }

    private void BorderColorBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        // Skip if not fully loaded yet
        if (!IsLoaded)
            return;
        UpdateColorPreview();
        UpdateStatus();
    }

    private void PositionRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;
        UpdateStatus();
    }

    private void SetPositionRadioButton(string position)
    {
        var radioButton = position switch
        {
            "TopLeft" => PositionTopLeft,
            "Top" => PositionTop,
            "TopRight" => PositionTopRight,
            "Left" => PositionLeft,
            "Right" => PositionRight,
            "BottomLeft" => PositionBottomLeft,
            "Bottom" => PositionBottom,
            "BottomRight" => PositionBottomRight,
            _ => PositionTopRight
        };
        
        if (radioButton != null)
            radioButton.IsChecked = true;
    }

    private string GetSelectedPosition()
    {
        if (PositionTopLeft?.IsChecked == true) return "TopLeft";
        if (PositionTop?.IsChecked == true) return "Top";
        if (PositionTopRight?.IsChecked == true) return "TopRight";
        if (PositionLeft?.IsChecked == true) return "Left";
        if (PositionRight?.IsChecked == true) return "Right";
        if (PositionBottomLeft?.IsChecked == true) return "BottomLeft";
        if (PositionBottom?.IsChecked == true) return "Bottom";
        if (PositionBottomRight?.IsChecked == true) return "BottomRight";
        return "TopRight"; // Default
    }

    private void UpdateColorPreview()
    {
        if (BorderColorBox == null || ColorPreview == null)
            return;
            
        try
        {
            var color = (System.Windows.Media.Color?)System.Windows.Media.ColorConverter.ConvertFromString(BorderColorBox.Text);
            if (color.HasValue)
            {
                ColorPreview.Fill = new SolidColorBrush(color.Value);
            }
            else
            {
                ColorPreview.Fill = System.Windows.Media.Brushes.Red;
            }
        }
        catch
        {
            if (ColorPreview != null)
                ColorPreview.Fill = System.Windows.Media.Brushes.Red;
        }
    }

    private void UpdateStatus()
    {
        if (ApiTokenBox == null || TeamIdBox == null || StatusLabel == null || SaveButton == null)
            return;
            
        var hasToken = !string.IsNullOrWhiteSpace(ApiTokenBox.Password) || !string.IsNullOrWhiteSpace(_configService.Config.ApiToken);
        var hasTeamId = !string.IsNullOrWhiteSpace(TeamIdBox.Text);

        var successColor = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Application.Current.Resources["SuccessColorValue"]);
        var warningColor = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Application.Current.Resources["WarningColorValue"]);
        
        if (hasToken && hasTeamId)
        {
            StatusLabel.Text = "Status: Ready to save";
            StatusLabel.Foreground = successColor;
            SaveButton.IsEnabled = true;
        }
        else
        {
            StatusLabel.Text = "Status: Please configure API Token and Team ID";
            StatusLabel.Foreground = warningColor;
            SaveButton.IsEnabled = false;
        }
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var apiToken = ApiTokenBox.Password;
        if (string.IsNullOrWhiteSpace(apiToken))
        {
            // Use existing token if password box is empty
            apiToken = _configService.Config.ApiToken;
        }

        if (string.IsNullOrWhiteSpace(apiToken))
        {
            System.Windows.MessageBox.Show("Please enter an API Token.", "Configuration Error", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(TeamIdBox.Text))
        {
            System.Windows.MessageBox.Show("Please enter a Team ID.", "Configuration Error", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Validate color
        try
        {
            var color = (System.Windows.Media.Color?)System.Windows.Media.ColorConverter.ConvertFromString(BorderColorBox.Text);
            if (!color.HasValue)
            {
                System.Windows.MessageBox.Show("Invalid color format. Please use hex format (e.g., #FF0000).", 
                    "Configuration Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
        catch
        {
            System.Windows.MessageBox.Show("Invalid color format. Please use hex format (e.g., #FF0000).", 
                "Configuration Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Test API connection before saving
        SaveButton.IsEnabled = false;
        StatusLabel.Text = "Status: Testing connection...";
        StatusLabel.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Application.Current.Resources["TextSecondaryColorValue"]);

        try
        {
            var testService = new Services.TimerPollingService();
            var (isValid, errorMessage) = await testService.TestConnectionWithDetails(apiToken, TeamIdBox.Text);
            testService.Dispose();

            if (!isValid)
            {
                StatusLabel.Text = "Status: Connection test failed";
                StatusLabel.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Application.Current.Resources["ErrorColorValue"]);
                SaveButton.IsEnabled = true;
                
                var message = "Connection test failed.\n\n";
                if (!string.IsNullOrEmpty(errorMessage))
                {
                    message += errorMessage + "\n\n";
                }
                message += "The configuration was NOT saved. Please correct the values and try again.\n\n";
                message += "Tip: Click the 'Help' button for step-by-step instructions.";
                
                System.Windows.MessageBox.Show(
                    message,
                    "Connection Test Failed", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Connection is valid, save configuration
            _configService.UpdateConfiguration(config =>
            {
                config.ApiToken = apiToken;
                config.TeamId = TeamIdBox.Text;
                config.PollIntervalSeconds = (int)PollIntervalSlider.Value;
                config.BorderColor = BorderColorBox.Text;
                config.OverlayPosition = GetSelectedPosition();
            });

            StatusLabel.Text = "Status: Configuration saved successfully!";
            StatusLabel.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Application.Current.Resources["SuccessColorValue"]);
            
            // Clear password box for security
            ApiTokenBox.Password = string.Empty;

            // Notify that configuration was saved (will be handled by App.xaml.cs)
            System.Windows.Application.Current.Properties["ConfigSaved"] = true;
        }
        catch (Exception ex)
        {
            StatusLabel.Text = "Status: Error testing connection";
            StatusLabel.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Application.Current.Resources["ErrorColorValue"]);
            System.Windows.MessageBox.Show(
                $"Error testing connection: {ex.Message}\n\nThe configuration was NOT saved.",
                "Connection Test Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SaveButton.IsEnabled = true;
        }
    }

    private async void TestOverlayButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Get overlay manager from application properties
            if (System.Windows.Application.Current.Properties.Contains("OverlayManager") &&
                System.Windows.Application.Current.Properties["OverlayManager"] is Services.OverlayWindowManager overlayManager)
            {
                // Get current UI values (not saved configuration)
                System.Windows.Media.Color borderColor;
                string overlayPosition;
                
                // Parse color from UI
                try
                {
                    var color = (System.Windows.Media.Color?)System.Windows.Media.ColorConverter.ConvertFromString(BorderColorBox?.Text ?? "#FF0000");
                    borderColor = color ?? System.Windows.Media.Colors.Red;
                }
                catch
                {
                    borderColor = System.Windows.Media.Colors.Red;
                }
                
                // Get position from UI
                overlayPosition = GetSelectedPosition();
                
                // Show overlay with current UI settings (test mode - no task info)
                overlayManager.ShowOverlays(borderColor, overlayPosition, "Test Overlay", DateTime.Now);
                
                // Disable button during test
                if (TestOverlayButton != null)
                    TestOverlayButton.IsEnabled = false;
                
                // Wait 3 seconds
                await Task.Delay(3000);
                
                // Hide overlay
                overlayManager.HideOverlays();
                
                // Re-enable button
                if (TestOverlayButton != null)
                    TestOverlayButton.IsEnabled = true;
            }
            else
            {
                System.Windows.MessageBox.Show("Overlay manager not available.", "Test Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error testing overlay: {ex.Message}", "Test Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
            if (TestOverlayButton != null)
                TestOverlayButton.IsEnabled = true;
        }
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        // Ask for confirmation
        var result = System.Windows.MessageBox.Show(
            "Are you sure you want to reset everything?\n\n" +
            "This will:\n" +
            "- Clear all configuration\n" +
            "- Stop polling\n" +
            "- Delete saved settings\n\n" +
            "This action cannot be undone.",
            "Reset Everything",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            // Stop polling if running
            if (System.Windows.Application.Current.Properties.Contains("TimerPollingService") &&
                System.Windows.Application.Current.Properties["TimerPollingService"] is Services.TimerPollingService pollingService)
            {
                pollingService.StopPolling();
            }

            // Reset configuration
            _configService.ResetConfiguration();

            // Clear all UI fields
            if (ApiTokenBox != null)
                ApiTokenBox.Password = string.Empty;
            if (TeamIdBox != null)
                TeamIdBox.Text = string.Empty;
            if (PollIntervalSlider != null)
            {
                PollIntervalSlider.Value = 5;
                if (PollIntervalValue != null)
                    PollIntervalValue.Text = "5";
            }
            if (BorderColorBox != null)
                BorderColorBox.Text = "#FF0000";
            
            // Reset position to default
            SetPositionRadioButton("TopRight");

            // Update UI
            UpdateColorPreview();
            UpdateStatus();

            StatusLabel.Text = "Status: Configuration reset. Please configure again.";
            StatusLabel.Foreground = System.Windows.Media.Brushes.Orange;

            System.Windows.MessageBox.Show(
                "Configuration has been reset successfully.\n\n" +
                "All settings have been cleared and polling has been stopped.",
                "Reset Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Error resetting configuration: {ex.Message}",
                "Reset Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        var helpWindow = new HelpWindow();
        helpWindow.ShowDialog();
    }

    private void LogButton_Click(object sender, RoutedEventArgs e)
    {
        if (System.Windows.Application.Current.Properties.Contains("LogWindow") &&
            System.Windows.Application.Current.Properties["LogWindow"] is LogWindow logWindow)
        {
            logWindow.Show();
            logWindow.WindowState = WindowState.Normal;
            logWindow.Activate();
        }
    }
}