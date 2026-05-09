using PCBetaMAUI.Services;
using PCBetaMAUI.Models;
using System.Diagnostics;
using System.Xml.Linq;

namespace PCBetaMAUI.DebugTests;

/// <summary>
/// 测试 pattl 容器附件解析功能
/// 用于验证从版主信息下方的 pattl div 中提取附件的功能
/// </summary>
public class PattlAttachmentParserTest
{
    /// <summary>
    /// 测试从 pattl 容器中解析附件
    /// 使用 forum.xml 中提供的实际 HTML 结构
    /// </summary>
    public static void TestPattlAttachmentParsing()
    {
        Debug.WriteLine("\n========== 开始测试 pattl 附件解析 ==========\n");

        // 模拟从 forum.xml 提供的 pattl 结构
        string testHtmlWithAttachments = @"<div class=""pattl"">
<ignore_js_op>
<dl class=""tattl attm"">
<dt></dt>
<dd>
<p class=""mbn"">
<a href=""https://bbs.pcbeta.com/forum.php?mod=attachment&amp;aid=NDYxNzMyMnwxMzliZDY1MnwxNzc0NzEyMTA1fDQ4MTk2NjJ8MjA2NzMwMA%3D%3D&amp;nothumb=yes"" onmouseover=""showMenu({'ctrlid':this.id,'pos':'12'})"" id=""aid4617322"" class=""xw1"" target=""_blank"">无标题.png</a>
<em class=""xg1"">(326.55 KB, 下载次数: 0)</em>
</p>
<div class=""tip tip_4"" id=""aid4617322_menu"" style=""display: none"" disautofocus=""true"">
<div class=""tip_c"">
<p class=""y"">2026-3-27 15:59 上传</p>
<p>点击文件名下载附件</p>
</div>
<div class=""tip_horn""></div>
</div>
<p class=""mbn"">
326.55 KB, 下载次数: 0, 下载积分: PB币 -1 
</p>
</dd>
</dl>
</ignore_js_op>
</div>";

        string testHtmlWithoutAttachments = @"<div class=""pattl"">
<!-- 空的 pattl 容器 -->
</div>";

        // 测试 1：有附件的情况
        Debug.WriteLine("【测试 1】pattl 容器中有附件");
        TestPattlParsing(testHtmlWithAttachments, shouldHaveAttachments: true);

        Debug.WriteLine("\n【测试 2】pattl 容器中没有附件");
        TestPattlParsing(testHtmlWithoutAttachments, shouldHaveAttachments: false);

        Debug.WriteLine("\n========== pattl 附件解析测试完成 ==========\n");
    }

    /// <summary>
    /// 辅助方法：执行单个 pattl 解析测试
    /// </summary>
    private static void TestPattlParsing(string htmlContent, bool shouldHaveAttachments)
    {
        try
        {
            var service = new XmlParsingService();

            // 创建完整的 XML 包装（模拟从 API 获取的格式）
            string xmlResponse = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<root><![CDATA[{htmlContent}]]></root>";

            Debug.WriteLine($"输入 HTML：{htmlContent.Substring(0, Math.Min(100, htmlContent.Length))}...");

            // 模拟帖子内容解析（会间接调用 pattl 解析）
            var threadContent = service.ParseThreadContent(xmlResponse);

            // 检查是否解析到附件
            if (threadContent.ContentElements != null)
            {
                var attachments = threadContent.ContentElements
                    .Where(e => e.Type == ContentElementType.Attachment)
                    .ToList();

                Debug.WriteLine($"解析结果：找到 {attachments.Count} 个附件");

                if (shouldHaveAttachments && attachments.Count > 0)
                {
                    Debug.WriteLine(" PASS：成功解析出附件（预期有附件）");
                    foreach (var att in attachments)
                    {
                        Debug.WriteLine($"   - 文件名：{att.FileName}");
                        Debug.WriteLine($"   - 大小：{att.FileSize}");
                        Debug.WriteLine($"   - URL：{att.Url?.Substring(0, Math.Min(60, att.Url.Length))}...");
                        Debug.WriteLine($"   - 是否已购买：{(att.SalePrice == null ? "是" : "否")}");
                    }
                }
                else if (!shouldHaveAttachments && attachments.Count == 0)
                {
                    Debug.WriteLine(" PASS：正确处理空附件列表（预期没有附件）");
                }
                else if (shouldHaveAttachments && attachments.Count == 0)
                {
                    Debug.WriteLine("❌ FAIL：预期有附件但解析失败");
                }
                else
                {
                    Debug.WriteLine("❌ FAIL：预期没有附件但意外解析出了附件");
                }
            }
            else
            {
                Debug.WriteLine("❌ ERROR：ContentElements 为 null");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ ERROR：{ex.Message}");
            Debug.WriteLine($"堆栈跟踪：{ex.StackTrace}");
        }
    }

    /// <summary>
    /// 测试空 pattl 容器的处理（条件解析验证）
    /// 确保只有在真正存在附件时才创建 ContentElement
    /// </summary>
    public static void TestConditionalPattlParsing()
    {
        Debug.WriteLine("\n========== 测试条件性 pattl 解析 ==========\n");

        var testCases = new[]
        {
            ("带多个附件的 pattl", @"<div class=""pattl"">
<ignore_js_op>
<dl class=""tattl attm"">
<dd>
<p><a href=""https://bbs.pcbeta.com/forum.php?mod=attachment&aid=123"">file1.zip</a><em>(10 KB, 下载次数: 5)</em></p>
</dd>
</dl>
<dl class=""tattl attm"">
<dd>
<p><a href=""https://bbs.pcbeta.com/forum.php?mod=attachment&aid=124"">file2.rar</a><em>(20 KB, 下载次数: 3)</em></p>
</dd>
</dl>
</ignore_js_op>
</div>", 2),

            ("仅文本的 pattl（无附件链接）", @"<div class=""pattl"">
<p>这是一些文本内容</p>
<p>但没有任何附件</p>
</div>", 0),

            ("带隐藏菜单的 pattl", @"<div class=""pattl"">
<ignore_js_op>
<dl class=""tattl attm"">
<dd>
<a href=""https://bbs.pcbeta.com/forum.php?mod=attachment&aid=125"">screenshot.png</a>
<div class=""tip"" style=""display: none""><p>2026-3-27 上传</p></div>
</dd>
</dl>
</ignore_js_op>
</div>", 1)
        };

        foreach (var (description, html, expectedCount) in testCases)
        {
            Debug.WriteLine($"【测试】{description}");
            Debug.WriteLine($"预期附件数：{expectedCount}");

            try
            {
                var service = new XmlParsingService();
                var xmlResponse = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<root><![CDATA[{html}]]></root>";

                var threadContent = service.ParseThreadContent(xmlResponse);
                var attachmentCount = threadContent.ContentElements?
                    .Count(e => e.Type == ContentElementType.Attachment) ?? 0;

                if (attachmentCount == expectedCount)
                {
                    Debug.WriteLine($" PASS：解析结果与预期匹配（{attachmentCount} 个附件）\n");
                }
                else
                {
                    Debug.WriteLine($"❌ FAIL：预期 {expectedCount} 个，实际 {attachmentCount} 个\n");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ ERROR：{ex.Message}\n");
            }
        }

        Debug.WriteLine("========== 条件性解析测试完成 ==========\n");
    }
}
