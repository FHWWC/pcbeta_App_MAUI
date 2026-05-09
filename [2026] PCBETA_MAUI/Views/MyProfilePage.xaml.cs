using PCBetaMAUI.ViewModels;
using System.Diagnostics;

namespace PCBetaMAUI.Views;

public partial class MyProfilePage : ContentPage
{
    public MyProfilePage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            if (BindingContext is MyProfileViewModel viewModel)
            {
                await viewModel.LoadProfileAsync();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"MyProfilePage OnAppearing error: {ex.Message}");
        }
    }
}
