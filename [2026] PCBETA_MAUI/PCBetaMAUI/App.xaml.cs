using Microsoft.Extensions.DependencyInjection;
using PCBetaMAUI.Services;
using PCBetaMAUI.ViewModels;
using System.Diagnostics;

namespace PCBetaMAUI
{
    public partial class App : Application
    {
        private readonly PasswordSecurityService _passwordService;
        private readonly ApiService _apiService;

        public App()
        {
            InitializeComponent();
            _passwordService = new PasswordSecurityService();
            _apiService = new ApiService();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }

        protected override async void OnStart()
        {
            base.OnStart();

            // Check for saved credentials and attempt auto-login
            await AttemptAutoLoginAsync();
        }

        private async Task AttemptAutoLoginAsync()
        {
            try
            {
                // Get the last used username
                var username = await _passwordService.GetLastUsernameAsync();
                if (string.IsNullOrEmpty(username))
                {
                    // No saved username, navigate to login page
                    Debug.WriteLine("No saved username found");
                    return;
                }

                // Check if password is saved
                var password = await _passwordService.GetPasswordAsync(username);
                if (string.IsNullOrEmpty(password))
                {
                    // No saved password, navigate to login page
                    Debug.WriteLine("No saved password found");
                    return;
                }

                // Get saved security question and answer if available
                var questionId = await _passwordService.GetSecurityQuestionIdAsync(username);
                var answer = await _passwordService.GetSecurityAnswerAsync(username);

                Debug.WriteLine($"Auto-login attempt for user: {username}");

                // Attempt login with saved credentials - now returns tuple with error message
                var (loginSuccess, errorMessage) = await _apiService.LoginAsync(username, password, questionId, answer);

                if (loginSuccess)
                {
                    Debug.WriteLine($"Auto-login successful for user: {username}");

                    // Update UI to show login status
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (Shell.Current is AppShell appShell)
                        {
                            appShell.SetLoginStatus(true);
                            appShell.UserName.Text = username;
                            Debug.WriteLine($"Updated AppShell username: {username}");
                        }
                    });

                    // Try to get user avatar
                    var avatarUrl = await _apiService.GetUserAvatarAsync();
                    if (!string.IsNullOrEmpty(avatarUrl))
                    {
                        MainThread.BeginInvokeOnMainThread(async () =>
                        {
                            if (Shell.Current is AppShell appShell)
                            {
                                if (avatarUrl.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                                {
                                    appShell.UserAvatar.Source = "defalut_avatar_big.png";
                                }
                                else
                                {
                                    if (await _apiService.IsUserAvatarSVGAsync(avatarUrl))
                                    {
                                        appShell.UserAvatar.Source = "defalut_avatar_big.png";
                                    }
                                    else
                                    {
                                        appShell.UserAvatar.Source = avatarUrl;
                                    }
                                }
                                Debug.WriteLine($"Updated AppShell avatar: {avatarUrl}");
                            }
                        });
                    }

                    // Get user info from main page (username, logout URL, etc.)
                    try
                    {
                        var (serverUsername, logoutUrl, formHash) = await _apiService.GetUserInfoFromMainPageAsync();

                        if (!string.IsNullOrEmpty(serverUsername))
                        {
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                if (Shell.Current is AppShell appShell)
                                {
                                    appShell.UserName.Text = serverUsername;
                                    Debug.WriteLine($"Updated username from server: {serverUsername}");
                                }
                            });
                        }

                        if (!string.IsNullOrEmpty(logoutUrl))
                        {
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                if (Shell.Current is AppShell appShell)
                                {
                                    appShell.LogoutUrl = logoutUrl;
                                    Debug.WriteLine($"Updated logout URL: {logoutUrl}");
                                }
                            });
                        }

                        // Store user info in global service
                        if (!string.IsNullOrEmpty(serverUsername) || !string.IsNullOrEmpty(logoutUrl))
                        {
                            UserCredentialsService.Instance.SetUserInfo(serverUsername ?? username, logoutUrl, formHash);
                            Debug.WriteLine("Stored user info in UserCredentialsService");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Get user info error: {ex.Message}");
                    }

                    // Navigate to main thread page
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        try
                        {
                            await Shell.Current.GoToAsync("//mainthread", animate: false);
                            await MainThreadViewModel.mainThreadViewModel.InitializeAsync();
                            Debug.WriteLine("Successfully navigated to mainthread");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Navigation error: {ex.Message}");
                        }
                    });
                }
                else
                {
                    Debug.WriteLine($"Auto-login failed for user: {username}. Error: {errorMessage}");
                    // Auto-login failed, will let user manually login
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Auto-login error: {ex.Message}");
                // If auto-login fails, continue to login page
            }
        }
    }
}