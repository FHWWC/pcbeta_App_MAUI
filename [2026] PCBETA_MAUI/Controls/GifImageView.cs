using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace PCBetaMAUI.Controls;

/// <summary>
/// GIF 显示控件：
/// - Android: 尝试使用 pl.droidsonroids.gif.GifDrawable（需在 Android 项目中添加依赖），
/// - 其它平台: 回退为普通 Image 显示第一帧。
/// </summary>
public class GifImageView : ContentView
{
    private readonly Image _fallbackImage;
    public string Url { get; }
    public double GifWidth { get; }
    public double GifHeight { get; }

    public GifImageView(string url, double width = -1, double height = -1)
    {
        Url = url ?? string.Empty;
        GifWidth = width;
        GifHeight = height;

        _fallbackImage = new Image
        {
            Aspect = Aspect.AspectFit,
            Margin = new Thickness(0, 10),
        };

        if (GifWidth > 0)
            _fallbackImage.WidthRequest = Math.Min(GifWidth, 300);
        if (GifHeight > 0)
            _fallbackImage.HeightRequest = GifHeight;

        Content = _fallbackImage;
        _ = LoadFallbackImageAsync();
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        _ = TryApplyNativeGifAsync();
    }

    private async Task TryApplyNativeGifAsync()
    {
        try
        {
            if (DeviceInfo.Platform != DevicePlatform.Android)
                return; // 仅在 Android 上尝试原生 GIF

#if ANDROID
            if (string.IsNullOrEmpty(Url))
                return;

            var cacheFile = Path.Combine(FileSystem.CacheDirectory, $"gif_{Guid.NewGuid()}.gif");

            try
            {
                Debug.WriteLine($"🎞️ 开始通过 HttpClient 下载 GIF: {Url}");
                var imageBytes = await Services.HttpClientManager.Instance.GetByteArrayAsync(Url);
                Debug.WriteLine($"🎞️ GIF 下载完成: {Url}, 长度={imageBytes.Length} bytes");
                await File.WriteAllBytesAsync(cacheFile, imageBytes);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"⚠️ 下载 GIF 失败，回退到静态 Image: {ex.Message}");
                return;
            }

            try
            {
                var nativeImage = _fallbackImage.Handler?.PlatformView as Android.Widget.ImageView;
                if (nativeImage == null)
                {
                    await Task.Delay(80);
                    nativeImage = _fallbackImage.Handler?.PlatformView as Android.Widget.ImageView;
                }

                if (nativeImage != null)
                {
                    try
                    {
                        // Avoid a direct compile-time dependency on the optional "pl.droidsonroids.gif" binding.
                        // Try to create the GifDrawable via reflection; if that fails, fall back to a static bitmap.
                        Type gifType = null;
                        try
                        {
                            gifType = Type.GetType("pl.droidsonroids.gif.GifDrawable");
                        }
                        catch { }

                        if (gifType != null)
                        {
                            object gifInstance = null;
                            try
                            {
                                gifInstance = Activator.CreateInstance(gifType, cacheFile);
                            }
                            catch
                            {
                                // Ignore and fall through to bitmap fallback below
                            }

                            if (gifInstance is Android.Graphics.Drawables.Drawable drawable)
                            {
                                nativeImage.SetImageDrawable(drawable);
                            }
                            else
                            {
                                var bitmap = Android.Graphics.BitmapFactory.DecodeFile(cacheFile);
                                if (bitmap != null)
                                    nativeImage.SetImageBitmap(bitmap);
                            }
                        }
                        else
                        {
                            var bitmap = Android.Graphics.BitmapFactory.DecodeFile(cacheFile);
                            if (bitmap != null)
                                nativeImage.SetImageBitmap(bitmap);
                        }
                    }
                    catch (Exception dex)
                    {
                        Debug.WriteLine($"❌ 设置 GifDrawable 失败: {dex.Message}");
                    }
                }

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ GifImageView Android handler error: {ex.Message}");
            }
#endif
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ GifImageView TryApplyNativeGifAsync 错误: {ex.Message}");
        }
    }

    private async Task LoadFallbackImageAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(Url))
                return;

            Debug.WriteLine($"🎞️ 开始下载 GIF 静态预览: {Url}");
            var imageBytes = await Services.HttpClientManager.Instance.GetByteArrayAsync(Url);
            Debug.WriteLine($"🎞️ GIF 静态预览下载完成: {Url}, 长度={imageBytes.Length} bytes");
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _fallbackImage.Source = ImageSource.FromStream(() =>
                    new MemoryStream(imageBytes, writable: false));
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"⚠️ 下载 GIF 静态预览失败: {ex.Message}");
        }
    }
}
