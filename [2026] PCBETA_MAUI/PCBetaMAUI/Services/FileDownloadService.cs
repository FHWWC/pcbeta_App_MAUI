using System.Diagnostics;
using System.Net.Http;
using CommunityToolkit.Maui.Storage;

namespace PCBetaMAUI.Services;

/// <summary>
/// 文件下载服务 - 使用 MAUI 社区工具包的 FileSaver
/// 支持下载论坛附件和其他资源
/// </summary>
public class FileDownloadService
{
    private readonly HttpClientWithCookieManagement _httpClientWrapper;
    private readonly HttpClient _httpClient;

    public FileDownloadService()
    {
        _httpClientWrapper = HttpClientManager.Instance;
        _httpClient = _httpClientWrapper.Client;
    }

    /// <summary>
    /// 下载文件并保存到本地（使用 FileSaver）
    ///  修复：正确处理ZIP等二进制文件下载，解决文件损坏问题
    /// 
    /// 使用建议：
    /// 1. 简单下载：DownloadAndSaveFileAsync(url) - 使用论坛主页作为Referer
    /// 2. 从特定帖子下载：DownloadAndSaveFileAsync(url, fileName, "https://bbs.pcbeta.com/forum.php?mod=viewthread&tid=...") - 使用帖子页面作为Referer
    /// </summary>
    public async Task<bool> DownloadAndSaveFileAsync(string url, string? suggestedFileName = null, string? referer = null)
    {
        if (string.IsNullOrEmpty(url))
        {
            await ShowError("错误", "无法获取下载链接");
            return false;
        }

        try
        {
            // 验证 URL 是否有效
            if (!IsValidUrl(url))
            {
                await ShowError("错误", "无效的下载链接");
                return false;
            }

            Debug.WriteLine($"📥 开始下载: {url}");

            //  修复1: 使用 HttpCompletionOption.ResponseHeadersRead 避免自动缓冲
            // 这样可以让我们直接处理响应流，避免被自动解压或修改
            var request = new HttpRequestMessage(HttpMethod.Get, url);

            //  新增：添加Referer头部（浏览器会自动添加）
            // 这对需要验证来源的服务器很重要
            if (!string.IsNullOrEmpty(referer))
            {
                request.Headers.Referrer = new Uri(referer);
            }
            else
            {
                // 如果没有指定referer，使用论坛主页作为默认值
                request.Headers.Referrer = new Uri("https://bbs.pcbeta.com/forum.php");
            }

            //  新增：添加更完整的Accept头部
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("*/*"));

            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            if (!response.IsSuccessStatusCode)
            {
                await ShowError("错误", $"下载失败: HTTP {(int)response.StatusCode}");
                Debug.WriteLine($"❌ HTTP 错误: {response.StatusCode}");
                return false;
            }

            //  修复2: 记录响应信息用于调试
            var contentLength = response.Content.Headers.ContentLength;
            var contentType = response.Content.Headers.ContentType?.MediaType;
            Debug.WriteLine($"📊 Content-Length: {contentLength}");
            Debug.WriteLine($"📊 Content-Type: {contentType}");
            Debug.WriteLine($"📊 Content-Encoding: {string.Join(", ", response.Content.Headers.ContentEncoding)}");

            //  新增详细日志：输出所有响应头用于诊断
            Debug.WriteLine("📋 响应头详情:");
            foreach (var header in response.Headers)
            {
                Debug.WriteLine($"   {header.Key}: {string.Join(", ", header.Value)}");
            }
            var contentStream2 = await response.Content.ReadAsStringAsync();
            Debug.WriteLine($"💾 返回的字符串: {contentStream2}");

            //  新增检查：如果Content-Type是HTML，可能返回的是错误页面或登录页面
            if (contentType?.Contains("text/html", StringComparison.OrdinalIgnoreCase) == true)
            {
                Debug.WriteLine("⚠️ 警告：服务器返回HTML");

                //  尝试从HTML中提取重定向URL（处理JavaScript重定向情况）
                string? redirectUrl = ExtractRedirectUrlFromHtml(contentStream2);

                if (!string.IsNullOrEmpty(redirectUrl))
                {
                    Debug.WriteLine($"🔄 检测到重定向URL: {redirectUrl}");

                    //  递归调用下载新的URL
                    // 使用相同的referer继续下载
                    return await DownloadAndSaveFileAsync(redirectUrl, suggestedFileName, referer);
                }

                Debug.WriteLine("🔍 可能原因:");
                Debug.WriteLine("   1. URL已过期或附件已被删除");
                Debug.WriteLine("   2. 需要从特定帖子页面访问（需要正确的Referer）");
                Debug.WriteLine("   3. 需要重新登录或认证");
                Debug.WriteLine("   4. 权限不足");
                Debug.WriteLine($"📌 当前Referer: {request.Headers.Referrer}");
                Debug.WriteLine($"📌 建议: 尝试从浏览器打开此URL验证是否有效");

                await ShowError("错误", 
                    "无法下载文件：\n\n" +
                    "• URL可能已失效\n" +
                    "• 需要重新登录\n" +
                    "• 权限不足\n\n" +
                    "请在浏览器中验证此URL是否有效");
                return false;
            }

            //  修复3: 使用 ReadAsStreamAsync 读取原始流
            // 关键：这样不会自动解压或修改内容
            var contentStream = await response.Content.ReadAsStreamAsync();
            try
            {
                //  修复4: 创建内存流副本以避免流的Length问题
                // 注意：HttpBaseStream 不支持 Length 属性，所以我们先复制到内存流
                var memoryStream = new MemoryStream();
                await contentStream.CopyToAsync(memoryStream);

                // 现在检查内存流是否为空
                if (memoryStream.Length == 0)
                {
                    await ShowError("错误", "下载内容为空");
                    return false;
                }

                memoryStream.Position = 0;  //  重置流位置到开始

                // 如果没有提供文件名，从 URL 或 Content-Disposition 中提取
                if (string.IsNullOrEmpty(suggestedFileName))
                {
                    suggestedFileName = ExtractFileNameFromUrl(url);

                    // 尝试从 Content-Disposition 头获取文件名
                    if (response.Content.Headers.ContentDisposition?.FileName != null)
                    {
                        suggestedFileName = response.Content.Headers.ContentDisposition.FileName.Trim('"');
                    }
                }

                Debug.WriteLine($"💾 保存文件名: {suggestedFileName}");
                Debug.WriteLine($"📦 流数据大小: {memoryStream.Length} 字节");

                //  修复5: 验证文件内容（对于ZIP文件）
                if (suggestedFileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    if (!IsValidZipContent(memoryStream))
                    {
                        await ShowError("错误", "下载的文件似乎不是有效的ZIP文件");
                        Debug.WriteLine("❌ ZIP文件签名验证失败");
                        return false;
                    }
                    memoryStream.Position = 0;  // 重置位置
                }

                // 使用 FileSaver 保存文件
                var fileSaverResult = await FileSaver.SaveAsync(suggestedFileName, memoryStream, CancellationToken.None);

                if (fileSaverResult.IsSuccessful)
                {
                    await Application.Current.Windows[0].Page.DisplayAlertAsync(
                        "成功",
                        $"文件已保存: {suggestedFileName}\n大小: {FormatFileSize(memoryStream.Length)}",
                        "确定");

                    Debug.WriteLine($" 文件下载成功: {suggestedFileName}");
                    Debug.WriteLine($"   路径: {fileSaverResult.FilePath}");
                    Debug.WriteLine($"   大小: {FormatFileSize(memoryStream.Length)}");
                    return true;
                }
                else
                {
                    await ShowError("错误", $"文件保存失败: {fileSaverResult.Exception?.Message}");
                    Debug.WriteLine($"❌ FileSaver 错误: {fileSaverResult.Exception?.Message}");
                    return false;
                }
            }
            finally
            {
                contentStream?.Dispose();
            }
        }
        catch (HttpRequestException ex)
        {
            await ShowError("网络错误", $"连接失败: {ex.Message}");
            Debug.WriteLine($"❌ 下载网络错误: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            await ShowError("错误", $"下载失败: {ex.Message}");
            Debug.WriteLine($"❌ 下载错误: {ex.Message}\n{ex.StackTrace}");
            return false;
        }
    }

