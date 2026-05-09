using PCBetaMAUI.ViewModels;
using System.Diagnostics;

namespace PCBetaMAUI.Views;

/// <summary>
/// 编辑回帖页面 - 实现 IQueryAttributable 接口以接收 Shell 查询参数
/// </summary>
public partial class EditReplyPage : ContentPage, IQueryAttributable
{
    private string _threadId = string.Empty;
    private string _forumId = string.Empty;
    private string _postId = string.Empty;  // ✅ 新增：被编辑的回帖ID
    private string _threadTitle = string.Empty;
    private bool _isInitialized = false;

    public EditReplyPage()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 实现 IQueryAttributable 接口 - 接收来自 Shell 导航的查询参数
    /// </summary>
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        try
        {
            Debug.WriteLine($"📝 EditReplyPage ApplyQueryAttributes 被调用");
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

            if (query.ContainsKey("postId"))
            {
                _postId = Uri.UnescapeDataString(query["postId"].ToString() ?? "");
                Debug.WriteLine($"✅ 从查询参数获取 PostId: {_postId}");
            }

            if (query.ContainsKey("threadTitle"))
            {
                _threadTitle = Uri.UnescapeDataString(query["threadTitle"].ToString() ?? "");
                Debug.WriteLine($"✅ 从查询参数获取 ThreadTitle: {_threadTitle}");
            }

            // BindingContext 设置后会触发 Loaded 事件
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ ApplyQueryAttributes 异常: {ex.Message}");
        }
    }

    /// <summary>
    /// 页面加载完成时初始化数据
    /// </summary>
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!_isInitialized && !string.IsNullOrEmpty(_threadId))
        {
            _isInitialized = true;

            Debug.WriteLine($"📄 EditReplyPage OnAppearing: 开始初始化编辑页面");

            try
            {
                if (BindingContext is EditReplyViewModel viewModel)
                {
                    // 初始化页面信息
                    viewModel.Initialize(_forumId, _threadId, _postId, _threadTitle);

                    // 异步加载编辑页面数据
                    await viewModel.LoadEditPageDataAsync();

                    Debug.WriteLine($"✅ EditReplyPage 初始化完成");
                }
                else
                {
                    Debug.WriteLine($"❌ BindingContext 不是 EditReplyViewModel");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ 初始化异常: {ex.Message}\n{ex.StackTrace}");
                await DisplayAlert("错误", $"初始化失败: {ex.Message}", "确定");
            }
        }
    }

    /// <summary>
    /// 提交按钮点击事件处理
    /// </summary>
    private void OnSubmitButtonClicked(object sender, EventArgs e)
    {
        Debug.WriteLine($"🔘 提交按钮被点击");
    }
}
