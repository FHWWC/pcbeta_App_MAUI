using PCBetaMAUI.ViewModels;
namespace PCBetaMAUI.Views;

public partial class MyPostPage : ContentPage
{
    private MyPostViewModel _vm => BindingContext as MyPostViewModel;

    public MyPostPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_vm != null)
        {
            await _vm.InitializeAsync();
        }
    }
}
