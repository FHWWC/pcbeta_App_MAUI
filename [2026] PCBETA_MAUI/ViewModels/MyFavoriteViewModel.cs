using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCBetaMAUI.Models;
using PCBetaMAUI.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;

namespace PCBetaMAUI.ViewModels;

public partial class MyFavoriteViewModel : ObservableObject
{
    private readonly ApiService _apiService;
    private readonly INavigationService _navigationService;
    private readonly XmlParsingService _xmlParsingService;
    private string _currentFormHash = string.Empty;

    [ObservableProperty]
    private ObservableCollection<FavoriteItem> favorites = new();

    [ObservableProperty]
    private bool isLoading = false;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private bool hasFavorites = false;

    [ObservableProperty]
    private int currentPage = 1;

    [ObservableProperty]
    private bool canGoToPreviousPage = false;

    [ObservableProperty]
    private bool canGoToNextPage = false;
    [ObservableProperty]
    public bool allSelectedCheck = false;

    public MyFavoriteViewModel()
    {
        _apiService = new ApiService();
        _navigationService = new NavigationService();
        _xmlParsingService = new XmlParsingService();
    }

    [RelayCommand]
    public async Task LoadFavoritesAsync(int page = 1)
    {
        try
        {
            IsLoading = true;
            ErrorMessage = string.Empty;
            CurrentPage = page;

            Debug.WriteLine($"📥 开始加载收藏（第 {page} 页）...");

            var html = await _apiService.GetMyFavoritePageHtmlAsync(page);

            if (string.IsNullOrEmpty(html))
            {
                ErrorMessage = "无法加载收藏页面";
                Debug.WriteLine("❌ 收藏页面HTML为空");
                return;
            }

            // 检查未登录提示
            var error = _xmlParsingService.ExtractErrorMessageFromResponse(html);
            if (!string.IsNullOrEmpty(error))
            {
                ErrorMessage = error;
                Favorites.Clear();
                HasFavorites = false;
                return;
            }

            // 提取formhash以供后续删除操作使用
            _currentFormHash = _xmlParsingService.ExtractFormHashFromFavoritePage(html);
            Debug.WriteLine($"📄 从收藏页面提取formhash: {_currentFormHash}");

            var list = _xmlParsingService.ParseFavorites(html);
            Favorites.Clear();
            foreach (var f in list)
                Favorites.Add(f);

            HasFavorites = Favorites.Count > 0;

            CanGoToPreviousPage = CurrentPage > 1;
            CanGoToNextPage = list.Count > 0;

            Debug.WriteLine($"✅ 成功加载 {Favorites.Count} 个收藏");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"加载收藏失败: {ex.Message}";
            Debug.WriteLine($"❌ 加载收藏错误: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task SelectFavoriteAsync(FavoriteItem item)
    {
        if (item == null || string.IsNullOrEmpty(item.ThreadId))
            return;

        await _navigationService.NavigateToAsync("threadcontent", new Dictionary<string, object>
        {
            { "threadId", item.ThreadId },
            { "threadTitle", item.Title }
        });
    }

    [RelayCommand]
    public async Task GoToPreviousPageAsync()
    {
        if (CurrentPage > 1)
            await LoadFavoritesAsync(CurrentPage - 1);
    }

    [RelayCommand]
    public async Task GoToNextPageAsync()
    {
        await LoadFavoritesAsync(CurrentPage + 1);
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        await LoadFavoritesAsync(CurrentPage);
    }

    [RelayCommand]
    public async Task DeleteSelectedAsync()
    {
        var selected = Favorites.Where(f => f.IsSelected).ToList();
        if (selected.Count == 0)
        {
            ErrorMessage = "请先选择要删除的收藏";
            return;
        }

        if (string.IsNullOrEmpty(_currentFormHash))
        {
            ErrorMessage = "无法删除收藏，请重新加载页面";
            Debug.WriteLine("❌ formhash为空，无法删除");
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;

        int successCount = 0;
        int failCount = 0;

        foreach (var item in selected)
        {
            try
            {
                if (string.IsNullOrEmpty(item.HandleKey))
                {
                    Debug.WriteLine($"⚠️ 跳过收藏 {item.FavId}：handlekey为空");
                    failCount++;
                    continue;
                }

                Debug.WriteLine($"🗑️ 开始删除收藏 (favId={item.FavId}, handlekey={item.HandleKey})...");

                var response = await _apiService.DeleteFavoriteAsync(item.FavId, _currentFormHash, item.HandleKey);

                if (_xmlParsingService.IsDeleteFavoriteSuccess(response))
                {
                    Debug.WriteLine($"✅ 删除成功：{item.Title}");
                    Favorites.Remove(item);
                    successCount++;
                }
                else
                {
                    var errorMsg = _xmlParsingService.ExtractDeleteErrorMessage(response);
                    Debug.WriteLine($"❌ 删除失败：{item.Title} - {errorMsg}");
                    failCount++;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ 删除收藏异常: {ex.Message}");
                failCount++;
            }
        }

        HasFavorites = Favorites.Count > 0;

        // 显示删除结果
        if (successCount > 0 && failCount == 0)
        {
            ErrorMessage = $"成功删除 {successCount} 个收藏";
            Debug.WriteLine($"📊 删除完成：成功 {successCount} 个");
        }
        else if (successCount > 0 && failCount > 0)
        {
            ErrorMessage = $"成功删除 {successCount} 个，失败 {failCount} 个";
            Debug.WriteLine($"📊 删除完成：成功 {successCount} 个，失败 {failCount} 个");
        }
        else if (failCount > 0)
        {
            ErrorMessage = $"删除失败（{failCount} 个）";
            Debug.WriteLine($"📊 删除完成：全部失败 ({failCount} 个)");
        }

        IsLoading = false;
    }

    partial void OnAllSelectedCheckChanged(bool value)
    {
        if (value)
        {
            var lists = Favorites.ToList();
            lists.ForEach(item =>
            {
                item.IsSelected = true;
            });
            Favorites = new ObservableCollection<FavoriteItem>(lists);
        }
        else
        {
            var lists = Favorites.ToList();
            lists.ForEach(item =>
            {
                item.IsSelected = false;
            });
            Favorites = new ObservableCollection<FavoriteItem>(lists);
        }
    }
}
