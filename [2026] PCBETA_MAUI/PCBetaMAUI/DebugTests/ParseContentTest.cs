using PCBetaMAUI.Services;
using PCBetaMAUI.Models;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Xml.Linq;

namespace PCBetaMAUI.DebugTests;

/// <summary>
/// Quick debug test for content parsing to verify blockPattern improvements
/// Usage: Call TestParseForumContent() from debug console
/// </summary>
public class ParseContentTest
{
    /// <summary>
    /// 诊断渲染错误 - 查找哪些 ContentElement 有无效的 URL
    /// </summary>
    public static void DiagnoseRenderingErrors()
    {
        Debug.WriteLine("\n========== 开始诊断渲染错误 ==========");
        Debug.WriteLine("检查所有 ContentElement 的 URL 有效性...\n");

        // 这个方法应该在线程内容加载后调用
        // 需要访问 ThreadContentPage 中的 _threadContent 对象

        Debug.WriteLine("诊断完成。请查看上面的日志。");
    }

    /// <summary>
    /// 验证 URL 格式
    /// </summary>
    public static void ValidateUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            Debug.WriteLine("❌ URL 为空或全空白");
            return;
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            Debug.WriteLine($" URL 有效: {url}");
            Debug.WriteLine($"   - 方案: {uri.Scheme}");
            Debug.WriteLine($"   - 主机: {uri.Host}");
            Debug.WriteLine($"   - 路径: {uri.AbsolutePath}");
        }
        else
        {
            Debug.WriteLine($"❌ URL 无效: {url}");

            // 尝试诊断问题
            if (url.Contains(" "))
                Debug.WriteLine("   - 问题：URL 中包含空格");
            if (!url.StartsWith("http://") && !url.StartsWith("https://") && !url.StartsWith("data:"))
                Debug.WriteLine("   - 问题：缺少协议方案 (http:// 或 https://)");
            if (url.Contains("<") || url.Contains(">"))
                Debug.WriteLine("   - 问题：URL 中包含 HTML 标签");
            if (url.Contains("\"") || url.Contains("'"))
                Debug.WriteLine("   - 问题：URL 中包含引号");
        }
    }

    /// <summary>
    /// 测试改造后的 SplitContentIntoTokens 方法
    /// 验证其是否正确返回完整的HTML标签块而不是碎片化的tokens
    /// </summary>
    public static void TestSplitContentIntoTokens()
    {
        Debug.WriteLine("\n========== 开始测试 SplitContentIntoTokens ==========");

        // 测试用例1：简单表格
        var testCase1 = @"<table cellspacing=""0""><tr><td><a href=""url"">链接</a></td></tr></table>";
        TestTokenSplitting(testCase1, "测试用例1：简单表格");

        // 测试用例2：嵌套表格
        var testCase2 = @"<table><tr><td><table><tr><td>内容</td></tr></table></td></tr></table>";
        TestTokenSplitting(testCase2, "测试用例2：嵌套表格");

        // 测试用例3：混合内容（表格 + 文本 + 链接）
        var testCase3 = @"<table><tr><td>表格内容</td></tr></table>
中间的文本
<a href=""https://example.com"">链接文本</a>";
        TestTokenSplitting(testCase3, "测试用例3：混合内容");

        // 测试用例4：自闭合标签
        var testCase4 = @"<br/><img src=""test.jpg""/><hr/>";
        TestTokenSplitting(testCase4, "测试用例4：自闭合标签");

        // 测试用例5：真实论坛HTML（简化版）
        var testCase5 = @"<table cellspacing=""0"" class=""t_table"" style=""width:98%"" bgcolor=""lemonchiffon"">
<tr><td><a href=""https://bbs.pcbeta.com/viewthread-2064627-1-1.html"" target=""_blank"">[天翼云][资源] MSDN Windows 11 26H1 简繁英ISO</a>&nbsp;&nbsp;<strong>2026年2月11日</strong><br />
<a href=""https://bbs.pcbeta.com/viewthread-2063021-1-1.html"" target=""_blank"">[天翼云] [资源] 2026年1月 MSDN Windows 11</a></td><td>dsfdsfsdfsdf</td></tr>
</table>";
        TestTokenSplitting(testCase5, "测试用例5：真实论坛HTML（简化版）");

        Debug.WriteLine("========== 测试完成 ==========\n");
    }

    private static void TestTokenSplitting(string htmlContent, string caseDescription)
    {
        Debug.WriteLine($"\n{caseDescription}");
        Debug.WriteLine($"输入长度: {htmlContent.Length} 字符");

        // 使用反射调用 private 方法
        var service = new XmlParsingService();
        var method = typeof(XmlParsingService).GetMethod(
            "SplitContentIntoTokens",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
        );

        if (method == null)
        {
            Debug.WriteLine("❌ 无法找到 SplitContentIntoTokens 方法");
            return;
        }

        var tokens = method.Invoke(service, new object[] { htmlContent }) as List<string>;

        if (tokens == null || tokens.Count == 0)
        {
            Debug.WriteLine("❌ 返回的 tokens 为空或为 null");
            return;
        }

        Debug.WriteLine($" 返回 {tokens.Count} 个 token:");
        for (int i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            var displayToken = token.Length > 60 
                ? token.Substring(0, 57) + "..."
                : token;
            // 去除换行符以便显示
            displayToken = displayToken.Replace("\n", "\\n").Replace("\r", "\\r");

            Debug.WriteLine($"   [{i}] {displayToken} (长度: {token.Length})");
        }

        // 验证：检查是否没有孤立的开始/结束标签
        VerifyTokenIntegrity(tokens);
    }

    private static void VerifyTokenIntegrity(List<string> tokens)
    {
        Debug.WriteLine("\n验证 token 完整性:");
        bool hasIssues = false;

        foreach (var token in tokens)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                Debug.WriteLine("  ⚠️  发现空白 token");
                hasIssues = true;
                continue;
            }

            var trimmed = token.Trim();

            // 检查是否是孤立的结束标签
            if (trimmed.StartsWith("</") && !trimmed.Contains(">"))
            {
                Debug.WriteLine($"  ⚠️  孤立的结束标签: {trimmed}");
                hasIssues = true;
            }

            // 检查开始标签是否有对应的结束标签
            if (trimmed.StartsWith("<") && !trimmed.StartsWith("</") && !trimmed.EndsWith("/>"))
            {
                var tagNameMatch = System.Text.RegularExpressions.Regex.Match(trimmed, @"<(\w+)");
                if (tagNameMatch.Success)
                {
                    var tagName = tagNameMatch.Groups[1].Value.ToLower();
                    var endTag = $"</{tagName}>";

                    if (!trimmed.Contains(endTag, StringComparison.OrdinalIgnoreCase))
                    {
                        // 对于单行标签，允许没有结束标签的情况
                        if (!trimmed.Contains("/>") && !trimmed.Contains($"</{tagName}>"))
                        {
                            Debug.WriteLine($"  ⚠️  开始标签缺少结束标签: {tagName}");
                            hasIssues = true;
                        }
                    }
                }
            }
        }

        if (!hasIssues)
        {
            Debug.WriteLine("   所有 token 完整性检查通过");
        }
    }

    public static void TestParseForumContent()
    {
        try
        {
            // Load forum.xml from disk
            var forumXmlPath = Path.Combine(AppContext.BaseDirectory, "WebAch", "forum.xml");


            {
                Debug.WriteLine($"❌ 找不到测试文件: {forumXmlPath}");
                return;
            }

            // Parse XML and extract CDATA content
            var xmlDoc = XDocument.Load(forumXmlPath);
            var root = xmlDoc.Root;
            
            if (root == null || string.IsNullOrEmpty(root.Value))
            {
                Debug.WriteLine("❌ XML 根节点或内容为空");
                return;
            }

            var htmlContent = root.Value;
            
            // Look for post content (postmessage_56578178)
            var startMarker = "id=\"postmessage_56578178\"";
            var startIndex = htmlContent.IndexOf(startMarker);
            
            if (startIndex == -1)
            {
                Debug.WriteLine("❌ 找不到帖子内容标记");
                return;
            }

            // Extract content from td tag
            var contentStart = htmlContent.IndexOf(">", startIndex) + 1;
            var contentEnd = htmlContent.IndexOf("</td>", contentStart);
            var postContent = htmlContent.Substring(contentStart, contentEnd - contentStart);

            Debug.WriteLine($"📋 原始帖子内容长度: {postContent.Length} 字符");
            Debug.WriteLine($"📋 前 200 字符: {postContent.Substring(0, Math.Min(200, postContent.Length))}");
            Debug.WriteLine("");

            // Now test the parsing service
            // Use reflection to call private ParseRichTextContent method
            var method = typeof(XmlParsingService).GetMethod(
                "ParseRichTextContent",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
            );

            if (method == null)
            {
                Debug.WriteLine("❌ 无法找到 ParseRichTextContent 方法");
                return;
            }

            // Create an instance of XmlParsingService to call the instance method
            var service = new XmlParsingService();
            var elements = method.Invoke(service, new object[] { postContent }) as List<ContentElement>;

            Debug.WriteLine($" 解析完成!");
            Debug.WriteLine($"📊 提取的元素数量: {elements?.Count ?? 0}");
            Debug.WriteLine("");
            Debug.WriteLine($"📋 原始帖子内容长度: {postContent.Length} 字符");
            
            if (elements != null && elements.Count > 0)
            {
                Debug.WriteLine("");
                Debug.WriteLine("📝 提取的内容预览 (前20个元素):");
                Debug.WriteLine(new string('-', 80));
                
                for (int i = 0; i < Math.Min(20, elements.Count); i++)
                {
                    var elem = elements[i];
                    var preview = elem.Text ?? elem.Title ?? elem.Url ?? $"[{elem.Type}]";
                    if (preview.Length > 100)
                        preview = preview.Substring(0, 100) + "...";
                    
                    Debug.WriteLine($"{i + 1:D2}. [{elem.Type}] {preview}");
                }
                
                Debug.WriteLine(new string('-', 80));
                Debug.WriteLine("");
                Debug.WriteLine($"总共提取 {elements.Count} 个内容元素");
                
                // Stats by type
                var typeStats = new Dictionary<ContentElementType, int>();
                foreach (var elem in elements)
                {
                    if (!typeStats.ContainsKey(elem.Type))
                        typeStats[elem.Type] = 0;
                    typeStats[elem.Type]++;
                }
                
                Debug.WriteLine("");
                Debug.WriteLine("📊 元素类型统计:");
                foreach (var kvp in typeStats)
                {
                    Debug.WriteLine($"  {kvp.Key}: {kvp.Value} 个");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 测试失败: {ex.Message}");
            Debug.WriteLine($"   堆栈: {ex.StackTrace}");
        }
    }

    /// <summary>
    ///  新增：测试附件解析
    /// 验证是否能正确识别和解析 PCBETA 论坛中的附件
    /// </summary>
    public static void TestAttachmentParsing()
    {
        Debug.WriteLine("\n========== 开始测试附件解析 ==========");

        // 测试用例 1：标准附件容器结构（来自 forum.xml）
        var attachmentSpan = @"<span style=""white-space: nowrap"" id=""attach_4238522"">
<a href=""https://bbs.pcbeta.com/forum.php?mod=attachment&aid=NDIzODUyMnw4N2MwN2YwMnwxNzc0MjcyODc4fDQ4MTk2NjJ8MTg3MjU1OQ%3D%3D"" target=""_blank"">Microsoft-Activation-Scripts-v3.9_ZH-CN.zip</a>
<em class=""xg1"">(459.47 KB, 下载次数: 19009)</em>
</span>";

        Debug.WriteLine("\n测试用例 1：标准附件容器");
        Debug.WriteLine("输入 HTML:");
        Debug.WriteLine(attachmentSpan);

        // 测试用例 2：简化的附件链接
        var simpleAttachment = @"<a href=""https://bbs.pcbeta.com/forum.php?mod=attachment&aid=123456789"">test-file.zip</a>";

        Debug.WriteLine("\n测试用例 2：简化的附件链接");
        Debug.WriteLine("输入 HTML:");
        Debug.WriteLine(simpleAttachment);

        // 使用 XmlParsingService 的 ParseThreadContent 方法来测试
        var service = new XmlParsingService();

        // 将附件 HTML 包装在完整的帖子内容中
        var testPostContent = $@"<td class=""t_f"" id=""postmessage_12345"">
{attachmentSpan}
<br/>
一些文本内容
<br/>
{simpleAttachment}
</td>";

        Debug.WriteLine("\n完整帖子内容:");
        Debug.WriteLine("正在解析...");

        var threadContent = service.ParseThreadContent(testPostContent);

        Debug.WriteLine($"\n 解析完成!");
        Debug.WriteLine($"📊 提取的元素数量: {threadContent.ContentElements?.Count ?? 0}");

        if (threadContent.ContentElements != null && threadContent.ContentElements.Count > 0)
        {
            Debug.WriteLine("\n📝 提取的内容元素：");
            Debug.WriteLine(new string('-', 80));

            int attachmentCount = 0;
            foreach (var elem in threadContent.ContentElements)
            {
                if (elem.Type == ContentElementType.Attachment)
                {
                    attachmentCount++;
                    Debug.WriteLine($" 附件 #{attachmentCount}");
                    Debug.WriteLine($"   文件名: {elem.FileName}");
                    Debug.WriteLine($"   大小: {elem.FileSize}");
                    Debug.WriteLine($"   URL: {elem.Url.Substring(0, Math.Min(60, elem.Url.Length))}...");
                    Debug.WriteLine($"   分组ID: {elem.HorizontalGroupId}");
                }
                else if (elem.Type == ContentElementType.Text)
                {
                    Debug.WriteLine($"📄 文本: {elem.Text.Substring(0, Math.Min(50, elem.Text.Length))}");
                }
                else if (elem.Type == ContentElementType.Link)
                {
                    Debug.WriteLine($"🔗 链接: {elem.Title} -> {elem.Url.Substring(0, Math.Min(40, elem.Url.Length))}...");
                }
                else
                {
                    Debug.WriteLine($"[{elem.Type}] {elem.Text ?? elem.Title ?? ""}");
                }
            }

            Debug.WriteLine(new string('-', 80));
            Debug.WriteLine($"\n 总共识别出 {attachmentCount} 个附件");

            if (attachmentCount == 2)
            {
                Debug.WriteLine(" 附件识别正确！");
            }
            else
            {
                Debug.WriteLine($"⚠️  预期 2 个附件，但识别了 {attachmentCount} 个");
            }
        }

        Debug.WriteLine("\n========== 附件解析测试完成 ==========\n");
    }

    /// <summary>
    ///  新增：测试编辑状态和审核信息提取
    /// 验证是否能正确提取帖子的编辑状态和审核通过信息
    /// </summary>
    public static void TestEditStatusAndModerationInfo()
    {
        Debug.WriteLine("\n========== 开始测试编辑状态和审核信息提取 ==========");

        try
        {
            // 使用 forum.xml 作为测试数据
            var forumXmlPath = Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..",  // 从 bin 目录回退到项目根目录
                "WebAch", "forum.xml"
            );

            if (!File.Exists(forumXmlPath))
            {
                Debug.WriteLine($"❌ 找不到测试文件: {forumXmlPath}");
                return;
            }

            // 读取 XML 文件
            var xmlContent = File.ReadAllText(forumXmlPath);
            Debug.WriteLine($" 已加载 forum.xml, 大小: {xmlContent.Length} 字节");

            // 使用 XmlParsingService 解析帖子内容
            var service = new XmlParsingService();
            var threadContent = service.ParseThreadContent(xmlContent);

            Debug.WriteLine("\n📊 提取的帖子信息:");
            Debug.WriteLine($"  标题: {threadContent.Title}");
            Debug.WriteLine($"  作者: {threadContent.Author}");
            Debug.WriteLine($"  发表时间: {threadContent.PostTime}");

            Debug.WriteLine("\n🔍 编辑和审核信息:");

            if (!string.IsNullOrEmpty(threadContent.EditStatus))
            {
                Debug.WriteLine($" 编辑状态: {threadContent.EditStatus}");
            }
            else
            {
                Debug.WriteLine("❌ 未能提取编辑状态");
            }

            if (!string.IsNullOrEmpty(threadContent.ModerationInfo))
            {
                Debug.WriteLine($" 审核信息: {threadContent.ModerationInfo}");
            }
            else
            {
                Debug.WriteLine("❌ 未能提取审核信息");
            }

            // 验证内容
            Debug.WriteLine($"\n📋 内容元素统计:");
            Debug.WriteLine($"  总元素数: {threadContent.ContentElements.Count}");

            // 统计各类型元素
            var typeStats = new Dictionary<ContentElementType, int>();
            foreach (var elem in threadContent.ContentElements)
            {
                if (!typeStats.ContainsKey(elem.Type))
                    typeStats[elem.Type] = 0;
                typeStats[elem.Type]++;
            }

            foreach (var kvp in typeStats.OrderByDescending(x => x.Value))
            {
                Debug.WriteLine($"  {kvp.Key}: {kvp.Value} 个");
            }

            // 最终验证
            Debug.WriteLine("\n 测试完成！");
            if (!string.IsNullOrEmpty(threadContent.EditStatus) && !string.IsNullOrEmpty(threadContent.ModerationInfo))
            {
                Debug.WriteLine(" 编辑状态和审核信息都已成功提取！");
            }
            else
            {
                Debug.WriteLine("⚠️  某些信息未能提取，请检查正则表达式或 HTML 格式");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 测试失败: {ex.Message}");
            Debug.WriteLine($"   堆栈: {ex.StackTrace}");
        }

        Debug.WriteLine("\n========== 编辑状态和审核信息测试完成 ==========\n");
    }
}


