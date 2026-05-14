using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace NNotify.Windows;

internal static class ModalDialogAnimator
{
    public static void StartEntrance(Window window, ScaleTransform scaleTransform, TranslateTransform translateTransform)
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = TimeSpan.FromMilliseconds(220);

        window.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, duration)
        {
            EasingFunction = ease
        });

        scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.985, 1, duration)
        {
            EasingFunction = ease
        });
        scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.985, 1, duration)
        {
            EasingFunction = ease
        });
        translateTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(10, 0, duration)
        {
            EasingFunction = ease
        });
    }
}
