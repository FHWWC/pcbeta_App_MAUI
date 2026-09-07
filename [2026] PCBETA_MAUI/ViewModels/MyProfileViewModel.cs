using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Diagnostics;
using PCBetaMAUI.Models;
using PCBetaMAUI.Services;

namespace PCBetaMAUI.ViewModels;

/// <summary>
/// ViewModel for user profile page
/// </summary>
public partial class MyProfileViewModel : ObservableObject
{
    private readonly ApiService _apiService;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private UserProfileInfo? profileInfo;

    [ObservableProperty]
    private bool isLoading = false;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private bool hasMedals = false;

    public MyProfileViewModel()
    {
        _apiService = new ApiService();
        _navigationService = new NavigationService();
    }

    /// <summary>
    /// Load user profile from API
    /// </summary>
    [RelayCommand]
    public async Task LoadProfileAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            var profile = await _apiService.GetUserProfileAsync();

            if (profile != null)
            {
                //处理SVG头像
                await CheckUserAvatar(profile);
                await LoadProfileImagesAsync(profile);
                ProfileInfo = profile;

                HasMedals = profile.Medals?.Count > 0;
            }
            else
            {
                ErrorMessage = "Failed to load profile information";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Load profile error: {ex.Message}");
            ErrorMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static async Task LoadProfileImagesAsync(UserProfileInfo profile)
    {
        profile.AvatarSource = await ImageDataLoader.LoadAsync(profile.AvatarUrl);

        if (profile.Medals == null)
            return;

        await Task.WhenAll(profile.Medals.Select(async medal =>
        {
            medal.ImageSource = await ImageDataLoader.LoadAsync(medal.ImageUrl);
        }));
    }

    /// <summary>
    /// Back to previous page
    /// </summary>
    [RelayCommand]
    public async Task GoBackAsync()
    {
        await _navigationService.GoBackAsync();
    }

    /// <summary>
    /// Refresh profile data
    /// </summary>
    [RelayCommand]
    public async Task RefreshAsync()
    {
        await LoadProfileAsync();
    }

    public async Task CheckUserAvatar(UserProfileInfo userProfileInfo)
    {
        if (userProfileInfo.AvatarUrl.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
        {
            userProfileInfo.AvatarUrl = "defalut_avatar_big.png";
        }
        else
        {
            if (await _apiService.IsUserAvatarSVGAsync(userProfileInfo.AvatarUrl))
            {
                userProfileInfo.AvatarUrl = "defalut_avatar_big.png";
            }
        }
    }
}
