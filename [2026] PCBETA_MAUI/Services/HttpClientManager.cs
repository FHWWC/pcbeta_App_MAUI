using System.Net.Http;
using System.Diagnostics;

namespace PCBetaMAUI.Services;

/// <summary>
/// 全局 HttpClient 管理
/// 因为接口调用不返回 Auth Token，所以在应用程序生命周期内使用同一个 HttpClient
/// 通过 CookieContainer 管理用户会话
/// </summary>
public static class HttpClientManager
{
    //  改为可变的引用，支持重新创建
    private static HttpClientWithCookieManagement _httpClientWrapper = CreateNewHttpClient();

    /// <summary>
    /// 获取全局 HttpClient 实例的包装对象
    /// </summary>
    public static HttpClientWithCookieManagement Instance => _httpClientWrapper;

    /// <summary>
    /// 创建全新的 HttpClient 和 Handler
    /// </summary>
    private static HttpClientWithCookieManagement CreateNewHttpClient()
    {
        var handler = new HttpClientHandler
        {
            UseCookies = true,
            CookieContainer = new System.Net.CookieContainer(),
            AllowAutoRedirect = true,
            //  自动处理各种内容编码
            AutomaticDecompression = System.Net.DecompressionMethods.All
        };

        InitializeJavaScriptVerificationCookies(handler.CookieContainer);

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        // 根据运行平台设置 User-Agent
#if ANDROID
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (X11; Linux aarch64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/154.0.0.0 Safari/537.36 CrKey/1.54.250320");
        //client.DefaultRequestHeaders.Add("sec-ch-ua-mobile", "?0");
        //client.DefaultRequestHeaders.Add("sec-ch-ua-platform", "Windows");
#else
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/152.0.0.0 Safari/537.36");
#endif

        //  添加Accept-Encoding头部
        client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
        //  添加Accept-Language头部
        client.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9");

        return new HttpClientWithCookieManagement(client, handler);
    }

    /// <summary>
    /// 初始化服务端 JavaScript 校验脚本需要的 Cookie。
    /// HttpClient 不执行 JavaScript，因此手动写入脚本会设置的校验值。
    /// </summary>
    private static void InitializeJavaScriptVerificationCookies(System.Net.CookieContainer cookieContainer)
    {
        var uri = new Uri("https://pcbeta.com");
        cookieContainer.Add(uri, new System.Net.Cookie("access_js_verified", "1", "/", "pcbeta.com"));
        cookieContainer.Add(uri, new System.Net.Cookie("access_js_platform", "Win32", "/", "pcbeta.com"));

        Debug.WriteLine("✅ 已初始化 pcbeta.com 域 JavaScript 校验 Cookie");
    }

    /// <summary>
    /// ⚠️ 重置 HttpClient（创建全新实例）
    /// 这是最彻底的方式清除所有 Cookie 和会话信息
    /// </summary>
    public static void ResetHttpClient()
    {
        try
        {
            Debug.WriteLine("⚠️ 正在重置 HttpClient...");

            // 释放旧的 HttpClient 和 Handler
            _httpClientWrapper?.Dispose();

            // 创建全新的 HttpClient 和 Handler
            _httpClientWrapper = CreateNewHttpClient();

            Debug.WriteLine(" HttpClient 已重置，所有 Cookie 已清除");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 重置 HttpClient 时出错: {ex.Message}");
        }
    }
}

/// <summary>
/// HttpClient 包装器，提供Cookie管理功能
/// </summary>
public class HttpClientWithCookieManagement : IDisposable
{
    private const string LoginPageUrl = "https://bbs.pcbeta.com/member.php?mod=logging&action=login";
    private const string AccessReviewUrl = "https://bbs.pcbeta.com/__access_review";
    private readonly HttpClient _httpClient;
    private readonly HttpClientHandler _handler;
    private readonly Task _environmentVerificationTask;
    private bool _disposed = false;

    public HttpClientWithCookieManagement(HttpClient httpClient, HttpClientHandler handler)
    {
        _httpClient = httpClient;
        _handler = handler;
        _environmentVerificationTask = VerifyEnvironmentAsync();
    }

    /// <summary>
    /// 获取基础 HttpClient 实例
    /// </summary>
    public HttpClient Client => _httpClient;

