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
using static System.Net.WebRequestMethods;

namespace PCBetaMAUI.ViewModels;

/// <summary>
/// 发新帖页面 ViewModel
/// 处理新帖编辑、文件上传、删除等业务逻辑
/// 基于 ReplyThreadViewModel，但针对发新帖场景进行了优化
/// </summary>
public class PostNewThreadViewModel : INotifyPropertyChanged
{
    private readonly ApiService _apiService;
    private string _boardId;  // fid
    private string _boardName;
    private string _postContent = string.Empty;
    private string _threadTitle = string.Empty;
    private int _characterCount;
    private int _threadTitleCharCount;
    private bool _isSubmitEnabled;

    // ✅ 新增：主题分类参数
    private string _selectedTypeId = "0";
    private List<(string id, string name)> _typeIdOptions = new();
    // ✅ 新增：阅读权限选项列表
    private List<(string id, string name)> _readpermOptions = new();

    // ✅ 新增：额外选项参数
    private string _replycreditTimes = "1";
    private string _replycreditExtcredits = "0";
    private string _replycreditMembertimes = "1";
    private string _replycreditRandom = "100";
    private string _readperm = "";  // 阅读权限
    private string _price = "";  // 售价
    private bool _hiddenreplies = false;  // 回帖仅作者可见
    private bool _ordertype = false;  // 回帖倒序排列
    private bool _allownoticeauthor = true;  // 接收回复通知
    private bool _usesig = true;  // 使用个人签名

    public ObservableCollection<UploadedFileInfo> UploadedImages { get; }
    public ObservableCollection<UploadedFileInfo> UploadedAttachments { get; }

    // 绑定属性
    public string PostContent
    {
        get => _postContent;
        set
        {
            if (SetProperty(ref _postContent, value))
            {
                CharacterCount = value?.Length ?? 0;
                UpdateSubmitButtonState();
            }
        }
    }

    public string ThreadTitle
    {
        get => _threadTitle;
        set
        {
            if (SetProperty(ref _threadTitle, value))
            {
                ThreadTitleCharCount = value?.Length ?? 0;
                UpdateSubmitButtonState();
            }
        }
    }

    public int CharacterCount
    {
        get => _characterCount;
        set => SetProperty(ref _characterCount, value);
    }

    public int ThreadTitleCharCount
    {
        get => _threadTitleCharCount;
        set => SetProperty(ref _threadTitleCharCount, value);
    }

    public bool IsSubmitEnabled
    {
        get => _isSubmitEnabled;
        set => SetProperty(ref _isSubmitEnabled, value);
    }

    public string BoardName
    {
        get => _boardName;
        set => SetProperty(ref _boardName, value);
    }

    // ✅ 新增：额外选项属性（10个参数）
    public string ReplycreditTimes
    {
        get => _replycreditTimes;
        set => SetProperty(ref _replycreditTimes, value);
    }

    public string ReplycreditExtcredits
    {
        get => _replycreditExtcredits;
        set => SetProperty(ref _replycreditExtcredits, value);
    }

    public string ReplycreditMembertimes
    {
        get => _replycreditMembertimes;
        set => SetProperty(ref _replycreditMembertimes, value);
    }

    public string ReplycreditRandom
    {
        get => _replycreditRandom;
        set => SetProperty(ref _replycreditRandom, value);
    }

    public string Readperm
    {
        get => _readperm;
        set => SetProperty(ref _readperm, value);
    }

    public string Price
    {
        get => _price;
        set => SetProperty(ref _price, value);
    }

    public bool Hiddenreplies
    {
        get => _hiddenreplies;
        set => SetProperty(ref _hiddenreplies, value);
    }

    public bool Ordertype
    {
        get => _ordertype;
        set => SetProperty(ref _ordertype, value);
    }

    public bool Allownoticeauthor
    {
        get => _allownoticeauthor;
        set => SetProperty(ref _allownoticeauthor, value);
    }

    public bool Usesig
    {
        get => _usesig;
        set => SetProperty(ref _usesig, value);
    }

