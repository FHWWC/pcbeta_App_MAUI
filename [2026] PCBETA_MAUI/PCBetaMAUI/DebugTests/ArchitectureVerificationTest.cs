using PCBetaMAUI.Services;
using System.Diagnostics;
using System.Xml.Linq;

namespace PCBetaMAUI.DebugTests;

/// <summary>
/// 验证修复后的 pattl 附件提取是否在正确的架构层级工作
/// 确认 pattl 容器现在在 ExtractThreadContentFromHtml() 中被正确处理
/// </summary>
public class ArchitectureVerificationTest
{
    public static void VerifyPattlExtractionArchitecture()
    {
        Debug.WriteLine("\n========== 验证 pattl 提取架构修复 ==========\n");

        // 完整的帖子 HTML 结构，包含 postmessage 和 pattl
        string completePostHtml = @"
<div id=""post_1234"" class=""mlt"">
    <table cellpadding=""0"" cellspacing=""0"">
        <tbody>
            <tr>
                <td class=""t_f"" id=""postmessage_1234"">
                    <br />
                    <br />
                    测试附件，请勿下载。<br />
                    <ignore_js_op>
                    <img id=""aimg_4616922"" aid=""4616922"" src=""static/image/common/none.gif"" file=""/data/attachment/forum/202603/25/112936wohdl0enesp6i06l.png"" class=""zoom"" />
                    <div class=""tip tip_4 aimg_tip"" id=""aimg_4616922_menu"" style=""position: absolute; display: none"">
                        <div class=""xs0"">
                            <p><strong>common_498_icon.png</strong> <em class=""xg1"">(12.87 KB, 下载次数: 1)</em></p>
                            <p><a href=""https://bbs.pcbeta.com/forum.php?mod=attachment&amp;aid=NDYxNjkyMnwxOGRhNjE0ZnwxNzc0NzIwNTQwfDQ4OTAzMzd8MjA2NzMwMA%3D%3D"">下载附件</a></p>
                            <p class=""xg1 y"">2026-3-25 11:29 上传</p>
                        </div>
                    </div>
                    </ignore_js_op>
                    <br />
                    <br />
                </td>
            </tr>
        </tbody>
    </table>
    
    <!-- 版主信息 (这里会有版主操作标记) -->
    <div class=""mbn"">版主信息区域</div>
    
    <!-- pattl 容器 - 这是新修复的目标，应该在这里被正确提取 -->
    <div class=""pattl"">
        <ignore_js_op>
        <dl class=""tattl attm"">
        <dt></dt>
        <dd>
        <p class=""mbn"">
        <a href=""https://bbs.pcbeta.com/forum.php?mod=attachment&amp;aid=test1"" onmouseover=""showMenu({'ctrlid':this.id,'pos':'12'})"" id=""aid4617322"" class=""xw1"" target=""_blank"">附件文件1.pdf</a>
        <em class=""xg1"">(326.55 KB, 下载次数: 5)</em>
        </p>
        <div class=""tip tip_4"" id=""aid4617322_menu"" style=""display: none"">
        <div class=""tip_c"">
        <p class=""y"">2026-3-27 15:59 上传</p>
        </div>
        </div>
        </dd>
        </dl>
        </ignore_js_op>
    </div>
</div>";

        var parser = new XmlParsingService();

        // 调用 ParseThreadContent - 这应该会在 ExtractThreadContentFromHtml() 中进行 pattl 提取
        var result = parser.ParseThreadContent(completePostHtml);

        Debug.WriteLine($"\n 解析结果：");
        Debug.WriteLine($"  - 标题: {result.Title}");
        Debug.WriteLine($"  - 作者: {result.Author}");
        Debug.WriteLine($"  - 内容元素数: {result.ContentElements.Count}");
        
        Debug.WriteLine($"\n📋 内容元素列表：");
        foreach (var element in result.ContentElements)
        {
            Debug.WriteLine($"  - 类型: {element.Type}");
            if (!string.IsNullOrEmpty(element.FileName))
                Debug.WriteLine($"    文件名: {element.FileName}");
            if (!string.IsNullOrEmpty(element.Text))
                Debug.WriteLine($"    文本: {element.Text}");
        }

        // 关键验证：检查是否包含 pattl 中的附件
        var pattlAttachments = result.ContentElements.Where(e => e.Type == PCBetaMAUI.Models.ContentElementType.Attachment).ToList();
        Debug.WriteLine($"\n🎯 关键验证 - 附件数: {pattlAttachments.Count}");
        if (pattlAttachments.Count > 0)
        {
            Debug.WriteLine($" 成功！pattl 容器中的附件已被正确提取");
            foreach (var att in pattlAttachments)
            {
                Debug.WriteLine($"   - {att.FileName} ({att.FileSize})");
            }
        }
        else
        {
            Debug.WriteLine($"⚠️ 未找到附件 - 可能 pattl 提取仍未工作");
        }

        Debug.WriteLine("\n========== 验证完成 ==========\n");
    }
}
