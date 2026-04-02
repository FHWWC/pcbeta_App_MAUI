using System.Diagnostics;
using System.Text.RegularExpressions;
using PCBetaMAUI.Models;

namespace PCBetaMAUI.Services;

/// <summary>
///  新增：XmlParsingService 的回帖解析扩展
/// 提取论坛帖子的用户回复（回帖）信息，包含用户信息、楼层号、发帖时间、IP属地等
/// </summary>
public partial class XmlParsingService
{
    /// <summary>
    ///  新增：从HTML中提取回帖列表
    /// 从 <div id="post_XXXXXXXXX"> 元素提取用户的回复
    /// 注意：第一个 post 是楼主发帖，后续的都是用户回帖
    /// </summary>
    private List<ReplyInfo>? ExtractRepliesFromHtml(string htmlContent)
    {
        var replies = new List<ReplyInfo>();

        try
        {
            // 找所有 <div id="post_*"> 元素（第一个是楼主，后续是回帖）
            var postPattern = @"<div[^>]*id=""(post_\d+)""[^>]*>";
            var postMatches = Regex.Matches(htmlContent, postPattern, RegexOptions.IgnoreCase);

            if (postMatches.Count <= 1)
            {
                // 没有回帖（只有楼主）
                Debug.WriteLine("⏭️ 没有检测到回帖");
                return null;
            }

            // 跳过第一个 post（楼主），从第二个开始提取回帖
            for (int i = 1; i < postMatches.Count; i++)
            {
                var postMatch = postMatches[i];
                var postId = postMatch.Groups[1].Value;

                // 提取这个 post 的完整内容
                // 从当前 <div id="post_*"> 开始，到下一个 <div id="post_*"> 或 <div id="postlistreply"> 结束
                int startIndex = postMatch.Index + postMatch.Length;
                int endIndex = htmlContent.Length;

                // 找下一个 post 的位置
                if (i + 1 < postMatches.Count)
                {
                    endIndex = postMatches[i + 1].Index;
                }
                else
                {
                    // 找 postlistreply div 的位置（回帖列表的结束标记）
                    var endMatch = Regex.Match(
                        htmlContent.Substring(startIndex),
                        @"<div[^>]*id=""postlistreply""",
                        RegexOptions.IgnoreCase
                    );
                    if (endMatch.Success)
                    {
                        endIndex = startIndex + endMatch.Index;
                    }
                }

                var postHtml = htmlContent.Substring(postMatch.Index, endIndex - postMatch.Index);
                var reply = ExtractReplyFromPost(postHtml, postId);

                if (reply != null)
                {
                    replies.Add(reply);
                    Debug.WriteLine($" 提取回帖: {reply.Username} - 楼层:{reply.FloorNumber}");
                }
            }

            return replies.Count > 0 ? replies : null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 提取回帖列表错误: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///  新增：从单个 post HTML 中提取回帖信息
    /// </summary>
    private ReplyInfo? ExtractReplyFromPost(string postHtml, string postId)
    {
        try
        {
            var reply = new ReplyInfo { Id = postId };

            // 1. 提取楼层号 - 关键特征是 id="postnum" 属性
            // 楼层号链接格式可能包含多种形式：
            // - 纯文本：<a href="..." id="postnum56578180" ...>沙发</a> 或 <a ...>楼主</a>
            // - 包含HTML标签：<a ...><em>6</em><sup>F</sup></a> （数字楼层）
            // 用户名链接格式：<a href="...space-uid..." class="xi2">用户名</a>
            // 两者都在 <strong> 标签内，需要用 id="postnum" 来区分

            // 修改方案1：使用 (.*?) 来匹配任何内容（包括HTML标签），然后剥离标签
            var floorPattern = @"<a[^>]*id=""postnum[^>]*>(.*?)</a>";
            var floorMatch = Regex.Match(postHtml, floorPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (floorMatch.Success)
            {
                var floorHtml = floorMatch.Groups[1].Value;
                // 先剥离HTML标签，再解码HTML实体
                reply.FloorNumber = HtmlDecode(StripHtmlTags(floorHtml).Trim());
                Debug.WriteLine($" 楼层号提取成功: {reply.FloorNumber}");
            }
            else
            {
                // 备用方案：如果主方案没有匹配，尝试在 <strong> 内查找
                var floorPattern2 = @"<strong[^>]*>(.*?)</strong>";
                var floorMatch2 = Regex.Match(postHtml, floorPattern2, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (floorMatch2.Success)
                {
                    var floorText = floorMatch2.Groups[1].Value;
                    // 确保不是用户名（用户名链接包含 space-uid）
                    if (!floorText.Contains("space-uid", StringComparison.OrdinalIgnoreCase))
                    {
                        floorText = StripHtmlTags(floorText).Trim();
                        reply.FloorNumber = HtmlDecode(floorText);
                        Debug.WriteLine($" 楼层号备用方案成功: {reply.FloorNumber}");
                    }
                }
            }

            // 2. 提取用户信息（左侧栏）
            // 用户名：<a href="..." class="xi2 an">用户名</a>
            var usernamePattern = @"class=""xw1""[^>]*>([^<]+)</a>";
            var usernameMatch = Regex.Match(postHtml, usernamePattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (usernameMatch.Success)
            {
                reply.Username = HtmlDecode(usernameMatch.Groups[1].Value.Trim());
            }

            // 3. 提取用户ID（UID）和个人空间链接
            // UID: <a href="?1419113" target="_blank" class="xi2">1419113</a>
            var uidPattern = @"<a[^>]*href=""(\?(\d+))""[^>]*class=""xi2""[^>]*>(\d+)</a>";
            var uidMatch = Regex.Match(postHtml, uidPattern, RegexOptions.IgnoreCase);
            if (uidMatch.Success)
            {
                reply.UserId = uidMatch.Groups[3].Value.Trim();
                reply.ProfileUrl = uidMatch.Groups[1].Value.Trim();
            }

            // 4. 提取头像URL
            // <img src="https://uc.pcbeta.com/data/avatar/..." class="user_avatar">
            var avatarPattern = @"<img[^>]*src=""([^""]+)""[^>]*class=""user_avatar""";
            var avatarMatch = Regex.Match(postHtml, avatarPattern, RegexOptions.IgnoreCase);
            if (avatarMatch.Success)
            {
                reply.AvatarUrl = avatarMatch.Groups[1].Value.Trim();
            }

            // 5. 提取发帖时间 - 格式：发表于 2015-8-30 19:32
            var timePattern = @"<em[^>]*>发表于\s+([^<]+)</em>";
            var timeMatch = Regex.Match(postHtml, timePattern, RegexOptions.IgnoreCase);
            if (timeMatch.Success)
            {
                reply.PostTime = HtmlDecode(timeMatch.Groups[1].Value.Trim());
            }

            // 6. 提取IP属地 - 格式：IPv4属地北京 或 IPv4属地浙江
            var ipPattern = @">(IPv.*?属地([^<\|]+))(?:</|$)";
            var ipMatch = Regex.Match(postHtml, ipPattern, RegexOptions.IgnoreCase);
            if (ipMatch.Success)
            {
                reply.IPLocation = HtmlDecode(ipMatch.Groups[1].Value.Trim());
            }

            // 7. 提取回帖内容
            var messageHtml = ExtractReplyMessageContent(postHtml);
            if (!string.IsNullOrEmpty(messageHtml))
            {
                reply.ContentElements = ParseRichTextContent(messageHtml, skipEditStatus: false);
                reply.PlainTextContent = GeneratePlainText(reply.ContentElements);
            }

            // 8. 提取编辑状态
            var editStatusPattern = @"<i[^>]*class=""pstatus""[^>]*>\s*本帖最后由\s*([^\s于]+)\s*于\s*([^\s]+(?:\s+\d+:\d+)?)\s*编辑\s*</i>";
            var editStatusMatch = Regex.Match(postHtml, editStatusPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (editStatusMatch.Success)
            {
                var editedBy = HtmlDecode(editStatusMatch.Groups[1].Value.Trim());
                var editedAt = editStatusMatch.Groups[2].Value.Trim();
                reply.EditStatus = $"本帖最后由 {editedBy} 于 {editedAt} 编辑";
                Debug.WriteLine($" 回帖编辑状态: {reply.EditStatus}");
            }

            // 9. 提取评论（点评）
            var comments = ExtractReplyComments(postHtml);
            if (comments != null && comments.Count > 0)
            {
                reply.Comments = comments;
                Debug.WriteLine($" 回帖评论数: {comments.Count}");
            }

            // 10. 提取评分信息
            var ratingSummary = ExtractReplyRatings(postHtml);
            if (ratingSummary != null)
            {
                reply.RatingSummary = ratingSummary;
                Debug.WriteLine($" 回帖评分数: {ratingSummary.TotalRatingCount}");
            }

            // 11. 设置操作按钮的可见性
            reply.CanComment = !string.IsNullOrEmpty(messageHtml);
            reply.CanRate = !string.IsNullOrEmpty(messageHtml);
            reply.CanReply = !string.IsNullOrEmpty(messageHtml);

            return reply;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 提取单个回帖错误: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///  新增：从回帖HTML中提取消息内容
    /// 与楼主使用相同的方法
    /// </summary>
    private string ExtractReplyMessageContent(string postHtml)
    {
        try
        {
            // Find the opening <td class="t_f" id="postmessage_*">
            var openingPattern = @"<td[^>]*class=""t_f""[^>]*id=""postmessage_(\d+)""[^>]*>";
            var openingMatch = Regex.Match(postHtml, openingPattern, RegexOptions.IgnoreCase);

            if (!openingMatch.Success)
                return string.Empty;

            int startIndex = openingMatch.Index + openingMatch.Length;
            int nestingLevel = 1;
            int currentIndex = startIndex;

            while (currentIndex < postHtml.Length && nestingLevel > 0)
            {
                int tdOpenIndex = postHtml.IndexOf("<td", currentIndex, StringComparison.OrdinalIgnoreCase);
                int tdCloseIndex = postHtml.IndexOf("</td>", currentIndex, StringComparison.OrdinalIgnoreCase);

                if (tdCloseIndex == -1)
                    return postHtml.Substring(startIndex);

                if (tdOpenIndex != -1 && tdOpenIndex < tdCloseIndex)
                {
                    int tagEndIndex = postHtml.IndexOf(">", tdOpenIndex, StringComparison.OrdinalIgnoreCase);
                    if (tagEndIndex != -1 && tagEndIndex < tdCloseIndex)
                    {
                        nestingLevel++;
                        currentIndex = tagEndIndex + 1;
                    }
                    else
                    {
                        currentIndex = tdOpenIndex + 3;
                    }
                }
                else
                {
                    nestingLevel--;
                    if (nestingLevel == 0)
                        return postHtml.Substring(startIndex, tdCloseIndex - startIndex);

                    currentIndex = tdCloseIndex + 5;
                }
            }

            return postHtml.Substring(startIndex);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 提取回帖消息内容错误: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    ///  新增：从回帖HTML中提取评论（点评）
    /// 使用与楼主相同的逻辑
    /// </summary>
    private List<CommentInfo>? ExtractReplyComments(string postHtml)
    {
        try
        {
            // 查找 <div id="comment_XXXXXXXXX" class="cm">
            //var commentDivPattern = @"<div[^>]*id=""comment_\d+""[^>]*class=""cm""[^>]*>.*?</div>\s*<(?:h3|div|dl)";
            var commentDivPattern = @"<div[^>]*id=""comment_\d+""[^>]*class=""cm""[^>]*>.*?</div>\s*<(.*?rate)";
            var commentMatch = Regex.Match(postHtml, commentDivPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (!commentMatch.Success)
                return null;

            var commentContainerHtml = commentMatch.Value;
            var comments = new List<CommentInfo>();

            // 使用与楼主相同的评论提取逻辑
            var commentPattern = @"<div[^>]*class=""[^""]*pstl[^""]*""[^>]*>([\s\S]*?<div[^>]*class=""psti[^""]*""[^>]*>[\s\S]*?</div>)\s*</div>";
            var commentMatches = Regex.Matches(commentContainerHtml, commentPattern, RegexOptions.IgnoreCase);

            foreach (Match match in commentMatches)
            {
                var commentHtml = match.Value;

                // 提取用户名
                var usernameMatch = Regex.Match(commentHtml, @"<a[^>]*class=""xi2 xw1""[^>]*>([^<]+)</a>");
                var username = usernameMatch.Success ? HtmlDecode(usernameMatch.Groups[1].Value.Trim()) : "匿名";

                // 提取头像URL
                var avatarMatch = Regex.Match(commentHtml, @"<img[^>]*src=""([^""]+)""[^>]*class=""user_avatar""");
                var avatarUrl = avatarMatch.Success ? avatarMatch.Groups[1].Value.Trim() : null;

                // 提取评论时间戳
                var timestampMatch = Regex.Match(commentHtml, @"<span[^>]*class=""xg1""[^>]*>发表于\s+([^<]+)</span>");
                var timestamp = timestampMatch.Success ? HtmlDecode(timestampMatch.Groups[1].Value.Trim()) : "";

                // 提取评论文本
                var commentTextMatch = Regex.Match(commentHtml, @"<div[^>]*class=""psti""[^>]*>(.*?)</div>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                var commentText = commentTextMatch.Success ? HtmlDecode(commentTextMatch.Groups[1].Value.Trim()) : "";

                if (!string.IsNullOrEmpty(commentText))
                {
                    comments.Add(new CommentInfo
                    {
                        Username = username,
                        AvatarUrl = avatarUrl,
                        Timestamp = timestamp,
                        CommentText = commentText
                    });
                }
            }

            return comments.Count > 0 ? comments : null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 提取回帖评论错误: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///  新增：从回帖HTML中提取评分信息
    /// 使用与楼主相同的逻辑
    /// </summary>
    private RatingSummary? ExtractReplyRatings(string postHtml)
    {
        try
        {
            // 查找 <dl id="ratelog_*" class="rate">
            var ratingPattern = @"<dl[^>]*id=""ratelog_\d+""[^>]*class=""rate""[^>]*>(.*?)</dl>";
            var ratingMatch = Regex.Match(postHtml, ratingPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (!ratingMatch.Success)
                return null;

            var ratingHtml = ratingMatch.Value;
            var ratingSummary = new RatingSummary { RatingDetails = new List<RatingInfo>() };

            // 提取总评分数：<strong><a href="..." onclick="...">26</a></strong>
            var totalRatingMatch = Regex.Match(ratingHtml, @"<strong><a[^>]*>(\d+)</a></strong>");
            if (totalRatingMatch.Success)
            {
                ratingSummary.TotalRatingCount = int.Parse(totalRatingMatch.Groups[1].Value);
            }

            // 提取评分详情 - 每条评分：<li><p id="rate_*">...评分内容...</p>...
            var ratingItemPattern = @"<li>.*?<p[^>]*id=""rate_\d+[^>]*>.*?</li>";
            var ratingMatches = Regex.Matches(ratingHtml, ratingItemPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

            foreach (Match match in ratingMatches)
            {
                var itemHtml = match.Value;

                // 提取用户名和头像
                var usernameMatch = Regex.Match(itemHtml, @"<a[^>]*href=""https://i\.pcbeta\.com/space-uid-\d+\.html""[^>]*target=""_blank""[^>]*>([^<]+)</a>");
                var username = usernameMatch.Success ? HtmlDecode(usernameMatch.Groups[1].Value.Trim()) : "匿名";

                var avatarMatch = Regex.Match(itemHtml, @"<img[^>]*src=""([^""]+)""[^>]*class=""user_avatar""");
                var avatarUrl = avatarMatch.Success ? avatarMatch.Groups[1].Value.Trim() : null;

                // 提取评分内容（可能包含评分说明和PB币数）
                // 格式：<strong>评分说明</strong> <em class='xi1'>PB币 + 1 </em>
                var contentMatch = Regex.Match(itemHtml, @"<p[^>]*onmouseover=""showTip\(this\)""[^>]*tip=""<strong>([^<]*)</strong>[^""]*""", RegexOptions.IgnoreCase);
                var ratingContent = contentMatch.Success ? HtmlDecode(contentMatch.Groups[1].Value.Trim()) : "";

                // 如果没有从 tip 属性提取到，尝试从 HTML 内容直接提取
                if (string.IsNullOrEmpty(ratingContent))
                {
                    var contentHtmlMatch = Regex.Match(itemHtml, @"<strong>([^<]*)</strong>");
                    if (contentHtmlMatch.Success)
                    {
                        ratingContent = HtmlDecode(contentHtmlMatch.Groups[1].Value.Trim());
                    }
                }

                if (!string.IsNullOrEmpty(username))
                {
                    ratingSummary.RatingDetails.Add(new RatingInfo
                    {
                        Username = username,
                        AvatarUrl = avatarUrl,
                        RatingContent = ratingContent
                    });
                }
            }

            return ratingSummary.TotalRatingCount > 0 || ratingSummary.RatingDetails.Count > 0 ? ratingSummary : null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 提取回帖评分错误: {ex.Message}");
            return null;
        }
    }
}
