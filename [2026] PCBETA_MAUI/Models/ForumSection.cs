using System.Collections.Generic;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Maui.Controls;

namespace PCBetaMAUI.Models;

/// <summary>
/// 论坛分类组（例如：华为论坛、Windows论坛等）
/// 包含分类名称和该分类下的所有板块
/// </summary>
public partial class ForumCategory : ObservableObject
{
    [JsonPropertyName("categoryId")]
    public string? CategoryId { get; set; }

    [JsonPropertyName("categoryName")]
    public string? CategoryName { get; set; }

    [JsonPropertyName("sections")]
    public List<ForumSection> Sections { get; set; } = new();

    /// <summary>
    /// 分类是否展开（非序列化属性，用于 UI 展开/收回）
    /// </summary>
    [JsonIgnore]
    [ObservableProperty]
    private bool isExpanded = true;
}

/// <summary>
/// 论坛板块（具体的讨论版块，例如：Windows 11论坛、Windows 10论坛等）
/// ThreadCount �?PostCount �?string 类型以保�?�?等格式化显示
/// TodayNewPosts 存储今日新帖�?/// </summary>
public class ForumSection
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("threadCount")]
    public string? ThreadCount { get; set; }

    [JsonPropertyName("postCount")]
    public string? PostCount { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("logoUrl")]
    public string? LogoUrl { get; set; }

    [JsonIgnore]
    public ImageSource? LogoSource { get; set; }

    [JsonPropertyName("todayNewPosts")]
    public string? TodayNewPosts { get; set; }
    [JsonPropertyName("lastReply")]
    public string? LastReply { get; set; }
}

public class ThreadInfo
{
    public string Id { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public DateTime PostTime { get; set; }
    public int Replies { get; set; }
    public int Views { get; set; }
    public string Category { get; set; } = string.Empty;

    public bool IsReply { get; set; }

    public List<string> CommentContents { get; set; } = new();

    /// <summary>
    /// 是否为置顶帖（true = 置顶帖，false = 普通帖子）
    /// </summary>
    public bool IsSticky { get; set; } = false;

    /// <summary>
    /// 新增：帖子图标列表（如锁、投票、悬赏、辩论等），按解析顺序保留。
    /// 每个图标使用字符串表示（可为 emoji 或文本），支持多个图标同时存在。
    /// </summary>
    public List<string> Icons { get; set; } = new();

    /// <summary>
    /// 新增：阅读权限信息（示例："[阅读权限 10]"），默认空字符串表示无特殊阅读权限。
    /// 从帖子 HTML 中提取 <span class=\"xw1\">数字</span> 并封装为上例格式。
    /// </summary>
    public string ReadPrem { get; set; } = string.Empty;

    /// <summary>
    /// 新增：帖子印章图片URL（例如：static/image/stamp/002.small.gif），若为相对路径则拼接 ApiService.BaseUrl
    /// 默认值为空字符串
    /// </summary>
    public string StampUrl { get; set; } = string.Empty;

    [JsonIgnore]
    public ImageSource? StampSource { get; set; }

    /// <summary>
    /// 新增：回帖奖励信息（例如："还剩564PB币"），为空字符串表示无回帖奖励
    /// </summary>
    public string Replycredit { get; set; } = string.Empty;
}

public class SearchThreadInfo
{
    public string Id { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string ForumName { get; set; } = string.Empty;
    public string PreviewContent { get; set; } = string.Empty;
    public DateTime PostTime { get; set; }
    public int Replies { get; set; }
    public int Views { get; set; }
}

// 内容元素类型
public enum ContentElementType
{
    /// <summary>普通文�?/summary>
    Text,
    /// <summary>加粗文本</summary>
    Bold,
    /// <summary>斜体文本</summary>
    Italic,
    /// <summary>超链�?/summary>
    Link,
    /// <summary>图片</summary>
    Image,
    /// <summary>附件</summary>
    Attachment,
    /// <summary>表情</summary>
    Emoji,
    /// <summary>代码�?/summary>
    Code,
    /// <summary>引用�?/summary>
    Quote,
    /// <summary>表格</summary>
    Table,
    /// <summary>表格�?- 包含一行的所有单元格</summary>
    TableRow,
    /// <summary>表格单元�?- 包含单元格内的所有元素，支持 LineBreak 换行</summary>
    TableCell,
    /// <summary>换行 - 仅在 TableCell 内有效，用于分隔单元格内的内�?/summary>
    LineBreak,
    /// <summary>分隔�?/summary>
    Separator
}

/// <summary>
/// 内容元素 - 用于富文本表�?/// </summary>
public class ContentElement
{
    /// <summary>元素类型</summary>
    public ContentElementType Type { get; set; }

    /// <summary>文本内容（适用�?Text, Bold, Italic, Link, Code, Quote�?/summary>
    public string? Text { get; set; }

