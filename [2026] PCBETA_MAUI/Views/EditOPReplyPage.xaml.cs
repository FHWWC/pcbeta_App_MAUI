using PCBetaMAUI.ViewModels;
using System.Diagnostics;

namespace PCBetaMAUI.Views;

/// <summary>
/// 编辑楼主发帖页面 - 实现 IQueryAttributable 接口以接收 Shell 查询参数
/// </summary>
public partial class EditOPReplyPage : ContentPage, IQueryAttributable
{
    private string _boardId = string.Empty;
    private string _threadId = string.Empty;
    private string _postId = string.Empty;
    private string _boardName = string.Empty;
    private string _threadTitle = string.Empty;
    private bool _isInitialized = false;

    public EditOPReplyPage()
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
            Debug.WriteLine($"📝 EditOPReplyPage ApplyQueryAttributes 被调用");
            Debug.WriteLine($"   参数数量: {query.Count}");

            if (query.ContainsKey("boardId"))
            {
                _boardId = Uri.UnescapeDataString(query["boardId"].ToString() ?? "");
                Debug.WriteLine($"✅ 从查询参数获取 BoardId: {_boardId}");
            }

            if (query.ContainsKey("threadId"))
            {
                _threadId = Uri.UnescapeDataString(query["threadId"].ToString() ?? "");
                Debug.WriteLine($"✅ 从查询参数获取 ThreadId: {_threadId}");
            }

            if (query.ContainsKey("postId"))
            {
                _postId = Uri.UnescapeDataString(query["postId"].ToString() ?? "");
                Debug.WriteLine($"✅ 从查询参数获取 PostId: {_postId}");
            }

            if (query.ContainsKey("boardName"))
            {
                _boardName = Uri.UnescapeDataString(query["boardName"].ToString() ?? "");
                Debug.WriteLine($"✅ 从查询参数获取 BoardName: {_boardName}");
            }

