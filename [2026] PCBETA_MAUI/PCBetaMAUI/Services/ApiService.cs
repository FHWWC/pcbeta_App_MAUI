using System.Diagnostics;

namespace PCBetaMAUI.Services;

/// <summary>
/// Service for making API calls to the PCBETA forum using the shared HttpClient
/// </summary>
public class ApiService
{
    private const string BaseUrl = "https://bbs.pcbeta.com";
    private readonly XmlParsingService _xmlParsingService;

    /// <summary>
    /// Stores the last retrieved page content for login status checking
    /// </summary>
    public string LastPageContent { get; private set; } = string.Empty;

    public ApiService()
    {
        _xmlParsingService = new XmlParsingService();
    }

    /// <summary>
    /// Attempts login with username and password
    /// ✅ 改进：返回元组 (isSuccess, errorMessage)，支持返回具体的错误信息
    /// </summary>
    public async Task<(bool isSuccess, string? errorMessage)> LoginAsync(string username, string password, string? questionId = null, string? answer = null)
    {
        try
        {
            var loginUrl = $"{BaseUrl}/member.php?mod=logging&action=login&username={Uri.EscapeDataString(username)}&password={Uri.EscapeDataString(password)}&loginsubmit=yes&loginhash=L"+ GenerateRandomCode() + "&inajax=1";

            if (!string.IsNullOrEmpty(questionId) && !string.IsNullOrEmpty(answer))
            {
                loginUrl += $"&questionid={Uri.EscapeDataString(questionId)}&answer={Uri.EscapeDataString(answer)}";
            }

            var response = await HttpClientManager.Instance.GetAsync(loginUrl);
            response.EnsureSuccessStatusCode();

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
    /// </summary>
    public async Task<List<Models.ThreadInfo>> GetThreadListAsync(string boardId, int page = 1)
    {
        try
        {
            var url = $"{BaseUrl}/forum-{boardId}-{page}.html?inajax=1";
            var response = await HttpClientManager.Instance.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
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
            return _xmlParsingService.ParseThreadContent(content);
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
            return response.IsSuccessStatusCode;
        }
        catch
        {
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
