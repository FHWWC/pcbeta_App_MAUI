using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Maui.Controls;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace PCBetaMAUI.Models;

/// <summary>
/// 用户个人资料信息模型
/// </summary>
public class UserProfileInfo
{
    // 基本信息
    [JsonPropertyName("uid")]
    public string Uid { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("avatarUrl")]
    public string AvatarUrl { get; set; } = string.Empty;

    [JsonIgnore]
    public ImageSource? AvatarSource { get; set; }

    // 账户信息
    [JsonPropertyName("spacePV")]
    public string SpacePV { get; set; } = string.Empty;

    [JsonPropertyName("emailStatus")]
    public string EmailStatus { get; set; } = string.Empty;

    [JsonPropertyName("replyCount")]
    public string ReplyCount { get; set; } = string.Empty;

    [JsonPropertyName("threadCount")]
    public string ThreadCount { get; set; } = string.Empty;

    [JsonPropertyName("friendCount")]
    public string FriendCount { get; set; } = string.Empty;

    [JsonPropertyName("credits")]
    public string Credits { get; set; } = string.Empty;

    [JsonPropertyName("pbCoins")]
    public string PBCoins { get; set; } = string.Empty;

    [JsonPropertyName("reputation")]
    public string Reputation { get; set; } = string.Empty;

    [JsonPropertyName("contribution")]
    public string Contribution { get; set; } = string.Empty;

    [JsonPropertyName("technology")]
    public string Technology { get; set; } = string.Empty;

    [JsonPropertyName("activity")]
    public string Activity { get; set; } = string.Empty;

    [JsonPropertyName("recordCount")]
    public string RecordCount { get; set; } = string.Empty;

    [JsonPropertyName("blogCount")]
    public string BlogCount { get; set; } = string.Empty;

    [JsonPropertyName("albumCount")]
    public string AlbumCount { get; set; } = string.Empty;

    [JsonPropertyName("shareCount")]
    public string ShareCount { get; set; } = string.Empty;

    [JsonPropertyName("usedSpace")]
    public string UsedSpace { get; set; } = string.Empty;

    // 荣誉信息
    [JsonPropertyName("medals")]
    public ObservableCollection<Medal> Medals { get; set; } = new();

    // 活跃概况
    [JsonPropertyName("userGroup")]
    public string UserGroup { get; set; } = string.Empty;

    [JsonPropertyName("registerTime")]
    public string RegisterTime { get; set; } = string.Empty;

    [JsonPropertyName("lastAccessTime")]
    public string LastAccessTime { get; set; } = string.Empty;

    [JsonPropertyName("lastVisitIP")]
    public string LastVisitIP { get; set; } = string.Empty;

    [JsonPropertyName("lastActivityTime")]
    public string LastActivityTime { get; set; } = string.Empty;

    [JsonPropertyName("lastPostTime")]
    public string LastPostTime { get; set; } = string.Empty;

    [JsonPropertyName("timezone")]
    public string Timezone { get; set; } = string.Empty;
}

/// <summary>
/// 勋章信息模型
/// </summary>
public class Medal
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("imageUrl")]
    public string ImageUrl { get; set; } = string.Empty;

    [JsonIgnore]
    public ImageSource? ImageSource { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}
