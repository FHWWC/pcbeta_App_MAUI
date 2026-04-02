using System.Diagnostics;

namespace PCBetaMAUI.Services;

/// <summary>
/// Service for storing and managing global user credentials and session information
/// </summary>
public class UserCredentialsService
{
    private static UserCredentialsService? _instance;
    private static readonly object _lockObject = new object();

    public string? Username { get; set; }
    public string? LogoutUrl { get; set; }
    public string? FormHash { get; set; }

    private UserCredentialsService()
    {
    }

    /// <summary>
    /// Gets the singleton instance of UserCredentialsService
    /// </summary>
    public static UserCredentialsService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lockObject)
                {
                    if (_instance == null)
                    {
                        _instance = new UserCredentialsService();
                    }
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// Clears all stored credentials
    /// </summary>
    public void Clear()
    {
        Username = null;
        LogoutUrl = null;
        FormHash = null;
        Debug.WriteLine("User credentials cleared");
    }

    /// <summary>
    /// Sets user information
    /// </summary>
    public void SetUserInfo(string? username, string? logoutUrl, string? formHash = null)
    {
        Username = username;
        LogoutUrl = logoutUrl;
        FormHash = formHash;
        Debug.WriteLine($"User credentials updated - Username: {username}");
    }
}
