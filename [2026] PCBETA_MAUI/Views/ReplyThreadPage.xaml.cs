using PCBetaMAUI.ViewModels;
using System.Diagnostics;

namespace PCBetaMAUI.Views;

/// <summary>
/// 回帖页面 - 实现 IQueryAttributable 接口以接收 Shell 查询参数
/// </summary>
public partial class ReplyThreadPage : ContentPage, IQueryAttributable
{
    private string _threadId = string.Empty;
    private string _forumId = string.Empty;
    private string _threadTitle = string.Empty;
    private string _repquote = string.Empty;  // ✅ 新增：评论回复ID
    private string _userReplyContent = string.Empty;  // ✅ 新增：用户评论内容
    private string _author = string.Empty;  // ✅ 改进：评论者用户名
    private string _postTime = string.Empty;  // ✅ 改进：发表时间
    private bool _isInitialized = false;

    public ReplyThreadPage()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 实现 IQueryAttributable 接口 - 接收来自 Shell 导航的查询参数
    /// 注意：这个方法在 BindingContext 设置之前被调用，所以只保存参数
    /// ✅ 改进：添加对 author 和 postTime 参数的接收
    /// </summary>
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        try
        {
            Debug.WriteLine($"📝 ReplyThreadPage ApplyQueryAttributes 被调用");
            Debug.WriteLine($"   参数数量: {query.Count}");

            if (query.ContainsKey("threadId"))
            {
                _threadId = Uri.UnescapeDataString(query["threadId"].ToString() ?? "");
                Debug.WriteLine($"✅ 从查询参数获取 ThreadId: {_threadId}");
            }

            if (query.ContainsKey("forumId"))
            {
                _forumId = Uri.UnescapeDataString(query["forumId"].ToString() ?? "");
                Debug.WriteLine($"✅ 从查询参数获取 ForumId: {_forumId}");
            }

            if (query.ContainsKey("threadTitle"))
            {
                _threadTitle = Uri.UnescapeDataString(query["threadTitle"].ToString() ?? "");
                Debug.WriteLine($"✅ 从查询参数获取 ThreadTitle: {_threadTitle}");
            }

            // ✅ 新增：提取评论回复ID参数
            if (query.ContainsKey("repquote"))
            {
                _repquote = Uri.UnescapeDataString(query["repquote"].ToString() ?? "");
                Debug.WriteLine($"✅ 从查询参数获取 Repquote: {_repquote}");
            }

            if (query.ContainsKey("userReplyContent"))
            {
                _userReplyContent = Uri.UnescapeDataString(query["userReplyContent"].ToString() ?? "");
                Debug.WriteLine($"✅ 从查询参数获取 UserReplyContent: {_userReplyContent}");
            }
            else
            {
                _repquote = string.Empty;
                Debug.WriteLine($"ℹ️ 未提供 Repquote 参数 - 这是普通的线程回复");
            }

            // ✅ 改进：提取评论者用户名
            if (query.ContainsKey("author"))
            {
                _author = Uri.UnescapeDataString(query["author"].ToString() ?? "");
                Debug.WriteLine($"✅ 从查询参数获取 Author: {_author}");
            }

            // ✅ 改进：提取发表时间
            if (query.ContainsKey("postTime"))
            {
                _postTime = Uri.UnescapeDataString(query["postTime"].ToString() ?? "");
                Debug.WriteLine($"✅ 从查询参数获取 PostTime: {_postTime}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ ApplyQueryAttributes 失败: {ex.Message}");
        }
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        Debug.WriteLine($"📝 ReplyThreadPage OnNavigatedTo");

        // 在 OnNavigatedTo 中初始化 ViewModel（此时 BindingContext 应该已经被设置）
        if (!_isInitialized && !string.IsNullOrEmpty(_threadId))
        {
            try
            {
                // 等待 BindingContext 被设置
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    Debug.WriteLine($"📝 检查 BindingContext 类型: {BindingContext?.GetType().Name}");
                    Debug.WriteLine($"📝 SubmitReplyCommand 是否存在: {(BindingContext as ReplyThreadViewModel)?.SubmitReplyCommand != null}");

                    if (BindingContext is ReplyThreadViewModel viewModel)
                    {
                        Debug.WriteLine($"🔄 初始化 ReplyThreadViewModel (fid={_forumId}, tid={_threadId}, title={_threadTitle}, repquote={_repquote}, author={_author}, postTime={_postTime})");

                        // 第一步：同步初始化（设置基本信息和新的参数）
                        viewModel.Initialize(_forumId, _threadId, _threadTitle, _repquote, _userReplyContent, _author, _postTime);

                        // 第二步：异步初始化（获取回帖编辑页面并提取参数）
                        await viewModel.InitializeReplyPageAsync(_forumId, _threadId, _repquote);

                        _isInitialized = true;
                        Debug.WriteLine($"✅ ReplyThreadViewModel 初始化完成");
                    }
                    else
                    {
                        Debug.WriteLine($"⚠️ BindingContext 仍然不是 ReplyThreadViewModel，type={BindingContext?.GetType().Name}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ 初始化 ReplyThreadViewModel 失败: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 发送按钮点击事件处理器 - 用于测试命令绑定
    /// </summary>
    private void OnSubmitButtonClicked(object? sender, EventArgs e)
    {
        Debug.WriteLine($"📝 OnSubmitButtonClicked 被触发");

        if (BindingContext is ReplyThreadViewModel viewModel)
        {
            Debug.WriteLine($"📝 ViewModel 已找到，尝试执行 SubmitReplyCommand");
            Debug.WriteLine($"📝 SubmitReplyCommand != null: {viewModel.SubmitReplyCommand != null}");
            Debug.WriteLine($"📝 SubmitReplyCommand.CanExecute(null): {viewModel.SubmitReplyCommand?.CanExecute(null)}");

            // 尝试执行命令
            if (viewModel.SubmitReplyCommand?.CanExecute(null) == true)
            {
                Debug.WriteLine($"🚀 执行 SubmitReplyCommand");
                viewModel.SubmitReplyCommand?.Execute(null);
            }
            else
            {
                Debug.WriteLine($"❌ SubmitReplyCommand 无法执行 (CanExecute=false)");
            }
        }
        else
        {
            Debug.WriteLine($"❌ BindingContext 不是 ReplyThreadViewModel");
        }
    }
}
