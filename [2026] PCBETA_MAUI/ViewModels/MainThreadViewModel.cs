using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using PCBetaMAUI.Models;
using PCBetaMAUI.Services;

namespace PCBetaMAUI.ViewModels;

/// <summary>
/// ViewModel for the main forum page displaying all forum categories and sections
/// </summary>
public partial class MainThreadViewModel : ObservableObject
{
    private readonly ApiService _apiService;
    private readonly INavigationService _navigationService;
    public static MainThreadViewModel mainThreadViewModel;
    public string currentformHash = "";

    [ObservableProperty]
    private ObservableCollection<ForumCategory> forumCategories = new();

    [ObservableProperty]
    private bool isLoading = false;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private bool hasForums = false;

    public MainThreadViewModel()
    {
        _apiService = new ApiService();
        _navigationService = new NavigationService();

        mainThreadViewModel = this;
        //InitializeAsync();
    }

    /// <summary>
    /// Loads the main page forum categories and sections
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            await LoadForumsAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Initialize error: {ex.Message}");
        }
    }

    /// <summary>
    /// Fetches and displays all forum categories with their sections
    /// </summary>
    [RelayCommand]
    public async Task LoadForumsAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            var categories = await _apiService.GetMainPageForumsAsync();

            var sections = categories
                .SelectMany(category => category.Sections)
                .ToList();
            await Task.WhenAll(sections.Select(LoadForumLogoAsync));

            ForumCategories.Clear();
            foreach (var category in categories)
            {
                ForumCategories.Add(category);
            }

            HasForums = ForumCategories.Count > 0;

            if (!HasForums)
            {
                ErrorMessage = "No forums available";
            }

            // Check login status based on content
            UpdateLoginStatus();


            // Get user information from main page
            await UpdateUserInfoAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Load forums error: {ex.Message}");
            ErrorMessage = $"Failed to load forums: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static async Task LoadForumLogoAsync(ForumSection forum)
    {
        var logoUrl = forum.LogoUrl?.Trim();
        if (string.IsNullOrEmpty(logoUrl))
        {
            return;
        }

        if (!Uri.TryCreate(logoUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            forum.LogoSource = ImageSource.FromFile(logoUrl);
            return;
        }

        try
        {
            var imageBytes = await HttpClientManager.Instance.GetByteArrayAsync(logoUrl);
            if (imageBytes.Length > 0)
            {
                forum.LogoSource = ImageSource.FromStream(() =>
                    new MemoryStream(imageBytes, writable: false));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Forum logo loading failed: {logoUrl}, {ex.Message}");
        }
    }

    /// <summary>
    /// Updates user information from the main page
    /// </summary>
    private async Task UpdateUserInfoAsync()
    {
        try
        {
            var (username, logoutUrl, formHash) = await _apiService.GetUserInfoFromMainPageAsync();

            if (!string.IsNullOrEmpty(username) || !string.IsNullOrEmpty(logoutUrl))
            {
                currentformHash = formHash;
                // Store user info in global service
                UserCredentialsService.Instance.SetUserInfo(username, logoutUrl, formHash);

                // Update AppShell with username and logout URL
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (Application.Current?.Windows[0].Page is AppShell appShell)
                    {
                        if (!string.IsNullOrEmpty(username))
                        {
                            appShell.UserName.Text = username;
                            Debug.WriteLine($"Updated username: {username}");
                        }

                        if (!string.IsNullOrEmpty(logoutUrl))
                        {
                            appShell.LogoutUrl = logoutUrl;
                            Debug.WriteLine($"Updated logout URL: {logoutUrl}");
                        }
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Update user info error: {ex.Message}");
        }
    }

    /// <summary>
    /// Updates the login status in AppShell based on the page content
    /// </summary>
    private void UpdateLoginStatus()
    {
        var isLoggedIn = _apiService.IsLoggedInByContent(_apiService.LastPageContent);

        if (Application.Current?.Windows[0].Page is AppShell appShell)
        {
            appShell.SetLoginStatus(isLoggedIn);
        }
    }

    /// <summary>
    /// Navigates to thread list for the selected forum section
    /// </summary>
    [RelayCommand]
    public async Task SelectForumAsync(ForumSection forum)
    {
        if (forum == null || string.IsNullOrEmpty(forum.Id))
        {
            ErrorMessage = "Invalid forum selected";
            return;
        }

        try
        {
            var parameters = new Dictionary<string, object>
            {
                { "boardId", forum.Id },
                { "forumName", forum.Name ?? "Forum" }
            };

            await _navigationService.NavigateToAsync("threadlist", parameters);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Select forum error: {ex.Message}");
            ErrorMessage = "Failed to navigate to forum";
        }
    }

    /// <summary>
    /// Logs out and returns to login page
    /// </summary>
    [RelayCommand]
    public async Task LogoutAsync()
    {
        try
        {
            await _apiService.LogoutAsync();
            await _navigationService.NavigateToAsyncClearStack("login");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Logout error: {ex.Message}");
            ErrorMessage = "Logout failed";
        }
    }

    /// <summary>
    /// Opens the AppShell menu
    /// </summary>
    [RelayCommand]
    public void OpenMenu()
    {
        try
        {
            if (Shell.Current is AppShell appShell)
            {
                appShell.FlyoutIsPresented = !appShell.FlyoutIsPresented;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Open menu error: {ex.Message}");
        }
    }

    /// <summary>
    /// Refreshes the forum list
    /// </summary>
    [RelayCommand]
    public async Task RefreshAsync()
    {
        await LoadForumsAsync();
    }

    /// <summary>
    /// Navigates to search page
    /// </summary>
    [RelayCommand]
    public async Task SearchAsync()
    {
        try
        {
            // 验证登录
            var loggedIn = await _apiService.IsLoggedInAsync();
            if (!loggedIn)
            {
                // 导航到登录页
                await _navigationService.NavigateToAsync("login");
                return;
            }

            var parameters = new Dictionary<string, object>
            {
                { "formHash", currentformHash }
            };
            await _navigationService.NavigateToAsync("search", parameters);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Search navigation error: {ex.Message}");
            ErrorMessage = "Failed to navigate to search";
        }
    }

    /// <summary>
    /// Toggles the expanded state of a forum category
    /// </summary>
    [RelayCommand]
    public void ToggleCategoryExpanded(ForumCategory category)
    {
        if (category != null)
        {
            category.IsExpanded = !category.IsExpanded;
        }
    }

    /// <summary>
    /// Navigates to my profile/user page
    /// </summary>
    [RelayCommand]
    public async Task MyAsync()
    {
        try
        {
            // Check if user is logged in
            //if (Application.Current?.Windows[0].Page is AppShell appShell)
            //{
            //    if (!appShell.IsLoggedIn)
            //    {
            //        await Application.Current.MainPage?.DisplayAlert("提示", "您需要先登录才能查看个人资料", "确定");
            //        return;
            //    }
            //}

            // 验证登录
            var loggedIn = await _apiService.IsLoggedInAsync();
            if (!loggedIn)
            {
                // 导航到登录页
                await _navigationService.NavigateToAsync("login");
                return;
            }

            await _navigationService.NavigateToAsync("myprofile");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"My page navigation error: {ex.Message}");
            ErrorMessage = "Failed to navigate to my page";
        }
    }
}

