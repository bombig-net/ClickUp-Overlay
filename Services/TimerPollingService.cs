using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace ClickUpOverlay.Services;

public class TimerStateChangedEventArgs : EventArgs
{
    public bool IsRunning { get; set; }
    public string? TaskId { get; set; }
    public string? TaskName { get; set; }
    public DateTime? StartTime { get; set; }
}

public class TimerPollingService : IDisposable
{
    private readonly HttpClient _httpClient;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _pollingTask;
    private bool _isRunning;
    private bool _lastKnownState;
    private readonly object _lock = new();

    public event EventHandler<TimerStateChangedEventArgs>? TimerStateChanged;
    public event EventHandler<string>? ErrorOccurred;
    public event EventHandler<string>? LogMessage;

    public bool IsPolling
    {
        get
        {
            lock (_lock)
            {
                return _isRunning;
            }
        }
    }

    private int _consecutiveErrors = 0;
    private const int MAX_CONSECUTIVE_ERRORS = 3;

    public TimerPollingService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<(bool Success, string? ErrorMessage)> TestConnectionWithDetails(string apiToken, string teamId)
    {
        try
        {
            using var testClient = new HttpClient();
            testClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            // ClickUp API uses the token directly, not as Bearer token
            testClient.DefaultRequestHeaders.Add("Authorization", apiToken);
            
            var url = $"https://api.clickup.com/api/v2/team/{teamId}/time_entries/current";
            var response = await testClient.GetAsync(url);

            // Check for authentication errors
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || 
                response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                return (false, "Invalid API token. Please check that your token is correct and not expired.");
            }

            // Check for other client errors (4xx) - likely invalid team ID or endpoint
            if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return (false, $"Team ID '{teamId}' not found. Please verify your Team ID is correct.");
                }
                return (false, $"API returned error: {response.StatusCode}. Please check your Team ID.");
            }

            // If we get a successful response (200 OK) or server error (5xx), 
            // the connection and authentication are working
            // Server errors are acceptable for testing - they mean the API is reachable
            if (response.IsSuccessStatusCode || (int)response.StatusCode >= 500)
            {
                return (true, null);
            }

            // For any other status, consider it a failure
            return (false, $"Unexpected response: {response.StatusCode}");
        }
        catch (HttpRequestException ex)
        {
            // Network errors - connection failed
            return (false, $"Network error: {ex.Message}. Please check your internet connection.");
        }
        catch (Exception ex)
        {
            // Any other exception - connection failed
            return (false, $"Error: {ex.Message}");
        }
    }

    public async Task<bool> TestConnection(string apiToken, string teamId)
    {
        try
        {
            using var testClient = new HttpClient();
            testClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            // ClickUp API uses the token directly, not as Bearer token
            testClient.DefaultRequestHeaders.Add("Authorization", apiToken);
            
            var url = $"https://api.clickup.com/api/v2/team/{teamId}/time_entries/current";
            var response = await testClient.GetAsync(url);

            // Check for authentication errors
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || 
                response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                return false;
            }

            // Check for other client errors (4xx) - likely invalid team ID or endpoint
            if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500)
            {
                return false;
            }

            // If we get a successful response (200 OK) or server error (5xx), 
            // the connection and authentication are working
            // Server errors are acceptable for testing - they mean the API is reachable
            if (response.IsSuccessStatusCode || (int)response.StatusCode >= 500)
            {
                return true;
            }

            // For any other status, consider it a failure
            return false;
        }
        catch (HttpRequestException)
        {
            // Network errors - connection failed
            return false;
        }
        catch (Exception)
        {
            // Any other exception - connection failed
            return false;
        }
    }

    public void StartPolling(string apiToken, string teamId, int pollIntervalSeconds)
    {
        lock (_lock)
        {
            if (_isRunning)
            {
                StopPolling();
            }

            _consecutiveErrors = 0;
            // ClickUp API uses the token directly, not as Bearer token
            _httpClient.DefaultRequestHeaders.Remove("Authorization");
            _httpClient.DefaultRequestHeaders.Add("Authorization", apiToken);
            _cancellationTokenSource = new CancellationTokenSource();
            _isRunning = true;
            _pollingTask = Task.Run(() => PollLoop(teamId, pollIntervalSeconds, _cancellationTokenSource.Token));
        }
    }

    public void StopPolling()
    {
        lock (_lock)
        {
            if (!_isRunning)
                return;

            _cancellationTokenSource?.Cancel();
            _pollingTask?.Wait(TimeSpan.FromSeconds(5));
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            _isRunning = false;
        }
    }

    private async Task PollLoop(string teamId, int pollIntervalSeconds, CancellationToken cancellationToken)
    {
        // Ensure minimum interval of 2 seconds to respect rate limits (100 req/min = 1.67s minimum)
        var interval = Math.Max(pollIntervalSeconds, 2) * 1000;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var state = await CheckTimerState(teamId);
                
                // Reset error counter on success
                lock (_lock)
                {
                    _consecutiveErrors = 0;
                    if (state != _lastKnownState)
                    {
                        _lastKnownState = state;
                        OnTimerStateChanged(state, null, null, null);
                    }
                }
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("401") || ex.Message.Contains("403"))
            {
                lock (_lock)
                {
                    _consecutiveErrors++;
                    if (_consecutiveErrors >= MAX_CONSECUTIVE_ERRORS)
                    {
                        OnErrorOccurred("Invalid API token. Polling stopped. Please check your configuration.");
                        StopPolling();
                        break;
                    }
                    else if (_consecutiveErrors == 1)
                    {
                        OnErrorOccurred("Invalid API token. Please check your configuration.");
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but continue with last known state
                System.Diagnostics.Debug.WriteLine($"Polling error: {ex.Message}");
            }

            try
            {
                await Task.Delay(interval, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private async Task<bool> CheckTimerState(string teamId)
    {
        try
        {
            var url = $"https://api.clickup.com/api/v2/team/{teamId}/time_entries/current";
            var response = await _httpClient.GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || 
                response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                OnErrorOccurred("Invalid API token. Please check your configuration.");
                return _lastKnownState; // Return last known state
            }

            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            
            OnLogMessage($"API Response: {json}");
            
            // Parse JSON to check if timer is running
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            // Check if there's a time entry (if data exists and is not null/empty)
            if (root.TryGetProperty("data", out var dataElement))
            {
                if (dataElement.ValueKind == JsonValueKind.Object && dataElement.GetRawText() != "null")
                {
                    // Extract task info if available
                    string? taskId = null;
                    string? taskName = null;
                    DateTime? startTime = null;
                    
                    if (dataElement.TryGetProperty("task", out var taskElement))
                    {
                        if (taskElement.TryGetProperty("id", out var idElement))
                            taskId = idElement.GetString();
                        if (taskElement.TryGetProperty("name", out var nameElement))
                            taskName = nameElement.GetString();
                    }
                    
                    // Extract duration first - negative duration means timer is running
                    long? durationMs = null;
                    
                    if (dataElement.TryGetProperty("duration", out var durationElement))
                    {
                        if (durationElement.ValueKind == JsonValueKind.Number)
                        {
                            durationMs = durationElement.GetInt64();
                            OnLogMessage($"Duration (number): {durationMs}ms");
                        }
                        else if (durationElement.ValueKind == JsonValueKind.String)
                        {
                            if (long.TryParse(durationElement.GetString(), out var dur))
                            {
                                durationMs = dur;
                                OnLogMessage($"Duration (string): {durationMs}ms");
                            }
                        }
                    }
                    
                    // If duration is negative, timer is running - calculate start time from duration
                    if (durationMs.HasValue && durationMs.Value < 0)
                    {
                        // Negative duration means timer is running
                        // Calculate actual start: current time - absolute duration
                        var actualDuration = Math.Abs(durationMs.Value);
                        startTime = DateTime.Now.AddMilliseconds(-actualDuration);
                        OnLogMessage($"Using duration to calculate start time: {startTime} (elapsed: {actualDuration}ms = {TimeSpan.FromMilliseconds(actualDuration)})");
                    }
                    else if (dataElement.TryGetProperty("start", out var startElement))
                    {
                        OnLogMessage($"Found 'start' property: {startElement.ValueKind}, Raw: {startElement.GetRawText()}");
                        
                        long timestamp = 0;
                        if (startElement.ValueKind == JsonValueKind.Number)
                        {
                            timestamp = startElement.GetInt64();
                        }
                        else if (startElement.ValueKind == JsonValueKind.String)
                        {
                            if (!long.TryParse(startElement.GetString(), out timestamp))
                            {
                                OnLogMessage("Failed to parse start timestamp as number");
                            }
                        }
                        
                        if (timestamp > 0)
                        {
                            OnLogMessage($"Start timestamp: {timestamp}");
                            
                            // The timestamp might be in microseconds - check if it's too large
                            if (timestamp > 1000000000000000) // Microseconds (very large)
                            {
                                // Convert microseconds to milliseconds
                                timestamp = timestamp / 1000;
                                OnLogMessage($"Converted from microseconds to milliseconds: {timestamp}");
                            }
                            
                            // Now parse as milliseconds
                            if (timestamp > 1000000000000) // Millisecond timestamp
                            {
                                var parsedTime = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).DateTime;
                                OnLogMessage($"Parsed as milliseconds: {parsedTime}");
                                
                                // Check if the parsed time is in the future (more than 1 hour)
                                // If so, it's likely wrong and we should use duration instead
                                if (parsedTime > DateTime.Now.AddHours(1))
                                {
                                    OnLogMessage($"Parsed time is in the future, ignoring and using duration instead");
                                }
                                else
                                {
                                    startTime = parsedTime;
                                }
                            }
                            else if (timestamp > 1000000000) // Second timestamp
                            {
                                startTime = DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime;
                                OnLogMessage($"Parsed as seconds: {startTime}");
                            }
                        }
                    }
                    else
                    {
                        OnLogMessage("No 'start' property found in data");
                    }
                    
                    // If no start time found, use current time (timer just started)
                    if (!startTime.HasValue)
                    {
                        startTime = DateTime.Now;
                        OnLogMessage($"No start time found, using current time: {startTime}");
                    }
                    
                    var elapsed = DateTime.Now - startTime.Value;
                    OnLogMessage($"Final values - Task: {taskName ?? "N/A"}, StartTime: {startTime}, Elapsed: {elapsed}");

                    lock (_lock)
                    {
                        if (!_lastKnownState || taskId != null)
                        {
                            OnTimerStateChanged(true, taskId, taskName, startTime);
                        }
                    }
                    return true;
                }
            }

            // No active timer
            lock (_lock)
            {
                if (_lastKnownState)
                {
                    OnTimerStateChanged(false, null, null, null);
                }
            }
            return false;
        }
        catch (HttpRequestException)
        {
            // Network error - return last known state
            return _lastKnownState;
        }
    }

    private void OnTimerStateChanged(bool isRunning, string? taskId, string? taskName, DateTime? startTime = null)
    {
        TimerStateChanged?.Invoke(this, new TimerStateChangedEventArgs
        {
            IsRunning = isRunning,
            TaskId = taskId,
            TaskName = taskName,
            StartTime = startTime
        });
    }

    private void OnErrorOccurred(string message)
    {
        ErrorOccurred?.Invoke(this, message);
    }

    private void OnLogMessage(string message)
    {
        LogMessage?.Invoke(this, message);
    }

    public void Dispose()
    {
        StopPolling();
        _httpClient?.Dispose();
    }
}