    // ✅ 新增：主题分类（typeid）相关属性
    public string SelectedTypeId
    {
        get => _selectedTypeId;
        set => SetProperty(ref _selectedTypeId, value);
    }

    public List<(string id, string name)> TypeIdOptions
    {
        get => _typeIdOptions;
        set => SetProperty(ref _typeIdOptions, value);
    }

    // ✅ 新增：阅读权限相关属性
    public List<(string id, string name)> ReadpermOptions
    {
        get => _readpermOptions;
        set => SetProperty(ref _readpermOptions, value);
    }

    // 命令
    public IAsyncRelayCommand PickImageCommand { get; }
    public IAsyncRelayCommand PickAttachmentCommand { get; }
    public IAsyncRelayCommand<UploadedFileInfo> DeleteImageCommand { get; }
    public IAsyncRelayCommand<UploadedFileInfo> DeleteAttachmentCommand { get; }
    public IAsyncRelayCommand<UploadedFileInfo> InsertImageCommand { get; }
    public IAsyncRelayCommand<UploadedFileInfo> InsertAttachmentCommand { get; }
    public IAsyncRelayCommand SubmitPostCommand { get; }
    public IAsyncRelayCommand CancelCommand { get; }

    /// <summary>
    /// 无参构造函数 - 用于 XAML 数据绑定和 Shell 导航
    /// </summary>
    public PostNewThreadViewModel() : this(new ApiService(), "", "")
    {
    }

    public PostNewThreadViewModel(ApiService apiService, string boardId, string boardName)
    {
        _apiService = apiService;
        _boardId = boardId;
        _boardName = boardName;

        UploadedImages = new ObservableCollection<UploadedFileInfo>();
        UploadedAttachments = new ObservableCollection<UploadedFileInfo>();

        // 初始化命令
        PickImageCommand = new AsyncRelayCommand(OnPickImageAsync);
        PickAttachmentCommand = new AsyncRelayCommand(OnPickAttachmentAsync);
        DeleteImageCommand = new AsyncRelayCommand<UploadedFileInfo>(OnDeleteImageAsync);
        DeleteAttachmentCommand = new AsyncRelayCommand<UploadedFileInfo>(OnDeleteAttachmentAsync);
        InsertImageCommand = new AsyncRelayCommand<UploadedFileInfo>(OnInsertImageAsync);
        InsertAttachmentCommand = new AsyncRelayCommand<UploadedFileInfo>(OnInsertAttachmentAsync);
        SubmitPostCommand = new AsyncRelayCommand(OnSubmitPostAsync);
        CancelCommand = new AsyncRelayCommand(OnCancelAsync);

        UpdateSubmitButtonState();
    }

    /// <summary>
    /// 初始化发帖页面的所有必要信息
    /// 包括 boardId 和版块名称
    /// </summary>
    public void Initialize(string boardId, string boardName)
    {
        _boardId = boardId;
        _boardName = boardName;
        BoardName = boardName;

        Debug.WriteLine($"✅ PostNewThreadViewModel 初始化完成: fid={_boardId}, boardName={BoardName}");
    }

