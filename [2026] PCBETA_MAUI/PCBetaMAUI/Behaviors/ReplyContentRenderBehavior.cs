using Microsoft.Maui.Controls;
using PCBetaMAUI.Models;
using PCBetaMAUI.Services;
using System.Diagnostics;

namespace PCBetaMAUI.Behaviors;

/// <summary>
/// 回帖内容动态渲染行为
/// 当回帖项的数据绑定上下文改变时，自动渲染其 ContentElements 到容器中
/// 这样可以正确显示附件等富文本元素
/// </summary>
public class ReplyContentRenderBehavior : Behavior<VerticalStackLayout>
{
    protected override void OnAttachedTo(VerticalStackLayout bindable)
    {
        bindable.BindingContextChanged += OnBindingContextChanged;
        base.OnAttachedTo(bindable);
    }

    protected override void OnDetachingFrom(VerticalStackLayout bindable)
    {
        bindable.BindingContextChanged -= OnBindingContextChanged;
        base.OnDetachingFrom(bindable);
    }

    private void OnBindingContextChanged(object? sender, EventArgs e)
    {
        try
        {
            if (sender is not VerticalStackLayout container)
                return;

            var replyInfo = container.BindingContext as ReplyInfo;
            if (replyInfo == null)
            {
                Debug.WriteLine("⚠️ 回帖绑定上下文不是 ReplyInfo");
                return;
            }

            // 清空容器中的动态内容（保留 XAML 中定义的静态元素）
            // 只清除之前动态添加的内容
            var dynamicElements = container.Children
                .Where(c => c is VerticalStackLayout vsl && vsl.ClassId == "reply_dynamic_content")
                .ToList();
            foreach (var element in dynamicElements)
            {
                container.Children.Remove(element);
            }

            // 如果有 ContentElements，动态渲染
            if (replyInfo.ContentElements != null && replyInfo.ContentElements.Count > 0)
            {
                Debug.WriteLine($"🎨 动态渲染回帖内容: {replyInfo.ContentElements.Count} 个元素");

                // 创建一个新的容器用于放置动态渲染的内容
                var dynamicContainer = new VerticalStackLayout
                {
                    Spacing = 5,
                    Padding = new Thickness(0, 8, 0, 0),
                    ClassId = "reply_dynamic_content"  // 标记为动态内容，便于清理
                };

                // 调用渲染器
                ContentElementRenderer.RenderContentElements(
                    dynamicContainer,
                    replyInfo.ContentElements,
                    string.Empty  // 这里可以从 ThreadContentPage 传入 referer，或使用 ViewModel 中的值
                );

                // 将动态容器插入到适当位置（在编辑状态之后，在操作按钮之前）
                // 查找要插入的位置：应该在 EditStatus Label 之后，操作按钮 Grid 之前
                int insertIndex = 0;
                for (int i = 0; i < container.Children.Count; i++)
                {
                    var child = container.Children[i];

                    // 如果找到了操作按钮 Grid（ClassId="reply_actions"），就在它之前插入
                    if (child is Grid grid && grid.ClassId == "reply_actions")
                    {
                        insertIndex = i;
                        break;
                    }
                }

                // 默认插入位置：在所有静态元素之后
                if (insertIndex == 0)
                {
                    insertIndex = container.Children.Count;
                }

                container.Insert(insertIndex, dynamicContainer);
                Debug.WriteLine($"✓ 回帖动态内容已插入到位置 {insertIndex}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 渲染回帖内容错误: {ex.Message}\n{ex.StackTrace}");
        }
    }
}
