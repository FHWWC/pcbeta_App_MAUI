using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using System;
using System.Diagnostics;

namespace PCBetaMAUI.Controls;

public class ZoomableImageView : ContentView
{
    private readonly Image _image;
    private readonly VisualElement _container;
    private double _currentScale = 1;
    private double _startScale = 1;
    private double _xOffset = 0;
    private double _yOffset = 0;

    public ZoomableImageView(ImageSource source)
    {
        _image = new Image
        {
            Source = source,
            Aspect = Aspect.AspectFit,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };

        var grid = new Grid { Children = { _image } };
        Content = grid;
        _container = grid;

        // Pinch for Android/General
        var pinch = new PinchGestureRecognizer();
        pinch.PinchUpdated += OnPinchUpdated;
        GestureRecognizers.Add(pinch);

        // Pan to move when zoomed
        var pan = new PanGestureRecognizer();
        pan.PanUpdated += OnPanUpdated;
        GestureRecognizers.Add(pan);

        // Double tap to reset
        var tap = new TapGestureRecognizer { NumberOfTapsRequired = 2 };
        tap.Tapped += (s, e) => Reset();
        GestureRecognizers.Add(tap);
    }

    private void OnPinchUpdated(object? sender, PinchGestureUpdatedEventArgs e)
    {
        try
        {
            if (e.Status == GestureStatus.Started)
            {
                _startScale = _container.Scale;
                // Use the pinch origin as anchor so the container scales around gesture center
                _container.AnchorX = e.ScaleOrigin.X;
                _container.AnchorY = e.ScaleOrigin.Y;
            }
            else if (e.Status == GestureStatus.Running)
            {
                // 修复 Android pinch 缩放问题：
                // Android 上 e.Scale 是相对于上一帧的增量缩放，需要累积应用
                // 直接使用 e.Scale 作为增量应用到当前缩放值
                var newScale = _currentScale * e.Scale;
                newScale = Math.Max(1, Math.Min(newScale, 4));

                // Apply scale to container
                _container.Scale = newScale;
                _currentScale = newScale;

                // 仅当缩放到最小值时才重置位移，避免误触发
                if (Math.Abs(newScale - 1.0) < 0.01)
                {
                    _container.TranslationX = 0;
                    _container.TranslationY = 0;
                    _xOffset = 0;
                    _yOffset = 0;
                }
            }
            else if (e.Status == GestureStatus.Completed || e.Status == GestureStatus.Canceled)
            {
                // 完成或取消时，保存当前缩放值作为下次的基础
                _startScale = _currentScale;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Pinch error: {ex.Message}");
        }
    }

    private void OnPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        try
        {
            if (e.StatusType == GestureStatus.Running)
            {
                // Only allow panning when zoomed in
                if (_currentScale <= 1)
                    return;

                var translationX = e.TotalX + _xOffset;
                var translationY = e.TotalY + _yOffset;

                _container.TranslationX = translationX;
                _container.TranslationY = translationY;
            }
            else if (e.StatusType == GestureStatus.Completed)
            {
                _xOffset = _container.TranslationX;
                _yOffset = _container.TranslationY;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Pan error: {ex.Message}");
        }
    }

    public void Reset()
    {
        _container.Scale = 1;
        _container.TranslationX = 0;
        _container.TranslationY = 0;
        _currentScale = 1;
        _startScale = 1;
        _xOffset = 0;
        _yOffset = 0;
    }

#if WINDOWS
    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        try
        {
            // Attach mouse wheel on Windows to zoom (use container handler)
            var platformView = _container.Handler?.PlatformView as Microsoft.UI.Xaml.FrameworkElement;
            if (platformView != null)
            {
                platformView.PointerWheelChanged += PlatformView_PointerWheelChanged;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Attach wheel handler failed: {ex.Message}");
        }
    }

    private void PlatformView_PointerWheelChanged(object? sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        try
        {
            var delta = e.GetCurrentPoint(null).Properties.MouseWheelDelta;
            // delta is multiples of 120
            var factor = delta > 0 ? 1.1 : 0.9;
            var newScale = _container.Scale * factor;
            newScale = Math.Max(1, Math.Min(newScale, 4));
            _container.Scale = newScale;
            _currentScale = newScale;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Wheel zoom error: {ex.Message}");
        }
    }
#endif

}
