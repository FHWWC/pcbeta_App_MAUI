using PCBetaMAUI.Views;
using PCBetaMAUI.Services;
using System.Diagnostics;
using System.ComponentModel;

namespace PCBetaMAUI
{
    public partial class AppShell : Shell
    {
        private bool _isLoggedIn = false;
        private string? _logoutUrl;
        private bool _isMyNoticeVisible = false;

        public bool IsLoggedIn
        {
            get => _isLoggedIn;
            set
            {
                if (_isLoggedIn != value)
                {
                    _isLoggedIn = value;
                    IsMyNoticeVisible = value;  // 同步更新我的通知可见性
                    UpdateLoginUI();
                }
            }
        }

        public bool IsMyNoticeVisible
        {
            get => _isMyNoticeVisible;
            set
            {
                if (_isMyNoticeVisible != value)
                {
                    _isMyNoticeVisible = value;
                    OnPropertyChanged(nameof(IsMyNoticeVisible));
                }
            }
        }

        public string? LogoutUrl
        {
            get => _logoutUrl;
            set => _logoutUrl = value;
        }

        public AppShell()
        {
            InitializeComponent();
            Shell.SetNavBarIsVisible(this, false);

            // ✅ 注册所有路由，包括新增的 mynotice
            Routing.RegisterRoute("login", typeof(LoginPage));
            Routing.RegisterRoute("threadlist", typeof(ThreadListPage));
            Routing.RegisterRoute("threadcontent", typeof(ThreadContentPage));
            Routing.RegisterRoute("replythread", typeof(ReplyThreadPage));
            Routing.RegisterRoute("postnewthread", typeof(PostNewThreadPage));
            Routing.RegisterRoute("editreply", typeof(EditReplyPage));
            Routing.RegisterRoute("editopreply", typeof(EditOPReplyPage));
            Routing.RegisterRoute("mynotice", typeof(MyNoticePage));  // ✅ 新增：注册我的通知页面路由
            Routing.RegisterRoute("search", typeof(SearchPage));
            Routing.RegisterRoute("myprofile", typeof(MyProfilePage));

            // Hook up the logout button click event
            var logoutButton = this.FindByName<Button>("LogoutButton");
            if (logoutButton != null)
            {
                logoutButton.Clicked += OnLogoutButtonClicked;
            }
        }

        private void UpdateLoginUI()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Update button visibility based on login status
                var loginButton = this.FindByName<Button>("LoginButton");
                var logoutButton = this.FindByName<Button>("LogoutButton");

                if (loginButton != null)
                    loginButton.IsVisible = !_isLoggedIn;

                if (logoutButton != null)
                    logoutButton.IsVisible = _isLoggedIn;

                Debug.WriteLine($"✅ 登录状态已更新: {_isLoggedIn}，我的通知可见性: {_isMyNoticeVisible}");
            });
        }

        public void SetLoginStatus(bool isLoggedIn)
        {
            IsLoggedIn = isLoggedIn;
        }

        private async void OnLoginButtonClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("/login");
        }

        private async void OnLogoutButtonClicked(object sender, EventArgs e)
        {
            try
            {
                // Try to use the logout URL from the server if available
                if (!string.IsNullOrEmpty(_logoutUrl))
                {
                    Debug.WriteLine($"Logging out with URL: {_logoutUrl}");
                    using (var httpClient = new HttpClient())
                    {
                        try
                        {
                            await httpClient.GetAsync(_logoutUrl);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Logout URL call error: {ex.Message}");
                        }
                    }
                }
                else
                {
                    // Fallback to API logout
                    var apiService = new ApiService();
                    await apiService.LogoutAsync();
                }

                //  新增：最彻底的 Cookie 清除方式 - 重新创建 HttpClient
                // 这将创建全新的 HttpClient 和 HttpClientHandler，完全清除所有 Cookie
                HttpClientManager.ResetHttpClient();
                Debug.WriteLine(" 已重置 HttpClient，所有会话信息已清除");

                // Clear user credentials
                UserCredentialsService.Instance.Clear();

                // Clear stored login info
                var passwordService = new PasswordSecurityService();
                var lastUsername = await passwordService.GetLastUsernameAsync();
                if (!string.IsNullOrEmpty(lastUsername))
                {
                    await passwordService.ClearPasswordAsync(lastUsername);
                    await passwordService.ClearSecurityAnswerAsync(lastUsername);
                }

                // Reset UI
                IsLoggedIn = false;
                UserName.Text = "欢迎您！游客，请登录";
                UserAvatar.Source = "defalut_avatar_big.png";

                // Navigate to login page
                await Shell.Current.GoToAsync("/login");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Logout error: {ex.Message}");
                await DisplayAlert("错误", "退出失败", "确定");
            }
        }
    }
}
