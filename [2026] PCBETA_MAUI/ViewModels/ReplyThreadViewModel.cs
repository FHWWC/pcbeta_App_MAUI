using CommunityToolkit.Mvvm.Input;
using PCBetaMAUI.Models;
using PCBetaMAUI.Services;
using PCBetaMAUI.Views;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using static PCBetaMAUI.Services.ApiService;

namespace PCBetaMAUI.ViewModels;

/// <summary>
/// 回帖页面 ViewModel
/// 处理回帖编辑、文件上传、删除等业务逻辑
/// </summary>
public class ReplyThreadViewModel : INotifyPropertyChanged
{
    private readonly ApiService _apiService;
    private readonly PasswordSecurityService _passwordService;
    private string _forumId;  // fid
    private string _threadId; // tid
    private string _repquote = string.Empty;  // ✅ 新增：评论回复ID
    private string _replyContent = string.Empty;
    private string _userReplyContent = string.Empty;//评论区某个用户的发帖内容，因为当前要回复他，所以保存他的评论
    private string _author = string.Empty;  // ✅ 改进：评论者用户名
    private string _postTime = string.Empty;  // ✅ 改进：发表时间
    private int _characterCount;
    private bool _isSubmitEnabled;
    private string _threadTitle = string.Empty;
    private string _threadInfo = string.Empty;
    private bool _isSubmitting = false;  // ✅ 新增：提交状态标志
    private string _formhash = string.Empty;
    private string _uid = string.Empty;
    private string _hash = string.Empty;

    public ObservableCollection<UploadedFileInfo> UploadedImages { get; }
    public ObservableCollection<UploadedFileInfo> UploadedAttachments { get; }

    // 绑定属性
    public string ReplyContent
    {
        get => _replyContent;
        set
        {
            if (SetProperty(ref _replyContent, value))
            {
                CharacterCount = value?.Length ?? 0;
                UpdateSubmitButtonState();
                // ✅ 改进：当内容变化时，通知 SubmitReplyCommand 重新检查 canExecute 条件
                SubmitReplyCommand?.NotifyCanExecuteChanged();
                Debug.WriteLine($"📝 ReplyContent 已更新: 长度={value?.Length ?? 0}, IsSubmitEnabled={IsSubmitEnabled}");
            }
        }
    }

    public int CharacterCount
    {
        get => _characterCount;
        set => SetProperty(ref _characterCount, value);
    }

    public bool IsSubmitEnabled
    {
        get => _isSubmitEnabled;
        set
        {
            if (SetProperty(ref _isSubmitEnabled, value))
            {
                // ✅ 改进：当 IsSubmitEnabled 变化时，通知 SubmitReplyCommand 重新评估 CanExecute
                SubmitReplyCommand?.NotifyCanExecuteChanged();
                Debug.WriteLine($"🔔 IsSubmitEnabled 已变化: {value}，已通知 SubmitReplyCommand 重新计算 CanExecute");
            }
        }
    }

    public string ThreadTitle
    {
        get => _threadTitle;
        set => SetProperty(ref _threadTitle, value);
    }

    public string ThreadInfo
    {
        get => _threadInfo;
        set => SetProperty(ref _threadInfo, value);
    }

    // ✅ 新增：显示是否为评论回复的属性
    private bool _isCommentReply;
    public bool IsCommentReply
    {
        get => _isCommentReply;
        set => SetProperty(ref _isCommentReply, value);
    }

    // ✅ 新增：显示评论ID的属性
    private string _replyToCommentId = string.Empty;
    public string ReplyToCommentId
    {
        get => _replyToCommentId;
        set => SetProperty(ref _replyToCommentId, value);
    }

    // ✅ 新增：提交状态属性 - 用于 UI 禁用按钮
    public bool IsSubmitting
    {
        get => _isSubmitting;
        set => SetProperty(ref _isSubmitting, value);
    }

    // 命令
    public IAsyncRelayCommand PickImageCommand { get; }
    public IAsyncRelayCommand PickAttachmentCommand { get; }
    public IAsyncRelayCommand<UploadedFileInfo> DeleteImageCommand { get; }
    public IAsyncRelayCommand<UploadedFileInfo> DeleteAttachmentCommand { get; }
    public IAsyncRelayCommand<UploadedFileInfo> InsertImageCommand { get; }
    public IAsyncRelayCommand<UploadedFileInfo> InsertAttachmentCommand { get; }
    public IAsyncRelayCommand SubmitReplyCommand { get; }
    public IAsyncRelayCommand CancelCommand { get; }

    /// <summary>
    /// 无参构造函数 - 用于 XAML 数据绑定和 Shell 导航
    /// </summary>
    public ReplyThreadViewModel() : this(new ApiService(), "", "")
    {
    }

    public ReplyThreadViewModel(ApiService apiService, string forumId, string threadId)
    {
        _apiService = apiService;
        _passwordService = new PasswordSecurityService();
        _forumId = forumId;
        _threadId = threadId;

        UploadedImages = new ObservableCollection<UploadedFileInfo>();
        UploadedAttachments = new ObservableCollection<UploadedFileInfo>();

        // 初始化命令
        PickImageCommand = new AsyncRelayCommand(OnPickImageAsync);
        PickAttachmentCommand = new AsyncRelayCommand(OnPickAttachmentAsync);
        DeleteImageCommand = new AsyncRelayCommand<UploadedFileInfo>(OnDeleteImageAsync);
        DeleteAttachmentCommand = new AsyncRelayCommand<UploadedFileInfo>(OnDeleteAttachmentAsync);
        InsertImageCommand = new AsyncRelayCommand<UploadedFileInfo>(OnInsertImageAsync);
        InsertAttachmentCommand = new AsyncRelayCommand<UploadedFileInfo>(OnInsertAttachmentAsync);

        // ✅ 改进：为 SubmitReplyCommand 添加 canExecute 委托
        // 直接检查 ReplyContent 属性而不是私有字段，确保获取最新值
        // 这样可以避免字段和属性之间的同步问题
        SubmitReplyCommand = new AsyncRelayCommand(
            OnSubmitReplyAsync,
            () => _isSubmitEnabled && !string.IsNullOrWhiteSpace(ReplyContent) && ReplyContent.Length >= 8);

        CancelCommand = new AsyncRelayCommand(OnCancelAsync);

        UpdateSubmitButtonState();
    }

