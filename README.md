# ClickUpOverlay

A Windows desktop application that displays a visual overlay indicator when a ClickUp timer is active. The overlay appears on all monitors with customizable corner indicators showing the current task name and elapsed time.

## Features

- **Visual Overlay Indicator**: Red corner brackets appear on all monitors when a ClickUp timer is running
- **Multi-Monitor Support**: Overlay automatically appears on all connected displays
- **System Tray Integration**: Runs in the background with a system tray icon for easy access
- **Task Information Display**: Shows the current task name and elapsed time in a styled badge
- **Customizable Appearance**: 
  - Choose from 9 overlay positions (corners and edges)
  - Customizable border color (hex color picker)
- **Automatic Polling**: Configurable polling interval (2-60 seconds) to check ClickUp timer status
- **Connection Validation**: Tests API connection before starting polling to prevent errors
- **Debug Logging**: Built-in log window to troubleshoot API responses and timer parsing
- **Test Mode**: Preview overlay without ClickUp connection using the test button

## Requirements

- **.NET 8.0 Runtime** (or .NET 8.0 SDK for building from source)
- **Windows 10/11**
- **ClickUp Account** with API access
- **Internet Connection** (for API polling)

## Installation

### Build from Source

1. Clone this repository:
   ```bash
   git clone <repository-url>
   cd ClickUpOverlay
   ```

2. Ensure you have the [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) installed

3. Build the project:
   ```bash
   dotnet build
   ```

4. Run the application:
   ```bash
   dotnet run
   ```

   Or run the compiled executable:
   ```
   bin\Debug\net8.0-windows\ClickUpOverlay.exe
   ```

### Step 1: Get Your ClickUp API Token