    /// <summary>
    /// 异步初始化发帖页面
    /// 关键：发送 GET 请求到发帖编辑页面，以建立正确的 session
    /// 并获取和解析所有表单参数（typeid、readperm、回帖奖励等）
    /// 此方法应在页面加载时调用，与 GetAuthenticationParamsAsync 分离
    /// </summary>
    public async Task InitializePostPageAsync(string boardId)
    {
        try
        {
            if (string.IsNullOrEmpty(boardId))
            {
                Debug.WriteLine("⚠️ BoardId 为空，跳过发帖页面初始化");
                return;
            }

            Debug.WriteLine($"📄 开始初始化发帖页面: fid={boardId}");
            _boardId = boardId;

            // 获取发帖编辑页面的 HTML
            var pageUrl = $"https://bbs.pcbeta.com/forum.php?mod=post&action=newthread&fid={boardId}&inajax=1";
            var response = await HttpClientManager.Instance.GetAsync(pageUrl);
            response.EnsureSuccessStatusCode();

            var pageHtml = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrEmpty(pageHtml))
            {
                Debug.WriteLine("⚠️ 无法获取发帖页面 HTML");
                return;
            }

            Debug.WriteLine($"✅ 成功获取发帖页面 HTML (大小: {pageHtml.Length} 字节)");

            // 解析 typeid 选项 - 发帖页面初始化时获取
            ParseTypeIdOptionsFromHtml(pageHtml);

            // 解析回帖奖励和其他表单参数 - 发帖页面初始化时获取
            ParseReplyRewardParametersFromHtml(pageHtml);

            Debug.WriteLine($"✅ 发帖页面初始化完成");
            Debug.WriteLine($"   - typeid 选项已加载: {TypeIdOptions.Count} 项");
            Debug.WriteLine($"   - 阅读权限选项已加载: {ReadpermOptions.Count} 项");
            Debug.WriteLine($"   - 这确保了后续的附件上传和发帖提交能被正确关联");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 发帖页面初始化失败: {ex.Message}\n{ex.StackTrace}");
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
            var (formhash, uid, hash) = await GetAuthenticationParamsAsync();

            // 上传文件
            var uploadResult = await UploadFileAsync(fileResult.FullPath, isImage: true, uid, hash);

            if (uploadResult.HasValue)
            {
                fileInfo.RemoteUrl = uploadResult.Value.Url ?? "";
                fileInfo.AttachmentId = uploadResult.Value.AttachmentId ?? "";
                fileInfo.IsUploading = false;
                fileInfo.StatusText = "上传成功";
                fileInfo.StatusColor = Colors.Green;
                Debug.WriteLine($"✅ 图片上传成功: {fileInfo.FileName} (aid={fileInfo.AttachmentId})");
            }
            else
            {
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


            if (result != null)
            {
                await UploadAttachmentAsync(result);
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
            var (formhash, uid, hash) = await GetAuthenticationParamsAsync();

            // 上传文件
            var uploadResult = await UploadFileAsync(fileResult.FullPath, isImage: false, uid, hash);

            if (uploadResult.HasValue)
            {
                fileInfo.RemoteUrl = uploadResult.Value.Url ?? "";
                fileInfo.AttachmentId = uploadResult.Value.AttachmentId ?? "";
                fileInfo.IsUploading = false;
                fileInfo.StatusText = "上传成功";
                fileInfo.StatusColor = Colors.Green;
                Debug.WriteLine($"✅ 附件上传成功: {fileInfo.FileName} (aid={fileInfo.AttachmentId})");
            }
            else
            {
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
    /// 删除图片
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
    /// 删除附件
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
    /// 插入图片
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
    /// 插入附件
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
    /// 提交新帖
    /// </summary>
    private async Task OnSubmitPostAsync()
    {
        Debug.WriteLine($"🚀 OnSubmitPostAsync 被调用");
        try
        {
            // 验证输入
            if (string.IsNullOrEmpty(ThreadTitle) || ThreadTitle.Length < 2)
            {
                await ShowErrorAlert("提示", "请输入至少2个字符的帖子标题");
                Debug.WriteLine($"⚠️ 帖子标题验证失败");
                return;
            }

            if (string.IsNullOrEmpty(PostContent) || PostContent.Length < 8)
            {
                await ShowErrorAlert("提示", "请输入至少8个字符的帖子内容");
                Debug.WriteLine($"⚠️ 帖子内容验证失败");
                return;
            }

            // ✅ 新增：验证主题分类（typeid）
            if (SelectedTypeId == "0")
            {
                await ShowErrorAlert("提示", "请选择主题分类");
                Debug.WriteLine($"⚠️ 用户未选择主题分类");
                return;
            }

            Debug.WriteLine($"🚀 开始提交新帖: title={ThreadTitle.Substring(0, Math.Min(20, ThreadTitle.Length))}..., content={PostContent.Substring(0, Math.Min(30, PostContent.Length))}...");

            IsSubmitEnabled = false;

            // 获取认证参数
            var (formhash, uid, hash) = await GetAuthenticationParamsAsync();

            if (string.IsNullOrEmpty(formhash))
            {
                await ShowErrorAlert("错误", "无法获取必要的认证参数，请重试");
                IsSubmitEnabled = true;
                return;
            }

            Debug.WriteLine($"✅ 获取到认证参数: formhash={formhash.Substring(0, Math.Min(8, formhash.Length))}...");

            // 构建附件元数据列表
            var attachmentMetadataList = BuildAttachmentMetadataList();

            // 调用 API 发帖
            Debug.WriteLine($"📝 开始调用发帖API");
            ApiResultModel result = await _apiService.PostNewThreadAsync(
                _boardId,
                ThreadTitle,
                PostContent,
                formhash,
                attachmentMetadataList,
                // ✅ 新增：10个额外参数
                ReplycreditTimes,
                ReplycreditExtcredits,
                ReplycreditMembertimes,
                ReplycreditRandom,
                Readperm,
                Price,
                Hiddenreplies,
                Ordertype,
                Allownoticeauthor,
                Usesig,
                // ✅ 新增：typeid（主题分类）
                SelectedTypeId
            );

            if (result.IsSuccess)
            {
                Debug.WriteLine($"✅ 新帖发布成功");
                await ShowSuccessAlert("成功", "帖子已成功发布！");
                Debug.WriteLine($"📝 即将返回上一页");

                // 导航返回
                await GoBackAsync();

                // 等待导航完成
                await Task.Delay(1000);

                // 尝试刷新前一个页面（ThreadListPage）
                try
                {
                    // 从导航栈获取当前页面
                    var navStack = Shell.Current?.Navigation.NavigationStack;
                    if (navStack != null && navStack.Count > 0)
                    {
                        var currentPage = navStack.LastOrDefault();

                        // 尝试获取 ViewModel 并刷新
                        if (currentPage?.BindingContext is ThreadListViewModel tlvm)
                        {
                            Debug.WriteLine($"🔄 调用 ThreadListViewModel.LoadThreadsAsync() 刷新页面");
                            await tlvm.LoadThreadsAsync();
                            Debug.WriteLine($"✅ 刷新完成");
                        }
                    }
                }
                catch (Exception refreshEx)
                {
                    Debug.WriteLine($"⚠️ 刷新前一个页面失败: {refreshEx.Message}");
                    // 刷新失败不影响用户体验，只记录日志
                }
            }
            else
            {
                Debug.WriteLine($"❌ 新帖发布失败");
                await ShowErrorAlert("失败", result.Message);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 提交新帖错误: {ex.Message}\n{ex.StackTrace}");
            await ShowErrorAlert("发布失败", ex.Message);
        }
        finally
        {
            IsSubmitEnabled = true;
        }
    }

    /// <summary>
    /// 取消
    /// </summary>
    private async Task OnCancelAsync()
    {
        Debug.WriteLine($"📝 取消发帖");
        await GoBackAsync();
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
            var currentPage = navStack.Last() as PostNewThreadPage;
            if (currentPage == null)
            {
                Debug.WriteLine("⚠️ 当前页面不是PostNewThreadPage");
                return;
            }

            // 通过反射获取Editor控件
            var postContentEditorField = currentPage.GetType().GetField("PostContentEditor", 
                System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (postContentEditorField == null)
            {
                Debug.WriteLine("⚠️ 未找到PostContentEditor字段");
                return;
            }

            var editor = postContentEditorField.GetValue(currentPage) as Editor;
            if (editor == null)
            {
                Debug.WriteLine("⚠️ PostContentEditor为null");
                return;
            }

            // 获取当前光标位置
            var cursorPos = editor.CursorPosition;
            if (cursorPos < 0)
            {
                // 如果获取失败，默认插入到末尾
                cursorPos = PostContent.Length;
                Debug.WriteLine($"⚠️ 无法获取光标位置，默认插入到末尾 (pos={cursorPos})");
            }

            // 在光标位置插入文本
            var newContent = PostContent.Insert(cursorPos, text);
            PostContent = newContent;

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
            var (formhash, _, _) = await GetAuthenticationParamsAsync();

            var deleteUrl = $"https://bbs.pcbeta.com/forum.php?mod=ajax&action=deleteattach&inajax=yes&aids[]={attachmentId}&fid={_boardId}&formhash={formhash}";

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
    /// 更新提交按钮状态
    /// </summary>
    private void UpdateSubmitButtonState()
    {
        IsSubmitEnabled = !string.IsNullOrEmpty(ThreadTitle) && ThreadTitle.Length >= 2 &&
                         !string.IsNullOrEmpty(PostContent) && PostContent.Length >= 8;
    }

    /// <summary>
    /// 获取认证参数（formhash、uid、hash）
    /// 这些参数需要从发帖页面的 HTML 中动态提取
    /// </summary>
    private async Task<(string formhash, string uid, string hash)> GetAuthenticationParamsAsync()
    {
        try
        {
            // 获取发帖页面的 HTML
            var pageUrl = $"https://bbs.pcbeta.com/forum.php?mod=post&action=newthread&fid={_boardId}&inajax=1";
            var response = await HttpClientManager.Instance.GetAsync(pageUrl);
            response.EnsureSuccessStatusCode();

            var pageHtml = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrEmpty(pageHtml))
            {
                Debug.WriteLine("⚠️ 无法获取发帖页面 HTML");
                return ("", "", "");
            }

            // 从 HTML 中提取参数
            var formhash = ExtractFormHashFromHtml(pageHtml);
            var uid = ExtractUidFromHtml(pageHtml);
            var hash = ExtractHashFromHtml(pageHtml);

            Debug.WriteLine($"✅ 提取认证参数: formhash={formhash}, uid={uid}, hash={hash.Substring(0, Math.Min(8, hash.Length))}...");

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
    /// 格式：<input type="hidden" name="formhash" id="formhash" value="46b6c5de" />
    /// </summary>
    private string ExtractFormHashFromHtml(string html)
    {
        try
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
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 提取 formhash 失败: {ex.Message}");
            return "";
        }
    }

    /// <summary>
    /// 从 HTML 中提取 uid
    /// 格式：uploadformdata:{uid:"4819662", ...}
    /// </summary>
    private string ExtractUidFromHtml(string html)
    {
        try
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                html,
                @"uid[""']?\s*:\s*[""']?(\d+)[""']?",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );

            return match.Success ? match.Groups[1].Value : "";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 提取 uid 失败: {ex.Message}");
            return "";
        }
    }

    /// <summary>
    /// 从 HTML 中提取 hash
    /// 格式：uploadformdata:{..., hash:"eed3eb9354f87f458b4d6d838624370e"}
    /// </summary>
    private string ExtractHashFromHtml(string html)
    {
        try
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                html,
                @"hash[""']?\s*:\s*[""']([a-f0-9]{32})[""']",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );

            return match.Success ? match.Groups[1].Value : "";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 提取 hash 失败: {ex.Message}");
            return "";
        }
    }

    /// <summary>
    /// ✅ 新增：从 HTML 中提取 typeid 选项
    /// 格式：<select name="typeid" id="typeid">...
    ///       <option value="58">纯净水</option>
    ///       <option value="59">矿泉水</option>
    ///       ...</select>
    /// </summary>
    private void ParseTypeIdOptionsFromHtml(string html)
    {
        try
        {
            var options = new List<(string id, string name)>();

            // 第一步：提取 <select name="typeid">...</select> 块
            var selectPattern = @"<select[^>]*name=""typeid""[^>]*>(.*?)</select>";
            var selectMatch = System.Text.RegularExpressions.Regex.Match(
                html,
                selectPattern,
                System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );

            if (selectMatch.Success)
            {
                var optionsBlock = selectMatch.Groups[1].Value;
                Debug.WriteLine($"📋 找到 typeid select 块");

                // 第二步：从块中提取所有 <option> 标签
                var optionPattern = @"<option[^>]*value=""([^""]+)""[^>]*>([^<]+)</option>";
                var optionMatches = System.Text.RegularExpressions.Regex.Matches(
                    optionsBlock,
                    optionPattern,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase
                );

                foreach (System.Text.RegularExpressions.Match match in optionMatches)
                {
                    var id = match.Groups[1].Value;
                    var name = match.Groups[2].Value.Trim();
                    options.Add((id, name));
                    Debug.WriteLine($"✅ 提取 typeid 选项: id={id}, name={name}");
                }

                Debug.WriteLine($"✅ 共提取 {options.Count} 个 typeid 选项");
            }
            else
            {
                Debug.WriteLine($"⚠️ 未找到 typeid select 块");
            }

            // 更新 TypeIdOptions
            TypeIdOptions = options;

            // 重置选择为默认值 "0"
            SelectedTypeId = "0";
            Debug.WriteLine($"📋 typeid 选项已更新，当前选择: {SelectedTypeId}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 解析 typeid 选项失败: {ex.Message}");
            TypeIdOptions = new();
        }
    }

    /// <summary>
    /// ✅ 新增：从 HTML 中提取回帖奖励和其他表单参数
    /// 参数列表（共10个）：
    /// 1. replycredit_times (文本框) - 共奖励 *次
    /// 2. replycredit_extcredits (文本框) - 单次回帖奖励: * PB币
    /// 3. replycredit_membertimes (下拉框) - 每人最多可获得 * 次（1-10）
    /// 4. replycredit_random (下拉框) - 中奖率 *%（100、90、80、70、60、50、40、30、20、10）
    /// 5. readperm (下拉框) - 阅读权限
    /// 6. price (文本框) - 主题售价（最高值5）
    /// 7. hiddenreplies (复选框) - 回帖仅作者可见
    /// 8. ordertype (复选框) - 回帖倒序排列
    /// 9. allownoticeauthor (复选框) - 接收回复通知
    /// 10. usesig (复选框) - 使用个人签名
    /// 
    /// HTML位置：<div id="extra_replycredit_c" class="exfm cl" style="display: block;">
    /// </summary>
    private void ParseReplyRewardParametersFromHtml(string html)
    {
        try
        {
            Debug.WriteLine($"📋 开始解析回帖奖励和表单参数");

            // 1. 提取 replycredit_times (文本框默认值)
            var match = System.Text.RegularExpressions.Regex.Match(
                html,
                @"name=""replycredit_times""[^>]*value=""([^""]+)""",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
            if (match.Success)
            {
                ReplycreditTimes = match.Groups[1].Value;
                Debug.WriteLine($"✅ 提取 replycredit_times: {ReplycreditTimes}");
            }

            // 2. 提取 replycredit_extcredits (文本框默认值)
            match = System.Text.RegularExpressions.Regex.Match(
                html,
                @"name=""replycredit_extcredits""[^>]*value=""([^""]*)""",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
            if (match.Success)
            {
                ReplycreditExtcredits = match.Groups[1].Value;
                Debug.WriteLine($"✅ 提取 replycredit_extcredits: {ReplycreditExtcredits}");
            }

            // 3. 提取 replycredit_membertimes 下拉框（默认值1）
            match = System.Text.RegularExpressions.Regex.Match(
                html,
                @"name=""replycredit_membertimes""[^>]*>.*?<option[^>]*value=""([^""]+)""[^>]*selected",
                System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
            if (match.Success)
            {
                ReplycreditMembertimes = match.Groups[1].Value;
            }
            else
            {
                ReplycreditMembertimes = "1";  // 默认值
            }
            Debug.WriteLine($"✅ 提取 replycredit_membertimes: {ReplycreditMembertimes}");

            // 4. 提取 replycredit_random 下拉框（默认值100）
            match = System.Text.RegularExpressions.Regex.Match(
                html,
                @"name=""replycredit_random""[^>]*>.*?<option[^>]*value=""([^""]+)""[^>]*selected",
                System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
            if (match.Success)
            {
                ReplycreditRandom = match.Groups[1].Value;
            }
            else
            {
                ReplycreditRandom = "100";  // 默认值
            }
            Debug.WriteLine($"✅ 提取 replycredit_random: {ReplycreditRandom}");

            // 5. 提取 readperm 下拉框（阅读权限，需要提取所有选项）
            try
            {
                var readpermOptions = new List<(string id, string name)>();

                // 第一步：提取 <select name="readperm">...</select> 块
                var readpermSelectPattern = @"<select[^>]*name=""readperm""[^>]*>(.*?)</select>";
                var readpermSelectMatch = System.Text.RegularExpressions.Regex.Match(
                    html,
                    readpermSelectPattern,
                    System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase
                );

                if (readpermSelectMatch.Success)
                {
                    var readpermOptionsBlock = readpermSelectMatch.Groups[1].Value;
                    Debug.WriteLine($"📋 找到 readperm select 块");

                    // 第二步：从块中提取所有 <option> 标签（包括空值选项）
                    var readpermOptionPattern = @"<option[^>]*value=""([^""]*)""[^>]*>([^<]+)</option>";
                    var readpermOptionMatches = System.Text.RegularExpressions.Regex.Matches(
                        readpermOptionsBlock,
                        readpermOptionPattern,
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase
                    );

                    foreach (System.Text.RegularExpressions.Match readpermMatch in readpermOptionMatches)
                    {
                        var id = readpermMatch.Groups[1].Value;
                        var name = readpermMatch.Groups[2].Value.Trim();
                        readpermOptions.Add((id, name));
                        Debug.WriteLine($"✅ 提取 readperm 选项: id='{id}', name='{name}'");
                    }

                    Debug.WriteLine($"✅ 共提取 {readpermOptions.Count} 个 readperm 选项");

                    // 提取当前选中值
                    var readpermMatch2 = System.Text.RegularExpressions.Regex.Match(
                        html,
                        @"name=""readperm""[^>]*>.*?<option[^>]*value=""([^""]*)""[^>]*selected",
                        System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase
                    );
                    if (readpermMatch2.Success)
                    {
                        Readperm = readpermMatch2.Groups[1].Value;
                    }
                    else
                    {
                        Readperm = "";  // 默认空值
                    }
                }
                else
                {
                    Debug.WriteLine($"⚠️ 未找到 readperm select 块");
                    Readperm = "";  // 默认空值
                }

                // 更新 ReadpermOptions
                ReadpermOptions = readpermOptions;
                Debug.WriteLine($"✅ 提取 readperm 当前值: '{Readperm}'");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ 提取 readperm 选项失败: {ex.Message}");
                Readperm = "";
                ReadpermOptions = new();
            }

            // 6. 提取 price (文本框默认值，通常为空)
            match = System.Text.RegularExpressions.Regex.Match(
                html,
                @"name=""price""[^>]*value=""([^""]*)""",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
            if (match.Success)
            {
                Price = match.Groups[1].Value;
            }
            else
            {
                Price = "";  // 默认空值
            }
            Debug.WriteLine($"✅ 提取 price: '{Price}'");

            // 7. 提取复选框状态 - hiddenreplies (回帖仅作者可见)
            match = System.Text.RegularExpressions.Regex.Match(
                html,
                @"name=""hiddenreplies""[^>]*(?:checked)?",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
            Hiddenreplies = match.Success && match.Value.Contains("checked");
            Debug.WriteLine($"✅ 提取 hiddenreplies: {Hiddenreplies}");

            // 8. 提取复选框状态 - ordertype (回帖倒序排列)
            match = System.Text.RegularExpressions.Regex.Match(
                html,
                @"name=""ordertype""[^>]*(?:checked)?",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
            Ordertype = match.Success && match.Value.Contains("checked");
            Debug.WriteLine($"✅ 提取 ordertype: {Ordertype}");

            // 9. 提取复选框状态 - allownoticeauthor (接收回复通知) - 通常默认选中
            match = System.Text.RegularExpressions.Regex.Match(
                html,
                @"name=""allownoticeauthor""[^>]*(?:checked)?",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
            Allownoticeauthor = !match.Success || match.Value.Contains("checked");  // 默认true
            Debug.WriteLine($"✅ 提取 allownoticeauthor: {Allownoticeauthor}");

            // 10. 提取复选框状态 - usesig (使用个人签名) - 通常默认选中
            match = System.Text.RegularExpressions.Regex.Match(
                html,
                @"name=""usesig""[^>]*(?:checked)?",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
            Usesig = !match.Success || match.Value.Contains("checked");  // 默认true
            Debug.WriteLine($"✅ 提取 usesig: {Usesig}");

            Debug.WriteLine($"📋 回帖奖励和表单参数解析完成");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 解析回帖奖励参数失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 上传文件
    /// </summary>
    private async Task<(string? Url, string? AttachmentId)?> UploadFileAsync(string filePath, bool isImage, string uid, string hash)
    {
        try
        {
            // 步骤1：构建上传URL
            var uploadUrl = isImage
                ? $"https://bbs.pcbeta.com/misc.php?mod=swfupload&action=swfupload&operation=upload&type=image"
                : $"https://bbs.pcbeta.com/misc.php?mod=swfupload&action=swfupload&operation=upload&fid={_boardId}";

            Debug.WriteLine($"📝 步骤1: 上传文件");
            var uploadResult = await _apiService.UploadFileAsync(uploadUrl, filePath, uid, hash,isImage, _boardId);

            if (uploadResult == null)
            {
                Debug.WriteLine($"❌ 文件上传失败");
                return null;
            }

            Debug.WriteLine($"✅ 文件上传成功，附件ID: {uploadResult.AttachmentId}");

            // 步骤2：绑定附件到帖子（仅对非图片有效，图片自动绑定）
            if (!isImage && !string.IsNullOrEmpty(uploadResult.AttachmentId) && !string.IsNullOrEmpty(_boardId))
            {
                Debug.WriteLine($"📝 步骤2: 绑定附件到版块");
                var isBindSuccess = await _apiService.BindAttachmentAsync(uploadResult.AttachmentId, _boardId,isImage);

                if (isBindSuccess)
                {
                    Debug.WriteLine($"✅ 附件绑定成功");
                }
                else
                {
                    Debug.WriteLine($"⚠️ 附件绑定失败（但上传成功）");
                }
            }

            return (uploadResult.Url, uploadResult.AttachmentId);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 文件上传请求错误: {ex.Message}");
            return null;
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
    /// 获取文件大小字符串
    /// </summary>
    private async Task<string> GetFileSizeStringAsync(string filePath)
    {
        try
        {
            var info = new FileInfo(filePath);
            var size = info.Length;

            if (size < 1024)
                return $"{size} B";
            else if (size < 1024 * 1024)
                return $"{size / 1024.0:F2} KB";
            else
                return $"{size / (1024.0 * 1024):F2} MB";
        }
        catch
        {
            return "未知";
        }
    }

    /// <summary>
    /// 返回上一页
    /// </summary>
    private async Task GoBackAsync()
    {
        try
        {
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 导航返回错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 显示错误提示
    /// </summary>
    private async Task ShowErrorAlert(string title, string message)
    {
        await Application.Current?.MainPage?.DisplayAlert(title, message, "确定");
    }

    /// <summary>
    /// 显示成功提示
    /// </summary>
    private async Task ShowSuccessAlert(string title, string message)
    {
        await Application.Current?.MainPage?.DisplayAlert(title, message, "确定");
    }

    // INotifyPropertyChanged 实现
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(backingStore, value))
            return false;

        backingStore = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