    private async Task ReauthenticateAfterReplyAsync()
    {
        try
        {
            var username = await _passwordService.GetLastUsernameAsync();
            if (string.IsNullOrWhiteSpace(username))
            {
                Debug.WriteLine("⚠️ 回帖后重认证跳过：没有保存的用户名");
                return;
            }

            var password = await _passwordService.GetPasswordAsync(username);
            if (string.IsNullOrEmpty(password))
            {
                Debug.WriteLine("⚠️ 回帖后重认证跳过：没有保存的密码，保留当前会话");
                return;
            }

            var questionId = await _passwordService.GetSecurityQuestionIdAsync(username);
            var answer = await _passwordService.GetSecurityAnswerAsync(username);

            Debug.WriteLine("🔄 回帖提交成功，开始重新认证会话");

            // 优先使用论坛页面返回的完整退出 URL（通常包含 formhash）。
            // 该 URL 必须通过共享 HttpClient 调用，才能操作当前应用会话。
            var logoutUrl = UserCredentialsService.Instance.LogoutUrl;
            if (!string.IsNullOrWhiteSpace(logoutUrl))
            {
                if (!Uri.TryCreate(logoutUrl, UriKind.Absolute, out var logoutUri))
                {
                    logoutUri = new Uri($"{ApiService.BaseUrl}/{logoutUrl.TrimStart('/')}");
                }

                using var logoutResponse = await HttpClientManager.Instance.GetAsync(logoutUri.ToString());
                Debug.WriteLine($"✅ 回帖后退出接口调用完成: {(int)logoutResponse.StatusCode}");
            }
            else
            {
                await _apiService.LogoutAsync();
                Debug.WriteLine("✅ 回帖后调用备用退出接口完成");
            }

            // 与手动退出流程保持一致，清除失效的认证 Cookie 和访问校验状态。
            HttpClientManager.ResetHttpClient();
            Debug.WriteLine("✅ 回帖后已重置共享 HttpClient，准备重新登录");

            var (loginSuccess, errorMessage) = await _apiService.LoginAsync(
                username,
                password,
                questionId,
                answer);

            if (loginSuccess)
            {
                Debug.WriteLine("✅ 回帖后重新登录成功");
            }
            else
            {
                Debug.WriteLine($"❌ 回帖后重新登录失败: {errorMessage ?? "未知错误"}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 回帖后重认证异常: {ex.Message}");
        }
    }

    /// <summary>
    /// 初始化页面信息（从导航参数传入）
    /// </summary>
    public void InitializeThreadInfo(string title, string author, string postTime)
    {
        ThreadTitle = title;
        ThreadInfo = $"楼主: {author} | 发布于: {postTime}";
    }

    /// <summary>
    /// 初始化回帖页面的所有必要信息
    /// 包括 forumId、threadId 和线程信息
    /// ✅ 改进：支持评论回复（repquote）参数和新的 author、postTime 参数
    /// </summary>
    public void Initialize(string forumId, string threadId, string threadTitle, string? repquote = null, string? userReplyContent = null, string? author = null, string? postTime = null)
    {
        _forumId = forumId;
        _threadId = threadId;
        _repquote = repquote ?? string.Empty;
        _userReplyContent = userReplyContent ?? string.Empty;
        _author = author ?? string.Empty;  // ✅ 改进：存储评论者用户名
        _postTime = postTime ?? string.Empty;  // ✅ 改进：存储发表时间
        ThreadTitle = threadTitle;

        // ✅ 改进：根据是否有 repquote 参数来显示不同的信息
        if (!string.IsNullOrEmpty(_repquote))
        {
            ThreadInfo = $"回复帖子 #{threadId}";
            IsCommentReply = true;
            ReplyToCommentId = _repquote;
            Debug.WriteLine($"📝 初始化为评论回复模式: 回复评论 #{_repquote}, 作者={_author}, 时间={_postTime}");
        }
        else
        {
            ThreadInfo = $"回复帖子 #{threadId}";
            IsCommentReply = false;
            ReplyToCommentId = string.Empty;
            Debug.WriteLine($"📝 初始化为帖子回复模式: 回复帖子 #{threadId}");
        }

        Debug.WriteLine($"✅ ReplyThreadViewModel 初始化完成: fid={_forumId}, tid={_threadId}, repquote={_repquote}, title={ThreadTitle}");
    }

    /// <summary>
    /// 异步初始化回帖页面
    /// 关键：发送 GET 请求到回帖编辑页面，以建立正确的 session
    /// 这样后续的附件上传和回帖提交才能被正确绑定到该帖子上
    /// 
    /// ✅ 改进：支持评论回复（repquote）参数
    /// </summary>
    public async Task InitializeReplyPageAsync(string forumId, string threadId, string? repquote = null)
    {
        try
        {
            if (string.IsNullOrEmpty(forumId) || string.IsNullOrEmpty(threadId))
            {
                Debug.WriteLine("⚠️ ForumId 或 ThreadId 为空，跳过回帖页面初始化");
                return;
            }

            if (!string.IsNullOrEmpty(repquote))
            {
                Debug.WriteLine($"📄 开始初始化回帖页面（评论回复模式）: fid={forumId}, tid={threadId}, repquote={repquote}");
            }
            else
            {
                Debug.WriteLine($"📄 开始初始化回帖页面（帖子回复模式）: fid={forumId}, tid={threadId}");
            }

            // 获取回帖页面的 HTML（这会建立 session）
            // ✅ 改进：传递 repquote 参数到 API 方法
            var pageHtml = await _apiService.GetReplyEditPageHtmlAsync(forumId, threadId, repquote);

            if (string.IsNullOrEmpty(pageHtml))
            {
                Debug.WriteLine("⚠️ 回帖页面 HTML 为空");
                return;
            }

            _formhash = ExtractFormHashFromHtml(pageHtml);
            _uid = ExtractUidFromHtml(pageHtml);
            _hash = ExtractHashFromHtml(pageHtml);

            Debug.WriteLine($"✅ 回帖页面初始化完成，页面大小: {pageHtml.Length} 字节");
            Debug.WriteLine($"   - 这确保了后续的附件上传和回帖提交能被正确关联");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 回帖页面初始化失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 选择图片
    /// </summary>
    private async Task OnPickImageAsync()
    {
        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                FileTypes = FilePickerFileType.Images,
                PickerTitle = "选择图片"
            });

            if (result != null)
            {
                await UploadImageAsync(result);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 选择图片错误: {ex.Message}");
            await ShowErrorAlert("选择图片失败", ex.Message);
        }
    }

    /// <summary>
    /// 上传图片
    /// </summary>
    private async Task UploadImageAsync(FileResult fileResult)
    {
        try
        {
            var fileInfo = new UploadedFileInfo
            {
                FileName = Path.GetFileName(fileResult.FullPath),
                LocalPath = fileResult.FullPath,
                FileSize = await GetFileSizeStringAsync(fileResult.FullPath),
                IsUploading = true,
                StatusText = "上传中..."
            };

            UploadedImages.Add(fileInfo);

            // 获取认证参数
            //var (formhash, uid, hash) = await GetAuthenticationParamsAsync();

            // 上传文件
            var uploadResult = await UploadFileAsync(fileResult.FullPath, isImage: true, _uid, _hash);

            if (uploadResult != null)
            {
                fileInfo.RemoteUrl = uploadResult.Url ?? "";
                fileInfo.AttachmentId = uploadResult.AttachmentId ?? "";
                fileInfo.IsUploading = false;
                fileInfo.StatusText = "上传成功";
                fileInfo.StatusColor = Colors.Green;
                Debug.WriteLine($"✅ 图片上传成功: {fileInfo.FileName} (aid={fileInfo.AttachmentId})");
            }
            else
            {
                // 上传失败，删除该项
                UploadedImages.Remove(fileInfo);
                await ShowErrorAlert("上传失败", "图片上传失败，请重试");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 上传图片错误: {ex.Message}");
            await ShowErrorAlert("上传错误", ex.Message);
        }
    }

    /// <summary>
    /// 选择附件
    /// </summary>
    private async Task OnPickAttachmentAsync()
    {
        try
        {
            var fileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
{
    // iOS / macOS 使用 UTType（或通配）
    { DevicePlatform.iOS, new[]
        {
            "public.image",   // 覆盖 gif/jpg/png/webp（大部分）
            "public.archive", // zip
            "public.data"     // 兜底（允许其他文件显示）
        }
    },

    { DevicePlatform.macOS, new[]
        {
            "public.image",
            "public.archive",
            "public.data"
        }
    },

    // Android 用 MIME
    { DevicePlatform.Android, new[]
        {
            "image/*",
            "application/zip",
            "application/x-rar-compressed",
            "application/x-7z-compressed",
            "application/x-bittorrent"
        }
    },

    // Windows 必须用扩展名（关键）
    { DevicePlatform.WinUI, new[]
        {
            ".gif", ".jpg", ".jpeg", ".png", ".webp",
            ".zip", ".rar", ".7z", ".torrent", ".sitx"
        }
    }
});

            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "选择附件",
                FileTypes = fileTypes
            });

//            var allowedExtensions = new[]
//{
//    ".gif", ".jpg", ".jpeg", ".png", ".webp",
//    ".zip", ".rar", ".7z", ".torrent", ".sitx"
//};


            if (result != null)
            {
                await UploadAttachmentAsync(result);


                //var ext = Path.GetExtension(result.FileName).ToLower();

                //if (!allowedExtensions.Contains(ext))
                //{
                //    await Application.Current.MainPage.DisplayAlert("提示", "不支持的文件类型", "确定");
                //    return;
                //}

            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 选择附件错误: {ex.Message}");
            await ShowErrorAlert("选择附件失败", ex.Message);
        }
    }

