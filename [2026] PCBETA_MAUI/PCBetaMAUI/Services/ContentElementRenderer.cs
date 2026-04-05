using Microsoft.Maui.Controls;
using PCBetaMAUI.Models;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace PCBetaMAUI.Services;

/// <summary>
/// 将 ContentElement 动态转换为 MAUI UI 控件
/// </summary>
public static class ContentElementRenderer
{
    /// <summary>
    /// 将 ContentElements 列表渲染为 MAUI 控件，添加到指定的布局容器中
    /// 支持按 HorizontalGroupId 分组的横向排列（来自 ignore_js_op 标签的文本元素）
    /// </summary>
    public static void RenderContentElements(VerticalStackLayout container, List<ContentElement>? elements, string? referer = null)
    {
        if (container == null || elements == null || elements.Count == 0)
            return;

        // 清空容器中的现有内容
        container.Clear();

        // 获取所有不同的 HorizontalGroupId
        var groupIds = elements
            .Where(e => e.HorizontalGroupId != null)
            .Select(e => e.HorizontalGroupId!)
            .Distinct()
            .ToList();

        // 为每个分组 ID 构建一个包含该分组的所有文本元素索引的集合
        var groupElementIndices = new Dictionary<string, HashSet<int>>();
        foreach (var groupId in groupIds)
        {
            groupElementIndices[groupId] = new HashSet<int>();
            for (int i = 0; i < elements.Count; i++)
            {
                if (elements[i].HorizontalGroupId == groupId)
                {
                    groupElementIndices[groupId].Add(i);
                }
            }
        }

        // 追踪已渲染的分组
        var renderedGroups = new HashSet<string>();
        HorizontalStackLayout? currentHorizontalLayout = null;

        for (int i = 0; i < elements.Count; i++)
        {
            var element = elements[i];

            Debug.WriteLine($"🔍 RenderContentElements - 处理第 {i} 个元素: Type={element.Type}, HorizontalGroupId={element.HorizontalGroupId}, FileName={element.FileName}");

            // 如果该元素属于某个分组
            if (element.HorizontalGroupId != null && !renderedGroups.Contains(element.HorizontalGroupId))
            {
                Debug.WriteLine($"✅ 创建新的横向容器，分组ID={element.HorizontalGroupId}");
                // 创建新的横向容器并添加该分组的所有文本元素
                currentHorizontalLayout = new HorizontalStackLayout
                {
                    Spacing = 5,
                    Margin = new Thickness(0, 5),
                    VerticalOptions = LayoutOptions.Center
                };
                container.Add(currentHorizontalLayout);

                // 添加该分组中的所有文本元素
                foreach (var groupElementIndex in groupElementIndices[element.HorizontalGroupId].OrderBy(x => x))
                {
                    var groupElement = elements[groupElementIndex];
                    Debug.WriteLine($"  └─ 添加分组内的元素 {groupElementIndex}: {groupElement.FileName}");
                    //  修复：传入 referer 参数
                    var view = ConvertElementToView(groupElement, isHorizontal: true, referer);
                    if (view != null)
                    {
                        currentHorizontalLayout.Add(view);
                    }
                }

                renderedGroups.Add(element.HorizontalGroupId);
            }
            // 如果该元素不属于任何分组，则正常渲染它
            else if (element.HorizontalGroupId == null)
            {
                Debug.WriteLine($"✅ 直接添加元素到容器: {element.FileName}");
                currentHorizontalLayout = null;
                //  修复：传入 referer 参数
                var view = ConvertElementToView(element, isHorizontal: false, referer);
                if (view != null)
                {
                    container.Add(view);
                }
            }
            else
            {
                Debug.WriteLine($"⏭️ 跳过已渲染的分组ID={element.HorizontalGroupId}");
            }
        }
    }

    /// <summary>
    /// 将单个 ContentElement 转换为对应的 View
    ///  修复：添加 referer 参数，用于文件下载
    /// </summary>
    private static View? ConvertElementToView(ContentElement element, bool isHorizontal = false, string? referer = null)
    {
        return element.Type switch
        {
            ContentElementType.Text => CreateTextLabel(element, isHorizontal),
            ContentElementType.Bold => CreateBoldLabel(element, isHorizontal),
            ContentElementType.Italic => CreateItalicLabel(element, isHorizontal),
            ContentElementType.Link => CreateLinkLabel(element),
            ContentElementType.Image => CreateImageView(element),
            ContentElementType.Attachment => CreateAttachmentButton(element, referer),
            ContentElementType.Emoji => CreateEmojiImage(element),
            ContentElementType.Code => CreateCodeBlock(element, isHorizontal),
            ContentElementType.Quote => CreateQuoteBlock(element, isHorizontal),
            ContentElementType.Table => CreateTableView(element),
            ContentElementType.TableRow => CreateTableRowView(element, 0),
            ContentElementType.TableCell => CreateTableCell(element, referer),
            ContentElementType.LineBreak => CreateLineBreak(),
            ContentElementType.Separator => CreateSeparator(),
            _ => null
        };
    }