    /// <summary>
    /// 执行 GET 请求
    /// </summary>
    private async Task VerifyEnvironmentAsync()
    {
        try
        {
            using (var pageResponse = await _httpClient.GetAsync(LoginPageUrl))
            {
                await pageResponse.Content.ReadAsStringAsync();
                pageResponse.EnsureSuccessStatusCode();
            }

            var loginUri = new Uri(LoginPageUrl);
            var challengeCookie = _handler.CookieContainer
                .GetCookies(loginUri)
                .Cast<System.Net.Cookie>()
                .FirstOrDefault(cookie => cookie.Name == "access_env_challenge");

            if (challengeCookie == null)
            {
                throw new InvalidOperationException("访问登录页后未获取 access_env_challenge Cookie");
            }

            using var reviewContent = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["action"] = "environment",
                ["platform"] = "Win32",
                ["webdriver"] = "0",
                ["headless"] = "0",
                ["cdp"] = "1"
            });

            using var reviewResponse = await _httpClient.PostAsync(AccessReviewUrl, reviewContent);
            await reviewResponse.Content.ReadAsStringAsync();
            reviewResponse.EnsureSuccessStatusCode();

            var verifiedCookie = _handler.CookieContainer
                .GetCookies(new Uri(AccessReviewUrl))
                .Cast<System.Net.Cookie>()
                .FirstOrDefault(cookie => cookie.Name == "access_env_verified");

            if (verifiedCookie == null)
            {
                throw new InvalidOperationException("环境审核响应未获取 access_env_verified Cookie");
            }