    /// <summary>
    /// 上传附件
    /// </summary>
    private async Task UploadAttachmentAsync(FileResult fileResult)
    {
        try
        {
            var fileInfo = new UploadedFileInfo
            {
                FileName = Path.GetFileName(fileResult.FullPath),
                LocalPath = fileResult.FullPath,
                FileSize = await GetFileSizeStringAsync(fileResult.FullPath),
                IsUploading = true,
                StatusText = "上传中..."
            };

            UploadedAttachments.Add(fileInfo);

            // 获取认证参数
            //var (formhash, uid, hash) = await GetAuthenticationParamsAsync();

            // 上传文件
            var uploadResult = await UploadFileAsync(fileResult.FullPath, isImage: false, _uid, _hash);

            if (uploadResult != null)
            {
                fileInfo.RemoteUrl = uploadResult.Url ?? "";
                fileInfo.AttachmentId = uploadResult.AttachmentId ?? "";
                fileInfo.IsUploading = false;
                fileInfo.StatusText = "上传成功";
                fileInfo.StatusColor = Colors.Green;
                Debug.WriteLine($"✅ 附件上传成功: {fileInfo.FileName} (aid={fileInfo.AttachmentId})");
            }
            else
            {
                // 上传失败，删除该项
                UploadedAttachments.Remove(fileInfo);
                await ShowErrorAlert("上传失败", "附件上传失败，请重试");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 上传附件错误: {ex.Message}");
            await ShowErrorAlert("上传错误", ex.Message);
        }
    }

    /// <summary>
    /// 通用文件上传方法
    /// 调用 /misc.php?mod=swfupload&operation=upload 接口
    /// 
    /// 包含两个步骤：
    /// 1. 上传文件 - 获得附件ID
    /// 2. 绑定附件 - 将附件关联到帖子
    /// </summary>
    private async Task<UploadResult?> UploadFileAsync(string filePath, bool isImage, string uid, string hash)
    {
        try
        {
            // 步骤1：构建上传URL
            var uploadUrl = isImage
                ? $"https://bbs.pcbeta.com/misc.php?mod=swfupload&action=swfupload&operation=upload&type=image"
                : $"https://bbs.pcbeta.com/misc.php?mod=swfupload&action=swfupload&operation=upload&fid={_forumId}";

            Debug.WriteLine($"📝 步骤1: 上传文件");
            var uploadResult = await _apiService.UploadFileAsync(uploadUrl, filePath, uid, hash,isImage, _forumId);

            if (uploadResult == null)
            {
                Debug.WriteLine($"❌ 文件上传失败");
                return null;
            }

            Debug.WriteLine($"✅ 文件上传成功，附件ID: {uploadResult.AttachmentId}");

            // 步骤2：绑定附件到帖子（仅对非图片有效，图片自动绑定）
            if (!isImage && !string.IsNullOrEmpty(uploadResult.AttachmentId) && !string.IsNullOrEmpty(_forumId))
            {
                Debug.WriteLine($"📝 步骤2: 绑定附件到帖子");
                var isBindSuccess = await _apiService.BindAttachmentAsync(uploadResult.AttachmentId, _forumId,isImage);

                if (isBindSuccess)
                {
                    Debug.WriteLine($"✅ 附件绑定成功");
                }
                else
                {
                    Debug.WriteLine($"⚠️ 附件绑定失败（但上传成功）");
                    // 上传成功即可，绑定失败可能是临时问题，发帖时会重新绑定
                }
            }

            return uploadResult;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 文件上传请求错误: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 删除已上传的图片
    /// </summary>
    private async Task OnDeleteImageAsync(UploadedFileInfo? fileInfo)
    {
        if (fileInfo == null) return;

        try
        {
            // 如果已上传到服务器，先删除服务器文件
            if (!string.IsNullOrEmpty(fileInfo.AttachmentId))
            {
                await DeleteAttachmentFromServerAsync(fileInfo.AttachmentId);
            }

            // 从本地列表移除
            UploadedImages.Remove(fileInfo);
            Debug.WriteLine($"✅ 删除图片: {fileInfo.FileName}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 删除图片错误: {ex.Message}");
            await ShowErrorAlert("删除失败", ex.Message);
        }
    }

    /// <summary>
    /// 删除已上传的附件
    /// </summary>
    private async Task OnDeleteAttachmentAsync(UploadedFileInfo? fileInfo)
    {
        if (fileInfo == null) return;

        try
        {
            // 如果已上传到服务器，先删除服务器文件
            if (!string.IsNullOrEmpty(fileInfo.AttachmentId))
            {
                await DeleteAttachmentFromServerAsync(fileInfo.AttachmentId);
            }

            // 从本地列表移除
            UploadedAttachments.Remove(fileInfo);
            Debug.WriteLine($"✅ 删除附件: {fileInfo.FileName}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 删除附件错误: {ex.Message}");
            await ShowErrorAlert("删除失败", ex.Message);
        }
    }

    /// <summary>
    /// 插入已上传的图片到编辑器光标位置
    /// 格式：[attachimg]{AttachmentId}[/attachimg]
    /// </summary>
    private async Task OnInsertImageAsync(UploadedFileInfo? fileInfo)
    {
        if (fileInfo == null || string.IsNullOrEmpty(fileInfo.AttachmentId))
        {
            await ShowErrorAlert("插入失败", "图片未上传或附件ID为空");
            return;
        }

        try
        {
            var tag = $"[attachimg]{fileInfo.AttachmentId}[/attachimg]";
            InsertTextAtCursor(tag);
            Debug.WriteLine($"✅ 已插入图片标签: [attachimg]{fileInfo.AttachmentId}[/attachimg]");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 插入图片标签失败: {ex.Message}");
            await ShowErrorAlert("插入失败", ex.Message);
        }
    }

    /// <summary>
    /// 插入已上传的附件到编辑器光标位置
    /// 格式：[attach]{AttachmentId}[/attach]
    /// </summary>
    private async Task OnInsertAttachmentAsync(UploadedFileInfo? fileInfo)
    {
        if (fileInfo == null || string.IsNullOrEmpty(fileInfo.AttachmentId))
        {
            await ShowErrorAlert("插入失败", "附件未上传或附件ID为空");
            return;
        }

        try
        {
            var tag = $"[attach]{fileInfo.AttachmentId}[/attach]";
            InsertTextAtCursor(tag);
            Debug.WriteLine($"✅ 已插入附件标签: [attach]{fileInfo.AttachmentId}[/attach]");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 插入附件标签失败: {ex.Message}");
            await ShowErrorAlert("插入失败", ex.Message);
        }
    }

    /// <summary>
    /// 在编辑器光标位置插入文本
    /// 获取当前光标位置，在该位置插入标签，然后更新光标位置
    /// </summary>
    private void InsertTextAtCursor(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        try
        {
            // 从Shell导航栈获取当前页面
            var navStack = Shell.Current?.Navigation.NavigationStack;
            if (navStack == null || navStack.Count == 0)
            {
                Debug.WriteLine("⚠️ 无法获取导航栈");
                return;
            }

            // 获取当前页面（最后一个）
            var currentPage = navStack.Last() as ReplyThreadPage;
            if (currentPage == null)
            {
                Debug.WriteLine("⚠️ 当前页面不是ReplyThreadPage");
                return;
            }

            // 通过反射获取Editor控件
            var replyEditorField = currentPage.GetType().GetField("ReplyEditor", 
                System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (replyEditorField == null)
            {
                Debug.WriteLine("⚠️ 未找到ReplyEditor字段");
                return;
            }

            var editor = replyEditorField.GetValue(currentPage) as Editor;
            if (editor == null)
            {
                Debug.WriteLine("⚠️ ReplyEditor为null");
                return;
            }

            // 获取当前光标位置
            var cursorPos = editor.CursorPosition;
            if (cursorPos < 0)
            {
                // 如果获取失败，默认插入到末尾
                cursorPos = ReplyContent.Length;
                Debug.WriteLine($"⚠️ 无法获取光标位置，默认插入到末尾 (pos={cursorPos})");
            }

            // 在光标位置插入文本
            var newContent = ReplyContent.Insert(cursorPos, text);
            ReplyContent = newContent;

            // 更新光标位置到插入文本之后
            editor.CursorPosition = cursorPos + text.Length;

            Debug.WriteLine($"✅ 文本已插入到位置 {cursorPos}，新长度: {newContent.Length}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 插入文本时发生异常: {ex.Message}\n{ex.StackTrace}");
        }
    }

    /// <summary>
    /// 从服务器删除附件
    /// 调用 /forum.php?mod=ajax&action=deleteattach 接口
    /// </summary>
    private async Task DeleteAttachmentFromServerAsync(string attachmentId)
    {
        try
        {
            // 动态获取 formhash
            //var (formhash, _, _) = await GetAuthenticationParamsAsync();

            var deleteUrl = $"https://bbs.pcbeta.com/forum.php?mod=ajax&action=deleteattach&inajax=yes&aids[]={attachmentId}&tid={_threadId}&pid=&formhash={_formhash}";

            await _apiService.DeleteAttachmentAsync(deleteUrl);
            Debug.WriteLine($"✅ 服务器删除附件: aid={attachmentId}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"⚠️ 服务器删除附件失败（本地已删除）: {ex.Message}");
            // 本地删除已成功，服务器删除失败可以忽略
        }
    }

    /// <summary>
    /// 获取页面认证参数（formhash、uid、hash）
    /// 这些参数需要从回帖页面的 HTML 中动态提取
    /// </summary>
    private async Task<(string formhash, string uid, string hash)> GetAuthenticationParamsAsync()
    {
        try
        {
            // 获取回帖页面的 HTML
            var pageHtml = await _apiService.GetThreadPageHtmlAsync(_threadId);

            if (string.IsNullOrEmpty(pageHtml))
            {
                Debug.WriteLine("⚠️ 无法获取回帖页面 HTML");
                return ("", "", "");
            }

            // 从 HTML 中提取参数
            var formhash = ExtractFormHashFromHtml(pageHtml);
            var uid = ExtractUidFromHtml(pageHtml);
            var hash = ExtractHashFromHtml(pageHtml);

            Debug.WriteLine($"✅ 提取认证参数: formhash={formhash}, uid={uid}, hash={hash.Substring(0, 8)}...");

            return (formhash, uid, hash);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"⚠️ 获取认证参数失败: {ex.Message}");
            return ("", "", "");
        }
    }

    /// <summary>
    /// 从 HTML 中提取 formhash
    /// 格式：<input type="hidden" name="formhash" id="formhash" value="bb8a32ca" />
    /// </summary>
    private string ExtractFormHashFromHtml(string html)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            html,
            @"<input\s+type=""hidden""\s+name=""formhash""\s+id=""formhash""\s+value=""([^""]+)""",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );

        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        // 备用模式
        match = System.Text.RegularExpressions.Regex.Match(
            html,
            @"<input[^>]*name=""formhash""[^>]*value=""([^""]+)""",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );

        return match.Success ? match.Groups[1].Value : "";
    }

    /// <summary>
    /// 从 HTML 中提取 uid
    /// 格式：uploadformdata:{uid:"4819662", ...}
    /// </summary>
    private string ExtractUidFromHtml(string html)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            html,
            @"uid[""']?\s*:\s*[""']?(\d+)[""']?",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );

        return match.Success ? match.Groups[1].Value : "";
    }