    /// <summary>
    /// 创建普通文本标签
    ///  改进：支持文本换行，避免被遮挡
    /// </summary>
    private static Label CreateTextLabel(ContentElement element, bool isHorizontal = false)
    {
        var label = new Label
        {
            Text = element.Text ?? string.Empty,
            FontSize = isHorizontal ? 12 : 14,
            //  改进：即使是横向排列，也使用 WordWrap 支持换行
            LineBreakMode = LineBreakMode.WordWrap,
            Padding = new Thickness(isHorizontal ? 2 : 0, 2),
            VerticalOptions = isHorizontal ? LayoutOptions.Center : LayoutOptions.Start,
            //  改进：添加水平自动换行
            HorizontalOptions = LayoutOptions.Fill,
        };

        //  改进：根据情景设置合理的宽度
        if (isHorizontal)
        {
            // 在水平布局中，允许更多宽度
            label.MaximumWidthRequest = 200;  // 增加到 200
        }

        return label;
    }

    /// <summary>
    /// 创建粗体文本标签
    ///  改进：支持文本换行
    /// </summary>
    private static Label CreateBoldLabel(ContentElement element, bool isHorizontal = false)
    {
        var label = new Label
        {
            Text = element.Text ?? string.Empty,
            FontSize = isHorizontal ? 12 : 14,
            FontAttributes = FontAttributes.Bold,
            //  改进：使用 WordWrap 支持换行
            LineBreakMode = LineBreakMode.WordWrap,
            Padding = new Thickness(isHorizontal ? 2 : 0, 2),
            VerticalOptions = isHorizontal ? LayoutOptions.Center : LayoutOptions.Start,
            //  改进：添加水平自动换行
            HorizontalOptions = LayoutOptions.Fill,
        };

        //  改进：根据情景设置合理的宽度
        if (isHorizontal)
        {
            label.MaximumWidthRequest = 200;  // 增加到 200
        }

        return label;
    }

    /// <summary>
    /// 创建斜体文本标签
    ///  改进：支持文本换行
    /// </summary>
    private static Label CreateItalicLabel(ContentElement element, bool isHorizontal = false)
    {
        var label = new Label
        {
            Text = element.Text ?? string.Empty,
            FontSize = isHorizontal ? 12 : 14,
            FontAttributes = FontAttributes.Italic,
            //  改进：使用 WordWrap 支持换行
            LineBreakMode = LineBreakMode.WordWrap,
            Padding = new Thickness(isHorizontal ? 2 : 0, 2),
            VerticalOptions = isHorizontal ? LayoutOptions.Center : LayoutOptions.Start,
            //  改进：添加水平自动换行
            HorizontalOptions = LayoutOptions.Fill,
        };

        //  改进：根据情景设置合理的宽度
        if (isHorizontal)
        {
            label.MaximumWidthRequest = 200;  // 增加到 200
        }

        return label;
    }

    /// <summary>
    /// 创建可点击的链接标签
    /// 支持普通链接和可下载资源（图片、PDF等）
    /// </summary>
    private static View CreateLinkLabel(ContentElement element)
    {
        var label = new Label
        {
            Text = element.Title ?? element.Text ?? element.Url ?? "链接",
            TextColor = Colors.Blue,
            TextDecorations = TextDecorations.Underline,
            FontSize = 14,
            LineBreakMode = LineBreakMode.TailTruncation,
            Padding = new Thickness(0, 2),
        };

        // 添加点击手势
        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += async (s, e) => await OnLinkTapped(element.Url, element.Title);
        label.GestureRecognizers.Add(tapGesture);

        return label;
    }

    /// <summary>
    /// 创建图片视图（支持点击下载）
    /// </summary>
    private static View CreateImageView(ContentElement element)
    {
        if (string.IsNullOrEmpty(element.Url))
        {
            Debug.WriteLine("⚠️ 图片 URL 为空");
            return new Label { Text = "[图片加载失败]" };
        }

        try
        {
            // 验证 URL 格式
            if (!Uri.TryCreate(element.Url, UriKind.Absolute, out var uri))
            {
                Debug.WriteLine($"⚠️ 无效的图片 URL: {element.Url}");
                return new Label { Text = "[图片 URL 无效]" };
            }

            var image = new Image
            {
                Source = ImageSource.FromUri(uri),
                Aspect = Aspect.AspectFit,
                Margin = new Thickness(0, 10),
            };

            // 如果指定了宽度和高度
            if (element.ImageWidth > 0 && element.ImageHeight > 0)
            {
                image.WidthRequest = Math.Min(element.ImageWidth, 300); // 最大宽度 300
                image.HeightRequest = element.ImageHeight * (element.ImageWidth <= 300 ? element.ImageWidth / 300 : 1);
            }

            // 添加点击手势用于下载图片
            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += async (s, e) => 
            {
                var mainPage = Application.Current?.Windows[0].Page;

            if (mainPage != null)
            {
                var result = await mainPage.DisplayAlertAsync(
                    "保存图片", 
                    "是否保存此图片？", 
                    "保存", "取消");

                if (result)
                {
                    var downloadService = new FileDownloadService();
                    await downloadService.DownloadAndSaveFileAsync(element.Url, element.Title);
                }
            }
                };
                image.GestureRecognizers.Add(tapGesture);

                return image;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ 创建图片视图错误: {ex.Message}");
                return new Label { Text = $"[图片加载错误]" };
            }
        }

