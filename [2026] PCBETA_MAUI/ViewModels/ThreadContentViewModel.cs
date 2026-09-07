using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCBetaMAUI.Models;
using PCBetaMAUI.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using static PCBetaMAUI.Services.ApiService;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Graphics;

namespace PCBetaMAUI.ViewModels;

/// <summary>
/// ViewModel for thread content page showing individual thread/post content
/// </summary>
public partial class ThreadContentViewModel : ObservableObject
{
    private readonly ApiService _apiService;
    private readonly INavigationService _navigationService;
    private readonly IAlertService _alertService;  //  新增：弹窗服务
    private string? _currentThreadId;
    private string? _currentForumId;
    private string? _boardName;
    private int _currentPage = 1;
    private RatingFormData? _currentRatingFormData = null;  //  新增：当前评分表单数据（存储 formhash）

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
    private string replyRewardText = string.Empty;

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
    private int totalPage = 1;

    [ObservableProperty]
    private List<ContentElement> contentElements = new();

    [ObservableProperty]
    private string currentThreadUrl = string.Empty;  //  新增：当前帖子URL（用于下载Referer）

    [ObservableProperty]
    private string? editStatus = null;  //  新增：编辑状态 - 格式：本帖最后由 用户 于 日期 编辑

    [ObservableProperty]
    private string? moderationInfo = null;  //  新增：审核信息 - 格式：本主题由 审核员 于 日期 审核通过

    [ObservableProperty]
    private PollInfo? poll;

    [ObservableProperty]
    private bool hasPoll;

    [ObservableProperty]
    private bool isSubmittingPoll;

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

    [ObservableProperty]
    private bool canEditOp = false;  //  ✅ 新增：是否可以编辑楼主发帖

    [ObservableProperty]
    private bool ratingFormVisible = false;  //  新增：评分表单是否显示

    [ObservableProperty]
    private ReplyInfo? selectedReply = null;  //  新增：当前选中的回帖（用于评分）

    [ObservableProperty]
    private int ratingScore = 3;  //  新增：评分分数（1-6），默认中间值

    [ObservableProperty]
    private string ratingScoreText = "3"; // 新增：用于在 UI 上绑定输入框的分数文本

    [ObservableProperty]
    private string ratingReason = string.Empty;  //  新增：评分理由（可选）

    [ObservableProperty]
    private bool isSubmittingRating = false;  //  新增：是否正在提交评分

    [ObservableProperty]
    private int minScore = 0;  //  新增：最小评分

    [ObservableProperty]
    private int maxScore = 6;  //  新增：最大评分

    [ObservableProperty]
    private int dailyRemaining = 0;  //  新增：今日剩余评分次数

    /// <summary>
    /// ✅ 新增：楼主信息（用于评分）
    /// </summary>
    [ObservableProperty]
    private ReplyInfo? threadAuthorInfo = null;  //  新增：楼主信息（作为一个特殊的ReplyInfo对象）

    // ========== 新增：点评（评论）相关属性 ==========
    [ObservableProperty]
    private bool commentFormVisible = false;  //  点评表单是否显示

    [ObservableProperty]
    private ReplyInfo? selectedCommentTarget = null;  //  当前要点评的回帖

    [ObservableProperty]
    private string commentText = string.Empty;  //  点评文本

    [ObservableProperty]
    private bool isSubmittingComment = false;  //  是否正在提交点评

    [ObservableProperty]
    private string commentFormHash = string.Empty;  //  点评表单的formhash（从页面获取）

    // 短提示面板内容和可见性（供页面显示/动画使用）
    [ObservableProperty]
    private string transientMessageText = string.Empty;

    [ObservableProperty]
    private bool transientMessageVisible = false;

    public string ForumId => _currentForumId ?? string.Empty;

    public string RatingRangeDisplay => $"可评分范围：{MinScore} - {MaxScore}";

    public ThreadContentViewModel()
    {
        _apiService = new ApiService();
        _navigationService = new NavigationService();
        _alertService = new AlertService();  //  新增：初始化弹窗服务
    }

    [RelayCommand]
    public async Task Favorite()
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

