namespace PCBetaMAUI.Models;

/// <summary>
/// 单条评分信息模型
/// 对应HTML结构中 <ul class="cl"><li>...</li></ul> 中的每个评分项
/// </summary>
public class RatingInfo
{
    /// <summary>
    /// 评分者用户名
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 评分者头像URL
    /// </summary>
    public string AvatarUrl { get; set; } = string.Empty;

    /// <summary>
    /// 评分内容（简短版本，如"PB币 + 1"或"技术 + 1, PB币 + 60"）
    /// </summary>
    public string RatingContent { get; set; } = string.Empty;

    /// <summary>
    /// 评分完整信息（从tooltip中提取，包含详细说明，如"优秀文章/资源/回复 PB币 + 1"）
    /// </summary>
    public string RatingFullInfo { get; set; } = string.Empty;

    /// <summary>
    /// 评分者个人空间链接
    /// </summary>
    public string UserProfileUrl { get; set; } = string.Empty;
}

/// <summary>
/// 评分汇总信息模型
/// 对应HTML结构：<dl id="ratelog_*" class="rate">
/// </summary>
public class RatingSummary
{
    /// <summary>
    /// 总评分数（有多少人给过评分）
    /// </summary>
    public int TotalRatingCount { get; set; }

    /// <summary>
    /// "查看全部评分" 的链接URL
    /// </summary>
    public string ViewAllRatingsUrl { get; set; } = string.Empty;

    /// <summary>
    /// 所有评分的详细列表（通常显示前面几个）
    /// </summary>
    public List<RatingInfo> RatingDetails { get; set; } = new();
}
