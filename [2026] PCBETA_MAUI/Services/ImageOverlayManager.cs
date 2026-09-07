using Microsoft.Maui.Controls;
using System;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using System.Diagnostics;

namespace PCBetaMAUI.Services
{
    public static class ImageOverlayManager
    {
        private static bool _isShown = false;
        private static string? _currentImageUrl;

        public static void Show(ImageSource source, string? imageUrl = null)
        {
            try
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        Page page = GetCurrentPage();
                        if (page == null)
                            return;

                        var overlay = page.FindByName<AbsoluteLayout>("ImageOverlay");
                        var overlayContainer = page.FindByName<ContentView>("OverlayImageContainer");
                        var closeBtn = page.FindByName<Button>("OverlayCloseButton");
                        var downloadBtn = page.FindByName<Button>("OverlayDownloadButton");

                        if (overlay == null || overlayContainer == null || closeBtn == null || downloadBtn == null)
                        {
                            Debug.WriteLine("⚠️ ImageOverlay not found on current page");
                            return;
                        }

                        // 使用 ZoomableImageView 控件包裹图片，支持手势缩放和平移
                        var zoomable = new PCBetaMAUI.Controls.ZoomableImageView(source);

                        overlayContainer.Content = zoomable;
                        overlay.IsVisible = true;
                        _isShown = true;
                        _currentImageUrl = imageUrl;

                        // attach handler once
                        closeBtn.Clicked -= CloseBtn_Clicked;
                        closeBtn.Clicked += CloseBtn_Clicked;
                        downloadBtn.Clicked -= DownloadBtn_Clicked;
                        downloadBtn.Clicked += DownloadBtn_Clicked;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"❌ ImageOverlayManager Show 错误: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ ImageOverlayManager Show BeginInvoke 错误: {ex.Message}");
            }
        }

        public static void ShowGif(string? gifUrl)
        {
            try
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        Page page = GetCurrentPage();
                        if (page == null)
                            return;

                        var overlay = page.FindByName<AbsoluteLayout>("ImageOverlay");
                        var overlayContainer = page.FindByName<ContentView>("OverlayImageContainer");
                        var closeBtn = page.FindByName<Button>("OverlayCloseButton");
                        var downloadBtn = page.FindByName<Button>("OverlayDownloadButton");

                        if (overlay == null || overlayContainer == null || closeBtn == null || downloadBtn == null)
                        {
                            Debug.WriteLine("⚠️ ImageOverlay not found on current page");
                            return;
                        }

                        if (!string.IsNullOrEmpty(gifUrl) && DeviceInfo.Platform == DevicePlatform.Android)
                        {
                            // 在 Android 上使用 GifImageView（尝试原生 GifDrawable）
                            var gifView = new PCBetaMAUI.Controls.GifImageView(gifUrl);
                            overlayContainer.Content = gifView;
                        }
                        else
                        {
                            // 其他平台回退到普通的 Image 预览
                            overlayContainer.Content = new PCBetaMAUI.Controls.ZoomableImageView(ImageSource.FromUri(new Uri(gifUrl ?? string.Empty)));
                        }

                        overlay.IsVisible = true;
                        _isShown = true;
                        _currentImageUrl = gifUrl;

                        closeBtn.Clicked -= CloseBtn_Clicked;
                        closeBtn.Clicked += CloseBtn_Clicked;
                        downloadBtn.Clicked -= DownloadBtn_Clicked;
                        downloadBtn.Clicked += DownloadBtn_Clicked;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"❌ ImageOverlayManager ShowGif 错误: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ ImageOverlayManager ShowGif BeginInvoke 错误: {ex.Message}");
            }
        }

        private static void CloseBtn_Clicked(object? sender, EventArgs e)
        {
            Hide();
        }

        private static async void DownloadBtn_Clicked(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_currentImageUrl))
            {
                var page = GetCurrentPage();
                if (page != null)
                    await page.DisplayAlertAsync("错误", "无法获取图片下载链接", "确定");
                return;
            }

            try
            {
                var downloadService = new FileDownloadService();
                await downloadService.DownloadAndSaveFileAsync(_currentImageUrl);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ 图片下载错误: {ex.Message}");
                var page = GetCurrentPage();
                if (page != null)
                    await page.DisplayAlertAsync("错误", $"图片下载失败: {ex.Message}", "确定");
            }
        }

        public static void Hide()
        {
            try
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        Page page = GetCurrentPage();
                        if (page == null)
                            return;

                        var overlay = page.FindByName<AbsoluteLayout>("ImageOverlay");
                        var overlayContainer = page.FindByName<ContentView>("OverlayImageContainer");
                        var closeBtn = page.FindByName<Button>("OverlayCloseButton");
                        var downloadBtn = page.FindByName<Button>("OverlayDownloadButton");

                        if (overlay == null || overlayContainer == null || closeBtn == null || downloadBtn == null)
                            return;

                        overlay.IsVisible = false;
                        if (overlayContainer != null)
                        {
                            overlayContainer.Content = null;
                        }
                        _isShown = false;
                        _currentImageUrl = null;

                        closeBtn.Clicked -= CloseBtn_Clicked;
                        downloadBtn.Clicked -= DownloadBtn_Clicked;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"❌ ImageOverlayManager Hide 错误: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ ImageOverlayManager Hide BeginInvoke 错误: {ex.Message}");
            }
        }

        private static Page GetCurrentPage()
        {
            try
            {
                // Prefer Shell.Current.CurrentPage
                if (Shell.Current != null && Shell.Current.CurrentPage != null)
                    return Shell.Current.CurrentPage;

                if (Application.Current?.MainPage is Page p)
                    return p;

                // As a fallback, try Navigation
                if (Application.Current?.MainPage is NavigationPage nav && nav.CurrentPage != null)
                    return nav.CurrentPage;

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ GetCurrentPage 错误: {ex.Message}");
                return null;
            }
        }
    }
}
