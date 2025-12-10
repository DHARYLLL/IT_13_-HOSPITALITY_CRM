using Microsoft.Maui.Networking;
using Hospitality.Database;

namespace Hospitality.Services;

/// <summary>
/// Monitors network connectivity and database availability
/// </summary>
public class ConnectivityService : IDisposable
{
    private bool _isOnline = false;
    private bool _canReachOnlineDb = false;
    private DateTime _lastOnlineCheck = DateTime.MinValue;
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(10);
    private Timer? _pollingTimer;
    private bool _isDisposed = false;

    /// <summary>
    /// Event fired when connectivity status changes
    /// </summary>
    public event Action<bool>? ConnectivityChanged;

    /// <summary>
    /// Event fired when online database becomes reachable (use Action for simpler invocation)
    /// </summary>
    public event Action? OnlineDbAvailable;

    public ConnectivityService()
    {
        // Initial state
        _isOnline = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

        // Subscribe to connectivity changes
        Connectivity.ConnectivityChanged += OnConnectivityChanged;

     // Start background polling as fallback (every 15 seconds)
        _pollingTimer = new Timer(async _ => await PollConnectivityAsync(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15));

        Console.WriteLine($"?? ConnectivityService initialized. Network: {(_isOnline ? "Online" : "Offline")}");
    }

  /// <summary>
    /// Returns true if the device has internet access
    /// </summary>
    public bool IsNetworkAvailable => Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

    /// <summary>
    /// Returns true if we can reach the online database
    /// </summary>
    public bool CanReachOnlineDatabase => _canReachOnlineDb;

    /// <summary>
    /// Returns the current connection mode
    /// </summary>
    public string CurrentMode => _canReachOnlineDb ? "Online" : "Offline";

  /// <summary>
    /// Background polling to detect connectivity changes
    /// </summary>
    private async Task PollConnectivityAsync()
    {
        if (_isDisposed) return;

   bool wasReachable = _canReachOnlineDb;
        bool isNetworkUp = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

        if (isNetworkUp && !wasReachable)
   {
       // Network is up but we weren't connected to DB - check now
   Console.WriteLine("?? Polling: Network detected, checking database...");
   _lastOnlineCheck = DateTime.MinValue; // Force recheck
       await CheckOnlineDatabaseAsync();

            if (_canReachOnlineDb && !wasReachable)
            {
   Console.WriteLine("?? Polling detected connection restored - triggering sync...");
        TriggerOnlineDbAvailable();
            }
        }
        else if (!isNetworkUp && wasReachable)
        {
     _canReachOnlineDb = false;
       Console.WriteLine("?? Polling: Network lost");
  ConnectivityChanged?.Invoke(false);
   }
    }

    private async void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
 bool wasOnline = _isOnline;
        _isOnline = e.NetworkAccess == NetworkAccess.Internet;

        Console.WriteLine($"?? Connectivity changed: {(wasOnline ? "Online" : "Offline")} ? {(_isOnline ? "Online" : "Offline")}");

        if (_isOnline && !wasOnline)
        {
            // Just came online - check if we can reach the database
 Console.WriteLine("?? Network restored, checking database connection...");
       _lastOnlineCheck = DateTime.MinValue; // Force recheck
      bool wasReachable = _canReachOnlineDb;
    await CheckOnlineDatabaseAsync();

       if (_canReachOnlineDb)
     {
            Console.WriteLine("? Database is reachable!");
    if (!wasReachable)
   {
           Console.WriteLine("?? Connection restored - triggering sync...");
        TriggerOnlineDbAvailable();
                }
   }
        else
 {
         Console.WriteLine("? Database not reachable despite network being up");
     }
        }
        else if (!_isOnline)
        {
  _canReachOnlineDb = false;
            Console.WriteLine("?? Network lost, switching to offline mode");
        }

        ConnectivityChanged?.Invoke(_canReachOnlineDb);
    }

    /// <summary>
    /// Safely trigger the OnlineDbAvailable event
    /// </summary>
    private void TriggerOnlineDbAvailable()
    {
        try
   {
         Console.WriteLine("?? Triggering OnlineDbAvailable event...");
         int subscriberCount = OnlineDbAvailable?.GetInvocationList()?.Length ?? 0;
            Console.WriteLine($"?? Event has {subscriberCount} subscriber(s)");
            OnlineDbAvailable?.Invoke();
      Console.WriteLine("? OnlineDbAvailable event triggered successfully");
     }
        catch (Exception ex)
   {
            Console.WriteLine($"? Error in OnlineDbAvailable handler: {ex.Message}");
  }
    }

    /// <summary>
    /// Check if we can reach the online database
    /// </summary>
    public async Task<bool> CheckOnlineDatabaseAsync()
    {
        // Don't check too frequently
     if (DateTime.Now - _lastOnlineCheck < _checkInterval && _lastOnlineCheck != DateTime.MinValue)
        {
        return _canReachOnlineDb;
        }

        _lastOnlineCheck = DateTime.Now;

        if (!IsNetworkAvailable)
 {
            _canReachOnlineDb = false;
            return false;
        }

    try
        {
  _canReachOnlineDb = await DbConnection.CanConnectToOnlineAsync();
          Console.WriteLine($"?? Online database check: {(_canReachOnlineDb ? "Reachable ?" : "Unreachable ?")}")
;
            return _canReachOnlineDb;
        }
        catch (Exception ex)
        {
     Console.WriteLine($"? Error checking online database: {ex.Message}");
            _canReachOnlineDb = false;
    return false;
   }
    }

    /// <summary>
  /// Force a connectivity check and trigger sync if online
    /// </summary>
    public async Task<bool> RefreshAndSyncAsync()
  {
        bool wasReachable = _canReachOnlineDb;
     _lastOnlineCheck = DateTime.MinValue; // Force recheck
      bool isReachable = await CheckOnlineDatabaseAsync();

        if (isReachable && !wasReachable)
        {
          TriggerOnlineDbAvailable();
        }

      return isReachable;
    }

  /// <summary>
    /// Force a connectivity check and return current status
    /// </summary>
    public async Task<bool> RefreshStatusAsync()
    {
  _lastOnlineCheck = DateTime.MinValue; // Force recheck
        return await CheckOnlineDatabaseAsync();
    }

    public void Dispose()
    {
        _isDisposed = true;
        _pollingTimer?.Dispose();
        Connectivity.ConnectivityChanged -= OnConnectivityChanged;
    }
}
