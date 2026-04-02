using Microsoft.Maui.Controls;

namespace PCBetaMAUI.Behaviors
{
    public class ImageLoadingBehavior : Behavior<Image>
    {
        public static readonly BindableProperty FallbackSourceProperty =
            BindableProperty.Create(
                nameof(FallbackSource),
                typeof(ImageSource),
                typeof(ImageLoadingBehavior),
                null);

        public ImageSource FallbackSource
        {
            get => (ImageSource)GetValue(FallbackSourceProperty);
            set => SetValue(FallbackSourceProperty, value);
        }

        protected override void OnAttachedTo(Image image)
        {
            // 初始化时检查并监听 Source 变化
            image.PropertyChanged += OnImagePropertyChanged;
            base.OnAttachedTo(image);
        }

        protected override void OnDetachingFrom(Image image)
        {
            image.PropertyChanged -= OnImagePropertyChanged;
            base.OnDetachingFrom(image);
        }

        private void OnImagePropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender is Image image && e.PropertyName == nameof(Image.Source))
            {
                // 当 Source 设置为网络地址时，添加加载超时和失败处理
                if (image.Source is UriImageSource uriImageSource)
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        try
                        {
                            // 尝试加载网络图片，设置 3 秒超时
                            var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(3));

                            // 尝试访问网络资源
                            using (var client = new HttpClient())
                            {
                                var request = new HttpRequestMessage(HttpMethod.Head, uriImageSource.Uri);
                                await client.SendAsync(request, cts.Token);
                            }
                        }
                        catch (Exception ex)
                        {
                            // 加载失败，使用备用图片
                            System.Diagnostics.Debug.WriteLine($"网络图片加载失败: {ex.Message}，使用备用图片");
                            if (FallbackSource != null)
                            {
                                image.Source = FallbackSource;
                            }
                        }
                    });
                }
            }
        }
    }
}
