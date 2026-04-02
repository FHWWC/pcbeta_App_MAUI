using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Diagnostics;
using PCBetaMAUI.Models;
using PCBetaMAUI.Services;

namespace PCBetaMAUI.ViewModels;

/// <summary>
/// ViewModel for thread content page showing individual thread/post content
/// </summary>
public partial class ThreadContentViewModel : ObservableObject
{
    private readonly ApiService _apiService;
    private readonly INavigationService _navigationService;
    private string? _currentThreadId;
    private int _currentPage = 1;

    [ObservableProperty]
    private string threadTitle = "Thread";

    [ObservableProperty]
    private string threadId = string.Empty;

    [ObservableProperty]
    private string author = string.Empty;

    [ObservableProperty]
    private string content = string.Empty;

    [ObservableProperty]
    private string postTime = string.Empty;

    [ObservableProperty]
    private string otherIfm = string.Empty;

    [ObservableProperty]
    private string threadID = string.Empty;

    [ObservableProperty]
    private int replies = 0;

    [ObservableProperty]
    private bool isLoading = false;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private bool hasContent = false;

    [ObservableProperty]
    private bool canGoToPreviousPage = false;

    [ObservableProperty]
    private bool canGoToNextPage = false;

    [ObservableProperty]
    private int currentPage = 1;

    [ObservableProperty]
    private List<ContentElement> contentElements = new();

    [ObservableProperty]
    private string currentThreadUrl = string.Empty;  //  新增：当前帖子URL（用于下载Referer）

    [ObservableProperty]
    private string? editStatus = null;  //  新增：编辑状态 - 格式：本帖最后由 用户 于 日期 编辑

    [ObservableProperty]
    private string? moderationInfo = null;  //  新增：审核信息 - 格式：本主题由 审核员 于 日期 审核通过

    [ObservableProperty]
    private ObservableCollection<CommentInfo> comments = new();  //  新增：评论列表

    [ObservableProperty]
    private bool hasComments = false;  //  新增：是否有评论（用于控制UI可见性）

    [ObservableProperty]
    private RatingSummary? ratingSummaryData = null;  //  新增：评分汇总（改名以避免与RatingSummary类冲突）

    [ObservableProperty]
    private bool hasRatings = false;  //  新增：是否有评分（用于控制UI可见性）

    [ObservableProperty]
    private bool areCommentsExpanded = true;  //  新增：点评是否展开

    [ObservableProperty]
    private bool areRatingsExpanded = true;  //  新增：评分是否展开

    [ObservableProperty]
    private ObservableCollection<ReplyInfo> replyList = new();  //  新增：回帖列表

    [ObservableProperty]
    private bool hasReplies = false;  //  新增：是否有回帖（用于控制UI可见性）

    [ObservableProperty]
    private bool areRepliesExpanded = true;  //  新增：回帖是否展开

    public ThreadContentViewModel()
    {
        _apiService = new ApiService();
        _navigationService = new NavigationService();
    }

    /// <summary>
    /// Initializes the view model with thread ID
    /// </summary>
    public async Task InitializeAsync(string threadId, string threadTitle)
    {
        _currentThreadId = threadId;
        ThreadId = threadId;
        ThreadTitle = threadTitle;
        _currentPage = 1;
        CurrentPage = 1;

        //  新增：构建当前页面的完整URL（用于下载Referer）
        CurrentThreadUrl = $"https://bbs.pcbeta.com/forum.php?mod=viewthread&tid={threadId}";
        Debug.WriteLine($"📄 当前页面URL: {CurrentThreadUrl}");

        await LoadThreadContentAsync();
    }

