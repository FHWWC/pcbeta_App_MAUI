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
/// 编辑楼主发帖页面 ViewModel
/// 处理楼主（原始发帖人）的帖子编辑业务逻辑
/// 使用传统的 INotifyPropertyChanged 而不是 ObservableObject
/// </summary>
public class EditOPReplyViewModel : INotifyPropertyChanged
{
    private readonly ApiService _apiService;
    
    // 基本参数
    private string _boardId = string.Empty;
    private string _threadId = string.Empty;
    private string _postId = string.Empty;
    private string _boardName = string.Empty;
    private string _threadTitle = string.Empty;
    private string _postContent = string.Empty;
    private string _originalContent = string.Empty;
    private int _characterCount;
    private int _threadTitleCharCount;
    private bool _isSubmitEnabled;
    private bool _isLoading = true;

    // 编辑特有参数
    private string _formhash = string.Empty;
    private string _posttime = string.Empty;
    private string _typeId = "0";

    // ✅ 主题分类选项
    private List<(string id, string name)> _typeIdOptions = new();
    private List<(string id, string name)> _readpermOptions = new();

    // ✅ 额外选项参数（10个）
    private string _selectedTypeId = "0";
    private string _replycreditTimes = "1";
    private string _replycreditExtcredits = "0";
    private string _replycreditMembertimes = "1";
    private string _replycreditRandom = "100";
    private string _readperm = "";
    private string _price = "";
    private bool _hiddenreplies = false;
    private bool _ordertype = false;
    private bool _allownoticeauthor = true;
    private bool _usesig = true;

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

