using PCBetaMAUI.ViewModels;
using System.Diagnostics;

namespace PCBetaMAUI.Views;

[QueryProperty(nameof(FormHash), "formHash")]
public partial class SearchPage : ContentPage
{
    private SearchViewModel? _viewModel;
    private string? _formHash;

    public string? FormHash
    {
        get => _formHash;
        set
        {
            _formHash = Uri.UnescapeDataString(value ?? "");
            Debug.WriteLine($"SearchPage QueryProperty FormHash received: {_formHash}");
        }
    }

    public SearchPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            _viewModel = BindingContext as SearchViewModel;
            if (_viewModel != null)
            {
                // Set formHash if it was received via navigation parameters
                if (!string.IsNullOrEmpty(_formHash))
                {
                    _viewModel.SetFormHash(_formHash);
                    Debug.WriteLine($"SearchPage OnAppearing: Set formHash to ViewModel: {_formHash}");
                }

                await _viewModel.InitializeAsync();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SearchPage OnAppearing error: {ex.Message}");
        }
    }
}
