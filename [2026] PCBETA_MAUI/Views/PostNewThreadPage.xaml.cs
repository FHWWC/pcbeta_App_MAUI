using PCBetaMAUI.ViewModels;
using System.Diagnostics;

namespace PCBetaMAUI.Views;

/// <summary>
/// 发新帖页面 - 实现 IQueryAttributable 接口以接收 Shell 查询参数
/// </summary>
public partial class PostNewThreadPage : ContentPage, IQueryAttributable
{
    private string _boardId = string.Empty;
    private string _boardName = string.Empty;
    private bool _isInitialized = false;

    public PostNewThreadPage()
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
            Debug.WriteLine($"📝 PostNewThreadPage ApplyQueryAttributes 被调用");
            Debug.WriteLine($"   参数数量: {query.Count}");

            if (query.ContainsKey("boardId"))
            {
                _boardId = Uri.UnescapeDataString(query["boardId"].ToString() ?? "");
                Debug.WriteLine($"✅ 从查询参数获取 BoardId: {_boardId}");
            }

            if (query.ContainsKey("boardName"))
            {
                _boardName = Uri.UnescapeDataString(query["boardName"].ToString() ?? "");
                Debug.WriteLine($"✅ 从查询参数获取 BoardName: {_boardName}");
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
        Debug.WriteLine($"📝 PostNewThreadPage OnNavigatedTo");

        // 在 OnNavigatedTo 中初始化 ViewModel（此时 BindingContext 应该已经被设置）
        if (!_isInitialized && !string.IsNullOrEmpty(_boardId))
        {
            try
            {
                // 等待 BindingContext 被设置
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    Debug.WriteLine($"📝 检查 BindingContext 类型: {BindingContext?.GetType().Name}");
                    Debug.WriteLine($"📝 SubmitPostCommand 是否存在: {(BindingContext as PostNewThreadViewModel)?.SubmitPostCommand != null}");

                    if (BindingContext is PostNewThreadViewModel viewModel)
                    {
                        Debug.WriteLine($"🔄 初始化 PostNewThreadViewModel (boardId={_boardId}, boardName={_boardName})");

                        // 初始化 ViewModel
                        viewModel.Initialize(_boardId, _boardName);

                        // 异步初始化（获取发帖编辑页面参数等）
                        await viewModel.InitializePostPageAsync(_boardId);

                        _isInitialized = true;
                        Debug.WriteLine($"✅ PostNewThreadViewModel 初始化完成");

                        // ✅ 新增：初始化 Picker 绑定
                        InitializeTypeIdPickerBinding(viewModel);
                        // ✅ 新增：初始化其他参数 Picker 绑定
                        InitializeParameterPickerBindings(viewModel);
                    }
                    else
                    {
                        Debug.WriteLine($"⚠️ BindingContext 仍然不是 PostNewThreadViewModel，type={BindingContext?.GetType().Name}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ 初始化 PostNewThreadViewModel 失败: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// ✅ 新增：初始化 TypeId Picker 的数据绑定
    /// </summary>
    private void InitializeTypeIdPickerBinding(PostNewThreadViewModel viewModel)
    {
        if (viewModel?.TypeIdOptions?.Count > 0)
        {
            try
            {
                // 获取选项名称列表
                var itemsSource = viewModel.TypeIdOptions.Select(x => x.name).ToList();
                TypeIdPicker.ItemsSource = itemsSource;
                TypeIdPicker.SelectedIndex = 0;  // 默认选中第一项（"选择主题分类"）
                Debug.WriteLine($"✅ TypeIdPicker 已初始化，包含 {itemsSource.Count} 个选项");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ 初始化 TypeIdPicker 失败: {ex.Message}");
            }
        }
        else
        {
            Debug.WriteLine($"⚠️ TypeIdOptions 为空或为 null");
        }
    }

    /// <summary>
    /// ✅ 新增：初始化其他参数 Picker 的数据绑定（ReadpermPicker、ReplycreditMembertimesPicker、ReplycreditRandomPicker）
    /// </summary>
    private void InitializeParameterPickerBindings(PostNewThreadViewModel viewModel)
    {
        try
        {
            // 1. 初始化 ReadpermPicker（阅读权限 - 动态选项，从 ViewModel 的 ReadpermOptions 获取）
            if (viewModel?.ReadpermOptions?.Count > 0)
            {
                try
                {
                    var readpermNames = viewModel.ReadpermOptions.Select(x => x.name).ToList();
                    ReadpermPicker.ItemsSource = readpermNames;

                    // 查找当前选择的索引（从 ViewModel.Readperm 值）
                    int selectedIndex = viewModel.ReadpermOptions.FindIndex(x => x.id == viewModel.Readperm);
                    ReadpermPicker.SelectedIndex = Math.Max(0, selectedIndex);  // 如果未找到则选中第一项

                    Debug.WriteLine($"✅ ReadpermPicker 已初始化，包含 {readpermNames.Count} 个选项，当前选择索引: {ReadpermPicker.SelectedIndex}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"❌ 初始化 ReadpermPicker 失败: {ex.Message}");
                }
            }
            else
            {
                Debug.WriteLine($"⚠️ ReadpermOptions 为空或为 null");
            }

            // 2. 初始化 ReplycreditMembertimesPicker（每人最多可获得次数 1-10）
            try
            {
                var membertimesOptions = new List<string>();
                for (int i = 1; i <= 10; i++)
                {
                    membertimesOptions.Add(i.ToString());
                }
                ReplycreditMembertimesPicker.ItemsSource = membertimesOptions;

                // 从 ViewModel 的值中查找匹配的索引
                if (int.TryParse(viewModel.ReplycreditMembertimes, out int membertimesValue))
                {
                    ReplycreditMembertimesPicker.SelectedIndex = Math.Max(0, membertimesValue - 1);  // 1-10 对应 0-9
                }
                else
                {
                    ReplycreditMembertimesPicker.SelectedIndex = 0;  // 默认选中 1
                }

                Debug.WriteLine($"✅ ReplycreditMembertimesPicker 已初始化，当前选择: {ReplycreditMembertimesPicker.SelectedItem}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ 初始化 ReplycreditMembertimesPicker 失败: {ex.Message}");
            }

            // 3. 初始化 ReplycreditRandomPicker（中奖率 100、90、80、70、60、50、40、30、20、10）
            try
            {
                var randomOptions = new List<string> { "100", "90", "80", "70", "60", "50", "40", "30", "20", "10" };
                ReplycreditRandomPicker.ItemsSource = randomOptions;

                // 从 ViewModel 的值中查找匹配的索引
                int randomIndex = randomOptions.IndexOf(viewModel.ReplycreditRandom);
                ReplycreditRandomPicker.SelectedIndex = Math.Max(0, randomIndex);  // 如果未找到则选中第一项

                Debug.WriteLine($"✅ ReplycreditRandomPicker 已初始化，当前选择: {ReplycreditRandomPicker.SelectedItem}%");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ 初始化 ReplycreditRandomPicker 失败: {ex.Message}");
            }

            Debug.WriteLine($"✅ 所有参数 Picker 初始化完成");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 初始化参数 Picker 失败: {ex.Message}");
        }
    }

    /// <summary>
    /// ✅ 新增：处理 TypeId Picker 选择事件
    /// </summary>
    private void OnTypeIdPickerSelectedIndexChanged(object sender, EventArgs e)
    {
        try
        {
            if (BindingContext is PostNewThreadViewModel viewModel)
            {
                if (TypeIdPicker.SelectedIndex >= 0 && viewModel.TypeIdOptions != null)
                {
                    var selected = viewModel.TypeIdOptions[TypeIdPicker.SelectedIndex];
                    viewModel.SelectedTypeId = selected.id;
                    Debug.WriteLine($"✅ 已选择主题分类: id={selected.id}, name={selected.name}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 处理 TypeIdPicker 选择失败: {ex.Message}");
        }
    }

    /// <summary>
    /// ✅ 新增：处理 ReadpermPicker 选择事件
    /// </summary>
    private void OnReadpermPickerSelectedIndexChanged(object sender, EventArgs e)
    {
        try
        {
            if (BindingContext is PostNewThreadViewModel viewModel)
            {
                if (ReadpermPicker.SelectedIndex >= 0 && viewModel.ReadpermOptions != null && viewModel.ReadpermOptions.Count > ReadpermPicker.SelectedIndex)
                {
                    var selected = viewModel.ReadpermOptions[ReadpermPicker.SelectedIndex];
                    viewModel.Readperm = selected.id;
                    Debug.WriteLine($"✅ 已选择阅读权限: id={selected.id}, name={selected.name}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 处理 ReadpermPicker 选择失败: {ex.Message}");
        }
    }

    /// <summary>
    /// ✅ 新增：处理 ReplycreditMembertimesPicker 选择事件
    /// </summary>
    private void OnReplycreditMembertimesPickerSelectedIndexChanged(object sender, EventArgs e)
    {
        try
        {
            if (BindingContext is PostNewThreadViewModel viewModel)
            {
                if (ReplycreditMembertimesPicker.SelectedIndex >= 0)
                {
                    // 索引 0-9 对应值 1-10
                    string selectedValue = (ReplycreditMembertimesPicker.SelectedIndex + 1).ToString();
                    viewModel.ReplycreditMembertimes = selectedValue;
                    Debug.WriteLine($"✅ 已选择每人最多可获得次数: {selectedValue}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 处理 ReplycreditMembertimesPicker 选择失败: {ex.Message}");
        }
    }

    /// <summary>
    /// ✅ 新增：处理 ReplycreditRandomPicker 选择事件
    /// </summary>
    private void OnReplycreditRandomPickerSelectedIndexChanged(object sender, EventArgs e)
    {
        try
        {
            if (BindingContext is PostNewThreadViewModel viewModel)
            {
                if (ReplycreditRandomPicker.SelectedIndex >= 0 && ReplycreditRandomPicker.SelectedItem != null)
                {
                    string selectedValue = ReplycreditRandomPicker.SelectedItem.ToString() ?? "100";
                    viewModel.ReplycreditRandom = selectedValue;
                    Debug.WriteLine($"✅ 已选择中奖率: {selectedValue}%");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 处理 ReplycreditRandomPicker 选择失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 发送按钮点击事件处理器 - 用于测试命令绑定
    /// </summary>
    private void OnSubmitButtonClicked(object? sender, EventArgs e)
    {
        Debug.WriteLine($"📝 OnSubmitButtonClicked 被触发");

        if (BindingContext is PostNewThreadViewModel viewModel)
        {
            Debug.WriteLine($"📝 ViewModel 已找到，尝试执行 SubmitPostCommand");
            Debug.WriteLine($"📝 SubmitPostCommand != null: {viewModel.SubmitPostCommand != null}");
            Debug.WriteLine($"📝 SubmitPostCommand.CanExecute(null): {viewModel.SubmitPostCommand?.CanExecute(null)}");

            // 尝试执行命令
            if (viewModel.SubmitPostCommand?.CanExecute(null) == true)
            {
                Debug.WriteLine($"🚀 执行 SubmitPostCommand");
                viewModel.SubmitPostCommand?.Execute(null);
            }
            else
            {
                Debug.WriteLine($"❌ SubmitPostCommand 无法执行 (CanExecute=false)");
            }
        }
        else
        {
            Debug.WriteLine($"❌ BindingContext 不是 PostNewThreadViewModel");
        }
    }
}