    public string BoardName
    {
        get => _boardName;
        set => SetProperty(ref _boardName, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string BoardId
    {
        get => _boardId;
        set => SetProperty(ref _boardId, value);
    }

    public string ThreadId
    {
        get => _threadId;
        set => SetProperty(ref _threadId, value);
    }

    public string PostId
    {
        get => _postId;
        set => SetProperty(ref _postId, value);
    }

    public string Formhash
    {
        get => _formhash;
        set => SetProperty(ref _formhash, value);
    }

    public string Posttime
    {
        get => _posttime;
        set => SetProperty(ref _posttime, value);
    }

    public string TypeId
    {
        get => _typeId;
        set => SetProperty(ref _typeId, value);
    }

    public List<(string id, string name)> TypeIdOptions
    {
        get => _typeIdOptions;
        set => SetProperty(ref _typeIdOptions, value);
    }

    public List<(string id, string name)> ReadpermOptions
    {
        get => _readpermOptions;
        set => SetProperty(ref _readpermOptions, value);
    }

    public string SelectedTypeId
    {
        get => _selectedTypeId;
        set
        {
            if (SetProperty(ref _selectedTypeId, value))
            {
                UpdateSubmitButtonState();
            }
        }
    }

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

    // 命令
    public IAsyncRelayCommand PickImageCommand { get; }
    public IAsyncRelayCommand PickAttachmentCommand { get; }
    public IAsyncRelayCommand<UploadedFileInfo> DeleteImageCommand { get; }
    public IAsyncRelayCommand<UploadedFileInfo> DeleteAttachmentCommand { get; }
    public IAsyncRelayCommand<UploadedFileInfo> InsertImageCommand { get; }
    public IAsyncRelayCommand<UploadedFileInfo> InsertAttachmentCommand { get; }
    public IAsyncRelayCommand SubmitEditCommand { get; }
    public IAsyncRelayCommand CancelCommand { get; }

    /// <summary>
    /// 无参构造函数 - 用于 XAML 数据绑定和 Shell 导航
    /// </summary>
    public EditOPReplyViewModel() : this(new ApiService(), "", "", "", "")
    {
    }

    public EditOPReplyViewModel(ApiService apiService, string boardId, string threadId, string postId, string boardName)
    {
        _apiService = apiService;
        _boardId = boardId;
        _threadId = threadId;
        _postId = postId;
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
        SubmitEditCommand = new AsyncRelayCommand(OnSubmitEditAsync);
        CancelCommand = new AsyncRelayCommand(OnCancelAsync);

        UpdateSubmitButtonState();
    }

    /// <summary>
    /// 初始化编辑页面信息
    /// </summary>
    public void Initialize(string boardId, string threadId, string postId, string boardName, string threadTitle)
    {
        BoardId = boardId;
        ThreadId = threadId;
        PostId = postId.Replace("post_","");
        BoardName = boardName;
        ThreadTitle = threadTitle;

        Debug.WriteLine($"✅ EditOPReplyViewModel 初始化: fid={boardId}, tid={threadId}, pid={postId}");
    }

    /// <summary>
    /// 异步加载编辑页面数据
    /// </summary>
    public async Task LoadEditPageDataAsync()
    {
        try
        {
            IsLoading = true;
            Debug.WriteLine($"📄 开始加载编辑页面数据: fid={BoardId}, tid={ThreadId}, pid={PostId}");

            if (string.IsNullOrEmpty(BoardId) || string.IsNullOrEmpty(ThreadId) || string.IsNullOrEmpty(PostId))
            {
                Debug.WriteLine("⚠️ 参数不完整，无法加载编辑页面");
                return;
            }

            var editUrl = $"https://bbs.pcbeta.com/forum.php?mod=post&action=edit&fid={BoardId}&tid={ThreadId}&pid={PostId}&inajax=1";
            Debug.WriteLine($"📝 编辑页面URL: {editUrl}");

            var pageHtml = await _apiService.GetPageHtmlAsync(editUrl);

            if (string.IsNullOrEmpty(pageHtml))
            {
                Debug.WriteLine("❌ 无法获取编辑页面HTML");
                await ShowErrorAlert("加载失败", "无法加载编辑页面");
                return;
            }

            // 从HTML中提取关键数据
            Formhash = ExtractFormHashFromHtml(pageHtml);
            Posttime = ExtractPostTimeFromHtml(pageHtml);
            var originalContent = ExtractMessageFromHtml(pageHtml);
            var subject = ExtractSubjectFromHtml(pageHtml);
            var extractedTypeId = ExtractTypeIdFromHtml(pageHtml);

            Debug.WriteLine($"✅ 提取的数据:");
            Debug.WriteLine($"   - formhash: {Formhash}");
            Debug.WriteLine($"   - posttime: {Posttime}");
            Debug.WriteLine($"   - subject: {subject}");
            Debug.WriteLine($"   - typeid: {extractedTypeId}");

            // 设置原始内容和编辑内容
            OriginalContent = originalContent;
            PostContent = originalContent;
            
            // 设置标题
            if (!string.IsNullOrEmpty(subject))
            {
                ThreadTitle = subject;
            }

            // 设置主题分类
            if (!string.IsNullOrEmpty(extractedTypeId))
            {
                SelectedTypeId = extractedTypeId;
                TypeId = extractedTypeId;
                Debug.WriteLine($"✅ 设置主题分类: {extractedTypeId}");
            }

            // 解析其他表单参数
            ParseExtraParametersFromHtml(pageHtml);

            // 解析主题分类选项
            ParseTypeIdOptionsFromHtml(pageHtml);

            // 解析阅读权限选项
            ParseReadpermOptionsFromHtml(pageHtml);

            // 加载已有的附件
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
                return match.Groups[1].Value;

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
    /// 从HTML中提取posttime
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
                return match.Groups[1].Value;

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
    /// 从HTML中提取message
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
                content = System.Net.WebUtility.HtmlDecode(content);
                return content.Trim();
            }

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
    /// 从HTML中提取subject
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
    /// 从HTML中提取typeid
    /// </summary>
    private string ExtractTypeIdFromHtml(string html)
    {
        try
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                html,
                @"<option[^>]*value=""([^""]+)""[^>]*selected[^>]*>",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );

            if (match.Success)
                return match.Groups[1].Value;

            match = System.Text.RegularExpressions.Regex.Match(
                html,
                @"<option[^>]*selected=""selected""[^>]*value=""([^""]+)""",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );

            return match.Success ? match.Groups[1].Value : "0";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 提取typeid失败: {ex.Message}");
            return "0";
        }
    }

    /// <summary>
    /// 解析主题分类选项
    /// </summary>
    private void ParseTypeIdOptionsFromHtml(string html)
    {
        try
        {
            var options = new List<(string id, string name)>();
            var regex = new System.Text.RegularExpressions.Regex(
                @"<option\s+value=""([^""]+)""[^>]*>([^<]+)</option>",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );

            var matches = regex.Matches(html);
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                var id = match.Groups[1].Value;
                var name = System.Net.WebUtility.HtmlDecode(match.Groups[2].Value.Trim());

                if (name.Contains("选择") || name.Contains("分类"))
                    continue;

                options.Add((id, name));
                Debug.WriteLine($"  - 主题分类: {id} = {name}");
            }

            if (options.Count > 0)
            {
                TypeIdOptions = options;
                Debug.WriteLine($"✅ 已加载 {options.Count} 个主题分类选项");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 解析主题分类失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 解析阅读权限选项
    /// </summary>
    private void ParseReadpermOptionsFromHtml(string html)
    {
        try
        {
            var options = new List<(string id, string name)>();
            var pattern = @"name=""readperm"".*?</select>";
            var selectMatch = System.Text.RegularExpressions.Regex.Match(html, pattern, 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);

            if (selectMatch.Success)
            {
                var selectContent = selectMatch.Value;
                var optionRegex = new System.Text.RegularExpressions.Regex(
                    @"<option\s+value=""([^""]+)""[^>]*>([^<]+)</option>",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase
                );

                var matches = optionRegex.Matches(selectContent);
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    var id = match.Groups[1].Value;
                    var name = System.Net.WebUtility.HtmlDecode(match.Groups[2].Value.Trim());
                    options.Add((id, name));
                    Debug.WriteLine($"  - 阅读权限: {id} = {name}");
                }

                if (options.Count > 0)
                {
                    ReadpermOptions = options;
                    Debug.WriteLine($"✅ 已加载 {options.Count} 个阅读权限选项");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 解析阅读权限失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 解析额外的表单参数
    /// </summary>
    private void ParseExtraParametersFromHtml(string html)
    {
        try
        {
            var tempValue = ReplycreditTimes;
            ExtractInputValue(html, "replycredit_times", ref tempValue);
            ReplycreditTimes = tempValue;

            tempValue = ReplycreditExtcredits;
            ExtractInputValue(html, "replycredit_extcredits", ref tempValue);
            ReplycreditExtcredits = tempValue;

            tempValue = ReplycreditMembertimes;
            ExtractSelectValue2(html, "replycredit_membertimes", ref tempValue);
            ReplycreditMembertimes = tempValue;

            tempValue = ReplycreditRandom;
            ExtractSelectValue2(html, "replycredit_random", ref tempValue);
            ReplycreditRandom = tempValue;

            tempValue = Readperm;
            ExtractSelectValue(html, "readperm", ref tempValue);
            Readperm = tempValue;

            tempValue = Price;
            ExtractInputValue(html, "price", ref tempValue);
            Price = tempValue;

            // ✅ 提取4个复选框参数
            Hiddenreplies = ExtractCheckboxValue(html, "hiddenreplies");
            Ordertype = ExtractCheckboxValue(html, "ordertype");
            Allownoticeauthor = ExtractCheckboxValue(html, "allownoticeauthor");
            Usesig = ExtractCheckboxValue(html, "usesig");

            Debug.WriteLine($"✅ 已解析额外表单参数");
            Debug.WriteLine($"   - 复选框: hiddenreplies={Hiddenreplies}, ordertype={Ordertype}, allownoticeauthor={Allownoticeauthor}, usesig={Usesig}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"⚠️ 解析额外参数失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 从HTML中提取输入框的值
    /// </summary>
    private void ExtractInputValue(string html, string fieldName, ref string value)
    {
        try
        {
            var pattern = $@"<input[^>]*name=""{fieldName}""[^>]*value=""([^""]*)""";
            var match = System.Text.RegularExpressions.Regex.Match(html, pattern, 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (match.Success)
                value = System.Net.WebUtility.HtmlDecode(match.Groups[1].Value);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"⚠️ 提取输入框值失败 ({fieldName}): {ex.Message}");
        }
    }

    /// <summary>
    /// 从HTML中提取下拉框的选中值，使用适用于 select+name（阅读权限）
    /// </summary>
    private void ExtractSelectValue(string html, string fieldName, ref string value)
    {
        try
        {
            var pattern = $@"<select[^>]*name=""{fieldName}""[^>]*>[\s\S]*?<option(?=[^>]*selected=""selected"")(?=[^>]*value=""([^""]*)"")[^>]*>";
            var match = System.Text.RegularExpressions.Regex.Match(html, pattern, 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);

            if (match.Success)
                value = System.Net.WebUtility.HtmlDecode(match.Groups[1].Value);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"⚠️ 提取下拉框值失败 ({fieldName}): {ex.Message}");
        }
    }
    /// <summary>
    /// 从HTML中提取下拉框的选中值，使用适用于 select+id（中奖率,每人最多获得几次）
    /// </summary>
    private void ExtractSelectValue2(string html, string fieldName, ref string value)
    {
        try
        {
            var pattern = $@"<select[^>]*id=""{fieldName}""[^>]*>[\s\S]*?<option(?=[^>]*selected=""selected"")(?=[^>]*value=""([^""]*)"")[^>]*>";
            var match = System.Text.RegularExpressions.Regex.Match(html, pattern, 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);

            if (match.Success)
                value = System.Net.WebUtility.HtmlDecode(match.Groups[1].Value);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"⚠️ 提取下拉框值失败 ({fieldName}): {ex.Message}");
        }
    }

    /// <summary>
    /// 从HTML中提取复选框的选中状态
    /// </summary>
    private bool ExtractCheckboxValue(string html, string fieldName)
    {
        try
        {
            var pattern = $@"<input[^>]*type=""checkbox""[^>]*name=""{fieldName}""[^>]*(checked=""checked"")[^>]*>";
            var match = System.Text.RegularExpressions.Regex.Match(html, pattern, 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            return match.Success;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"⚠️ 提取复选框值失败 ({fieldName}): {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 从编辑页面HTML中加载已有的附件
    /// </summary>
    private async Task LoadExistingAttachmentsAsync(string pageHtml)
    {
        try
        {
            Debug.WriteLine("📎 开始加载已有的附件...");

            var xmlParsingService = new XmlParsingService();
            var (existingImages, existingAttachments) = xmlParsingService.ExtractAttachmentsFromEditPageHtml(pageHtml);

            UploadedImages.Clear();
            UploadedAttachments.Clear();

            foreach (var image in existingImages)
            {
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

            foreach (var attachment in existingAttachments)
            {
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
                await UploadImageAsync(result);
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

            var (formhash, uid, hash) = await GetAuthenticationParamsAsync();
            var uploadResult = await UploadFileAsync(fileResult.FullPath, isImage: true, uid, hash);

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
                await UploadAttachmentAsync(result);
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

            var (formhash, uid, hash) = await GetAuthenticationParamsAsync();
            var uploadResult = await UploadFileAsync(fileResult.FullPath, isImage: false, uid, hash);

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
    /// 删除图片
    /// </summary>
    private async Task OnDeleteImageAsync(UploadedFileInfo? fileInfo)
    {
        if (fileInfo == null) return;

        try
        {
            if (!string.IsNullOrEmpty(fileInfo.AttachmentId))
                await DeleteAttachmentFromServerAsync(fileInfo.AttachmentId);

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
            if (!string.IsNullOrEmpty(fileInfo.AttachmentId))
                await DeleteAttachmentFromServerAsync(fileInfo.AttachmentId);

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
            Debug.WriteLine($"✅ 已插入图片标签");
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

            var currentPage = navStack.Last() as EditOPReplyPage;
            if (currentPage == null)
            {
                Debug.WriteLine("⚠️ 当前页面不是EditOPReplyPage");
                return;
            }

            var postContentField = currentPage.GetType().GetField("PostContentEditor",
                System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (postContentField == null)
            {
                Debug.WriteLine("⚠️ 未找到PostContentEditor字段");
                return;
            }

            var editor = postContentField.GetValue(currentPage) as Editor;
            if (editor == null)
            {
                Debug.WriteLine("⚠️ PostContentEditor为null");
                return;
            }

            var cursorPos = editor.CursorPosition;
            if (cursorPos < 0)
            {
                cursorPos = PostContent.Length;
                Debug.WriteLine($"⚠️ 无法获取光标位置，默认插入到末尾");
            }

            var newContent = PostContent.Insert(cursorPos, text);
            PostContent = newContent;

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
            var (formhash, _, _) = await GetAuthenticationParamsAsync();
            var deleteUrl = $"https://bbs.pcbeta.com/forum.php?mod=ajax&action=deleteattach&inajax=yes&aids[]={attachmentId}&tid={ThreadId}&pid={PostId}&formhash={formhash}";
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
            var pageHtml = await _apiService.GetThreadPageHtmlAsync(ThreadId);

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
    /// 通用文件上传方法
    /// </summary>
    private async Task<UploadResult?> UploadFileAsync(string filePath, bool isImage, string uid, string hash)
    {
        try
        {
            var uploadUrl = isImage
                ? $"https://bbs.pcbeta.com/misc.php?mod=swfupload&action=swfupload&operation=upload&type=image"
                : $"https://bbs.pcbeta.com/misc.php?mod=swfupload&action=swfupload&operation=upload&fid={BoardId}";

            var uploadResult = await _apiService.UploadFileAsync(uploadUrl, filePath, uid, hash, isImage, BoardId);

            if (uploadResult == null)
            {
                Debug.WriteLine($"❌ 文件上传失败");
                return null;
            }

            Debug.WriteLine($"✅ 文件上传成功，附件ID: {uploadResult.AttachmentId}");

            if (!isImage && !string.IsNullOrEmpty(uploadResult.AttachmentId) && !string.IsNullOrEmpty(BoardId))
            {
                var isBindSuccess = await _apiService.BindAttachmentAsync(uploadResult.AttachmentId, BoardId, isImage);

                if (isBindSuccess)
                    Debug.WriteLine($"✅ 附件绑定成功");
                else
                    Debug.WriteLine($"⚠️ 附件绑定失败（但上传成功）");
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
    /// 提交编辑
    /// </summary>
    private async Task OnSubmitEditAsync()
    {
        Debug.WriteLine($"🚀 OnSubmitEditAsync 被调用");
        try
        {
            if (string.IsNullOrEmpty(ThreadTitle) || ThreadTitle.Length < 2)
            {
                await ShowErrorAlert("提示", "请输入至少2个字符的帖子标题");
                return;
            }

            if (string.IsNullOrEmpty(PostContent) || PostContent.Length < 8)
            {
                await ShowErrorAlert("提示", "请输入至少8个字符的帖子内容");
                return;
            }

            if (SelectedTypeId == "0")
            {
                await ShowErrorAlert("提示", "请选择主题分类");
                return;
            }

            if (string.IsNullOrEmpty(Formhash) || string.IsNullOrEmpty(Posttime))
            {
                await ShowErrorAlert("错误", "无法获取必要的编辑参数，请重新加载页面");
                return;
            }

            Debug.WriteLine($"📝 提交编辑请求");

            var attachmentMetadataList = BuildAttachmentMetadataList();

            // ✅ 改为调用 SubmitEditOPReplyAsync，传递所有编辑楼主发帖所需的参数
            ApiResultModel result = await _apiService.SubmitEditOPReplyAsync(
                BoardId,
                ThreadId,
                PostId,
                ThreadTitle,  // ✅ 新增：编辑楼主发帖需要标题
                PostContent,
                Formhash,
                Posttime,
                attachmentMetadataList,
                // ✅ 新增：13个额外参数
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
                SelectedTypeId  // ✅ typeid 参数
            );

            if (result.IsSuccess)
            {
                Debug.WriteLine($"✅ 帖子编辑成功");
                await ShowSuccessAlert("更新成功", "帖子已更新");

                try
                {
                    await Shell.Current.GoToAsync("..");
                    Debug.WriteLine($"✅ 返回上一页");

                    await Task.Delay(1000);

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
                }
                catch (Exception navEx)
                {
                    Debug.WriteLine($"❌ 导航和刷新错误: {navEx.Message}");
                }
            }
            else
            {
                Debug.WriteLine($"❌ 帖子编辑失败");
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
                    IsAttachment = false
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
                    IsAttachment = true
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
                    await DeleteAttachmentFromServerAsync(file.AttachmentId);
            }

            foreach (var file in UploadedAttachments.ToList())
            {
                if (!string.IsNullOrEmpty(file.AttachmentId))
                    await DeleteAttachmentFromServerAsync(file.AttachmentId);
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
    /// 更新提交按钮状态
    /// </summary>
    private void UpdateSubmitButtonState()
    {
        IsSubmitEnabled = !string.IsNullOrWhiteSpace(PostContent) && PostContent.Length >= 8 
                       && !string.IsNullOrWhiteSpace(ThreadTitle) && ThreadTitle.Length >= 2
                       && SelectedTypeId != "0";
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
