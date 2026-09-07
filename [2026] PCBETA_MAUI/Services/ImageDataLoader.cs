using System.Diagnostics;
using System.IO;
using Microsoft.Maui.Controls;

namespace PCBetaMAUI.Services;

public static class ImageDataLoader
{
    public static async Task<ImageSource?> LoadAsync(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return null;

        try
        {
            var imageBytes = await HttpClientManager.Instance.GetByteArrayAsync(imageUrl.Trim());
            if (imageBytes.Length == 0)
                return null;

            // 保留完整的 GIF 字节流，不转换为 Bitmap 或 PNG，确保 GIF 动画帧不丢失。
            return ImageSource.FromStream(() => new MemoryStream(imageBytes, writable: false));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"图片数据流加载失败: {imageUrl}, {ex.Message}");
            return null;
        }
    }
}
