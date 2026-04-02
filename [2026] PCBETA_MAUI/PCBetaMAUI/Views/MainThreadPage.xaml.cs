using PCBetaMAUI.ViewModels;
using PCBetaMAUI.Models;
using System.Diagnostics;

namespace PCBetaMAUI.Views;

public partial class MainThreadPage : ContentPage
{
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
}