    /// <summary>
    ///  新增：验证 ZIP 文件的有效性
    /// ZIP 文件必须以特定的魔法数字开头：0x50 0x4B 0x03 0x04 (PK♥♦)
    /// </summary>
    private bool IsValidZipContent(MemoryStream stream)
    {
        try
        {
            if (stream.Length < 4)
                return false;

            // ZIP 文件的标准签名
            byte[] zipSignature = { 0x50, 0x4B, 0x03, 0x04 };
            byte[] buffer = new byte[4];

            stream.Position = 0;
            stream.Read(buffer, 0, 4);

            bool isValid = buffer.SequenceEqual(zipSignature);
            Debug.WriteLine($"📋 ZIP 签名检查: {(isValid ? " 有效" : "❌ 无效")}");

            return isValid;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"⚠️ ZIP 验证异常: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    ///  新增：格式化文件大小为可读格式
    /// </summary>
    private string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }

    /// <summary>
    /// 验证 URL 是否有效
    /// </summary>
    private bool IsValidUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 从 URL 中提取文件名
    /// </summary>
    private string ExtractFileNameFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var fileName = System.IO.Path.GetFileName(uri.LocalPath);
            
            // 如果没有有效的文件名，生成一个
            if (string.IsNullOrEmpty(fileName) || fileName == "/")
            {
                fileName = $"download_{DateTime.Now:yyyyMMdd_HHmmss}";
            }

            return fileName;
        }
        catch
        {
            return $"download_{DateTime.Now:yyyyMMdd_HHmmss}";
        }
    }

    /// <summary>
    /// 显示错误信息
    /// </summary>
    private async Task ShowError(string title, string message)
    {
        await Application.Current?.Windows[0].Page?.DisplayAlertAsync(title, message, "确定");
    }

    /// <summary>
    ///  新增：从HTML中提取重定向URL
    /// 处理服务器返回的JavaScript重定向情况
    /// 例如：window.location.href = 'url'
    /// 或：<a href="url">链接</a>
    /// </summary>
    private string? ExtractRedirectUrlFromHtml(string html)
    {
        if (string.IsNullOrEmpty(html))
            return null;

        try
        {
            //  方法1：从 window.location.href 中提取
            // 匹配模式：window.location.href = 'url'
            var locationMatch = System.Text.RegularExpressions.Regex.Match(
                html,
                @"window\.location\.href\s*=\s*[""']([^""']+)[""']",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );

            if (locationMatch.Success)
            {
                var url = locationMatch.Groups[1].Value;
                Debug.WriteLine($" 从 window.location.href 提取到URL: {url}");

                // 如果URL是相对路径，转换为绝对路径
                if (!url.StartsWith("http"))
                {
                    url = "https://bbs.pcbeta.com/" + url.TrimStart('/');
                }

                return url;
            }

            //  方法2：从 href 属性中提取（通常是下载链接）
            // 匹配模式：href="attachment&aid=..."
            var hrefMatch = System.Text.RegularExpressions.Regex.Match(
                html,
                @"href=[""']([^""']*mod=attachment[^""']*)[""']",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );

            if (hrefMatch.Success)
            {
                var url = hrefMatch.Groups[1].Value;
                Debug.WriteLine($" 从 href 属性提取到URL: {url}");

                // 如果URL是相对路径，转换为绝对路径
                if (!url.StartsWith("http"))
                {
                    url = "https://bbs.pcbeta.com/" + url.TrimStart('/');
                }

                return url;
            }

            Debug.WriteLine("⚠️ 未能从HTML中提取到重定向URL");
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 提取重定向URL异常: {ex.Message}");
            return null;
        }
    }
}