            Debug.WriteLine("✅ PCBETA 环境验证完成，已获取 access_env_verified Cookie");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ PCBETA 环境验证失败: {ex.Message}");
            throw;
        }
    }

    public async Task<HttpResponseMessage> GetAsync(string requestUri)
    {
        await _environmentVerificationTask;
        return await _httpClient.GetAsync(requestUri);
    }

    /// <summary>
    /// 下载二进制内容
    /// </summary>
    public async Task<byte[]> GetByteArrayAsync(string requestUri)
    {
        try
        {
            await _environmentVerificationTask;
            using var response = await _httpClient.GetAsync(requestUri);
            var responseBytes = await response.Content.ReadAsByteArrayAsync();
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "未提供";

            Debug.WriteLine($"📥 二进制请求: {requestUri}");
            Debug.WriteLine($"   状态码: {(int)response.StatusCode} {response.ReasonPhrase}");
            Debug.WriteLine($"   Content-Type: {contentType}");
            Debug.WriteLine($"   响应长度: {responseBytes.Length} bytes");

            var responsePreview = GetResponsePreview(responseBytes);
            if (!response.IsSuccessStatusCode)
            {
                Debug.WriteLine($"❌ 请求失败，响应内容预览: {responsePreview}");
                throw new HttpRequestException(
                    $"下载失败: {(int)response.StatusCode} {response.ReasonPhrase}; URL={requestUri}; Content-Type={contentType}");
            }

            if (IsJavaScriptVerificationResponse(responsePreview))
            {
                Debug.WriteLine($"❌ 检测到 JavaScript 校验页面，未返回图片: {requestUri}");
                Debug.WriteLine($"   校验页内容预览: {responsePreview}");
                throw new InvalidOperationException(
                    $"服务器返回 JavaScript 校验页面而非二进制图片: {requestUri}");
            }

            if (responseBytes.Length == 0)
            {
                Debug.WriteLine($"⚠️ 请求成功但响应为空: {requestUri}");
            }

            return responseBytes;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ GetByteArrayAsync 异常: URL={requestUri}");
            Debug.WriteLine($"   异常类型: {ex.GetType().FullName}");
            Debug.WriteLine($"   异常信息: {ex.Message}");
            Debug.WriteLine($"   InnerException: {ex.InnerException?.Message ?? "无"}");
            throw;
        }
    }

    private static string GetResponsePreview(byte[] responseBytes)
    {
        if (responseBytes.Length == 0)
        {
            return "<空响应>";
        }

        var previewLength = Math.Min(responseBytes.Length, 512);
        var preview = System.Text.Encoding.UTF8.GetString(responseBytes, 0, previewLength)
            .Replace("\r", " ")
            .Replace("\n", " ");
        return preview.Length > 300 ? preview[..300] : preview;
    }

    private static bool IsJavaScriptVerificationResponse(string responsePreview)
    {
        return responsePreview.Contains("请启用 JavaScript", StringComparison.OrdinalIgnoreCase) ||
               responsePreview.Contains("access_js_verified", StringComparison.OrdinalIgnoreCase) ||
               responsePreview.Contains("access_js_platform", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 执行 POST 请求
    /// </summary>
    public async Task<HttpResponseMessage> PostAsync(string requestUri, HttpContent content)
    {
        await _environmentVerificationTask;
        return await _httpClient.PostAsync(requestUri, content);
    }

    /// <summary>
    /// 使用指定的 HttpRequestMessage 发送请求
    /// </summary>
    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
    {
        await _environmentVerificationTask;
        return await _httpClient.SendAsync(request);
    }

    /// <summary>
    /// 执行 HEAD 请求
    /// </summary>
    public async Task<HttpResponseMessage> HeadAsync(string requestUri)
    {
        await _environmentVerificationTask;
        var request = new HttpRequestMessage(HttpMethod.Head, requestUri);
        return await _httpClient.SendAsync(request);
    }

    /// <summary>
    /// 清除所有保存的 Cookie
    /// ⚠️ 用于退出登录时调用，清除用户会话
    /// 
    /// 这个方法使用两种策略：
    /// 1. 尝试标记所有 Cookie 为过期（某些情况下可能无效）
    /// 2. 创建新的 CookieContainer（部分清除）
    /// 
    /// 最彻底的方法：调用 HttpClientManager.ResetHttpClient() 创建全新的 HttpClient
    /// </summary>
    public void ClearCookies()
    {
        try
        {
            if (_handler?.CookieContainer != null)
            {
                //  策略1：获取所有域的 Cookie 并标记为过期
                try
                {
                    var domains = new[] { "bbs.pcbeta.com", "i.pcbeta.com", "pcbeta.com", ".pcbeta.com" };
                    int totalCleared = 0;

                    foreach (var domain in domains)
                    {
                        try
                        {
                            var uri = new Uri($"https://{domain}");
                            var cookies = _handler.CookieContainer.GetCookies(uri);

                            foreach (System.Net.Cookie cookie in cookies)
                            {
                                cookie.Expired = true;
                                totalCleared++;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"⚠️ 清除 {domain} 的 Cookie 时出错: {ex.Message}");
                        }
                    }

                    Debug.WriteLine($"📋 已标记 {totalCleared} 个 Cookie 为过期");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"⚠️ 标记 Cookie 过期失败: {ex.Message}");
                }

                //  策略2：创建全新的 CookieContainer（这会清除所有 Cookie）
                try
                {
                    _handler.CookieContainer = new System.Net.CookieContainer();
                    Debug.WriteLine(" 已创建新的 CookieContainer");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"❌ 创建新 CookieContainer 失败: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 清除 Cookie 时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 清除特定域名的 Cookie
    /// </summary>
    public void ClearCookiesForDomain(string domain)
    {
        try
        {
            if (_handler?.CookieContainer != null)
            {
                // 获取指定域的所有 Cookie
                var uri = new Uri($"https://{domain}");
                var cookies = _handler.CookieContainer.GetCookies(uri);

                Debug.WriteLine($"📋 准备清除域 {domain} 的 {cookies.Count} 个 Cookie");

                // 遍历并删除所有 Cookie
                foreach (System.Net.Cookie cookie in cookies)
                {
                    cookie.Expired = true;
                }

                Debug.WriteLine($" 已清除域 {domain} 的 Cookie");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 清除特定域 Cookie 时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取 CookieContainer（用于调试或高级操作）
    /// </summary>
    public System.Net.CookieContainer? GetCookieContainer()
    {
        return _handler?.CookieContainer;
    }

    /// <summary>
    /// 获取当前保存的所有 Cookie 数量（用于调试）
    /// </summary>
    public int GetCookieCount()
    {
        try
        {
            if (_handler?.CookieContainer == null)
                return 0;

            var domains = new[] { "bbs.pcbeta.com", "i.pcbeta.com", "pcbeta.com" };
            int totalCount = 0;

            foreach (var domain in domains)
            {
                try
                {
                    var uri = new Uri($"https://{domain}");
                    var cookies = _handler.CookieContainer.GetCookies(uri);
                    totalCount += cookies.Count;
                }
                catch { }
            }

            return totalCount;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        try
        {
            _handler?.Dispose();
            _httpClient?.Dispose();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"⚠️ 释放资源时出错: {ex.Message}");
        }

        _disposed = true;
    }
}