    /// <summary>
    /// 创建附件块 - 显示完整的附件信息（文件名、大小、下载次数、上传时间）
    ///  增强：显示完整的附件元数据
    ///  修复：现在支持传入 Referer，确保下载时使用正确的来源页面
    ///  改进：添加加载动画，下载时禁用按钮并显示旋转的加载指示器
    ///  新增：支持付费附件购买功能
    /// </summary>
    private static View CreateAttachmentButton(ContentElement element, string? referer = null)
    {
        var attachmentContainer = new VerticalStackLayout
        {
            Margin = new Thickness(0, 10),
            Padding = new Thickness(12),
            BackgroundColor = Color.FromArgb("#F0F0F0"),
            Spacing = 8
        };

        // 第一行：文件名 + 下载/购买按钮
        var headerGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            ColumnSpacing = 10,
            RowSpacing = 0,
        };

        // 文件名图标和文本
        var fileName = new Label
        {
            Text = $"📎 {element.FileName ?? "附件"}",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center,
        };
        headerGrid.Add(fileName, 0, 0);

        // 检查是否为付费附件
        var isPaidAttachment = element.SalePrice.HasValue && element.SalePrice.Value > 0;

        // 按钮文本和颜色
        var buttonText = isPaidAttachment ? $"购买({element.SalePrice})" : "下载";
        var buttonColor = isPaidAttachment ? Color.FromArgb("#FF9800") : Colors.Blue; // 购买按钮用橙色

        var actionButton = new Button
        {
            Text = buttonText,
            BackgroundColor = buttonColor,
            TextColor = Colors.White,
            Padding = new Thickness(15, 5),
            FontSize = 11,
            CornerRadius = 5,
            WidthRequest = isPaidAttachment ? 85 : 70,
        };

        // 加载动画指示器（初始隐藏）
        var loadingIndicator = new ActivityIndicator
        {
            IsRunning = false,
            IsVisible = false,
            Color = Colors.White,
            Scale = 0.8,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
        };

        // 创建一个容器来放置按钮和加载动画，使它们重叠
        var buttonContainer = new Grid
        {
            WidthRequest = isPaidAttachment ? 85 : 70,
            HeightRequest = 35,
            ColumnDefinitions = new ColumnDefinitionCollection { new ColumnDefinition(GridLength.Star) },
            RowDefinitions = new RowDefinitionCollection { new RowDefinition(GridLength.Star) },
            Padding = new Thickness(0),
            ColumnSpacing = 0,
            RowSpacing = 0,
        };

        buttonContainer.Add(actionButton, 0, 0);
        buttonContainer.Add(loadingIndicator, 0, 0);

        //  修复：捕获参数并在点击时传入
        var attachmentUrl = element.Url;
        var attachmentFileName = element.FileName;
        var attachmentId = element.AttachmentId;
        var originalButtonColor = buttonColor;

        actionButton.Clicked += async (s, e) =>
        {
            // 禁用按钮并显示加载动画
            actionButton.IsEnabled = false;
            actionButton.BackgroundColor = Colors.Gray;  // 变灰色表示禁用
            actionButton.Opacity = 0.6;  // 降低透明度增强禁用效果
            loadingIndicator.IsVisible = true;
            loadingIndicator.IsRunning = true;

            try
            {
                if (isPaidAttachment)
                {
                    // 付费附件：显示购买确认窗口
                    await OnAttachmentBuyClicked(attachmentId, attachmentFileName, element.SalePrice ?? 0, referer);
                    await RefreshCurrentPage(); // 购买完成后刷新页面，更新附件状态
                }
                else
                {
                    // 免费附件：直接下载
                    await OnAttachmentDownloadClicked(attachmentUrl, attachmentFileName, referer);
                }
            }
            finally
            {
                // 完成后，启用按钮并隐藏加载动画
                actionButton.IsEnabled = true;
                actionButton.BackgroundColor = originalButtonColor;  // 恢复原始颜色
                actionButton.Opacity = 1.0;  // 恢复透明度
                loadingIndicator.IsRunning = false;
                loadingIndicator.IsVisible = false;
            }
        };

        headerGrid.Add(buttonContainer, 1, 0);

        attachmentContainer.Add(headerGrid);

        // 第二行：文件大小 + 下载次数 + 售价信息
        var infoStack = new HorizontalStackLayout
        {
            Spacing = 15,
            Padding = new Thickness(0),
        };

        if (!string.IsNullOrEmpty(element.FileSize))
        {
            var fileSizeLabel = new Label
            {
                Text = $"💾 {element.FileSize}",
                FontSize = 12,
                TextColor = Colors.DarkGray,
            };
            infoStack.Add(fileSizeLabel);
        }

        if (element.DownloadCount.HasValue && element.DownloadCount.Value > 0)
        {
            var downloadCountLabel = new Label
            {
                Text = $"⬇️ {element.DownloadCount}",
                FontSize = 12,
                TextColor = Colors.DarkGray,
            };
            infoStack.Add(downloadCountLabel);
        }