    /// <summary>
    /// 从 HTML 中提取 hash
    /// 格式：uploadformdata:{..., hash:"eed3eb9354f87f458b4d6d838624370e"}
    /// </summary>
    private string ExtractHashFromHtml(string html)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            html,
            @"hash[""']?\s*:\s*[""']([a-f0-9]{32})[""']",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );

        return match.Success ? match.Groups[1].Value : "";
    }

    /// <summary>
    /// ✅ 新增：从 HTML 中提取预加密的 noticeauthor 值
    /// 论坛服务器已经在生成回帖页面时计算好了加密值
    /// 格式：<input type="hidden" name="noticeauthor" value="a108TbtVcU6wSDyNb6hnLS+076lMMoohw6m015TOh8RNe1/7Dw" />
    /// </summary>
    private string ExtractNoticeAuthorFromHtml(string html)
    {
        try
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                html,
                @"<input\s+type=""hidden""\s+name=""noticeauthor""\s+value=""([^""]+)""",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );

            if (match.Success)
            {
                var encryptedAuthor = match.Groups[1].Value;
                Debug.WriteLine($"✅ 成功提取预加密的 noticeauthor: {encryptedAuthor.Substring(0, 20)}...");
                return encryptedAuthor;
            }

            // 备用模式
            match = System.Text.RegularExpressions.Regex.Match(
                html,
                @"name=""noticeauthor""\s+value=""([^""]+)""",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );

            if (match.Success)
            {
                var encryptedAuthor = match.Groups[1].Value;
                Debug.WriteLine($"✅ 使用备用模式提取预加密的 noticeauthor: {encryptedAuthor.Substring(0, 20)}...");
                return encryptedAuthor;
            }

            Debug.WriteLine($"⚠️ 未找到 noticeauthor 值，可能不是评论回复模式");
            return "";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 提取 noticeauthor 失败: {ex.Message}");
            return "";
        }
    }

    /// <summary>
    /// ✅ 新增：从 HTML 中提取预格式化的引用内容 noticetrimstr
    /// 论坛服务器生成的格式化引用（包含 [quote] 标签）
    /// 格式：<input type="hidden" name="noticetrimstr" value="[quote]...[/quote]" />
    /// 注意：该值可能跨多行
    /// </summary>
    private string ExtractNoticeTrimstrFromHtml(string html)
    {
        try
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                html,
                @"<input\s+type=""hidden""\s+name=""noticetrimstr""\s+value=""([^""]*?)""",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline
            );

            if (match.Success)
            {
                var trimstr = match.Groups[1].Value;
                Debug.WriteLine($"✅ 成功提取 noticetrimstr: {trimstr.Substring(0, Math.Min(30, trimstr.Length))}...");
                return trimstr;
            }

            // 备用模式：处理属性顺序不同的情况
            match = System.Text.RegularExpressions.Regex.Match(
                html,
                @"name=""noticetrimstr""\s+value=""([^""]*?)""",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline
            );

            if (match.Success)
            {
                var trimstr = match.Groups[1].Value;
                Debug.WriteLine($"✅ 使用备用模式提取 noticetrimstr");
                return trimstr;
            }

            Debug.WriteLine($"⚠️ 未找到 noticetrimstr 值");
            return "";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 提取 noticetrimstr 失败: {ex.Message}");
            return "";
        }
    }

    /// <summary>
    /// ✅ 新增：从 HTML 中提取纯文本的被回复内容 noticeauthormsg
    /// 这是被回复内容的纯文本版本，用于引用显示
    /// 格式：<input type="hidden" name="noticeauthormsg" value="纯文本内容" />
    /// </summary>
    private string ExtractNoticeAuthorMsgFromHtml(string html)
    {
        try
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                html,
                @"<input\s+type=""hidden""\s+name=""noticeauthormsg""\s+value=""([^""]*?)""",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline
            );

            if (match.Success)
            {
                var msg = match.Groups[1].Value;
                Debug.WriteLine($"✅ 成功提取 noticeauthormsg: {msg.Substring(0, Math.Min(30, msg.Length))}...");
                return msg;
            }

            // 备用模式
            match = System.Text.RegularExpressions.Regex.Match(
                html,
                @"name=""noticeauthormsg""\s+value=""([^""]*?)""",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline
            );

            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            Debug.WriteLine($"⚠️ 未找到 noticeauthormsg 值");
            return "";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 提取 noticeauthormsg 失败: {ex.Message}");
            return "";
        }
    }

    /// <summary>
    /// ✅ 新增：从 HTML 中提取被回复的 post ID (reppid)
    /// 格式：<input type="hidden" name="reppid" value="57919100" />
    /// </summary>
    private string ExtractReppidFromHtml(string html)
    {
        try
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                html,
                @"<input\s+type=""hidden""\s+name=""reppid""\s+value=""(\d+)""",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );

            if (match.Success)
            {
                var reppid = match.Groups[1].Value;
                Debug.WriteLine($"✅ 成功提取 reppid: {reppid}");
                return reppid;
            }

            // 备用模式
            match = System.Text.RegularExpressions.Regex.Match(
                html,
                @"name=""reppid""\s+value=""(\d+)""",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );

            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            Debug.WriteLine($"⚠️ 未找到 reppid 值");
            return "";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 提取 reppid 失败: {ex.Message}");
            return "";
        }
    }

    /// <summary>
    /// ✅ 新增：从 HTML 中提取被回复的 post ID (reppost)
    /// 格式：<input type="hidden" name="reppost" value="57919100" />
    /// </summary>
    private string ExtractReppostFromHtml(string html)
    {
        try
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                html,
                @"<input\s+type=""hidden""\s+name=""reppost""\s+value=""(\d+)""",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );

            if (match.Success)
            {
                var reppost = match.Groups[1].Value;
                Debug.WriteLine($"✅ 成功提取 reppost: {reppost}");
                return reppost;
            }

            // 备用模式
            match = System.Text.RegularExpressions.Regex.Match(
                html,
                @"name=""reppost""\s+value=""(\d+)""",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );

            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            Debug.WriteLine($"⚠️ 未找到 reppost 值");
            return "";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 提取 reppost 失败: {ex.Message}");
            return "";
        }
    }

    /// <summary>
    /// 提交回帖
    /// </summary>
    private async Task OnSubmitReplyAsync()
    {
        Debug.WriteLine($"🚀 OnSubmitReplyAsync 被调用");
        //CookieDiagnostics.TakeSnapshot("回帖提交前");
        IsSubmitting = true;  // ✅ 新增：标记提交状态
        try
        {
            if (!ValidateReply())
            {
                Debug.WriteLine($"⚠️ 回帖验证失败");
                return;
            }

            // 获取认证参数
            //var (formhash, uid, hash) = await GetAuthenticationParamsAsync();

            if (string.IsNullOrEmpty(_formhash))
            {
                await ShowErrorAlert("错误", "无法获取必要的认证参数，请重试");
                return;
            }

            // 调用发帖 API
            ApiResultModel result = new ApiResultModel();
            if(IsCommentReply)
            {
                // ✅ 关键改进：从回帖页面 HTML 中提取所有预加密的隐藏参数
                Debug.WriteLine($"📝 提交评论回复模式：开始提取隐藏参数");

                // 获取回帖页面的 HTML（包含所有隐藏参数）
                var pageHtml = await _apiService.GetReplyEditPageHtmlAsync(_forumId, _threadId, ReplyToCommentId);

                if (string.IsNullOrEmpty(pageHtml))
                {
                    Debug.WriteLine($"⚠️ 无法获取回帖页面HTML，使用回退方案");
                    // 回退：使用之前保存的值
                    string noticeStr = $"[quote][color=#999999]{_author} 发表于 {_postTime}[/color]\n{_userReplyContent}[/quote]";
                    string noticeAuthorMsg = _userReplyContent;

                    // ✅ 新增：构建附件元数据列表
                    var attachmentMetadataList = BuildAttachmentMetadataList();

                    result = await _apiService.SubmitReplyPLQAsync(
                        _forumId,
                        _threadId, 
                        _replyContent, 
                        _formhash,
                        noticeStr,
                        noticeAuthorMsg,
                        ReplyToCommentId,
                        "",  // ✅ 回退方案中没有 noticeauthor，传空字符串
                        attachmentMetadataList);  // ✅ 新增：添加附件元数据
                }
                else
                {
                    // 从 HTML 中提取所有隐藏参数
                    var noticeAuthor = ExtractNoticeAuthorFromHtml(pageHtml);  // 🔐 加密的用户名
                    var noticeTrimstr = ExtractNoticeTrimstrFromHtml(pageHtml).Replace("amp;", "");  // 格式化的引用内容
                    var noticeAuthorMsg = ExtractNoticeAuthorMsgFromHtml(pageHtml);  // 纯文本回复内容
                    var reppid = ExtractReppidFromHtml(pageHtml);  // 被回复post ID
                    var reppost = ExtractReppostFromHtml(pageHtml);  // 被回复post ID

                    Debug.WriteLine($"📝 提交评论回复: 回复评论 #{ReplyToCommentId}");
                    Debug.WriteLine($"   - noticeAuthor (encrypted): {(string.IsNullOrEmpty(noticeAuthor) ? "❌ 未找到" : noticeAuthor.Substring(0, Math.Min(20, noticeAuthor.Length)) + "...")}");
                    Debug.WriteLine($"   - noticeTrimstr: {(string.IsNullOrEmpty(noticeTrimstr) ? "❌ 未找到" : noticeTrimstr.Substring(0, Math.Min(50, noticeTrimstr.Length)) + "...")}");
                    Debug.WriteLine($"   - noticeAuthorMsg: {(string.IsNullOrEmpty(noticeAuthorMsg) ? "❌ 未找到" : noticeAuthorMsg.Substring(0, Math.Min(30, noticeAuthorMsg.Length)) + "...")}");
                    Debug.WriteLine($"   - reppid: {reppid}");
                    Debug.WriteLine($"   - reppost: {reppost}");

                    // ✅ 新增：构建附件元数据列表
                    var attachmentMetadataList = BuildAttachmentMetadataList();

                    // ✅ 使用从HTML中提取的真实值进行提交
                    result = await _apiService.SubmitReplyPLQAsync(
                        _forumId,
                        _threadId, 
                        _replyContent, 
                        _formhash,
                        noticeTrimstr,  // 使用提取的格式化引用内容
                        noticeAuthorMsg,  // 使用提取的纯文本内容
                        ReplyToCommentId,
                        noticeAuthor,  // ✅ 关键：添加加密的用户名参数
                        attachmentMetadataList);  // ✅ 新增：添加附件元数据
                }
            }
            else
            {
                Debug.WriteLine($"📝 提交帖子回复");

                // ✅ 新增：构建附件元数据列表
                var attachmentMetadataList = BuildAttachmentMetadataList();
                result = await _apiService.SubmitReplyLZAsync(_forumId, _threadId, _replyContent, _formhash, attachmentMetadataList);
            }

            if (result.IsSuccess)
            {
                Debug.WriteLine($"✅ 回帖提交成功");
                await ShowSuccessAlert("发送成功", "回帖已发送");
                await ReauthenticateAfterReplyAsync();
                Debug.WriteLine($"📝 即将返回上一页并刷新");

                //CookieDiagnostics.TakeSnapshot("回帖提交后");
                //CookieDiagnostics.PrintAllSnapshots();  // 打印诊断信息

                // 使用 Shell 导航返回上一页
                try
                {
                    // ✅ 改进：只返回上一页，让 ThreadContentPage.OnAppearing 自动处理刷新
                    // 这避免了两次 LoadThreadContentAsync 调用导致的会话状态不一致问题
                    await Shell.Current.GoToAsync("..");
                    Debug.WriteLine($"✅ Shell GoToAsync('..')完成，ThreadContentPage.OnAppearing 将自动触发刷新");
                }
                catch (Exception navEx)
                {
                    Debug.WriteLine($"❌ 导航错误: {navEx.Message}\n{navEx.StackTrace}");
                }
            }
            else
            {
                await ShowErrorAlert("发送失败", result.Message);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 提交回帖错误: {ex.Message}\n{ex.StackTrace}");
            await ShowErrorAlert("提交错误", ex.Message);
        }
        finally
        {
            IsSubmitting = false;  // ✅ 新增：重置提交状态
            Debug.WriteLine($"✅ IsSubmitting 已重置为 false");
        }
    }

    /// <summary>
    /// ✅ 新增：构建附件元数据列表
    /// 将 UploadedImages 和 UploadedAttachments 中的信息转换为 AttachmentMetadata 列表
    /// ✅ 改进：根据文件类型设置 IsAttachment 标志
    /// </summary>
    private List<AttachmentMetadata> BuildAttachmentMetadataList()
    {
        var metadataList = new List<AttachmentMetadata>();

        // 添加已上传的图片的元数据
        foreach (var image in UploadedImages)
        {
            if (!string.IsNullOrEmpty(image.AttachmentId))
            {
                metadataList.Add(new AttachmentMetadata
                {
                    AttachmentId = image.AttachmentId,
                    Description = image.Description ?? "",
                    ReadPerm = image.ReadPerm ?? "0",
                    Price = image.Price ?? "0",
                    IsAttachment = false  // ✅ 图片标记为 false
                });
                Debug.WriteLine($"📸 已添加图片附件元数据: {image.AttachmentId}");
            }
        }

        // 添加已上传的附件的元数据
        foreach (var attachment in UploadedAttachments)
        {
            if (!string.IsNullOrEmpty(attachment.AttachmentId))
            {
                metadataList.Add(new AttachmentMetadata
                {
                    AttachmentId = attachment.AttachmentId,
                    Description = attachment.Description ?? "",
                    ReadPerm = attachment.ReadPerm ?? "0",
                    Price = attachment.Price ?? "0",
                    IsAttachment = true  // ✅ 附件标记为 true
                });
                Debug.WriteLine($"📎 已添加文件附件元数据: {attachment.AttachmentId}");
            }
        }

        Debug.WriteLine($"✅ 构建附件元数据列表完成，共 {metadataList.Count} 个附件");
        return metadataList;
    }

    /// <summary>
    /// 取消回帖
    /// </summary>
    private async Task OnCancelAsync()
    {
        var result = await Application.Current!.MainPage!.DisplayAlert(
            "取消确认",
            "确定要取消回帖吗？已上传的文件将被删除。",
            "取消", "继续编辑");

        if (result)
        {
            // 删除所有已上传的文件
            foreach (var file in UploadedImages.ToList())
            {
                if (!string.IsNullOrEmpty(file.AttachmentId))
                {
                    await DeleteAttachmentFromServerAsync(file.AttachmentId);
                }
            }

            foreach (var file in UploadedAttachments.ToList())
            {
                if (!string.IsNullOrEmpty(file.AttachmentId))
                {
                    await DeleteAttachmentFromServerAsync(file.AttachmentId);
                }
            }

            // 使用 Shell 导航返回上一页
            try
            {
                await Shell.Current.GoToAsync("..");
                Debug.WriteLine($"✅ 取消回帖，返回上一页成功");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ 取消回帖返回失败: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 验证回帖内容
    /// </summary>
    private bool ValidateReply()
    {
        if (string.IsNullOrWhiteSpace(_replyContent))
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                App.Current?.MainPage?.DisplayAlert("验证失败", "请输入回帖内容", "确定");
            });
            return false;
        }

        if (_replyContent.Length < 8)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                App.Current?.MainPage?.DisplayAlert("验证失败", "回帖内容至少需要8个字符", "确定");
            });
            return false;
        }

        return true;
    }

    /// <summary>
    /// 更新提交按钮状态
    /// </summary>
    private void UpdateSubmitButtonState()
    {
        var isEmpty = string.IsNullOrWhiteSpace(_replyContent);
        var isTooShort = (_replyContent?.Length ?? 0) < 8;
        var newState = !isEmpty && !isTooShort;

        if (IsSubmitEnabled != newState)
        {
            IsSubmitEnabled = newState;
            Debug.WriteLine($"🔘 提交按钮状态更新: {newState}");
            Debug.WriteLine($"   - 内容为空: {isEmpty}");
            Debug.WriteLine($"   - 内容过短 (<8字): {isTooShort}");
            Debug.WriteLine($"   - 当前长度: {_replyContent?.Length ?? 0}");
        }
    }

    /// <summary>
    /// 获取文件大小字符串
    /// </summary>
    private async Task<string> GetFileSizeStringAsync(string filePath)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            var sizeInBytes = fileInfo.Length;
            return FormatFileSize(sizeInBytes);
        }
        catch
        {
            return "未知大小";
        }
    }

    /// <summary>
    /// 格式化文件大小
    /// </summary>
    private static string FormatFileSize(long sizeInBytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = sizeInBytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }

    /// <summary>
    /// 显示错误提示
    /// </summary>
    private async Task ShowErrorAlert(string title, string message)
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
            App.Current?.MainPage?.DisplayAlert(title, message, "确定")
        );
    }

    /// <summary>
    /// 显示成功提示
    /// </summary>
    private async Task ShowSuccessAlert(string title, string message)
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
            App.Current?.MainPage?.DisplayAlert(title, message, "确定")
        );
    }

    // INotifyPropertyChanged 实现
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}

