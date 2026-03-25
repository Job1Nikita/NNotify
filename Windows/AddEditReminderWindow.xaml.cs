using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using NNotify.Localization;
using NNotify.Models;

namespace NNotify.Windows;

public partial class AddEditReminderWindow : Window
{
    private readonly Reminder? _existingReminder;
    private readonly bool _focusTitleOnLoad;
    private int _selectedPriority = 1;

    public AddEditReminderWindow(Reminder? reminder = null, bool createFromTemplate = false)
    {
        InitializeComponent();
        ConfigureDatePickerBounds();
        Loaded += OnWindowLoaded;

        _existingReminder = createFromTemplate ? null : reminder;
        _focusTitleOnLoad = reminder is null;
        _selectedPriority = reminder?.Priority ?? 1;
        SetPriority(_selectedPriority);

        if (reminder is null)
        {
            WindowTitleText.Text = Loc.Text("AddEditWindowTitleNew");
            Title = WindowTitleText.Text;
            SetDueLocal(DateTime.Now.AddMinutes(5));
            return;
        }

        if (createFromTemplate)
        {
            WindowTitleText.Text = Loc.Text("AddEditWindowTitleRepeat");
            Title = WindowTitleText.Text;
            TitleTextBox.Text = reminder.Title;
            SetDueLocal(DateTime.Now.AddMinutes(5));
            return;
        }

        WindowTitleText.Text = Loc.Text("AddEditWindowTitleEdit");
        Title = WindowTitleText.Text;
        TitleTextBox.Text = reminder.Title;
        SetDueLocal(reminder.EffectiveDueLocal);
    }

