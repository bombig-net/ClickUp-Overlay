using System.IO;
using System.Text.Json;
using System.Windows.Media;

namespace ClickUpOverlay.Services;

public class AppConfiguration
{
    public string ApiToken { get; set; } = string.Empty;
    public string TeamId { get; set; } = string.Empty;
    public int PollIntervalSeconds { get; set; } = 5;
    public string BorderColor { get; set; } = "#FF0000"; // Red
    public string OverlayPosition { get; set; } = "TopRight"; // TopRight, Top, TopLeft, Right, Left, BottomRight, Bottom, BottomLeft
}

public class ConfigurationService
{
    private static readonly Lazy<ConfigurationService> _instance = new(() => new ConfigurationService());
    private static readonly object _lock = new();
    private readonly string _configPath;
    private AppConfiguration _config;

    public static ConfigurationService Instance => _instance.Value;

    public AppConfiguration Config
    {
        get
        {
            lock (_lock)
            {
                return _config;
            }
        }
    }

    private ConfigurationService()
    {
        var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        _configPath = Path.Combine(appDirectory, "config.json");
        _config = LoadConfiguration();
    }

    private AppConfiguration LoadConfiguration()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                var config = JsonSerializer.Deserialize<AppConfiguration>(json);
                if (config != null)
                {
                    return config;
                }
            }
        }
        catch (Exception)
        {
            // If loading fails, return default configuration
        }

        return new AppConfiguration();
    }

    public void SaveConfiguration()
    {
        lock (_lock)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(_config, options);
                File.WriteAllText(_configPath, json);
            }
            catch (Exception)
            {
                // Log error if needed, but don't throw
            }
        }
    }

    public void UpdateConfiguration(Action<AppConfiguration> updateAction)
    {
        lock (_lock)
        {
            updateAction(_config);
            SaveConfiguration();
        }
    }

    public System.Windows.Media.Color GetBorderColor()
    {
        try
        {
            var color = (System.Windows.Media.Color?)System.Windows.Media.ColorConverter.ConvertFromString(Config.BorderColor);
            return color ?? System.Windows.Media.Colors.Red;
        }
        catch
        {
            return System.Windows.Media.Colors.Red;
        }
    }

    public void ResetConfiguration()
    {
        lock (_lock)
        {
            // Reset to default values
            _config = new AppConfiguration();
            
            // Delete config file if it exists
            try
            {
                if (File.Exists(_configPath))
                {
                    File.Delete(_configPath);
                }
            }
            catch (Exception)
            {
                // If deletion fails, just reset the in-memory config
            }
        }
    }
}

