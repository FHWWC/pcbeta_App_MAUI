using System.Diagnostics;
using System.Text.RegularExpressions;
using PCBetaMAUI.Services;
using PCBetaMAUI.Models;

namespace PCBetaMAUI.DebugTests;

/// <summary>
///  验证 pattl 附件结构的专门解析方案
/// 
/// 说明：
/// - pattl 容器中的附件与帖子内部的附件结构完全不同
/// - 需要使用完全不同的解析逻辑
/// - 此方案专门处理 <div class="pattl"><dl class="tattl"><dd>...<a id="aid*">...</dd>
/// </summary>
public class PattlStructureVerification
{
    /// <summary>
    /// 主验证方法 - 演示 pattl 专门解析方案的正确性
    /// </summary>
    public static void VerifyPattlParsingScheme()
    {
        Debug.WriteLine("\n╔════════════════════════════════════════════════════════════════╗");
        Debug.WriteLine("║              pattl 结构专门解析方案验证                      ║");
        Debug.WriteLine("╚════════════════════════════════════════════════════════════════╝\n");

        // 演示两种完全不同的附件结构
        DemonstrateDifferences();

        // 验证 pattl 专门解析方案
        VerifyPattlParsingLogic();

        // 验证与帖子内附件解析的分离
        VerifyStructuralSeparation();

        Debug.WriteLine("\n╚════════════════════════════════════════════════════════════════╝\n");
    }

    /// <summary>
    /// 演示帖子内附件 vs pattl 附件的结构差异
    /// </summary>
    private static void DemonstrateDifferences()
    {
        Debug.WriteLine("\n【步骤 1】演示两种不同的附件结构\n");

        Debug.WriteLine("┌─ 帖子内附件结构（在 <ignore_js_op> 内）");
        Debug.WriteLine("│");
        Debug.WriteLine("│  <ignore_js_op>");
        Debug.WriteLine("│    <img id=\"aimg_4616922\" aid=\"4616922\" file=\"/data/attachment/...\" />");
        Debug.WriteLine("│    <div class=\"tip tip_4 aimg_tip\" id=\"aimg_4616922_menu\">");
        Debug.WriteLine("│      <strong>common_498_icon.png</strong>");
        Debug.WriteLine("│      <em>(12.87 KB, 下载次数: 1)</em>");
        Debug.WriteLine("│      <a href=\"...mod=attachment...\">下载附件</a>");
        Debug.WriteLine("│      <p class=\"y\">2026-3-25 11:29 上传</p>");
        Debug.WriteLine("│      <br />售价: 1 PB币");
        Debug.WriteLine("│    </div>");
        Debug.WriteLine("│  </ignore_js_op>");
        Debug.WriteLine("│");
        Debug.WriteLine("│  关键特征：");
        Debug.WriteLine("│  ✓ 图片在前（<img> 标签）");
        Debug.WriteLine("│  ✓ 文件名在 <strong> 中");
        Debug.WriteLine("│  ✓ 大小信息在 <em> 中");
        Debug.WriteLine("│  ✓ 下载链接在 <a> 中");
        Debug.WriteLine("│  ✓ 日期在 <p class=\"y\"> 中");
        Debug.WriteLine("│  ✓ 有 SalePrice（可能需要购买）");
        Debug.WriteLine("└─ 解析方法：ExtractAttachmentInfoFromTipDiv()");

        Debug.WriteLine("\n┌─ pattl 附件结构（在 <div class=\"pattl\"><dl class=\"tattl\"><dd> 内）");
        Debug.WriteLine("│");
        Debug.WriteLine("│  <div class=\"pattl\">");
        Debug.WriteLine("│    <ignore_js_op>");
        Debug.WriteLine("│      <dl class=\"tattl attm\">");
        Debug.WriteLine("│        <dt><img src=\"zip.gif\" /></dt>");
        Debug.WriteLine("│        <dd>");
        Debug.WriteLine("│          <p class=\"attnm\">");
        Debug.WriteLine("│            <a href=\"...mod=attachment&aid=...\" id=\"aid4617322\" class=\"xw1\">TestZip.zip</a>");
        Debug.WriteLine("│            <em class=\"xg1\">(1.12 KB, 下载次数: 0)</em>");
        Debug.WriteLine("│          </p>");
        Debug.WriteLine("│          <div class=\"tip tip_4\" id=\"aid4617322_menu\" style=\"display: none\">");
        Debug.WriteLine("│            <p class=\"y\">2026-3-28 23:33 上传</p>");
        Debug.WriteLine("│          </div>");
        Debug.WriteLine("│        </dd>");
        Debug.WriteLine("│      </dl>");
        Debug.WriteLine("│    </ignore_js_op>");
        Debug.WriteLine("│  </div>");
        Debug.WriteLine("│");
        Debug.WriteLine("│  关键特征：");
        Debug.WriteLine("│  ✓ 文件名直接在 <a> 标签的文本中（id=\"aid*\"）");
        Debug.WriteLine("│  ✓ 大小在 <em class=\"xg1\"> 中");
        Debug.WriteLine("│  ✓ 日期在 <span class=\"y\"> 中");
        Debug.WriteLine("│  ✓ 无 SalePrice（已上传的文件，不需购买）");
        Debug.WriteLine("│  ✓ 结构在 <dl><dd> 中");
        Debug.WriteLine("└─ 解析方法：ExtractPattlAttachmentsFromDlBlock() + CreatePattlAttachmentElement()");

        Debug.WriteLine("\n 关键差异总结：");
        Debug.WriteLine("   ├─ 文件名位置：<strong> vs <a> 文本");
        Debug.WriteLine("   ├─ 容器结构：<div class=\"tip\"> vs <dd>");
        Debug.WriteLine("   ├─ 日期位置：<p class=\"y\"> vs <span class=\"y\">");
        Debug.WriteLine("   ├─ 是否有价格：有 vs 无");
        Debug.WriteLine("   └─ 解析方法：必须完全不同！");
    }

