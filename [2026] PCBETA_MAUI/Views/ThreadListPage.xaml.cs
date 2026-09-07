using PCBetaMAUI.ViewModels;
using System.Diagnostics;

namespace PCBetaMAUI.Views;

[QueryProperty(nameof(BoardId), "boardId")]
[QueryProperty(nameof(ForumName), "forumName")]
[QueryProperty(nameof(PageNum), "page")]
public partial class ThreadListPage : ContentPage
{
    private static readonly Color LightThreadBackgroundColor = Color.FromArgb("#FFFFFF");
    private static readonly Color DarkThreadBackgroundColor = Color.FromArgb("#202938");
    private static readonly Color ThreadHoverBackgroundColor = Color.FromArgb("#E8F3FF");
    private const string ThreadHoverAnimationName = "ThreadHoverBackground";

    private string? _boardId;
    private string? _forumName;
    private int _pageNum = 1;

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

    public ThreadListPage()
    {
        InitializeComponent();
    }

    private async void OnThreadPointerEntered(object sender, PointerEventArgs e)
    {
        if (GetThreadFrame(sender) is Frame frame)
        {
            await AnimateThreadBackgroundAsync(frame, ThreadHoverBackgroundColor);
        }
    }

    private async void OnThreadPointerExited(object sender, PointerEventArgs e)
    {
        if (GetThreadFrame(sender) is Frame frame)
        {
            var originalColor = Application.Current?.RequestedTheme == AppTheme.Dark
                ? DarkThreadBackgroundColor
                : LightThreadBackgroundColor;

            await AnimateThreadBackgroundAsync(frame, originalColor);
        }
    }

    private static Frame? GetThreadFrame(object sender)
    {
        return sender switch
        {
            Frame frame => frame,
            PointerGestureRecognizer recognizer => recognizer.Parent as Frame,
            _ => null
        };
    }

    private static Task AnimateThreadBackgroundAsync(Frame frame, Color targetColor)
    {
        frame.AbortAnimation(ThreadHoverAnimationName);

        var startColor = frame.BackgroundColor;
        var completion = new TaskCompletionSource<bool>();
        var animation = new Animation(progress =>
        {
            frame.BackgroundColor = Color.FromRgba(
                startColor.Red + (targetColor.Red - startColor.Red) * progress,
                startColor.Green + (targetColor.Green - startColor.Green) * progress,
                startColor.Blue + (targetColor.Blue - startColor.Blue) * progress,
                startColor.Alpha + (targetColor.Alpha - startColor.Alpha) * progress);
        });

        frame.Animate(
            ThreadHoverAnimationName,
            animation,
            16,
            180,
            Easing.CubicOut,
            (_, _) => completion.TrySetResult(true));

        return completion.Task;
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
                    Debug.WriteLine($"Initializing with parameters - BoardId: {BoardId}, ForumName: {ForumName}, Page: {_pageNum}");
                    await viewModel.InitializeAsync(BoardId, ForumName, _pageNum);
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