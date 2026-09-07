namespace PCBetaMAUI.Views;

public partial class MyFavoritePage : ContentPage
{
    private readonly ViewModels.MyFavoriteViewModel _viewModel;

	public MyFavoritePage()
	{
		InitializeComponent();
		_viewModel = new ViewModels.MyFavoriteViewModel();
		BindingContext = _viewModel;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _viewModel.LoadFavoritesAsync(1);
	}
}