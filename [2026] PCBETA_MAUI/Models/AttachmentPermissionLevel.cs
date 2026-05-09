namespace PCBetaMAUI.Models;

/// <summary>
/// 附件权限级别定义
/// 对应论坛中不同的用户级别
/// </summary>
public static class AttachmentPermissionLevel
{
    /// <summary>
    /// 权限级别列表 - 用于UI显示和选择
    /// </summary>
    public static readonly List<PermissionOption> PermissionOptions = new()
    {
        new PermissionOption { Value = "", Display = "不限" },
        new PermissionOption { Value = "9", Display = "游客" },
        new PermissionOption { Value = "10", Display = "PCBETA Alpha" },
        new PermissionOption { Value = "20", Display = "PCBETA Milestone" },
        new PermissionOption { Value = "30", Display = "PCBETA Beta1" },
        new PermissionOption { Value = "40", Display = "PCBETA Beta2" },
        new PermissionOption { Value = "50", Display = "PCBETA RC1" },
        new PermissionOption { Value = "70", Display = "PCBETA RC2" },
        new PermissionOption { Value = "100", Display = "PCBETA RTM" },
        new PermissionOption { Value = "120", Display = "PCBETA SP1" },
        new PermissionOption { Value = "140", Display = "PCBETA SP2" },
        new PermissionOption { Value = "160", Display = "PCBETA SP3" },
        new PermissionOption { Value = "200", Display = "PCBETA Plus" },
        new PermissionOption { Value = "205", Display = "Microsoft MVP / 远景活动组 / 远景达人 / 贡献会员 / 论坛贵宾" },
        new PermissionOption { Value = "210", Display = "远景解答组" },
        new PermissionOption { Value = "220", Display = "远景督察 / 见习督察 / 见习版主 / 远景版主" },
        new PermissionOption { Value = "225", Display = "远景休假组" },
        new PermissionOption { Value = "235", Display = "分区版主" },
        new PermissionOption { Value = "250", Display = "超级督察 / 超级版主" },
        new PermissionOption { Value = "255", Display = "远景管理 / PCBeta Team / 最高权限" }
    };
}

/// <summary>
/// 权限级别选项
/// </summary>
public class PermissionOption
{
    /// <summary>权限值（对应表单参数值）</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>显示文本（用于UI展示）</summary>
    public string Display { get; set; } = string.Empty;

    public override string ToString() => Display;
}
