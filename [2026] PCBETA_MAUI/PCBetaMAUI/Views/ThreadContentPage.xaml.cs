using PCBetaMAUI.ViewModels;
using PCBetaMAUI.Models;
using PCBetaMAUI.Services;
using System.Diagnostics;

namespace PCBetaMAUI.Views;

[QueryProperty(nameof(ThreadId), "threadId")]
[QueryProperty(nameof(ThreadTitle), "threadTitle")]
public partial class ThreadContentPage : ContentPage
{
    private string? _threadId;
    private string? _threadTitle;
    private ThreadContentViewModel? _viewModel;

    public string? ThreadId
    {
        get => _threadId;
        set
        {
            _threadId = Uri.UnescapeDataString(value ?? "");
            Debug.WriteLine($" QueryProperty ThreadId: {_threadId}");
        }
    }

    public string? ThreadTitle
    {
        get => _threadTitle;
        set
        {
            _threadTitle = Uri.UnescapeDataString(value ?? "");
            Title = _threadTitle ?? "帖子内容";
            Debug.WriteLine($" QueryProperty ThreadTitle: {_threadTitle}");
        }
    }

    public ThreadContentPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            _viewModel = BindingContext as ThreadContentViewModel;
            
            if (_viewModel != null && !string.IsNullOrEmpty(_threadId))
            {
                Debug.WriteLine($"🔄 初始化 ViewModel - ThreadId: {_threadId}");
                await _viewModel.InitializeAsync(_threadId, _threadTitle ?? "Thread");
                
                // 加载后渲染内容
                RenderThreadContent();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ ThreadContentPage OnAppearing 错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 渲染线程内容到 UI
    ///  改进：传入当前帖子URL作为Referer，确保文件下载时使用正确的来源
    /// </summary>
    private void RenderThreadContent()
    {
        try
        {
            if (_viewModel?.ContentElements == null)
            {
                Debug.WriteLine("⚠️ ContentElements 为空");
                return;
            }

            Debug.WriteLine($"🎨 开始渲染 {_viewModel.ContentElements.Count} 个内容元素");

            //  关键修改：传入当前帖子URL作为Referer
            string currentThreadUrl = $"https://bbs.pcbeta.com/forum.php?mod=viewthread&tid={_threadId}";
            ContentElementRenderer.RenderContentElements(ContentContainer, _viewModel.ContentElements, currentThreadUrl);

            Debug.WriteLine($" 内容渲染完成，当前帖子URL: {currentThreadUrl}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 渲染内容错误: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private async void OnBackButtonClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    /// <summary>
    /// 刷新页面内容
    ///  新增：支持购买完成后刷新页面
    /// 重新从服务器加载帖子内容，并重新渲染
    /// </summary>
    public async Task RefreshAsync()
    {
        try
        {
            Debug.WriteLine($"🔄 刷新帖子内容 - ThreadId: {_threadId}");

            if (_viewModel != null && !string.IsNullOrEmpty(_threadId))
            {
                // 重新加载内容
                await _viewModel.InitializeAsync(_threadId, _threadTitle ?? "Thread");

                // 重新渲染 UI
                RenderThreadContent();

                Debug.WriteLine(" 帖子内容已刷新");
                await DisplayAlertAsync("刷新完成", "页面内容已刷新", "确定");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 刷新页面失败: {ex.Message}");
            await DisplayAlertAsync("刷新失败", $"错误: {ex.Message}", "确定");
        }
    }

    //  新增：处理附件下载点击
    private async void OnAttachmentClicked(string attachmentUrl, string fileName)
    {
        if (_viewModel == null)
        {
            await DisplayAlertAsync("错误", "ViewModel未初始化", "确定");
            return;
        }

        try
        {
            var downloadService = new FileDownloadService();

            //  关键：使用当前帖子URL作为Referer
            bool success = await downloadService.DownloadAndSaveFileAsync(
                attachmentUrl,
                fileName,
                _viewModel.CurrentThreadUrl  // ← 从该帖子页面下载
            );

            if (!success)
            {
                await DisplayAlertAsync("提示",
                    "下载失败，请检查：\n" +
                    "1. 附件URL是否有效\n" +
                    "2. 是否需要重新登录\n" +
                    "3. 权限是否足够",
                    "确定");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 下载异常: {ex.Message}");
            await DisplayAlertAsync("错误", $"下载出错: {ex.Message}", "确定");
        }
    }
}