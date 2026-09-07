using PCBetaMAUI.Models;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace PCBetaMAUI.Services;

public partial class XmlParsingService
{
    // Use XmlParsingService.ExtractErrorMessageFromResponse defined in the main partial class

    /// <summary>
    /// Parses favorites page HTML and extracts favorite items
    /// </summary>
    public List<FavoriteItem> ParseFavorites(string htmlContent)
    {
        var list = new List<FavoriteItem>();
        try
        {
            if (string.IsNullOrEmpty(htmlContent))
                return list;

            // Find the favorite list UL
            var ulMatch = Regex.Match(htmlContent, @"<ul[^>]*id=""favorite_ul""[^>]*>(.*?)</ul>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (!ulMatch.Success)
                return list;

            var ulContent = ulMatch.Groups[1].Value;

            // Each LI represents a favorite item
            var liMatches = Regex.Matches(ulContent, @"<li[^>]*id=""fav_(\d+)""[^>]*>(.*?)</li>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            foreach (Match li in liMatches)
            {
                try
                {
                    var favId = li.Groups[1].Value;
                    var liContent = li.Groups[2].Value;

                    // Extract thread link and title
                    var aMatch = Regex.Match(liContent, @"<a[^>]*href=""([^""]*viewthread[^""]*)""[^>]*>(.*?)</a>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                    string title = string.Empty;
                    string threadUrl = string.Empty;
                    if (aMatch.Success)
                    {
                        threadUrl = System.Net.WebUtility.HtmlDecode(aMatch.Groups[1].Value);
                        title = Regex.Replace(aMatch.Groups[2].Value, "<.*?>", "").Trim();
                    }

                    // Extract vid attribute from input checkbox
                    var vidMatch = Regex.Match(liContent, @"<input[^>]*vid=""(\d+)""", RegexOptions.IgnoreCase);
                    var vid = vidMatch.Success ? vidMatch.Groups[1].Value : string.Empty;

                    // Extract added time
                    var timeMatch = Regex.Match(liContent, @"<span[^>]*class=""xg1""[^>]*>([^<]+)</span>", RegexOptions.IgnoreCase);
                    var addedTime = timeMatch.Success ? timeMatch.Groups[1].Value.Trim() : string.Empty;

                    // Extract handleKey from delete link (a_delete_FAVID)
                    var handleKeyMatch = Regex.Match(liContent, @"<a[^>]*id=""(a_delete_\d+)""", RegexOptions.IgnoreCase);
                    var handleKey = handleKeyMatch.Success ? handleKeyMatch.Groups[1].Value : string.Empty;

                    var item = new FavoriteItem
                    {
                        FavId = favId,
                        ThreadId = vid,
                        Title = System.Net.WebUtility.HtmlDecode(title),
                        AddedTime = addedTime,
                        HandleKey = handleKey
                    };

                    list.Add(item);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Parse favorite li error: {ex.Message}");
                    continue;
                }
            }

        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Parse favorites error: {ex.Message}");
        }

        return list;
    }

    /// <summary>
    /// 从收藏页面HTML中提取formhash
    /// formhash通常在<input type="hidden" name="formhash" value="xxx">中
    /// </summary>
    public string ExtractFormHashFromFavoritePage(string htmlContent)
    {
        try
        {
            if (string.IsNullOrEmpty(htmlContent))
                return string.Empty;

            // 查找隐藏的formhash input字段
            var formhashMatch = Regex.Match(htmlContent, @"<input[^>]*type=""hidden""[^>]*name=""formhash""[^>]*value=""([^""]*)""", RegexOptions.IgnoreCase);
            if (formhashMatch.Success)
            {
                var formhash = formhashMatch.Groups[1].Value.Trim();
                Debug.WriteLine($"✅ 提取formhash: {formhash}");
                return formhash;
            }

            // 备用方案：在表单顶部查找
            formhashMatch = Regex.Match(htmlContent, @"name=""formhash""[^>]*value=""([^""]*)""", RegexOptions.IgnoreCase);
            if (formhashMatch.Success)
            {
                return formhashMatch.Groups[1].Value.Trim();
            }

            Debug.WriteLine("⚠️ 未能提取formhash");
            return string.Empty;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 提取formhash错误: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// 检查删除收藏的响应是否成功
    /// 成功标记：包含 "succeedhandle_" 和 "操作成功"
    /// </summary>
    public bool IsDeleteFavoriteSuccess(string xmlResponse)
    {
        try
        {
            if (string.IsNullOrEmpty(xmlResponse))
                return false;

            // 提取CDATA内容
            string htmlContent = xmlResponse;
            try
            {
                var doc = System.Xml.Linq.XDocument.Parse(xmlResponse);
                var root = doc.Root;
                var extracted = ExtractCDataContent(root);
                if (!string.IsNullOrEmpty(extracted))
                    htmlContent = extracted;
            }
            catch
            {
                // 如果XML解析失败，使用原始内容
            }

            // 检查成功标记
            bool hasSucceedHandle = htmlContent.Contains("succeedhandle_");
            bool hasSuccessMsg = htmlContent.Contains("操作成功");

            if (hasSucceedHandle && hasSuccessMsg)
            {
                Debug.WriteLine("✅ 删除收藏成功");
                return true;
            }

            // 检查失败标记
            if (htmlContent.Contains("errorhandle_"))
            {
                Debug.WriteLine("❌ 删除收藏失败");
                return false;
            }

            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 检查删除响应错误: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 从删除响应中提取错误信息
    /// </summary>
    public string ExtractDeleteErrorMessage(string xmlResponse)
    {
        try
        {
            if (string.IsNullOrEmpty(xmlResponse))
                return "删除失败";

            // 提取CDATA内容
            string htmlContent = xmlResponse;
            try
            {
                var doc = System.Xml.Linq.XDocument.Parse(xmlResponse);
                var root = doc.Root;
                var extracted = ExtractCDataContent(root);
                if (!string.IsNullOrEmpty(extracted))
                    htmlContent = extracted;
            }
            catch
            {
                // 如果XML解析失败，使用原始内容
            }

            // 查找错误信息：在errorhandle_之后提取错误文本
            var errorMatch = Regex.Match(htmlContent, @"errorhandle_[^(]*\('([^']*)'", RegexOptions.IgnoreCase);
            if (errorMatch.Success)
            {
                return errorMatch.Groups[1].Value.Trim();
            }

            // 备用方案：查找纯文本错误信息
            if (htmlContent.Contains("您指定的收藏不存在"))
                return "您指定的收藏不存在";

            if (htmlContent.Contains("未登录"))
                return "未登录";

            return "删除失败";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 提取错误信息错误: {ex.Message}");
            return "删除失败";
        }
    }
}
