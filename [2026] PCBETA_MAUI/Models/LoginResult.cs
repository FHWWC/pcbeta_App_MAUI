namespace PCBetaMAUI.Models;

/// <summary>
/// 登录结果 - 返回登录状态和具体信息
/// </summary>
public class LoginResult
{
    /// <summary>
    /// 登录是否成功
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 错误或状态消息
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 创建成功的登录结果
    /// </summary>
    public static LoginResult Success()
    {
        return new LoginResult { IsSuccess = true, Message = "登录成功" };
    }

    /// <summary>
    /// 创建失败的登录结果
    /// </summary>
    public static LoginResult Failure(string message)
    {
        return new LoginResult { IsSuccess = false, Message = message };
    }
}
