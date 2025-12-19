using System.Windows;

namespace ClickUpOverlay;

public partial class LogWindow : Window
{
    public LogWindow()
    {
        InitializeComponent();
    }

    public void AddLog(string message)
    {
        if (LogTextBox != null)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogTextBox.Text += $"[{timestamp}] {message}\n";
            LogTextBox.ScrollToEnd();
        }
    }

    public void ClearLogs()
    {
        if (LogTextBox != null)
        {
            LogTextBox.Text = "";
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        ClearLogs();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }
}

