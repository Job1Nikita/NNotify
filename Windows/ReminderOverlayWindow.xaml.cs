using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using NNotify.Localization;
using NNotify.Models;

namespace NNotify.Windows;

public partial class ReminderOverlayWindow : Window
{
    private readonly Reminder _reminder;
    private readonly Func<Reminder, Task<bool>>? _editReminderAsync;
    private readonly bool _showTelegramEscalation;
    private readonly TaskCompletionSource<OverlayAction> _resultTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly DispatcherTimer _countdownTimer;
    private DateTimeOffset _timeoutAt;

    public ReminderOverlayWindow(
        Reminder reminder,
        Func<Reminder, Task<bool>>? editReminderAsync,
        bool showTelegramEscalation = true)
    {
        InitializeComponent();

        _reminder = reminder;
        _editReminderAsync = editReminderAsync;
        _showTelegramEscalation = showTelegramEscalation;
        TitleText.Text = $"[{_reminder.PriorityLabel}] {_reminder.Title}";
        DueText.Text = Loc.Format("OverlayDueTemplate", _reminder.EffectiveDueUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));

        _countdownTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _countdownTimer.Tick += (_, _) => UpdateCountdown();
        Loaded += (_, _) => StartPulseAnimation();
    }

    public async Task<OverlayAction> WaitForActionAsync(TimeSpan timeout)
    {
        _timeoutAt = DateTimeOffset.Now.Add(timeout);
        UpdateCountdown();
        _countdownTimer.Start();

        var completed = await Task.WhenAny(_resultTcs.Task, Task.Delay(timeout));
        if (completed == _resultTcs.Task)
        {
            return await _resultTcs.Task;
        }

        Complete(OverlayAction.Timeout);
        return OverlayAction.Timeout;
    }

    private void OnAckClick(object sender, RoutedEventArgs e)
    {
        Complete(OverlayAction.Ack);
    }

    private void OnOverlayMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        if (e.OriginalSource is DependencyObject source && FindVisualParent<System.Windows.Controls.Button>(source) is not null)
        {
            return;
        }

        try
        {
            DragMove();
            e.Handled = true;
        }
        catch
        {
            // Ignore drag race conditions.
        }
    }

    private void OnSnooze5Click(object sender, RoutedEventArgs e)
    {
        Complete(OverlayAction.Snooze5);
    }

    private void OnSnooze15Click(object sender, RoutedEventArgs e)
    {
        Complete(OverlayAction.Snooze15);
    }

    private async void OnEditClick(object sender, RoutedEventArgs e)
    {
        if (_editReminderAsync is null)
        {
            Complete(OverlayAction.Edited);
            return;
        }

        SetButtonsEnabled(false);
        try
        {
            var edited = await _editReminderAsync(_reminder);
            if (edited)
            {
                Complete(OverlayAction.Edited);
            }
        }
        catch (Exception ex)
        {
            Services.ErrorLogger.Log("Overlay edit failed", ex);
        }
        finally
        {
            SetButtonsEnabled(true);
        }
    }

    private void Complete(OverlayAction action)
    {
        if (_resultTcs.TrySetResult(action))
        {
            _countdownTimer.Stop();
            Close();
        }
    }

    private void UpdateCountdown()
    {
        var left = _timeoutAt - DateTimeOffset.Now;
        if (left < TimeSpan.Zero)
        {
            left = TimeSpan.Zero;
        }

        var secondsLeft = Math.Max(0, (int)Math.Ceiling(left.TotalSeconds));
        CountdownText.Text = _showTelegramEscalation
            ? Loc.Format("OverlayCountdownEscalationTemplate", secondsLeft)
            : Loc.Format("OverlayCountdownAutoCloseTemplate", secondsLeft);
    }

    private void SetButtonsEnabled(bool enabled)
    {
        foreach (var button in FindVisualChildren<System.Windows.Controls.Button>(this))
        {
            button.IsEnabled = enabled;
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject dependencyObject) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(dependencyObject); i++)
        {
            var child = VisualTreeHelper.GetChild(dependencyObject, i);
            if (child is T target)
            {
                yield return target;
            }

            foreach (var subChild in FindVisualChildren<T>(child))
            {
                yield return subChild;
            }
        }
    }

    private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
    {
        DependencyObject? current = child;
        while (current is not null)
        {
            if (current is T target)
            {
                return target;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private void StartPulseAnimation()
    {
        var easing = new QuadraticEase { EasingMode = EasingMode.EaseInOut };

        var startColorAnimation = new ColorAnimation
        {
            From = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1FA9FF"),
            To = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#7AC4FF"),
            Duration = TimeSpan.FromMilliseconds(850),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = easing
        };

        var endColorAnimation = new ColorAnimation
        {
            From = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#355CFF"),
            To = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#5A7DFF"),
            Duration = TimeSpan.FromMilliseconds(850),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = easing
        };

        OverlayStartStop.BeginAnimation(GradientStop.ColorProperty, startColorAnimation);
        OverlayEndStop.BeginAnimation(GradientStop.ColorProperty, endColorAnimation);
    }

    protected override void OnClosed(EventArgs e)
    {
        _countdownTimer.Stop();

        if (!_resultTcs.Task.IsCompleted)
        {
            _resultTcs.TrySetResult(OverlayAction.Timeout);
        }

        base.OnClosed(e);
    }
}
