using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using PCBetaMAUI.Models;
using PCBetaMAUI.Services;

namespace PCBetaMAUI.ViewModels;

/// <summary>
/// ViewModel for thread list page showing threads in a specific forum/board
/// </summary>
public partial class ThreadListViewModel : ObservableObject
{
    private readonly ApiService _apiService;
    private readonly INavigationService _navigationService;
    private string? _currentBoardId;
    private int _currentPage = 1;

    [ObservableProperty]
    private string boardName = "论坛";

    [ObservableProperty]
    private ObservableCollection<ThreadInfo> stickyThreads = new();

    [ObservableProperty]
    private ObservableCollection<ThreadInfo> threads = new();

    [ObservableProperty]
    private bool isLoading = false;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private bool hasThreads = false;

    [ObservableProperty]
    private bool canGoToPreviousPage = false;

    [ObservableProperty]
    private bool canGoToNextPage = false;

    [ObservableProperty]
    private int currentPage = 1;

    [ObservableProperty]
    private bool isStickyExpanded = true;

    [ObservableProperty]
    private bool isThreadsExpanded = true;

    public ThreadListViewModel()
    {
        _apiService = new ApiService();
        _navigationService = new NavigationService();
    }

    /// <summary>
    /// Initializes the view model with board ID
    /// </summary>
    public async Task InitializeAsync(string boardId, string forumName)
    {
        _currentBoardId = boardId;
        BoardName = forumName;
        _currentPage = 1;
        CurrentPage = 1;

        await LoadThreadsAsync();
    }

    /// <summary>
    /// Loads threads for the current board
    /// </summary>
    [RelayCommand]
    public async Task LoadThreadsAsync()
    {
        if (string.IsNullOrEmpty(_currentBoardId))
        {
            ErrorMessage = "板块ID为空！";
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            var allThreads = await _apiService.GetThreadListAsync(_currentBoardId, _currentPage);

            //  改进：分别处理置顶帖和普通帖子
            var stickyList = allThreads.Where(t => t.IsSticky).ToList();
            var regularList = allThreads.Where(t => !t.IsSticky && t.Id != "ERROR").ToList();

            // 检查是否有错误
            var errorThread = allThreads.FirstOrDefault(t => t.Id == "ERROR");
            if (errorThread != null)
            {
                HasThreads = false;
                CanGoToPreviousPage = false;
                CanGoToNextPage = false;
                ErrorMessage = errorThread.Title;
                return;
            }

            //  更新置顶帖集合
            StickyThreads.Clear();
            foreach (var thread in stickyList)
            {
                StickyThreads.Add(thread);
            }

            //  更新普通帖子集合
            Threads.Clear();
            foreach (var thread in regularList)
            {
                Threads.Add(thread);
            }

            HasThreads = regularList.Count > 0; // 基于普通帖子判断是否有内容

            // Update pagination buttons
            CanGoToPreviousPage = _currentPage > 1;
            CanGoToNextPage = regularList.Count > 0; // Simplified: assume there's a next page if we got results

            if (!HasThreads && _currentPage == 1)
            {
                ErrorMessage = "未获取到数据";
            }

            Debug.WriteLine($" 线程已加载 - 置顶帖: {StickyThreads.Count}, 普通帖: {Threads.Count}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Load threads error: {ex.Message}");
            ErrorMessage = $"加载数据失败，原因： {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Navigates to the thread content page for the selected thread
    /// </summary>
    [RelayCommand]
    public async Task SelectThreadAsync(ThreadInfo thread)
    {
        if (thread == null || string.IsNullOrEmpty(thread.Id))
        {
            ErrorMessage = "Invalid thread selected";
            return;
        }

        try
        {
            var parameters = new Dictionary<string, object>
            {
                { "threadId", thread.Id },
                { "threadTitle", thread.Title ?? "Thread" }
            };

            await _navigationService.NavigateToAsync("threadcontent", parameters);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Select thread error: {ex.Message}");
            ErrorMessage = "Failed to navigate to thread";
        }
    }

    /// <summary>
    /// Goes to the next page of threads
    /// </summary>
    [RelayCommand]
    public async Task NextPageAsync()
    {
        _currentPage++;
        CurrentPage = _currentPage;
        await LoadThreadsAsync();
    }

    /// <summary>
    /// Goes to the previous page of threads
    /// </summary>
    [RelayCommand]
    public async Task PreviousPageAsync()
    {
        if (_currentPage > 1)
        {
            _currentPage--;
            CurrentPage = _currentPage;
            await LoadThreadsAsync();
        }
    }

    /// <summary>
    /// Goes back to the main forum page
    /// </summary>
    [RelayCommand]
    public async Task GoBackAsync()
    {
        await _navigationService.GoBackAsync();
    }

    /// <summary>
    /// Refreshes the current page
    /// </summary>
    [RelayCommand]
    public async Task RefreshAsync()
    {
        await LoadThreadsAsync();
    }

    /// <summary>
    /// Toggles the sticky threads section expand/collapse state
    /// </summary>
    [RelayCommand]
    public void ToggleStickyExpanded()
    {
        IsStickyExpanded = !IsStickyExpanded;
    }

    /// <summary>
    /// Toggles the regular threads section expand/collapse state
    /// </summary>
    [RelayCommand]
    public void ToggleThreadsExpanded()
    {
        IsThreadsExpanded = !IsThreadsExpanded;
    }
}