            // 确保有 formhash（若没有则尝试从线程页面获取）
            var formhash = _currentRatingFormData?.FormHash ?? string.Empty;
            if (string.IsNullOrEmpty(formhash))
            {
                try
                {
                    var fetched = await _apiService.GetFormHashFromThreadAsync(CurrentThreadUrl + "&inajax=1");
                    if (!string.IsNullOrEmpty(fetched))
                    {
                        formhash = fetched;
                        if (_currentRatingFormData == null) _currentRatingFormData = new RatingFormData { FormHash = fetched };
                        else _currentRatingFormData.FormHash = fetched;
                    }
                }
                catch { }
            }
            var tid = _currentThreadId ?? ThreadID;
            var url = $"https://bbs.pcbeta.com/home.php?mod=spacecp&ac=favorite&type=thread&id={Uri.EscapeDataString(tid)}&formhash={Uri.EscapeDataString(formhash)}&inajax=1";

            var response = await HttpClientManager.Instance.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            // 解析返回内容，检查是否包含 信息收藏成功 标识
            string message = "收藏失败";
            if (content.Contains("信息收藏成功") || content.Contains("succeedhandle_"))
            {
                message = "收藏成功";
            }
            else
            {
                // 尝试从CDATA中提取更详细错误信息
                var start = content.IndexOf("CDATA[");
                if (start >= 0)
                {
                    var end = content.IndexOf("]]", start);
                    if (end > start)
                    {
                        var cdata = content.Substring(start + 6, end - start - 6);
                        // 移除HTML标签
                        var plain = System.Text.RegularExpressions.Regex.Replace(cdata, "<.*?>", "");
                        plain = System.Net.WebUtility.HtmlDecode(plain).Trim();
                        if (!string.IsNullOrEmpty(plain)) message = plain;
                    }
                }
            }

            // 在 VM 中设置提示文本并控制可见性，UI 层负责动画
            TransientMessageText = message;
            // Show
            TransientMessageVisible = true;

