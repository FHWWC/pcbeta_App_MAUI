using System.Diagnostics;

namespace PCBetaMAUI.Services;

/// <summary>
/// Interface for application navigation
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// Navigates to a page by name
    /// </summary>
    Task NavigateToAsync(string pageName, Dictionary<string, object>? parameters = null);

    /// <summary>
    /// Goes back to the previous page
    /// </summary>
    Task GoBackAsync();

    /// <summary>
    /// Clears the navigation stack and navigates to a page
    /// </summary>
    Task NavigateToAsyncClearStack(string pageName, Dictionary<string, object>? parameters = null);
}

/// <summary>
/// Implementation of navigation service using MAUI Shell
/// </summary>
public class NavigationService : INavigationService
{
    public async Task NavigateToAsync(string pageName, Dictionary<string, object>? parameters = null)
    {
        try
        {
            var navigationParameter = parameters != null ? BuildQueryString(parameters) : "";

            // 构建路由 - 对于特定页面使用相对路由
            string route;

            // 对于 replythread、login、postnewthread、search、myprofile 使用相对导航，避免 Shell 堆栈问题
            if (pageName.Equals("replythread", StringComparison.OrdinalIgnoreCase)
                || pageName.Equals("editreply", StringComparison.OrdinalIgnoreCase)
                || pageName.Equals("editopreply", StringComparison.OrdinalIgnoreCase)
                || pageName.Equals("login", StringComparison.OrdinalIgnoreCase)
                || pageName.Equals("postnewthread", StringComparison.OrdinalIgnoreCase)
                || pageName.Equals("search", StringComparison.OrdinalIgnoreCase)
                || pageName.Equals("myprofile", StringComparison.OrdinalIgnoreCase))
            {
                // 使用相对导航
                route = string.IsNullOrEmpty(navigationParameter)
                    ? pageName
                    : $"{pageName}?{navigationParameter}";
            }
            else
            {
                // 对于其他页面使用原有逻辑
                route = string.IsNullOrEmpty(navigationParameter) 
                    ? $"///{pageName}" 
                    : $"///{pageName}?{navigationParameter}";

                if(route.StartsWith("///threadlist", StringComparison.OrdinalIgnoreCase)
                    || route.StartsWith("///threadcontent", StringComparison.OrdinalIgnoreCase))
                {
                    route = string.IsNullOrEmpty(navigationParameter)
                        ? $"/{pageName}"
                        : $"/{pageName}?{navigationParameter}";
                }
            }

            Debug.WriteLine($"🔗 导航路由: {route}");
            await Shell.Current.GoToAsync(route);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Navigation error to {pageName}: {ex.Message}");
            throw;
        }
    }

    public async Task GoBackAsync()
    {
        try
        {
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Navigation back error: {ex.Message}");
            throw;
        }
    }

    public async Task NavigateToAsyncClearStack(string pageName, Dictionary<string, object>? parameters = null)
    {
        try
        {
            var navigationParameter = parameters != null ? BuildQueryString(parameters) : "";
            var route = string.IsNullOrEmpty(navigationParameter)
                ? $"///{pageName}"
                : $"///{pageName}?{navigationParameter}";

            await Shell.Current.GoToAsync(route);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Navigation error to {pageName}: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Builds query string from parameters dictionary
    /// </summary>
    private string BuildQueryString(Dictionary<string, object> parameters)
    {
        try
        {
            var queryParams = parameters
                .Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value?.ToString() ?? "")}")
                .ToList();

            return string.Join("&", queryParams);
        }
        catch
        {
            return "";
        }
    }
}
