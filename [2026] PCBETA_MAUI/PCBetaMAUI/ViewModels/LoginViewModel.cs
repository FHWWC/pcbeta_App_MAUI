using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using PCBetaMAUI.Services;

namespace PCBetaMAUI.ViewModels;

/// <summary>
/// ViewModel for login page handling authentication, password persistence, and auto-login
/// </summary>
public partial class LoginViewModel : ObservableObject
{
    private readonly ApiService _apiService;
    private readonly PasswordSecurityService _passwordService;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private string username = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private bool rememberPassword = false;

    [ObservableProperty]
    private string selectedQuestionId = "0";

    [ObservableProperty]
    private int questionPickerIndex = 0;

    [ObservableProperty]
    private string securityAnswer = string.Empty;

    [ObservableProperty]
    private bool isLoading = false;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private bool isLoginEnabled = true;

    public LoginViewModel()
    {
        _apiService = new ApiService();
        _passwordService = new PasswordSecurityService();
        _navigationService = new NavigationService();
    }

    /// <summary>
    /// Initializes the view model - checks for saved credentials and attempts auto-login
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            // Get the last used username
            var lastUsername = await _passwordService.GetLastUsernameAsync();
            if (!string.IsNullOrEmpty(lastUsername))
            {
                Username = lastUsername;

                // Check if password is saved
                var savedPassword = await _passwordService.GetPasswordAsync(lastUsername);
                if (!string.IsNullOrEmpty(savedPassword))
                {
                    Password = savedPassword;
                    RememberPassword = true;

                    // Load saved security question and answer if available
                    var questionId = await _passwordService.GetSecurityQuestionIdAsync(lastUsername);
                    var answer = await _passwordService.GetSecurityAnswerAsync(lastUsername);
                    if (!string.IsNullOrEmpty(questionId))
                    {
                        SelectedQuestionId = questionId;
                        // Set the picker index based on question ID (0-7)
                        if (int.TryParse(questionId, out int qId))
                        {
                            QuestionPickerIndex = qId;
                        }
                    }
                    if (!string.IsNullOrEmpty(answer))
                    {
                        SecurityAnswer = answer;
                    }

                    // Automatically attempt login
                    await LoginCommand.ExecuteAsync(null);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Initialize error: {ex.Message}");
            ErrorMessage = "Failed to load saved credentials";
        }
    }

    /// <summary>
    /// Executes login with username and password
    /// </summary>
    [RelayCommand]
    public async Task Login()
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Please enter username and password";
            return;
        }

        IsLoading = true;
        IsLoginEnabled = false;
        ErrorMessage = string.Empty;

        try
        {
            // Prepare question and answer for login - only pass if a question is selected
            string? questionId = null;
            string? answer = null;
            if (QuestionPickerIndex > 0 && !string.IsNullOrWhiteSpace(SecurityAnswer))
            {
                questionId = QuestionPickerIndex.ToString();
                answer = SecurityAnswer;
            }

            // Attempt login via API - now returns tuple with error message
            var (loginSuccess, errorMessage) = await _apiService.LoginAsync(Username, Password, questionId, answer);

            if (loginSuccess)
            {
                if (Shell.Current is AppShell appShell)
                {
                    appShell.UserName.Text = Username;
                    appShell.SetLoginStatus(true);

                    // Get user avatar URL
                    var avatarUrl = await _apiService.GetUserAvatarAsync();
                    if (!string.IsNullOrEmpty(avatarUrl))
                    {
                        if(avatarUrl.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                        {
                            appShell.UserAvatar.Source = "defalut_avatar_big.png";
                        }
                        else
                        {
                            if(await _apiService.IsUserAvatarSVGAsync(avatarUrl))
                            {
                                appShell.UserAvatar.Source = "defalut_avatar_big.png";
                            }
                            else
                            {
                                appShell.UserAvatar.Source = avatarUrl;
                            }
                        }
                    }
                }

                // Handle password persistence
                if (RememberPassword)
                {
                    // Save encrypted password
                    await _passwordService.SavePasswordAsync(Username, Password);

                    //  改进：正确处理安全问答保存/清除逻辑
                    if (QuestionPickerIndex > 0 && !string.IsNullOrWhiteSpace(SecurityAnswer))
                    {
                        // 用户勾选了安全问答且有输入 → 保存
                        await _passwordService.SaveSecurityAnswerAsync(Username, QuestionPickerIndex.ToString(), SecurityAnswer);
                        Debug.WriteLine($" 保存安全问答：用户={Username}, 问答ID={QuestionPickerIndex}");
                    }
                    else
                    {
                        // 用户没有勾选或没有输入安全问答 → 清除之前可能保存的问答
                        await _passwordService.ClearSecurityAnswerAsync(Username);
                        Debug.WriteLine($" 清除安全问答：用户={Username}（记住密码时未选择问答）");
                    }
                }
                else
                {
                    // 用户未勾选"记住密码" → 清除所有保存的凭据
                    await _passwordService.ClearPasswordAsync(Username);
                    await _passwordService.ClearSecurityAnswerAsync(Username);
                    Debug.WriteLine($" 清除所有凭据：用户={Username}（未勾选记住密码）");
                }

                // Navigate to main page
                await _navigationService.NavigateToAsyncClearStack("mainthread");
            }
            else
            {
                //  改进：显示具体的错误信息
                ErrorMessage = errorMessage ?? "Login failed. Please check your username and password.";

                //  新增：弹窗提示错误信息
                await Application.Current!.MainPage!.DisplayAlert("Login Failed", ErrorMessage, "OK");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Login error: {ex.Message}");
            ErrorMessage = $"Login error: {ex.Message}";
            await Application.Current!.MainPage!.DisplayAlert("Login Error", ErrorMessage, "OK");
        }
        finally
        {
            IsLoading = false;
            IsLoginEnabled = true;
        }
    }

    /// <summary>
    /// Handles the toggle of "Remember Password" checkbox
    /// </summary>
    [RelayCommand]
    public async Task OnRememberPasswordChangedAsync()
    {
        try
        {
            if (!RememberPassword)
            {
                // User unchecked "remember password" - clear saved credentials
                if (!string.IsNullOrEmpty(Username))
                {
                    await _passwordService.ClearPasswordAsync(Username);
                    await _passwordService.ClearSecurityAnswerAsync(Username);
                    Password = string.Empty;
                    QuestionPickerIndex = 0;
                    SelectedQuestionId = "0";
                    SecurityAnswer = string.Empty;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Remember password toggle error: {ex.Message}");
        }
    }

    /// <summary>
    /// Navigates to guest (non-login) access
    /// </summary>
    [RelayCommand]
    public async Task GuestAccessAsync()
    {
        try
        {
            // Navigate to main page without login
            await _navigationService.NavigateToAsyncClearStack("mainthread");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Guest access error: {ex.Message}");
            ErrorMessage = "Failed to access as guest";
        }
    }

    /// <summary>
    /// Clears error message
    /// </summary>
    [RelayCommand]
    public void ClearError()
    {
        ErrorMessage = string.Empty;
    }
}
