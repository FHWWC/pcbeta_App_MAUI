using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PCBetaMAUI.Models;

/// <summary>
/// 已上传文件信息
/// 用于在 UI 中显示上传后的文件状态和元数据
/// </summary>
public class UploadedFileInfo : INotifyPropertyChanged
{
    private string _fileName = string.Empty;
    private string _fileSize = string.Empty;
    private string _localPath = string.Empty;
    private string _remoteUrl = string.Empty;
    private string _attachmentId = string.Empty;
    private bool _isUploading;
    private string _statusText = string.Empty;
    private Color _statusColor = Colors.Orange;
    private string _description = string.Empty;
    private string _readPerm = "0";
    private string _price = "0";

    /// <summary>
    /// 文件名
    /// </summary>
    public string FileName
    {
        get => _fileName;
        set => SetProperty(ref _fileName, value);
    }

    /// <summary>
    /// 文件大小字符串
    /// </summary>
    public string FileSize
    {
        get => _fileSize;
        set => SetProperty(ref _fileSize, value);
    }

    /// <summary>
    /// 本地文件路径
    /// </summary>
    public string LocalPath
    {
        get => _localPath;
        set => SetProperty(ref _localPath, value);
    }

    /// <summary>
    /// 服务器上的远程 URL
    /// </summary>
    public string RemoteUrl
    {
        get => _remoteUrl;
        set => SetProperty(ref _remoteUrl, value);
    }

    /// <summary>
    /// 附件 ID（从服务器返回）
    /// </summary>
    public string AttachmentId
    {
        get => _attachmentId;
        set => SetProperty(ref _attachmentId, value);
    }

    /// <summary>
    /// 是否正在上传
    /// </summary>
    public bool IsUploading
    {
        get => _isUploading;
        set => SetProperty(ref _isUploading, value);
    }

    /// <summary>
    /// 上传状态文本
    /// </summary>
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    /// <summary>
    /// 状态显示颜色
    /// </summary>
    public Color StatusColor
    {
        get => _statusColor;
        set => SetProperty(ref _statusColor, value);
    }

    /// <summary>
    /// 附件描述
    /// </summary>
    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    /// <summary>
    /// 阅读权限（0=所有人, 其他值表示特定权限等级）
    /// </summary>
    public string ReadPerm
    {
        get => _readPerm;
        set => SetProperty(ref _readPerm, value);
    }

    /// <summary>
    /// 附件售价（金币数，0表示免费）
    /// </summary>
    public string Price
    {
        get => _price;
        set => SetProperty(ref _price, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
