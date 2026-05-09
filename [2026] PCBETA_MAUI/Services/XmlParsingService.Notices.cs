using PCBetaMAUI.Models;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace PCBetaMAUI.Services;

/// <summary>
/// XmlParsingService扩展 - 用于解析用户通知页面
/// </summary>
public partial class XmlParsingService
{
    /// <summary>
    /// 解析通知页面HTML，提取通知列表
    /// ✅ 新增：从forum.xml的通知页面解析通知列表
    /// 
    /// HTML结构：
    /// <div class="mn">
    ///   <div class="xld xlda">
    ///     <div class="nts">
    ///       <dl class="cl" notice="18588811" id="notice_18588811">
    ///         <dd class="m avt mbn">
    ///           <a href="..."><img src="..." class="user_avatar"></a>
    ///         </dd>
    ///         <dt>
    ///           <a class="d b" href="...">屏蔽</a>
    ///           <span class="xg1 xw0">2026-4-27 17:05</span>
    ///         </dt>
    ///         <dd class="ntc_body" style="">
    ///           <a href="...">堂西</a> 回复了您的帖子 <a href="https://bbs.pcbeta.com/forum.php?mod=redirect&goto=findpost&ptid=2069323&pid=57971291">APP发布新贴测试</a> ...
    ///         </dd>
    ///       </dl>
    ///     </div>
    ///   </div>
    /// </div>
    /// </summary>
    /// <param name="htmlContent">通知页面的HTML内容</param>
    /// <returns>通知信息列表</returns>
    public List<NoticeInfo> ParseNotices(string htmlContent)
    {
        var notices = new List<NoticeInfo>();

        try
        {
            if (string.IsNullOrEmpty(htmlContent))
            {
                Debug.WriteLine("❌ 通知页面HTML为空");
                return notices;
            }

            // 1. 首先找到 <div class="mn"> 区域（主内容区域）
            var mainMatch = Regex.Match(htmlContent, @"<div class=""mn"">(.*?)<div class=""appl""", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (!mainMatch.Success)
            {
                Debug.WriteLine("❌ 未能找到 <div class=\"mn\"> 区域");
                return notices;
            }

            string mainContent = mainMatch.Groups[1].Value;

            // 2. 在主区域内查找所有的通知项 <dl class="cl" notice="xxx">
            var noticePattern = @"<dl\b(?=[^>]*\bnotice=""(\d+)"")(?=[^>]*\bid=""notice_\d+"")[^>]*>([\s\S]*?)</dl>";
            var noticeMatches = Regex.Matches(mainContent, noticePattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);

            Debug.WriteLine($"✅ 找到 {noticeMatches.Count} 个通知项");

            foreach (Match noticeMatch in noticeMatches)
            {
                try
                {
                    var noticeId = noticeMatch.Groups[1].Value;
                    var noticeContent = noticeMatch.Groups[2].Value;

                    // 3. 提取用户头像 URL
                    var avatarUrl = ExtractAvatarUrl(noticeContent);

                    // 4. 提取通知时间
                    var noticeTime = ExtractNoticeTime(noticeContent);

                    // 5. 提取通知文本内容
                    var noticeText = ExtractNoticeText(noticeContent);

                    // 6. 从通知文本中提取 ThreadId 和 PostId
                    var (threadId, postId) = ExtractThreadInfoFromNotice(noticeContent);

                    var userName = ExtractUserName(noticeContent);
                    var threadTitle = ExtractTitle(noticeContent);

                    var notice = new NoticeInfo
                    {
                        NoticeId = noticeId,
                        AvatarUrl = avatarUrl,
                        UserName = userName,
                        ThreadTitle = threadTitle,
                        NoticeTime = noticeTime,
                        NoticeText = noticeText,
                        ThreadId = threadId,
                        PostId = postId
                    };

                    notices.Add(notice);
                    Debug.WriteLine($"✅ 解析通知 (ID={noticeId}): {noticeText.Substring(0, Math.Min(50, noticeText.Length))}...");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"⚠️ 解析单个通知项失败: {ex.Message}");
                    continue;
                }
            }

            Debug.WriteLine($"✅ 成功解析 {notices.Count} 个通知");
            return notices;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 解析通知页面错误: {ex.Message}");
            return notices;
        }
    }

