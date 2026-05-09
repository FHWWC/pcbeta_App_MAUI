using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCBetaMAUI.Models;
using PCBetaMAUI.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace PCBetaMAUI.ViewModels;

public partial class SearchViewModel : ObservableObject
{
    private readonly ApiService _apiService;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<SearchThreadInfo> searchResults = new();

    [ObservableProperty]
    private SearchThreadInfo? selectedThread;

    [ObservableProperty]
    private bool isLoading = false;

    [ObservableProperty]
    private int currentPage = 1;

    [ObservableProperty]
    private bool canGoPrevious = false;

    [ObservableProperty]
    private bool canGoNext = false;

    [ObservableProperty]
    private string pageDisplay = "第 1 页";

    [ObservableProperty]
    private bool isLastPostChecked = true;
    [ObservableProperty]
    private bool isDatelineChecked = false;


    private string _searchId = string.Empty; // random 4 digits
    private string _formHash = string.Empty;
    private bool _initialized = false;

    public SearchViewModel()
    {
        _apiService = new ApiService();
        _navigationService = new NavigationService();

        SelectedThread = null;
    }

    /// <summary>
    /// Sets the formHash value from MainThreadPage
    /// </summary>
    public void SetFormHash(string formHash)
    {
        _formHash = formHash;
        Debug.WriteLine($"SearchViewModel.SetFormHash set to: {_formHash}");
    }

    public async Task InitializeAsync()
    {
        if (_initialized)
            return;

        _initialized = true;

        try
        {
            IsLoading = true;
            var html = await _apiService.GetSearchPageAsync(_formHash, IsLastPostChecked);

            if (!string.IsNullOrEmpty(html))
            {
                if (html.Contains("class=\"alert_info\"") && html.Contains("只能进行一次搜索"))
                {
                    await Application.Current?.Windows[0].Page?.DisplayAlertAsync("提示信息", "您在 10 秒内只能进行一次搜索", "确定");
                    return;
                }
                else if (html.Contains("class=\"alert_error\"") && html.Contains("没有权限进行此操作"))
                {
                    await Application.Current?.Windows[0].Page?.DisplayAlertAsync("提示信息", "您没有权限进行此操作，如有疑问请联系管理员", "确定");
                    return;
                }

                var parser = new XmlParsingService();
                _searchId = parser.GetSearchID(html);
                var results = parser.ParseSearchResultsFromHtml(html);

                SearchResults.Clear();
                foreach (var t in results)
                    SearchResults.Add(t);

                UpdatePagination(html);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Search init error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
            return;

        try
        {
            IsLoading = true;
            CurrentPage = 1;

            // post search
            if (string.IsNullOrEmpty(_formHash))
            {
                // try to initialize if missing
                await InitializeAsync();
            }

            //var postOk = await _apiService.PostSearchAsync(_formHash, SearchText);
            //Debug.WriteLine($"PostSearch returned: {postOk}");

            var html = await _apiService.GetSearchPageAsync(_formHash, IsLastPostChecked, SearchText);
            if (html.Contains("class=\"alert_info\"") && html.Contains("只能进行一次搜索"))
            {
                await Application.Current?.Windows[0].Page?.DisplayAlertAsync("提示信息", "您在 10 秒内只能进行一次搜索", "确定");
                return;
            }
            else if(html.Contains("class=\"alert_error\"") &&html.Contains("没有权限进行此操作"))
            {
                await Application.Current?.Windows[0].Page?.DisplayAlertAsync("提示信息", "您没有权限进行此操作，如有疑问请联系管理员", "确定");
                return;
            }

            var parser = new XmlParsingService();
            var results = parser.ParseSearchResultsFromHtml(html);

            SearchResults.Clear();
            foreach (var t in results)
                SearchResults.Add(t);

            UpdatePagination(html);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Search error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void UpdatePagination(string html)
    {
        try
        {
            // detect previous/next links
            CanGoPrevious = CurrentPage > 1;
            CanGoNext = html.Contains("下一页") || Regex.IsMatch(html, "page=\\d+\">下一页", RegexOptions.IgnoreCase);
            PageDisplay = $"第 {CurrentPage} 页";
        }
        catch { }
    }

    [RelayCommand]
    public async Task PreviousPageAsync()
    {
        if (CurrentPage <= 1)
            return;

        CurrentPage--;
        await LoadPageAsync(CurrentPage);
    }

    [RelayCommand]
    public async Task NextPageAsync()
    {
        CurrentPage++;
        await LoadPageAsync(CurrentPage);
    }
    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            // If no search text, reinitialize the page
            _initialized = false;
            await InitializeAsync();
        }
        else
        {
            // If there's search text, reload current page results
            await LoadPageAsync(CurrentPage);
        }
    }

    [RelayCommand]
    public async Task SelectThreadAsync(SearchThreadInfo? thread)
    {
        if (thread == null)
            return;

        try
        {
            var parameters = new Dictionary<string, object>
            {
                { "threadId", thread.Id },
                { "threadTitle", thread.Title ?? "" },
                { "forumId", string.Empty },
                { "boardName", thread.ForumName }
            };
            await _navigationService.NavigateToAsync("threadcontent", parameters);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Navigate to thread error: {ex.Message}");
        }
    }

    private async Task LoadPageAsync(int page)
    {
        try
        {
            IsLoading = true;
            var html = await _apiService.GetSearchPageAsync(_formHash,IsLastPostChecked,SearchText,page);

            var parser = new XmlParsingService();
            var results = parser.ParseSearchResultsFromHtml(html);

            SearchResults.Clear();
            foreach (var t in results)
                SearchResults.Add(t);

            UpdatePagination(html);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"LoadPage error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedThreadChanged(SearchThreadInfo? value)
    {
        if (value == null)
            return;

        // navigate to thread content
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                var parameters = new Dictionary<string, object>
            {
                { "threadId", value.Id },
                { "threadTitle", value.Title ?? "" },
                { "forumId", string.Empty },
                                { "boardName", value.ForumName }
            };
                await _navigationService.NavigateToAsync("threadcontent", parameters);

                // clear selection
                SelectedThread = null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Navigate to thread error: {ex.Message}");
            }
        });
    }
}
