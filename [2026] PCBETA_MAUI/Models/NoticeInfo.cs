namespace PCBetaMAUI.Models;

/// <summary>
/// 用户通知信息模型
/// 对应HTML结构：<div class="nts"><dl class="cl" notice="xxx">
/// </summary>
public class NoticeInfo
{
    /// <summary>
    /// 通知ID
    /// </summary>
    public string NoticeId { get; set; } = string.Empty;

    /// <summary>
    /// 用户头像URL
    /// </summary>
    public string AvatarUrl { get; set; } = string.Empty;
    /// <summary>
    /// 用户名
    /// </summary>
    public string UserName { get; set; } = string.Empty;
    /// <summary>
    /// 帖子标题（此处也可以放其他通知的目标页面）
    /// </summary>
    public string ThreadTitle { get; set; } = string.Empty;

    /// <summary>
    /// 通知时间（如 "2026-4-27 17:05"）
    /// </summary>
    public string NoticeTime { get; set; } = string.Empty;

    /// <summary>
    /// 通知文本内容
    /// </summary>
    public string NoticeText { get; set; } = string.Empty;

    /// <summary>
    /// ✅ 新增：帖子线程ID（用于跳转到ThreadContentPage）
    /// 从通知文本中的URL提取，如果消息包含 ptid=xxx
    /// </summary>
    public string? ThreadId { get; set; }

    /// <summary>
    /// ✅ 新增：帖子回复ID（用于定位到具体的回复）
    /// 从通知文本中的URL提取，如果消息包含 pid=xxx
    /// </summary>
    public string? PostId { get; set; }

    /// <summary>
    /// ✅ 新增：是否有可跳转的线程（用于UI显示点击提示）
    /// 如果ThreadId不为空则为true
    /// </summary>
    public bool HasThreadLink => !string.IsNullOrEmpty(ThreadId);
}