        if (isPaidAttachment)
        {
            var priceLabel = new Label
            {
                Text = $"💰 售价: {element.SalePrice} PB币",
                FontSize = 12,
                TextColor = Color.FromArgb("#FF9800"),
                FontAttributes = FontAttributes.Bold,
            };
            infoStack.Add(priceLabel);
        }

        if (infoStack.Children.Count > 0)
        {
            attachmentContainer.Add(infoStack);
        }

        // 第三行：上传时间
        if (!string.IsNullOrEmpty(element.UploadTime))
        {
            var uploadTimeLabel = new Label
            {
                Text = $"⏰ {element.UploadTime}",
                FontSize = 11,
                TextColor = Colors.Gray,
            };
            attachmentContainer.Add(uploadTimeLabel);
        }

        return attachmentContainer;
    }

    /// <summary>
    /// 创建表情图片
    /// 改进：如果 URL 为空或无效，显示备用文本而不是空白
    /// </summary>
    private static View CreateEmojiImage(ContentElement element)
    {
        if (string.IsNullOrEmpty(element.Url))
        {
            // ✅ 修复：当 Url 为空时，使用 Text 作为备用显示文本
            // 这样可以避免返回空 Label，导致后续内容无法显示
            return new Label 
            { 
                Text = element.Text ?? "[表情]",
                FontSize = 16,  // 与表情图片大小相符
                VerticalOptions = LayoutOptions.Center,
                Margin = new Thickness(2, 0)
            };
        }

        try
        {
            // 尝试从 URL 加载表情图片
            var image = new Image
            {
                Source = ImageSource.FromUri(new Uri(element.Url)),
                WidthRequest = 24,
                HeightRequest = 24,
                Aspect = Aspect.AspectFit,
                Margin = new Thickness(2, 0),
            };

            return image;
        }
        catch (Exception ex)
        {
            // ✅ 修复：如果 URL 加载失败，显示备用文本而不是空白
            Debug.WriteLine($"⚠️ 表情图片加载失败 ({element.Url}): {ex.Message}");
            return new Label 
            { 
                Text = element.Text ?? "[表情]",
                FontSize = 16,
                VerticalOptions = LayoutOptions.Center,
                Margin = new Thickness(2, 0)
            };
        }
    }

    /// <summary>
    /// 创建代码块
    /// </summary>
    private static View CreateCodeBlock(ContentElement element, bool isHorizontal = false)
    {
        var frame = new Frame
        {
            BorderColor = Colors.Gray,
            CornerRadius = 8,
            Padding = new Thickness(12),
            Margin = new Thickness(0, 10),
            BackgroundColor = Color.FromArgb("#F5F5F5"),
        };

        var label = new Label
        {
            Text = element.Text ?? string.Empty,
            FontSize = isHorizontal ? 10 : 12,
            FontFamily = "Courier New",
            LineBreakMode = isHorizontal ? LineBreakMode.TailTruncation : LineBreakMode.CharacterWrap,
            TextColor = Colors.Black,
        };

        if (isHorizontal)
        {
            label.MaximumWidthRequest = 150;
        }

        frame.Content = label;
        return frame;
    }

    /// <summary>
    /// 创建引用块
    /// </summary>
    private static View CreateQuoteBlock(ContentElement element, bool isHorizontal = false)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection { new ColumnDefinition(8), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 0,
            Padding = new Thickness(0),
            Margin = new Thickness(0, isHorizontal ? 0 : 10),
        };

        // 左侧边框
        var border = new BoxView
        {
            Color = Colors.Blue,
            WidthRequest = 4,
        };
        grid.Add(border, 0, 0);

        // 引用内容
        var content = new Frame
        {
            BorderColor = Color.FromArgb("#E0E0E0"),
            CornerRadius = 0,
            Padding = new Thickness(isHorizontal ? 6 : 12),
            Margin = new Thickness(0),
            BackgroundColor = Color.FromArgb("#F9F9F9"),
        };

        var label = new Label
        {
            Text = element.Text ?? string.Empty,
            FontSize = isHorizontal ? 10 : 13,
            LineBreakMode = isHorizontal ? LineBreakMode.TailTruncation : LineBreakMode.WordWrap,
            TextColor = Colors.Gray,
        };

        if (isHorizontal)
        {
            label.MaximumWidthRequest = 150;
        }

        content.Content = label;
        grid.Add(content, 1, 0);

        return grid;
    }

     /// <summary>
     /// 创建表格单元格容器
     /// 将单元格内的所有元素（支持多行通过 LineBreak）横向或纵向布局
     ///  改进：容器填满分配的单元格宽度，横向文本使用 ScrollView 支持横向滚动
     ///  修复：添加 referer 参数，用于文件下载
     /// </summary>
     private static View CreateTableCell(ContentElement element, string? referer = null)
     {
         // 创建一个垂直布局容器来包含单元格内的所有元素
         var cellContainer = new VerticalStackLayout
         {
             Spacing = 0,
             Padding = new Thickness(0),
             //  关键：让单元格容器填满分配的宽度
             HorizontalOptions = LayoutOptions.Fill,
         };

         if (element.Children != null && element.Children.Count > 0)
         {
             // 当前水平组的临时容器
             HorizontalStackLayout? currentHorizontalLayout = null;

             foreach (var childElement in element.Children)
             {
                 if (childElement.Type == ContentElementType.LineBreak)
                 {
                     // LineBreak：结束当前水平布局，开始新行
                     currentHorizontalLayout = null;
                     // 添加行分隔符（可选的垂直空间）
                     cellContainer.Add(new BoxView 
                     { 
                         HeightRequest = 0,
                         BackgroundColor = Colors.Transparent 
                     });
                     continue;
                 }

                 // 检查是否有 HorizontalGroupId（表示应该横向排列）
                 if (!string.IsNullOrEmpty(childElement.HorizontalGroupId))
                 {
                     // 如果当前没有水平布局，或者分组 ID 改变了，创建新的
                     if (currentHorizontalLayout == null || 
                         currentHorizontalLayout.ClassId != childElement.HorizontalGroupId)
                     {
                         currentHorizontalLayout = new HorizontalStackLayout
                         {
                             Spacing = 2,
                             Padding = new Thickness(0),
                             VerticalOptions = LayoutOptions.Start,
                             ClassId = childElement.HorizontalGroupId,
                             HorizontalOptions = LayoutOptions.Start,
                         };

                         //  新增：为水平布局包装 ScrollView，支持横向滚动
                         var horizontalScrollView = new ScrollView
                         {
                             Orientation = ScrollOrientation.Horizontal,
                             HorizontalScrollBarVisibility = ScrollBarVisibility.Always,
                             VerticalScrollBarVisibility = ScrollBarVisibility.Never,
                             Content = currentHorizontalLayout,
                             HeightRequest = 50,
                             HorizontalOptions = LayoutOptions.Fill,
                         };
                         cellContainer.Add(horizontalScrollView);
                     }

                     //  修复：传入 referer 参数
                     var view = ConvertElementToView(childElement, isHorizontal: true, referer);
                     if (view != null)
                     {
                         currentHorizontalLayout.Add(view);
                     }
                 }
                 else
                 {
                     // 没有 HorizontalGroupId：垂直排列
                     currentHorizontalLayout = null;
                     //  修复：传入 referer 参数
                     var view = ConvertElementToView(childElement, isHorizontal: false, referer);
                     if (view != null)
                     {
                         cellContainer.Add(view);
                     }
                 }
             }
         }

         return cellContainer;
     }

    /// <summary>
    /// 创建表格视图
    /// 新结构：Table → TableRow → TableCell → Elements
    /// 每个 TableRow 包含多个 TableCell，正确表示行列结构
    ///  改进：填满父容器宽度，不留空白
    /// </summary>
    private static View CreateTableView(ContentElement element)
    {
        var stackLayout = new VerticalStackLayout
        {
            Margin = new Thickness(0, 10),
            Spacing = 0,
            BackgroundColor = Colors.Gray,
            //  关键：让 StackLayout 填满可用宽度
            HorizontalOptions = LayoutOptions.Fill,
        };

        if (element.Children != null && element.Children.Count > 0)
        {
            int rowCount = 0;
            foreach (var rowElement in element.Children)
            {
                //  新结构：直接处理 TableRow
                if (rowElement.Type == ContentElementType.TableRow)
                {
                    var rowView = CreateTableRowView(rowElement, element.ColumnCount);
                    stackLayout.Add(rowView);
                    rowCount++;
                }
            }

            Debug.WriteLine($" 表格渲染：{rowCount} 行 × {element.ColumnCount} 列");
        }

        //  改进：不使用 ScrollView 包装，让表格直接填满父容器
        // ScrollView 会干扰宽度计算
        return stackLayout;
    }

    /// <summary>
    /// 创建表格行视图
    /// 新方法：直接处理 TableRow 元素
    /// TableRow.Children 包含多个 TableCell
    ///  改进：使用 Star GridLength 填满可用宽度，每列等宽
    /// </summary>
    private static View CreateTableRowView(ContentElement rowElement, int columnCount)
    {
        var grid = new Grid
        {
            ColumnSpacing = 1,
            RowSpacing = 0,
            BackgroundColor = Colors.Gray,
            Padding = new Thickness(0),
            //  关键：让 Grid 填满可用宽度
            HorizontalOptions = LayoutOptions.Fill,
        };

        //  改进：使用 Star 让所有列等宽填满可用宽度
        // Star 分布：每列获得等份的可用宽度，确保填满屏幕宽度
        for (int i = 0; i < columnCount; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        }

        // 添加行中的每个单元格
        if (rowElement.Children != null)
        {
            int columnIndex = 0;
            foreach (var cellElement in rowElement.Children)
            {
                if (columnIndex >= columnCount)
                    break;

                // 为每个单元格创建容器
                var cellFrame = new Frame
                {
                    BorderColor = Colors.Gray,
                    CornerRadius = 0,
                    Padding = new Thickness(8),
                    Margin = new Thickness(0),
                    BackgroundColor = Colors.White,
                    //  让 Frame 填满分配的列宽
                    HorizontalOptions = LayoutOptions.Fill,
                };

                //  处理 TableCell 类型
                View cellContent;
                if (cellElement.Type == ContentElementType.TableCell)
                {
                    // TableCell 已经包含了单元格内的所有内容，直接渲染
                    cellContent = CreateTableCell(cellElement);
                }
                else
                {
                    // 其他类型：转换为通用视图
                    cellContent = ConvertElementToView(cellElement, isHorizontal: false) ?? 
                                 CreateTextLabel(cellElement, isHorizontal: false);
                }

                cellFrame.Content = cellContent;
                grid.Add(cellFrame, columnIndex, 0);
                columnIndex++;
            }
        }

        return grid;
    }

    /// <summary>
    /// 创建表格行视图（旧方法 - 保留以兼容现有代码）
    /// 支持单元格内的复杂内容（链接、图片、加粗文本等）
    /// </summary>
    private static View CreateTableRow(List<ContentElement> cells, int columnCount = 0)
    {
        // 如果未指定列数，从单元格数量推导
        if (columnCount <= 0)
        {
            columnCount = cells.Count > 0 ? cells.Count : 1;
        }

        var grid = new Grid
        {
            ColumnSpacing = 1,
            RowSpacing = 0,
            BackgroundColor = Colors.Gray,
            Padding = new Thickness(0),
        };

        // 根据列数设置列定义（而不是根据单元格数量）
        for (int i = 0; i < columnCount; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        }

        // 添加单元格
        for (int i = 0; i < cells.Count && i < columnCount; i++)
        {
            var cellElement = cells[i];

            // 为每个单元格创建容器
            var cellFrame = new Frame
            {
                BorderColor = Colors.Gray,
                CornerRadius = 0,
                Padding = new Thickness(8),
                Margin = new Thickness(0),
                BackgroundColor = Colors.White,
            };

            // 根据元素类型创建单元格内容
            View cellContent;

            //  处理新的 TableCell 类型
            if (cellElement.Type == ContentElementType.TableCell)
            {
                // TableCell 已经包含了单元格内的所有内容，直接渲染
                cellContent = CreateTableCell(cellElement);
            }
            // 检查是否是多元素单元格（包含 LineBreak 或多个内容元素）
            else if (cellElement.Type == ContentElementType.Text && 
                cellElement.HorizontalGroupId == "multi_element_cell" && 
                cellElement.Children != null && cellElement.Children.Count > 1)
            {
                // 多元素单元格：使用 VerticalStackLayout 来显示多行内容
                var stackLayout = new VerticalStackLayout
                {
                    Spacing = 0,
                    Padding = new Thickness(0),
                };

                foreach (var childElement in cellElement.Children)
                {
                    if (childElement.Type == ContentElementType.LineBreak)
                    {
                        // 换行：添加空白区域
                        stackLayout.Add(new BoxView { HeightRequest = 8, BackgroundColor = Colors.Transparent });
                    }
                    else if (childElement.Type == ContentElementType.Link)
                    {
                        stackLayout.Add(CreateLinkLabel(childElement));
                    }
                    else if (childElement.Type == ContentElementType.Bold)
                    {
                        stackLayout.Add(CreateBoldLabel(childElement, isHorizontal: false));
                    }
                    else if (childElement.Type == ContentElementType.Text)
                    {
                        stackLayout.Add(CreateTextLabel(childElement, isHorizontal: false));
                    }
                    else if (childElement.Type == ContentElementType.Image)
                    {
                        stackLayout.Add(CreateImageView(childElement));
                    }
                    else
                    {
                        var view = ConvertElementToView(childElement, isHorizontal: false);
                        if (view != null)
                            stackLayout.Add(view);
                    }
                }

                cellContent = stackLayout;
            }
            else if (cellElement.Type == ContentElementType.Link)
            {
                // 链接单元格
                cellContent = CreateLinkLabel(cellElement);
            }
            else if (cellElement.Type == ContentElementType.Bold)
            {
                // 加粗文本单元格
                cellContent = CreateBoldLabel(cellElement, isHorizontal: false);
            }
            else if (cellElement.Type == ContentElementType.Text)
            {
                // 普通文本单元格
                cellContent = CreateTextLabel(cellElement, isHorizontal: false);
            }
            else if (cellElement.Type == ContentElementType.Image)
            {
                // 图片单元格
                cellContent = CreateImageView(cellElement);
            }
            else if (cellElement.Type == ContentElementType.LineBreak)
            {
                // 换行 - 在表格单元格中应该显示为垂直空间
                cellContent = new Label { Text = "", HeightRequest = 10 };
            }
            else
            {
                // 其他类型：转换为通用视图
                cellContent = ConvertElementToView(cellElement, isHorizontal: false) ?? CreateTextLabel(cellElement, isHorizontal: false);
            }

            // 处理链接的特殊情况（CreateLinkLabel 返回的可能是 Frame）
            if (cellContent is Frame linkFrame && cellElement.Type == ContentElementType.Link)
            {
                cellFrame.Content = linkFrame.Content;
            }
            else
            {
                cellFrame.Content = cellContent;
            }

            grid.Add(cellFrame, i, 0);
        }

        return grid;
    }

    // ⚠️ 旧方法已删除 - 不再需要按列数或分隔符分组
    // 新的表格结构使用 TableRow 容器，直接包含 TableCell 元素
    // 这使得行列结构更清晰，渲染逻辑更简单

    /// <summary>
    /// 创建换行符（空白区域）
    /// </summary>
    private static View CreateLineBreak()
    {
        return new BoxView
        {
            HeightRequest = 1,
            BackgroundColor = Colors.Transparent,
            Margin = new Thickness(0, 5),
        };
    }

    /// <summary>
    /// 创建分隔符
    /// </summary>
    private static View CreateSeparator()
    {
        return new BoxView
        {
            HeightRequest = 1,
            BackgroundColor = Colors.LightGray,
            Margin = new Thickness(0, 10),
        };
    }


    /// <summary>
    /// 从 Shell 导航栈获取当前页面
    /// </summary>
    private static ContentPage? GetCurrentPageFromShell()
    {
        try
        {
            var shell = Shell.Current;
            if (shell != null)
            {
                // 获取导航栈中的当前页面
                var navigation = shell.Navigation;
                if (navigation.NavigationStack.Count > 0)
                {
                    var currentPage = navigation.NavigationStack[navigation.NavigationStack.Count - 1];
                    return currentPage as ContentPage;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"⚠️ 从 Shell 获取页面失败: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// 刷新当前页面
    ///  新增：支持购买完成后刷新页面
    /// 支持 ThreadContentPage 和其他带有 RefreshAsync 方法的页面
    ///  改进：更强大的页面查找和刷新机制
    /// </summary>
    private static async Task RefreshCurrentPage()
    {
        try
        {
            // 方案1：尝试从 Shell 的导航栈获取当前页面
            ContentPage? currentPage = GetCurrentPageFromShell();

            if (currentPage == null)
            {
                // 方案2：从应用程序主窗口获取
                currentPage = Application.Current?.Windows[0].Page as ContentPage;
            }

            Debug.WriteLine($"🔍 当前页面类型: {currentPage?.GetType().Name ?? "null"}");

            if (currentPage != null)
            {
                // 尝试调用页面上的刷新方法
                if (await TryInvokeRefreshAsync(currentPage))
                {
                    Debug.WriteLine(" 页面刷新成功（使用 RefreshAsync）");
                    return;
                }

                // 如果页面有 ViewModel，尝试调用 ViewModel 的刷新方法
                var viewModel = currentPage.BindingContext;
                if (viewModel != null)
                {
                    Debug.WriteLine($"🔍 ViewModel 类型: {viewModel.GetType().Name}");
                    if (await TryInvokeRefreshAsync(viewModel))
                    {
                        Debug.WriteLine(" 页面刷新成功（使用 ViewModel.RefreshAsync）");
                        return;
                    }
                }

                Debug.WriteLine("⚠️ 当前页面或 ViewModel 不支持刷新");
            }
            else
            {
                Debug.WriteLine("⚠️ 无法获取当前页面");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"⚠️ 页面刷新失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 尝试调用对象上的 RefreshAsync 方法（使用反射）
    /// 支持任何具有 public async Task RefreshAsync() 方法的对象
    /// </summary>
    private static async Task<bool> TryInvokeRefreshAsync(object? target)
    {
        if (target == null)
            return false;

        try
        {
            var refreshMethod = target.GetType().GetMethod(
                "RefreshAsync",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                null,
                System.Type.EmptyTypes,
                null
            );

            if (refreshMethod != null && refreshMethod.ReturnType == typeof(Task))
            {
                Debug.WriteLine($"🔄 调用 {target.GetType().Name}.RefreshAsync()...");
                var task = (Task?)refreshMethod.Invoke(target, null);
                if (task != null)
                {
                    await task;
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"⚠️ 调用 RefreshAsync 失败: {ex.Message}");
        }

        return false;
    }

    /// <summary>
    /// 链接点击事件处理
    /// 判断URL类型：可下载资源使用FileSaver，普通链接使用浏览器
    /// </summary>
    private static async Task OnLinkTapped(string? url, string? title)
    {
        if (string.IsNullOrEmpty(url))
            return;

        try
        {
            // 检查是否为可下载的文件类型
            if (IsDownloadableUrl(url))
            {
                var downloadService = new FileDownloadService();
                await downloadService.DownloadAndSaveFileAsync(url, title);
            }
            else
            {
                // 普通链接在浏览器中打开
                await Launcher.OpenAsync(new Uri(url));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 打开链接错误: {ex.Message}");
            await Application.Current?.Windows[0].Page?.DisplayAlertAsync("错误", $"无法打开链接: {ex.Message}", "确定");
        }
    }

    /// <summary>
    /// 附件下载事件处理
    /// 使用FileDownloadService处理文件下载
    ///  修复：现在支持传入 Referer 参数，确保下载时使用正确的来源页面
    /// </summary>
    private static async Task OnAttachmentDownloadClicked(string? url, string? fileName, string? referer = null)
    {
        if (string.IsNullOrEmpty(url))
        {
            await Application.Current?.Windows[0].Page?.DisplayAlertAsync("错误", "无法获取下载链接", "确定");
            return;
        }

        try
        {
            var downloadService = new FileDownloadService();
            //  修复：传入正确的 Referer 参数
            await downloadService.DownloadAndSaveFileAsync(url, fileName, referer);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 附件下载错误: {ex.Message}");
            await Application.Current?.Windows[0].Page?.DisplayAlertAsync("错误", 
                $"下载失败: {ex.Message}", "确定");
        }
    }

    /// <summary>
    /// 判断URL是否为可下载的文件类型
    /// </summary>
    private static bool IsDownloadableUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
            return false;

        // 转换为小写进行比较
        var lowerUrl = url.ToLower();

        // 判断URL是否包含文件扩展名或下载关键字
        var downloadableExtensions = new[] { 
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
            ".zip", ".rar", ".7z", ".exe", ".apk",
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp",
            ".mp3", ".mp4", ".avi", ".mkv", ".mov",
            ".txt", ".csv", ".json", ".xml"
        };

        // 检查文件扩展名
        foreach (var ext in downloadableExtensions)
        {
            if (lowerUrl.EndsWith(ext) || lowerUrl.Contains(ext + "?"))
                return true;
        }

        // 检查URL中是否包含下载相关的关键字
        if (lowerUrl.Contains("attachment") || lowerUrl.Contains("download") || lowerUrl.Contains("file"))
            return true;

        return false;
    }

    /// <summary>
    /// 附件购买事件处理
    /// 显示购买确认窗口，用户确认后调用购买接口
    ///  新增：支持付费附件购买功能
    ///  改进：从购买确认页面提取必需的表单参数（formhash、referer、aid）
    /// </summary>
    private static async Task OnAttachmentBuyClicked(string? attachmentId, string? fileName, int salePrice, string? referer = null)
    {
        if (string.IsNullOrEmpty(attachmentId))
        {
            await Application.Current?.Windows[0].Page?.DisplayAlertAsync("错误", "无法获取附件ID", "确定");
            return;
        }

        try
        {
            var apiService = new ApiService();
            var xmlParsingService = new XmlParsingService();

            // 第一步：获取购买确认页面
            Debug.WriteLine($"📝 正在获取购买确认信息...（AID: {attachmentId}）");
            var confirmPageHtml = await apiService.GetAttachmentBuyConfirmPageAsync(attachmentId);

            if (string.IsNullOrEmpty(confirmPageHtml))
            {
                await Application.Current?.Windows[0].Page?.DisplayAlertAsync("错误", "无法获取购买信息", "确定");
                return;
            }

            //  新增：从购买确认页面提取隐藏参数
            var purchaseParams = xmlParsingService.ExtractAttachmentPurchaseParams(confirmPageHtml);
            if (string.IsNullOrEmpty(purchaseParams.FormHash))
            {
                await Application.Current?.Windows[0].Page?.DisplayAlertAsync("错误", "无法提取购买参数，请重试", "确定");
                return;
            }

            // 第二步：显示购买确认对话框
            var mainPage = Application.Current?.Windows[0].Page;
            if (mainPage == null)
                return;

            var confirmMessage = $"附件名称: {fileName}\n\n" +
                                $"售价: {salePrice} PB币\n\n" +
                                $"确认购买此附件吗？";

            var result = await mainPage.DisplayAlertAsync("购买确认", confirmMessage, "确认购买", "取消");

            if (!result)
            {
                Debug.WriteLine("❌ 用户取消购买");
                return;
            }

            // 第三步：调用购买接口，传入提取的参数
            Debug.WriteLine($"💳 正在处理购买请求...（AID: {attachmentId}）");
            var buyResult = await apiService.BuyAttachmentAsync(
                attachmentId,
                formhash: purchaseParams.FormHash,
                referer: purchaseParams.Referer,
                tid: purchaseParams.Tid
            );

            if (buyResult?.IsSuccess == true)
            {
                // 购买成功
                await mainPage.DisplayAlertAsync(
                    "购买成功",
                    $"附件购买成功！即将开始下载...",
                    "确定");

                Debug.WriteLine($" 附件购买成功（AID: {attachmentId}）");

                // 如果返回了下载链接，自动下载
                if (!string.IsNullOrEmpty(buyResult.DownloadUrl))
                {
                    var downloadService = new FileDownloadService();
                    await downloadService.DownloadAndSaveFileAsync(buyResult.DownloadUrl, fileName, referer);
                }
            }
            else
            {
                // 购买失败
                var errorMsg = buyResult?.Message ?? "购买失败，请检查PB币余额";
                await mainPage.DisplayAlertAsync("购买失败", errorMsg, "确定");
                Debug.WriteLine($"❌ 附件购买失败: {errorMsg}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ 附件购买错误: {ex.Message}");
            await Application.Current?.Windows[0].Page?.DisplayAlertAsync("错误", $"购买失败: {ex.Message}", "确定");
        }
    }
}