/// <summary>
/// 已上传文件信息
/// </summary>
public class UploadedFileInfo : INotifyPropertyChanged
{
    private string _fileName = string.Empty;
    private string _fileSize = string.Empty;
    private string _localPath = string.Empty;
    private string _remoteUrl = string.Empty;
    private string _attachmentId = string.Empty;
    private bool _isUploading;
    private string _statusText = string.Empty;
    private Color _statusColor = Colors.Orange;
    // ✅ 新增：附件元数据属性
    private string _description = string.Empty;
    private string _readPerm = "";
    private string _price = "0";
    private PermissionOption? _selectedPermission;

    public string FileName
    {
        get => _fileName;
        set => SetProperty(ref _fileName, value);
    }

    public string FileSize
    {
        get => _fileSize;
        set => SetProperty(ref _fileSize, value);
    }

    public string LocalPath
    {
        get => _localPath;
        set => SetProperty(ref _localPath, value);
    }

    public string RemoteUrl
    {
        get => _remoteUrl;
        set => SetProperty(ref _remoteUrl, value);
    }

    public string AttachmentId
    {
        get => _attachmentId;
        set => SetProperty(ref _attachmentId, value);
    }

    public bool IsUploading
    {
        get => _isUploading;
        set => SetProperty(ref _isUploading, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public Color StatusColor
    {
        get => _statusColor;
        set => SetProperty(ref _statusColor, value);
    }

    // ✅ 新增：附件元数据属性
    /// <summary>
    /// 附件描述
    /// </summary>
    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    /// <summary>
    /// 阅读权限（0=所有人, 其他值表示特定权限等级）
    /// </summary>
    public string ReadPerm
    {
        get => _readPerm;
        set => SetProperty(ref _readPerm, value);
    }

    /// <summary>
    /// ✅ 新增：选中的权限选项（用于 Picker 绑定）
    /// 这是一个PermissionOption对象，包含Value和Display
    /// Picker会通过ItemDisplayBinding展示Display，通过SelectedItem获取整个对象
    /// </summary>
    public PermissionOption? SelectedPermission
    {
        get
        {
            if (_selectedPermission == null)
            {
                // 如果还没有选中，根据当前的ReadPerm值找到对应的PermissionOption
                _selectedPermission = AttachmentPermissionLevel.PermissionOptions.FirstOrDefault(
                    p => p.Value == _readPerm) ?? AttachmentPermissionLevel.PermissionOptions.FirstOrDefault();
            }
            return _selectedPermission;
        }
        set
        {
            if (value != null)
            {
                _selectedPermission = value;
                ReadPerm = value.Value;  // 同步更新ReadPerm值
            }
        }
    }

    /// <summary>
    /// ✅ 弃用：ReadPermIndex - 改用 SelectedPermission 属性
    /// 保留此方法是为了兼容性，但推荐使用 SelectedPermission
    /// </summary>
    [Obsolete("使用 SelectedPermission 代替")]
    public int ReadPermIndex
    {
        get
        {
            var permOptions = AttachmentPermissionLevel.PermissionOptions;
            var index = permOptions.FindIndex(p => p.Value == _readPerm);
            return index >= 0 ? index : 0;
        }
        set
        {
            if (value >= 0 && value < AttachmentPermissionLevel.PermissionOptions.Count)
            {
                SelectedPermission = AttachmentPermissionLevel.PermissionOptions[value];
            }
        }
    }

    /// <summary>
    /// 附件售价（金币数，0表示免费）
    /// </summary>
    public string Price
    {
        get => _price;
        set => SetProperty(ref _price, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
