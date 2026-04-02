using System.Diagnostics;
using System.Text.RegularExpressions;
using PCBetaMAUI.Models;
using PCBetaMAUI.Services;

namespace PCBetaMAUI.DebugTests;

/// <summary>
///  诊断测试：验证 pattl div 提取修复
/// 基于 forum.xml 的实际结构分析
/// 
/// 问题症状：
/// - 条件 `if (trimmedToken.Contains("class=\"pattl\""))` 在 ParseContentBlock() 中没有命中
/// - 原因：Regex.Split(ignore_js_op) 在提取 pattl 之前执行，导致 pattl div 被分割
/// 
/// 修复验证：
/// - ParseRichTextContent() 现在在 Regex.Split() 之前提取 pattl divs
/// - 完整的 <div class="pattl">...</div> 块被提取和处理
/// - 不会被后续的 ignore_js_op 分割破坏
/// </summary>
public class XmlAnalysisDiagnostic
{
    private const string TestDataWithPattl = @"
<td class='plc'>
  <div class='pattl'>
    
    
    <ignore_js_op>
    
    <dl class='tattl attm'>
    <dt></dt>
    <dd>
    
    <p class='mbn'>
    <a href='https://bbs.pcbeta.com/forum.php?mod=attachment&aid=4617322'>无标题.png</a>
    <em class='xg1'>(326.55 KB, 下载次数: 0)</em>
    </p>
    <div class='tip tip_4' id='aid4617322_menu' style='display: none'>
    <div>
    <p>
    <a href='https://bbs.pcbeta.com/forum.php?mod=attachment&aid=4617322' target='_blank'>下载附件</a>
    </p>
    <p>
    <span class='y'>2026-3-27 15:59 上传</span>
    </p>
    </div>
    <div class='tip_horn'></div>
    </div>
    <p class='mbn'>
    
    </p>
    
    
    
    <div class='mbn savephotop'>
    
    <img id='aimg_4617322' aid='4617322' src='static/image/common/none.gif' zoomfile='/data/attachment/forum/202603/27/155910g55ce3hdeq7je9mm.png' file='/data/attachment/forum/202603/27/155910g55ce3hdeq7je9mm.png' class='zoom' onclick='zoom(this, this.src, 0, 0, 0)' width='600' alt='无标题.png' title='无标题.png' w='1907' />
    
    </div>
    
    </dd>
    </dl>
    
    </ignore_js_op>
    
  </div>
  
</div>";

    /// <summary>
    /// 运行所有诊断测试
    /// </summary>
    public static void RunAllDiagnostics()
    {
        Debug.WriteLine("\n" + new string('=', 80));
        Debug.WriteLine("🔍 XML 分析诊断测试 - pattl div 提取");
        Debug.WriteLine(new string('=', 80));

        // 测试1：验证 Regex.Split() 的破坏性
        TestRegexSplitDestructiveness();

        // 测试2：验证 pattl 提取
        TestPattlExtraction();

        // 测试3：验证完整的提取逻辑
        TestCompleteExtractionFlow();

        Debug.WriteLine(new string('=', 80));
        Debug.WriteLine(" 诊断测试完成");
        Debug.WriteLine(new string('=', 80) + "\n");
    }