            if (query.ContainsKey("threadTitle"))
            {
                _threadTitle = Uri.UnescapeDataString(query["threadTitle"].ToString() ?? "");
                Debug.WriteLine($"✅ 从查询参数获取 ThreadTitle: {_threadTitle}");
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
        Debug.WriteLine($"📝 EditOPReplyPage OnNavigatedTo");

        // 在 OnNavigatedTo 中初始化 ViewModel
        if (!_isInitialized && !string.IsNullOrEmpty(_boardId) && !string.IsNullOrEmpty(_threadId) && !string.IsNullOrEmpty(_postId))
        {
            try
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    Debug.WriteLine($"📝 检查 BindingContext 类型: {BindingContext?.GetType().Name}");

                    if (BindingContext is EditOPReplyViewModel viewModel)
                    {
                        Debug.WriteLine($"🔄 初始化 EditOPReplyViewModel (fid={_boardId}, tid={_threadId}, pid={_postId})");

                        // 初始化 ViewModel
                        viewModel.Initialize(_boardId, _threadId, _postId, _boardName, _threadTitle);

                        // 异步加载编辑页面数据
                        await viewModel.LoadEditPageDataAsync();

                        _isInitialized = true;
                        Debug.WriteLine($"✅ EditOPReplyViewModel 初始化完成");

                        // 初始化 Picker 绑定
                        InitializeTypeIdPickerBinding(viewModel);
                        InitializeParameterPickerBindings(viewModel);
                    }
                    else
                    {
                        Debug.WriteLine($"⚠️ BindingContext 仍然不是 EditOPReplyViewModel，type={BindingContext?.GetType().Name}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ 初始化 EditOPReplyViewModel 失败: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 初始化 TypeId Picker 的数据绑定
    /// </summary>
    private void InitializeTypeIdPickerBinding(EditOPReplyViewModel viewModel)
    {
        if (viewModel?.TypeIdOptions?.Count > 0)
        {
            try
            {
                var itemsSource = viewModel.TypeIdOptions.Select(x => x.name).ToList();
                TypeIdPicker.ItemsSource = itemsSource;

                // 查找当前选择的索引
                int selectedIndex = viewModel.TypeIdOptions.FindIndex(x => x.id == viewModel.SelectedTypeId);
                TypeIdPicker.SelectedIndex = Math.Max(0, selectedIndex);

                Debug.WriteLine($"✅ TypeIdPicker 已初始化，包含 {itemsSource.Count} 个选项，当前选择: {viewModel.SelectedTypeId}");
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
    /// 初始化其他参数 Picker 的数据绑定
    /// </summary>
    private void InitializeParameterPickerBindings(EditOPReplyViewModel viewModel)
    {
        try
        {
            // 1. 初始化 ReadpermPicker（阅读权限）
            if (viewModel?.ReadpermOptions?.Count > 0)
            {
                try
                {
                    var readpermNames = viewModel.ReadpermOptions.Select(x => x.name).ToList();
                    ReadpermPicker.ItemsSource = readpermNames;

                    int selectedIndex = viewModel.ReadpermOptions.FindIndex(x => x.id == viewModel.Readperm);
                    ReadpermPicker.SelectedIndex = Math.Max(0, selectedIndex);

                    Debug.WriteLine($"✅ ReadpermPicker 已初始化，包含 {readpermNames.Count} 个选项");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"❌ 初始化 ReadpermPicker 失败: {ex.Message}");
                }
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

                if (int.TryParse(viewModel.ReplycreditMembertimes, out int membertimesValue))
                {
                    ReplycreditMembertimesPicker.SelectedIndex = Math.Max(0, membertimesValue - 1);
                }
                else
                {
                    ReplycreditMembertimesPicker.SelectedIndex = 0;
                }

                Debug.WriteLine($"✅ ReplycreditMembertimesPicker 已初始化");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ 初始化 ReplycreditMembertimesPicker 失败: {ex.Message}");
            }

            // 3. 初始化 ReplycreditRandomPicker（中奖率）
            try
            {
                var randomOptions = new List<string> { "100", "90", "80", "70", "60", "50", "40", "30", "20", "10" };
                ReplycreditRandomPicker.ItemsSource = randomOptions;

                int randomIndex = randomOptions.IndexOf(viewModel.ReplycreditRandom);
                ReplycreditRandomPicker.SelectedIndex = Math.Max(0, randomIndex);

                Debug.WriteLine($"✅ ReplycreditRandomPicker 已初始化");
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
    /// TypeId Picker 选项变更事件
    /// </summary>
    private void OnTypeIdPickerSelectedIndexChanged(object? sender, EventArgs e)
    {
        try
        {
            if (BindingContext is EditOPReplyViewModel viewModel && TypeIdPicker.ItemsSource is List<string> itemsSource)
            {
                int selectedIndex = TypeIdPicker.SelectedIndex;
                if (selectedIndex >= 0 && selectedIndex < viewModel.TypeIdOptions.Count)
                {
                    var selectedOption = viewModel.TypeIdOptions[selectedIndex];
                    viewModel.SelectedTypeId = selectedOption.id;
                    viewModel.TypeId = selectedOption.id;
                    Debug.WriteLine($"✅ TypeId 已更改为: {selectedOption.id} ({selectedOption.name})");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ TypeId Picker 选项变更失败: {ex.Message}");
        }
    }

    /// <summary>
    /// Readperm Picker 选项变更事件
    /// </summary>
    private void OnReadpermPickerSelectedIndexChanged(object? sender, EventArgs e)
    {
        try
        {
            if (BindingContext is EditOPReplyViewModel viewModel)
            {
                int selectedIndex = ReadpermPicker.SelectedIndex;
                if (selectedIndex >= 0 && selectedIndex < viewModel.ReadpermOptions.Count)
                {
                    var selectedOption = viewModel.ReadpermOptions[selectedIndex];
                    viewModel.Readperm = selectedOption.id;
                    Debug.WriteLine($"✅ Readperm 已更改为: {selectedOption.id} ({selectedOption.name})");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Readperm Picker 选项变更失败: {ex.Message}");
        }
    }

    /// <summary>
    /// ReplycreditMembertimes Picker 选项变更事件
    /// </summary>
    private void OnReplycreditMembertimesPickerSelectedIndexChanged(object? sender, EventArgs e)
    {
        try
        {
            if (BindingContext is EditOPReplyViewModel viewModel && ReplycreditMembertimesPicker.SelectedItem is string selectedValue)
            {
                viewModel.ReplycreditMembertimes = selectedValue;
                Debug.WriteLine($"✅ ReplycreditMembertimes 已更改为: {selectedValue}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ ReplycreditMembertimes Picker 选项变更失败: {ex.Message}");
        }
    }

    /// <summary>
    /// ReplycreditRandom Picker 选项变更事件
    /// </summary>
    private void OnReplycreditRandomPickerSelectedIndexChanged(object? sender, EventArgs e)
    {
        try
        {
            if (BindingContext is EditOPReplyViewModel viewModel && ReplycreditRandomPicker.SelectedItem is string selectedValue)
            {
                viewModel.ReplycreditRandom = selectedValue;
                Debug.WriteLine($"✅ ReplycreditRandom 已更改为: {selectedValue}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ ReplycreditRandom Picker 选项变更失败: {ex.Message}");
        }
    }
}