1. Log in to ClickUp at [https://app.clickup.com](https://app.clickup.com)
2. Click on your profile picture/avatar in the bottom left corner
3. Select **Apps** from the menu
4. Click on **API** in the left sidebar
5. Click **Generate** to create a new API token (or copy an existing one)
6. Copy the token - it looks like: `pk_xxxxxxxxxxxxxxxxxxxxxxxxxxxxx`

### Step 2: Find Your Team ID

1. In ClickUp, go to any Workspace
2. Look at the URL in your browser - it will look like:
   ```
   https://app.clickup.com/1234567/v/li/987654321
   ```
3. The Team ID is the first number after `/`, in this example: **1234567**

### Step 3: Configure the Application

1. Launch the application
2. Click the system tray icon (red clock) or the application window should appear
3. Enter your **API Token** in the password field
4. Enter your **Team ID** in the text field
5. Adjust other settings as desired:
   - **Poll Interval**: How often to check for timer status (default: 5 seconds)
   - **Border Color**: Color of the overlay indicator (default: Red #FF0000)
   - **Overlay Position**: Where to display the overlay (default: TopRight)
6. Click **Save Configuration**
   - The app will automatically test the connection
   - If successful, polling will start automatically
   - If there's an error, check the error message and verify your credentials

### Step 4: Test the Overlay

1. Click the **Test Overlay** button to preview the overlay without a ClickUp connection
2. The overlay should appear for 3 seconds showing the configured settings
3. Start a timer in ClickUp to see the real overlay in action

## Configuration

The application stores configuration in `config.json` (created automatically in the same directory as the executable). This file contains:

- **ApiToken**: Your ClickUp API token (stored in plain text - keep this file secure!)
- **TeamId**: Your ClickUp Team ID
- **PollIntervalSeconds**: How often to poll the API (2-60 seconds, minimum 2 seconds to respect rate limits)
- **BorderColor**: Hex color code for the overlay (e.g., `#FF0000` for red)
- **OverlayPosition**: Position of the overlay indicator. Options:
  - `TopLeft`, `Top`, `TopRight`
  - `Left`, `Right`
  - `BottomLeft`, `Bottom`, `BottomRight`
  - `Center` (not recommended - may block content)

### Example Configuration

```json
{
  "ApiToken": "pk_your_token_here",
  "TeamId": "1234567",
  "PollIntervalSeconds": 5,
  "BorderColor": "#FF0000",
  "OverlayPosition": "TopRight"
}
```

**Security Note**: The `config.json` file contains your API token in plain text. Keep this file secure and never commit it to version control. 

TODO: Consider using Windows Credential Manager or file encryption for production use. (Commits Welcome!)

## Usage

### System Tray

The application runs in the system tray with a red clock icon. Right-click the icon to access:

- **Show Settings**: Opens the configuration window
- **Pause/Resume Polling**: Temporarily stop or start API polling
- **Exit**: Close the application

Double-click the tray icon to open the settings window.

### Overlay Behavior

- The overlay automatically appears when a ClickUp timer is running
- It displays on all connected monitors
- The overlay shows:
  - Corner bracket indicators in your chosen color
  - A badge with the current task name
  - Elapsed time (updates every second)
- The overlay disappears when the timer stops
- The overlay is click-through (doesn't interfere with your work)
- The overlay is always on top of other windows

### Settings Window

- **API Token**: Your ClickUp API token (masked for security)
- **Team ID**: Your ClickUp Team ID
- **Poll Interval**: Slider to adjust polling frequency (2-60 seconds)
- **Border Color**: Hex color input (e.g., `#FF0000`)
- **Overlay Position**: Visual grid to select overlay position
- **Test Overlay**: Preview the overlay with current settings
- **Log**: Open debug log window to view API responses
- **Reset Everything**: Clear all configuration and start fresh
- **Save Configuration**: Save settings and test connection

### Log Window

The log window shows:
- Raw API responses from ClickUp
- Parsed timer data
- Start time calculations
- Error messages

Use this for debugging if the timer display is incorrect or connection issues occur.

## Troubleshooting

### Connection Errors

**"Invalid API token"**
- Verify you copied the full token correctly (starts with `pk_`)
- Check that the token hasn't expired
- Ensure there are no extra spaces before/after the token
- ClickUp API does NOT use "Bearer" prefix - the token is sent directly

**"Team ID not found"**
- Verify the Team ID is correct (numbers only, no spaces)
- Check the URL method in the Quick Start guide
- Ensure you're using the Team ID, not Workspace ID

**"Connection test failed"**
- Check your internet connection
- Verify ClickUp API is accessible: [https://api.clickup.com](https://api.clickup.com)
- Try the Help button for detailed API setup instructions

### Overlay Not Appearing

- Check that a timer is actually running in ClickUp
- Verify the overlay position isn't set to a location outside your screen bounds
- Try the "Test Overlay" button to verify overlay functionality
- Check the system tray icon - if it's grayed out, polling may have stopped
- Open the Log window to see if API calls are succeeding

### Timer Not Updating

- Check the Log window for API response errors
- Verify your polling interval isn't too long
- Ensure polling hasn't been paused (check system tray menu)
- After 3 consecutive API errors, polling stops automatically - check the error message

### Configuration Issues

- Use "Reset Everything" to clear all settings and start fresh
- Ensure `config.json` is in the same directory as the executable
- Check file permissions - the app needs write access to create/update `config.json`
- If settings don't persist, check Windows Event Viewer for file access errors

### Polling Stops Automatically

The application stops polling after 3 consecutive authentication errors to prevent infinite loops. If this happens:

1. Check the error message in the settings window
2. Verify your API token and Team ID are correct
3. Click "Save Configuration" again to restart polling

## API Information

### Endpoint

```
GET /api/v2/team/{team_id}/time_entries/current
```

### Base URL

```
https://api.clickup.com
```

### Authentication

ClickUp API uses personal API tokens sent directly in the Authorization header (no "Bearer" prefix):

```
Authorization: pk_your_personal_token
```

### Rate Limits

ClickUp API allows 100 requests per minute per API token. This application respects this limit by:
- Enforcing a minimum polling interval of 2 seconds (30 requests/minute)
- Default interval of 5 seconds (12 requests/minute)
- Maximum interval of 60 seconds

## Contributing

We welcome contributions! Here's how to get started:

### Development Setup

**Prerequisites:**
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022, Rider, or VS Code with C# extension
- Git

**Setup Steps:**

1. Clone the repository:
   ```bash
   git clone <repository-url>
   cd ClickUpOverlay
   ```

2. Copy the example configuration:
   ```bash
   copy config.json.example config.json
   ```

3. Edit `config.json` and add your test ClickUp API credentials:
   ```json
   {
     "ApiToken": "pk_your_test_token",
     "TeamId": "your_team_id",
     "PollIntervalSeconds": 5,
     "BorderColor": "#FF0000",
     "OverlayPosition": "TopRight"
   }
   ```

4. Restore dependencies and build:
   ```bash
   dotnet restore
   dotnet build
   ```

5. Run the application:
   ```bash
   dotnet run
   ```

### Project Structure

```
ClickUpOverlay/
├── App.xaml / App.xaml.cs          # Application entry point, system tray, service initialization
├── MainWindow.xaml / MainWindow.xaml.cs  # Settings UI
├── HelpWindow.xaml / HelpWindow.xaml.cs  # API setup guide window
├── LogWindow.xaml / LogWindow.xaml.cs    # Debug logging window
├── Services/
│   ├── ConfigurationService.cs     # Configuration persistence (JSON file)
│   ├── TimerPollingService.cs      # ClickUp API polling and timer state management
│   └── OverlayWindowManager.cs     # Overlay window creation and management
├── Win32/
│   └── Win32Interop.cs             # Win32 API interop (always-on-top, click-through)
├── ClickUpOverlay.csproj           # Project file
├── AssemblyInfo.cs                 # Assembly metadata
├── config.json.example             # Configuration template (safe to commit)
└── README.md                       # This file
```

### Building

**Debug Build:**
```bash
dotnet build
```

Output location: `bin/Debug/net8.0-windows/ClickUpOverlay.exe`

**Release Build:**
```bash
dotnet build -c Release
```

Output location: `bin/Release/net8.0-windows/ClickUpOverlay.exe`

**Running:**
```bash
dotnet run
```

Or run the compiled executable directly.

### Development Workflow

1. **Create a feature branch:**
   ```bash
   git checkout -b feature/your-feature-name
   ```

2. **Make your changes:**
   - Follow C# coding conventions
   - Use nullable reference types appropriately
   - Add comments for complex logic
   - Test your changes thoroughly

3. **Test locally:**
   - Build and run the application
   - Test the specific feature you're working on
   - Use the Log window to verify API interactions
   - Test on multiple monitors if possible

4. **Commit your changes:**
   ```bash
   git add .
   git commit -m "Description of your changes"
   ```

   Use descriptive commit messages that explain what and why.

5. **Push and create a pull request:**
   ```bash
   git push origin feature/your-feature-name
   ```

   Then create a pull request on the repository.

### Code Style

- Follow standard C# conventions
- Use nullable reference types (`string?` for nullable strings)
- Use `async/await` for asynchronous operations
- Keep methods focused and single-purpose
- Add XML documentation comments for public APIs
- Use meaningful variable and method names

### Testing

Currently, testing is manual. When making changes:

1. **Test Overlay Functionality:**
   - Use "Test Overlay" button
   - Verify overlay appears on all monitors
   - Check that overlay position works correctly
   - Verify task name and time display

2. **Test API Integration:**
   - Start a timer in ClickUp
   - Verify overlay appears
   - Check elapsed time updates correctly
   - Stop timer and verify overlay disappears

3. **Test Configuration:**
   - Save settings and verify they persist
   - Test connection validation
   - Verify error handling for invalid credentials
   - Test "Reset Everything" functionality

4. **Test System Tray:**
   - Verify icon appears
   - Test context menu options
   - Test pause/resume functionality

### Areas for Contribution

- Additional overlay styles/themes
- Sound notifications when timer starts/stops
- Statistics tracking (time logged per task)
- Multiple ClickUp account support
- Auto-start with Windows (registry integration)
- Unit tests
- Automated integration tests
- Improved error messages
- Localization support

## License

[Add your license here - e.g., MIT, Apache 2.0, etc.]

## Support

For issues, questions, or feature requests, please open an issue on the repository.

---

**Note**: This application is not affiliated with ClickUp. It's an independent tool that uses the ClickUp API.

