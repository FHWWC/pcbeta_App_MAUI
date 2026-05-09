using PCBetaMAUI.Models;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace PCBetaMAUI.Services;

/// <summary>
/// XmlParsingService 的分部类 - 用于解析用户个人资料页面
/// </summary>
public partial class XmlParsingService
{
    /// <summary>
    /// Parses user profile information from the profile page HTML
    /// Extracts profile details including avatar, statistics, medals, and activity info
    /// </summary>
    public UserProfileInfo? ParseUserProfile(string htmlContent)
    {
        try
        {
            if (string.IsNullOrEmpty(htmlContent))
            {
                Debug.WriteLine("❌ Profile HTML is empty");
                return null;
            }

            var profile = new UserProfileInfo();

            // Extract username
            var usernameMatch = Regex.Match(htmlContent, @"<h2[^>]*>\s*([^<]+)[\s\S]*?<span[^>]*>\s*\(UID:\s*(\d+)\)\s*</span>", RegexOptions.IgnoreCase);
            if (usernameMatch.Success)
            {
                profile.Username = usernameMatch.Groups[1].Value.Trim();
                profile.Uid = usernameMatch.Groups[2].Value.Trim();
                Debug.WriteLine($"✅ User: {profile.Username} (UID: {profile.Uid})");
            }

            // Extract avatar URL
            var avatarMatch = Regex.Match(htmlContent, @"<img[^>]*?src=""([^""]*avatar[^""]*?)""[^>]*>", RegexOptions.IgnoreCase);
            if (avatarMatch.Success)
            {
                var avatarUrl = avatarMatch.Groups[1].Value.Trim();
                // Handle relative URLs
                if (!avatarUrl.StartsWith("http"))
                {
                    avatarUrl = "https://uc.pcbeta.com" + (avatarUrl.StartsWith("/") ? avatarUrl : "/" + avatarUrl);
                }
                profile.AvatarUrl = avatarUrl;
                Debug.WriteLine($"✅ Avatar URL: {avatarUrl.Substring(0, Math.Min(50, avatarUrl.Length))}...");
            }

            // Extract account info from the profile data
            ExtractAccountInfo(htmlContent, profile);

            // Extract medals
            ExtractMedals(htmlContent, profile);

            // Extract activity info
            ExtractActivityInfo(htmlContent, profile);

            Debug.WriteLine($"✅ Profile parsed successfully");
            return profile;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Error parsing profile: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Extracts account information including statistics
    /// </summary>
    private void ExtractAccountInfo(string htmlContent, UserProfileInfo profile)
    {
        try
        {
            // Extract space PV (space visits)
            var pvMatch = Regex.Match(htmlContent, @"<li><em>空间访问量</em><strong[^>]*>(\d+)</strong></li>", RegexOptions.IgnoreCase);
            if (pvMatch.Success)
                profile.SpacePV = pvMatch.Groups[1].Value.Trim();

            // Extract email status
            var emailMatch = Regex.Match(htmlContent, @"<li><em>邮箱状态</em>([^<]+)</li>", RegexOptions.IgnoreCase);
            if (emailMatch.Success)
                profile.EmailStatus = emailMatch.Groups[1].Value.Trim();

            // Extract reply count
            var replyMatch = Regex.Match(htmlContent, @"<a[^>]*?href=""[^""]*do=thread[^""]*type=reply[^""]*""[^>]*>回帖数\s*(\d+)</a>", RegexOptions.IgnoreCase);
            if (replyMatch.Success)
                profile.ReplyCount = replyMatch.Groups[1].Value.Trim();

            // Extract thread count
            var threadMatch = Regex.Match(htmlContent, @"<a[^>]*?href=""[^""]*do=thread[^""]*type=thread[^""]*""[^>]*>主题数\s*(\d+)</a>", RegexOptions.IgnoreCase);
            if (threadMatch.Success)
                profile.ThreadCount = threadMatch.Groups[1].Value.Trim();

            // Extract friend count
            var friendMatch = Regex.Match(htmlContent, @"<a[^>]*?href=""[^""]*do=friend[^""]*""[^>]*>好友数\s*(\d+)</a>", RegexOptions.IgnoreCase);
            if (friendMatch.Success)
                profile.FriendCount = friendMatch.Groups[1].Value.Trim();

            // Extract credits and other points using more flexible regex
            ExtractPointsData(htmlContent, profile);

            // Extract record, blog, album, share count
            var recordMatch = Regex.Match(htmlContent, @"<a[^>]*?href=""[^""]*do=doing[^""]*""[^>]*>记录数\s*(\d+)</a>", RegexOptions.IgnoreCase);
            if (recordMatch.Success)
                profile.RecordCount = recordMatch.Groups[1].Value.Trim();

            var blogMatch = Regex.Match(htmlContent, @"<a[^>]*?href=""[^""]*do=blog[^""]*""[^>]*>日志数\s*(\d+)</a>", RegexOptions.IgnoreCase);
            if (blogMatch.Success)
                profile.BlogCount = blogMatch.Groups[1].Value.Trim();

            var albumMatch = Regex.Match(htmlContent, @"<a[^>]*?href=""[^""]*do=album[^""]*""[^>]*>相册数\s*(\d+)</a>", RegexOptions.IgnoreCase);
            if (albumMatch.Success)
                profile.AlbumCount = albumMatch.Groups[1].Value.Trim();

            var shareMatch = Regex.Match(htmlContent, @"<a[^>]*?href=""[^""]*do=share[^""]*""[^>]*>分享数\s*(\d+)</a>", RegexOptions.IgnoreCase);
            if (shareMatch.Success)
                profile.ShareCount = shareMatch.Groups[1].Value.Trim();

            // Extract used space
            var spaceMatch = Regex.Match(htmlContent, @"<li><em>已用空间</em>([^<]+)</li>", RegexOptions.IgnoreCase);
            if (spaceMatch.Success)
                profile.UsedSpace = spaceMatch.Groups[1].Value.Trim();

            Debug.WriteLine($"✅ Account info extracted");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"⚠️ Error extracting account info: {ex.Message}");
        }
    }

    /// <summary>
    /// Extracts credit points data (credits, PB coins, reputation, etc.)
    /// </summary>
    private void ExtractPointsData(string htmlContent, UserProfileInfo profile)
    {
        try
        {
            // Find the statistics section containing points information
            var statsMatch = Regex.Match(htmlContent, @"<div[^>]*id=""psts""[^>]*>(.*?)</div>\s*</div>\s*</div>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            
            if (statsMatch.Success)
            {
                var statsContent = statsMatch.Groups[1].Value;

                // Extract each point type using flexible patterns
                var creditsMatch = Regex.Match(statsContent, @"<li><em>积分</em>([^<]+)</li>", RegexOptions.IgnoreCase);
                if (creditsMatch.Success)
                    profile.Credits = creditsMatch.Groups[1].Value.Trim();

                var pbCoinsMatch = Regex.Match(statsContent, @"<li><em>PB币</em>([^<]+)</li>", RegexOptions.IgnoreCase);
                if (pbCoinsMatch.Success)
                    profile.PBCoins = pbCoinsMatch.Groups[1].Value.Trim();

                var reputationMatch = Regex.Match(statsContent, @"<li><em>威望</em>([^<]+)</li>", RegexOptions.IgnoreCase);
                if (reputationMatch.Success)
                    profile.Reputation = reputationMatch.Groups[1].Value.Trim();

                var contributionMatch = Regex.Match(statsContent, @"<li><em>贡献</em>([^<]+)</li>", RegexOptions.IgnoreCase);
                if (contributionMatch.Success)
                    profile.Contribution = contributionMatch.Groups[1].Value.Trim();

                var techMatch = Regex.Match(statsContent, @"<li><em>技术</em>([^<]+)</li>", RegexOptions.IgnoreCase);
                if (techMatch.Success)
                    profile.Technology = techMatch.Groups[1].Value.Trim();

                var activityMatch = Regex.Match(statsContent, @"<li><em>活跃</em>([^<]+)</li>", RegexOptions.IgnoreCase);
                if (activityMatch.Success)
                    profile.Activity = activityMatch.Groups[1].Value.Trim();

                Debug.WriteLine($"✅ Points data extracted");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"⚠️ Error extracting points data: {ex.Message}");
        }
    }

    /// <summary>
    /// Extracts medal information
    /// </summary>
    private void ExtractMedals(string htmlContent, UserProfileInfo profile)
    {
        try
        {
            // Find all medal images and their descriptions
            var medalMatches = Regex.Matches(htmlContent, @"<img[^>]*?src=""([^""]*?)""[^>]*?alt=""([^""]+)""[^>]*?id=""md_(\d+)""", RegexOptions.IgnoreCase);

            foreach (Match match in medalMatches)
            {
                var medalUrl = match.Groups[1].Value.Trim();
                var medalName = match.Groups[2].Value.Trim();

                // Handle relative URLs
                if (!medalUrl.StartsWith("http"))
                {
                    medalUrl = "https://bbs.pcbeta.com/" + medalUrl.TrimStart('/');
                }

                var medal = new Medal
                {
                    Name = medalName,
                    ImageUrl = medalUrl
                };

                // Try to extract medal description from the tooltip
                var descMatch = Regex.Match(htmlContent, @"<div[^>]*id=""md_\d+_menu""[^>]*>.*?<h4>([^<]+)</h4>\s*<p>([^<]+)</p>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (descMatch.Success)
                {
                    medal.Description = descMatch.Groups[2].Value.Trim();
                }

                profile.Medals.Add(medal);
                Debug.WriteLine($"✅ Medal added: {medalName}");
            }

            Debug.WriteLine($"✅ Total medals extracted: {profile.Medals.Count}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"⚠️ Error extracting medals: {ex.Message}");
        }
    }

    /// <summary>
    /// Extracts activity information (user group, registration time, last access, etc.)
    /// </summary>
    private void ExtractActivityInfo(string htmlContent, UserProfileInfo profile)
    {
        try
        {
            // Find the activity section
            var activityMatch = Regex.Match(  htmlContent, @"<ul[^>]*id=""pbbs""[^>]*>([\s\S]*?)</ul>",   RegexOptions.IgnoreCase);

            if (activityMatch.Success)
            {
                var activityContent = activityMatch.Groups[1].Value;

                // Extract user group
                var groupMatch = Regex.Match(htmlContent,@"用户组[\s\S]*?<a[^>]*>[\s\S]*?>([^<]+)</",RegexOptions.IgnoreCase);
                if (groupMatch.Success)
                    profile.UserGroup = groupMatch.Groups[1].Value.Trim();

                // Extract register time
                var regMatch = Regex.Match(activityContent, @"<li><em>注册时间</em>([^<]+)</li>", RegexOptions.IgnoreCase);
                if (regMatch.Success)
                    profile.RegisterTime = regMatch.Groups[1].Value.Trim();

                // Extract last access time
                var lastAccessMatch = Regex.Match(activityContent, @"<li><em>最后访问</em>([^<]+)</li>", RegexOptions.IgnoreCase);
                if (lastAccessMatch.Success)
                    profile.LastAccessTime = lastAccessMatch.Groups[1].Value.Trim();

                // Extract last visit IP
                var ipMatch = Regex.Match(activityContent, @"<li><em>上次访问\s*IP</em>([^<]+)</li>", RegexOptions.IgnoreCase);
                if (ipMatch.Success)
                    profile.LastVisitIP = ipMatch.Groups[1].Value.Trim();

                // Extract last activity time
                var lastActivityMatch = Regex.Match(activityContent, @"<li><em>上次活动时间</em>([^<]+)</li>", RegexOptions.IgnoreCase);
                if (lastActivityMatch.Success)
                    profile.LastActivityTime = lastActivityMatch.Groups[1].Value.Trim();

                // Extract last post time
                var lastPostMatch = Regex.Match(activityContent, @"<li><em>上次发表时间</em>([^<]+)</li>", RegexOptions.IgnoreCase);
                if (lastPostMatch.Success)
                    profile.LastPostTime = lastPostMatch.Groups[1].Value.Trim();

                // Extract timezone
                var tzMatch = Regex.Match(activityContent, @"<li><em>所在时区</em>([^<]+)</li>", RegexOptions.IgnoreCase);
                if (tzMatch.Success)
                    profile.Timezone = tzMatch.Groups[1].Value.Trim();

                Debug.WriteLine($"✅ Activity info extracted");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"⚠️ Error extracting activity info: {ex.Message}");
        }
    }
}
