using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCBetaMAUI.Models;
using PCBetaMAUI.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace PCBetaMAUI.ViewModels;

public partial class MyPostViewModel : ObservableObject
{
    private readonly ApiService _apiService;
    private readonly INavigationService _navigationService;
    private int _currentPage = 1;
    private string _currentFilter = string.Empty;

    [ObservableProperty]
    private string selectedFilter = "all";

    [ObservableProperty]
    private bool isReplyMode;

    [ObservableProperty]
    private ObservableCollection<ThreadInfo> threads = new();

    [ObservableProperty]
    private bool isLoading = false;

    [ObservableProperty]
    private bool canGoPrevious = false;

    [ObservableProperty]
    private bool canGoNext = false;

    [ObservableProperty]
    private string pageDisplay = "第 1 页";
    [ObservableProperty]
    private string errorText = "";

    public MyPostViewModel()
    {
        _apiService = new ApiService();
        _navigationService = new NavigationService();
    }

    public async Task InitializeAsync()
    {
        _currentPage = 1;
        await LoadPageAsync(_currentPage);
    }

    [RelayCommand]
    public async Task SelectContentTypeAsync(string type)
    {
        var replyMode = string.Equals(type, "reply", StringComparison.OrdinalIgnoreCase);
        IsReplyMode = replyMode;
        SelectedFilter = "all";
        _currentFilter = string.Empty;
        _currentPage = 1;
        await LoadPageAsync(_currentPage);
    }

    [RelayCommand]
    public async Task FilterAsync(string filter)
    {
        // keep track of which filter button is selected (useful for UI highlighting)
        SelectedFilter = filter ?? string.Empty;

        // treat special "all" token as empty filter for API
        _currentFilter = (filter == "all") ? string.Empty : (filter ?? string.Empty);
        _currentPage = 1;
        await LoadPageAsync(_currentPage);
    }

    [RelayCommand]
    public async Task LoadPageAsync(int page)
    {
        try
        {
            ErrorText = string.Empty;
            IsLoading = true;
            var list = await _apiService.GetMyPostsAsync(page, _currentFilter, IsReplyMode ? "reply" : "thread");

            Threads.Clear();
            foreach (var t in list)
                Threads.Add(t);

            // detect error
            var error = list.FirstOrDefault(x => x.Id == "ERROR");
            if (error != null)
            {
                CanGoPrevious = false;
                CanGoNext = false;
                PageDisplay= "";
                ErrorText = error.Title;
                return;
            }

            // No results
            if (list == null || list.Count == 0)
            {
                Threads.Clear();
                CanGoPrevious = _currentPage > 1;
                CanGoNext = false;
                PageDisplay = "";
                ErrorText = "还没有相关的内容";
                return;
            }

            CanGoPrevious = _currentPage > 1;
            // next page: check if HTML contained 下一页 marker saved by ApiService
            CanGoNext = _apiService.LastPageContent?.Contains("下一页") == true;
            PageDisplay = $"第 {_currentPage} 页";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"MyPost LoadPage error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task NextPageAsync()
    {
        _currentPage++;
        await LoadPageAsync(_currentPage);
    }

    [RelayCommand]
    public async Task PreviousPageAsync()
    {
        if (_currentPage <= 1) return;
        _currentPage--;
        await LoadPageAsync(_currentPage);
    }

    [RelayCommand]
    public async Task SelectThreadAsync(ThreadInfo thread)
    {
        if (thread == null) return;
        try
        {
            var parameters = new Dictionary<string, object>
            {
                { "threadId", thread.Id },
                { "threadTitle", thread.Title ?? "" },
                { "forumId", string.Empty },
                { "boardName", thread.Category }
            };

            await _navigationService.NavigateToAsync("threadcontent", parameters);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Navigate to thread error: {ex.Message}");
        }
    }
}
