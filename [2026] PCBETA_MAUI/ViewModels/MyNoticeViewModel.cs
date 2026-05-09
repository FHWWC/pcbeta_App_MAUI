using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Diagnostics;
using PCBetaMAUI.Models;
using PCBetaMAUI.Services;

namespace PCBetaMAUI.ViewModels;

/// <summary>
/// ViewModel for MyNoticePage - 展示用户收到的通知
/// ✅ 新增：用于管理通知页面的数据和交互逻辑
/// </summary>
public partial class MyNoticeViewModel : ObservableObject
{
    private readonly ApiService _apiService;
    private readonly INavigationService _navigationService;
    private readonly XmlParsingService _xmlParsingService;

    [ObservableProperty]
    private ObservableCollection<NoticeInfo> notices = new();

    [ObservableProperty]
    private bool isLoading = false;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private bool hasNotices = false;

    [ObservableProperty]
    private int currentPage = 1;

    [ObservableProperty]
    private bool canGoToPreviousPage = false;

    [ObservableProperty]
    private bool canGoToNextPage = false;

    public MyNoticeViewModel()
    {
        _apiService = new ApiService();
        _navigationService = new NavigationService();
        _xmlParsingService = new XmlParsingService();
    }

    /// <summary>
    /// 加载通知列表
    /// ✅ 新增：从API加载通知页面并解析数据
    /// </summary>
    [RelayCommand]
    public async Task LoadNoticesAsync(int page = 1)
    {
        try
        {
            IsLoading = true;
            ErrorMessage = string.Empty;
            CurrentPage = page;

            Debug.WriteLine($"📥 开始加载通知（第 {page} 页）...");

            // ✅ 关键：调用API获取用户通知页面的HTML
            var html = await _apiService.GetNoticesPageHtmlAsync(page);

            if (string.IsNullOrEmpty(html))
            {
                ErrorMessage = "无法加载通知页面";
                Debug.WriteLine("❌ 通知页面HTML为空");
                return;
            }

            // 使用XmlParsingService解析通知
            var noticeList = _xmlParsingService.ParseNotices(html);

            // 清空之前的通知并添加新的
            Notices.Clear();
            foreach (var notice in noticeList)
            {
                Notices.Add(notice);
            }

            HasNotices = Notices.Count > 0;
            Debug.WriteLine($"✅ 成功加载 {Notices.Count} 个通知");

            // 检查分页按钮状态
            CanGoToPreviousPage = CurrentPage > 1;
            // ✅ 改进：可以根据实际页数确定是否有下一页
            CanGoToNextPage = noticeList.Count > 0; // 简化判断，如果返回了通知则认为还有下一页
        }
        catch (Exception ex)
        {
            ErrorMessage = $"加载通知失败: {ex.Message}";
            Debug.WriteLine($"❌ 加载通知错误: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 处理通知项的点击事件
    /// ✅ 新增：当通知包含线程链接时，跳转到ThreadContentPage
    /// </summary>
    [RelayCommand]
    public async Task SelectNoticeAsync(NoticeInfo notice)
    {
        try
        {
            if (notice == null || string.IsNullOrEmpty(notice.ThreadId))
            {
                Debug.WriteLine("⚠️ 通知不包含线程信息，无法跳转");
                return;
            }

            Debug.WriteLine($"🔗 跳转到线程: threadId={notice.ThreadId}, postId={notice.PostId}");

            // ✅ 跳转到 ThreadContentPage
            // 注意：参数名称必须匹配 ThreadContentPage 的 [QueryProperty] 装饰器定义
            await _navigationService.NavigateToAsync("threadcontent", new Dictionary<string, object>
            {
                { "threadId", notice.ThreadId },
                { "threadTitle", notice.NoticeText ?? "通知内容" }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 跳转失败: {ex.Message}");
            ErrorMessage = $"跳转失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 加载上一页
    /// </summary>
    [RelayCommand]
    public async Task GoToPreviousPageAsync()
    {
        if (CurrentPage > 1)
        {
            await LoadNoticesAsync(CurrentPage - 1);
        }
    }

    /// <summary>
    /// 加载下一页
    /// </summary>
    [RelayCommand]
    public async Task GoToNextPageAsync()
    {
        await LoadNoticesAsync(CurrentPage + 1);
    }

    /// <summary>
    /// 刷新当前页面
    /// </summary>
    [RelayCommand]
    public async Task RefreshAsync()
    {
        await LoadNoticesAsync(CurrentPage);
    }
}
