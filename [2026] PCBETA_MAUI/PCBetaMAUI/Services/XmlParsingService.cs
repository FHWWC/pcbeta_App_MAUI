using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using PCBetaMAUI.Models;

namespace PCBetaMAUI.Services;

/// <summary>
/// Service for parsing XML responses from the PCBETA forum API.
/// API responses come as XML with CDATA containing HTML content.
/// </summary>
public partial class XmlParsingService
{
    /// <summary>
    /// Checks if login was successful by parsing login response XML
    ///  改进：正确处理CDATA中的HTML内容，检查错误提示
    ///  改进：返回元组 (isSuccess, errorMessage)，支持返回具体的错误信息
    /// 
    /// 错误响应例子：
    /// &lt;div class="alert_error"&gt;登录失败，您还可以尝试 2 次&lt;/div&gt;
    /// &lt;h3 class="flb"&gt;&lt;em&gt;提示信息&lt;/em&gt;...&lt;/h3&gt;
    /// &lt;div class="c altw"&gt;&lt;div class="alert_error"&gt;错误信息&lt;/div&gt;&lt;/div&gt;
    /// 
    /// 返回值：(isSuccess, errorMessage)
    /// - 登录成功：(true, null)
    /// - 登录失败：(false, "具体错误信息")
    /// </summary>
    public (bool isSuccess, string? errorMessage) IsLoginSuccessful(string xmlResponse)
    {
        try
        {
            var doc = XDocument.Parse(xmlResponse);
            var root = doc.Root;

            if (root == null)
            {
                Debug.WriteLine("❌ 登录响应：根元素为空");
                return (false, "登录响应格式错误");
            }

            //  改进1：首先提取CDATA内容（登录响应的内容在CDATA中）
            string htmlContent = ExtractCDataContent(root) ?? root.Value ?? "";

            //  改进2：检查常见的错误标记
            // 错误类型1：alert_error - 最直接的错误指示
            if (htmlContent.Contains("alert_error") || htmlContent.Contains("登录失败"))
            {
                var errorMatch = Regex.Match(htmlContent, @"<div class=""alert_error"">(.*?)<script", RegexOptions.IgnoreCase);
                if (errorMatch.Success)
                {
                    var errorMsg = HtmlDecode(errorMatch.Groups[1].Value.Trim());
                    Debug.WriteLine($"❌ 登录失败: {errorMsg}");
                    return (false, errorMsg);
                }

                // 如果没有匹配到具体的错误消息，但包含"登录失败"关键词，也返回false
                Debug.WriteLine("❌ 登录失败: 检测到登录失败标记");
                return (false, "登录失败，请检查用户名和密码");
            }

            //  改进3：检查是否包含成功指示
            // 如果没有错误，且返回了有效的HTML内容，认为登录成功
            if (!string.IsNullOrEmpty(htmlContent))
            {
                Debug.WriteLine(" 登录成功");
                return (true, null);
            }

            //  改进4：尝试从根元素的<result>元素判断（某些API可能会使用这种格式）
            var resultElement = root.Element("result");
            if (resultElement != null)
            {
                var isSuccess = resultElement.Value == "1" || resultElement.Value.ToLower() == "success";
                if (isSuccess)
                {
                    Debug.WriteLine(" 登录成功 (result=1)");
                    return (true, null);
                }
                else
                {
                    Debug.WriteLine("❌ 登录失败 (result!=1)");
                    return (false, "登录失败，请检查用户名和密码");
                }
            }

            // 默认假设成功（如果内容非空但无明显错误标记）
            Debug.WriteLine("⚠️ 无法确定登录状态，假设成功");
            return (!string.IsNullOrEmpty(htmlContent), null);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 登录响应解析错误: {ex.Message}");
            return (false, $"登录请求失败: {ex.Message}");
        }
    }