    /// <summary>
    /// 从通知项中提取用户头像URL
    /// </summary>
    private string ExtractAvatarUrl(string noticeContent)
    {
        try
        {
            // 查找 <dd class="m avt mbn">...<img src="...">
            var avatarMatch = Regex.Match(noticeContent, @"<img\s+src=""([^""]+)""\s+(?:onerror|class)=", RegexOptions.IgnoreCase);
            if (avatarMatch.Success)
            {
                var url = avatarMatch.Groups[1].Value;
                // 如果URL不是完整的HTTP(S)，补全
                if (!url.StartsWith("http"))
                {
                    url = "https:" + url;
                }
                return url;
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"⚠️ 提取头像URL失败: {ex.Message}");
            return string.Empty;
        }
    }
    private string ExtractUserName(string noticeContent)
    {
        try
        {
            var userNameMatch = Regex.Match(noticeContent, @"class=""ntc_body""[^>]*>\s*<a[^>]*>([^<]+)", RegexOptions.IgnoreCase);
            if (userNameMatch.Success)
            {
                return userNameMatch.Groups[1].Value.Trim();
            }

            userNameMatch = Regex.Match(noticeContent, @"帖子被\s*<a[^>]*>([^<]+)", RegexOptions.IgnoreCase);
            if (userNameMatch.Success)
            {
                return userNameMatch.Groups[1].Value.Trim();
            }

            userNameMatch = Regex.Match(noticeContent, @"管理团队(.*?)审核通过", RegexOptions.IgnoreCase);
            if (userNameMatch.Success)
            {
                return userNameMatch.Groups[1].Value.Trim();
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"⚠️ 提取用户名失败: {ex.Message}");
            return string.Empty;
        }
    }
    private string ExtractTitle(string noticeContent)
    {
        try
        {
            var titleMatch = Regex.Match(noticeContent, @"(帖子|主题)\s*<a[^>]*>([^<]+)", RegexOptions.IgnoreCase);
            if (titleMatch.Success)
            {
                return titleMatch.Groups[2].Value.Trim();
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"⚠️ 提取帖子标题失败: {ex.Message}");
            return string.Empty;
        }
    }
    /// <summary>
    /// 从通知项中提取通知时间
    /// 格式: <span class="xg1 xw0">2026-4-27 17:05</span>
    /// </summary>
    private string ExtractNoticeTime(string noticeContent)
    {
        try
        {
            var timeMatch = Regex.Match(noticeContent, @"<span class=""xg1 xw0"">([^<]+)</span>", RegexOptions.IgnoreCase);
            if (timeMatch.Success)
            {
                return timeMatch.Groups[1].Value.Trim();
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"⚠️ 提取时间失败: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// 从通知项中提取通知文本内容
    /// 通知文本在 <dd class="ntc_body"> ... </dd> 中
    /// ✅ 改进：处理HTML标签，提取纯文本内容
    /// </summary>
    private string ExtractNoticeText(string noticeContent)
    {
        try
        {
            // 查找 <dd class="ntc_body" ...>...内容...</dd>
            var textMatch = Regex.Match(noticeContent, @"<dd class=""ntc_body""[^>]*>(.*?)</dd>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (textMatch.Success)
            {
                var rawText = textMatch.Groups[1].Value;

                // 移除所有HTML标签
                var cleanText = Regex.Replace(rawText, @"<[^>]+>", string.Empty);

                // 移除 HTML 实体编码（如 &rsaquo;, &nbsp; 等）
                cleanText = Regex.Replace(cleanText, @"&[a-zA-Z]+;", " ");

                // 清理多余空格
                cleanText = Regex.Replace(cleanText, @"\s+", " ").Trim();

                return cleanText;
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"⚠️ 提取通知文本失败: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// ✅ 新增：从通知内容中提取 ThreadId 和 PostId
    /// 目标URL格式: https://bbs.pcbeta.com/forum.php?mod=redirect&goto=findpost&ptid=2069323&pid=57971291
    /// 或: https://bbs.pcbeta.com/forum.php?mod=redirect&goto=findpost&pid=57971291&ptid=2069323
    /// 
    /// ptid = Thread ID（必须提取）
    /// pid = Post ID（必须提取）
    /// </summary>
    private (string? threadId, string? postId) ExtractThreadInfoFromNotice(string noticeContent)
    {
        try
        {
            // 查找 ptid 参数（Thread ID）
            var ptidMatch = Regex.Match(noticeContent, @"ptid=(\d+)", RegexOptions.IgnoreCase);
            var threadId = ptidMatch.Success ? ptidMatch.Groups[1].Value : null;

            // 查找 pid 参数（Post ID）
            var pidMatch = Regex.Match(noticeContent, @"pid=(\d+)", RegexOptions.IgnoreCase);
            var postId = pidMatch.Success ? pidMatch.Groups[1].Value : null;

            if (!string.IsNullOrEmpty(threadId) && !string.IsNullOrEmpty(postId))
            {
                Debug.WriteLine($"✅ 从通知中提取线程信息: ptid={threadId}, pid={postId}");
            }

            return (threadId, postId);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"⚠️ 提取线程信息失败: {ex.Message}");
            return (null, null);
        }
    }
}
