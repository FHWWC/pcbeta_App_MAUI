using PCBetaMAUI.ViewModels;

namespace PCBetaMAUI.Views;

public partial class LoginPage : ContentPage
{
    private readonly List<string> _questionIds = new() { "0", "1", "2", "3", "4", "5", "6", "7" };

    private readonly Dictionary<string, string> _questionOptions = new()
    {
        { "0", "未设置请忽略" },
        { "1", "母亲的名字" },
        { "2", "爷爷的名字" },
        { "3", "父亲出生的城市" },
        { "4", "您其中一位老师的名字" },
        { "5", "您个人计算机的型号" },
        { "6", "您最喜欢的餐馆名称" },
        { "7", "驾驶执照最后四位数字" }
    };

    public LoginPage()
    {
        InitializeComponent();
        InitializeQuestionPicker();
    }

    private void InitializeQuestionPicker()
    {
        var questions = new List<string>
        {
            "未设置请忽略",
            "母亲的名字",
            "爷爷的名字",
            "父亲出生的城市",
            "您其中一位老师的名字",
            "您个人计算机的型号",
            "您最喜欢的餐馆名称",
            "驾驶执照最后四位数字"
        };

        QuestionPicker.ItemsSource = questions;
        QuestionPicker.SelectedIndexChanged += OnQuestionPickerSelectedIndexChanged;
    }

    private void OnQuestionPickerSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (BindingContext is LoginViewModel viewModel)
        {
            // Update SelectedQuestionId based on SelectedIndex
            if (QuestionPicker.SelectedIndex >= 0)
            {
                viewModel.SelectedQuestionId = QuestionPicker.SelectedIndex.ToString();
            }
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is LoginViewModel viewModel)
        {
            await viewModel.InitializeAsync();
        }
    }
}