    /// <summary>链接 URL（适用�?Link, Image, Attachment�?/summary>
    public string? Url { get; set; }

    /// <summary>链接标题（适用�?Link�?/summary>
    public string? Title { get; set; }

    /// <summary>文件名（适用�?Attachment�?/summary>
    public string? FileName { get; set; }

    /// <summary>文件大小（适用�?Attachment�?/summary>
    public string? FileSize { get; set; }

    /// <summary>上传时间（适用�?Attachment�? 格式�?"2026-3-18 15:21"</summary>
    public string? UploadTime { get; set; }

    /// <summary>附件售价（适用�?Attachment�? 如果为空�?表示免费，否则表示收�?/summary>
    public int? SalePrice { get; set; }

    /// <summary>附件ID（适用�?Attachment�? 用于购买和下�?/summary>
    public string? AttachmentId { get; set; }

    /// <summary>下载次数（适用�?Attachment�?/summary>
    public int? DownloadCount { get; set; }

    /// <summary>图片宽度（适用�?Image�?/summary>
    public int ImageWidth { get; set; }

    /// <summary>图片高度（适用�?Image�?/summary>
    public int ImageHeight { get; set; }

    /// <summary>表情 ID（适用�?Emoji�?/summary>
    public string? EmojiId { get; set; }

    /// <summary>子元素列表（适用�?Table�?/summary>
    public List<ContentElement>? Children { get; set; }

    /// <summary>表格列数（适用�?Table 类型�?/summary>
    public int ColumnCount { get; set; } = 0;

    /// <summary>属性字典（用于存储其他自定义属性）</summary>
    public Dictionary<string, string>? Attributes { get; set; }

    /// <summary>是否为横向排列（用于标记来自 ignore_js_op 标签内的元素�?/summary>
    public bool IsHorizontal { get; set; } = false;

    /// <summary>横向分组 ID - 用于将来自同一 ignore_js_op 块的文本元素分组在一�?/summary>
    /// <remarks>�?null 表示不分组，相同非空值的元素应在同一行横向显�?/remarks>
    public string? HorizontalGroupId { get; set; }
}

/// <summary>
/// ��ҳ��Ϣģ�� - ���ڴ洢�� HTML �������ķ�ҳ����
/// </summary>
public class PaginationInfo
{
    /// <summary>
    /// ��ǰҳ��
    /// </summary>
    public int CurrentPage { get; set; } = 1;

    /// <summary>
    /// ��ҳ��
    /// </summary>
    public int TotalPages { get; set; } = 1;

}

public class PollInfo
{
    public string FormHash { get; set; } = string.Empty;
    public string SubmitUrl { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Notice { get; set; } = string.Empty;
    public string EndTimeText { get; set; } = string.Empty;
    public bool IsMultiple { get; set; }
    public int MaxChoices { get; set; }
    public bool CanVote { get; set; }
    public List<PollOption> Options { get; set; } = new();
}

public class PollOption
{
    public string Id { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public int VoteCount { get; set; }
    public double Percentage { get; set; }
    public bool IsSelected { get; set; }
}

/// <summary>
/// 改进的帖子内容模�?/// </summary>
public class ThreadContent
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string PostTime { get; set; } = string.Empty;
    public string OtherIfm { get; set; } = string.Empty;

    /// <summary>
    /// 原始 HTML 内容（保留备用）
    /// </summary>
    public string RawHtmlContent { get; set; } = string.Empty;

    /// <summary>
    /// 纯文本内容（简化版�?    /// </summary>
    public string PlainTextContent { get; set; } = string.Empty;

    /// <summary>
    /// 富文本内容元素列�?- 支持多种类型的内�?    /// </summary>
    public List<ContentElement> ContentElements { get; set; } = new();

    public int Replies { get; set; }

    /// <summary>
    /// �?新增：编辑状态信�?    /// 格式：本帖最后由 用户�?�?日期 编辑
    /// �?null 表示帖子未被编辑
    /// </summary>
    public string? EditStatus { get; set; }

    /// <summary>
    /// �?新增：审核通过信息
    /// 格式：本主题�?审核员名�?�?日期 审核通过
    /// �?null 表示无审核通过信息
    /// </summary>
    public string? ModerationInfo { get; set; }

    /// <summary>
    /// �?新增：帖子的评论列表
    /// 可选字�?- 仅当帖子有评论时存在
    /// </summary>
    public List<CommentInfo>? Comments { get; set; }

    /// <summary>
    /// �?新增：帖子的评分汇总信�?    /// 可选字�?- 仅当帖子有评分时存在
    /// </summary>
    public RatingSummary? RatingSummary { get; set; }

    /// <summary>
    /// 当前帖子的完整URL（用于下载附件时作为Referer�?    /// </summary>
    public string CurrentThreadUrl { get; set; } = string.Empty;

    /// <summary>
    /// 楼主发帖的评分URL（用于给楼主评分）
    /// </summary>
    public string? RatingUrl { get; set; }

