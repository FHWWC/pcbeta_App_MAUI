using PCBetaMAUI.ViewModels;
using PCBetaMAUI.Models;
using PCBetaMAUI.Services;
using System.Diagnostics;
using Microsoft.Maui.Devices;

namespace PCBetaMAUI.Views;

[QueryProperty(nameof(ThreadId), "threadId")]
[QueryProperty(nameof(ThreadTitle), "threadTitle")]
[QueryProperty(nameof(ForumId), "forumId")]
[QueryProperty(nameof(BoardName), "boardName")]
[QueryProperty(nameof(PageNum), "page")]
public partial class ThreadContentPage : ContentPage
{
    private string? _threadId;
    private string? _threadTitle;
    private string? _forumId;
    private string? _boardName;
    private int _pageNum = 1;
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

    public string? ForumId
    {
        get => _forumId;
        set
        {
            _forumId = Uri.UnescapeDataString(value ?? "");
            Debug.WriteLine($"✅ QueryProperty ForumId: {_forumId}");
        }
    }

    public string? BoardName
    {
        get => _boardName;
        set
        {
            _boardName = Uri.UnescapeDataString(value ?? "");
            Debug.WriteLine($"✅ QueryProperty BoardName: {_boardName}");
        }
    }

    public int PageNum
    {
        get => _pageNum;
        set
        {
            if (int.TryParse(value.ToString(), out var pageNum) && pageNum > 0)
                _pageNum = pageNum;
            else
                _pageNum = 1;
            Debug.WriteLine($"QueryProperty PageNum: {_pageNum}");
        }
    }

    public ThreadContentPage()
    {
        InitializeComponent();
    }

    private void PollOption_CheckedChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (!e.Value || sender is not CheckBox checkBox ||
            BindingContext is not ThreadContentViewModel viewModel ||
            viewModel.Poll is not PollInfo poll ||
            checkBox.BindingContext is not PollOption selectedOption)
            return;

        if (!poll.IsMultiple)
        {
            foreach (var option in poll.Options)
            {
                if (!ReferenceEquals(option, selectedOption))
                    option.IsSelected = false;
            }
        }
        else if (poll.MaxChoices > 0 && poll.Options.Count(option => option.IsSelected) > poll.MaxChoices)
        {
            selectedOption.IsSelected = false;
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            _viewModel = BindingContext as ThreadContentViewModel;

            if (_viewModel != null && !string.IsNullOrEmpty(_threadId))
            {
                Debug.WriteLine($"🔄 初始化 ViewModel - ThreadId: {_threadId}, ForumId: {_forumId}, Page: {_pageNum}");
                await _viewModel.InitializeAsync(_threadId, _threadTitle ?? "Thread", _forumId, _boardName, _pageNum);

                // 加载后渲染内容
                RenderThreadContent();
                // 监听 ViewModel 属性变更以便在翻页后自动滚动到回帖区域
                _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
                _viewModel.PropertyChanged += ViewModel_PropertyChanged;
                // 将短提示面板的初始状态设置为隐藏（确保 Opacity=0）
                try
                {
                    var frame = this.FindByName<Border>("TransientMessageFrame");
                    if (frame != null)
                    {
                        frame.Opacity = 0;
                        frame.IsVisible = false;
                    }
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ ThreadContentPage OnAppearing 错误: {ex.Message}");
        }
    }

    private async void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        try
        {
            if (sender is ThreadContentViewModel vm)
            {
                // 当 CurrentPage 或 IsLoading 变化时检查是否需要滚动
                if (e.PropertyName == nameof(vm.CurrentPage) || e.PropertyName == nameof(vm.IsLoading) || e.PropertyName == nameof(vm.ReplyList))
                {
                    // 如果加载完成且有回帖，则滚动到回帖区域
                    if (!vm.IsLoading && vm.HasReplies && MainScrollView != null && RepliesBorder != null)
                    {
                        // 延迟微小时间以确保布局完成
                        Device.BeginInvokeOnMainThread(async () =>
                        {
                            try
                            {
                                await Task.Delay(120);
                                // Scroll to the top of the RepliesBorder
                                await MainScrollView.ScrollToAsync(RepliesBorder, ScrollToPosition.Start, true);
                                Debug.WriteLine($"🔽 已自动滚动到回帖区域（第 {vm.CurrentPage} 页）");
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"❌ 自动滚动失败: {ex.Message}");
                            }
                        });
                    }
                }

                // 监听短提示可见性变化，执行淡入淡出动画
                if (e.PropertyName == nameof(vm.TransientMessageVisible))
                {
                    try
                    {
                        var frame = this.FindByName<Border>("TransientMessageFrame");
                        if (frame == null)
                            return;

                        // 获取当前值
                        bool visible = vm.TransientMessageVisible;

                        // 在主线程执行动画
                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            try
                            {
                                if (visible)
                                {
                                    frame.IsVisible = true;
                                    frame.Opacity = 0;
                                    await frame.FadeTo(1, 250);
                                }
                                else
                                {
                                    // 若已经不可见则直接确保隐藏
                                    await frame.FadeTo(0, 400);
                                    frame.IsVisible = false;
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"短提示动画失败: {ex.Message}");
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"处理短提示变化出错: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ ViewModel_PropertyChanged 错误: {ex.Message}");
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

            //  关键修改：传入当前帖子URL作为Referer，并仅在 Android 平台启用图片虚拟化（节省内存，避免 UI 卡顿）
            string currentThreadUrl = $"https://bbs.pcbeta.com/forum.php?mod=viewthread&tid={_threadId}";
            bool enableImageVirtualization = DeviceInfo.Platform == DevicePlatform.Android;
            ContentElementRenderer.RenderContentElements(ContentContainer, _viewModel.ContentElements, currentThreadUrl, MainScrollView, enableImageVirtualization: enableImageVirtualization);

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
                await _viewModel.InitializeAsync(_threadId, _threadTitle ?? "Thread", _forumId, _boardName);

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