            // 保持 4 秒后隐藏
            await Task.Delay(4000);
            TransientMessageVisible = false;

        }
        catch (Exception ex)
        {
            Debug.WriteLine($"收藏出错: {ex.Message}");
        }
    }

    /// <summary>
    /// Initializes the view model with thread ID
    /// </summary>
    public async Task InitializeAsync(string threadId, string threadTitle, string? forumId = null, string? boardName = null, int pageNum = 1)
    {
        _currentThreadId = threadId;
        _currentForumId = forumId;
        ThreadId = threadId;
        ThreadTitle = threadTitle;
        _boardName = boardName;
        _currentPage = pageNum;
        CurrentPage = pageNum;

        //  新增：构建当前页面的完整URL（用于下载Referer）
        CurrentThreadUrl = $"https://bbs.pcbeta.com/forum.php?mod=viewthread&tid={threadId}";
        if (pageNum > 1)
            CurrentThreadUrl += $"&page={pageNum}";
        Debug.WriteLine($"📄 当前页面URL: {CurrentThreadUrl}");
        Debug.WriteLine($"📄 Forum ID: {ForumId}");

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
                _currentForumId = threadContent.ForumId ?? _currentForumId;
                //只有在第一页才设置楼主信息和帖子内容等等，后续页只更新回帖列表和分页信息
                if (_currentPage == 1)
                {
                    Author = threadContent.Author ?? "Unknown";
                    // 使用纯文本版本显示（可以后续改为使用富文本元素列表）
                    Content = string.IsNullOrEmpty(threadContent.PlainTextContent)
                        ? threadContent.RawHtmlContent
                        : threadContent.PlainTextContent;

                    PostTime = threadContent.PostTime;
                    OtherIfm = threadContent.OtherIfm;
                    Replies = threadContent.Replies;
                    ThreadID = _currentThreadId;
                    ReplyRewardText = threadContent.ReplyRewardText;

                    //  新增：赋值编辑状态和审核信息
                    EditStatus = threadContent.EditStatus;
                    ModerationInfo = threadContent.ModerationInfo;
                    Poll = threadContent.Poll;
                    HasPoll = Poll != null;

                    //  ✅ 新增：赋值编辑楼主发帖的权限和URL
                    CanEditOp = threadContent.CanEditOp;
                    Debug.WriteLine($" ViewModel 已设置 CanEditOp={CanEditOp}, EditOpUrl={threadContent.EditOpUrl?.Substring(0, Math.Min(50, threadContent.EditOpUrl?.Length ?? 0)) ?? "null"}");
                    if (!string.IsNullOrEmpty(EditStatus))
                    {
                        Debug.WriteLine($" ViewModel 已设置 EditStatus: {EditStatus}");
                    }

                    if (!string.IsNullOrEmpty(ModerationInfo))
                    {
                        Debug.WriteLine($" ViewModel 已设置 ModerationInfo: {ModerationInfo}");
                    }

                    //  新增：处理楼主帖子的点评
                    await PrepareCommentAvatarsAsync(threadContent.Comments);
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


                    //  新增：为楼主创建一个虚拟的 ReplyInfo 对象（用于评分）
                    ThreadAuthorInfo = new ReplyInfo
                    {
                        Id = !string.IsNullOrEmpty(threadContent.AuthorPostId)
                            ? $"post_{threadContent.AuthorPostId}"
                            : "post_1",  //  使用提取的post ID，如果为空则回退到post_1
                        Username = Author,
                        UserId = "author",
                        FloorNumber = "1楼",
                        PostTime = PostTime,
                        RatingUrl = threadContent.RatingUrl,
                        CanRate = !string.IsNullOrEmpty(threadContent.RatingUrl)
                    };
                    // Ensure ReplyId is set (SubmitRatingAsync checks ReplyId). Keep it consistent with Id.
                    ThreadAuthorInfo.ReplyId = ThreadAuthorInfo.Id;
                    Debug.WriteLine($"✅ 为楼主创建 ReplyInfo: Id={ThreadAuthorInfo.Id}, Username={ThreadAuthorInfo.Username}, CanRate={ThreadAuthorInfo.CanRate}");


                    //  新增：处理楼主帖子的评分
                    if (threadContent.RatingSummary?.RatingDetails != null)
                    {
                        await PrepareRatingAvatarsAsync(threadContent.RatingSummary.RatingDetails);
                    }
                    RatingSummaryData = threadContent.RatingSummary;
                    HasRatings = RatingSummaryData != null && (RatingSummaryData.TotalRatingCount > 0 || RatingSummaryData.RatingDetails.Count > 0);
                    if (HasRatings)
                    {
                        Debug.WriteLine($" ViewModel 已加载评分汇总: 总数={RatingSummaryData?.TotalRatingCount ?? 0}, 详情数={RatingSummaryData?.RatingDetails.Count ?? 0}");
                    }

                }

                //  新增：处理回帖
                await PrepareReplyAvatarsAsync(threadContent.ReplyList);
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
                Debug.WriteLine($"   - 分页信息: 当前第 {threadContent.CurrentPage} 页，共 {threadContent.TotalPages} 页");

                // 更新分页按钮 - 基于实际的分页数据，而不是 Replies 字段
                CanGoToPreviousPage = _currentPage > 1;
                CanGoToNextPage = _currentPage < threadContent.TotalPages;
                TotalPage= threadContent.TotalPages;

                Debug.WriteLine($"📖 分页按钮状态: 上一页={CanGoToPreviousPage}, 下一页={CanGoToNextPage}");
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

    private static async Task PrepareCommentAvatarsAsync(IEnumerable<CommentInfo>? comments)
    {
        if (comments == null)
        {
            return;
        }

        await Task.WhenAll(comments.Select(async comment =>
        {
            comment.AvatarSource = await LoadAvatarSourceAsync(comment.AvatarUrl);
        }));
    }

    private static async Task PrepareRatingAvatarsAsync(IEnumerable<RatingInfo> ratings)
    {
        await Task.WhenAll(ratings.Select(async rating =>
        {
            rating.AvatarSource = await LoadAvatarSourceAsync(rating.AvatarUrl);
        }));
    }

    private static async Task PrepareReplyAvatarsAsync(IEnumerable<ReplyInfo>? replies)
    {
        if (replies == null)
        {
            return;
        }

        await Task.WhenAll(replies.Select(async reply =>
        {
            reply.AvatarSource = await LoadAvatarSourceAsync(reply.AvatarUrl);
        }));
    }

    private static async Task<ImageSource?> LoadAvatarSourceAsync(string? avatarUrl)
    {
        var trimmedUrl = avatarUrl?.Trim();
        if (string.IsNullOrEmpty(trimmedUrl))
        {
            return null;
        }

        if (!Uri.TryCreate(trimmedUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return ImageSource.FromFile(trimmedUrl);
        }

        try
        {
            var imageBytes = await HttpClientManager.Instance.GetByteArrayAsync(trimmedUrl);
            return imageBytes.Length == 0
                ? null
                : ImageSource.FromStream(() => new MemoryStream(imageBytes, writable: false));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Avatar loading failed: {trimmedUrl}, {ex.Message}");
            return null;
        }
    }

    [RelayCommand]
    public async Task ShareBtn()
    {
        await Clipboard.SetTextAsync(CurrentThreadUrl);
        await _alertService.DisplayAlertAsync("提示信息", "已复制帖子URL，快去分享吧~", "确定");
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

    [RelayCommand]
    public async Task SubmitPollAsync()
    {
        if (Poll == null || !Poll.CanVote || IsSubmittingPoll)
            return;

        var selectedIds = Poll.Options.Where(option => option.IsSelected).Select(option => option.Id).ToList();
        if (selectedIds.Count == 0)
        {
            ErrorMessage = "请选择至少一个投票选项";
            return;
        }

        if (!Poll.IsMultiple && selectedIds.Count > 1)
        {
            ErrorMessage = "单选投票只能选择一个选项";
            return;
        }

        if (Poll.MaxChoices > 0 && selectedIds.Count > Poll.MaxChoices)
        {
            ErrorMessage = $"最多只能选择 {Poll.MaxChoices} 项";
            return;
        }

        try
        {
            if (!await _apiService.IsLoggedInAsync())
            {
                await _navigationService.NavigateToAsync("login");
                return;
            }

            IsSubmittingPoll = true;
            var response = await _apiService.SubmitPollAsync(ForumId, _currentThreadId ?? string.Empty, Poll.FormHash, selectedIds);
            if (string.IsNullOrWhiteSpace(response))
            {
                ErrorMessage = "投票提交失败，请稍后重试";
                return;
            }

            if (_apiService.LastPageContent.Contains("错误", StringComparison.OrdinalIgnoreCase) ||
                _apiService.LastPageContent.Contains("不能投票", StringComparison.OrdinalIgnoreCase))
            {
                ErrorMessage = "投票未提交成功，请检查投票状态";
                return;
            }

            await RefreshAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"投票提交失败: {ex.Message}";
            Debug.WriteLine($"❌ {ErrorMessage}");
        }
        finally
        {
            IsSubmittingPoll = false;
        }
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

    /// <summary>
    ///  新增：给楼主评分（从底部导航按钮触发）
    /// </summary>
    [RelayCommand]
    public async Task RateThreadAuthorAsync()
    {
        // 验证登录
        var loggedIn = await _apiService.IsLoggedInAsync();
        if (!loggedIn)
        {
            // 导航到登录页
            await _navigationService.NavigateToAsync("login");
            return;
        }

        if (ThreadAuthorInfo == null)
        {
            ErrorMessage = "无法获取楼主信息";
            Debug.WriteLine($"❌ {ErrorMessage}");
            return;
        }

        if (!ThreadAuthorInfo.CanRate || string.IsNullOrEmpty(ThreadAuthorInfo.RatingUrl))
        {
            ErrorMessage = "该帖子暂无法评分";
            Debug.WriteLine($"❌ {ErrorMessage}");
            return;
        }

        // 调用通用的 OpenRatingAsync，传递楼主信息
        await OpenRatingAsync(ThreadAuthorInfo);
    }

    /// <summary>
    [RelayCommand]
    public async Task OpenRatingAsync(ReplyInfo reply)
    {
        // 验证登录
        var loggedIn = await _apiService.IsLoggedInAsync();
        if (!loggedIn)
        {
            // 导航到登录页
            await _navigationService.NavigateToAsync("login");
            return;
        }

        if (reply == null)
        {
            ErrorMessage = "回帖信息无效";
            Debug.WriteLine($"❌ {ErrorMessage}");
            return;
        }

        if (string.IsNullOrEmpty(reply.RatingUrl))
        {
            ErrorMessage = "该回帖暂无法评分";
            Debug.WriteLine($"❌ {ErrorMessage}");
            return;
        }

        try
        {
            reply.RatingUrl += "&inajax=1";
            Debug.WriteLine($"⭐ 打开评分表单 (reply ID={reply.ReplyId}, rating URL={reply.RatingUrl})");

            // 设置选中的回帖
            SelectedReply = reply;
            IsSubmittingRating = true;

            // 获取评分表单数据（评分范围、剩余次数、formhash 等）
            var ratingFormData = await _apiService.GetRatingFormAsync(reply.RatingUrl);

            if (ratingFormData != null)
            {
                MinScore = ratingFormData.MinScore;
                MaxScore = ratingFormData.MaxScore;
                DailyRemaining = ratingFormData.DailyRemaining;
                
                // 设置默认评分为中间值，确保不选择0分（如果MinScore为0则从1开始）
                int defaultScore = MinScore == 0 ? 1 : MinScore;
                if (MaxScore > 0)
                {
                    defaultScore = Math.Max(1, (MinScore + MaxScore) / 2);
                }
                RatingScore = defaultScore;
                RatingScoreText = RatingScore.ToString();
                RatingReason = string.Empty;

                // 保存表单数据（包含 formhash）用于后续提交
                _currentRatingFormData = ratingFormData;

                Debug.WriteLine($"✅ 评分表单数据加载成功: 评分范围={MinScore}-{MaxScore}, 今日剩余={DailyRemaining}, 默认分数={defaultScore}");
                if (!string.IsNullOrEmpty(ratingFormData.FormHash))
                {
                    Debug.WriteLine($"✅ formhash: {ratingFormData.FormHash.Substring(0, Math.Min(20, ratingFormData.FormHash.Length))}...");
                }
            }
            else
            {
                ErrorMessage = "无法加载评分表单";
                Debug.WriteLine($"❌ 评分表单数据加载失败");
                SelectedReply = null;
            }

            // 显示评分表单
            RatingFormVisible = true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"打开评分表单失败: {ex.Message}";
            Debug.WriteLine($"❌ 错误: {ErrorMessage}");
            SelectedReply = null;
        }
        finally
        {
            IsSubmittingRating = false;
        }
    }

    /// <summary>
    ///  新增：选择评分分数
    /// </summary>
    [RelayCommand]
    public void SelectRatingScore(string score)
    {
        if (int.TryParse(score, out int scoreValue) && scoreValue >= MinScore && scoreValue <= MaxScore)
        {
            RatingScore = scoreValue;
            RatingScoreText = scoreValue.ToString();
            Debug.WriteLine($"⭐ 选择评分: {scoreValue} 分");
        }
    }

    /// <summary>
    ///  新增：提交评分
    /// </summary>
    [RelayCommand]
    public async Task SubmitRatingAsync()
    {
        if (SelectedReply == null)
        {
            ErrorMessage = "没有选中要评分的回帖";
            Debug.WriteLine($"❌ {ErrorMessage}");
            return;
        }

        if (string.IsNullOrEmpty(SelectedReply.ReplyId) || string.IsNullOrEmpty(SelectedReply.RatingUrl))
        {
            ErrorMessage = "回帖信息不完整";
            Debug.WriteLine($"❌ {ErrorMessage}");
            return;
        }

        if (_currentRatingFormData == null || string.IsNullOrEmpty(_currentRatingFormData.FormHash))
        {
            ErrorMessage = "无法获取表单数据，评分失败";
            Debug.WriteLine($"❌ {ErrorMessage}");
            return;
        }

        // 解析评分输入文本
        if (!int.TryParse(RatingScoreText, out var parsedScore))
        {
            ErrorMessage = "请输入有效的评分数值";
            Debug.WriteLine($"❌ {ErrorMessage}");
            return;
        }

        // 不允许评分为0，且必须在 MinScore..MaxScore 范围内
        var minAllowed = Math.Max(1, MinScore);
        if (parsedScore < minAllowed || parsedScore > MaxScore)
        {
            ErrorMessage = $"评分必须在 {minAllowed} 到 {MaxScore} 范围内";
            Debug.WriteLine($"❌ {ErrorMessage}");
            return;
        }

        // 将解析后的值写回 RatingScore
        RatingScore = parsedScore;

        try
        {
            IsSubmittingRating = true;

            // 从 ReplyId 中提取纯数字 pid（去掉 "post_" 前缀）
            var pidOnly = SelectedReply.ReplyId.Replace("post_", "");

            Debug.WriteLine($"📤 提交评分: TID={_currentThreadId}, PID={pidOnly}, Score={RatingScore}, FormHash={'*' + (_currentRatingFormData.FormHash?.Length ?? 0)}, Reason={RatingReason}");

            // 调用 API 提交评分，参数：tid, pid, formhash, referer, score1, reason
            ApiResultModel result = await _apiService.SubmitRatingAsync(
                tid: _currentThreadId ?? "",
                pid: pidOnly,  //  使用清理后的 pid（纯数字）
                formhash: _currentRatingFormData.FormHash ?? "",  //  使用提取的 formhash
                referer: SelectedReply.RatingUrl,
                score1: RatingScore.ToString(),  //  正确的参数名：score1
                reason: string.IsNullOrEmpty(RatingReason) ? null : RatingReason
            );

            if (result.IsSuccess)
            {
                ErrorMessage = "✅ 评分成功！";
                Debug.WriteLine($"✅ 评分提交成功");

                // 显示成功提示弹窗
                await _alertService.DisplayAlertAsync("评分成功", $"您已成功给该回帖评分 {RatingScore} 分", "确定");

                // 关闭评分表单
                RatingFormVisible = false;
                SelectedReply = null;
                RatingScore = 3;
                RatingScoreText = RatingScore.ToString();
                RatingReason = string.Empty;
                _currentRatingFormData = null;

                // 刷新页面以更新评分信息
                await RefreshAsync();
            }
            else
            {
                ErrorMessage = "评分提交失败，请重试";
                Debug.WriteLine($"❌ 评分提交失败");

                // 显示失败提示弹窗
                await _alertService.DisplayAlertAsync("评分失败",result.Message, "确定");
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"提交评分失败: {ex.Message}";
            Debug.WriteLine($"❌ 错误: {ErrorMessage}");
        }
        finally
        {
            IsSubmittingRating = false;
        }
    }

    /// <summary>
    ///  新增：关闭评分表单
    /// </summary>
    [RelayCommand]
    public void CloseRatingForm()
    {
        RatingFormVisible = false;
        SelectedReply = null;
        RatingScore = 3;
        RatingScoreText = RatingScore.ToString();
        RatingReason = string.Empty;
        ErrorMessage = string.Empty;
        Debug.WriteLine($"❌ 关闭评分表单");
    }

    /// <summary>
    /// 打开回帖编辑窗口
    /// </summary>
    [RelayCommand]
    public async Task OpenReplyThreadAsync()
    {
        // 验证登录
        var loggedIn = await _apiService.IsLoggedInAsync();
        if (!loggedIn)
        {
            // 导航到登录页
            await _navigationService.NavigateToAsync("login");
            return;
        }

        if (string.IsNullOrEmpty(_currentThreadId))
        {
            ErrorMessage = "无法打开回帖窗口：thread ID 未设置";
            Debug.WriteLine($"❌ {ErrorMessage}");
            return;
        }

        try
        {
            Debug.WriteLine($"📝 打开回帖窗口 (tid={_currentThreadId}, fid={ForumId})");

            // 导航到 ReplyThreadPage，并传递 threadId、forumId 和 threadTitle
            await _navigationService.NavigateToAsync("replythread", new Dictionary<string, object>
            {
                { "threadId", _currentThreadId },
                { "forumId", ForumId },
                { "threadTitle", ThreadTitle }
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"打开回帖窗口失败: {ex.Message}";
            Debug.WriteLine($"❌ {ErrorMessage}");
        }
    }

    /// <summary>
    /// ✅ 新增：打开评论回复窗口
    /// 用于回复某个具体的评论
    /// ✅ 改进：传递 author 和 postTime 参数用于格式化回复
    /// </summary>
    [RelayCommand]
    public async Task OpenReplyCommentAsync(ReplyInfo comment)
    {
        // 验证登录
        var loggedIn = await _apiService.IsLoggedInAsync();
        if (!loggedIn)
        {
            // 导航到登录页
            await _navigationService.NavigateToAsync("login");
            return;
        }

        if (comment == null)
        {
            ErrorMessage = "评论信息无效";
            Debug.WriteLine($"❌ {ErrorMessage}");
            return;
        }

        if (string.IsNullOrEmpty(_currentThreadId))
        {
            ErrorMessage = "无法打开评论回复窗口：thread ID 未设置";
            Debug.WriteLine($"❌ {ErrorMessage}");
            return;
        }

        try
        {
            Debug.WriteLine($"💬 打开评论回复窗口 (tid={_currentThreadId}, fid={ForumId}, repquote={comment.Id.Replace("post_", "")})");

            // 导航到 ReplyThreadPage，并额外传递 repquote 参数
            await _navigationService.NavigateToAsync("replythread", new Dictionary<string, object>
            {
                { "threadId", _currentThreadId },
                { "forumId", ForumId },
                { "threadTitle", ThreadTitle },
                { "repquote", comment.ReplyId.Replace("post_", "") },  // ✅ 新增：评论ID参数
                { "userReplyContent", comment.PlainTextContent },  // ✅ 新增：用户评论内容
                { "author", comment.Username },  // ✅ 改进：传递评论者用户名
                { "postTime", comment.PostTime }  // ✅ 改进：传递发表时间
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"打开评论回复窗口失败: {ex.Message}";
            Debug.WriteLine($"❌ {ErrorMessage}");
        }
    }

    partial void OnMinScoreChanged(int value)
    {
        OnPropertyChanged(nameof(RatingRangeDisplay));
    }

    partial void OnMaxScoreChanged(int value)
    {
        OnPropertyChanged(nameof(RatingRangeDisplay));
    }

    // ========== 新增：点评（评论）相关命令 ==========

    /// <summary>
    /// 打开点评表单
    /// </summary>
    [RelayCommand]
    public async Task OpenCommentAsync(ReplyInfo reply)
    {
        // 验证登录
        var loggedIn = await _apiService.IsLoggedInAsync();
        if (!loggedIn)
        {
            // 导航到登录页
            await _navigationService.NavigateToAsync("login");
            return;
        }

        if (reply == null)
        {
            ErrorMessage = "回帖信息无效";
            Debug.WriteLine($"❌ {ErrorMessage}");
            return;
        }

        try
        {
            SelectedCommentTarget = reply;
            CommentText = string.Empty;

            // 从线程内容URL中提取formhash
            // URL格式: https://bbs.pcbeta.com/forum.php?mod=viewthread&tid=2069023
            CommentFormHash = _currentRatingFormData?.FormHash ?? "";

            // 如果没有cached formhash，需要从页面获取
            if (string.IsNullOrEmpty(CommentFormHash))
            {
                Debug.WriteLine($"📝 获取点评表单的formhash...");
                var formHash = await _apiService.GetFormHashFromThreadAsync(CurrentThreadUrl+"&inajax=1");
                if (!string.IsNullOrEmpty(formHash))
                {
                    CommentFormHash = formHash;
                    // 缓存到评分表单数据中
                    if (_currentRatingFormData == null)
                    {
                        _currentRatingFormData = new RatingFormData { FormHash = formHash };
                    }
                    else
                    {
                        _currentRatingFormData.FormHash = formHash;
                    }
                }
            }

            Debug.WriteLine($"💬 打开点评表单 (reply ID={reply.Id}, formhash={CommentFormHash?.Substring(0, Math.Min(20, CommentFormHash?.Length ?? 0)) ?? "unknown"}...)");

            // 显示点评表单
            CommentFormVisible = true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"打开点评表单失败: {ex.Message}";
            Debug.WriteLine($"❌ 错误: {ErrorMessage}");
            SelectedCommentTarget = null;
        }
    }

    /// <summary>
    /// 关闭点评表单
    /// </summary>
    [RelayCommand]
    public void CloseCommentForm()
    {
        CommentFormVisible = false;
        SelectedCommentTarget = null;
        CommentText = string.Empty;
        ErrorMessage = string.Empty;
        Debug.WriteLine($"❌ 关闭点评表单");
    }

    /// <summary>
    /// 提交点评
    /// </summary>
    [RelayCommand]
    public async Task SubmitCommentAsync()
    {
        if (SelectedCommentTarget == null)
        {
            ErrorMessage = "没有选中要点评的回帖";
            Debug.WriteLine($"❌ {ErrorMessage}");
            return;
        }

        if (string.IsNullOrEmpty(CommentText?.Trim()))
        {
            ErrorMessage = "点评内容不能为空";
            Debug.WriteLine($"❌ {ErrorMessage}");
            return;
        }

        if (string.IsNullOrEmpty(_currentThreadId))
        {
            ErrorMessage = "线程ID未设置";
            Debug.WriteLine($"❌ {ErrorMessage}");
            return;
        }

        if (string.IsNullOrEmpty(CommentFormHash))
        {
            ErrorMessage = "无法获取表单数据，点评失败";
            Debug.WriteLine($"❌ {ErrorMessage}");
            return;
        }

        try
        {
            IsSubmittingComment = true;
            ErrorMessage = string.Empty;

            // 从 ReplyId/Id 中提取纯数字 pid（去掉 "post_" 前缀）
            var pidOnly = SelectedCommentTarget.ReplyId.Replace("post_", "");

            Debug.WriteLine($"📤 提交点评: TID={_currentThreadId}, PID={pidOnly}, FormHash={'*' + (CommentFormHash?.Length ?? 0)}, Comment={CommentText.Substring(0, Math.Min(50, CommentText.Length))}...");

            // 调用 API 提交点评
            ApiResultModel result = await _apiService.SubmitCommentAsync(
                tid: _currentThreadId,
                pid: pidOnly,
                formhash: CommentFormHash,
                message: CommentText
            );

            if (result.IsSuccess)
            {
                ErrorMessage = "✅ 点评成功！";
                Debug.WriteLine($"✅ 点评提交成功");

                // 显示成功提示弹窗
                await _alertService.DisplayAlertAsync("点评成功", "您的点评已提交成功", "确定");

                // 关闭点评表单
                CommentFormVisible = false;
                SelectedCommentTarget = null;
                CommentText = string.Empty;

                // 延迟刷新以确保服务器已更新
                await Task.Delay(1000);

                // 刷新页面以更新点评信息
                await RefreshAsync();
            }
            else
            {
                ErrorMessage = "点评提交失败，请重试";

                // 显示失败提示弹窗
                await _alertService.DisplayAlertAsync("点评失败", result.Message, "确定");
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"提交点评失败: {ex.Message}";
            Debug.WriteLine($"❌ 错误: {ErrorMessage}");
        }
        finally
        {
            IsSubmittingComment = false;
        }
    }

    /// <summary>
    /// ✅ 新增：打开编辑回帖窗口
    /// 用于编辑用户自己的回帖
    /// </summary>
    [RelayCommand]
    public async Task OpenEditReplyAsync(ReplyInfo reply)
    {
        if (reply == null)
        {
            ErrorMessage = "回帖信息无效";
            Debug.WriteLine($"❌ {ErrorMessage}");
            return;
        }

        if (!reply.CanEdit || string.IsNullOrEmpty(reply.EditUrl))
        {
            ErrorMessage = "该回帖无法编辑";
            Debug.WriteLine($"❌ {ErrorMessage}");
            return;
        }

        if (string.IsNullOrEmpty(_currentThreadId))
        {
            ErrorMessage = "无法打开编辑窗口：thread ID 未设置";
            Debug.WriteLine($"❌ {ErrorMessage}");
            return;
        }

        try
        {
            // 从 ReplyId 中提取纯数字 pid（去掉 "post_" 前缀）
            var pidOnly = reply.ReplyId.Replace("post_", "");

            Debug.WriteLine($"✏️ 打开编辑回帖窗口 (tid={_currentThreadId}, pid={pidOnly}, editUrl={reply.EditUrl})");

            // 导航到 EditReplyPage，并传递编辑信息
            await _navigationService.NavigateToAsync("editreply", new Dictionary<string, object>
            {
                { "threadId", _currentThreadId },
                { "replyId", pidOnly },
                { "editUrl", reply.EditUrl },
                { "threadTitle", ThreadTitle },
                { "username", reply.Username },
                { "originalContent", reply.PlainTextContent },
                { "forumId", ForumId },
                { "postId", pidOnly }
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"打开编辑窗口失败: {ex.Message}";
            Debug.WriteLine($"❌ {ErrorMessage}");
        }
    }
    /// <summary>
    /// ✅ 新增：打开编辑楼主发帖窗口
    /// 用于编辑当前用户自己发表的帖子（楼主发帖）
    /// </summary>
    [RelayCommand]
    public async Task OpenEditOpReplyAsync()
    {
        if (!CanEditOp)
        {
            ErrorMessage = "该帖子无法编辑";
            Debug.WriteLine($"❌ {ErrorMessage}");
            return;
        }

        if (string.IsNullOrEmpty(_currentThreadId))
        {
            ErrorMessage = "无法打开编辑窗口：thread ID 未设置";
            Debug.WriteLine($"❌ {ErrorMessage}");
            return;
        }

        try
        {
            Debug.WriteLine($"✏️ 打开编辑楼主发帖窗口 (tid={_currentThreadId}, fid={ForumId})");

            // 导航到 EditOPReplyPage，并传递编辑信息
            await _navigationService.NavigateToAsync("editopreply", new Dictionary<string, object>
            {
                { "threadId", _currentThreadId },
                { "boardId", ForumId },
                { "threadTitle", ThreadTitle },
                { "boardName", _boardName },
                { "postId", ThreadAuthorInfo.Id }
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"打开编辑窗口失败: {ex.Message}";
            Debug.WriteLine($"❌ {ErrorMessage}");
        }
    }
}