    /// <summary>
    /// Extracts username from main page HTML
    /// Looks for the username in the top bar section, typically in format: <a href="...">用户名</a>
    /// </summary>
    public string? ExtractUsernameFromMainPage(string xmlResponse)
    {
        try
        {
            // Look for username in top bar: <a href="https://i.pcbeta.com/space-uid-XXX.html" ...>用户名</a>
            // The pattern looks for user space links
            var userNamePattern = @"title=""访问我的空间"">(.*?)</a>";
            var match = Regex.Match(xmlResponse, userNamePattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (match.Success && match.Groups.Count > 1)
            {
                var username = match.Groups[1].Value.Trim();
                Debug.WriteLine($"Extracted username: {username}");
                return username;
            }

            Debug.WriteLine("Could not extract username from main page");
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Extract username error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Extracts logout URL from main page HTML
    /// Looks for the logout link in the top bar, typically in format: member.php?mod=logging&action=logout&formhash=XXX
    /// </summary>
    public (string? logoutUrl, string? formHash) ExtractLogoutInfoFromMainPage(string xmlResponse)
    {
        try
        {
            // Look for logout link in top bar: member.php?mod=logging&action=logout&formhash=XXX
            // Pattern: <a href="member.php?mod=logging&amp;action=logout&amp;formhash=XXXX">...退出...</a>
            var logoutPattern = @"<a[^>]*href=""([^""]*?member.php[^""]*?mod=logging[^""]*?action=logout[^""]*?)""[^>]*>([^<]*退出[^<]*)</a>";
            var match = Regex.Match(xmlResponse, logoutPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (match.Success && match.Groups.Count > 1)
            {
                var logoutUrl = match.Groups[1].Value.Trim();

                // Decode HTML entities
                logoutUrl = System.Net.WebUtility.HtmlDecode(logoutUrl);

                Debug.WriteLine($"Extracted logout URL: {logoutUrl}");

                // Extract formhash from the URL
                var formHashMatch = Regex.Match(logoutUrl, @"formhash=([a-f0-9]+)", RegexOptions.IgnoreCase);
                var formHash = formHashMatch.Success ? formHashMatch.Groups[1].Value : null;

                return (logoutUrl, formHash);
            }

            Debug.WriteLine("Could not extract logout URL from main page");
            return (null, null);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Extract logout info error: {ex.Message}");
            return (null, null);
        }
    }

    /// <summary>
    /// Parses forum main page XML to extract forum categories with their sections
    /// </summary>
    public List<ForumCategory> ParseMainPageForums(string xmlResponse)
    {
        var categories = new List<ForumCategory>();

        try
        {
            if(xmlResponse.Contains("<strong>登录</strong>"))
            {
                // Parse HTML to extract forum categories and sections
                categories = ExtractForumCategoriesFromHtml(xmlResponse);
            }
            else
            {
                var doc = XDocument.Parse(xmlResponse);
                var root = doc.Root;

                // Extract CDATA content if present
                var htmlContent = ExtractCDataContent(root);
                if (string.IsNullOrEmpty(htmlContent))
                    htmlContent = root?.Value ?? xmlResponse;

                // Parse HTML to extract forum categories and sections
                categories = ExtractForumCategoriesFromHtml(htmlContent);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Forum parse error: {ex.Message}");
        }

        return categories;
    }

    /// <summary>
    /// Parses thread list XML to extract thread information
    /// </summary>
    public List<ThreadInfo> ParseThreadList(string xmlResponse)
    {
        var threads = new List<ThreadInfo>();

        try
        {
            //  新增：检查是否包含错误信息（如访问权限错误、需要登录等）
            var errorMessage = ExtractErrorMessageFromResponse(xmlResponse);
            if (!string.IsNullOrEmpty(errorMessage))
            {
                threads.Add(new ThreadInfo()
                {
                    Id = "ERROR",
                    Title = errorMessage
                });

                return threads;
            }


            var doc = XDocument.Parse(xmlResponse);
            var root = doc.Root;

            // Extract CDATA content if present
            var htmlContent = ExtractCDataContent(root);
            if (string.IsNullOrEmpty(htmlContent))
                htmlContent = root?.Value ?? xmlResponse;

            // Parse HTML to extract thread information
            threads = ExtractThreadsFromHtml(htmlContent);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Thread list parse error: {ex.Message}");
        }

        return threads;
    }

    /// <summary>
    /// Parses thread content XML to extract post content
    /// </summary>
    public ThreadContent ParseThreadContent(string xmlResponse)
    {
        var content = new ThreadContent
        {
            Id = "",
            Title = "",
            Author = "",
            PostTime = "发表于 --",
            PlainTextContent = "",
            RawHtmlContent = "",
            Replies = 0
        };

        try
        {
            if (xmlResponse.Contains("<strong>登录</strong>"))
            {
                // Parse HTML to extract thread content
                content = ExtractThreadContentFromHtml(xmlResponse);
            }
            else
            {
                var doc = XDocument.Parse(xmlResponse);
                var root = doc.Root;

                // Extract CDATA content if present
                var htmlContent = ExtractCDataContent(root);
                if (string.IsNullOrEmpty(htmlContent))
                    htmlContent = root?.Value ?? xmlResponse;

                // Parse HTML to extract thread content
                content = ExtractThreadContentFromHtml(htmlContent);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Thread content parse error: {ex.Message}");
        }

        return content;
    }

    /// <summary>
    /// Extracts CDATA content from XML root element
    /// </summary>
    private string? ExtractCDataContent(XElement? root)
    {
        if (root == null)
            return null;

        try
        {
            // Look for CDATA in direct children or root value
            var nodes = root.Nodes().OfType<XCData>();
            if (nodes.Any())
            {
                return nodes.First().Value;
            }

            // Try to get from first element's value
            var firstChild = root.FirstNode;
            if (firstChild is XCData cdata)
            {
                return cdata.Value;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extracts forum categories and their sections from HTML content
    /// Based on PCBETA forum structure: each category has specific layout (col_0, col_2, col_3)
    /// </summary>
    private List<ForumCategory> ExtractForumCategoriesFromHtml(string htmlContent)
    {
        var categories = new List<ForumCategory>();

        try
        {
            // Category definitions matching the 8 main categories
            var categoryDefinitions = new Dictionary<string, (string id, string name)>
            {
                ["fg_196"] = ("196", "华为论坛"),
                ["fg_213"] = ("213", "全新 Windows 论坛 - 微软全新一代操作系统"),
                ["fg_287"] = ("287", "硬件论坛 - 在这里我们只讨论硬件"),
                ["fg_86"] = ("86", "国内权威黑苹果论坛 - DIY你的苹果系统"),
                ["fg_106"] = ("106", "Microsoft 微软产品主题论坛"),
                ["fg_508"] = ("508", "GNU/Linux - 远景开源社区"),
                ["fg_15"] = ("15", "精华版块讨论区"),
                ["fg_7"] = ("7", "远景 - 服务区")
            };

            foreach (var categoryDef in categoryDefinitions)
            {
                var categoryId = categoryDef.Value.id;
                var categoryName = categoryDef.Value.name;
                var categoryDivId = categoryDef.Key;

                // Extract the entire category div: <div id="fg_XXX" ...>...</div>
                // More robust pattern that properly handles nested divs
                var categoryPattern = $@"<div[^>]*id=""{categoryDivId}""[^>]*class=""bm bmw[^>]*>.*?(?=<div[^>]*id=""fg_|<div[^>]*id=""online|$)";
                var categoryMatch = Regex.Match(htmlContent, categoryPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

                if (categoryMatch.Success)
                {
                    var categoryHtml = categoryMatch.Value;
                    var sections = ExtractSectionsFromCategoryHtml(categoryHtml, categoryId);

                    if (sections.Count > 0)
                    {
                        //部分板块使用本地图标，因为直接获取的是SVG格式，无法直接显示
                        if (categoryId == "196")
                        {
                            var timage1 = sections.FirstOrDefault(p => p.LogoUrl.Contains("common_574_icon"));
                            if (timage1 != null)
                            {
                                timage1.LogoUrl = "common_574_icon.png";
                            }
                            var timage2 = sections.FirstOrDefault(p => p.LogoUrl.Contains("common_575_icon"));
                            if (timage2 != null)
                            {
                                timage2.LogoUrl = "common_575_icon.png";
                            }
                        }
                        else if (categoryId == "213")
                        {
                            var timage1 = sections.FirstOrDefault(p => p.LogoUrl.Contains("common_451_icon"));
                            if (timage1 != null)
                            {
                                timage1.LogoUrl = "common_451_icon.png";
                            }
                            var timage2 = sections.FirstOrDefault(p => p.LogoUrl.Contains("common_469_icon"));
                            if (timage2 != null)
                            {
                                timage2.LogoUrl = "common_469_icon.png";
                            }
                            var timage3 = sections.FirstOrDefault(p => p.LogoUrl.Contains("common_548_icon")); //共用logo
                            if (timage3 != null)
                            {
                                timage3.LogoUrl = "common_548_icon.png";
                            }
                            timage3 = sections.FirstOrDefault(p => p.LogoUrl.Contains("common_237_icon")); //共用logo
                            if (timage3 != null)
                            {
                                timage3.LogoUrl = "common_548_icon.png";
                            }
                            var timage4 = sections.FirstOrDefault(p => p.LogoUrl.Contains("common_563_icon"));
                            if (timage4 != null)
                            {
                                timage4.LogoUrl = "common_563_icon.png";
                            }
                            var timage5 = sections.FirstOrDefault(p => p.LogoUrl.Contains("common_521_icon"));
                            if (timage5 != null)
                            {
                                timage5.LogoUrl = "common_521_icon.png";
                            }
                        }
                        else if (categoryId == "287")
                        {
                            var timage1 = sections.FirstOrDefault(p => p.LogoUrl.Contains("common_255_icon"));
                            if (timage1 != null)
                            {
                                timage1.LogoUrl = "common_255_icon.png";
                            }
                            var timage2 = sections.FirstOrDefault(p => p.LogoUrl.Contains("common_520_icon"));
                            if (timage2 != null)
                            {
                                timage2.LogoUrl = "common_520_icon.png";
                            }
                            var timage3 = sections.FirstOrDefault(p => p.LogoUrl.Contains("common_568_icon"));
                            if (timage3 != null)
                            {
                                timage3.LogoUrl = "common_568_icon.png";
                            }
                        }
                        else if (categoryId == "106")
                        {
                            var timage1 = sections.FirstOrDefault(p => p.LogoUrl.Contains("common_518_icon"));
                            if (timage1 != null)
                            {
                                timage1.LogoUrl = "common_518_icon.png";
                            }
                            var timage2 = sections.FirstOrDefault(p => p.LogoUrl.Contains("common_76_icon"));
                            if (timage2 != null)
                            {
                                timage2.LogoUrl = "common_76_icon.png";
                            }
                            var timage3 = sections.FirstOrDefault(p => p.LogoUrl.Contains("common_210_icon")); //共用logo
                            if (timage3 != null)
                            {
                                timage3.LogoUrl = "common_210_icon.png";
                            }
                            timage3 = sections.FirstOrDefault(p => p.LogoUrl.Contains("common_139_icon")); //共用logo
                            if (timage3 != null)
                            {
                                timage3.LogoUrl = "common_210_icon.png";
                            }
                            timage3 = sections.FirstOrDefault(p => p.LogoUrl.Contains("common_137_icon")); //共用logo
                            if (timage3 != null)
                            {
                                timage3.LogoUrl = "common_210_icon.png";
                            }
                            var timage4 = sections.FirstOrDefault(p => p.LogoUrl.Contains("common_555_icon"));
                            if (timage4 != null)
                            {
                                timage4.LogoUrl = "common_555_icon.png";
                            }
                        }

                        if(categoryId == "86")//URL是相对地址需要拼接
                        {
                            sections.ForEach(p =>
                            {
                                p.LogoUrl = "https://bbs.pcbeta.com"+p.LogoUrl;
                            });
                        }

                        categories.Add(new ForumCategory
                        {
                            CategoryId = categoryId,
                            CategoryName = categoryName,
                            Sections = sections
                        });

                        Debug.WriteLine($"Category {categoryName}: extracted {sections.Count} sections");
                    }
                }
                else
                {
                    Debug.WriteLine($"Category {categoryName} (div id: {categoryDivId}): pattern not matched");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Category HTML parsing error: {ex.Message}");
        }

        return categories;
    }

    /// <summary>
    /// Extracts individual forum sections from within a category HTML block
    /// Handles both single-column (col_0) and multi-column (col_2, col_3) layouts
    /// </summary>
    private List<ForumSection> ExtractSectionsFromCategoryHtml(string categoryHtml, string categoryId)
    {
        var sections = new List<ForumSection>();

        try
        {
            // Find the table within the category
            var tablePattern = @"<table[^>]*class=""fl_tb[^>]*>.*?</table>";
            var tableMatch = Regex.Match(categoryHtml, tablePattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (!tableMatch.Success)
                return sections;

            var tableHtml = tableMatch.Value;

            // Determine layout type and extract sections accordingly
            if (categoryId == "196")
            {
                // fg_196: col_0 single-column layout - extract from <tr> rows
                sections = ExtractSectionsFromSingleColumnLayout(tableHtml);
            }
            else
            {
                // fg_213, 287, 86, 106, 508, 15, 7: multi-column layout - extract from <td class="fl_*">
                sections = ExtractSectionsFromMultiColumnLayout(tableHtml, categoryId);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Section HTML parsing error: {ex.Message}");
        }

        return sections;
    }

    /// <summary>
    /// Extracts sections from single-column layout (fg_196)
    /// Each row contains one forum section with pattern: <tr><td class="fl_icn">...</td><td class="fl_ei"><dl>...</dl></td>...</tr>
    /// </summary>
    private List<ForumSection> ExtractSectionsFromSingleColumnLayout(string tableHtml)
    {
        var sections = new List<ForumSection>();

        try
        {
            // Extract each <tr> that contains forum data
            var rowPattern = @"<tr[^>]*>.*?</tr>";
            var rowMatches = Regex.Matches(tableHtml, rowPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

            foreach (Match rowMatch in rowMatches)
            {
                var rowHtml = rowMatch.Value;

                // Skip rows without data (e.g., rows with only <tr class="fl_row"></tr>)
                if (!rowHtml.Contains("<dl"))
                    continue;

                // Extract <dl> which contains the forum info
                var dlPattern = @"<dl[^>]*>(.*?)</dl>";
                var dlMatch = Regex.Match(rowHtml, dlPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

                if (!dlMatch.Success)
                    continue;

                var dlHtml = dlMatch.Value;

                // Extract forum link: <dt><a href="...">name</a>...
                var linkPattern = @"<dt[^>]*>.*?<a[^>]*href=""([^""]*forum-([a-z0-9]+)-\d+[^""]*)""[^>]*>([^<]+)</a>";
                var linkMatch = Regex.Match(dlHtml, linkPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

                if (!linkMatch.Success)
                    continue;

                var url = linkMatch.Groups[1].Value;
                var id = linkMatch.Groups[2].Value;
                var name = HtmlDecode(linkMatch.Groups[3].Value.Trim());

                if (string.IsNullOrEmpty(name))
                    continue;

                // Extract logo URL from the entire row (may be in left td)
                var logoUrl = ExtractLogoUrl(rowHtml);

                // Extract thread count and post count
                // Try to get from <span title="..."> first (contains actual number), then fallback to plain text
                var threadSpanMatch = Regex.Match(dlHtml, @"主题:\s*<span[^>]*title=""(\d+)""");
                var threadCount = threadSpanMatch.Success 
                    ? threadSpanMatch.Groups[1].Value 
                    : (Regex.Match(dlHtml, @"主题:\s*([^,<]+)").Groups[1].Value.Trim());

                var postSpanMatch = Regex.Match(dlHtml, @"帖数:\s*<span[^>]*title=""(\d+)""");
                var postCount = postSpanMatch.Success 
                    ? postSpanMatch.Groups[1].Value 
                    : (Regex.Match(dlHtml, @"帖数:\s*([^,<]+)").Groups[1].Value.Trim());

                // Extract today's new posts
                var todayNewPosts = ExtractTodayNewPosts(dlHtml);

                // Extract last reply information from fl_by container
                var lastReply = ExtractLastReplySingleColumn(rowHtml);

                var section = new ForumSection
                {
                    Id = id,
                    Name = name,
                    Url = url,
                    Description = "",
                    ThreadCount = threadCount,
                    PostCount = postCount,
                    TodayNewPosts = todayNewPosts,
                    LogoUrl = logoUrl,
                    LastReply = lastReply
                };

                sections.Add(section);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Single column layout parsing error: {ex.Message}");
        }

        return sections;
    }

    /// <summary>
    /// Extracts sections from multi-column layout (fg_213, 287, 86, 106, 508, 15, 7)
    /// Each cell <td class="fl_*"> contains one forum section
    /// Handles both tr-based layout (multiple rows with cells) and direct cell extraction
    /// </summary>
    private List<ForumSection> ExtractSectionsFromMultiColumnLayout(string tableHtml, string categoryId)
    {
        var sections = new List<ForumSection>();

        try
        {
            // Extract each <td class="fl_*"> that contains forum data
            // Match both fl_g, fl_icn_g and other fl_* classes
            var cellPattern = @"<td[^>]*class=""[^""]*fl_[^""]*""[^>]*>.*?</td>";
            var cellMatches = Regex.Matches(tableHtml, cellPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

            foreach (Match cellMatch in cellMatches)
            {
                var cellHtml = cellMatch.Value;

                // Skip empty cells or spacing cells
                if (!cellHtml.Contains("<dl") || !cellHtml.Contains("<a href"))
                    continue;

                // Extract <dl> which contains the forum info
                var dlPattern = @"<dl[^>]*>(.*?)</dl>";
                var dlMatch = Regex.Match(cellHtml, dlPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

                if (!dlMatch.Success)
                    continue;

                var dlHtml = dlMatch.Value;

                // Extract forum link: <dt><a href="...">name</a>...
                var linkPattern = @"<dt[^>]*>.*?<a[^>]*href=""([^""]*forum-([a-z0-9]+)-\d+[^""]*)""[^>]*>([^<]+)</a>";
                var linkMatch = Regex.Match(dlHtml, linkPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

                if (!linkMatch.Success)
                    continue;

                var url = linkMatch.Groups[1].Value.Trim();
                var id = linkMatch.Groups[2].Value.Trim();
                var name = HtmlDecode(linkMatch.Groups[3].Value.Trim());

                // Remove (X) suffix if present (e.g., "Forum Name (123)" -> "Forum Name")
                name = Regex.Replace(name, @"\s*\([^)]*\)\s*$", "").Trim();

                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(id))
                    continue;

                // Extract logo URL - look for img in the cell, preferring .fl_icn_g div
                var logoUrl = ExtractLogoUrl(cellHtml);

                // Skip sections without valid logo URL (often indicates data extraction issues)
                if (string.IsNullOrEmpty(logoUrl))
                    continue;

                // Extract thread count and post count from the entire cell
                // Try to get from <span title="..."> first (contains actual number), then fallback to plain text
                var threadSpanMatch = Regex.Match(cellHtml, @"主题:\s*<span[^>]*title=""(\d+)""");
                var threadCount = threadSpanMatch.Success 
                    ? threadSpanMatch.Groups[1].Value 
                    : (Regex.Match(cellHtml, @"主题:\s*([^,<]+)").Groups[1].Value.Trim());

                var postSpanMatch = Regex.Match(cellHtml, @"帖数:\s*<span[^>]*title=""(\d+)""");
                var postCount = postSpanMatch.Success 
                    ? postSpanMatch.Groups[1].Value 
                    : (Regex.Match(cellHtml, @"帖数:\s*([^,<]+)").Groups[1].Value.Trim());

                // Extract today's new posts
                var todayNewPosts = ExtractTodayNewPosts(dlHtml);

                // Extract last reply information - use different method based on category
                string lastReply;
                if (categoryId == "508")
                {
                    // fg_508 has different <dd> structure
                    lastReply = ExtractLastReplyFg508(dlHtml);
                }
                else
                {
                    // Other categories: fg_213, 287, 86, 106, 15, 7
                    lastReply = ExtractLastReplyMultiColumn(dlHtml);
                }

                // Validate and clean numeric fields
                threadCount = CleanNumericString(threadCount);
                postCount = CleanNumericString(postCount);

                var section = new ForumSection
                {
                    Id = id,
                    Name = name,
                    Url = url,
                    Description = "",
                    ThreadCount = threadCount,
                    PostCount = postCount,
                    TodayNewPosts = todayNewPosts,
                    LogoUrl = logoUrl,
                    LastReply = lastReply
                };

                sections.Add(section);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Multi column layout parsing error: {ex.Message}");
        }

        return sections;
    }

    /// <summary>
    /// Extracts logo URL from HTML section
    /// First tries to find img in .fl_icn_g container, then looks for any img tag
    /// </summary>
    private string ExtractLogoUrl(string html)
    {
        try
        {
            // First, try to find img in fl_icn_g (icon container)
            var iconContainerPattern = @"<div[^>]*class=""[^""]*fl_icn_g[^""]*""[^>]*>.*?<img[^>]*src=""([^""]+)""";
            var iconMatch = Regex.Match(html, iconContainerPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (iconMatch.Success)
            {
                var url = iconMatch.Groups[1].Value.Trim();
                if (!string.IsNullOrEmpty(url) && url != "javascript:void(0);")
                {
                    return url;
                }
            }

            // Fallback: find any img tag with src attribute
            var logoPattern = @"<img[^>]*src=""([^""]+)""[^>]*>";
            var logoMatch = Regex.Match(html, logoPattern, RegexOptions.IgnoreCase);

            if (logoMatch.Success)
            {
                var url = logoMatch.Groups[1].Value.Trim();
                if (!string.IsNullOrEmpty(url) && url != "javascript:void(0);")
                {
                    return url;
                }
            }
        }
        catch { }

        return string.Empty;
    }

    /// <summary>
    /// Extracts today's new posts count from HTML
    /// Looks for pattern: <em ... title="今日">(...)</em>
    /// </summary>
    private string ExtractTodayNewPosts(string html)
    {
        try
        {
            var todayPattern = @"<em[^>]*title=""今日""[^>]*>\s*\((\d+)\)\s*</em>";
            var todayMatch = Regex.Match(html, todayPattern, RegexOptions.IgnoreCase);

            if (todayMatch.Success)
            {
                return todayMatch.Groups[1].Value;
            }
        }
        catch { }

        return string.Empty;
    }

    /// <summary>
    /// Simple HTML entity decoder
    /// </summary>
    private string HtmlDecode(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        text = text.Replace("&lt;", "<");
        text = text.Replace("&gt;", ">");
        text = text.Replace("&amp;", "&");
        text = text.Replace("&quot;", "\"");
        text = text.Replace("&#39;", "'");
        text = text.Replace("&nbsp;", " ");
        text = Regex.Replace(text, @"&#(\d+);", match =>
        {
            if (int.TryParse(match.Groups[1].Value, out int charCode))
            {
                return char.ConvertFromUtf32(charCode);
            }
            return match.Value;
        });

        return text;
    }

    public string SpecialStringHandle(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;
        // 替换特殊字符串
        value = value.Replace("下载附件", "")
                     .Replace("保存到相册", "")
                     .Replace("设为封面", "")
                     .Replace("&quot;", "")
                     .Replace("&#39;", "")
                     .Replace("&nbsp;", "");
        return value;
    }

    /// <summary>
    /// Extracts thread information from HTML content
    /// Handles the PCBETA forum structure where each thread is in a <tbody id="normalthread_XXXXX"> container (regular threads)
    /// or <tbody id="stickthread_XXXXX"> container (pinned/sticky threads)
    /// with nested elements for title, author, time, replies, and views
    /// Sticky threads appear before the <tbody id="separatorline"> element, regular threads after
    /// </summary>
    private List<ThreadInfo> ExtractThreadsFromHtml(string htmlContent)
    {
        var threads = new List<ThreadInfo>();

        try
        {
            //  提取置顶帖 - 在 <tbody id="separatorline"> 之前的 stickthread_* 块
            var stickyThreads = ExtractThreadsByType(htmlContent, "stickthread", isSticky: true);
            threads.AddRange(stickyThreads);
            Debug.WriteLine($" 提取置顶帖: {stickyThreads.Count} 个");

            //  提取非置顶帖 - 在 <tbody id="separatorline"> 之后的 normalthread_* 块
            var regularThreads = ExtractThreadsByType(htmlContent, "normalthread", isSticky: false);
            threads.AddRange(regularThreads);
            Debug.WriteLine($" 提取非置顶帖: {regularThreads.Count} 个");

            Debug.WriteLine($" 总共提取线程数: {threads.Count}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 线程HTML解析错误: {ex.Message}");
        }

        return threads;
    }

    /// <summary>
    ///  新增：按类型提取线程（置顶或普通）
    /// 提取指定类型的线程，并为每个线程设置IsSticky标志
    /// </summary>
    private List<ThreadInfo> ExtractThreadsByType(string htmlContent, string threadType, bool isSticky)
    {
        var threads = new List<ThreadInfo>();

        try
        {
            // 根据线程类型构建正则模式
            var threadTypeId = threadType == "stickthread" ? "stickthread" : "normalthread";
            var threadPattern = $@"<tbody[^>]*id=""{threadTypeId}_(\d+)""[^>]*>(.*?)</tbody>";
            var matches = Regex.Matches(htmlContent, threadPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

            Debug.WriteLine($"ℹ️ 查找{(isSticky ? "置顶" : "普通")}帖({threadTypeId}): 找到 {matches.Count} 个匹配");

            foreach (Match match in matches)
            {
                try
                {
                    if (match.Groups.Count < 3)
                        continue;

                    var threadId = match.Groups[1].Value.Trim();
                    var threadHtml = match.Groups[2].Value;

                    // Extract title and URL from <a class="s xst" href="...viewthread-XXXXX-..."> or similar
                    var titlePattern = @"<a[^>]*href=""([^""]*viewthread-(\d+)[^""]*)""[^>]*class=""[^""]*(?:s\s+)?xst[^""]*""[^>]*>([^<]+)</a>";
                    var titleMatch = Regex.Match(threadHtml, titlePattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

                    if (!titleMatch.Success)
                    {
                        // Fallback: try to match without the strict class pattern
                        titlePattern = @"<a[^>]*href=""([^""]*viewthread-(\d+)[^""]*)""[^>]*>([^<]+)</a>";
                        titleMatch = Regex.Match(threadHtml, titlePattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    }

                    if (!titleMatch.Success)
                        continue;

                    var url = titleMatch.Groups[1].Value.Trim();
                    var title = HtmlDecode(titleMatch.Groups[3].Value.Trim());

                    // Extract author: look for <a href="...space-uid-XXXXX.html"> or <a href="...space-username-...html">
                    var authorPattern = @"<a[^>]*href=""[^""]*(?:space-uid-|space-username-)([^""]+)""[^>]*>([^<]+)</a>";
                    var authorMatch = Regex.Match(threadHtml, authorPattern, RegexOptions.IgnoreCase);
                    var author = authorMatch.Success ? HtmlDecode(authorMatch.Groups[2].Value.Trim()) : "Unknown";

                    // Extract post time: look for <span> with date/time format like "2026-3-16 15:52"
                    var timePattern = @"<span[^>]*>(\d{4}-\d{1,2}-\d{1,2}\s+\d{1,2}:\d{2})</span>";
                    var timeMatch = Regex.Match(threadHtml, timePattern, RegexOptions.IgnoreCase);
                    var postTime = DateTime.Now;
                    if (timeMatch.Success)
                    {
                        var timeStr = timeMatch.Groups[1].Value;
                        if (DateTime.TryParse(timeStr, out var parsedTime))
                        {
                            postTime = parsedTime;
                        }
                    }

                    // Extract replies and views
                    // Pattern: <a>34</a><em>754</em> where 34 is replies and 754 is views
                    // Look for numeric pattern within <a> and <em> tags in the stats area
                    var repliesPattern = @"<a[^>]*>(\d+)</a><em>(\d+)</em>";
                    var repliesMatch = Regex.Match(threadHtml, repliesPattern, RegexOptions.IgnoreCase);

                    var replies = 0;
                    var views = 0;

                    if (repliesMatch.Success)
                    {
                        if (int.TryParse(repliesMatch.Groups[1].Value, out var r))
                            replies = r;
                        if (int.TryParse(repliesMatch.Groups[2].Value, out var v))
                            views = v;
                    }

                    // Create ThreadInfo object
                    var thread = new ThreadInfo
                    {
                        Id = threadId,
                        Url = url,
                        Title = title,
                        Author = author,
                        PostTime = postTime,
                        Replies = replies,
                        Views = views,
                        Category = "[讨论]",  // Default category
                        IsSticky = isSticky   //  设置是否为置顶帖
                    };

                    threads.Add(thread);
                    Debug.WriteLine($" 提取{(isSticky ? "置顶" : "普通")}帖: {threadId} - {title} 作者: {author}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"❌ 解析单个线程错误: {ex.Message}");
                    continue;
                }
            }

            Debug.WriteLine($" {(isSticky ? "置顶" : "普通")}帖共提取: {threads.Count} 个");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 按类型提取线程错误({(isSticky ? "sticky" : "normal")}): {ex.Message}");
        }

        return threads;
    }

    /// <summary>
    /// Extracts thread content from HTML
    /// </summary>
    private ThreadContent ExtractThreadContentFromHtml(string htmlContent)
    {
        var content = new ThreadContent
        {
            Id = "",
            Title = "",
            Author = "",
            PostTime = "发表于 --",
            PlainTextContent = "",
            RawHtmlContent = htmlContent,
            Replies = 0
        };

        try
        {
            // 1. 提取标题
            var titleMatch = Regex.Match(htmlContent, @"<h1[^>]*class=""ts""[^>]*>.*?<span[^>]*id=""thread_subject""[^>]*>([^<]+)</span>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (titleMatch.Success)
            {
                content.Title = HtmlDecode(titleMatch.Groups[1].Value.Trim());
            }

            // 2. 提取作者和发布时间
            var authorMatch = Regex.Match(htmlContent, @"<a[^>]*class=""xi2 an""[^>]*>([^<]+)</a>", RegexOptions.IgnoreCase);
            if (authorMatch.Success)
            {
                content.Author = HtmlDecode(authorMatch.Groups[1].Value.Trim());
            }

            // 提取时间戳
            var timeMatch = Regex.Match(htmlContent, @"<em id="".*?"">(.*?)</em>", RegexOptions.IgnoreCase);
            if (timeMatch.Success)
            {
                content.PostTime = timeMatch.Groups[1].Value;
            }

            // 提取其他信息
            var otherIfm = Regex.Match(htmlContent, @"<a class=""pipe"">\|</a>(.*?)</div>", RegexOptions.IgnoreCase);
            if (otherIfm.Success)
            {
                content.OtherIfm = otherIfm.Groups[1].Value;
            }

			//  新增：直接在楼主内容范围内寻找 pattl 附件（避免读取跟帖中的附件）
			// 提取楼主的内容范围：从第一个 <div id="post_*"> 开始，到下一个 <div id="post_*"> 结束
			var threadOwnerContent = ExtractThreadOwnerContent(htmlContent);


			//  新增：提取编辑状态信息 - 格式：<i class="pstatus"> 本帖最后由 用户名 于 日期 编辑 </i>
			var editStatusMatch = Regex.Match(threadOwnerContent, @"<i[^>]*class=""pstatus""[^>]*>\s*本帖最后由\s*([^\s于]+)\s*于\s*([^\s]+(?:\s+\d+:\d+)?)\s*编辑\s*</i>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (editStatusMatch.Success)
            {
                var editedBy = HtmlDecode(editStatusMatch.Groups[1].Value.Trim());
                var editedAt = editStatusMatch.Groups[2].Value.Trim();
                content.EditStatus = $"本帖最后由 {editedBy} 于 {editedAt} 编辑";
                Debug.WriteLine($" 提取编辑状态: {content.EditStatus}");
            }

            //  新增：提取审核通过信息 - 格式：<div class="modact"><a ...>本主题由 用户名 于 日期 审核通过</a></div>
            var moderationMatch = Regex.Match(htmlContent, @"<div[^>]*class=""modact""[^>]*>.*?<a[^>]*>(.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (moderationMatch.Success)
            {
                content.ModerationInfo = HtmlDecode(moderationMatch.Groups[1].Value.Trim());
                Debug.WriteLine($" 提取审核信息: {content.ModerationInfo}");
            }

            // 3. 提取帖子内容容器（正确处理嵌套的 <td> 标签）
            var messageHtml = ExtractMessageContent(htmlContent);

            if (!string.IsNullOrEmpty(messageHtml))
            {
                // 4. 使用富文本解析器解析内容（删除编辑状态，因为已在上层单独提取并显示）
                content.ContentElements = ParseRichTextContent(messageHtml, skipEditStatus: false);

                // 5. 生成纯文本版本（用于搜索、索引等）
                content.PlainTextContent = GeneratePlainText(content.ContentElements);
            }

            if (!string.IsNullOrEmpty(htmlContent))
            {
                var pattlAttachments = ExtractAllPattlAttachments(threadOwnerContent);
                if (pattlAttachments != null && pattlAttachments.Count > 0)
                {
                    content.ContentElements.AddRange(pattlAttachments);
                    Debug.WriteLine($" 从 pattl 容器添加 {pattlAttachments.Count} 个附件到内容元素");
                }
            }

            //  新增：提取评论和评分信息
            var comments = ExtractCommentsFromHtml(htmlContent);
            if (comments != null && comments.Count > 0)
            {
                content.Comments = comments;
                Debug.WriteLine($" 提取评论: {comments.Count} 条");
            }

            var ratingSummary = ExtractRatingsFromHtml(threadOwnerContent);
            if (ratingSummary != null)
            {
                content.RatingSummary = ratingSummary;
                Debug.WriteLine($" 提取评分: 总数={ratingSummary.TotalRatingCount}, 详情数={ratingSummary.RatingDetails.Count}");
            }

            //  新增：提取回帖（用户回复）
            var replies = ExtractRepliesFromHtml(htmlContent);
            if (replies != null && replies.Count > 0)
            {
                content.ReplyList = replies;
                Debug.WriteLine($" 提取回帖: {replies.Count} 条");
            }

            Debug.WriteLine($" 已提取帖子内容 - 标题: {content.Title}, 作者: {content.Author}, 元素数: {content.ContentElements.Count}, 评论数: {content.Comments?.Count ?? 0}, 评分数: {content.RatingSummary?.TotalRatingCount ?? 0}, 回帖数: {content.ReplyList?.Count ?? 0}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 线程内容解析错误: {ex.Message}\n{ex.StackTrace}");
        }

        return content;
    }

    /// <summary>
    /// Extracts post message content from HTML by properly handling nested &lt;td&gt; tags
    /// Uses tag counting approach instead of naive regex to find the correct closing &lt;/td&gt;
    /// Pattern: finds &lt;td class="t_f" id="postmessage_\d+"&gt; and extracts until matching &lt;/td&gt;
    /// </summary>
    private string ExtractMessageContent(string htmlContent)
    {
        try
        {
            // Find the opening <td class="t_f" id="postmessage_\d+"> tag
            var openingPattern = @"<td[^>]*class=""t_f""[^>]*id=""postmessage_(\d+)""[^>]*>";
            var openingMatch = Regex.Match(htmlContent, openingPattern, RegexOptions.IgnoreCase);

            if (!openingMatch.Success)
            {
                Debug.WriteLine("❌ Opening <td class=\"t_f\"> tag not found");
                return string.Empty;
            }

            int startIndex = openingMatch.Index + openingMatch.Length;
            int nestingLevel = 1;  // We've already counted the opening <td>
            int currentIndex = startIndex;

            // Track the position for extracting content
            while (currentIndex < htmlContent.Length && nestingLevel > 0)
            {
                // Find the next <td or </td tag
                int tdOpenIndex = htmlContent.IndexOf("<td", currentIndex, StringComparison.OrdinalIgnoreCase);
                int tdCloseIndex = htmlContent.IndexOf("</td>", currentIndex, StringComparison.OrdinalIgnoreCase);

                if (tdCloseIndex == -1)
                {
                    // No closing </td> found - return what we have so far as partial match
                    Debug.WriteLine("⚠️ No matching </td> found, returning partial content");
                    return htmlContent.Substring(startIndex);
                }

                if (tdOpenIndex != -1 && tdOpenIndex < tdCloseIndex)
                {
                    // Found an opening <td> before the closing </td>
                    // Check if it's a complete <td ...> tag (ends with >)
                    int tagEndIndex = htmlContent.IndexOf(">", tdOpenIndex, StringComparison.OrdinalIgnoreCase);
                    if (tagEndIndex != -1 && tagEndIndex < tdCloseIndex)
                    {
                        nestingLevel++;
                        currentIndex = tagEndIndex + 1;
                    }
                    else
                    {
                        // Malformed tag, skip it
                        currentIndex = tdOpenIndex + 3;
                    }
                }
                else
                {
                    // No more opening <td> tags, process closing </td>
                    nestingLevel--;
                    if (nestingLevel == 0)
                    {
                        // Found the matching closing tag
                        return htmlContent.Substring(startIndex, tdCloseIndex - startIndex);
                    }
                    currentIndex = tdCloseIndex + 5;  // 5 = length of "</td>"
                }
            }

            Debug.WriteLine("⚠️ Nesting level mismatch, returning partial content");
            return htmlContent.Substring(startIndex);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Error extracting message content: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// 解析富文本内容 - 支持文本、图片、附件、表情、表格等
    /// 基于实际的PCBETA论坛HTML结构
    /// 支持 <ignore_js_op> 标签，其内的纯文本标记为横向排列，图片/附件等保持正常
    ///  新增：为 ignore_js_op 块内的图片和附件创建分组面板，显示为 [图片] 文件名 下载按钮 大小 日期
    ///  新增：skipEditStatus 参数 - 如果为 true，则不删除编辑状态标记，因为已在上层提取
    /// 
    /// 注意：pattl div（版主操作后的附件容器）不在这个方法的处理范围内
    /// pattl div 应在 ExtractThreadContentFromHtml() 中单独处理
    /// </summary>
    private List<ContentElement> ParseRichTextContent(string htmlContent, bool skipEditStatus = false)
    {
        var elements = new List<ContentElement>();
        string originalHtmlContent = htmlContent;  //  保存原始内容用于日期提取

        try
        {
            // 删除script和style标签
            htmlContent = Regex.Replace(htmlContent, @"<script[^>]*>.*?</script>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            htmlContent = Regex.Replace(htmlContent, @"<style[^>]*>.*?</style>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);

            //  改进：条件性地删除编辑状态标记
            // 如果 skipEditStatus 为 true，说明编辑状态已在上层提取，就不删除
            // 否则删除以保持向后兼容性
            if (!skipEditStatus)
            {
                htmlContent = Regex.Replace(htmlContent, @"<i[^>]*class=""pstatus""[^>]*>.*?</i>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            }

            // 删除审核通过信息容器，因为已在上层提取
            htmlContent = Regex.Replace(htmlContent, @"<div[^>]*class=""modact""[^>]*>.*?</div>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);

            // 处理 <font> 标签 - 递归处理嵌套的font标签，保留内容
            while (htmlContent.Contains("<font"))
            {
                htmlContent = Regex.Replace(htmlContent, @"<font[^>]*>", "", RegexOptions.IgnoreCase);
                htmlContent = Regex.Replace(htmlContent, @"</font>", "", RegexOptions.IgnoreCase);
            }

            // 处理 <u> 标签
            htmlContent = Regex.Replace(htmlContent, @"</?u[^>]*>", "", RegexOptions.IgnoreCase);

            // 分割内容，识别 <ignore_js_op> 块
            var ignorePattern = @"<ignore_js_op[^>]*>(.*?)</ignore_js_op>";
            var parts = Regex.Split(htmlContent, ignorePattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

            // parts[0] = 第一段外部内容
            // parts[1] = 第一段 ignore_js_op 内部内容
            // parts[2] = 第二段外部内容
            // parts[3] = 第二段 ignore_js_op 内部内容
            // ...以此类推

            int horizontalGroupCounter = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                if (string.IsNullOrEmpty(parts[i]))
                    continue;

                if (i % 2 == 0)
                {
                    // 偶数索引 = 外部内容，正常解析，不分组
                    var externalElements = ParseContentBlock(parts[i], horizontalGroupId: null, suggestedFileName: null, fullHtmlContent: originalHtmlContent);
                    elements.AddRange(externalElements);
                }
                else
                {
                    // 奇数索引 = ignore_js_op 内部内容，分配分组 ID
                    horizontalGroupCounter++;
                    var groupId = $"ignore_js_op_{horizontalGroupCounter}";

                    // 从 ignore_js_op 块中提取文件名
                    var fileName = ExtractFileNameFromIgnoreBlock(parts[i]);

                    var internalElements = ParseContentBlock(parts[i], horizontalGroupId: groupId, suggestedFileName: fileName, fullHtmlContent: originalHtmlContent);
                    elements.AddRange(internalElements);
                }
            }

            Debug.WriteLine($" 解析富文本内容完成，共 {elements.Count} 个元素");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 富文本解析错误: {ex.Message}");
        }

        return elements;
    }

    /// <summary>
    /// 解析内容块（可能是 ignore_js_op 内部或外部，或表格单元格内）
    /// </summary>
    /// <param name="htmlContent">HTML 内容</param>
    /// <param name="horizontalGroupId">如果不为 null，则所有文本类型元素将分配此分组 ID（用于在同一行横向显示）</param>
    /// <param name="suggestedFileName">建议的文件名，用于附件链接（来自 ignore_js_op 块中的文件信息）</param>
    /// <param name="isTableCell">是否在表格单元格内 - 如果为 true，<br/> 标签将被当作 LineBreak，而不是 Separator</param>
    /// <param name="fullHtmlContent">完整的HTML内容，用于从隐藏菜单中提取日期</param>
    private List<ContentElement> ParseContentBlock(string htmlContent, string? horizontalGroupId, string? suggestedFileName = null, bool isTableCell = false, string? fullHtmlContent = null)
    {
        var elements = new List<ContentElement>();

        try
        {
            // 使用 Regex.Matches 提取所有块级元素和完整的标签对，避免标签被分割
            // 策略：先匹配所有标签，然后在标签之间填充文本内容
            // 这样确保每个 token 要么是完整的标签块，要么是纯文本内容
            var tokens = SplitContentIntoTokens(htmlContent);

            for (int i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];

                if (string.IsNullOrEmpty(token))
                    continue;

                var trimmedToken = token.Trim();

                if (string.IsNullOrEmpty(trimmedToken))
                    continue;

                //  新增：跳过隐藏元素（style="display: none"）和提示菜单容器
                if (ShouldSkipHiddenElement(trimmedToken))
                {
                    continue;
                }

                token = token.Contains("&nbsp;") ? token.Replace("&nbsp;", " ") : token;

                if (Regex.IsMatch(trimmedToken, @"<hr\s*/?.*?>", RegexOptions.IgnoreCase))
                {
                    elements.Add(new ContentElement { Type = ContentElementType.Separator });
                    continue;
                }

                // 处理图片（必须先于表情检查，因为表情也是img标签）
                // 图片和表情不标记为横向排列
                if (trimmedToken.StartsWith("<img", StringComparison.OrdinalIgnoreCase))
                {
                    // 检查是否是表情（有smilieid属性）
                    if (trimmedToken.Contains("smilieid"))
                    {
                        var emojiElement = ParseEmojiTag(trimmedToken);
                        if (emojiElement != null)
                        {
                            elements.Add(emojiElement);
                        }
                    }
                    else
                    {
                        // 普通图片 - 检查后续是否有附件信息 div
                        var imgElement = ParseImageTag(trimmedToken);
                        if (imgElement != null)
                        {
                            //  新增：为ignore_js_op块内的图片应用HorizontalGroupId，与附件信息分组显示
                            if (horizontalGroupId != null&& !htmlContent.Contains("保存到相册"))
                            {
                                imgElement.HorizontalGroupId = horizontalGroupId;
                            }

                            elements.Add(imgElement);

                            //  新增：检查后续 token 是否是 <div class="tip"> 附件信息容器
                            if (i + 1 < tokens.Count)
                            {
                                var nextToken = tokens[i + 1].Trim();
                                if (nextToken.StartsWith("<div", StringComparison.OrdinalIgnoreCase) && 
                                    nextToken.Contains("aimg_tip"))
                                {
                                    // 这是附件信息容器，提取其中的链接和文件名
                                    var attachmentInfo = ExtractAttachmentInfoFromTipDiv(nextToken);
                                    if (attachmentInfo != null)
                                    {
                                        //  新增：为附件也应用HorizontalGroupId，确保与图片在同一行
                                        if (horizontalGroupId != null && !htmlContent.Contains("保存到相册"))
                                        {
                                            attachmentInfo.HorizontalGroupId = horizontalGroupId;
                                        }
                                        elements.Add(attachmentInfo);
                                        i++;  // 跳过已处理的 div token
                                    }
                                }
                            }
                        }
                    }
                    continue;
                }

                // 处理链接和附件
                // 附件不标记为横向排列
                if (trimmedToken.StartsWith("<a", StringComparison.OrdinalIgnoreCase))
                {
                    //增加特殊规则
                    if(trimmedToken.Contains("保存到相册") || trimmedToken.Contains("设为封面"))
                    {
                        continue;
                    }

                    var linkElement = ParseLinkTag(trimmedToken, "", suggestedFileName);
                    if (linkElement != null)
                    {
                        elements.Add(linkElement);
                    }
                    continue;
                }

                // 处理表格
                if (trimmedToken.StartsWith("<table", StringComparison.OrdinalIgnoreCase))
                {
                    var tableElement = ParseTableTag(trimmedToken);
                  if (tableElement != null)
                    {
                        elements.Add(tableElement);
                    }
                    continue;
                }

                // 处理表格单元格内容 (<td> 和 <th>)
                if (trimmedToken.StartsWith("<td", StringComparison.OrdinalIgnoreCase) || trimmedToken.StartsWith("<th", StringComparison.OrdinalIgnoreCase))
                {
                    var cellContent = Regex.Match(trimmedToken, @"<(?:td|th)[^>]*>(.*?)(?=</(?:td|th)>|$)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    if (cellContent.Success)
                    {
                        var text = HtmlDecode(StripHtmlTags(cellContent.Groups[1].Value)).Trim();
                        if (!string.IsNullOrEmpty(text) && !text.All(char.IsWhiteSpace))
                        {
                            elements.Add(new ContentElement
                            {
                                Type = ContentElementType.Text,
                                Text = text,
                                HorizontalGroupId = horizontalGroupId
                            });
                        }
                    }
                    continue;
                }

                // 处理 <span> 和其他内联元素
                if (trimmedToken.StartsWith("<span", StringComparison.OrdinalIgnoreCase))
                {
                    //  新增：检查是否是附件容器 <span id="attach_*">
                    if (trimmedToken.Contains("id=\"attach_"))
                    {
                        // 这是一个附件容器，特殊处理
                        var attachmentElement = ParseAttachmentSpan(trimmedToken, horizontalGroupId, fullHtmlContent);
                        if (attachmentElement != null)
                        {
                            elements.Add(attachmentElement);
                        }
                    }
                    else
                    {
                        var spanContent = Regex.Match(trimmedToken, @"<span[^>]*>(.*?)(?=</span>|$)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                        if (spanContent.Success)
                        {
                            var text = HtmlDecode(StripHtmlTags(spanContent.Groups[1].Value)).Trim();
                            if (!string.IsNullOrEmpty(text) && !text.All(char.IsWhiteSpace))
                            {
                                elements.Add(new ContentElement
                                {
                                    Type = ContentElementType.Text,
                                    Text = text,
                                    HorizontalGroupId = horizontalGroupId
                                });
                            }
                        }
                    }
                    continue;
                }

                // 处理引用块
                if (trimmedToken.StartsWith("<blockquote", StringComparison.OrdinalIgnoreCase))
                {
                    var quoteContent = Regex.Match(trimmedToken, @"<blockquote[^>]*>(.*?)</blockquote>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    if (quoteContent.Success)
                    {
                        var quoteText = StripHtmlTags(quoteContent.Groups[1].Value).Trim();
                        if (!string.IsNullOrEmpty(quoteText))
                        {
                            elements.Add(new ContentElement
                            {
                                Type = ContentElementType.Quote,
                                Text = quoteText,
                                HorizontalGroupId = horizontalGroupId
                            });
                        }
                    }
                    continue;
                }

                // 处理代码块
                if (trimmedToken.StartsWith("<pre", StringComparison.OrdinalIgnoreCase))
                {
                    var codeContent = Regex.Match(trimmedToken, @"<pre[^>]*>(.*?)</pre>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    if (codeContent.Success)
                    {
                        var codeText = HtmlDecode(codeContent.Groups[1].Value).Trim();
                        if (!string.IsNullOrEmpty(codeText))
                        {
                            elements.Add(new ContentElement
                            {
                                Type = ContentElementType.Code,
                                Text = codeText,
                                HorizontalGroupId = horizontalGroupId
                            });
                        }
                    }
                    continue;
                }

                // 处理加粗 <strong> 或 <b>
                if (trimmedToken.StartsWith("<strong", StringComparison.OrdinalIgnoreCase) || (trimmedToken.StartsWith("<b", StringComparison.OrdinalIgnoreCase) && !trimmedToken.StartsWith("<br")))
                {
                    var boldContent = Regex.Match(trimmedToken, @"<(?:strong|b)[^>]*>(.*?)</(?:strong|b)>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    if (boldContent.Success)
                    {
                        var boldText = HtmlDecode(boldContent.Groups[1].Value).Trim();
                        if (!string.IsNullOrEmpty(boldText))
                        {
                            elements.Add(new ContentElement
                            {
                                Type = ContentElementType.Bold,
                                Text = boldText,
                                HorizontalGroupId = horizontalGroupId
                            });
                        }
                    }
                    continue;
                }

                // 处理斜体 <em> 或 <i>（排除<i class="pstatus">已经删除）
                if (trimmedToken.StartsWith("<em", StringComparison.OrdinalIgnoreCase) || trimmedToken.StartsWith("<i", StringComparison.OrdinalIgnoreCase))
                {
                    var italicContent = Regex.Match(trimmedToken, @"<(?:em|i)[^>]*>(.*?)</(?:em|i)>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    if (italicContent.Success)
                    {
                        var italicText = HtmlDecode(italicContent.Groups[1].Value).Trim();
                        if (!string.IsNullOrEmpty(italicText))
                        {
                            elements.Add(new ContentElement
                            {
                                Type = ContentElementType.Italic,
                                Text = italicText,
                                HorizontalGroupId = horizontalGroupId
                            });
                        }
                    }
                    continue;
                }

                // 处理 <div> 和 <p> 中的纯文本
                if (trimmedToken.StartsWith("<div", StringComparison.OrdinalIgnoreCase) || trimmedToken.StartsWith("<p", StringComparison.OrdinalIgnoreCase))
                {
                    var divContent = Regex.Match(trimmedToken, @"<(?:div|p)[^>]*>(.*?)</(?:div|p)>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    if (divContent.Success)
                    {
                        var text = HtmlDecode(divContent.Groups[1].Value).Trim();
                        if (!string.IsNullOrEmpty(text) && !text.All(char.IsWhiteSpace))
                        {
                            elements.Add(new ContentElement
                            {
                                Type = ContentElementType.Text,
                                Text = text,
                                HorizontalGroupId = horizontalGroupId
                            });
                        }
                    }
                    continue;
                }

                // 处理 <br> 和 <hr> 标签
                if (Regex.IsMatch(trimmedToken, @"<br\s*/?.*?>", RegexOptions.IgnoreCase))
                {
                    elements.Add(new ContentElement { Type = ContentElementType.LineBreak });
                    continue;
                }

                // 处理纯文本节点（不以 < 开头的内容）
                // 由于 SplitContentIntoTokens 确保纯文本已经被分离，这里可以直接处理
                if (!trimmedToken.StartsWith("<", StringComparison.OrdinalIgnoreCase))
                {
                    var text = HtmlDecode(trimmedToken).Trim();

                    if (!string.IsNullOrEmpty(text) && !text.All(char.IsWhiteSpace))
                    {
                        elements.Add(new ContentElement
                        {
                            Type = ContentElementType.Text,
                            Text = text,
                            HorizontalGroupId = horizontalGroupId
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 内容块解析错误: {ex.Message}");
        }

        return elements;
    }

    /// <summary>
    /// 从附件信息 Tip Div 容器中提取下载链接、文件名、大小、下载次数和上传时间
    /// 结构（已购买附件）：<div class="tip tip_4 aimg_tip" id="...">
    ///       <div class="xs0">
    ///         <p><strong>文件名</strong> <em class="xg1">(86.28 KB, 下载次数: 7)</em></p>
    ///         <p><a href="下载链接">下载附件</a> ...</p>
    ///         <p class="xg1 y">2026-3-18 15:21 上传</p>
    ///       </div></div>
    ///  新增：支持两种结构的附件处理
    /// - 已购买附件：返回 SalePrice = null（表示已购买）
    /// - 未购买附件：返回 SalePrice > 0（表示需要购买）
    /// </summary>
    private ContentElement? ExtractAttachmentInfoFromTipDiv(string tipDivHtml)
    {
        try
        {
            // 提取文件名（在 <strong> 标签中）
            var fileNameMatch = Regex.Match(tipDivHtml, @"<strong[^>]*>([^<]+)</strong>", RegexOptions.IgnoreCase);
            if (!fileNameMatch.Success)
                return null;

            var fileName = HtmlDecode(fileNameMatch.Groups[1].Value.Trim());
            if (string.IsNullOrEmpty(fileName))
                return null;

            // 提取文件大小和下载次数（在 <em> 标签中）
            // 格式: (86.28 KB, 下载次数: 7) 或 (2.5 MB, 下载次数: 123)
            var fileSizeMatch = Regex.Match(tipDivHtml, @"<em[^>]*>\s*\(([^,]+),\s*下载次数:\s*(\d+)\)\s*</em>", RegexOptions.IgnoreCase);
            var fileSize = "";
            var downloadCount = "";

            if (fileSizeMatch.Success)
            {
                fileSize = fileSizeMatch.Groups[1].Value.Trim();  // e.g., "86.28 KB"
                downloadCount = fileSizeMatch.Groups[2].Value.Trim();  // e.g., "7"
                Debug.WriteLine($" 提取文件大小={fileSize}, 下载次数={downloadCount}");
            }

            // 提取下载链接（href 属性中包含 attachment 的 <a> 标签）
            var downloadLinkMatch = Regex.Match(
                tipDivHtml, 
                @"<a[^>]*href=""([^""]*(?:attachment|download)[^""]*)""[^>]*>([^<]*)</a>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline
            );

            if (downloadLinkMatch.Success)
            {
                var url = HtmlDecode(downloadLinkMatch.Groups[1].Value.Trim());

                //  增强：将文件大小包含在 FileSize 字段中，格式：大小 (下载次数)
                string fileSizeDisplay = fileSize;
                if (!string.IsNullOrEmpty(downloadCount))
                {
                    fileSizeDisplay = $"{fileSize} ({downloadCount}次)";
                }

                //  提取上传时间：<p class="xg1 y">2026-3-18 15:21 上传</p>
                var uploadTimeMatch = Regex.Match(
                    tipDivHtml,
                    @"<p[^>]*class=""[^""]*xg1[^""]*""[^>]*>\s*([^<]*上传)\s*</p>",
                    RegexOptions.IgnoreCase
                );
                var uploadTime = "";
                if (uploadTimeMatch.Success)
                {
                    uploadTime = uploadTimeMatch.Groups[1].Value.Trim();  // e.g., "2026-3-18 15:21 上传"
                    // 移除末尾的 "上传" 字符，只保留时间戳
                    uploadTime = Regex.Replace(uploadTime, @"\s*上传\s*$", "", RegexOptions.IgnoreCase).Trim();
                    Debug.WriteLine($" 提取上传时间={uploadTime}");
                }

                //  新增：从 div id 提取附件ID
                string? attachmentId = null;
                var idMatch = Regex.Match(tipDivHtml, @"id=""aimg_(\d+)""", RegexOptions.IgnoreCase);
                if (idMatch.Success)
                {
                    attachmentId = idMatch.Groups[1].Value;
                }

                Debug.WriteLine($" 提取附件信息: 文件名={fileName}, 大小={fileSizeDisplay}, 上传时间={uploadTime}, URL={url.Substring(0, Math.Min(60, url.Length))}..., 已购买");

                return new ContentElement
                {
                    Type = ContentElementType.Attachment,
                    Url = url,
                    FileName = fileName,
                    FileSize = fileSizeDisplay,
                    UploadTime = uploadTime,
                    AttachmentId = attachmentId,
                    SalePrice = null  //  null 表示已购买附件
                };
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 提取附件信息错误: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    ///  新增：提取楼主发布的内容范围（不包括跟帖/回复）
    /// 楼主的内容在第一个 &lt;div id="post_*"&gt; 到下一个 &lt;div id="post_*"&gt; 之间
    /// 这样可以确保 ExtractAllPattlAttachments 只在楼主的内容范围内搜索附件
    /// 避免读取跟帖中的附件
    /// </summary>
    private string? ExtractThreadOwnerContent(string htmlContent)
    {
        try
        {
            // 查找第一个 <div id="post_*"> 的位置
            var firstPostPattern = @"<div[^>]*id=""post_(\d+)""[^>]*>";
            var firstPostMatch = Regex.Match(htmlContent, firstPostPattern, RegexOptions.IgnoreCase);

            if (!firstPostMatch.Success)
            {
                Debug.WriteLine("⚠️ 未找到第一个 post div");
                return null;
            }

            int firstPostStartIndex = firstPostMatch.Index;
            Debug.WriteLine($" 找到第一个 post div，位置: {firstPostStartIndex}");

            // 查找下一个 <div id="post_*"> 的位置（这是第二楼/回复的开始）
            var remainingContent = htmlContent.Substring(firstPostMatch.Index + firstPostMatch.Length);
            var secondPostMatch = Regex.Match(remainingContent, firstPostPattern, RegexOptions.IgnoreCase);

            int endIndex;
            if (secondPostMatch.Success)
            {
                // 找到了第二个 post div，截取到那里
                endIndex = firstPostMatch.Index + firstPostMatch.Length + secondPostMatch.Index;
                Debug.WriteLine($" 找到第二个 post div，位置: {endIndex}，截取范围: {firstPostStartIndex} - {endIndex}");
            }
            else
            {
                // 没有找到第二个 post div，说明只有楼主的内容，使用整个文档
                endIndex = htmlContent.Length;
                Debug.WriteLine($" 未找到第二个 post div，使用到文档末尾，截取范围: {firstPostStartIndex} - {endIndex}");
            }

            // 提取楼主内容范围
            var threadOwnerContent = htmlContent.Substring(firstPostStartIndex, endIndex - firstPostStartIndex);
            Debug.WriteLine($" 成功提取楼主内容范围，长度: {threadOwnerContent.Length}");
            return threadOwnerContent;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 提取楼主内容范围错误: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///  新增：直接从htmlContent中提取所有pattl附件
    /// 此方法跳过提取pattl容器本身，而是直接在htmlContent中查找所有 &lt;dl class="tattl"&gt; 块
    /// 这样避免了非贪心匹配在第一个嵌套 &lt;/div&gt; 时停止的问题
    /// 
    /// 优势：
    /// 1. 避免复杂的pattl容器提取逻辑
    /// 2. 支持多个pattl容器（虽然通常只有一个）
    /// 3. 自动处理 &lt;ignore_js_op&gt; 标签包裹
    /// 4. 直接操作完整的dlBlock，避免截断
    /// </summary>
    private List<ContentElement>? ExtractAllPattlAttachments(string htmlContent)
    {
        var allAttachments = new List<ContentElement>();

        try
        {
            // 直接在htmlContent中查找所有 <dl class="tattl"> 块
            // 这些块可能被 <ignore_js_op> 包裹，也可能不被包裹
            // 模式：<dl class="tattl">...</dl> 或 <dl class="tattl attm">...</dl> 等变体
            var dlPattern = @"<dl[^>]*class=""[^""]*tattl[^""]*""[^>]*>(.*?)</dl>";
            var dlMatches = Regex.Matches(htmlContent, dlPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (dlMatches.Count == 0)
            {
                Debug.WriteLine("⚠️ 未在htmlContent中找到任何 <dl class=\"tattl\"> 块");
                return null;
            }

            Debug.WriteLine($" 在htmlContent中找到 {dlMatches.Count} 个 <dl class=\"tattl\"> 块");

            foreach (Match dlMatch in dlMatches)
            {
                var dlBlockHtml = dlMatch.Value;  // 包含完整的 <dl>...</dl>

                // 调用现有的方法来解析这个块中的所有附件
                var attachmentsInBlock = ExtractPattlAttachmentsFromDlBlock(dlBlockHtml);

                if (attachmentsInBlock != null && attachmentsInBlock.Count > 0)
                {
                    allAttachments.AddRange(attachmentsInBlock);
                    Debug.WriteLine($" 从此 <dl> 块提取 {attachmentsInBlock.Count} 个附件");
                }
            }

            if (allAttachments.Count > 0)
            {
                Debug.WriteLine($" 总共从pattl中提取 {allAttachments.Count} 个附件");
                return allAttachments;
            }

            Debug.WriteLine("⚠️ 未能从任何 <dl> 块中提取附件");
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 提取所有pattl附件时出错: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///  新增：从版主信息下方的 pattl 容器中提取附件
    /// 结构: &lt;div class="pattl"&gt;...&lt;dl class="tattl"&gt;...&lt;a href="attachment"&gt;filename&lt;/a&gt;...&lt;/dl&gt;...&lt;/div&gt;
    /// 说明: 这些附件位于帖子正文下方，是作者上传的文件列表
    /// 目的: 解析版主操作下方可能存在的附件，可能有也可能没有
    ///  修复：移除 &lt;ignore_js_op&gt; 标签包裹，确保正则表达式能正确匹配 &lt;dl&gt; 块
    ///  新增：区分两种结构 - 帖子内附件 vs pattl中的附件，使用各自的解析方法
    /// </summary>
    private List<ContentElement>? ExtractAttachmentsFromPattlDiv(string pattlDivHtml)
    {
        var attachments = new List<ContentElement>();

        try
        {
            //  修复：移除 <ignore_js_op> 标签包裹，这些标签会破坏 <dl> 块的完整性
            // 格式: <ignore_js_op>...<dl>...</dl>...</ignore_js_op>
            pattlDivHtml = Regex.Replace(pattlDivHtml, @"</?ignore_js_op[^>]*>", "", RegexOptions.IgnoreCase);
            Debug.WriteLine($" 移除 ignore_js_op 标签后的内容: {pattlDivHtml.Substring(0, Math.Min(100, pattlDivHtml.Length))}...");

            // 提取所有 <dl class="tattl"> 元素
            // 格式: <dl class="tattl attm">...<a href="...attachment...">....</a>...</dl>
            var dlPattern = @"<dl[^>]*class=""[^""]*tattl[^""]*""[^>]*>(.*?)</dl>";
            var dlMatches = Regex.Matches(pattlDivHtml, dlPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (dlMatches.Count == 0)
            {
                Debug.WriteLine("⚠️  pattl 容器中未找到 tattl 列表");
                return null;
            }

            foreach (Match dlMatch in dlMatches)
            {
                var dlContent = dlMatch.Value;  // 使用完整的 <dl>...</dl> 内容

                // 调用专门的 pattl 附件解析方法
                var pattlAttachments = ExtractPattlAttachmentsFromDlBlock(dlContent);
                if (pattlAttachments != null && pattlAttachments.Count > 0)
                {
                    attachments.AddRange(pattlAttachments);
                }
            }

            // 只有当实际解析到附件时才返回列表，否则返回 null（表示没有附件）
            if (attachments.Count > 0)
            {
                Debug.WriteLine($" 从 pattl 容器成功解析 {attachments.Count} 个附件");
                return attachments;
            }

            Debug.WriteLine("⚠️  pattl 容器中没有解析到任何附件");
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 解析 pattl 容器错误: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///  新增：专门的 pattl 容器附件解析方法
    /// 结构对比（与帖子内附件不同）：
    /// - pattl 附件：&lt;dl class="tattl"&gt;...&lt;a id="aid*" class="xw1"&gt;文件名&lt;/a&gt;&lt;em&gt;(大小, 下载次数)&lt;/em&gt;...&lt;img ... /&gt;...&lt;/dl&gt;
    /// - 帖子内附件：&lt;img&gt; + &lt;div class="tip tip_4 aimg_tip"&gt;...&lt;strong&gt;文件名&lt;/strong&gt;...&lt;/div&gt;
    /// 
    /// pattl 附件特点：
    /// 1. 文件名直接在 &lt;a&gt; 标签的文本中（a 标签有 id="aid*"）
    /// 2. 大小和下载次数在紧跟其后的 &lt;em class="xg1"&gt; 中
    /// 3. 上传时间在 &lt;span class="y"&gt; 中
    /// 4. 通常没有售价信息（这些是已上传的用户文件，不是需要购买的）
    /// </summary>
    private List<ContentElement>? ExtractPattlAttachmentsFromDlBlock(string dlBlockHtml)
    {
        var attachments = new List<ContentElement>();

        try
        {
            //  新增：提取所有 <dd>...</dd> 块（每个块代表一个附件）
            var ddPattern = @"<dd[^>]*>(.*?)</dd>";
            var ddMatches = Regex.Matches(dlBlockHtml, ddPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (ddMatches.Count == 0)
            {
                Debug.WriteLine("⚠️ pattl 中 <dd> 块为空，未找到附件");
                return null;
            }

            foreach (Match ddMatch in ddMatches)
            {
                var ddContent = ddMatch.Value;

                try
                {
                    //  步骤 1：提取第一个 <a> 标签（文件名和链接）
                    // 改进：更灵活的模式，支持任何类型的 href（mod=attachment 和 attachpay 都可以）
                    // 关键：不再依赖 id="aid*" 属性或特定的 URL 类型，而是从 href 中提取 aid 参数
                    var linkPattern = @"<a[^>]*href=""([^""]*)""[^>]*>([^<]+)</a>";
                    var linkMatch = Regex.Match(ddContent, linkPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

                    if (!linkMatch.Success)
                    {
                        continue;
                    }

                    var downloadUrl = HtmlDecode(linkMatch.Groups[1].Value.Trim());
                    var fileName = HtmlDecode(linkMatch.Groups[2].Value.Trim());

                    if (string.IsNullOrEmpty(downloadUrl) || string.IsNullOrEmpty(fileName))
                    {
                        continue;
                    }

                    //  步骤 2：从 URL 中提取 aid 参数
                    // 支持两种 URL 类型：
                    // - 免费：https://...?mod=attachment&aid=4617522...
                    // - 付费：https://...?mod=misc&action=attachpay&aid=4617688...
                    var aidMatch = Regex.Match(downloadUrl, @"aid=([^&]+)", RegexOptions.IgnoreCase);

                    if (!aidMatch.Success)
                    {
                        Debug.WriteLine($"⚠️ 无法从 URL 中提取 aid 参数: {downloadUrl.Substring(0, Math.Min(60, downloadUrl.Length))}...");
                        continue;
                    }

                    var attachmentId = aidMatch.Groups[1].Value;

                    Debug.WriteLine($" 提取 pattl 附件链接: {fileName}, URL={downloadUrl.Substring(0, Math.Min(80, downloadUrl.Length))}..., aid={attachmentId}");

                    var attachment = CreatePattlAttachmentElement(downloadUrl, attachmentId, fileName, ddContent);
                    if (attachment != null)
                    {
                        attachments.Add(attachment);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"❌ 解析 pattl 中的单个 <dd> 块错误: {ex.Message}");
                    continue;
                }
            }

            if (attachments.Count > 0)
            {
                Debug.WriteLine($" 从 pattl <dl> 块解析到 {attachments.Count} 个附件");
                return attachments;
            }

            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 解析 pattl <dl> 块错误: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///  新增：创建 pattl 附件元素的辅助方法
    /// 从 &lt;dd&gt; 块中提取文件大小、下载次数、上传时间和售价信息
    ///  改进：支持付费附件解析，使用URL类型判断是否需要购买
    /// 
    /// 逻辑说明（与帖子内部逻辑一致）：
    /// - URL包含 mod=attachment → 可直接下载（免费或已购买）→ SalePrice = null
    /// - URL包含 mod=misc&action=attachpay → 需要购买 → SalePrice > 0（从HTML解析）
    /// </summary>
    private ContentElement? CreatePattlAttachmentElement(string downloadUrl, string attachmentId, string fileName, string ddContent)
    {
        try
        {
            //  步骤 1：判断链接类型决定是否已购买
            // 根据URL类型判断：这是帖子内部逻辑的直接应用
            bool isPaidAttachment = ddContent.Contains("购买");
            bool canDirectDownload = downloadUrl.Contains("mod=attachment");

            //付费附件需重新匹配URL
            if(isPaidAttachment)
            {
                var linkMatch = Regex.Match(ddContent, @"<a[^>]*href=""([^""]*)""[^>]*>购买</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (linkMatch.Success)
                {
                    downloadUrl = HtmlDecode(linkMatch.Groups[1].Value.Trim());

                    var aidMatch = Regex.Match(downloadUrl, @"aid=([^&]+)", RegexOptions.IgnoreCase);
                    if (aidMatch.Success)
                    {
                        attachmentId = aidMatch.Groups[1].Value;
                    }
                }
            }

            Debug.WriteLine($" pattl 附件链接分析: {fileName}, isPaid={isPaidAttachment}, canDownload={canDirectDownload}, URL={downloadUrl.Substring(0, Math.Min(60, downloadUrl.Length))}...");

            //  步骤 2：提取文件大小和下载次数
            // 真实结构：<p>1.12 KB, 下载次数: 1, 下载积分: PB币 -1 </p>
            // 注意：必须使用 <p[^>]*> 来正确匹配所有 <p> 标签及其属性
            // 并使用 [^<]+ 来精确匹配到第一个逗号前的内容，避免跨越多个 <p> 标签
            var sizeMatch = Regex.Match(ddContent, @"<p[^>]*>\s*([^<,]+?)\s*,\s*下载次数:\s*(\d+)", RegexOptions.IgnoreCase);

            var fileSize = "";
            var downloadCount = "";

            if (sizeMatch.Success)
            {
                fileSize = sizeMatch.Groups[1].Value.Trim();  // e.g., "1.12 KB"
                downloadCount = sizeMatch.Groups[2].Value.Trim();  // e.g., "1"
                Debug.WriteLine($" pattl 附件: 文件={fileName}, 大小={fileSize}, 下载={downloadCount}次");
            }
            else
            {
                Debug.WriteLine($"⚠️ 未能从 <p> 标签中提取文件大小，尝试从其他位置...");
                // 备选方案：尝试从 <em class="xg1"> 提取（以防某些页面有这种格式）
                var fallbackMatch = Regex.Match(ddContent, @"<em[^>]*class=""[^""]*xg1[^""]*""[^>]*>\s*\(([^,]+),\s*下载次数:\s*(\d+)\)", RegexOptions.IgnoreCase);
                if (fallbackMatch.Success)
                {
                    fileSize = fallbackMatch.Groups[1].Value.Trim();
                    downloadCount = fallbackMatch.Groups[2].Value.Trim();
                    Debug.WriteLine($" 从备选位置提取: 大小={fileSize}, 下载={downloadCount}次");
                }
            }

            // 构建文件大小显示
            string fileSizeDisplay = fileSize;
            if (!string.IsNullOrEmpty(downloadCount))
            {
                fileSizeDisplay = $"{fileSize} ({downloadCount}次)";
            }

            //  步骤 3：提取上传时间
            // 模式：<p class="y">2026-3-27 15:59 上传</p>
            var uploadTimeMatch = Regex.Match(ddContent, @"<p class=""y"">(.*?) 上传</p>", RegexOptions.IgnoreCase);

            var uploadTime = "";
            if (uploadTimeMatch.Success)
            {
                uploadTime = uploadTimeMatch.Groups[1].Value.Trim();
                // 移除末尾的 "上传" 字符，只保留时间戳
                uploadTime = Regex.Replace(uploadTime, @"\s*上传\s*$", "", RegexOptions.IgnoreCase).Trim();
                Debug.WriteLine($" 提取上传时间: {uploadTime}");
            }
            else
            {
                Debug.WriteLine($"⚠️ 未能从 <p> 标签中提取上传时间，尝试从其他位置...");
                // 备选方案：尝试从 <span class="y"> 提取（以防某些页面有这种格式）
                var fallbackMatch = Regex.Match(ddContent, @"<span class=""y"">(.*?) 上传</span>", RegexOptions.IgnoreCase);
                if (fallbackMatch.Success)
                {
                    uploadTime = fallbackMatch.Groups[1].Value.Trim();
                    // 移除末尾的 "上传" 字符，只保留时间戳
                    uploadTime = Regex.Replace(uploadTime, @"\s*上传\s*$", "", RegexOptions.IgnoreCase).Trim();
                    Debug.WriteLine($" 从备选位置提取上传时间: {uploadTime}");
                }
            }

            //  步骤 4：根据链接类型判断是否需要购买
            // 核心逻辑：与帖子内部逻辑一致
            int? salePrice = null;

            if (isPaidAttachment)
            {
                // 这是购买链接，需要从HTML中提取售价
                // 模式：售价: <strong>1 PB币</strong> 或 售价: 1 PB币
                var priceMatch = Regex.Match(ddContent, @"售价:\s*(?:<strong>)?(\d+)(?:</strong>)?\s*PB币", RegexOptions.IgnoreCase);
                if (priceMatch.Success && int.TryParse(priceMatch.Groups[1].Value, out int price))
                {
                    salePrice = price;
                    Debug.WriteLine($" pattl 付费附件（需要购买）: 文件={fileName}, 售价={price} PB币");
                }
                else
                {
                    // 如果找不到价格但是有购买链接，默认设为1（防止漏掉付费附件）
                    salePrice = 1;
                    Debug.WriteLine($"⚠️ 付费附件但未找到价格，默认=1 PB币: {fileName}");
                }
            }
            else if (canDirectDownload)
            {
                // 这是直接下载链接，说明可以下载（无论免费还是已购买）
                salePrice = null;
                Debug.WriteLine($" pattl 可下载附件（免费或已购买）: {fileName}");
            }

            return new ContentElement
            {
                Type = ContentElementType.Attachment,
                Url = downloadUrl,
                FileName = fileName,
                FileSize = fileSizeDisplay,
                UploadTime = uploadTime,
                AttachmentId = attachmentId,
                SalePrice = salePrice  //  null = 可下载，> 0 = 需要购买
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 创建 pattl 附件元素错误: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 解析图片标签
    /// </summary>
    private ContentElement? ParseImageTag(string imgTag)
    {
        try
        {
            var srcMatch = Regex.Match(imgTag, @"file=""([^""]+)""", RegexOptions.IgnoreCase);
            var widthMatch = Regex.Match(imgTag, @"width[^=]*=""?([^""\s]+)", RegexOptions.IgnoreCase);
            var heightMatch = Regex.Match(imgTag, @"height[^=]*=""?([^""\s]+)", RegexOptions.IgnoreCase);
            var altMatch = Regex.Match(imgTag, @"alt=""([^""]*)""", RegexOptions.IgnoreCase);

            string imageUrl = "";
            if (srcMatch.Success)
            {
                 imageUrl = srcMatch.Groups[1].Value.Trim();

                // 如果是相对路径，补充域名
                if (imageUrl.StartsWith("/"))
                {
                    imageUrl = "https://bbs.pcbeta.com" + imageUrl;
                }
                else if (!imageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    imageUrl = "https://bbs.pcbeta.com/" + imageUrl;
                }
            }
            else //非附件图片，例如网络图片
            {
                var srcMatch2 = Regex.Match(imgTag, @"src=""([^""]+)""", RegexOptions.IgnoreCase);
                if (srcMatch2.Success)
                {
                    imageUrl = srcMatch2.Groups[1].Value.Trim();
                    // 如果 src 中的 URL 也是相对路径，补充域名
                    if (!imageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(imageUrl))
                    {
                        if (imageUrl.StartsWith("/"))
                        {
                            imageUrl = "https://bbs.pcbeta.com" + imageUrl;
                        }
                        else if (!imageUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                        {
                            imageUrl = "https://bbs.pcbeta.com/" + imageUrl;
                        }
                    }
                }
            }

            // 如果仍然没有获取到有效的 URL，返回 null
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                Debug.WriteLine("⚠️ 解析图片标签：未找到有效的 URL");
                return null;
            }

            int.TryParse(widthMatch.Groups[1].Value, out int width);
            int.TryParse(heightMatch.Groups[1].Value, out int height);

            return new ContentElement
            {
                Type = ContentElementType.Image,
                Url = imageUrl,
                Title = altMatch.Success ? altMatch.Groups[1].Value : "",
                ImageWidth = width,
                ImageHeight = height
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 解析图片标签错误: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    ///  新增：从隐藏的附件菜单中提取上传日期
    /// 查找格式：&lt;div class="y"&gt;2025-12-17 23:18 上传&lt;/div&gt;
    /// </summary>
    private string? ExtractUploadTimeFromHiddenMenu(string htmlContent, string attachmentId)
    {
        try
        {
            // 构建隐藏菜单的ID模式：attach_4238522_menu
            var menuPattern = $@"<div[^>]*id=""attach_{Regex.Escape(attachmentId)}_menu""[^>]*>.*?<div[^>]*class=""[^""]*\by\b[^""]*""[^>]*>([^<]*上传[^<]*)</div>";
            var match = Regex.Match(htmlContent, menuPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (match.Success)
            {
                var uploadTime = match.Groups[1].Value.Trim();
                // 移除末尾的 "上传" 字符，只保留时间戳
                uploadTime = Regex.Replace(uploadTime, @"\s*上传\s*$", "", RegexOptions.IgnoreCase).Trim();
                Debug.WriteLine($" 从隐藏菜单提取上传时间: {uploadTime}");
                return uploadTime;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"⚠️ 从隐藏菜单提取上传时间错误: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    ///  新增：解析附件容器 &lt;span id="attach_*"&gt;
    /// 支持两种结构：
    /// 1. 已购买附件（mod=attachment）：
    ///    &lt;span id="attach_4616601"&gt;
    ///      &lt;a href="...mod=attachment&amp;aid=4616601..."&gt;文件名&lt;/a&gt;
    ///      &lt;em&gt;(大小, 下载次数: X)&lt;/em&gt;
    ///    &lt;/span&gt;
    /// 2. 未购买附件（attachpay）：
    ///    &lt;span id="attach_4616856"&gt;
    ///      &lt;a href="...attachpay&amp;aid=4616856..."&gt;文件名&lt;/a&gt;
    ///      &lt;em&gt;(...下载次数: X, 售价: 1 PB币)&lt;/em&gt;
    ///    &lt;/span&gt;
    /// </summary>
    private ContentElement? ParseAttachmentSpan(string spanTag, string? horizontalGroupId, string? fullHtmlContent = null)
    {
        try
        {
            // 支持两种链接格式：
            // 1. 已购买：href="...mod=attachment..."
            // 2. 未购买：href="...attachpay..."
            var linkMatch = Regex.Match(spanTag, @"<a[^>]*href=""([^""]*(?:mod=attachment|attachpay)[^""]*)""[^>]*>([^<]+)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (!linkMatch.Success)
                return null;

            var downloadUrl = HtmlDecode(linkMatch.Groups[1].Value.Trim());
            var fileName = HtmlDecode(linkMatch.Groups[2].Value.Trim());

            if (string.IsNullOrEmpty(downloadUrl) || string.IsNullOrEmpty(fileName))
            {
                Debug.WriteLine("⚠️ 附件容器解析：缺少下载URL或文件名");
                return null;
            }

            //  新增：区分两种附件类型
            bool isPaidAttachment = downloadUrl.Contains("attachpay");

            // 提取文件大小和下载次数
            // 已购买：(大小, 下载次数: X)
            // 未购买：(...下载次数: X, 售价: Y PB币)
            var sizeMatch = Regex.Match(spanTag, @"\(([^,]+),\s*下载次数:\s*(\d+)", RegexOptions.IgnoreCase);
            var fileSize = "";
            var downloadCount = "";

            if (sizeMatch.Success)
            {
                fileSize = sizeMatch.Groups[1].Value.Trim();  // e.g., "459.47 KB"
                downloadCount = sizeMatch.Groups[2].Value.Trim();  // e.g., "19009"
                Debug.WriteLine($" 附件容器: 文件={fileName}, 大小={fileSize}, 下载={downloadCount}次");
            }

            // 构建文件大小显示：大小 (下载次数)
            string fileSizeDisplay = fileSize;
            if (!string.IsNullOrEmpty(downloadCount))
            {
                fileSizeDisplay = $"{fileSize} ({downloadCount}次)";
            }

            //  新增：从span中提取附件ID，并从隐藏菜单中提取上传时间
            string? uploadTime = null;
            string? attachmentId = null;
            var idMatch = Regex.Match(spanTag, @"id=""attach_(\d+)""", RegexOptions.IgnoreCase);
            if (idMatch.Success)
            {
                attachmentId = idMatch.Groups[1].Value;
                if (!string.IsNullOrEmpty(fullHtmlContent))
                {
                    uploadTime = ExtractUploadTimeFromHiddenMenu(fullHtmlContent, attachmentId);
                }
            }

            //  新增：提取销售价格（仅未购买附件）
            int? salePrice = null;
            if (isPaidAttachment)
            {
                // 格式：售价: 1 PB币 或 售价:1PB币
                var priceMatch = Regex.Match(spanTag, @"售价:\s*(\d+)\s*PB币", RegexOptions.IgnoreCase);
                if (priceMatch.Success)
                {
                    if (int.TryParse(priceMatch.Groups[1].Value, out int price))
                    {
                        salePrice = price;
                        Debug.WriteLine($" 未购买附件: {fileName}, 售价={price} PB币");
                    }
                }
            }

            return new ContentElement
            {
                Type = ContentElementType.Attachment,
                Url = downloadUrl,
                FileName = fileName,
                FileSize = fileSizeDisplay,
                UploadTime = uploadTime,
                AttachmentId = attachmentId,
                SalePrice = salePrice,  //  null = 已购买，> 0 = 未购买
                HorizontalGroupId = horizontalGroupId  //  支持水平分组
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 附件容器解析错误: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// 解析链接标签和附件
    /// 修复：正确处理 <a>...text...</a> 标签，去掉末尾的 </a>
    /// 增强：支持更多格式的 href 属性，包括空格、单引号等
    /// 增强：如果是附件且没有有效的文件名，使用 suggestedFileName
    /// 修复：正确识别 attachment 链接（包含 mod=attachment&aid 参数）
    /// </summary>
    private ContentElement? ParseLinkTag(string linkTag, string nextToken, string? suggestedFileName = null)
    {
        try
        {
            // 支持多种 href 格式：href="...", href='...', href= "...", 等
            var hrefMatch = Regex.Match(linkTag, @"href\s*=\s*[""']([^""']*)[""']", RegexOptions.IgnoreCase);

            // 如果上面的正则没匹配，尝试不带引号的格式
            if (!hrefMatch.Success)
            {
                hrefMatch = Regex.Match(linkTag, @"href\s*=\s*([^\s>]+)", RegexOptions.IgnoreCase);
            }

            // 提取 > 到 </a> 之间的完整文本内容，更宽松的模式
            var titleMatch = Regex.Match(linkTag, @">([^<]*?)</a>", RegexOptions.IgnoreCase);

            if (hrefMatch.Success)
            {
                var url = HtmlDecode(hrefMatch.Groups[1].Value.Trim());
                var title = titleMatch.Success ? HtmlDecode(titleMatch.Groups[1].Value.Trim()) : "";

                // 如果 URL 为空，使用标题作为后备
                if (string.IsNullOrEmpty(url))
                    url = title;

                // 如果 URL 仍为空，说明解析失败
                if (string.IsNullOrEmpty(url))
                {
                    Debug.WriteLine("⚠️ 链接 URL 为空，跳过此链接");
                    return null;
                }

                //  改进：判断是否为附件
                // 附件URL特征：包含 "mod=attachment" 和 "aid=" 参数，或包含 "download" 关键字
                bool isAttachment = url.Contains("mod=attachment") && url.Contains("aid=") || 
                                   url.Contains("download") || 
                                   linkTag.Contains("class=\"attlink\"") ||
                                   linkTag.Contains("id=\"attach_");  //  新增：检查是否在 attach_ 容器中

                if (isAttachment)
                {
                    // 如果链接文本为"下载附件"或为空，则使用建议的文件名
                    string fileName = title;
                    if (string.IsNullOrEmpty(fileName) || fileName == "下载附件")
                    {
                        fileName = suggestedFileName ?? title ?? "附件";
                    }

                    Debug.WriteLine($" 识别附件: {fileName} (URL: {url.Substring(0, Math.Min(60, url.Length))}...)");

                    return new ContentElement
                    {
                        Type = ContentElementType.Attachment,
                        Url = url,
                        FileName = fileName,
                        FileSize = ""
                    };
                }
                else
                {
                    // 普通链接
                    return new ContentElement
                    {
                        Type = ContentElementType.Link,
                        Url = url,
                        Title = title
                    };
                }
            }
            else
            {
                Debug.WriteLine($"⚠️ 无法从链接标签中提取 href 属性: {linkTag.Substring(0, Math.Min(50, linkTag.Length))}...");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 解析链接标签错误: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// 从 ignore_js_op 块中提取文件名
    /// 查找 <strong>文件名</strong> 的模式，返回找到的第一个文件名
    /// 例如：<p><strong>无标题.png</strong> <em class="xg1">(86.28 KB, 下载次数: 5)</em></p>
    /// </summary>
    private string? ExtractFileNameFromIgnoreBlock(string htmlContent)
    {
        try
        {
            // 匹配 <strong>...</strong> 中的内容，作为可能的文件名
            var strongPattern = @"<strong[^>]*>([^<]+)</strong>";
            var match = Regex.Match(htmlContent, strongPattern, RegexOptions.IgnoreCase);

            if (match.Success)
            {
                var content = HtmlDecode(match.Groups[1].Value.Trim());

                // 检查是否看起来像文件名（包含扩展名）
                if (!string.IsNullOrEmpty(content) && content.Contains("."))
                {
                    if (content.Length < 255)
                    {
                        Debug.WriteLine($" 从 ignore_js_op 块中提取文件名: {content}");
                        return content;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 提取文件名错误: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// 解析表情标签
    /// </summary>
    private ContentElement? ParseEmojiTag(string emojiTag)
    {
        try
        {
            var smilieIdMatch = Regex.Match(emojiTag, @"smilieid=""([^""]*)""", RegexOptions.IgnoreCase);
            var srcMatch = Regex.Match(emojiTag, @"src=""([^""]*)""", RegexOptions.IgnoreCase);

            if (smilieIdMatch.Success || srcMatch.Success)
            {
                return new ContentElement
                {
                    Type = ContentElementType.Emoji,
                    EmojiId = smilieIdMatch.Success ? smilieIdMatch.Groups[1].Value : "",
                    Url = srcMatch.Success ? srcMatch.Groups[1].Value : ""
                };
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 解析表情标签错误: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// 解析表格标签，支持表格内的复杂元素（链接、加粗、表情、附件等）
    /// 正确的结构：Table → TableRow → TableCell → Elements
    /// 重要：检测并存储表格列数（从第一行的单元格数获取）
    /// </summary>
    private ContentElement? ParseTableTag(string tableHtml)
    {
        try
        {
            var tableElement = new ContentElement
            {
                Type = ContentElementType.Table,
                Children = new List<ContentElement>(),
                ColumnCount = 0  // 初始化，将在第一行被设置
            };

            // 提取所有行
            var rowPattern = @"<tr[^>]*>(.*?)</tr>";
            var rowMatches = Regex.Matches(tableHtml, rowPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

            bool isFirstRow = true;
            int rowIndex = 0;

            foreach (Match rowMatch in rowMatches)
            {
                var rowHtml = rowMatch.Groups[1].Value;

                // 跳过空行
                if (!rowHtml.Contains("<td") && !rowHtml.Contains("<th"))
                    continue;

                // 提取行中的单元格
                var cellPattern = @"<(?:td|th)[^>]*>(.*?)</(?:td|th)>";
                var cellMatches = Regex.Matches(rowHtml, cellPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

                // 从第一行检测列数
                if (isFirstRow)
                {
                    tableElement.ColumnCount = cellMatches.Count;
                    Debug.WriteLine($" 检测到表格列数: {tableElement.ColumnCount}");
                    isFirstRow = false;
                }

                //  为每一行创建一个 TableRow 容器
                var tableRowElement = new ContentElement
                {
                    Type = ContentElementType.TableRow,
                    Children = new List<ContentElement>()
                };

                // 处理该行的每个单元格
                int cellIndexInRow = 0;
                foreach (Match cellMatch in cellMatches)
                {
                    var rawCellContent = cellMatch.Groups[1].Value.Trim();

                    // 递归解析单元格内容，支持链接、加粗、表情、附件等所有元素类型
                    var cellElements = ParseContentBlock(rawCellContent, horizontalGroupId: null, suggestedFileName: null, isTableCell: true);

                    //  关键修复：为单元格内的元素分组，默认水平排列
                    // 只有遇到 LineBreak 才换行
                    if (cellElements.Count > 0)
                    {
                        var groupedElements = new List<ContentElement>();
                        string currentGroupId = $"table_cell_r{rowIndex}_c{cellIndexInRow}_group1";
                        int groupCounter = 1;

                        foreach (var elem in cellElements)
                        {
                            if (elem.Type == ContentElementType.LineBreak)
                            {
                                // LineBreak：添加换行标记，重置分组
                                groupedElements.Add(elem);
                                groupCounter++;
                                currentGroupId = $"table_cell_r{rowIndex}_c{cellIndexInRow}_group{groupCounter}";
                            }
                            else
                            {
                                // 其他元素：分配水平分组 ID（使其在同一行显示）
                                elem.HorizontalGroupId = currentGroupId;
                                groupedElements.Add(elem);
                            }
                        }

                        var tableCellContainer = new ContentElement
                        {
                            Type = ContentElementType.TableCell,
                            Children = groupedElements  // 分组后的元素
                        };
                        tableRowElement.Children?.Add(tableCellContainer);
                    }
                    else
                    {
                        // 如果没有解析出任何元素（可能是空单元格或只有纯文本），
                        // 提取纯文本作为后备方案
                        var plainText = HtmlDecode(StripHtmlTags(rawCellContent)).Trim();
                        if (!string.IsNullOrEmpty(plainText))
                        {
                            var tableCellContainer = new ContentElement
                            {
                                Type = ContentElementType.TableCell,
                                Children = new List<ContentElement>
                                {
                                    new ContentElement
                                    {
                                        Type = ContentElementType.Text,
                                        Text = plainText,
                                        HorizontalGroupId = $"table_cell_r{rowIndex}_c{cellIndexInRow}_group1"
                                    }
                                }
                            };
                            tableRowElement.Children?.Add(tableCellContainer);
                        }
                        else
                        {
                            // 空单元格：创建一个空的 TableCell
                            var emptyCell = new ContentElement
                            {
                                Type = ContentElementType.TableCell,
                                Children = new List<ContentElement>()
                            };
                            tableRowElement.Children?.Add(emptyCell);
                        }
                    }

                    cellIndexInRow++;
                }

                // 将该行添加到表格
                tableElement.Children?.Add(tableRowElement);
                rowIndex++;
            }

            if (tableElement.Children?.Count > 0)
            {
                Debug.WriteLine($" 表格解析完成: {tableElement.ColumnCount} 列, {tableElement.Children.Count} 行");
                return tableElement;
            }

            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 解析表格标签错误: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// 从富文本元素列表生成纯文本
    /// </summary>
    private string GeneratePlainText(List<ContentElement> elements)
    {
        var textParts = new List<string>();

        foreach (var element in elements)
        {
            switch (element.Type)
            {
                case ContentElementType.Text:
                case ContentElementType.Bold:
                case ContentElementType.Italic:
                case ContentElementType.Code:
                case ContentElementType.Quote:
                    if (!string.IsNullOrEmpty(element.Text))
                        textParts.Add(element.Text);
                    break;

                case ContentElementType.Image:
                    if (!string.IsNullOrEmpty(element.Title))
                        textParts.Add($"[图片: {element.Title}]");
                    else if (!string.IsNullOrEmpty(element.Url))
                        textParts.Add($"[图片]");
                    break;

                case ContentElementType.Attachment:
                    var attachmentInfo = $"[附件: {element.FileName}";
                    if (!string.IsNullOrEmpty(element.FileSize))
                        attachmentInfo += $" ({element.FileSize})";
                    if (!string.IsNullOrEmpty(element.UploadTime))
                        attachmentInfo += $" - {element.UploadTime}";
                    attachmentInfo += "]";
                    textParts.Add(attachmentInfo);
                    break;

                case ContentElementType.Link:
                    textParts.Add($"[链接: {element.Title}]");
                    break;

                case ContentElementType.Emoji:
                    textParts.Add("[表情]");
                    break;

                case ContentElementType.LineBreak:
                    textParts.Add("\n");
                    break;

                case ContentElementType.Separator:
                    textParts.Add("\n---\n");
                    break;

                case ContentElementType.Table:
                    textParts.Add("[表格]");
                    break;
            }
        }

        return string.Join("", textParts).Trim();
    }

    /// <summary>
    /// 删除 HTML 标签，仅保留文本内容
    /// </summary>
    private string StripHtmlTags(string html)
    {
        try
        {
            var text = Regex.Replace(html, "<[^>]+>", "");
            text = Regex.Replace(text, @"\s+", " ");
            return text.Trim();
        }
        catch
        {
            return html;
        }
    }

    /// <summary>
    /// 清理数字字符串 - 提取纯数字部分
    /// 处理 "5万", "123K", "1,234" 等格式
    /// </summary>
    private string CleanNumericString(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "0";

        try
        {
            // 提取前导数字
            var digitMatch = Regex.Match(value, @"^(\d+(?:,\d+)*)");
            if (digitMatch.Success)
            {
                var numStr = digitMatch.Groups[1].Value.Replace(",", "");
                if (int.TryParse(numStr, out _))
                {
                    return numStr;
                }
            }

            // 如果提取失败，返回原值或 0
            return Regex.IsMatch(value, @"\d") ? Regex.Match(value, @"\d+").Value : "0";
        }
        catch
        {
            return "0";
        }
    }

    /// <summary>
    /// 从单列布局 HTML 提取最后回复信息
    /// </summary>
    private string ExtractLastReplySingleColumn(string rowHtml)
    {
        try
        {
            // 查找 <td class="fl_by"> 容器
            var flByPattern = @"<td[^>]*class=""[^""]*fl_by[^""]*""[^>]*>.*?<div>\s*<a[^>]*href=""[^""]*""[^>]*>([^<]+)</a>\s*<cite>([^<]+)<a";
            var match = Regex.Match(rowHtml, flByPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (match.Success)
            {
                var threadTitle = match.Groups[1].Value.Trim();
                var dateTimeAuthor = match.Groups[2].Value.Trim();
                return $"{threadTitle} {dateTimeAuthor}";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 提取最后回复 (单列) 错误: {ex.Message}");
        }

        return string.Empty;
    }

    /// <summary>
    /// 从 fg_508 (GNU/Linux) 分类提取最后回复信息
    /// fg_508 的 <dd> 结构与其他分类不同
    /// </summary>
    private string ExtractLastReplyFg508(string dlHtml)
    {
        try
        {
            // 匹配所有 <dd> 标签
            var ddPattern = @"<dd>.*?</dd>";
            var matches = Regex.Matches(dlHtml, ddPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

            // 获取第二个 <dd> (index 1)，包含最后回复信息
            if (matches.Count >= 2)
            {
                var secondDd = matches[1].Value;

                // 提取所有文本内容并删除 HTML 标签
                var textPattern = @"<dd>(.*?)</dd>";
                var textMatch = Regex.Match(secondDd, textPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

                if (textMatch.Success)
                {
                    // 删除所有 HTML 标签
                    var text = Regex.Replace(textMatch.Groups[1].Value, "<[^>]+>", "").Trim();
                    // 清理多余空格
                    text = Regex.Replace(text, @"\s+", " ");
                    return text;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 提取最后回复 (fg508) 错误: {ex.Message}");
        }

        return string.Empty;
    }

    /// <summary>
    /// 从多列布局 HTML 提取最后回复信息
    /// 从第 3 个 <dd> 标签中获取内容
    /// </summary>
    private string ExtractLastReplyMultiColumn(string dlHtml)
    {
        try
        {
            // 匹配所有 <dd> 标签
            var ddPattern = @"<dd>.*?</dd>";
            var matches = Regex.Matches(dlHtml, ddPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

            // 匹配第二个 <dd>
            if (matches.Count >= 2)
            {
                var thirdDd = matches[1].Value;
                // 从 <a> 标签中提取链接文本
                var linkPattern = @"<a[^>]*href=""[^""]*""[^>]*>([^<]+)</a>";
                var linkMatch = Regex.Match(thirdDd, linkPattern, RegexOptions.IgnoreCase);

                if (linkMatch.Success)
                {
                    return linkMatch.Groups[1].Value.Trim();
                }

                // 备选方案：从 <dd> 标签中提取任何文本
                var textPattern = @"<dd>(.*?)</dd>";
                var textMatch = Regex.Match(thirdDd, textPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (textMatch.Success)
                {
                    var text = Regex.Replace(textMatch.Groups[1].Value, "<[^>]+>", "").Trim();
                    return text;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 提取最后回复 (多列) 错误: {ex.Message}");
        }

        return string.Empty;
    }

    /// <summary>
    /// 将 HTML 内容分割为 tokens，每个 token 要么是完整的标签块（包括开始和结束标签），要么是纯文本内容
    /// 策略：识别开始标签，然后找到匹配的结束标签，作为一个整体返回
    /// 例如：<table>...</table> 作为一个单独的 token，而不是分割成多个
    /// 改进：更好地处理开始标签，即使找不到结束标签也会尝试找到匹配的结束标签或至少一个结束标签
    /// </summary>
    private List<string> SplitContentIntoTokens(string htmlContent)
    {
        var tokens = new List<string>();

        try
        {
            int i = 0;

            while (i < htmlContent.Length)
            {
                // 查找下一个 <
                int openBracket = htmlContent.IndexOf('<', i);

                if (openBracket == -1)
                {
                    // 没有更多的标签，添加剩余的文本
                    string remainingText = htmlContent.Substring(i).Trim();
                    if (!string.IsNullOrWhiteSpace(remainingText))
                    {
                        tokens.Add(remainingText);
                    }
                    break;
                }

                // 添加 < 之前的文本
                if (openBracket > i)
                {
                    string textBefore = htmlContent.Substring(i, openBracket - i).Trim();
                    if (!string.IsNullOrWhiteSpace(textBefore))
                    {
                        tokens.Add(textBefore);
                    }
                }

                // 查找对应的 >
                int closeBracket = htmlContent.IndexOf('>', openBracket);
                if (closeBracket == -1)
                {
                    // 格式错误，添加剩余内容
                    tokens.Add(htmlContent.Substring(openBracket));
                    break;
                }

                // 提取标签
                string tagStr = htmlContent.Substring(openBracket, closeBracket - openBracket + 1);

                // 判断标签类型
                if (tagStr.StartsWith("</"))
                {
                    // 这是一个未配对的结束标签，跳过
                    i = closeBracket + 1;
                    continue;
                }

                if (tagStr.EndsWith("/>"))
                {
                    // 自闭合标签（<br/>, <img/>, 等）
                    tokens.Add(tagStr);
                    i = closeBracket + 1;
                    continue;
                }

                // 这是一个开始标签，需要找到对应的结束标签
                var tagNameMatch = Regex.Match(tagStr, @"<(\w+)");
                if (!tagNameMatch.Success)
                {
                    tokens.Add(tagStr);
                    i = closeBracket + 1;
                    continue;
                }

                string tagName = tagNameMatch.Groups[1].Value.ToLower();

                // 对某些标签（如 a, span, strong, div 等），必须找到结束标签
                // 这些是通常包含内容的标签
                if (tagName == "a" || tagName == "span" || tagName == "strong" || tagName == "b" || 
                    tagName == "em" || tagName == "i" || tagName == "div" || tagName == "p" || 
                    tagName == "blockquote" || tagName == "pre" || tagName == "table" || 
                    tagName == "tbody" || tagName == "tr" || tagName == "td" || tagName == "th")
                {
                    // 查找匹配的结束标签，处理嵌套情况
                    int endTagIndex = FindMatchingClosingTag(htmlContent, closeBracket + 1, tagName);

                    if (endTagIndex != -1)
                    {
                        // 找到了匹配的结束标签
                        int endTagEnd = htmlContent.IndexOf('>', endTagIndex);
                        if (endTagEnd != -1)
                        {
                            // 完整的标签块：从开始 < 到结束 >
                            string completeTag = htmlContent.Substring(openBracket, endTagEnd - openBracket + 1);
                            tokens.Add(completeTag);
                            i = endTagEnd + 1;
                            continue;
                        }
                    }
                    else
                    {
                        // 没有找到结束标签，这可能是格式错误
                        // 但为了安全起见，仍然添加开始标签并继续
                        Debug.WriteLine($"⚠️ 未找到 <{tagName}> 的匹配结束标签");
                        tokens.Add(tagStr);
                        i = closeBracket + 1;
                        continue;
                    }
                }

                // 对于其他标签，只添加开始标签
                tokens.Add(tagStr);
                i = closeBracket + 1;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 分割内容错误: {ex.Message}");
        }

        return tokens;
    }

    /// <summary>
    /// 找到与给定开始标签匹配的结束标签位置
    /// 处理嵌套的同名标签
    /// 例如：<table><table>...</table></table> 返回最后一个 </table> 的位置
    /// 使用正则表达式确保只匹配真正的标签，不会误匹配相邻的独立标签
    /// </summary>
    private int FindMatchingClosingTag(string html, int searchStart, string tagName)
    {
        int nestingLevel = 1;
        int searchPos = searchStart;

        while (nestingLevel > 0 && searchPos < html.Length)
        {
            // 使用正则表达式查找真正的标签开始（标签名后跟空白或 >）
            // 这样可以区分 <table></table><table> 中的第二个 <table> 是新标签而非嵌套
            var openMatch = Regex.Match(
                html.Substring(searchPos),
                $@"<{Regex.Escape(tagName)}[\s>]",
                RegexOptions.IgnoreCase
            );

            int nextOpenTag = openMatch.Success ? searchPos + openMatch.Index : -1;
            int nextCloseTag = html.IndexOf($"</{tagName}", searchPos, StringComparison.OrdinalIgnoreCase);

            // 如果没有找到结束标签，返回 -1
            if (nextCloseTag == -1)
            {
                return -1;
            }

            // 如果有更早的开始标签，说明是嵌套的
            if (nextOpenTag != -1 && nextOpenTag < nextCloseTag)
            {
                // 跳过这个开始标签，增加嵌套层级
                nestingLevel++;
                searchPos = nextOpenTag + tagName.Length + 1;
            }
            else
            {
                // 找到了结束标签
                nestingLevel--;
                if (nestingLevel == 0)
                {
                    // 返回结束标签的起始位置
                    return nextCloseTag;
                }
                // 继续寻找下一个结束标签
                searchPos = nextCloseTag + tagName.Length + 3;  // 3 = "</" + ">"
            }
        }

        return -1;
    }

    /// <summary>
    /// 合并分割后的不完整标签对
    /// 如果遇到开始标签（如 <table>）但没有对应的结束标签（如 </table>），
    /// 则继续收集后续内容直到找到结束标签，然后将它们合并为一个完整的标签块。
    /// 自闭合标签（如 <br/> 或 <img/>）不需要特殊处理。
    /// </summary>
    private string[] MergeIncompleteTagPairs(string[] tokens)
    {
        var mergedTokens = new List<string>();
        var tagStack = new Stack<string>();  // 用于跟踪未关闭的标签
        var currentBuffer = new StringBuilder();  // 用于缓冲不完整的标签块

        for (int i = 0; i < tokens.Length; i++)
        {
            var token = tokens[i];

            if (string.IsNullOrEmpty(token))
                continue;

            // 检查是否是开始标签
            var openTagMatch = Regex.Match(token, @"^<((?:table|tbody|tr|td|th|blockquote|pre|div|span|p|strong|b|em|i|ul|li|ol|dl|dt|dd|h[1-6]))\b", RegexOptions.IgnoreCase);

            // 检查是否是结束标签
            var closeTagMatch = Regex.Match(token, @"^</((?:table|tbody|tr|td|th|blockquote|pre|div|span|p|strong|b|em|i|ul|li|ol|dl|dt|dd|h[1-6]))\b", RegexOptions.IgnoreCase);

            // 检查是否是自闭合标签（<br/>, <hr/>, <img/> 等）
            var selfClosingMatch = Regex.IsMatch(token, @"^<(?:br|hr|img|input|meta|link|source)[^>]*/>", RegexOptions.IgnoreCase);

            // 如果当前有缓冲内容，说明正在收集未关闭的标签块
            if (currentBuffer.Length > 0)
            {
                currentBuffer.Append(token);

                if (closeTagMatch.Success)
                {
                    var closingTagName = closeTagMatch.Groups[1].Value.ToLower();
                    if (tagStack.Count > 0 && tagStack.Peek().Equals(closingTagName, StringComparison.OrdinalIgnoreCase))
                    {
                        tagStack.Pop();

                        // 如果标签栈为空，说明已经找到了完整的配对，可以结束缓冲
                        if (tagStack.Count == 0)
                        {
                            mergedTokens.Add(currentBuffer.ToString());
                            currentBuffer.Clear();
                            continue;
                        }
                    }
                }
                else if (openTagMatch.Success)
                {
                    var openingTagName = openTagMatch.Groups[1].Value.ToLower();
                    tagStack.Push(openingTagName);
                }

                continue;
            }

            // 如果有开始标签但没有对应的结束标签，开始缓冲
            if (openTagMatch.Success && !selfClosingMatch)
            {
                var openingTagName = openTagMatch.Groups[1].Value.ToLower();
                tagStack.Push(openingTagName);
                currentBuffer.Append(token);

                // 检查这个 token 中是否已经有对应的结束标签
                var hasClosingTag = Regex.IsMatch(token, $@"</{openingTagName}(?:\s|>)", RegexOptions.IgnoreCase);
                if (hasClosingTag)
                {
                    tagStack.Pop();
                    if (tagStack.Count == 0)
                    {
                        mergedTokens.Add(currentBuffer.ToString());
                        currentBuffer.Clear();
                    }
                }
                continue;
            }

            // 普通 token，直接添加
            if (currentBuffer.Length == 0)
            {
                mergedTokens.Add(token);
            }
            else
            {
                // 继续收集缓冲内容
                currentBuffer.Append(token);
            }
        }

        // 如果仍有未处理的缓冲内容，将其添加为一个 token
        if (currentBuffer.Length > 0)
        {
            mergedTokens.Add(currentBuffer.ToString());
        }

        return mergedTokens.ToArray();
    }

    /// <summary>
    ///  新增：检查是否应该跳过隐藏元素
    /// 跳过条件：
    /// 1. 元素包含 style="display: none"（通过CSS隐藏）
    /// 2. 元素是提示菜单容器（class 包含 "tip" 或 id 匹配 "attach_*_menu" 模式）
    /// 目的：避免解析隐藏的提示框，如附件上传日期、菜单选项等
    /// </summary>
    private bool ShouldSkipHiddenElement(string htmlToken)
    {
        try
        {
            // 检查 1：style="display: none" 或 style='display: none'
            if (Regex.IsMatch(htmlToken, @"style\s*=\s*[""']?[^""']*display\s*:\s*none[^""']*[""']?", RegexOptions.IgnoreCase))
            {
                Debug.WriteLine($"⏭️  跳过隐藏元素 (display:none): {htmlToken.Substring(0, Math.Min(60, htmlToken.Length))}...");
                return true;
            }

            // 检查 2：提示菜单容器 (class 包含 "tip" 或 "tip_*")
            if (Regex.IsMatch(htmlToken, @"class\s*=\s*[""']?[^""']*\btip(?:_\d+)?\b[^""']*[""']?", RegexOptions.IgnoreCase))
            {
                Debug.WriteLine($"⏭️  跳过提示容器 (tip class): {htmlToken.Substring(0, Math.Min(60, htmlToken.Length))}...");
                return true;
            }

            // 检查 3：附件菜单容器 (id 匹配 "attach_*_menu" 模式)
            if (Regex.IsMatch(htmlToken, @"id\s*=\s*[""']?attach_\d+_menu[""']?", RegexOptions.IgnoreCase))
            {
                Debug.WriteLine($"⏭️  跳过附件菜单容器 (attach_*_menu): {htmlToken.Substring(0, Math.Min(60, htmlToken.Length))}...");
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 检查隐藏元素时出错: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    ///  新增：清理错误信息文本
    /// 特点：
    /// 1. 只移除真实的 HTML 标签，保留 &lt; 和 &gt; 等 HTML 实体转换后的符号
    /// 2. 移除 {} 内的内容（通常是脚本或内部标记）
    /// 3. 规范化空格和换行
    /// 相比 StripHtmlTags，这个方法保留了报错信息中真实的 < 和 > 符号
    /// </summary>
    private string CleanErrorMessageText(string html)
    {
        try
        {
            // 步骤 1: 移除 {} 内的所有内容（通常是脚本或内部标记）
            html = Regex.Replace(html, @"<script.*?</script>", "", RegexOptions.Singleline);

            // 步骤 2: 移除 HTML 标签（<tag>...</tag> 或 <br/> 等）
            // 但保留纯文本中的 < 和 > 符号（它们不在标签中）
            html = Regex.Replace(html, @"</?[a-zA-Z][^>]*>", "");  // 移除格式为 <tag...> 的标签
            html = Regex.Replace(html, @"<br\s*/?>\s*", " ");      // <br> 或 <br/> 替换为空格
            html = Regex.Replace(html, @"</?p>", " ");              // <p> 和 </p> 替换为空格
            html = Regex.Replace(html, @"</?div>", " ");            // <div> 和 </div> 替换为空格

            // 步骤 3: HTML 实体解码（&lt; → <, &gt; → >, &amp; → &, 等）
            html = HtmlDecode(html);

            // 步骤 4: 规范化空格和换行
            html = Regex.Replace(html, @"\r\n", " ");               // 换行转空格
            html = Regex.Replace(html, @"\n", " ");                 // 换行转空格
            html = Regex.Replace(html, @"\s+", " ");                // 多个空格转为一个

            return html.Trim();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 清理错误文本时出错: {ex.Message}");
            return html;
        }
    }

    /// <summary>
    /// 从购买确认页面提取隐藏的表单参数
    /// 提取 formhash、referer 和 aid 等参数
    ///  新增：用于支持完整的购买流程，需要在购买请求中提交这些参数
    /// </summary>
    public AttachmentPurchaseParams ExtractAttachmentPurchaseParams(string htmlContent)
    {
        var result = new AttachmentPurchaseParams();

        try
        {
            // 提取 formhash：<input type="hidden" name="formhash" value="36ab0dca" />
            var formhashMatch = Regex.Match(htmlContent, @"<input[^>]*name=""formhash""[^>]*value=""([^""]*)""", RegexOptions.IgnoreCase);
            if (formhashMatch.Success)
            {
                result.FormHash = formhashMatch.Groups[1].Value.Trim();
                Debug.WriteLine($" 提取 FormHash: {result.FormHash}");
            }

            // 提取 referer：<input type="hidden" name="referer" value="https://bbs.pcbeta.com/./" />
            var refererMatch = Regex.Match(htmlContent, @"<input[^>]*name=""referer""[^>]*value=""([^""]*)""", RegexOptions.IgnoreCase);
            if (refererMatch.Success)
            {
                result.Referer = HtmlDecode(refererMatch.Groups[1].Value.Trim());
                Debug.WriteLine($" 提取 Referer: {result.Referer}");
            }

            // 提取 aid：<input type="hidden" name="aid" value="4616856" />
            var aidMatch = Regex.Match(htmlContent, @"<input[^>]*name=""aid""[^>]*value=""([^""]*)""", RegexOptions.IgnoreCase);
            if (aidMatch.Success)
            {
                result.Aid = aidMatch.Groups[1].Value.Trim();
                Debug.WriteLine($" 提取 Aid: {result.Aid}");
            }

            // 提取交易ID（tid）：<input type="hidden" name="tid" value="..." />
            var tidMatch = Regex.Match(htmlContent, @"<input[^>]*name=""tid""[^>]*value=""([^""]*)""", RegexOptions.IgnoreCase);
            if (tidMatch.Success)
            {
                result.Tid = tidMatch.Groups[1].Value.Trim();
                Debug.WriteLine($" 提取 Tid: {result.Tid}");
            }

            if (string.IsNullOrEmpty(result.FormHash))
            {
                Debug.WriteLine("⚠️ 未能提取到 FormHash");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 提取购买参数错误: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    ///  新增：检测响应中的错误信息
    /// 支持的错误类型：
    /// 1. 访问权限错误：alert_error 类型的提示
    /// 2. 需要登录：包含 "登录" 的提示
    /// 3. 其他系统提示：通用的提示信息
    /// </summary>
    private string? ExtractErrorMessageFromResponse(string xmlResponse)
    {
        try
        {
            // 提取 CDATA 或原始内容
            string htmlContent = xmlResponse;
            try
            {
                var doc = XDocument.Parse(xmlResponse);
                var root = doc.Root;
                var extracted = ExtractCDataContent(root);
                if (!string.IsNullOrEmpty(extracted))
                    htmlContent = extracted;
            }
            catch
            {
                // 如果 XML 解析失败，使用原始内容
            }

            // 检查 1：访问权限错误（alert_error 类）
            // 格式：<div class="alert_error">错误信息</div>
            var errorAlertMatch = Regex.Match(
                htmlContent, 
                @"<div[^>]*class=""[^""]*alert_error[^""]*""[^>]*>([^<]+)</div>", 
                RegexOptions.IgnoreCase | RegexOptions.Singleline
            );

            if (errorAlertMatch.Success)
            {
                var errorMsg = HtmlDecode(errorAlertMatch.Groups[1].Value.Trim());
                Debug.WriteLine($"⚠️ 检测到错误信息 (alert_error): {errorMsg}");
                return $"❌ 错误：{errorMsg}";
            }

            // 检查 2：条件访问限制（您需要满足以下条件才能访问这个版块）
            // 这种情况通常会有访问条件和用户信息对比
            var accessConditionMatch = Regex.Match(
                htmlContent,
                @"您需要满足以下条件才能访问.*?<b>访问条件：</b>.*?<br\s*/?>(.*?)<b>您的信息：</b>(.*?)(?=<script|</div|$)",
                RegexOptions.IgnoreCase | RegexOptions.Singleline
            );

            if (accessConditionMatch.Success)
            {
                var conditions = CleanErrorMessageText(accessConditionMatch.Groups[1].Value).Trim();
                var userInfo = CleanErrorMessageText(accessConditionMatch.Groups[2].Value).Trim();

                var errorMessage = $"❌ 访问权限错误\n\n【访问条件】\n{conditions}\n\n【您的信息】\n{userInfo}";
                Debug.WriteLine($"⚠️ 检测到条件限制: {errorMessage}");
                return errorMessage;
            }

            // 检查 3：通用系统提示信息（包含"提示信息"标签）
            // 格式：<h3 class="flb"><em>提示信息</em>...
            if (htmlContent.Contains("<em>提示信息</em>") || htmlContent.Contains("alert_error"))
            {
                // 提取提示框中的所有文本内容
                var tipMatch = Regex.Match(
                    htmlContent,
                    @"<div[^>]*class=""[^""]*altw[^""]*""[^>]*>(.*?)</div>\s*</div>",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline
                );

                if (tipMatch.Success)
                {
                    var tipContent = CleanErrorMessageText(tipMatch.Groups[1].Value).Trim();
                    tipContent = Regex.Replace(tipContent, @"\s+", " ");  // 规范化空格

                    if (!string.IsNullOrEmpty(tipContent) && tipContent.Length > 5)
                    {
                        Debug.WriteLine($"⚠️ 检测到系统提示: {tipContent}");
                        return $"⚠️ {tipContent}";
                    }
                }
            }

            // 检查 4：需要登录（包含登录相关的提示）
            if (htmlContent.Contains("<strong>登录</strong>") && htmlContent.Contains("alert"))
            {
                Debug.WriteLine("⚠️ 检测到需要登录的错误");
                return "❌ 需要登录才能查看此内容";
            }

            // 未检测到错误
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 提取错误信息时出错: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Extracts user avatar URL from user profile XML response
    /// </summary>
    public string ExtractUserAvatarUrl(string xmlResponse)
    {
        try
        {
            var doc = XDocument.Parse(xmlResponse);
            var root = doc.Root;

            // Extract CDATA content if present
            var htmlContent = ExtractCDataContent(root);
            if (string.IsNullOrEmpty(htmlContent))
                htmlContent = root?.Value ?? xmlResponse;

            // Match img tag with class="user_avatar"
            string pattern = "<img.*?(https:\\/\\/uc.pcbeta.com(.*?))\"";
            var match = Regex.Match(htmlContent, pattern, RegexOptions.IgnoreCase);

            if (match.Success)
            {
                var avatarUrl = match.Groups[1].Value;
                Debug.WriteLine($"Extracted avatar URL: {avatarUrl}");
                return avatarUrl;
            }

            Debug.WriteLine("No avatar URL found in response");
            return string.Empty;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Extract avatar URL error: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    ///  新增：从HTML内容中提取帖子评论信息
    /// 对应HTML结构：&lt;div id="comment_XXXXXXX" class="cm"&gt;...&lt;/div&gt;
    /// 
    /// 评论结构：
    /// &lt;div id="comment_*" class="cm"&gt;
    ///   &lt;h3 class="psth"&gt;&lt;span&gt;点评&lt;/span&gt;&lt;/h3&gt;
    ///   &lt;div class="pstl"&gt;
    ///     &lt;div class="psta"&gt;
    ///       &lt;a href="..." target="_blank"&gt;&lt;img src="..." class="user_avatar"&gt;&lt;/a&gt;
    ///     &lt;/div&gt;
    ///     &lt;div class="psti"&gt;
    ///       &lt;a href="..." class="xi2 xw1"&gt;用户名&lt;/a&gt;
    ///       评论内容
    ///       &lt;span class="xg1"&gt;发表于 日期时间&lt;/span&gt;
    ///     &lt;/div&gt;
    ///   &lt;/div&gt;
    ///   ... 更多评论 ...
    /// &lt;/div&gt;
    /// </summary>
    private List<CommentInfo>? ExtractCommentsFromHtml(string htmlContent)
    {
        var comments = new List<CommentInfo>();

        try
        {
            // 查找评论容器 <div id="comment_*" class="cm">
            var commentContainerPattern = @"<div[^>]*id=""comment_\d+""[^>]*class=""[^""]*cm[^""]*""[^>]*>(.*?)(?=<div[^>]*id=""ratelog_|<div[^>]*class=""pct""|<script|$)";
            var containerMatch = Regex.Match(htmlContent, commentContainerPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (!containerMatch.Success)
            {
                Debug.WriteLine("⚠️ 未找到评论容器");
                return null;
            }

            var commentHtml = containerMatch.Groups[1].Value;

            // 提取每条评论：<div class="pstl xs1 cl">
            //  修复：使用更准确的模式来匹配包含 psta 和 psti 的完整 pstl 块
            // 原问题：(.*?)</div> 会在第一个 </div>（psta 的结束）处停止
            // 解决方案：匹配到 psti 的结束标签后的 </div>
            var commentPattern = @"<div[^>]*class=""[^""]*pstl[^""]*""[^>]*>([\s\S]*?<div[^>]*class=""psti[^""]*""[^>]*>[\s\S]*?</div>)\s*</div>";
            var commentMatches = Regex.Matches(commentHtml, commentPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

            Debug.WriteLine($" 找到 {commentMatches.Count} 条评论");

            foreach (Match commentMatch in commentMatches)
            {
                var commentBlock = commentMatch.Groups[1].Value;

                try
                {
                    // 提取用户头像URL - <img src="..." class="user_avatar">
                    var avatarMatch = Regex.Match(commentBlock, @"<img[^>]*src=""([^""]*)""[^>]*class=""user_avatar""", RegexOptions.IgnoreCase);
                    var avatarUrl = avatarMatch.Success ? avatarMatch.Groups[1].Value.Trim() : string.Empty;

                    // 提取用户名和个人空间链接 - <a href="..." class="xi2 xw1">用户名</a>
                    var usernameMatch = Regex.Match(commentBlock, @"<a[^>]*href=""([^""]*)""[^>]*class=""[^""]*xi2[^""]*""[^>]*>([^<]+)</a>", RegexOptions.IgnoreCase);

                    if (!usernameMatch.Success)
                        continue;

                    var userProfileUrl = HtmlDecode(usernameMatch.Groups[1].Value.Trim());
                    var username = HtmlDecode(usernameMatch.Groups[2].Value.Trim());

                    // 提取评论内容 - 在 <div class="psti"> 中的纯文本或经过解析的内容
                    var pstiMatch = Regex.Match(commentBlock, @"<div[^>]*class=""[^""]*psti[^""]*""[^>]*>(.*?)(?=</div>|<span[^>]*class=""xg1"")", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    var commentText = string.Empty;

                    if (pstiMatch.Success)
                    {
                        // 移除嵌套的 <a> 标签后的内容，只保留纯文本
                        commentText = pstiMatch.Groups[1].Value.Trim();
                        // 移除所有HTML标签
                        commentText = StripHtmlTags(commentText);
                        commentText = HtmlDecode(commentText).Trim();
                    }

                    // 提取时间戳 - <span class="xg1">发表于 日期</span>
                    var timestampMatch = Regex.Match(commentBlock,@"<span[^>]*class=""[^""]*xg1[^""]*""[^>]*>.*?发表于\s*(.*?)</span>",RegexOptions.Singleline);
                    var timestamp = timestampMatch.Success ? HtmlDecode(timestampMatch.Groups[1].Value.Trim()) : string.Empty;

                    Debug.WriteLine($" 提取评论: 用户={username}, 时间={timestamp}");

                    comments.Add(new CommentInfo
                    {
                        Username = username,
                        AvatarUrl = avatarUrl,
                        CommentText = commentText,
                        Timestamp = timestamp,
                        UserProfileUrl = userProfileUrl
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"❌ 解析单条评论错误: {ex.Message}");
                    continue;
                }
            }

            if (comments.Count > 0)
            {
                Debug.WriteLine($" 总共提取 {comments.Count} 条评论");
                return comments;
            }

            Debug.WriteLine("⚠️ 未能提取到任何评论");
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 提取评论HTML错误: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///  新增：从HTML内容中提取帖子评分信息
    /// 对应HTML结构：&lt;dl id="ratelog_XXXXXXX" class="rate"&gt;...&lt;/dl&gt;
    /// 
    /// 评分结构：
    /// &lt;dl id="ratelog_*" class="rate"&gt;
    ///   &lt;dt&gt;
    ///     &lt;strong&gt;&lt;a href="..." onclick="..."&gt;26&lt;/a&gt;&lt;/strong&gt;  &lt;!-- 评分数 --&gt;
    ///     &lt;p&gt;&lt;a href="..."&gt;查看全部评分&lt;/a&gt;&lt;/p&gt;
    ///   &lt;/dt&gt;
    ///   &lt;dd&gt;
    ///     &lt;div id="post_rate_*"&gt;&lt;/div&gt;
    ///     &lt;ul class="cl"&gt;
    ///       &lt;li&gt;
    ///         &lt;p id="rate_*" onmouseover="showTip(this)" tip="&lt;strong&gt;...&lt;/strong&gt;&amp;nbsp;&lt;em&gt;...&lt;/em&gt;"&gt;
    ///           &lt;a href="..." class="avt"&gt;&lt;img src="..." class="user_avatar"&gt;&lt;/a&gt;
    ///         &lt;/p&gt;
    ///         &lt;p&gt;&lt;a href="..."&gt;用户名&lt;/a&gt;&lt;/p&gt;
    ///       &lt;/li&gt;
    ///       ... 更多评分 ...
    ///     &lt;/ul&gt;
    ///   &lt;/dd&gt;
    /// &lt;/dl&gt;
    /// </summary>
    private RatingSummary? ExtractRatingsFromHtml(string htmlContent)
    {
        try
        {
            // 查找评分容器 <dl id="ratelog_*" class="rate">
            var ratingContainerPattern = @"<dl[^>]*id=""ratelog_\d+""[^>]*class=""[^""]*rate[^""]*""[^>]*>(.*?)</dl>";
            var containerMatch = Regex.Match(htmlContent, ratingContainerPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (!containerMatch.Success)
            {
                Debug.WriteLine("⚠️ 未找到评分容器");
                return null;
            }

            var ratingHtml = containerMatch.Groups[1].Value;

            // 初始化评分汇总
            var ratingSummary = new RatingSummary();

            // 提取总评分数 - <strong><a>26</a></strong>
            var countMatch = Regex.Match(ratingHtml, @"<strong[^>]*>\s*<a[^>]*>(\d+)</a>\s*</strong>", RegexOptions.IgnoreCase);
            if (countMatch.Success && int.TryParse(countMatch.Groups[1].Value, out int count))
            {
                ratingSummary.TotalRatingCount = count;
                Debug.WriteLine($" 评分总数: {count}");
            }

            // 提取"查看全部评分"链接 - <a href="...">查看全部评分</a>
            var viewAllMatch = Regex.Match(ratingHtml, @"<a[^>]*href=""([^""]*)""[^>]*>\s*查看全部评分\s*</a>", RegexOptions.IgnoreCase);
            if (viewAllMatch.Success)
            {
                ratingSummary.ViewAllRatingsUrl = HtmlDecode(viewAllMatch.Groups[1].Value.Trim());
                Debug.WriteLine($" 查看全部评分链接: {ratingSummary.ViewAllRatingsUrl}");
            }

            // 提取评分详情 - <ul class="cl"><li>...
            var ulMatch = Regex.Match(ratingHtml, @"<ul[^>]*class=""[^""]*cl[^""]*""[^>]*>(.*?)</ul>", RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (ulMatch.Success)
            {
                var ulContent = ulMatch.Groups[1].Value;

                // 提取每个 <li> 评分项
                var liPattern = @"<li>(.*?)</li>";
                var liMatches = Regex.Matches(ulContent, liPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

                Debug.WriteLine($" 找到 {liMatches.Count} 条评分详情");

                foreach (Match liMatch in liMatches)
                {
                    var liContent = liMatch.Groups[1].Value;

                    try
                    {
                        // 提取用户头像URL
                        var avatarMatch = Regex.Match(liContent, @"<img[^>]*src=""([^""]*)""[^>]*class=""user_avatar""", RegexOptions.IgnoreCase);
                        var avatarUrl = avatarMatch.Success ? avatarMatch.Groups[1].Value.Trim() : string.Empty;

                        // 提取用户名和个人空间链接 - <a href="..." target="_blank">用户名</a>
                        var usernameMatch = Regex.Match(liContent, @"<a[^>]*href=""([^""]*)""[^>]*target=""_blank""[^>]*>([^<]+)</a>", RegexOptions.IgnoreCase);

                        if (!usernameMatch.Success)
                            continue;

                        var userProfileUrl = HtmlDecode(usernameMatch.Groups[1].Value.Trim());
                        var username = HtmlDecode(usernameMatch.Groups[2].Value.Trim());

                        // 提取评分内容和完整信息 - <p id="rate_*" onmouseover="..." tip="...">
                        var pMatch = Regex.Match(liContent, @"<p[^>]*id=""rate_[^""]*""[^>]*tip=""([^""]*)""[^>]*>", RegexOptions.IgnoreCase);

                        var ratingFullInfo = string.Empty;
                        var ratingContent = string.Empty;

                        if (pMatch.Success)
                        {
                            // 从 tip 属性提取完整信息
                            ratingFullInfo = HtmlDecode(pMatch.Groups[1].Value);

                            // 从 tip 中解析 <em> 标签的内容作为简短内容
                            var emMatch = Regex.Match(ratingFullInfo, @"<em[^>]*class=""[^""]*xi1[^""]*""[^>]*>([^<]+)</em>");
                            if (emMatch.Success)
                            {
                                ratingContent = HtmlDecode(emMatch.Groups[1].Value).Trim();
                            }
                            else
                            {
                                // 备选：直接使用完整信息但清除HTML标签
                                ratingContent = StripHtmlTags(ratingFullInfo).Trim();
                            }

                            // 清理完整信息（移除HTML标签，仅保留文本）
                            ratingFullInfo = StripHtmlTags(ratingFullInfo).Trim();
                        }

                        Debug.WriteLine($" 提取评分: 用户={username}, 内容={ratingContent}");

                        ratingSummary.RatingDetails.Add(new RatingInfo
                        {
                            Username = username,
                            AvatarUrl = avatarUrl,
                            RatingContent = ratingContent,
                            RatingFullInfo = ratingFullInfo,
                            UserProfileUrl = userProfileUrl
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"❌ 解析单条评分错误: {ex.Message}");
                        continue;
                    }
                }
            }

            if (ratingSummary.TotalRatingCount > 0 || ratingSummary.RatingDetails.Count > 0)
            {
                Debug.WriteLine($" 总共提取评分汇总: 总数={ratingSummary.TotalRatingCount}, 详情数={ratingSummary.RatingDetails.Count}");
                return ratingSummary;
            }

            Debug.WriteLine("⚠️ 未能提取到任何评分信息");
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 提取评分HTML错误: {ex.Message}");
            return null;
        }
    }
}