    public Reminder? EditedReminder { get; private set; }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (!_focusTitleOnLoad)
        {
            return;
        }

        Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Input,
            new Action(() =>
            {
                TitleTextBox.Focus();
                Keyboard.Focus(TitleTextBox);
                TitleTextBox.SelectAll();
            }));
    }

    private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed)
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch
        {
            // Ignore drag race conditions.
        }
    }

    private void OnCloseButtonClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnPlus3Click(object sender, RoutedEventArgs e)
    {
        SetDueLocal(DateTime.Now.AddMinutes(3));
    }

    private void OnPlus5Click(object sender, RoutedEventArgs e)
    {
        SetDueLocal(DateTime.Now.AddMinutes(5));
    }

    private void OnPlus10Click(object sender, RoutedEventArgs e)
    {
        SetDueLocal(DateTime.Now.AddMinutes(10));
    }

    private void OnPriorityToggleClick(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton toggle || toggle.Tag is null)
        {
            return;
        }

        if (!int.TryParse(toggle.Tag.ToString(), out var priority))
        {
            return;
        }

        SetPriority(priority);
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var title = TitleTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            ShowValidationDialog(Loc.Text("AddEditValidationEnterText"));
            return;
        }

        if (DueDateCalendar.SelectedDate is not DateTime date)
        {
            ShowValidationDialog(Loc.Text("AddEditValidationSelectDate"));
            return;
        }

        if (!TryReadSpinnerTime(out var time))
        {
            ShowValidationDialog(Loc.Text("AddEditValidationInvalidTime"));
            return;
        }

        ApplyTimeToSpinner(time);
        var dueLocal = date.Date + time;
        var nowLocal = DateTime.Now;
        if (dueLocal <= nowLocal)
        {
            ShowValidationDialog(Loc.Text("AddEditValidationPastTime"));
            return;
        }

        var dueUtc = new DateTimeOffset(dueLocal).ToUniversalTime();

        EditedReminder = _existingReminder is null
            ? new Reminder
            {
                Id = Guid.NewGuid().ToString(),
                Title = title,
                DueAtUtc = dueUtc,
                Priority = _selectedPriority,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                Status = ReminderStatus.Scheduled
            }
            : new Reminder
            {
                Id = _existingReminder.Id,
                Title = title,
                DueAtUtc = dueUtc,
                Priority = _selectedPriority,
                CreatedAtUtc = _existingReminder.CreatedAtUtc,
                Status = ReminderStatus.Scheduled,
                LastFiredAtUtc = _existingReminder.LastFiredAtUtc,
                AckedAtUtc = null,
                SnoozeUntilUtc = null,
                TelegramEscalatedAtUtc = null
            };

        DialogResult = true;
        Close();
    }

    private void ConfigureDatePickerBounds()
    {
        var today = DateTime.Today;
        DueDateCalendar.BlackoutDates.Clear();
        DueDateCalendar.BlackoutDates.Add(new CalendarDateRange(DateTime.MinValue, today.AddDays(-1)));
        DueDateCalendar.DisplayDateStart = null;
        DueDateCalendar.DisplayDateEnd = null;
        DueDateCalendar.DisplayDate = today;
        if (DueDateCalendar.SelectedDate is null)
        {
            DueDateCalendar.SelectedDate = today;
        }

        UpdateDueDateText();
    }

    private void OnDueDateBorderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        ToggleDueDatePopup();
        e.Handled = true;
    }

    private void OnDueDateDropDownButtonClick(object sender, RoutedEventArgs e)
    {
        ToggleDueDatePopup();
    }

    private void OnDueDateCalendarSelectedDatesChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DueDateCalendar.SelectedDate is null)
        {
            return;
        }

        var today = DateTime.Today;
        if (DueDateCalendar.SelectedDate.Value.Date < today)
        {
            DueDateCalendar.SelectedDate = today;
            DueDateCalendar.DisplayDate = today;
            return;
        }

        UpdateDueDateText();
        DueDatePopup.IsOpen = false;
    }

    private void ToggleDueDatePopup()
    {
        DueDatePopup.IsOpen = !DueDatePopup.IsOpen;
        if (!DueDatePopup.IsOpen)
        {
            return;
        }

        DueDateCalendar.DisplayDate = DueDateCalendar.SelectedDate ?? DateTime.Today;
        DueDateCalendar.Focus();
    }

    private void UpdateDueDateText()
    {
        var selected = DueDateCalendar.SelectedDate ?? DateTime.Today;
        DueDateTextBlock.Text = selected.ToString("dd.MM.yyyy");
    }

    private void SetDueLocal(DateTime localDateTime)
    {
        var today = DateTime.Today;
        var selectedDate = localDateTime.Date < today ? today : localDateTime.Date;
        DueDateCalendar.SelectedDate = selectedDate;
        DueDateCalendar.DisplayDate = selectedDate;
        UpdateDueDateText();
        ApplyTimeToSpinner(localDateTime.TimeOfDay);
    }

    private void SetPriority(int priority)
    {
        _selectedPriority = priority switch
        {
            0 => 0,
            2 => 2,
            _ => 1
        };

        PriorityP0Button.IsChecked = _selectedPriority == 0;
        PriorityP1Button.IsChecked = _selectedPriority == 1;
        PriorityP2Button.IsChecked = _selectedPriority == 2;
    }

    private void OnHourUpClick(object sender, RoutedEventArgs e)
    {
        AdjustHour(1);
    }

    private void OnHourDownClick(object sender, RoutedEventArgs e)
    {
        AdjustHour(-1);
    }

    private void OnMinuteUpClick(object sender, RoutedEventArgs e)
    {
        AdjustMinute(1);
    }

    private void OnMinuteDownClick(object sender, RoutedEventArgs e)
    {
        AdjustMinute(-1);
    }

    private void OnHourMouseWheel(object sender, MouseWheelEventArgs e)
    {
        AdjustHour(e.Delta > 0 ? 1 : -1);
        e.Handled = true;
    }

    private void OnMinuteMouseWheel(object sender, MouseWheelEventArgs e)
    {
        AdjustMinute(e.Delta > 0 ? 1 : -1);
        e.Handled = true;
    }

    private void OnTimePartPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        foreach (var ch in e.Text)
        {
            if (!char.IsDigit(ch))
            {
                e.Handled = true;
                return;
            }
        }

        e.Handled = false;
    }

    private void OnTimePartPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox timePartTextBox)
        {
            return;
        }

        if (e.Key == Key.Up)
        {
            if (ReferenceEquals(timePartTextBox, HourTextBox))
            {
                AdjustHour(1);
            }
            else
            {
                AdjustMinute(1);
            }

            e.Handled = true;
            return;
        }

        if (e.Key == Key.Down)
        {
            if (ReferenceEquals(timePartTextBox, HourTextBox))
            {
                AdjustHour(-1);
            }
            else
            {
                AdjustMinute(-1);
            }

            e.Handled = true;
            return;
        }

        if (e.Key == Key.Space)
        {
            e.Handled = true;
        }
    }

    private void OnTimePartGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox textBox)
        {
            textBox.SelectAll();
        }
    }

    private void OnHourLostFocus(object sender, RoutedEventArgs e)
    {
        NormalizeHourTextBox();
    }

    private void OnMinuteLostFocus(object sender, RoutedEventArgs e)
    {
        NormalizeMinuteTextBox();
    }

    private void AdjustHour(int delta)
    {
        var hour = Wrap(GetHourOrDefault() + delta, 0, 23);
        HourTextBox.Text = $"{hour:00}";
    }

    private void AdjustMinute(int delta)
    {
        var minute = Wrap(GetMinuteOrDefault() + delta, 0, 59);
        MinuteTextBox.Text = $"{minute:00}";
    }

    private bool TryReadSpinnerTime(out TimeSpan time)
    {
        time = default;
        if (!TryParseTimePart(HourTextBox.Text, 0, 23, out var hours))
        {
            return false;
        }

        if (!TryParseTimePart(MinuteTextBox.Text, 0, 59, out var minutes))
        {
            return false;
        }

        time = new TimeSpan(hours, minutes, 0);
        return true;
    }

    private static bool TryParseTimePart(string text, int minValue, int maxValue, out int value)
    {
        value = 0;
        if (!int.TryParse(text.Trim(), out var parsed))
        {
            return false;
        }

        if (parsed < minValue || parsed > maxValue)
        {
            return false;
        }

        value = parsed;
        return true;
    }

    private void ApplyTimeToSpinner(TimeSpan time)
    {
        HourTextBox.Text = $"{time.Hours:00}";
        MinuteTextBox.Text = $"{time.Minutes:00}";
    }

    private void NormalizeHourTextBox()
    {
        var normalized = TryParseTimePart(HourTextBox.Text, 0, 23, out var hour)
            ? hour
            : GetHourOrDefault();
        HourTextBox.Text = $"{normalized:00}";
    }

    private void NormalizeMinuteTextBox()
    {
        var normalized = TryParseTimePart(MinuteTextBox.Text, 0, 59, out var minute)
            ? minute
            : GetMinuteOrDefault();
        MinuteTextBox.Text = $"{normalized:00}";
    }

    private int GetHourOrDefault()
    {
        if (TryParseTimePart(HourTextBox.Text, 0, 23, out var hour))
        {
            return hour;
        }

        return DateTime.Now.Hour;
    }

    private int GetMinuteOrDefault()
    {
        if (TryParseTimePart(MinuteTextBox.Text, 0, 59, out var minute))
        {
            return minute;
        }

        return DateTime.Now.Minute;
    }

    private static int Wrap(int value, int minValue, int maxValue)
    {
        var range = maxValue - minValue + 1;
        var shifted = (value - minValue) % range;
        if (shifted < 0)
        {
            shifted += range;
        }

        return shifted + minValue;
    }

    private void ShowValidationDialog(string message)
    {
        var dialog = new ConfirmDialogWindow(
            Loc.Text("CommonCheckDataTitle"),
            message,
            Loc.Text("CommonOk"),
            showCancel: false,
            destructive: false)
        {
            Owner = this
        };

        dialog.ShowDialog();
    }
}