    /// <summary>
    /// 加载当前线程的内容
    /// </summary>
    [RelayCommand]
    public async Task LoadThreadContentAsync()
    {
        if (string.IsNullOrEmpty(_currentThreadId))
        {
            ErrorMessage = "没有选中线程";
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            var threadContent = await _apiService.GetThreadContentAsync(_currentThreadId, _currentPage);

            if (threadContent != null)
            {
                ThreadTitle = threadContent.Title ?? ThreadTitle;
                Author = threadContent.Author ?? "Unknown";

                // 使用纯文本版本显示（可以后续改为使用富文本元素列表）
                Content = string.IsNullOrEmpty(threadContent.PlainTextContent)
                    ? threadContent.RawHtmlContent
                    : threadContent.PlainTextContent;

                PostTime = threadContent.PostTime;
                OtherIfm = threadContent.OtherIfm;
                Replies = threadContent.Replies;
                ThreadID = _currentThreadId;

                //  新增：赋值编辑状态和审核信息
                EditStatus = threadContent.EditStatus;
                ModerationInfo = threadContent.ModerationInfo;

                if (!string.IsNullOrEmpty(EditStatus))
                {
                    Debug.WriteLine($" ViewModel 已设置 EditStatus: {EditStatus}");
                }
                if (!string.IsNullOrEmpty(ModerationInfo))
                {
                    Debug.WriteLine($" ViewModel 已设置 ModerationInfo: {ModerationInfo}");
                }

                //  新增：处理评论
                Comments.Clear();
                HasComments = false;
                if (threadContent.Comments != null && threadContent.Comments.Count > 0)
                {
                    foreach (var comment in threadContent.Comments)
                    {
                        Comments.Add(comment);
                    }
                    HasComments = true;
                    Debug.WriteLine($" ViewModel 已加载 {Comments.Count} 条评论");
                }

                //  新增：处理评分
                RatingSummaryData = threadContent.RatingSummary;
                HasRatings = RatingSummaryData != null && (RatingSummaryData.TotalRatingCount > 0 || RatingSummaryData.RatingDetails.Count > 0);
                if (HasRatings)
                {
                    Debug.WriteLine($" ViewModel 已加载评分汇总: 总数={RatingSummaryData?.TotalRatingCount ?? 0}, 详情数={RatingSummaryData?.RatingDetails.Count ?? 0}");
                }

                //  新增：处理回帖
                ReplyList.Clear();
                HasReplies = false;
                if (threadContent.ReplyList != null && threadContent.ReplyList.Count > 0)
                {
                    foreach (var reply in threadContent.ReplyList)
                    {
                        ReplyList.Add(reply);
                    }
                    HasReplies = true;
                    Debug.WriteLine($" ViewModel 已加载 {ReplyList.Count} 条回帖");
                }

                ContentElements = threadContent.ContentElements ?? new List<ContentElement>();
                HasContent = ContentElements.Count > 0;

                // 日志输出 - 用于调试
                Debug.WriteLine($" 加载帖子内容成功");
                Debug.WriteLine($"   - 标题: {ThreadTitle}");
                Debug.WriteLine($"   - 作者: {Author}");
                Debug.WriteLine($"   - 富文本元素数: {threadContent.ContentElements.Count}");
                Debug.WriteLine($"   - 评论数: {Comments.Count}");
                Debug.WriteLine($"   - 评分数: {RatingSummaryData?.TotalRatingCount ?? 0}");
                Debug.WriteLine($"   - 回帖数: {ReplyList.Count}");
                Debug.WriteLine($"   - 内容长度: {Content?.Length ?? 0} 字符");

                // 更新分页按钮
                CanGoToPreviousPage = _currentPage > 1;
                CanGoToNextPage = Replies > 0;
            }
            else
            {
                ErrorMessage = "加载帖子内容失败";
                HasContent = false;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 加载线程内容错误: {ex.Message}");
            ErrorMessage = $"加载失败: {ex.Message}";
            HasContent = false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Goes to the next page of the thread
    /// </summary>
    [RelayCommand]
    public async Task NextPageAsync()
    {
        _currentPage++;
        CurrentPage = _currentPage;
        await LoadThreadContentAsync();
    }

    /// <summary>
    /// Goes to the previous page of the thread
    /// </summary>
    [RelayCommand]
    public async Task PreviousPageAsync()
    {
        if (_currentPage > 1)
        {
            _currentPage--;
            CurrentPage = _currentPage;
            await LoadThreadContentAsync();
        }
    }

    /// <summary>
    /// Goes back to the thread list
    /// </summary>
    [RelayCommand]
    public async Task GoBackAsync()
    {
        await _navigationService.GoBackAsync();
    }

    /// <summary>
    /// Refreshes the current page
    /// </summary>
    [RelayCommand]
    public async Task RefreshAsync()
    {
        await LoadThreadContentAsync();
    }

    /// <summary>
    ///  新增：切换点评（评论）展开/收回状态
    /// </summary>
    [RelayCommand]
    public void ToggleCommentsExpanded()
    {
        AreCommentsExpanded = !AreCommentsExpanded;
        Debug.WriteLine($"💬 点评现在是：{(AreCommentsExpanded ? "展开" : "收回")}");
    }

    /// <summary>
    ///  新增：切换评分展开/收回状态
    /// </summary>
    [RelayCommand]
    public void ToggleRatingsExpanded()
    {
        AreRatingsExpanded = !AreRatingsExpanded;
        Debug.WriteLine($"⭐ 评分现在是：{(AreRatingsExpanded ? "展开" : "收回")}");
    }

    /// <summary>
    ///  新增：切换回帖展开/收回状态
    /// </summary>
    [RelayCommand]
    public void ToggleRepliesExpanded()
    {
        AreRepliesExpanded = !AreRepliesExpanded;
        Debug.WriteLine($"📝 回帖现在是：{(AreRepliesExpanded ? "展开" : "收回")}");
    }
}

