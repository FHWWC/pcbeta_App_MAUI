using PCBetaMAUI.ViewModels;
using PCBetaMAUI.Models;

namespace PCBetaMAUI.Views;

public partial class MyNoticePage : ContentPage
{
    private readonly MyNoticeViewModel _viewModel;

    public MyNoticePage()
    {
        InitializeComponent();
        _viewModel = new MyNoticeViewModel();
        BindingContext = _viewModel;
    }

    /// <summary>
    /// 页面出现时加载通知列表
    /// </summary>
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // 加载第一页通知
        await _viewModel.LoadNoticesAsync(1);
    }

    /// <summary>
    /// 处理通知项的点击事件
    /// ✅ 改进：支持通过SelectionChanged事件跳转
    /// </summary>
    private async void OnNoticeSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is NoticeInfo selectedNotice)
        {
            // 调用ViewModel中的选择命令
            if (_viewModel.SelectNoticeCommand.CanExecute(selectedNotice))
            {
                _viewModel.SelectNoticeCommand.Execute(selectedNotice);
            }

            // 清除选择状态（避免重复点击相同项时无反应）
            if (sender is CollectionView collectionView)
            {
                collectionView.SelectedItem = null;
            }
        }
    }
}
