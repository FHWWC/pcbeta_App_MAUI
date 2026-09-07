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
/// 编辑回帖页面 ViewModel
/// 处理回帖编辑的加载、修改、提交等业务逻辑
/// </summary>
public class EditReplyViewModel : INotifyPropertyChanged
{
    private readonly ApiService _apiService;
    private string _forumId = string.Empty;  // fid
    private string _threadId = string.Empty; // tid
    private string _postId = string.Empty;   // pid - 被编辑的回帖ID
    private string _replyContent = string.Empty;
    private string _originalContent = string.Empty;  // ✅ 新增：原始回帖内容
    private int _characterCount;
    private bool _isSubmitEnabled;
    private string _threadTitle = string.Empty;
    private string _threadInfo = string.Empty;
    private string _formhash = string.Empty;  // ✅ 新增：表单哈希
    private string _uid = string.Empty;
    private string _hash = string.Empty;
    private string _posttime = string.Empty;  // ✅ 新增：发帖时间戳
    private bool _isLoading = true;

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
            }
        }
    }

    public string OriginalContent
    {
        get => _originalContent;
        set => SetProperty(ref _originalContent, value);
    }

    public int CharacterCount
    {
        get => _characterCount;
        set => SetProperty(ref _characterCount, value);
    }

    public bool IsSubmitEnabled
    {
        get => _isSubmitEnabled;
        set => SetProperty(ref _isSubmitEnabled, value);
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

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    // 命令
    public IAsyncRelayCommand PickImageCommand { get; }
    public IAsyncRelayCommand PickAttachmentCommand { get; }
    public IAsyncRelayCommand<UploadedFileInfo> DeleteImageCommand { get; }
    public IAsyncRelayCommand<UploadedFileInfo> DeleteAttachmentCommand { get; }
    public IAsyncRelayCommand<UploadedFileInfo> InsertImageCommand { get; }
    public IAsyncRelayCommand<UploadedFileInfo> InsertAttachmentCommand { get; }
    public IAsyncRelayCommand SubmitEditCommand { get; }  // ✅ 改为编辑提交
    public IAsyncRelayCommand CancelCommand { get; }

    /// <summary>
    /// 无参构造函数 - 用于 XAML 数据绑定和 Shell 导航
    /// </summary>
    public EditReplyViewModel() : this(new ApiService(), "", "", "")
    {
    }

    public EditReplyViewModel(ApiService apiService, string forumId, string threadId, string postId)
    {
        _apiService = apiService;
        _forumId = forumId;
        _threadId = threadId;
        _postId = postId;

        UploadedImages = new ObservableCollection<UploadedFileInfo>();
        UploadedAttachments = new ObservableCollection<UploadedFileInfo>();

        // 初始化命令
        PickImageCommand = new AsyncRelayCommand(OnPickImageAsync);
        PickAttachmentCommand = new AsyncRelayCommand(OnPickAttachmentAsync);
        DeleteImageCommand = new AsyncRelayCommand<UploadedFileInfo>(OnDeleteImageAsync);
        DeleteAttachmentCommand = new AsyncRelayCommand<UploadedFileInfo>(OnDeleteAttachmentAsync);
        InsertImageCommand = new AsyncRelayCommand<UploadedFileInfo>(OnInsertImageAsync);
        InsertAttachmentCommand = new AsyncRelayCommand<UploadedFileInfo>(OnInsertAttachmentAsync);
        SubmitEditCommand = new AsyncRelayCommand(OnSubmitEditAsync);
        CancelCommand = new AsyncRelayCommand(OnCancelAsync);

        UpdateSubmitButtonState();
    }

    /// <summary>
    /// 初始化编辑页面信息
    /// </summary>
    public void Initialize(string forumId, string threadId, string postId, string threadTitle)
    {
        _forumId = forumId;
        _threadId = threadId;
        _postId = postId;
        ThreadTitle = threadTitle;
        ThreadInfo = $"编辑回帖 #{postId}";

        Debug.WriteLine($"✅ EditReplyViewModel 初始化: fid={_forumId}, tid={_threadId}, pid={_postId}");
    }

    /// <summary>
    /// 异步加载编辑页面数据
    /// 请求编辑页面URL获取原始内容和隐藏参数
    /// </summary>
    public async Task LoadEditPageDataAsync()
    {
        try
        {
            IsLoading = true;
            Debug.WriteLine($"📄 开始加载编辑页面数据: fid={_forumId}, tid={_threadId}, pid={_postId}");

            if (string.IsNullOrEmpty(_forumId) || string.IsNullOrEmpty(_threadId) || string.IsNullOrEmpty(_postId))
            {
                Debug.WriteLine("⚠️ 参数不完整，无法加载编辑页面");
                return;
            }

            // 构建编辑页面URL
            var editUrl = $"https://bbs.pcbeta.com/forum.php?mod=post&action=edit&fid={_forumId}&tid={_threadId}&pid={_postId}&inajax=1";
            Debug.WriteLine($"📝 编辑页面URL: {editUrl}");

            // 获取编辑页面HTML
            var pageHtml = await _apiService.GetPageHtmlAsync(editUrl);

            if (string.IsNullOrEmpty(pageHtml))
            {
                Debug.WriteLine("❌ 无法获取编辑页面HTML");
                await ShowErrorAlert("加载失败", "无法加载编辑页面");
                return;
            }

            // 从HTML中提取关键数据
            _formhash = ExtractFormHashFromHtml(pageHtml);
            _uid = ExtractUidFromHtml(pageHtml);
            _hash = ExtractHashFromHtml(pageHtml);
            _posttime = ExtractPostTimeFromHtml(pageHtml);
            var originalContent = ExtractMessageFromHtml(pageHtml);
            var subject = ExtractSubjectFromHtml(pageHtml);

            Debug.WriteLine($"✅ 提取的数据:");
            Debug.WriteLine($"   - formhash: {_formhash}");
            Debug.WriteLine($"   - posttime: {_posttime}");
            Debug.WriteLine($"   - 原始内容: {(string.IsNullOrEmpty(originalContent) ? "❌ 未找到" : originalContent.Substring(0, Math.Min(50, originalContent.Length)) + "...")}");

            // 设置原始内容和编辑内容
            OriginalContent = originalContent;
            ReplyContent = originalContent;

            // ✅ 新增：加载已有的附件
            await LoadExistingAttachmentsAsync(pageHtml);

            Debug.WriteLine($"✅ 编辑页面数据加载完成");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 加载编辑页面数据失败: {ex.Message}\n{ex.StackTrace}");
            await ShowErrorAlert("加载失败", $"加载编辑页面数据失败: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// ✅ 新增：从编辑页面HTML中加载已有的附件
    /// 分别加载图片附件和普通附件，并绑定到UI
    /// </summary>
    private async Task LoadExistingAttachmentsAsync(string pageHtml)
    {
        try
        {
            Debug.WriteLine("📎 开始加载已有的附件...");

            // 使用 XmlParsingService 提取附件
            var xmlParsingService = new XmlParsingService();
            var (existingImages, existingAttachments) = xmlParsingService.ExtractAttachmentsFromEditPageHtml(pageHtml);

            // 清空现有集合
            UploadedImages.Clear();
            UploadedAttachments.Clear();

            // 添加已有的图片附件
            foreach (var image in existingImages)
            {
                // 显式转换以避免命名空间冲突
                UploadedImages.Add(new UploadedFileInfo
                {
                    AttachmentId = image.AttachmentId,
                    FileName = image.FileName,
                    FileSize = image.FileSize,
                    Description = image.Description,
                    ReadPerm = image.ReadPerm,
                    Price = image.Price,
                    LocalPath = image.LocalPath,
                    RemoteUrl = image.RemoteUrl
                });
                Debug.WriteLine($"  ✅ 加载图片: {image.FileName} (ID={image.AttachmentId})");
            }

            // 添加已有的普通附件
            foreach (var attachment in existingAttachments)
            {
                // 显式转换以避免命名空间冲突
                UploadedAttachments.Add(new UploadedFileInfo
                {
                    AttachmentId = attachment.AttachmentId,
                    FileName = attachment.FileName,
                    FileSize = attachment.FileSize,
                    Description = attachment.Description,
                    ReadPerm = attachment.ReadPerm,
                    Price = attachment.Price,
                    LocalPath = attachment.LocalPath,
                    RemoteUrl = attachment.RemoteUrl
                });
                Debug.WriteLine($"  ✅ 加载附件: {attachment.FileName} (ID={attachment.AttachmentId})");
            }

            Debug.WriteLine($"✅ 附件加载完成: {existingImages.Count} 个图片, {existingAttachments.Count} 个普通附件");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 加载附件失败: {ex.Message}");
            // 不中断主流程，继续允许用户编辑
        }
    }

    /// <summary>
    /// 从HTML中提取formhash
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
            Debug.WriteLine($"❌ 提取formhash失败: {ex.Message}");
            return "";
        }
    }

    /// <summary>
    /// 从HTML中提取posttime（发帖时间戳）
    /// </summary>
    private string ExtractPostTimeFromHtml(string html)
    {
        try
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                html,
                @"<input\s+type=""hidden""\s+name=""posttime""\s+id=""posttime""\s+value=""(\d+)""",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );

            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            // 备用模式
            match = System.Text.RegularExpressions.Regex.Match(
                html,
                @"<input[^>]*name=""posttime""[^>]*value=""(\d+)""",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );

            return match.Success ? match.Groups[1].Value : "";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 提取posttime失败: {ex.Message}");
            return "";
        }
    }

    /// <summary>
    /// 从HTML中提取message（回帖内容）
    /// 格式：<textarea name="message" id="e_textarea" ...>内容</textarea>
    /// </summary>
    private string ExtractMessageFromHtml(string html)
    {
        try
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                html,
                @"<textarea\s+name=""message""[^>]*>([^<]*)</textarea>",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline
            );

            if (match.Success)
            {
                var content = match.Groups[1].Value;
                // HTML解码
                content = System.Net.WebUtility.HtmlDecode(content);
                return content.Trim();
            }

            // 备用模式：处理CDATA或其他格式
            match = System.Text.RegularExpressions.Regex.Match(
                html,
                @"<textarea[^>]*id=""e_textarea""[^>]*>([^<]*)</textarea>",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline
            );

            if (match.Success)
            {
                var content = match.Groups[1].Value;
                content = System.Net.WebUtility.HtmlDecode(content);
                return content.Trim();
            }

            Debug.WriteLine($"⚠️ 未找到message内容");
            return "";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 提取message失败: {ex.Message}");
            return "";
        }
    }

    /// <summary>
    /// 从HTML中提取subject（帖子标题）
    /// </summary>
    private string ExtractSubjectFromHtml(string html)
    {
        try
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                html,
                @"<input\s+type=""text""\s+name=""subject""[^>]*value=""([^""]*)""",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );

            if (match.Success)
            {
                var subject = match.Groups[1].Value;
                return System.Net.WebUtility.HtmlDecode(subject);
            }

            return "";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 提取subject失败: {ex.Message}");
            return "";
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
                Debug.WriteLine($"✅ 图片上传成功: {fileInfo.FileName}");
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
                { DevicePlatform.iOS, new[] { "public.image", "public.archive", "public.data" } },
                { DevicePlatform.macOS, new[] { "public.image", "public.archive", "public.data" } },
                { DevicePlatform.Android, new[] { "image/*", "application/zip", "application/x-rar-compressed", "application/x-7z-compressed", "application/x-bittorrent" } },
                { DevicePlatform.WinUI, new[] { ".gif", ".jpg", ".jpeg", ".png", ".webp", ".zip", ".rar", ".7z", ".torrent", ".sitx" } }
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
                Debug.WriteLine($"✅ 附件上传成功: {fileInfo.FileName}");
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
    /// 通用文件上传方法
    /// </summary>
    private async Task<UploadResult?> UploadFileAsync(string filePath, bool isImage, string uid, string hash)
    {
        try
        {
            var uploadUrl = isImage
                ? $"https://bbs.pcbeta.com/misc.php?mod=swfupload&action=swfupload&operation=upload&type=image"
                : $"https://bbs.pcbeta.com/misc.php?mod=swfupload&action=swfupload&operation=upload&fid={_forumId}";

            Debug.WriteLine($"📝 上传文件");
            var uploadResult = await _apiService.UploadFileAsync(uploadUrl, filePath, uid, hash,isImage, _forumId);

            if (uploadResult == null)
            {
                Debug.WriteLine($"❌ 文件上传失败");
                return null;
            }

            Debug.WriteLine($"✅ 文件上传成功，附件ID: {uploadResult.AttachmentId}");

            // 绑定附件到帖子（仅对非图片有效）
            if (!isImage && !string.IsNullOrEmpty(uploadResult.AttachmentId) && !string.IsNullOrEmpty(_forumId))
            {
                Debug.WriteLine($"📝 绑定附件到帖子");
                var isBindSuccess = await _apiService.BindAttachmentAsync(uploadResult.AttachmentId, _forumId,isImage);

                if (isBindSuccess)
                {
                    Debug.WriteLine($"✅ 附件绑定成功");
                }
                else
                {
                    Debug.WriteLine($"⚠️ 附件绑定失败（但上传成功）");
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
            if (!string.IsNullOrEmpty(fileInfo.AttachmentId))
            {
                await DeleteAttachmentFromServerAsync(fileInfo.AttachmentId);
            }

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
            if (!string.IsNullOrEmpty(fileInfo.AttachmentId))
            {
                await DeleteAttachmentFromServerAsync(fileInfo.AttachmentId);
            }

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
    /// 插入已上传的图片
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
            Debug.WriteLine($"✅ 已插入图片标签");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 插入图片标签失败: {ex.Message}");
            await ShowErrorAlert("插入失败", ex.Message);
        }
    }

    /// <summary>
    /// 插入已上传的附件
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
            Debug.WriteLine($"✅ 已插入附件标签");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 插入附件标签失败: {ex.Message}");
            await ShowErrorAlert("插入失败", ex.Message);
        }
    }

    /// <summary>
    /// 在编辑器光标位置插入文本
    /// </summary>
    private void InsertTextAtCursor(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        try
        {
            var navStack = Shell.Current?.Navigation.NavigationStack;
            if (navStack == null || navStack.Count == 0)
            {
                Debug.WriteLine("⚠️ 无法获取导航栈");
                return;
            }

            var currentPage = navStack.Last() as EditReplyPage;
            if (currentPage == null)
            {
                Debug.WriteLine("⚠️ 当前页面不是EditReplyPage");
                return;
            }

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

            var cursorPos = editor.CursorPosition;
            if (cursorPos < 0)
            {
                cursorPos = ReplyContent.Length;
                Debug.WriteLine($"⚠️ 无法获取光标位置，默认插入到末尾");
            }

            var newContent = ReplyContent.Insert(cursorPos, text);
            ReplyContent = newContent;

            editor.CursorPosition = cursorPos + text.Length;

            Debug.WriteLine($"✅ 文本已插入");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 插入文本异常: {ex.Message}");
        }
    }

    /// <summary>
    /// 从服务器删除附件
    /// </summary>
    private async Task DeleteAttachmentFromServerAsync(string attachmentId)
    {
        try
        {
            //var (formhash, _, _) = await GetAuthenticationParamsAsync();
            var deleteUrl = $"https://bbs.pcbeta.com/forum.php?mod=ajax&action=deleteattach&inajax=yes&aids[]={attachmentId}&tid={_threadId}&pid={_postId}&formhash={_formhash}";
            await _apiService.DeleteAttachmentAsync(deleteUrl);
            Debug.WriteLine($"✅ 服务器删除附件: aid={attachmentId}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"⚠️ 服务器删除附件失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取页面认证参数
    /// </summary>
    private async Task<(string formhash, string uid, string hash)> GetAuthenticationParamsAsync()
    {
        try
        {
            var pageHtml = await _apiService.GetThreadPageHtmlAsync(_threadId);

            if (string.IsNullOrEmpty(pageHtml))
            {
                Debug.WriteLine("⚠️ 无法获取回帖页面 HTML");
                return ("", "", "");
            }

            var formhash = ExtractFormHashFromHtml(pageHtml);
            var uid = ExtractUidFromHtml(pageHtml);
            var hash = ExtractHashFromHtml(pageHtml);

            Debug.WriteLine($"✅ 提取认证参数");
            return (formhash, uid, hash);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"⚠️ 获取认证参数失败: {ex.Message}");
            return ("", "", "");
        }
    }

    private string ExtractUidFromHtml(string html)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            html,
            @"uid[""']?\s*:\s*[""']?(\d+)[""']?",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );
        return match.Success ? match.Groups[1].Value : "";
    }

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
    /// 提交编辑 - ✅ 关键方法：发送编辑请求
    /// </summary>
    private async Task OnSubmitEditAsync()
    {
        Debug.WriteLine($"🚀 OnSubmitEditAsync 被调用");
        try
        {
            if (!ValidateReply())
            {
                Debug.WriteLine($"⚠️ 回帖验证失败");
                return;
            }

            if (string.IsNullOrEmpty(_formhash) || string.IsNullOrEmpty(_posttime))
            {
                await ShowErrorAlert("错误", "无法获取必要的编辑参数，请重新加载页面");
                return;
            }

            Debug.WriteLine($"📝 提交编辑请求:");
            Debug.WriteLine($"   - fid: {_forumId}");
            Debug.WriteLine($"   - tid: {_threadId}");
            Debug.WriteLine($"   - pid: {_postId}");
            Debug.WriteLine($"   - formhash: {_formhash}");
            Debug.WriteLine($"   - posttime: {_posttime}");

            // ✅ 构建附件元数据列表
            var attachmentMetadataList = BuildAttachmentMetadataList();

            // ✅ 调用API提交编辑
            ApiResultModel result = await _apiService.SubmitEditReplyAsync(
                _forumId,
                _threadId,
                _postId,
                _replyContent,
                _formhash,
                _posttime,
                attachmentMetadataList
            );

            if (result.IsSuccess)
            {
                Debug.WriteLine($"✅ 回帖编辑成功");
                await ShowSuccessAlert("更新成功", "回帖已更新");

                // 返回上一页并刷新
                try
                {
                    await Shell.Current.GoToAsync("..");
                    Debug.WriteLine($"✅ 返回上一页");

                    /*
                                         // 等待页面初始化
                                        await Task.Delay(1000);

                                        // 尝试刷新ThreadContentPage
                                        if (Shell.Current.Navigation?.NavigationStack.Count > 0)
                                        {
                                            var lastPage = Shell.Current.Navigation.NavigationStack.LastOrDefault();
                                            if (lastPage is ThreadContentPage tcPage)
                                            {
                                                if (tcPage.BindingContext is ThreadContentViewModel tvm)
                                                {
                                                    Debug.WriteLine($"🔄 调用刷新");
                                                    await tvm.LoadThreadContentAsync();
                                                    Debug.WriteLine($"✅ 刷新完成");
                                                }
                                            }
                                        }
                     */
                }
                catch (Exception navEx)
                {
                    Debug.WriteLine($"❌ 导航和刷新错误: {navEx.Message}");
                }
            }
            else
            {
                Debug.WriteLine($"❌ 回帖编辑失败");
                await ShowErrorAlert("编辑失败", result.Message);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 提交编辑错误: {ex.Message}\n{ex.StackTrace}");
            await ShowErrorAlert("提交错误", ex.Message);
        }
    }

    /// <summary>
    /// 构建附件元数据列表
    /// ✅ 改进：根据文件类型设置 IsAttachment 标志
    /// </summary>
    private List<AttachmentMetadata> BuildAttachmentMetadataList()
    {
        var metadataList = new List<AttachmentMetadata>();

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
            }
        }

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
            }
        }

        Debug.WriteLine($"✅ 构建附件元数据列表: {metadataList.Count} 个附件");
        return metadataList;
    }

    /// <summary>
    /// 取消编辑
    /// </summary>
    private async Task OnCancelAsync()
    {
        var result = await Application.Current!.MainPage!.DisplayAlert(
            "取消确认",
            "确定要取消编辑吗？",
            "取消", "继续编辑");

        if (result)
        {
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

            try
            {
                await Shell.Current.GoToAsync("..");
                Debug.WriteLine($"✅ 取消编辑，返回上一页成功");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ 返回失败: {ex.Message}");
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
        IsSubmitEnabled = !string.IsNullOrWhiteSpace(_replyContent) && _replyContent.Length >= 8;
    }

    /// <summary>
    /// 获取文件大小字符串
    /// </summary>
    private async Task<string> GetFileSizeStringAsync(string filePath)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            return FormatFileSize(fileInfo.Length);
        }
        catch
        {
            return "未知大小";
        }
    }

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