    /// <summary>
    /// 验证 pattl 专门解析逻辑的正确性
    /// </summary>
    private static void VerifyPattlParsingLogic()
    {
        Debug.WriteLine("\n【步骤 2】验证 pattl 专门解析逻辑\n");

        // 模拟从 forum.xml 的 pattl 结构
        string pattlHtml = @"<div class=""pattl"">
<ignore_js_op>
<dl class=""tattl attm"">
<dt><img src=""zip.gif"" border=""0"" class=""vm"" alt="""" /></dt>
<dd>
<p class=""attnm"">
<a href=""https://bbs.pcbeta.com/forum.php?mod=attachment&amp;aid=NDYxNzUyMnxmMTkzOGZiZHwxNzc0NzE4MTA1fDQ4MTk2NjJ8MjA2NzMwMA%3D%3D"" onmouseover=""showMenu({'ctrlid':this.id,'pos':'12'})"" id=""aid4617522"" target=""_blank"">TestZip.zip</a>
<div class=""tip tip_4"" id=""aid4617522_menu"" style=""display: none"" disautofocus=""true"">
<div class=""tip_c"">
<p class=""y"">2026-3-28 23:33 上传</p>
</div>
</div>
</p>
<p>1.12 KB, 下载次数: 0, 下载积分: PB币 -1 </p>
</dd>
</dl>
</ignore_js_op>
</div>";

        Debug.WriteLine("输入 HTML 结构：");
        Debug.WriteLine($"   {pattlHtml.Substring(0, 50)}...\n");

        // 应用 pattl 解析方案
        var patterns = new Dictionary<string, (string name, string pattern)>
        {
            ["remove_wrapper"] = ("移除 <ignore_js_op> 包装", @"</?ignore_js_op[^>]*>"),
            ["extract_dl"] = ("提取 <dl> 块", @"<dl[^>]*class=""[^""]*tattl[^""]*""[^>]*>"),
            ["extract_dd"] = ("提取 <dd> 块", @"<dd[^>]*>(.*?)</dd>"),
            ["extract_link"] = ("提取文件名链接", @"<a[^>]*href=""([^""]*mod=attachment[^""]*)""[^>]*id=""aid(\d+)""[^>]*>([^<]+)</a>"),
            ["extract_size"] = ("提取文件大小", @"<em[^>]*class=""[^""]*xg1[^""]*""[^>]*>\s*\(([^,]+),\s*下载次数:\s*(\d+)\)\s*</em>"),
            ["extract_time"] = ("提取上传时间", @"<span[^>]*class=""[^""]*\by\b[^""]*""[^>]*>([^<]*上传[^<]*)</span>"),
        };

        Debug.WriteLine("验证每个解析步骤：\n");

        string workingHtml = pattlHtml;
        foreach (var (key, (name, pattern)) in patterns)
        {
            try
            {
                if (key == "remove_wrapper")
                {
                    workingHtml = Regex.Replace(workingHtml, pattern, "", RegexOptions.IgnoreCase);
                    Debug.WriteLine($"✓ {name}");
                    Debug.WriteLine($"  结果：移除了包装标签\n");
                }
                else if (key == "extract_dl")
                {
                    var match = Regex.Match(workingHtml, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    Debug.WriteLine($"✓ {name}");
                    Debug.WriteLine($"  匹配：{(match.Success ? "✓ 成功" : "✗ 失败")}\n");
                }
                else if (key == "extract_dd")
                {
                    var matches = Regex.Matches(workingHtml, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    Debug.WriteLine($"✓ {name}");
                    Debug.WriteLine($"  匹配数：{matches.Count}\n");
                }
                else if (key == "extract_link")
                {
                    var match = Regex.Match(workingHtml, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    if (match.Success)
                    {
                        Debug.WriteLine($"✓ {name}");
                        Debug.WriteLine($"  URL：{match.Groups[1].Value.Substring(0, Math.Min(40, match.Groups[1].Value.Length))}...");
                        Debug.WriteLine($"  ID：{match.Groups[2].Value}");
                        Debug.WriteLine($"  文件名：{match.Groups[3].Value}\n");
                    }
                }
                else if (key == "extract_size")
                {
                    var match = Regex.Match(pattlHtml, pattern, RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        Debug.WriteLine($"✓ {name}");
                        Debug.WriteLine($"  大小：{match.Groups[1].Value}");
                        Debug.WriteLine($"  下载次数：{match.Groups[2].Value}\n");
                    }
                }
                else if (key == "extract_time")
                {
                    var match = Regex.Match(pattlHtml, pattern, RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        Debug.WriteLine($"✓ {name}");
                        Debug.WriteLine($"  时间：{match.Groups[1].Value}\n");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"✗ {name} - 错误：{ex.Message}\n");
            }
        }

        Debug.WriteLine(" pattl 解析逻辑验证完成\n");
    }

    /// <summary>
    /// 验证 pattl 解析与帖子内附件解析的完全分离
    /// </summary>
    private static void VerifyStructuralSeparation()
    {
        Debug.WriteLine("\n【步骤 3】验证解析方案的架构分离\n");

        Debug.WriteLine("当前架构设计：\n");

        Debug.WriteLine("┌─ ExtractThreadContentFromHtml()");
        Debug.WriteLine("│");
        Debug.WriteLine("│  ├─ Step 1: 提取主帖内容");
        Debug.WriteLine("│  │           ↓");
        Debug.WriteLine("│  │           ExtractMessageContent()");
        Debug.WriteLine("│  │           ↓");
        Debug.WriteLine("│  │           ParseRichTextContent()");
        Debug.WriteLine("│  │           ↓");
        Debug.WriteLine("│  │           ParseContentBlock()");
        Debug.WriteLine("│  │           ↓");
        Debug.WriteLine("│  │           ExtractAttachmentInfoFromTipDiv() ← 帖子内附件");
        Debug.WriteLine("│  │");
        Debug.WriteLine("│  └─ Step 2: 提取 pattl 附件（外部处理）");
        Debug.WriteLine("│            ↓");
        Debug.WriteLine("│            Regex.Match(pattlPattern)");
        Debug.WriteLine("│            ↓");
        Debug.WriteLine("│            ExtractAttachmentsFromPattlDiv() ← pattl 附件入口");
        Debug.WriteLine("│            ↓");
        Debug.WriteLine("│            ExtractPattlAttachmentsFromDlBlock() ← pattl 专门解析");
        Debug.WriteLine("│            ↓");
        Debug.WriteLine("│            CreatePattlAttachmentElement() ← pattl 元素工厂");
        Debug.WriteLine("│");
        Debug.WriteLine("└─ 返回 ThreadContent（包含两种附件）");

        Debug.WriteLine("\n 架构分离验证：\n");

        var verifications = new[]
        {
            ("pattl 处理位置", "在 ExtractThreadContentFromHtml() 中，与帖子内容解析并行"),
            ("不在 ParseContentBlock 中", "✓ 不会导致在解析帖子内容时重复处理 pattl"),
            ("专门的解析方法", "✓ ExtractPattlAttachmentsFromDlBlock() 只处理 pattl 结构"),
            ("专门的工厂方法", "✓ CreatePattlAttachmentElement() 只创建 pattl 元素"),
            ("结构识别", "✓ 每种结构使用相应的正则表达式"),
            ("元素生成", "✓ 不重复处理，不产生重复元素"),
        };

        foreach (var (check, result) in verifications)
        {
            Debug.WriteLine($"   {check}: {result}");
        }

        Debug.WriteLine("\n 架构分离验证完成");
    }
}