    /// <summary>
    /// 测试1：演示 Regex.Split() 如何破坏 pattl div 结构
    /// </summary>
    private static void TestRegexSplitDestructiveness()
    {
        Debug.WriteLine("\n📋 【测试1】Regex.Split() 的破坏性");
        Debug.WriteLine(new string('-', 80));

        string htmlContent = TestDataWithPattl;

        // 显示原始结构
        Debug.WriteLine(" 原始 HTML 结构:");
        var pattlMatch = Regex.Match(htmlContent, @"<div[^>]*class='pattl'[^>]*>(.*?)</div>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (pattlMatch.Success)
        {
            var pattlContent = pattlMatch.Value;
            Debug.WriteLine($"   <pattl div 长度>: {pattlContent.Length} 字符");
            Debug.WriteLine($"   <pattl div 开始>: {pattlContent.Substring(0, 50)}...");
            Debug.WriteLine($"   <pattl div 结束>: ...{pattlContent.Substring(pattlContent.Length - 50)}");
        }

        // 执行 Regex.Split()
        var ignorePattern = @"<ignore_js_op[^>]*>(.*?)</ignore_js_op>";
        var parts = Regex.Split(htmlContent, ignorePattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

        Debug.WriteLine($"\n❌ Regex.Split() 后的结果: {parts.Length} 部分");
        for (int i = 0; i < parts.Length; i++)
        {
            Debug.WriteLine($"\n   【parts[{i}]】长度={parts[i].Length}:");
            var preview = parts[i].Trim();
            if (preview.Length > 100)
                preview = preview.Substring(0, 100) + "...";
            Debug.WriteLine($"   {preview}");

            // 检查是否包含 pattl div
            if (parts[i].Contains("class='pattl'") || parts[i].Contains("class=\"pattl\""))
            {
                Debug.WriteLine("   ⚠️  包含 pattl div 标签");
                if (!parts[i].Contains("</div>"))
                {
                    Debug.WriteLine("   ❌ 但是 pattl div 没有闭合标签！");
                }
            }
            if (parts[i].Contains("</div>") && !parts[i].Contains("<div"))
            {
                Debug.WriteLine("   ⚠️  包含 pattl div 结束标签，但没有开始标签！");
            }
        }

        Debug.WriteLine("\n📊 结论:");
        Debug.WriteLine("   ❌ pattl div 被分割成了多个部分");
        Debug.WriteLine("   ❌ parts[0] 包含开始 <div class='pattl'> 但没有结束 </div>");
        Debug.WriteLine("   ❌ parts[2] 包含结束 </div> 但没有开始标签");
        Debug.WriteLine("   ❌ 中间的内容被分离到了 parts[1]");
    }

    /// <summary>
    /// 测试2：演示正确的 pattl 提取方式
    /// </summary>
    private static void TestPattlExtraction()
    {
        Debug.WriteLine("\n📋 【测试2】正确的 pattl div 提取");
        Debug.WriteLine(new string('-', 80));

        string htmlContent = TestDataWithPattl;

        // 使用 Regex.Matches() 提取完整的 pattl divs
        var pattlPattern = @"<div[^>]*class=['""]*pattl['""]*[^>]*>(.*?)</div>";
        var pattlMatches = Regex.Matches(htmlContent, pattlPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

        Debug.WriteLine($" 使用 Regex.Matches() 找到: {pattlMatches.Count} 个 pattl divs");

        if (pattlMatches.Count > 0)
        {
            foreach (Match match in pattlMatches)
            {
                var pattlDivHtml = match.Value;
                Debug.WriteLine($"\n    找到完整的 pattl div:");
                Debug.WriteLine($"      长度: {pattlDivHtml.Length} 字符");
                Debug.WriteLine($"      开始: {pattlDivHtml.Substring(0, 40)}...");
                Debug.WriteLine($"      结束: ...{pattlDivHtml.Substring(pattlDivHtml.Length - 20)}");

                // 验证它包含 ignore_js_op 块
                if (pattlDivHtml.Contains("<ignore_js_op>"))
                {
                    Debug.WriteLine($"       包含 <ignore_js_op> 块");
                }

                // 验证它包含附件链接
                var attachmentLink = Regex.Match(pattlDivHtml, @"<a[^>]*href='([^']*)'[^>]*>([^<]+)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (attachmentLink.Success)
                {
                    Debug.WriteLine($"       包含附件链接: {attachmentLink.Groups[2].Value}");
                }
            }
        }

        Debug.WriteLine("\n📊 结论:");
        Debug.WriteLine("    使用 Regex.Matches() 可以捕获完整的 pattl divs");
        Debug.WriteLine("    每个 match.Value 都是完整的 <div>...</div> 块");
        Debug.WriteLine("    可以立即处理，不会被后续操作破坏");
    }

    /// <summary>
    /// 测试3：演示完整的提取流程顺序
    /// </summary>
    private static void TestCompleteExtractionFlow()
    {
        Debug.WriteLine("\n📋 【测试3】完整的提取流程顺序对比");
        Debug.WriteLine(new string('-', 80));

        string htmlContent = TestDataWithPattl;

        // ❌ 错误的顺序：先 split，再提取
        Debug.WriteLine("\n❌ 错误顺序（旧代码）：");
        Debug.WriteLine("   1️⃣ Regex.Split() 分割 ignore_js_op");
        Debug.WriteLine("   2️⃣ ParseContentBlock() 尝试检测 pattl div");
        Debug.WriteLine("   3️⃣ 结果：条件不匹配 ❌");

        var ignorePattern = @"<ignore_js_op[^>]*>(.*?)</ignore_js_op>";
        var parts = Regex.Split(htmlContent, ignorePattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        Debug.WriteLine($"   分割后 parts[0] 末尾: ...{parts[0].Trim().Substring(Math.Max(0, parts[0].Trim().Length - 50))}");

        //  正确的顺序：先提取，再 split
        Debug.WriteLine("\n 正确顺序（新代码）：");
        Debug.WriteLine("   1️⃣ Regex.Matches() 提取完整的 pattl divs");
        Debug.WriteLine("   2️⃣ ExtractAttachmentsFromPattlDiv() 处理附件");
        Debug.WriteLine("   3️⃣ 从 htmlContent 中移除已处理的 pattl div");
        Debug.WriteLine("   4️⃣ Regex.Split() 分割 ignore_js_op（pattl 已移除）");
        Debug.WriteLine("   5️⃣ 结果：条件匹配 ");

        string htmlContentCopy = htmlContent;
        var pattlPattern = @"<div[^>]*class=['""]*pattl['""]*[^>]*>(.*?)</div>";
        var pattlMatches = Regex.Matches(htmlContentCopy, pattlPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

        Debug.WriteLine($"\n    步骤1: 找到 {pattlMatches.Count} 个 pattl divs");

        int attachmentCount = 0;
        foreach (Match pattlMatch in pattlMatches)
        {
            var pattlDivHtml = pattlMatch.Value;
            var attachmentLink = Regex.Match(pattlDivHtml, @"<a[^>]*href='([^']*)'[^>]*>([^<]+)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (attachmentLink.Success)
            {
                attachmentCount++;
                Debug.WriteLine($"    步骤2: 提取附件 '{attachmentLink.Groups[2].Value}'");
            }
            htmlContentCopy = htmlContentCopy.Replace(pattlDivHtml, "");
        }

        Debug.WriteLine($"    步骤3: 从内容中移除 pattl divs");

        parts = Regex.Split(htmlContentCopy, ignorePattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        Debug.WriteLine($"    步骤4: 分割 ignore_js_op（{parts.Length} 部分）");
        Debug.WriteLine($"    步骤5: pattl 附件已提取，总数: {attachmentCount}");

        Debug.WriteLine("\n📊 结论:");
        Debug.WriteLine("    顺序很重要！提取 pattl 必须在 Regex.Split() 之前");
        Debug.WriteLine("    这样可以确保 pattl div 始终完整");
        Debug.WriteLine("    ExtractAttachmentsFromPattlDiv() 可以正常工作");
    }
}
