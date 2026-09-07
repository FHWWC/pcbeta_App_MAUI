using PCBetaMAUI.ViewModels;
using PCBetaMAUI.Models;
using System.Diagnostics;

namespace PCBetaMAUI.Views;

public partial class MainThreadPage : ContentPage
{
    private static readonly Color LightForumBackgroundColor = Color.FromArgb("#FFFFFF");
    private static readonly Color DarkForumBackgroundColor = Color.FromArgb("#202938");
    private static readonly Color ForumHoverBackgroundColor = Color.FromArgb("#E8F3FF");
    private const string ForumHoverAnimationName = "ForumHoverBackground";

    public MainThreadPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            if (BindingContext is MainThreadViewModel viewModel)
            {
                await viewModel.InitializeAsync();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"MainThreadPage OnAppearing error: {ex.Message}");
        }
    }

    /// <summary>
    /// 论坛板块点击事件处理
    /// </summary>
    private async void OnForumTapped(object sender, TappedEventArgs e)
    {
        try
        {
            // 获取点击的数据项（ForumSection）
            if (e.Parameter is ForumSection forum && BindingContext is MainThreadViewModel viewModel)
            {
                // 直接调用 ViewModel 的异步方法
                await viewModel.SelectForumAsync(forum);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Forum tap error: {ex.Message}");
        }
    }

    private async void OnForumPointerEntered(object sender, PointerEventArgs e)
    {
        if (GetForumFrame(sender) is Frame frame)
        {
            await AnimateForumBackgroundAsync(frame, ForumHoverBackgroundColor);
        }
    }

    private async void OnForumPointerExited(object sender, PointerEventArgs e)
    {
        if (GetForumFrame(sender) is Frame frame)
        {
            var originalColor = Application.Current?.RequestedTheme == AppTheme.Dark
                ? DarkForumBackgroundColor
                : LightForumBackgroundColor;

            await AnimateForumBackgroundAsync(frame, originalColor);
        }
    }

    private static Frame? GetForumFrame(object sender)
    {
        return sender switch
        {
            Frame frame => frame,
            PointerGestureRecognizer recognizer => recognizer.Parent as Frame,
            _ => null
        };
    }

    private static Task AnimateForumBackgroundAsync(Frame frame, Color targetColor)
    {
        frame.AbortAnimation(ForumHoverAnimationName);

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
            ForumHoverAnimationName,
            animation,
            16,
            180,
            Easing.CubicOut,
            (_, _) => completion.TrySetResult(true));

        return completion.Task;
    }
}