    /// <summary>
    /// �?新增：帖子的回帖列表（用户回复）
    /// 可选字�?- 仅当帖子有回帖时存在
    /// </summary>
    public List<ReplyInfo>? ReplyList { get; set; }

    public string? AuthorPostId { get; set; }

    /// <summary>
    /// ✅ 新增：是否可以编辑楼主发帖（根据XML确定）
    /// </summary>
    public bool CanEditOp { get; set; } = false;

    /// <summary>
    /// ✅ 新增：编辑楼主发帖的URL（如果为null，则无法编辑）
    /// </summary>
    public string? EditOpUrl { get; set; }

    /// <summary>
    /// 楼主发帖的回帖奖励内容，为空时表示没有回帖奖励。
    /// </summary>
    public string ReplyRewardText { get; set; } = string.Empty;

    /// <summary>
    /// ✅ 新增：论坛版块ID（从快速回复区域的高级模式链接中提取）
    /// 用于备用标识当前所在的论坛版块
    /// </summary>
    public string? ForumId { get; set; }

    /// <summary>
    /// ��������ǰҳ�루��ҳ���ܣ�
    /// Ĭ��Ϊ 1����ʾ��һҳ
    /// </summary>
    public int CurrentPage { get; set; } = 1;

    /// <summary>
    /// ��������ҳ������ҳ���ܣ�
    /// Ĭ��Ϊ 1����ʾֻ��һҳ
    /// </summary>
    public int TotalPages { get; set; } = 1;

    public PollInfo? Poll { get; set; }
}

/// <summary>
/// �?新增：回帖信息模�?- 表示用户对帖子的回复
/// 与ThreadContent结构相同，但增加了用户左侧栏和楼层号信息
/// </summary>
public class ReplyInfo
{
    /// <summary>回帖的唯一ID（对应HTML中的post_ID�?/summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>回帖楼层号（例如�?沙发"�?板凳"�?4F"�?/summary>
    public string FloorNumber { get; set; } = string.Empty;

    /// <summary>回帖者用户名</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>回帖者用户ID（UID�?/summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>回帖者头像URL</summary>
    public string? AvatarUrl { get; set; }

    [JsonIgnore]
    public ImageSource? AvatarSource { get; set; }

    /// <summary>回帖者个人空间链�?/summary>
    public string? ProfileUrl { get; set; }

    /// <summary>回帖时间戳（格式�?2015-8-30 19:32"�?/summary>
    public string PostTime { get; set; } = string.Empty;

    /// <summary>回帖者IP属地（例如："北京"�?/summary>
    public string? IPLocation { get; set; }

    /// <summary>
    /// 当前楼层的回帖奖励内容，为空时表示没有回帖奖励。
    /// </summary>
    public string ReplyRewardText { get; set; } = string.Empty;

    /// <summary>回帖纯文本内容（简化版�?/summary>
    public string PlainTextContent { get; set; } = string.Empty;

    /// <summary>回帖富文本内容元素列�?/summary>
    public List<ContentElement> ContentElements { get; set; } = new();

    /// <summary>回帖是否被编辑（编辑状态信息）</summary>
    public string? EditStatus { get; set; }

    /// <summary>回帖的评论列表（点评�?/summary>
    public List<CommentInfo>? Comments { get; set; }

    /// <summary>是否有评�?/summary>
    public bool HasComments => Comments != null && Comments.Count > 0;

    /// <summary>回帖的评分汇总信�?/summary>
    public RatingSummary? RatingSummary { get; set; }

    /// <summary>是否有评�?/summary>
    public bool HasRatings => RatingSummary != null && RatingSummary.RatingDetails?.Count > 0;

    /// <summary>是否可点评（根据XML确定�?/summary>
    public bool CanComment { get; set; } = true;

    /// <summary>是否可评分（根据XML确定�?/summary>
    public bool CanRate { get; set; } = true;

    /// <summary>是否可回复（根据XML确定�?/summary>
    public bool CanReply { get; set; } = true;

    /// <summary>������ΨһID���������֣���Ӧpost ID���磺post_57924204��</summary>
    public string ReplyId { get; set; } = string.Empty;

    /// <summary>�������ֱ��URL�����Ϊnull���޷����֣�</summary>
    public string? RatingUrl { get; set; }

    /// <summary>是否可编辑（根据XML确定）</summary>
    public bool CanEdit { get; set; } = false;

    /// <summary>编辑URL（如果为null，则无法编辑）</summary>
    public string? EditUrl { get; set; }

    /// <summary>已有的图片附件列表（编辑时从页面加载）</summary>
    public List<UploadedFileInfo> ExistingImages { get; set; } = new();

    /// <summary>已有的普通附件列表（编辑时从页面加载）</summary>
    public List<UploadedFileInfo> ExistingAttachments { get; set; } = new();
}

