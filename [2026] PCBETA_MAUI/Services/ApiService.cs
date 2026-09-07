using System.Diagnostics;
using System.Text;

namespace PCBetaMAUI.Services;

/// <summary>
/// Service for making API calls to the PCBETA forum using the shared HttpClient
/// </summary>
public partial class ApiService
{
    public const string BaseUrl = "https://bbs.pcbeta.com";
    private readonly XmlParsingService _xmlParsingService;

    /// <summary>
    /// Stores the last retrieved page content for login status checking
    /// </summary>
    public string LastPageContent { get; private set; } = string.Empty;

    /// <summary>
    /// 判断响应是否为站点访问校验页，而不是正常业务页面。
    /// </summary>
    public static bool IsAccessChallengeResponse(string? responseContent)
    {
        if (string.IsNullOrWhiteSpace(responseContent))
            return true;

        return responseContent.Contains("请启用 JavaScript", StringComparison.OrdinalIgnoreCase) ||
               responseContent.Contains("access_js_verified", StringComparison.OrdinalIgnoreCase) ||
               responseContent.Contains("access_js_platform", StringComparison.OrdinalIgnoreCase) ||
               responseContent.Contains("__access_review", StringComparison.OrdinalIgnoreCase) ||
               responseContent.Contains("access_env_challenge", StringComparison.OrdinalIgnoreCase) ||
               responseContent.Contains("access_env_verified", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 判断响应是否明确表示未登录。
    /// </summary>
    public static bool IsUnauthenticatedResponse(string? responseContent)
    {
        if (IsAccessChallengeResponse(responseContent))
            return true;

        return string.IsNullOrWhiteSpace(responseContent) ||
               responseContent.Contains(">登录</a>", StringComparison.OrdinalIgnoreCase) ||
               responseContent.Contains("请登录", StringComparison.OrdinalIgnoreCase);
    }

    public ApiService()
    {
        _xmlParsingService = new XmlParsingService();
    }

    public async Task<string> SubmitPollAsync(string forumId, string threadId, string formHash, IEnumerable<string> optionIds)
    {
        try
        {
            var url = $"{BaseUrl}/forum.php?mod=misc&action=votepoll&fid={Uri.EscapeDataString(forumId)}&tid={Uri.EscapeDataString(threadId)}&pollsubmit=yes&quickforward=yes&inajax=1";
            var values = new List<KeyValuePair<string, string>>
            {
                new("formhash", formHash),
                new("pollsubmit", "true")
            };
            values.AddRange(optionIds.Select(id => new KeyValuePair<string, string>("pollanswers[]", id)));

            using var content = new FormUrlEncodedContent(values);
            var response = await HttpClientManager.Instance.PostAsync(url, content);
            var result = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                return string.Empty;

            LastPageContent = result;
            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SubmitPollAsync error: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// 获取我的收藏页面 HTML (type=thread)
    /// </summary>
    public async Task<string> GetMyFavoritePageHtmlAsync(int page = 1)
    {
        try
        {
            var url = $"https://i.pcbeta.com/home.php?mod=space&do=favorite&type=thread&inajax=1";
            if (page > 1)
                url += $"&page={page}";

            var response = await HttpClientManager.Instance.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            LastPageContent = content;
            return content;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GetMyFavoritePageHtml error: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Fetches "我的帖子" 页面（用户个人发布的帖子）
    /// URL: https://i.pcbeta.com/home.php?mod=space&do=thread&view=me&type=thread&inajax=1
    /// 支持 type、page 和 filter 参数
    /// </summary>
    public async Task<List<Models.ThreadInfo>> GetMyPostsAsync(int page = 1, string filter = "", string type = "thread")
    {
        try
        {
            type = type == "reply" ? "reply" : "thread";
            var url = $"https://i.pcbeta.com/home.php?mod=space&do=thread&view=me&type={type}&inajax=1";
            if (page > 1)
                url += $"&page={page}";
            if (!string.IsNullOrEmpty(filter))
                url += $"&filter={Uri.EscapeDataString(filter)}";

            var response = await HttpClientManager.Instance.GetAsync(url);
            string content = string.Empty;

            if (response.IsSuccessStatusCode)
            {
                content = await response.Content.ReadAsStringAsync();
            }

            // Do NOT use local sample data for production; if response is empty, return parsed result (may be empty)
            LastPageContent = content ?? string.Empty;
            return _xmlParsingService.ParseMyPosts(content ?? string.Empty, type == "reply");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GetMyPostsAsync error: {ex.Message}");
            return new List<Models.ThreadInfo>();
        }
    }

    /// <summary>
    /// 删除单个收藏
    /// POST：https://i.pcbeta.com/home.php?mod=spacecp&ac=favorite&op=delete&favid=FAVID&type=thread&inajax=1
    /// Form参数：
    /// - referer: https://i.pcbeta.com/home.php?mod=space&do=favorite&type=thread
    /// - deletesubmit: true
    /// - formhash: 从收藏页面获取
    /// - handlekey: a_delete_FAVID
    /// </summary>
    public async Task<string> DeleteFavoriteAsync(string favId, string formHash, string handleKey)
    {
        try
        {
            var url = $"https://i.pcbeta.com/home.php?mod=spacecp&ac=favorite&op=delete&favid={favId}&type=thread&inajax=1";

            var formData = new Dictionary<string, string>
            {
                { "referer", "https://i.pcbeta.com/home.php?mod=space&do=favorite&type=thread" },
                { "deletesubmit", "true" },
                { "formhash", formHash },
                { "handlekey", handleKey }
            };

            var content = new FormUrlEncodedContent(formData);
            var response = await HttpClientManager.Instance.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadAsStringAsync();
            Debug.WriteLine($"✅ 删除收藏请求已发送 (favId={favId})");
            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 删除收藏错误: {ex.Message}");
            return string.Empty;
        }
    }

    public class ApiResultModel
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
    }
    /// <summary>
    /// Attempts login with username and password
    /// ✅ 改进：返回元组 (isSuccess, errorMessage)，支持返回具体的错误信息
    /// </summary>
    public async Task<(bool isSuccess, string? errorMessage)> LoginAsync(string username, string password, string? questionId = null, string? answer = null)
    {
        try
        {
            var loginUrl = $"{BaseUrl}/member.php?mod=logging&action=login&loginsubmit=yes&loginhash=L" + GenerateRandomCode() + "&inajax=1";

            var formData = new Dictionary<string, string>
            {
                { "username", username },
                { "password", password }
            };

            if (!string.IsNullOrEmpty(questionId) && !string.IsNullOrEmpty(answer))
            {
                formData["questionid"] = questionId;
                formData["answer"] = answer;
            }

            using var requestContent = new FormUrlEncodedContent(formData);
            using var request = new HttpRequestMessage(HttpMethod.Post, loginUrl)
            {
                Content = requestContent
            };
            //request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/154.0.0.0 Safari/537.36");

            var response = await HttpClientManager.Instance.SendAsync(request);
            //response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            return _xmlParsingService.IsLoginSuccessful(content);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Login API error: {ex.Message}");
            return (false, $"登录请求异常: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取搜索结果的接口，仅在页面初始化获取数据时调用这个接口
    /// </summary>
    /// <param name="formhash"></param>
    /// <returns></returns>
    public async Task<string> GetSearchPageAsync(string formhash, bool isLastPostChecked, string keyword="", int page=1)
    {
        try
        {
            var url = $"{BaseUrl}/search.php?mod=forum";
            var queryParams = new Dictionary<string, string>
            {
                { "formhash", formhash },
                { "searchsubmit", "yes" },
                { "orderby", isLastPostChecked ? "lastpost" : "dateline" },
                { "ascdesc", "desc" },
                { "page", page.ToString() }
            };

            if(!string.IsNullOrWhiteSpace(keyword))
            {
                queryParams["srchtxt"] = keyword;
            }
            else
            {
                queryParams["srchfrom"] = "86400";
            }

            var content = new FormUrlEncodedContent(queryParams);

            var response = await HttpClientManager.Instance.PostAsync(url, content);
            // 302 is expected; treat success if redirect
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync();
                LastPageContent = result;
                return result;
            }

        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GetSearchPage error: {ex.Message}");
        }

        return string.Empty;
    }

    /// <summary>
    /// [已弃用] 获取搜索结果的接口，页面初始化获取数据时不调用这个接口，而是调用上一个 GetSearchPageAsync 方法 GetSearchPageAsync(string formhash)
    /// </summary>
    /// <param name="searchid"></param>
    /// <param name="keyword"></param>
    /// <param name="page"></param>
    /// <returns></returns>
/*
     public async Task<string> GetSearchPageAsync(string formhash, string searchid,int page, string keyword="")
    {
        try
        {
            await GetSearchPageAsync(formhash, keyword,page); //每次搜索前都必须请求前序URL

            var url = $"https://bbs.pcbeta.com/search.php?mod=forum&searchid={searchid}&orderby=dateline&ascdesc=desc&kw={keyword}&page="+page.ToString()+ "&searchsubmit=yes";
            var response = await HttpClientManager.Instance.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            LastPageContent = content;
            return content;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Main page API error: {ex.Message}");
            return "";
        }
    }
 */

    /// <summary>
    /// 随机生成LoginHash
    /// </summary>
    /// <param name="length"></param>
    /// <returns></returns>
    public static string GenerateRandomCode(int length = 4)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var random = new Random();
        var result = new char[length];

        for (int i = 0; i < length; i++)
        {
            result[i] = chars[random.Next(chars.Length)];
        }

        return new string(result);
    }
    /// <summary>
    /// Fetches the forum main page with categories and sections
    /// </summary>
    public async Task<List<Models.ForumCategory>> GetMainPageForumsAsync()
    {
        try
        {
            var url = $"{BaseUrl}/forum.php?inajax=1";
            var response = await HttpClientManager.Instance.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            LastPageContent = content;
            return _xmlParsingService.ParseMainPageForums(content);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Main page API error: {ex.Message}");
            return new List<Models.ForumCategory>();
        }
    }

    /// <summary>
    /// 从论坛主页获取一些用户信息，仅在当前页面是论坛主页时才能调用，若不是则需要先导航到论坛主页
    /// </summary>
    public async Task<(string? username, string? logoutUrl, string? formHash)> GetUserInfoFromMainPageAsync()
    {
        try
        {
            var url = $"{BaseUrl}/forum.php";
            var response = await HttpClientManager.Instance.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            if(content.Contains("<strong>登录</strong>"))
            {
                return (null, null, null);
            }

            LastPageContent = content;

            // Extract username
            var username = _xmlParsingService.ExtractUsernameFromMainPage(content);

            // Extract logout URL and formhash
            var (logoutUrl, formHash) = _xmlParsingService.ExtractLogoutInfoFromMainPage(content);

            // Prepend base URL if logout URL is relative
            if (!string.IsNullOrEmpty(logoutUrl) && !logoutUrl.StartsWith("http"))
            {
                logoutUrl = BaseUrl + "/" + logoutUrl.TrimStart('/');
            }

            Debug.WriteLine($"User info - Username: {username}, LogoutUrl: {logoutUrl}");

            return (username, logoutUrl, formHash);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Get user info error: {ex.Message}");
            return (null, null, null);
        }
    }

    /// <summary>
    /// Fetches thread list for a specific board
    /// ✅ 改进：保存响应内容用于登录状态检测
    /// </summary>
    public async Task<List<Models.ThreadInfo>> GetThreadListAsync(string boardId, int page = 1)
    {
        try
        {
            var url = $"{BaseUrl}/forum-{boardId}-{page}.html?inajax=1";
            var response = await HttpClientManager.Instance.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            LastPageContent = content;  // ✅ 保存响应内容用于登录检测
            return _xmlParsingService.ParseThreadList(content);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Thread list API error: {ex.Message}");
            return new List<Models.ThreadInfo>();
        }
    }

    /// <summary>
    /// Fetches content of a specific thread
    /// </summary>
    public async Task<Models.ThreadContent> GetThreadContentAsync(string threadId, int page = 1)
    {
        try
        {
            var url = $"{BaseUrl}/forum.php?mod=viewthread&tid={threadId}&inajax=1";
            if (page > 1)
                url += $"&page={page}";

            var response = await HttpClientManager.Instance.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            return _xmlParsingService.ParseThreadContent(content, page);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Thread content API error: {ex.Message}");
            return new Models.ThreadContent
            {
                Id = threadId,
                Title = "",
                Author = "",
                PostTime = "发表于 --",
                OtherIfm = "",
                PlainTextContent = "",
                RawHtmlContent = "",
                Replies = 0
            };
        }
    }

    /// <summary>
    /// Tests if user is still logged in by making a request
    /// </summary>
    public async Task<bool> IsLoggedInAsync()
    {
        try
        {
            var response = await HttpClientManager.Instance.GetAsync($"{BaseUrl}/forum.php?inajax=1");
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode || IsUnauthenticatedResponse(responseContent))
            {
                Debug.WriteLine("[LoginDetection] 响应明确表示未登录或访问校验失败");
                return false;
            }

            Debug.WriteLine("[LoginDetection] 响应未发现未登录或访问校验标记，判定为已登录");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LoginDetection] 登录状态请求失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Logs out the current user
    /// </summary>
    public async Task LogoutAsync()
    {
        try
        {
            var url = $"{BaseUrl}/member.php?mod=logging&action=logout";
            await HttpClientManager.Instance.GetAsync(url);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Logout error: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the user avatar URL from the user profile page
    /// </summary>
    public async Task<string> GetUserAvatarAsync()
    {
        try
        {
            var url = "https://i.pcbeta.com/home.php?mod=spacecp&ac=avatar&inajax=1";
            var response = await HttpClientManager.Instance.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            return _xmlParsingService.ExtractUserAvatarUrl(content);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Get user avatar error: {ex.Message}");
            return string.Empty;
        }
    }
    /// <summary>
    /// 判断用户头像内部数据是否为SVG图像
    /// </summary>
    /// <param name="url"></param>
    /// <returns>True为SVG图像，False不是SVG图像</returns>
    public async Task<bool> IsUserAvatarSVGAsync(string url)
    {
        try
        {
            var response = await HttpClientManager.Instance.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            if(content.Contains("<svg"))
            {
                Debug.WriteLine("用户头像是SVG图像");
                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Get user avatar error: {ex.Message}");
        }

        return false;
    }

    /// <summary>
    /// Checks if user is logged in by examining the content for login link
    /// </summary>
    public bool IsLoggedInByContent(string content)
    {
        if (string.IsNullOrEmpty(content))
            return false;

        // If the login link exists, user is not logged in
        return !content.Contains("<a href=\"member.php?mod=logging&amp;action=login\">登录</a>");
    }

    /// <summary>
    /// 获取回帖编辑页面的 HTML
    /// 调用 /forum.php?mod=post&action=reply&fid={forumId}&tid={threadId}
    /// 或者 /forum.php?mod=post&action=reply&fid={forumId}&tid={threadId}&repquote={replyId}（回复评论）
    /// 用于从中提取 formhash、uid 和 hash 等认证参数
    /// 
    /// ✅ 关键：这个请求需要在上传附件前发送，以建立正确的 session，
    /// 这样后续的附件上传和发送回帖才能被正确绑定到该回帖上
    /// </summary>
    public async Task<string> GetReplyEditPageHtmlAsync(string forumId, string threadId, string? repquote = null)
    {
        try
        {
            // 重要：使用 GET 请求来初始化回帖会话
            var url = $"{BaseUrl}/forum.php?mod=post&action=reply&fid={forumId}&tid={threadId}";

            // 如果有 repquote 参数，表示这是回复评论而不是回复楼主
            if (!string.IsNullOrEmpty(repquote))
            {
                url += $"&repquote={Uri.EscapeDataString(repquote)}";
                Debug.WriteLine($"📝 回复评论 - repquote={repquote}");
            }

            Debug.WriteLine($"📄 获取回帖编辑页面: fid={forumId}, tid={threadId}");
            Debug.WriteLine($"   - URL: {url}");

            var response = await HttpClientManager.Instance.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            Debug.WriteLine($"✅ 获取回帖编辑页面成功 (fid={forumId}, tid={threadId})");
            return content;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 获取回帖编辑页面失败: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// 获取回帖页面的 HTML（用于查看回帖）
    /// 用于从中提取 formhash、uid 和 hash 等认证参数
    /// 
    /// ⚠️ 注意：这个方法用于获取查看页面，如果需要回帖请使用 GetReplyEditPageHtmlAsync
    /// </summary>
    public async Task<string> GetThreadPageHtmlAsync(string threadId)
    {
        try
        {
            var url = $"{BaseUrl}/forum.php?mod=viewthread&tid={threadId}&extra=&mobile=2";
            var response = await HttpClientManager.Instance.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            Debug.WriteLine($"✅ 获取回帖页面 HTML 成功 (tid={threadId})");
            return content;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 获取回帖页面 HTML 失败: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Gets the attachment purchase confirmation page
    /// ✅ 新增：用于获取购买确认信息
    /// </summary>
    public async Task<string> GetAttachmentBuyConfirmPageAsync(string attachmentId)
    {
        try
        {
            // 构建请求购买确认页面的 URL
            var url = $"{BaseUrl}/forum.php?mod=misc&action=attachpay&aid={Uri.EscapeDataString(attachmentId)}";

            var response = await HttpClientManager.Instance.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            Debug.WriteLine($"✅ 获取购买确认页面成功（AID: {attachmentId}）");
            return content;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 获取购买确认页面失败: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// 上传文件到服务器（用于回帖中的图片和附件）
    /// 调用 /misc.php?mod=swfupload&operation=upload 接口
    /// 
    /// 第一步：上传文件
    /// 请求方式：POST
    /// 请求URL：https://bbs.pcbeta.com/misc.php?mod=swfupload&action=swfupload&operation=upload&fid={forumId}
    /// 参数：
    ///   - mod: swfupload
    ///   - action: swfupload
    ///   - operation: upload
    ///   - fid: 论坛ID
    ///   - uid: 用户ID
    ///   - hash: 认证哈希
    ///   - filetype: 文件类型（如 image/jpeg）
    ///   - type: 文件类型（如 image/jpeg）
    ///   - size: 文件大小（字节）
    ///   - Filedata: 二进制文件内容
    /// 响应：返回一个字符串，例如"4619726"就是附件ID，其他内容则代表失败
    /// 
    /// 第二步：绑定附件（需要调用 BindAttachmentAsync）
    /// </summary>
    public async Task<UploadResult?> UploadFileAsync(string uploadUrl, string filePath, string uid, string hash, bool isImage, string forumId = "")
    {
        try
        {
            if (!File.Exists(filePath))
            {
                Debug.WriteLine($"❌ 文件不存在: {filePath}");
                return null;
            }

            if (string.IsNullOrEmpty(uid) || string.IsNullOrEmpty(hash))
            {
                Debug.WriteLine("❌ 缺少认证参数（uid 或 hash）");
                return null;
            }

            var fileInfo = new FileInfo(filePath);
            var fileName = fileInfo.Name;
            var fileSize = fileInfo.Length;
            // 获取文件MIME类型
            var mimeType = GetMimeType(filePath);
            // 检查文件名是否包含中文
            if (TextContainsChinese(fileName))
            {
                Debug.WriteLine("⚠️ 文件名包含中文");
                return null;
            }

            Debug.WriteLine($"📤 开始上传文件: {fileName}");
            Debug.WriteLine($"   - 文件大小: {fileSize} 字节");
            Debug.WriteLine($"   - MIME类型: {mimeType}");
            Debug.WriteLine($"   - 上传URL: {uploadUrl}");

            using var fileStream = File.OpenRead(filePath);
            using var multipartContent = new MultipartFormDataContent();

            // 添加所有必需的表单字段（按照文档要求）
            multipartContent.Add(new StringContent("swfupload"), "mod");
            multipartContent.Add(new StringContent("swfupload"), "action");
            multipartContent.Add(new StringContent("upload"), "operation");

            if (!string.IsNullOrEmpty(forumId))
            {
                multipartContent.Add(new StringContent(forumId), "fid");
            }

            multipartContent.Add(new StringContent(uid), "uid");
            multipartContent.Add(new StringContent(hash), "hash");
            multipartContent.Add(new StringContent(mimeType), "filetype");

            if(isImage)
            {
                multipartContent.Add(new StringContent("image"), "type");
            }
            else
            {
                multipartContent.Add(new StringContent(mimeType), "type");
            }

            multipartContent.Add(new StringContent(fileSize.ToString()), "size");

            // 添加文件内容（必须命名为 Filedata）
            multipartContent.Add(new StreamContent(fileStream), "Filedata", fileName);

            var response = await HttpClientManager.Instance.PostAsync(uploadUrl, multipartContent);
            response.EnsureSuccessStatusCode();

            var responseText = await response.Content.ReadAsStringAsync();
            Debug.WriteLine($"📥 上传响应: {responseText.Substring(0, Math.Min(200, responseText.Length))}");
            if(responseText=="-1")
            {
                return null;
            }

            // 判断上传是否成功
            // 成功响应格式：DISCUZUPLOAD|0|0|4619072|-1|202604/07/014503iqvlvpdnvzxdvlvd.gif|007.small.gif|0
            // 只要返回的数据包含文件名或数字ID就视为上传成功
            if (!string.IsNullOrEmpty(responseText) && responseText.Length > 0 && !responseText.Contains("error"))
            {
                Debug.WriteLine($"✅ 文件上传成功: {fileName}");

                // 从响应中提取附件ID（格式：DISCUZUPLOAD|0|0|{attachmentId}|...）
                var attachmentId = ExtractAttachmentIdFromUploadResponse(responseText);

                if (string.IsNullOrEmpty(attachmentId))
                {
                    // 如果无法提取，使用响应本身作为ID（可能就是简单的数字）
                    attachmentId = responseText.Trim();
                    Debug.WriteLine($"⚠️ 使用响应文本作为附件ID: {attachmentId}");
                }

                Debug.WriteLine($"✅ 提取到附件ID: {attachmentId}");

                return new UploadResult
                {
                    Url = uploadUrl,
                    AttachmentId = attachmentId,
                    FileName = fileName,
                    FileSize = fileSize.ToString()
                };
            }
            else
            {
                Debug.WriteLine($"❌ 文件上传失败: {responseText}");
                return null;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 上传文件错误: {ex.Message}");
            return null;
        }
    }
    /// <summary>
    /// 文件名是否包含中文
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public static bool TextContainsChinese(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        foreach (char c in text)
        {
            // 中文 Unicode 范围
            if (c >= 0x4E00 && c <= 0x9FFF)
                return true;
        }

        return false;
    }
    /// <summary>
    /// 从上传响应中提取附件ID
    /// 响应格式：DISCUZUPLOAD|0|0|4619072|-1|202604/07/014503iqvlvpdnvzxdvlvd.gif|007.small.gif|0
    /// 附件ID 通常在第 4 个字段（索引3）
    /// </summary>
    private static string? ExtractAttachmentIdFromUploadResponse(string responseText)
    {
        try
        {
            // 格式：DISCUZUPLOAD|0|0|{attachmentId}|...
            if (responseText.StartsWith("DISCUZUPLOAD"))
            {
                var parts = responseText.Split('|');
                if (parts.Length > 3 && long.TryParse(parts[3], out _))
                {
                    return parts[3];
                }
            }

            // 尝试使用正则表达式提取任何数字ID
            var aidMatch = System.Text.RegularExpressions.Regex.Match(
                responseText, 
                @"(?:DISCUZUPLOAD\|[^|]*\|[^|]*\|)?(\d+)", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );

            if (aidMatch.Success)
            {
                return aidMatch.Groups[1].Value;
            }

            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"⚠️ 提取附件ID失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 绑定附件到帖子（第二步）
    /// 调用 /forum.php?mod=ajax&action=attachlist 接口
    /// 
    /// 请求方式：GET
    /// 请求URL：https://bbs.pcbeta.com/forum.php?mod=ajax&action=attachlist&aids={attachmentId}&fid={forumId}&inajax=1&ajaxtarget=attachlist
    /// 其中：
    ///   - aids: 上一步得到的附件ID
    ///   - fid: 论坛ID
    ///   - inajax: 1（表示AJAX请求）
    ///   - ajaxtarget: attachlist（目标元素）
    /// 
    /// 成功响应格式：<root><![CDATA[...HTML content...]]></root>
    /// </summary>
    public async Task<bool> BindAttachmentAsync(string attachmentId, string forumId,bool isImage)
    {
        try
        {
            if (string.IsNullOrEmpty(attachmentId))
            {
                Debug.WriteLine("❌ 附件ID为空");
                return false;
            }

            if (string.IsNullOrEmpty(forumId))
            {
                Debug.WriteLine("❌ 论坛ID为空");
                return false;
            }

            // 构建绑定URL
            string bindUrl;
            if(isImage)
            {
                bindUrl = $"{BaseUrl}/forum.php?mod=ajax&action=imagelist&pid=&aids={Uri.EscapeDataString(attachmentId)}&fid={forumId}&inajax=1&ajaxtarget=image_td_{Uri.EscapeDataString(attachmentId)}";
            }
            else
            {
                bindUrl = $"{BaseUrl}/forum.php?mod=ajax&action=attachlist&aids={Uri.EscapeDataString(attachmentId)}&fid={forumId}&inajax=1&ajaxtarget=attachlist";
            }

            Debug.WriteLine($"🔗 开始绑定附件: aid={attachmentId}, fid={forumId}");
            Debug.WriteLine($"   - 绑定URL: {bindUrl}");

            var response = await HttpClientManager.Instance.GetAsync(bindUrl);
            response.EnsureSuccessStatusCode();

            var responseText = await response.Content.ReadAsStringAsync();
            Debug.WriteLine($"📥 绑定响应: {responseText.Substring(0, Math.Min(300, responseText.Length))}...");

            // 判断绑定是否成功
            // 成功响应包含 <root><![CDATA[...]]></root> 和 HTML 表格内容
            bool isSuccess = responseText.Contains("<root>") && 
                           responseText.Contains("<![CDATA[") && 
                           (responseText.Contains("attswf") || responseText.Contains("attachupdate"));

            if (isSuccess)
            {
                Debug.WriteLine($"✅ 附件绑定成功");
                return true;
            }
            else
            {
                Debug.WriteLine($"❌ 附件绑定失败: {responseText.Substring(0, Math.Min(200, responseText.Length))}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 绑定附件错误: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 获取文件的MIME类型
    /// </summary>
    private static string GetMimeType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".zip" => "application/x-zip-compressed",
            ".rar" => "application/x-rar-compressed",
            ".7z" => "application/x-7z-compressed",
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            _ => "application/octet-stream"
        };
    }

    /// <summary>
    /// 删除已上传的附件
    /// 调用 /forum.php?mod=ajax&action=deleteattach 接口
    /// 
    /// 删除成功判断：仅需检查 StatusCode = 200 OK，无需判断响应内容
    /// </summary>
    public async Task DeleteAttachmentAsync(string deleteUrl)
    {
        try
        {
            if (string.IsNullOrEmpty(deleteUrl))
            {
                Debug.WriteLine("❌ 删除 URL 为空");
                return;
            }

            Debug.WriteLine($"🗑️ 开始删除附件: {deleteUrl.Substring(0, Math.Min(100, deleteUrl.Length))}...");

            var response = await HttpClientManager.Instance.GetAsync(deleteUrl);

            // ✅ 简化成功判断：仅检查 StatusCode = 200 OK
            if (response.IsSuccessStatusCode)
            {
                Debug.WriteLine($"✅ 附件删除成功 (StatusCode: {response.StatusCode})");
            }
            else
            {
                Debug.WriteLine($"❌ 附件删除失败 (StatusCode: {response.StatusCode})");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"⚠️ 删除附件错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 提交 回复楼主的回帖
    /// 调用 /forum.php?mod=post&action=reply 接口
    /// ✅ 改进：支持附件元数据参数（描述、阅读权限、售价），仅对附件添加权限和售价参数
    /// </summary>
    public async Task<ApiResultModel> SubmitReplyLZAsync(string forumId, string threadId, string replyContent, string formhash, List<AttachmentMetadata>? attachments = null)
    {
        try
        {
            if (string.IsNullOrEmpty(replyContent))
            {
                return new ApiResultModel() { IsSuccess = false,Message= "回帖内容为空" };
            }

            if (string.IsNullOrEmpty(formhash))
            {
                return new ApiResultModel() { IsSuccess = false, Message = "hash校验失败" };
            }

            // 获取当前时间戳
            var posttime = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

            // 构建发帖表单数据
            var data = new Dictionary<string, string>
            {
                { "fid", forumId },
                { "tid", threadId },
                { "formhash", formhash },
                { "posttime", posttime },
                { "message", replyContent },
                { "usesig", "1" },
                { "replysubmit", "yes" }
            };

            // ✅ 新增：添加附件元数据参数
            if (attachments != null && attachments.Count > 0)
            {
                Debug.WriteLine($"📎 开始添加 {attachments.Count} 个附件的元数据参数");

                foreach (var attachment in attachments)
                {
                    if (string.IsNullOrEmpty(attachment.AttachmentId))
                    {
                        Debug.WriteLine($"⚠️ 跳过空的附件ID");
                        continue;
                    }

                    // 添加描述参数（所有类型都需要）
                    var descKey = $"attachnew[{attachment.AttachmentId}][description]";
                    data[descKey] = attachment.Description ?? "";

                    // ✅ 关键改进：仅当 IsAttachment=true 时才添加 readperm 和 price 参数
                    if (attachment.IsAttachment)
                    {
                        var readPermKey = $"attachnew[{attachment.AttachmentId}][readperm]";
                        var priceKey = $"attachnew[{attachment.AttachmentId}][price]";
                        data[readPermKey] = attachment.ReadPerm ?? "0";
                        data[priceKey] = attachment.Price ?? "0";
                        Debug.WriteLine($"✅ 已添加附件参数 (ID={attachment.AttachmentId}) - 附件类型");
                        Debug.WriteLine($"   - 描述: {attachment.Description}");
                        Debug.WriteLine($"   - 阅读权限: {attachment.ReadPerm}");
                        Debug.WriteLine($"   - 售价: {attachment.Price}");
                    }
                    else
                    {
                        Debug.WriteLine($"✅ 已添加图片参数 (ID={attachment.AttachmentId}) - 图片类型（不添加权限和售价）");
                        Debug.WriteLine($"   - 描述: {attachment.Description}");
                    }
                }
            }
            else
            {
                Debug.WriteLine($"📎 无附件数据");
            }

            var content = new FormUrlEncodedContent(data);
            var submitUrl = $"{BaseUrl}/forum.php?mod=post&action=reply&infloat=yes&replysubmit=yes&inajax=1";

            Debug.WriteLine($"📝 开始提交回帖: fid={forumId}, tid={threadId}");

            var response = await HttpClientManager.Instance.PostAsync(submitUrl, content);
            response.EnsureSuccessStatusCode();

            var responseText = await response.Content.ReadAsStringAsync();
            Debug.WriteLine($"📥 提交响应: {responseText.Substring(0, Math.Min(200, responseText.Length))}...");

            // 判断提交是否成功
            // 成功的响应可能包含："成功"
            bool isSuccess = responseText.Contains("成功");

            if (isSuccess)
            {
                return new ApiResultModel() { IsSuccess = true };
            }
            else
            {
                Debug.WriteLine($"❌ 回帖提交失败: {responseText.Substring(0, Math.Min(200, responseText.Length))}");
                return new ApiResultModel() { IsSuccess = false, Message = "回帖提交失败" };
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 提交回帖错误: {ex.Message}");
            return new ApiResultModel() { IsSuccess = false, Message = "提交回帖异常，请稍后重试" };
        }
    }

    /// <summary>
    /// 提交 回复评论区某用户的回帖
    /// 调用 /forum.php?mod=post&action=reply 接口
    /// ✅ 改进：添加 noticeauthor、noticeauthormsg 参数，并正确格式化 noticetrimstr，支持附件元数据，仅对附件添加权限和售价
    /// </summary>
    public async Task<ApiResultModel> SubmitReplyPLQAsync(string forumId, string threadId, string replyContent, string formhash, string noticetrimstr, string noticeauthormsg, string userReplyId, string noticeauthor = "", List<AttachmentMetadata>? attachments = null)
    {
        try
        {
            if (string.IsNullOrEmpty(replyContent))
            {
                return new ApiResultModel() { IsSuccess = false, Message = "回帖内容为空" };
            }

            if (string.IsNullOrEmpty(formhash))
            {
                return new ApiResultModel() { IsSuccess = false, Message = "hash校验失败" };
            }

            // 获取当前时间戳
            var posttime = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

            // 构建发帖表单数据
            var data = new Dictionary<string, string>
            {
                { "fid", forumId },
                { "tid", threadId },
                { "formhash", formhash },
                { "posttime", posttime },
                { "message", replyContent },
                { "usesig", "1" },
                { "replysubmit", "yes" },
                { "reppid", userReplyId},
                { "reppost", userReplyId },
                { "noticetrimstr", noticetrimstr },  // ✅ 改进：使用格式化后的参数
                { "noticeauthormsg", noticeauthormsg }  // ✅ 新增：添加纯文本内容参数
            };

            // ✅ 关键：如果有 noticeauthor（加密的用户名），则添加到表单
            if (!string.IsNullOrEmpty(noticeauthor))
            {
                data["noticeauthor"] = noticeauthor;
                Debug.WriteLine($"✅ 已添加 noticeauthor 参数");
            }
            else
            {
                Debug.WriteLine($"⚠️ noticeauthor 为空，跳过添加");
            }

            // ✅ 新增：添加附件元数据参数
            if (attachments != null && attachments.Count > 0)
            {
                Debug.WriteLine($"📎 开始添加 {attachments.Count} 个附件的元数据参数");

                foreach (var attachment in attachments)
                {
                    if (string.IsNullOrEmpty(attachment.AttachmentId))
                    {
                        Debug.WriteLine($"⚠️ 跳过空的附件ID");
                        continue;
                    }

                    // 添加描述参数（所有类型都需要）
                    var descKey = $"attachnew[{attachment.AttachmentId}][description]";
                    data[descKey] = attachment.Description ?? "";

                    // ✅ 关键改进：仅当 IsAttachment=true 时才添加 readperm 和 price 参数
                    if (attachment.IsAttachment)
                    {
                        var readPermKey = $"attachnew[{attachment.AttachmentId}][readperm]";
                        var priceKey = $"attachnew[{attachment.AttachmentId}][price]";
                        data[readPermKey] = attachment.ReadPerm ?? "0";
                        data[priceKey] = attachment.Price ?? "0";
                        Debug.WriteLine($"✅ 已添加附件参数 (ID={attachment.AttachmentId}) - 附件类型");
                        Debug.WriteLine($"   - 描述: {attachment.Description}");
                        Debug.WriteLine($"   - 阅读权限: {attachment.ReadPerm}");
                        Debug.WriteLine($"   - 售价: {attachment.Price}");
                    }
                    else
                    {
                        Debug.WriteLine($"✅ 已添加图片参数 (ID={attachment.AttachmentId}) - 图片类型（不添加权限和售价）");
                        Debug.WriteLine($"   - 描述: {attachment.Description}");
                    }
                }
            }
            else
            {
                Debug.WriteLine($"📎 无附件数据");
            }

            var content = new FormUrlEncodedContent(data);
            var submitUrl = $"{BaseUrl}/forum.php?mod=post&action=reply&infloat=yes&replysubmit=yes&inajax=1";

            Debug.WriteLine($"📝 开始提交回帖: fid={forumId}, tid={threadId}");
            Debug.WriteLine($"   - noticetrimstr: {noticetrimstr}");
            Debug.WriteLine($"   - noticeauthormsg: {noticeauthormsg}");
            Debug.WriteLine($"   - noticeauthor: {(string.IsNullOrEmpty(noticeauthor) ? "❌ 未提供" : noticeauthor.Substring(0, Math.Min(20, noticeauthor.Length)) + "...")}");

            var response = await HttpClientManager.Instance.PostAsync(submitUrl, content);
            response.EnsureSuccessStatusCode();

            var responseText = await response.Content.ReadAsStringAsync();
            Debug.WriteLine($"📥 提交响应: {responseText.Substring(0, Math.Min(200, responseText.Length))}...");

            // 判断提交是否成功
            // 成功的响应可能包含："成功"
            bool isSuccess = responseText.Contains("成功");

            if (isSuccess)
            {
                return new ApiResultModel() { IsSuccess = true };
            }
            else
            {
                Debug.WriteLine($"❌ 回帖提交失败: {responseText.Substring(0, Math.Min(200, responseText.Length))}");
                return new ApiResultModel() { IsSuccess = false, Message = "回帖提交失败" };
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 提交回帖错误: {ex.Message}");
            return new ApiResultModel() { IsSuccess = false, Message = "提交回帖异常，请稍后重试" };
        }
    }

    /// <summary>
    /// Purchases an attachment by sending a POST request
    /// ✅ 新增：用于提交购买请求，现在包含从HTML中提取的所有必需参数
    /// </summary>
    public async Task<AttachmentPurchaseResult?> BuyAttachmentAsync(string attachmentId, string? formhash = null, string? referer = null, string? tid = null)
    {
        try
        {
            var url = $"{BaseUrl}/forum.php?mod=misc&action=attachpay";

            // 构建 POST 数据，包含所有必需的隐藏参数
            var postData = new Dictionary<string, string>
            {
                { "aid", attachmentId },
                { "paysubmit", "yes" }  // ✅ 改为 "yes" 而不是 "true"
            };

            // ✅ 新增：添加从购买确认页面提取的参数
            if (!string.IsNullOrEmpty(formhash))
            {
                postData["formhash"] = formhash;
                Debug.WriteLine($"📋 使用 FormHash: {formhash}");
            }

            if (!string.IsNullOrEmpty(referer))
            {
                postData["referer"] = referer;
                Debug.WriteLine($"📋 使用 Referer: {referer}");
            }

            if (!string.IsNullOrEmpty(tid))
            {
                postData["tid"] = tid;
                Debug.WriteLine($"📋 使用 Tid: {tid}");
            }

            var content = new FormUrlEncodedContent(postData);
            var response = await HttpClientManager.Instance.PostAsync(url, content);

            var responseContent = await response.Content.ReadAsStringAsync();

            // 检查响应内容，判断购买是否成功
            // 成功响应会包含 "购买成功" 或下载链接相关内容
            var isSuccess = responseContent.Contains("购买成功") || 
                           responseContent.Contains("开始下载") || 
                           responseContent.Contains("attachment&aid");

            Debug.WriteLine($"📋 购买响应状态: {(isSuccess ? "成功" : "失败")}");

            // ✅ 改进：如果购买失败，提取具体的失败原因
            var message = isSuccess 
                ? "购买成功" 
                : (ExtractErrorMessage(responseContent) ?? "购买失败，请检查PB币余额或重新尝试");

            return new AttachmentPurchaseResult
            {
                IsSuccess = isSuccess,
                Message = message,
                DownloadUrl = ExtractDownloadUrl(responseContent)
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 购买附件错误: {ex.Message}");
            return new AttachmentPurchaseResult
            {
                IsSuccess = false,
                Message = $"购买请求失败: {ex.Message}",
                DownloadUrl = null
            };
        }
    }

    /// <summary>
    /// 从响应内容中提取错误信息
    /// ✅ 新增：使用正则表达式从 alert_error div 中提取具体的失败原因
    /// </summary>
    private static string? ExtractErrorMessage(string responseContent)
    {
        try
        {
            // 匹配格式: <div id="messagetext" class="alert_error"><p>错误信息</p>
            var pattern = @"<div\s+id=""messagetext""\s+class=""alert_error"">.*?<p>(.*?)</p>";
            var match = System.Text.RegularExpressions.Regex.Match(responseContent, pattern, System.Text.RegularExpressions.RegexOptions.Singleline);

            if (match.Success)
            {
                var errorMsg = match.Groups[1].Value.Trim();
                Debug.WriteLine($"📋 提取到错误信息: {errorMsg}");
                return errorMsg;
            }

            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"⚠️ 提取错误信息失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 从响应内容中提取下载URL
    /// </summary>
    private static string? ExtractDownloadUrl(string responseContent)
    {
        try
        {
            // 查找下载链接，通常格式为: forum.php?mod=attachment&aid=xxxxx
            var pattern = @"class=""alert_btnleft""><a href=""(.*?)"">如果";
            var match = System.Text.RegularExpressions.Regex.Match(responseContent, pattern);

            if (match.Success)
            {
                // 将 &amp; 转换为 &
                return match.Groups[1].Value.Replace("&amp;", "&");
            }

            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"⚠️ 提取下载URL失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 提交评分/评价到论坛
    /// ✅ 新增：用于评分回帖
    /// </summary>
    public async Task<ApiResultModel> SubmitRatingAsync(string tid, string pid, string formhash, string referer, string score1, string? reason = null)
    {
        try
        {
            var url = $"{BaseUrl}/forum.php?mod=misc&action=rate&infloat=yes&ratesubmit=yes&inajax=1";

            var postData = new Dictionary<string, string>
            {
                { "formhash", formhash },
                { "tid", tid },
                { "pid", pid },
                { "referer", referer },
                { "handlekey", "rate" },
                { "score1", score1 }
            };

            // 如果提供了理由，加入到表单数据
            if (!string.IsNullOrEmpty(reason))
            {
                postData["reason"] = reason;
                Debug.WriteLine($"📝 添加评分理由: {reason}");
            }

            Debug.WriteLine($"⭐ 开始提交评分: tid={tid}, pid={pid}, score1={score1}");

            var content = new FormUrlEncodedContent(postData);
            var response = await HttpClientManager.Instance.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            var responseText = await response.Content.ReadAsStringAsync();
            Debug.WriteLine($"📥 评分响应: {responseText.Substring(0, Math.Min(200, responseText.Length))}");

            // 判断评分是否成功
            bool isSuccess = responseText.Contains("感谢您的参与，现在将转入评分前页面");

            if (isSuccess)
            {
                return new ApiResultModel() { IsSuccess = true };
            }
            else
            {
                Debug.WriteLine($"❌ 评分提交失败");
                return new ApiResultModel() { IsSuccess = false, Message = "评分提交失败" };
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 提交评分错误: {ex.Message}");
            return new ApiResultModel() { IsSuccess = false, Message = "提交评分异常，请稍后重试" };
        }
    }

    /// <summary>
    /// 获取评分表单数据
    /// ✅ 新增：用于获取评分表单，包括评分范围和今日剩余配额
    /// </summary>
    public async Task<RatingFormData?> GetRatingFormAsync(string ratingUrl)
    {
        try
        {
            if (!ratingUrl.StartsWith("http"))
            {
                ratingUrl = BaseUrl + "/" + ratingUrl.TrimStart('/');
            }

            Debug.WriteLine($"📋 获取评分表单: {ratingUrl}");

            var response = await HttpClientManager.Instance.GetAsync(ratingUrl);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();

            // 提取评分范围 和 今日剩余配额
            // 格式: <td>0 ~ 6</td><td>25</td>
            var scoreRangePattern = @"<td>(\d+)\s*~\s*(\d+)</td>\s*<td>(\d+)</td>";
            var scoreRangeMatch = System.Text.RegularExpressions.Regex.Match(content, scoreRangePattern);

            // 提取 formhash
            var formHashPattern = @"name=""formhash""\s+value=""([^""]+)""";
            var formHashMatch = System.Text.RegularExpressions.Regex.Match(content, formHashPattern);

            var formHash = formHashMatch.Success ? formHashMatch.Groups[1].Value : null;

            if (formHashMatch.Success)
            {
                Debug.WriteLine($"✅ 成功提取 formhash");
            }
            else
            {
                Debug.WriteLine($"⚠️ 未能提取 formhash，请检查 HTML 结构");
            }

            if (scoreRangeMatch.Success)
            {
                var minScore = scoreRangeMatch.Groups[1].Value;
                var maxScore = scoreRangeMatch.Groups[2].Value;
                var dailyRemaining = scoreRangeMatch.Groups[3].Value;

                Debug.WriteLine($"✅ 成功获取评分表单数据 (范围: {minScore}~{maxScore}, 今日剩余: {dailyRemaining})");

                return new RatingFormData
                {
                    MinScore = int.Parse(minScore),
                    MaxScore = int.Parse(maxScore),
                    DailyRemaining = int.TryParse(dailyRemaining, out var remaining) ? remaining : 0,
                    FormHash = formHash,
                    RawHtml = content
                };
            }
            else
            {
                Debug.WriteLine($"⚠️ 未能提取评分范围和今日剩余配额");

                return new RatingFormData
                {
                    MinScore = 0,
                    MaxScore = 6,
                    DailyRemaining = 0,
                    FormHash = formHash,
                    RawHtml = content
                };
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 获取评分表单错误: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 获取页面的formhash（从thread页面）
    /// ✅ 新增：用于获取点评提交所需的formhash
    /// </summary>
    public async Task<string?> GetFormHashFromThreadAsync(string threadUrl)
    {
        try
        {
            if (!threadUrl.StartsWith("http"))
            {
                threadUrl = BaseUrl + "/" + threadUrl.TrimStart('/');
            }

            Debug.WriteLine($"📋 获取页面formhash: {threadUrl}");

            var response = await HttpClientManager.Instance.GetAsync(threadUrl);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();

            // 提取 formhash - 多种可能的格式
            // 格式1: name="formhash" value="xxx"
            // 格式2: input type="hidden" name="formhash" value="xxx"
            var formHashPattern = @"name=[""']formhash[""']\s+value=[""']([^""']+)[""']";
            var formHashMatch = System.Text.RegularExpressions.Regex.Match(content, formHashPattern);

            if (!formHashMatch.Success)
            {
                // 尝试另一种格式
                formHashPattern = @"value=[""']([^""']+)[""']\s+name=[""']formhash[""']";
                formHashMatch = System.Text.RegularExpressions.Regex.Match(content, formHashPattern);
            }

            var formHash = formHashMatch.Success ? formHashMatch.Groups[1].Value : null;

            if (!string.IsNullOrEmpty(formHash))
            {
                Debug.WriteLine($"✅ 成功提取 formhash: {formHash.Substring(0, Math.Min(20, formHash.Length))}...");
                return formHash;
            }
            else
            {
                Debug.WriteLine($"⚠️ 未能提取 formhash");
                return null;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 获取formhash错误: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 提交点评（评论）到论坛
    /// ✅ 新增：用于提交对回帖的点评
    /// </summary>
    public async Task<ApiResultModel> SubmitCommentAsync(string tid, string pid, string formhash, string message)
    {
        try
        {
            // 根据提示词4.txt的API文档：
            // URL: https://bbs.pcbeta.com/forum.php?mod=post&action=reply&comment=yes&tid=2052074&pid=57153988&commentsubmit=yes&infloat=yes&inajax=1
            // POST参数: formhash, handlekey="comment", message
            var url = $"{BaseUrl}/forum.php?mod=post&action=reply&comment=yes&tid={tid}&pid={pid}&commentsubmit=yes&infloat=yes&inajax=1";

            var postData = new Dictionary<string, string>
            {
                { "formhash", formhash },
                { "handlekey", "comment" },
                { "message", message }
            };

            Debug.WriteLine($"💬 开始提交点评: tid={tid}, pid={pid}, message length={message.Length}");

            var content = new FormUrlEncodedContent(postData);
            var response = await HttpClientManager.Instance.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            var responseText = await response.Content.ReadAsStringAsync();
            Debug.WriteLine($"📥 点评响应: {responseText.Substring(0, Math.Min(200, responseText.Length))}");

            // 判断点评是否成功 - 根据提示词4.txt，成功响应包含 "点评成功"
            bool isSuccess = responseText.Contains("点评成功");

            if (isSuccess)
            {
                return new ApiResultModel() { IsSuccess = true };
            }
            else
            {
                Debug.WriteLine($"❌ 点评提交失败 - 响应中未包含'点评成功'标识");
                return new ApiResultModel() { IsSuccess = false, Message = "点评提交失败" };
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 提交点评错误: {ex.Message}");
            return new ApiResultModel() { IsSuccess = false, Message = "提交点评异常，请稍后重试" };
        }
    }

    /// <summary>
    /// ✅ 新增：获取用户通知页面的HTML
    /// 调用 https://i.pcbeta.com/home.php?mod=space&do=notice&inajax=1 接口
    /// ✅ 改进：添加 &inajax=1 参数用于AJAX请求
    /// </summary>
    public async Task<string> GetNoticesPageHtmlAsync(int page = 1)
    {
        try
        {
            // ✅ 关键：构建通知页面URL，包含 &inajax=1 参数用于AJAX请求
            // 格式：https://i.pcbeta.com/home.php?mod=space&do=notice&inajax=1
            string url = "https://i.pcbeta.com/home.php?mod=space&do=notice&inajax=1";

            // 如果不是第一页，添加分页参数
            if (page > 1)
            {
                url += $"&page={page}";
            }

            Debug.WriteLine($"📄 获取通知页面: {url}");

            var response = await HttpClientManager.Instance.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var html = await response.Content.ReadAsStringAsync();
            Debug.WriteLine($"✅ 成功获取通知页面HTML (长度: {html.Length})");
            return html;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 获取通知页面失败: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// ✅ 新增：获取任意页面的HTML内容
    /// 用于获取编辑页面等各种页面的内容
    /// </summary>
    public async Task<string> GetPageHtmlAsync(string url)
    {
        try
        {
            if (!url.StartsWith("http"))
            {
                url = BaseUrl + "/" + url.TrimStart('/');
            }

            Debug.WriteLine($"📄 获取页面HTML: {url.Substring(0, Math.Min(100, url.Length))}...");

            var response = await HttpClientManager.Instance.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            Debug.WriteLine($"✅ 获取页面HTML成功");
            return content;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 获取页面HTML失败: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// ✅ 新增：提交编辑回帖请求
    /// 调用 /forum.php?mod=post&action=edit&editsubmit=yes 接口
    /// 
    /// 根据 提示词7.txt 的API文档：
    /// 编辑URL: https://bbs.pcbeta.com/forum.php?mod=post&action=edit&extra=&editsubmit=yes
    /// 请求方式: POST
    /// 
    /// 编辑回帖携带参数：
    /// - formhash (从编辑页面获取)
    /// - posttime (发帖时间戳，从编辑页面获取)
    /// - wysiwyg = 1 (写死)
    /// - fid (论坛ID)
    /// - tid (主题ID)
    /// - pid (回帖ID)
    /// - subject (帖子标题)
    /// - message (帖子内容)
    /// - usesig (使用个人签名)
    /// - attachupdate[附件ID] (值为空，表示保留原附件)
    /// - attachnew[附件ID][description] (更新后的描述)
    /// - attachnew[附件ID][readperm] (更新后的阅读权限)
    /// - attachnew[附件ID][price] (更新后的售价)
    /// 
    /// 成功判定：响应包含 "帖子编辑成功"
    /// </summary>
    public async Task<ApiResultModel> SubmitEditReplyAsync(
        string forumId,
        string threadId,
        string postId,
        string message,
        string formhash,
        string posttime,
        List<AttachmentMetadata>? attachments = null
    )
    {
        try
        {
            if (string.IsNullOrEmpty(message))
            {
                return new ApiResultModel() { IsSuccess = false, Message = "回帖内容为空" };
            }

            if (string.IsNullOrEmpty(formhash))
            {
                return new ApiResultModel() { IsSuccess = false, Message = "hash校验失败" };
            }

            if (string.IsNullOrEmpty(posttime))
            {
                return new ApiResultModel() { IsSuccess = false, Message = "posttime校验失败" };
            }

            // 构建编辑表单数据
            var data = new Dictionary<string, string>
            {
                { "formhash", formhash },
                { "posttime", posttime },
                { "wysiwyg", "1" },
                { "fid", forumId },
                { "tid", threadId },
                { "pid", postId },
                { "subject", "" },  // 编辑回帖时subject可以为空
                { "message", message },
                { "usesig", "1" },
                { "editsubmit", "yes" }
            };

            // ✅ 新增：添加附件元数据参数
            if (attachments != null && attachments.Count > 0)
            {
                Debug.WriteLine($"📎 开始添加 {attachments.Count} 个附件的元数据参数");

                foreach (var attachment in attachments)
                {
                    if (string.IsNullOrEmpty(attachment.AttachmentId))
                    {
                        Debug.WriteLine($"⚠️ 跳过空的附件ID");
                        continue;
                    }

                    // 添加描述参数（所有类型都需要）
                    var descKey = $"attachnew[{attachment.AttachmentId}][description]";
                    data[descKey] = attachment.Description ?? "";

                    // ✅ 关键改进：仅当 IsAttachment=true 时才添加 readperm 和 price 参数
                    if (attachment.IsAttachment)
                    {
                        var readPermKey = $"attachnew[{attachment.AttachmentId}][readperm]";
                        var priceKey = $"attachnew[{attachment.AttachmentId}][price]";
                        data[readPermKey] = attachment.ReadPerm ?? "0";
                        data[priceKey] = attachment.Price ?? "0";
                        Debug.WriteLine($"✅ 已添加附件参数 (ID={attachment.AttachmentId}) - 附件类型");
                        Debug.WriteLine($"   - 描述: {attachment.Description}");
                        Debug.WriteLine($"   - 阅读权限: {attachment.ReadPerm}");
                        Debug.WriteLine($"   - 售价: {attachment.Price}");
                    }
                    else
                    {
                        Debug.WriteLine($"✅ 已添加图片参数 (ID={attachment.AttachmentId}) - 图片类型（不添加权限和售价）");
                        Debug.WriteLine($"   - 描述: {attachment.Description}");
                    }
                }
            }
            else
            {
                Debug.WriteLine($"📎 无附件数据");
            }

            var content = new FormUrlEncodedContent(data);
            var submitUrl = $"{BaseUrl}/forum.php?mod=post&action=edit&extra=&editsubmit=yes";

            Debug.WriteLine($"📝 开始提交编辑回帖: fid={forumId}, tid={threadId}, pid={postId}");
            Debug.WriteLine($"   - formhash: {formhash}");
            Debug.WriteLine($"   - posttime: {posttime}");

            var response = await HttpClientManager.Instance.PostAsync(submitUrl, content);
            response.EnsureSuccessStatusCode();

            var responseText = await response.Content.ReadAsStringAsync();
            Debug.WriteLine($"📥 编辑响应: {responseText.Substring(0, Math.Min(200, responseText.Length))}...");

            // 判断编辑是否成功
            // 成功的响应包含："帖子编辑成功"
            bool isSuccess = responseText.Contains("帖子编辑成功");

            if (isSuccess)
            {
                return new ApiResultModel() { IsSuccess = true };
            }
            else
            {
                Debug.WriteLine($"❌ 回帖编辑失败: {responseText.Substring(0, Math.Min(200, responseText.Length))}");
                return new ApiResultModel() { IsSuccess = false, Message = "回帖编辑失败" };
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 提交编辑回帖错误: {ex.Message}");
            return new ApiResultModel() { IsSuccess = false, Message = "提交编辑回帖异常，请稍后重试" };
        }
    }

    /// <summary>
    /// 提交编辑楼主发帖请求
    /// 调用 /forum.php?mod=post&action=edit 接口
    /// ✅ 新增：用于编辑楼主发帖（区别于编辑用户回帖的 SubmitEditReplyAsync）
    /// 
    /// 根据 提示词6.txt 的API文档：
    /// 编辑URL：https://bbs.pcbeta.com/forum.php?mod=post&action=edit&extra=&editsubmit=yes
    /// 
    /// 编辑楼主发帖携带参数：
    /// - formhash (从编辑页面获取)
    /// - posttime (发帖时间戳，从编辑页面获取)
    /// - wysiwyg = 1 (写死)
    /// - fid (论坛ID)
    /// - tid (主题ID)
    /// - pid (回帖ID - 楼主发帖)
    /// - subject (帖子标题 - 编辑楼主发帖需要)
    /// - message (帖子内容)
    /// - readperm (阅读权限，默认空值)
    /// - price (主题售价，默认为空)
    /// - tags (空值写死，要传空值，但必须添加该参数)
    /// - typeid (主题分类，默认0)
    /// - replycredit_times, replycredit_extcredits, replycredit_membertimes, replycredit_random (回帖奖励)
    /// - hiddenreplies, ordertype, allownoticeauthor, usesig (checkbox 选项)
    /// - attachupdate[附件ID] (值为空，表示保留原附件)
    /// - attachnew[附件ID][description] (更新后的描述)
    /// - attachnew[附件ID][readperm] (更新后的阅读权限)
    /// - attachnew[附件ID][price] (更新后的售价)
    /// 
    /// 成功判定：响应包含 "帖子编辑成功"
    /// </summary>
    public async Task<ApiResultModel> SubmitEditOPReplyAsync(
        string forumId,
        string threadId,
        string postId,
        string subject,
        string message,
        string formhash,
        string posttime,
        List<AttachmentMetadata>? attachments = null,
        // ✅ 新增：13个额外参数（来自 post_extra 表单）
        string replycreditTimes = "",
        string replycreditExtcredits = "",
        string replycreditMembertimes = "",
        string replycreditRandom = "",
        string readperm = "",
        string price = "",
        bool hiddenreplies = false,
        bool ordertype = false,
        bool allownoticeauthor = true,
        bool usesig = true,
        // ✅ 新增：typeid（主题分类）
        string typeid = "0"
    )
    {
        try
        {
            if (string.IsNullOrEmpty(subject))
            {
                return new ApiResultModel() { IsSuccess = false, Message = "帖子标题为空" };
            }

            if (string.IsNullOrEmpty(message))
            {
                return new ApiResultModel() { IsSuccess = false, Message = "帖子内容为空" };
            }

            if (string.IsNullOrEmpty(formhash))
            {
                return new ApiResultModel() { IsSuccess = false, Message = "formhash校验失败" };
            }

            if (string.IsNullOrEmpty(posttime))
            {
                return new ApiResultModel() { IsSuccess = false, Message = "posttime校验失败" };
            }

            if (string.IsNullOrEmpty(forumId))
            {
                return new ApiResultModel() { IsSuccess = false, Message = "forumId校验失败" };
            }

            // 构建编辑楼主发帖表单数据
            var data = new Dictionary<string, string>
            {
                { "formhash", formhash },
                { "posttime", posttime },
                { "wysiwyg", "1" },  // 写死
                { "fid", forumId },
                { "tid", threadId },
                { "pid", postId },
                { "subject", subject },  // ✅ 编辑楼主发帖需要标题
                { "message", message },
                { "readperm", readperm },  // ✅ 使用参数
                { "price", price },  // ✅ 使用参数
                { "tags", "" },  // 空值写死，要传空值
                { "typeid", typeid },  // ✅ 使用参数
                // ✅ 回帖奖励参数（使用传入的参数）
                { "replycredit_times", replycreditTimes },
                { "replycredit_extcredits", replycreditExtcredits },
                { "replycredit_membertimes", replycreditMembertimes },
                { "replycredit_random", replycreditRandom },
                // ✅ 基本属性 checkbox（转换 bool 为 "0" 或 "1"）
                { "hiddenreplies", hiddenreplies ? "1" : "0" },
                { "ordertype", ordertype ? "1" : "0" },
                { "allownoticeauthor", allownoticeauthor ? "1" : "0" },
                { "usesig", usesig ? "1" : "0" },
                { "editsubmit", "yes" }
            };

            // ✅ 新增：添加附件元数据参数
            if (attachments != null && attachments.Count > 0)
            {
                Debug.WriteLine($"📎 开始添加 {attachments.Count} 个附件的元数据参数");

                foreach (var attachment in attachments)
                {
                    if (string.IsNullOrEmpty(attachment.AttachmentId))
                    {
                        Debug.WriteLine($"⚠️ 跳过空的附件ID");
                        continue;
                    }

                    // 添加描述参数（所有类型都需要）
                    var descKey = $"attachnew[{attachment.AttachmentId}][description]";
                    data[descKey] = attachment.Description ?? "";

                    // ✅ 关键改进：仅当 IsAttachment=true 时才添加 readperm 和 price 参数
                    if (attachment.IsAttachment)
                    {
                        var readPermKey = $"attachnew[{attachment.AttachmentId}][readperm]";
                        var priceKey = $"attachnew[{attachment.AttachmentId}][price]";
                        data[readPermKey] = attachment.ReadPerm ?? "0";
                        data[priceKey] = attachment.Price ?? "0";
                        Debug.WriteLine($"✅ 已添加附件参数 (ID={attachment.AttachmentId}) - 附件类型");
                        Debug.WriteLine($"   - 描述: {attachment.Description}");
                        Debug.WriteLine($"   - 阅读权限: {attachment.ReadPerm}");
                        Debug.WriteLine($"   - 售价: {attachment.Price}");
                    }
                    else
                    {
                        Debug.WriteLine($"✅ 已添加图片参数 (ID={attachment.AttachmentId}) - 图片类型（不添加权限和售价）");
                        Debug.WriteLine($"   - 描述: {attachment.Description}");
                    }
                }
            }
            else
            {
                Debug.WriteLine($"📎 无附件数据");
            }

            var content = new FormUrlEncodedContent(data);
            var submitUrl = $"{BaseUrl}/forum.php?mod=post&action=edit&extra=&editsubmit=yes";

            Debug.WriteLine($"📝 开始提交编辑楼主发帖: fid={forumId}, tid={threadId}, pid={postId}");
            Debug.WriteLine($"   - 标题: {subject.Substring(0, Math.Min(50, subject.Length))}");
            Debug.WriteLine($"   - 内容长度: {message.Length} 字符");
            Debug.WriteLine($"   - formhash: {formhash}");
            Debug.WriteLine($"   - posttime: {posttime}");
            Debug.WriteLine($"   - typeid: {typeid}");

            var response = await HttpClientManager.Instance.PostAsync(submitUrl, content);
            response.EnsureSuccessStatusCode();

            var responseText = await response.Content.ReadAsStringAsync();
            Debug.WriteLine($"📥 编辑响应: {responseText.Substring(0, Math.Min(200, responseText.Length))}...");

            // 判断编辑是否成功
            // 成功的响应包含："帖子编辑成功"
            bool isSuccess = responseText.Contains("帖子编辑成功");

            if (isSuccess)
            {
                return new ApiResultModel() { IsSuccess = true };
            }
            else
            {
                Debug.WriteLine($"❌ 楼主发帖编辑失败: {responseText.Substring(0, Math.Min(200, responseText.Length))}");
                return new ApiResultModel() { IsSuccess = false, Message = "楼主发帖编辑失败" };
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 提交编辑楼主发帖错误: {ex.Message}");
            return new ApiResultModel() { IsSuccess = false, Message = "提交编辑楼主发帖异常，请稍后重试" };
        }
    }

    /// <summary>
    /// 提交发新帖请求
    /// 调用 /forum.php?mod=post&action=newthread 接口
    /// ✅ 改进：支持附件元数据参数 + 10个额外的表单参数
    /// 
    /// 根据 提示词5.txt 的API文档：
    /// 发帖URL：https://bbs.pcbeta.com/forum.php?mod=post&action=newthread&fid={fid}&extra=&topicsubmit=yes
    /// 
    /// 发帖携带参数：
    /// - formhash (从前序获取)
    /// - posttime (时间戳)
    /// - wysiwyg = 1 (写死)
    /// - subject (帖子标题)
    /// - message (帖子内容)
    /// - readperm (阅读权限，默认空值)
    /// - price (主题售价，默认为空，最高值5)
    /// - tags (空值写死，要传空值，但必须添加该参数)
    /// - typeid (主题分类，默认0)
    /// - replycredit_times, replycredit_extcredits, replycredit_membertimes, replycredit_random (回帖奖励)
    /// - hiddenreplies, ordertype, allownoticeauthor, usesig (checkbox 选项)

    /// <summary>
    /// Gets user profile information from the profile page
    /// URL: https://bbs.pcbeta.com/home.php?mod=space&do=profile
    /// </summary>
    public async Task<Models.UserProfileInfo?> GetUserProfileAsync()
    {
        try
        {
            var url = $"{BaseUrl}/home.php?mod=space&do=profile";
            var response = await HttpClientManager.Instance.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            LastPageContent = content;
            return _xmlParsingService.ParseUserProfile(content);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Get user profile error: {ex.Message}");
            return null;
        }
    }

    public async Task<ApiResultModel> PostNewThreadAsync(
        string forumId, 
        string subject, 
        string message, 
        string formhash, 
        List<AttachmentMetadata>? attachments = null,
        // ✅ 新增：10个额外参数（来自 post_extra 表单）
        string replycreditTimes = "",
        string replycreditExtcredits = "",
        string replycreditMembertimes = "",
        string replycreditRandom = "",
        string readperm = "",
        string price = "",
        bool hiddenreplies = false,
        bool ordertype = false,
        bool allownoticeauthor = true,
        bool usesig = true,
        // ✅ 新增：typeid（主题分类）
        string typeid = "0"
    )
    {
        try
        {
            if (string.IsNullOrEmpty(subject))
            {
                return new ApiResultModel() { IsSuccess = false, Message = "帖子标题为空" };
            }

            if (string.IsNullOrEmpty(message))
            {
                return new ApiResultModel() { IsSuccess = false, Message = "帖子内容为空" };
            }

            if (string.IsNullOrEmpty(formhash))
            {
                return new ApiResultModel() { IsSuccess = false, Message = "formhash校验失败" };
            }

            if (string.IsNullOrEmpty(forumId))
            {
                return new ApiResultModel() { IsSuccess = false, Message = "forumId校验失败" };
            }

            // 获取当前时间戳
            var posttime = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

            // 构建发帖表单数据
            var data = new Dictionary<string, string>
            {
                { "formhash", formhash },
                { "posttime", posttime },
                { "wysiwyg", "1" },  // 写死
                { "subject", subject },
                { "message", message },
                { "readperm", readperm },  // ✅ 使用参数
                { "price", price },  // ✅ 使用参数
                { "tags", "" },  // 空值写死，要传空值
                { "typeid", typeid },  // ✅ 改为：使用传入的参数（不再硬编码 "0"）
                // ✅ 回帖奖励参数（使用传入的参数）
                { "replycredit_times", replycreditTimes },
                { "replycredit_extcredits", replycreditExtcredits },
                { "replycredit_membertimes", replycreditMembertimes },
                { "replycredit_random", replycreditRandom },
                // ✅ 基本属性 checkbox（转换 bool 为 "0" 或 "1"）
                { "hiddenreplies", hiddenreplies ? "1" : "0" },
                { "ordertype", ordertype ? "1" : "0" },
                { "allownoticeauthor", allownoticeauthor ? "1" : "0" },
                { "usesig", usesig ? "1" : "0" }
            };

            // ✅ 新增：添加附件元数据参数
            if (attachments != null && attachments.Count > 0)
            {
                Debug.WriteLine($"📎 开始添加 {attachments.Count} 个附件的元数据参数");

                foreach (var attachment in attachments)
                {
                    if (string.IsNullOrEmpty(attachment.AttachmentId))
                    {
                        Debug.WriteLine($"⚠️ 跳过空的附件ID");
                        continue;
                    }

                    // 添加描述参数（所有类型都需要）
                    var descKey = $"attachnew[{attachment.AttachmentId}][description]";
                    data[descKey] = attachment.Description ?? "";

                    // ✅ 关键改进：仅当 IsAttachment=true 时才添加 readperm 和 price 参数
                    if (attachment.IsAttachment)
                    {
                        var readPermKey = $"attachnew[{attachment.AttachmentId}][readperm]";
                        var priceKey = $"attachnew[{attachment.AttachmentId}][price]";
                        data[readPermKey] = attachment.ReadPerm ?? "0";
                        data[priceKey] = attachment.Price ?? "0";
                        Debug.WriteLine($"✅ 已添加附件参数 (ID={attachment.AttachmentId}) - 附件类型");
                        Debug.WriteLine($"   - 描述: {attachment.Description}");
                        Debug.WriteLine($"   - 阅读权限: {attachment.ReadPerm}");
                        Debug.WriteLine($"   - 售价: {attachment.Price}");
                    }
                    else
                    {
                        Debug.WriteLine($"✅ 已添加图片参数 (ID={attachment.AttachmentId}) - 图片类型（不添加权限和售价）");
                        Debug.WriteLine($"   - 描述: {attachment.Description}");
                    }
                }
            }
            else
            {
                Debug.WriteLine($"📎 无附件数据");
            }

            var content = new FormUrlEncodedContent(data);
            var submitUrl = $"{BaseUrl}/forum.php?mod=post&action=newthread&fid={forumId}&extra=&infloat=yes&topicsubmit=yes";

            Debug.WriteLine($"📝 开始提交新帖: fid={forumId}");
            Debug.WriteLine($"   - 标题: {subject.Substring(0, Math.Min(50, subject.Length))}");
            Debug.WriteLine($"   - 内容长度: {message.Length} 字符");

            var response = await HttpClientManager.Instance.PostAsync(submitUrl, content);
            response.EnsureSuccessStatusCode();

            var responseText = await response.Content.ReadAsStringAsync();
            Debug.WriteLine($"📥 提交响应: {responseText.Substring(0, Math.Min(200, responseText.Length))}...");

            // 判断提交是否成功
            // 成功的响应包含："您的主题已发布"
            bool isSuccess = responseText.Contains("您的主题已发布");

            if (isSuccess)
            {
                return new ApiResultModel() { IsSuccess = true };
            }
            else
            {
                Debug.WriteLine($"❌ 新帖发布失败: {responseText.Substring(0, Math.Min(200, responseText.Length))}");
                return new ApiResultModel() { IsSuccess = false, Message = "新帖发布失败" };
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 提交新帖错误: {ex.Message}");
            return new ApiResultModel() { IsSuccess = false, Message = "提交新帖异常，请稍后重试" };
        }
    }
}

/// <summary>
/// 附件购买结果
/// ✅ 新增：用于返回购买操作的结果
/// </summary>
public class AttachmentPurchaseResult
{
    /// <summary>购买是否成功</summary>
    public bool IsSuccess { get; set; }

    /// <summary>返回消息（成功或错误信息）</summary>
    public string? Message { get; set; }

    /// <summary>下载URL（如果购买成功且可立即下载）</summary>
    public string? DownloadUrl { get; set; }
}

/// <summary>
/// 附件购买表单参数
/// ✅ 新增：用于存储从购买确认页面提取的隐藏参数
/// </summary>
public class AttachmentPurchaseParams
{
    /// <summary>表单哈希值，用于防止CSRF攻击</summary>
    public string? FormHash { get; set; }

    /// <summary>引用来源URL，用于验证请求的来源</summary>
    public string? Referer { get; set; }

    /// <summary>附件ID</summary>
    public string? Aid { get; set; }

    /// <summary>交易ID（可选）</summary>
    public string? Tid { get; set; }
}

/// <summary>
/// 文件上传结果（用于回帖中的图片和附件上传）
/// ✅ 新增：用于返回文件上传操作的结果
/// </summary>
public class UploadResult
{
    /// <summary>上传后的文件URL（可能与上传接口URL相同）</summary>
    public string? Url { get; set; }

    /// <summary>服务器返回的附件ID（用于删除或其他操作）</summary>
    public string? AttachmentId { get; set; }

    /// <summary>上传的文件名</summary>
    public string? FileName { get; set; }

    /// <summary>文件大小（字节数）</summary>
    public string? FileSize { get; set; }
}

/// <summary>
/// 附件元数据信息
/// ✅ 新增：用于存储附件的描述、阅读权限、售价等信息
/// </summary>
public class AttachmentMetadata
{
    /// <summary>附件ID（从上传时获得）</summary>
    public string AttachmentId { get; set; } = string.Empty;

    /// <summary>附件描述</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>阅读权限（0=所有人, 其他值表示特定权限等级）</summary>
    public string ReadPerm { get; set; } = "0";

    /// <summary>附件售价（金币数，0表示免费）</summary>
    public string Price { get; set; } = "0";

    /// <summary>✅ 新增：是否为附件（true=附件，false=图片）</summary>
    public bool IsAttachment { get; set; } = true;
}


/// <summary>
/// 评分表单数据
/// ✅ 新增：用于存储评分表单的信息
/// </summary>
public class RatingFormData
{
    /// <summary>最小评分值</summary>
    public int MinScore { get; set; } = 0;

    /// <summary>最大评分值</summary>
    public int MaxScore { get; set; } = 6;

    /// <summary>今日剩余配额</summary>
    public int DailyRemaining { get; set; }

    /// <summary>表单Hash值（用于提交评分）</summary>
    public string? FormHash { get; set; }

    /// <summary>原始HTML内容（用于调试或提取其他信息）</summary>
    public string? RawHtml { get; set; }
}
