using PCBetaMAUI.ViewModels;
using System.Diagnostics;

namespace PCBetaMAUI.Views;

[QueryProperty(nameof(BoardId), "boardId")]
[QueryProperty(nameof(ForumName), "forumName")]
public partial class ThreadListPage : ContentPage
{
    private string? _boardId;
    private string? _forumName;

    public string? BoardId
    {
        get => _boardId;
        set
        {
            _boardId = Uri.UnescapeDataString(value ?? "");
            Debug.WriteLine($"QueryProperty BoardId: {_boardId}");
        }
    }

    public string? ForumName
    {
        get => _forumName;
        set
        {
            _forumName = Uri.UnescapeDataString(value ?? "");
            Debug.WriteLine($"QueryProperty ForumName: {_forumName}");
        }
    }

    public ThreadListPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            if (BindingContext is ThreadListViewModel viewModel)
            {
                // 如果通过 QueryProperty 获取到参数，则始终重新初始化
                // 这样可以处理第二次及以后的跳转，确保 ViewModel 被重新初始化
                if (!string.IsNullOrEmpty(BoardId) && !string.IsNullOrEmpty(ForumName))
                {
                    Debug.WriteLine($"Initializing with parameters - BoardId: {BoardId}, ForumName: {ForumName}");
                    await viewModel.InitializeAsync(BoardId, ForumName);
                }
                else
                {
                    // 没有收到导航参数，但 ViewModel 已初始化，则加载帖子列表
                    if (!string.IsNullOrEmpty(viewModel.BoardName) && viewModel.BoardName != "Forum")
                    {
                        Debug.WriteLine($"Reloading threads for existing board: {viewModel.BoardName}");
                        await viewModel.LoadThreadsAsync();
                    }
                    else
                    {
                        Debug.WriteLine("Warning: No board ID received and ViewModel not initialized");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ThreadListPage OnAppearing error: {ex.Message}");
        }
    }
}