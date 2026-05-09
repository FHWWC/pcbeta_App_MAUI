using System.Net.Http;
using System.Diagnostics;

namespace PCBetaMAUI.Services;

/// <summary>
/// 全局 HttpClient 管理器
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

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        //  更完整的User-Agent（模仿Chrome浏览器）
        client.DefaultRequestHeaders.Add("User-Agent", 
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/144.0.0.0 Safari/537.36");

        //  添加Accept-Encoding头部
        client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");

        //  添加Accept-Language头部
        client.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9");

        return new HttpClientWithCookieManagement(client, handler);
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
    private readonly HttpClient _httpClient;
    private readonly HttpClientHandler _handler;
    private bool _disposed = false;

    public HttpClientWithCookieManagement(HttpClient httpClient, HttpClientHandler handler)
    {
        _httpClient = httpClient;
        _handler = handler;
    }

    /// <summary>
    /// 获取基础 HttpClient 实例
    /// </summary>
    public HttpClient Client => _httpClient;

    /// <summary>
    /// 执行 GET 请求
    /// </summary>
    public async Task<HttpResponseMessage> GetAsync(string requestUri)
    {
        return await _httpClient.GetAsync(requestUri);
    }

    /// <summary>
    /// 执行 POST 请求
    /// </summary>
    public async Task<HttpResponseMessage> PostAsync(string requestUri, HttpContent content)
    {
        return await _httpClient.PostAsync(requestUri, content);
    }

    /// <summary>
    /// 执行 HEAD 请求
    /// </summary>
    public async Task<HttpResponseMessage> HeadAsync(string requestUri)
    {
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
