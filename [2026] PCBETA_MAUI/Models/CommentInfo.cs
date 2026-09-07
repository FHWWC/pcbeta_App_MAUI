using System.Text.Json.Serialization;
using Microsoft.Maui.Controls;

namespace PCBetaMAUI.Models;

/// <summary>
/// 帖子评论信息模型
/// 对应HTML结构：<div id="comment_*" class="cm">
/// </summary>
public class CommentInfo
{
    /// <summary>
    /// 评论ID（用于回复评论时指定回复目标）
    /// ✅ 新增：用于支持评论回复功能
    /// </summary>
    public string CommentId { get; set; } = string.Empty;

    /// <summary>
    /// 评论者用户名
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 评论者头像URL
    /// </summary>
    public string AvatarUrl { get; set; } = string.Empty;

    [JsonIgnore]
    public ImageSource? AvatarSource { get; set; }

    /// <summary>
    /// 评论内容
    /// </summary>
    public string CommentText { get; set; } = string.Empty;

    /// <summary>
    /// 评论发表时间
    /// </summary>
    public string Timestamp { get; set; } = string.Empty;

    /// <summary>
    /// 用户个人空间链接
    /// </summary>
    public string UserProfileUrl { get; set; } = string.Empty;
}
