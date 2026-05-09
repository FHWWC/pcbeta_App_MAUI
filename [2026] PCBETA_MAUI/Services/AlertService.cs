namespace PCBetaMAUI.Services;

/// <summary>
/// 服务接口：用于显示系统弹窗（AlertDialog）
/// </summary>
public interface IAlertService
{
    /// <summary>
    /// 显示简单的警告/确认弹窗
    /// </summary>
    /// <param name="title">弹窗标题</param>
    /// <param name="message">弹窗消息</param>
    /// <param name="cancel">取消按钮文本</param>
    /// <returns>用户是否点击了确定</returns>
    Task<bool> DisplayAlertAsync(string title, string message, string cancel = "确定");

    /// <summary>
    /// 显示确认弹窗（有确定和取消两个按钮）
    /// </summary>
    /// <param name="title">弹窗标题</param>
    /// <param name="message">弹窗消息</param>
    /// <param name="accept">确定按钮文本</param>
    /// <param name="cancel">取消按钮文本</param>
    /// <returns>用户是否点击了确定</returns>
    Task<bool> DisplayConfirmAsync(string title, string message, string accept = "确定", string cancel = "取消");

    /// <summary>
    /// 显示选项菜单弹窗
    /// </summary>
    /// <param name="title">弹窗标题</param>
    /// <param name="message">弹窗消息</param>
    /// <param name="buttons">选项按钮文本数组</param>
    /// <param name="cancel">取消按钮文本（可选）</param>
    /// <returns>用户选择的按钮索引，或 -1 如果取消</returns>
    Task<string?> DisplayActionSheetAsync(string title, string cancel, params string[] buttons);
}

/// <summary>
/// AlertService 实现：使用 MAUI 的 DisplayAlert 和 DisplayActionSheet
/// </summary>
public class AlertService : IAlertService
{
    public async Task<bool> DisplayAlertAsync(string title, string message, string cancel = "确定")
    {
        if (Application.Current?.MainPage == null)
        {
            return false;
        }

        await Application.Current.MainPage.DisplayAlert(title, message, cancel);
        return true;
    }

    public async Task<bool> DisplayConfirmAsync(string title, string message, string accept = "确定", string cancel = "取消")
    {
        if (Application.Current?.MainPage == null)
        {
            return false;
        }

        return await Application.Current.MainPage.DisplayAlert(title, message, accept, cancel);
    }

    public async Task<string?> DisplayActionSheetAsync(string title, string cancel, params string[] buttons)
    {
        if (Application.Current?.MainPage == null)
        {
            return null;
        }

        return await Application.Current.MainPage.DisplayActionSheet(title, cancel, null, buttons);
    }
}
