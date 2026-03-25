using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using NNotify.Localization;
using NNotify.Models;
using NNotify.Native;
using NNotify.Windows;

namespace NNotify;

public partial class MainWindow : Window
{
    private const int HotKeyId = 2001;
    private static readonly double[] UpcomingPreferredWidths = [166, 94, 300, 138];
    private static readonly double[] UpcomingMinWidths = [108, 78, 150, 96];
    private static readonly double[] MissedPreferredWidths = [156, 98, 255];
    private static readonly double[] MissedMinWidths = [108, 78, 140];
    private static readonly double[] HistoryPreferredWidths = [168, 98, 424, 140, 160];
    private static readonly double[] HistoryMinWidths = [120, 78, 170, 104, 110];

    private readonly ObservableCollection<Reminder> _upcoming = [];
    private readonly ObservableCollection<Reminder> _missed = [];
    private readonly ObservableCollection<Reminder> _history = [];
    private HotKeyManager? _hotKeyManager;
    private HwndSource? _hwndSource;
    private bool _entranceAnimationPlayed;
    private bool _hotKeyRegistrationFailed;

    public MainWindow()
    {
        InitializeComponent();

        UpcomingListView.ItemsSource = _upcoming;
        MissedListView.ItemsSource = _missed;
        HistoryListView.ItemsSource = _history;

        var app = GetApp();
        TopmostCheckBox.IsChecked = app.Settings.MainWindowTopmost;
        DarkThemeCheckBox.IsChecked = app.Settings.UseDarkTheme;
        Topmost = app.Settings.MainWindowTopmost;
        if (NormalizeHotKeySettings(app.Settings))
        {
            app.SaveSettings();
        }

        UpdateHotKeyHintText();

        Loaded += OnLoaded;
        SourceInitialized += OnSourceInitialized;
        Closing += OnClosing;
        Closed += OnClosed;
        StateChanged += OnStateChanged;
        SizeChanged += OnWindowSizeChanged;
        UpcomingListView.SizeChanged += OnUpcomingListSizeChanged;
        MissedListView.SizeChanged += OnMissedListSizeChanged;
        HistoryListView.SizeChanged += OnHistoryListSizeChanged;
        UpcomingListView.SelectionChanged += OnUpcomingSelectionChanged;
        MissedListView.SelectionChanged += OnMissedSelectionChanged;
        HistoryListView.SelectionChanged += OnHistorySelectionChanged;
        UpcomingListView.AddHandler(UIElement.PreviewMouseRightButtonDownEvent, new MouseButtonEventHandler(OnListViewPreviewMouseRightButtonDown), true);
        MissedListView.AddHandler(UIElement.PreviewMouseRightButtonDownEvent, new MouseButtonEventHandler(OnListViewPreviewMouseRightButtonDown), true);
        HistoryListView.AddHandler(UIElement.PreviewMouseRightButtonDownEvent, new MouseButtonEventHandler(OnListViewPreviewMouseRightButtonDown), true);
        UpcomingListView.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(OnGridViewThumbDragCompleted), true);
        MissedListView.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(OnGridViewThumbDragCompleted), true);
        HistoryListView.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(OnGridViewThumbDragCompleted), true);

        PrepareEntranceVisual(TitleBarCard);
        PrepareEntranceVisual(HeaderCard);
        PrepareEntranceVisual(UpcomingCard);
        PrepareEntranceVisual(MissedCard);
        PrepareEntranceVisual(HistoryCard);
        PrepareEntranceVisual(FooterCard);
        UpdateQuickActionsVisibility();
    }

    public async Task ReloadDataAsync()
    {
        try
        {
            var app = GetApp();
            await app.Repository.MarkStaleFiredAsMissedAsync(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(65));
            var upcoming = await app.Repository.GetUpcomingAsync();
            var missed = await app.Repository.GetMissedAsync();
            var history = await app.Repository.GetHistoryAsync();

            ReplaceCollection(_upcoming, upcoming);
            ReplaceCollection(_missed, missed);
            ReplaceCollection(_history, history);
            UpdateQuickActionsVisibility();
            ScheduleColumnAdjust();
        }
        catch (Exception ex)
        {
            Services.ErrorLogger.Log("Failed to reload main window data", ex);
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await ReloadDataAsync();

        if (!_entranceAnimationPlayed)
        {
            _entranceAnimationPlayed = true;
            StartEntranceAnimation();
        }

        UpdateMaximizeGlyph();
        ScheduleColumnAdjust();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        _hwndSource = HwndSource.FromHwnd(handle);
        _hwndSource?.AddHook(WndProc);

        ApplyHotKeyConfiguration();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        var app = GetApp();
        if (app.IsExitRequested)
        {
            return;
        }

        e.Cancel = true;
        Hide();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_hwndSource is not null)
        {
            _hwndSource.RemoveHook(WndProc);
            _hwndSource = null;
        }

        _hotKeyManager?.Dispose();
        _hotKeyManager = null;
    }

    private async void OnAddClick(object sender, RoutedEventArgs e)
    {
        await GetApp().AddReminderInteractiveAsync(this);
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        await ReloadDataAsync();
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        var app = GetApp();
        var hasStoredToken =
            !string.IsNullOrWhiteSpace(app.Settings.TelegramBotTokenEncrypted) ||
            !string.IsNullOrWhiteSpace(app.Settings.TelegramBotTokenPlain);
        var hasStoredSyncSession = !string.IsNullOrWhiteSpace(app.Settings.SyncRefreshTokenEncrypted);

        var dialog = new SettingsWindow(
            hasStoredToken,
            app.Settings.TelegramChatId ?? string.Empty,
            app.Settings.TelegramUserId ?? string.Empty,
            app.Settings.HotKeyEnabled,
            app.Settings.HotKeyGesture,
            app.Settings.SyncServerUrl ?? string.Empty,
            app.Settings.SyncUsername ?? string.Empty,
            hasStoredSyncSession)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var encrypted = true;
        if (!dialog.KeepExistingToken)
        {
            encrypted = app.SettingsService.SetBotToken(app.Settings, dialog.BotTokenForSave);
        }

        app.Settings.TelegramChatId = string.IsNullOrWhiteSpace(dialog.ChatId) ? null : dialog.ChatId;
        app.Settings.TelegramUserId = string.IsNullOrWhiteSpace(dialog.UserId) ? null : dialog.UserId;

        if (!string.IsNullOrWhiteSpace(app.Settings.TelegramChatId))
        {
            app.Settings.TelegramUserId = null;
        }
        else if (!string.IsNullOrWhiteSpace(app.Settings.TelegramUserId))
        {
            app.Settings.TelegramChatId = null;
        }

        app.Settings.HotKeyEnabled = dialog.HotKeyEnabled;
        app.Settings.HotKeyGesture = dialog.HotKeyGesture;
        app.Settings.SyncServerUrl = string.IsNullOrWhiteSpace(dialog.SyncServerUrl) ? null : dialog.SyncServerUrl;
        app.Settings.SyncUsername = string.IsNullOrWhiteSpace(dialog.SyncUsername) ? null : dialog.SyncUsername;

        app.SaveSettings();
        ApplyHotKeyConfiguration();

        if (!encrypted)
        {
            System.Windows.MessageBox.Show(this,
                Loc.Text("MainDapiSaveWarningBody"),
                Loc.Text("AppName"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void OnTopmostToggle(object sender, RoutedEventArgs e)
    {
        var isTopmost = TopmostCheckBox.IsChecked == true;
        Topmost = isTopmost;
        GetApp().SetMainWindowTopmost(isTopmost);
    }

    private void OnThemeToggle(object sender, RoutedEventArgs e)
    {
        var darkTheme = DarkThemeCheckBox.IsChecked == true;
        GetApp().SetDarkTheme(darkTheme);
    }

    private async void OnAckClick(object sender, RoutedEventArgs e)
    {
        var selected = GetSelectedUpcoming();
        if (selected is null)
        {
            return;
        }

        var app = GetApp();
        await app.Repository.AckAsync(selected.Id, DateTimeOffset.UtcNow);
        app.Scheduler.SignalReschedule();
        await ReloadDataAsync();
    }

    private async void OnSnooze5Click(object sender, RoutedEventArgs e)
    {
        var selected = GetSelectedUpcoming();
        if (selected is null)
        {
            return;
        }

        var app = GetApp();
        await app.Repository.SnoozeAsync(selected.Id, DateTimeOffset.UtcNow.AddMinutes(5));
        app.Scheduler.SignalReschedule();
        await ReloadDataAsync();
    }

    private async void OnSnooze15Click(object sender, RoutedEventArgs e)
    {
        var selected = GetSelectedUpcoming();
        if (selected is null)
        {
            return;
        }

        var app = GetApp();
        await app.Repository.SnoozeAsync(selected.Id, DateTimeOffset.UtcNow.AddMinutes(15));
        app.Scheduler.SignalReschedule();
        await ReloadDataAsync();
    }

    private async void OnEditClick(object sender, RoutedEventArgs e)
    {
        var selected = GetSelectedUpcoming();
        if (selected is null)
        {
            return;
        }

        await GetApp().OpenEditReminderDialogAsync(selected, this);
    }

    private async void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        var selected = GetSelectedAny();
        if (selected is null)
        {
            return;
        }

        await DeleteReminderAsync(selected);
    }

    private async void OnUpcomingContextEditClick(object sender, RoutedEventArgs e)
    {
        if (UpcomingListView.SelectedItem is not Reminder selected)
        {
            return;
        }

        await GetApp().OpenEditReminderDialogAsync(selected, this);
    }

    private async void OnUpcomingContextDeleteClick(object sender, RoutedEventArgs e)
    {
        if (UpcomingListView.SelectedItem is not Reminder selected)
        {
            return;
        }

        await DeleteReminderAsync(selected);
    }

    private async void OnUpcomingMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        if (e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        if (FindVisualParent<GridViewColumnHeader>(source) is not null ||
            FindVisualParent<System.Windows.Controls.ListViewItem>(source) is null)
        {
            return;
        }

        if (UpcomingListView.SelectedItem is not Reminder selected)
        {
            return;
        }

        e.Handled = true;
        await GetApp().OpenEditReminderDialogAsync(selected, this);
    }

    private async void OnMissedContextDeleteClick(object sender, RoutedEventArgs e)
    {
        if (MissedListView.SelectedItem is not Reminder selected)
        {
            return;
        }

        await DeleteReminderAsync(selected);
    }

    private async void OnHistoryContextDeleteClick(object sender, RoutedEventArgs e)
    {
        if (HistoryListView.SelectedItem is not Reminder selected)
        {
            return;
        }

        await DeleteReminderAsync(selected);
    }

    private async void OnHistoryContextRepeatClick(object sender, RoutedEventArgs e)
    {
        if (HistoryListView.SelectedItem is not Reminder selected)
        {
            return;
        }

        await GetApp().RepeatReminderInteractiveAsync(selected, this);
    }

    private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximizeRestore();
            return;
        }

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

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaximizeRestoreClick(object sender, RoutedEventArgs e)
    {
        ToggleMaximizeRestore();
    }

    private void OnCloseButtonClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        UpdateMaximizeGlyph();
        ScheduleColumnAdjust();
    }

    private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
    {
        ScheduleColumnAdjust();
    }

    private void OnUpcomingListSizeChanged(object sender, SizeChangedEventArgs e)
    {
        ScheduleColumnAdjust();
    }

    private void OnMissedListSizeChanged(object sender, SizeChangedEventArgs e)
    {
        ScheduleColumnAdjust();
    }

    private void OnHistoryListSizeChanged(object sender, SizeChangedEventArgs e)
    {
        ScheduleColumnAdjust();
    }

    private void OnGridViewThumbDragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        // Recalculate only after GridView header resize drag.
        if (FindVisualParent<System.Windows.Controls.GridViewColumnHeader>(source) is null)
        {
            return;
        }

        ScheduleColumnAdjust();
    }

    private void OnListViewPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        var item = FindVisualParent<System.Windows.Controls.ListViewItem>(source);
        if (item is null)
        {
            return;
        }

        item.IsSelected = true;
        item.Focus();
        if (sender is System.Windows.Controls.ListView selectedList)
        {
            ClearSelectionExcept(selectedList);
        }
    }

    private void OnRootPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        if (FindVisualParent<System.Windows.Controls.Primitives.ButtonBase>(source) is not null ||
            FindVisualParent<System.Windows.Controls.TextBox>(source) is not null ||
            FindVisualParent<System.Windows.Controls.PasswordBox>(source) is not null ||
            FindVisualParent<System.Windows.Controls.ComboBox>(source) is not null ||
            FindVisualParent<System.Windows.Controls.CheckBox>(source) is not null ||
            FindVisualParent<System.Windows.Controls.DatePicker>(source) is not null ||
            FindVisualParent<System.Windows.Controls.Primitives.ScrollBar>(source) is not null ||
            FindVisualParent<GridViewColumnHeader>(source) is not null)
        {
            return;
        }

        var listView = FindVisualParent<System.Windows.Controls.ListView>(source);
        if (listView is not null)
        {
            if (FindVisualParent<System.Windows.Controls.ListViewItem>(source) is not null)
            {
                return;
            }

            ClearAllSelections();
            return;
        }

        ClearAllSelections();
    }

    private void OnUpcomingSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (UpcomingListView.SelectedItem is not null)
        {
            ClearSelectionExcept(UpcomingListView);
        }

        UpdateQuickActionsVisibility();
    }

    private void OnMissedSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MissedListView.SelectedItem is not null)
        {
            ClearSelectionExcept(MissedListView);
        }

        UpdateQuickActionsVisibility();
    }

    private void OnHistorySelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (HistoryListView.SelectedItem is not null)
        {
            ClearSelectionExcept(HistoryListView);
        }

        UpdateQuickActionsVisibility();
    }

    private void ClearSelectionExcept(System.Windows.Controls.ListView selectedList)
    {
        if (!ReferenceEquals(selectedList, UpcomingListView))
        {
            UpcomingListView.SelectedItem = null;
        }

        if (!ReferenceEquals(selectedList, MissedListView))
        {
            MissedListView.SelectedItem = null;
        }

        if (!ReferenceEquals(selectedList, HistoryListView))
        {
            HistoryListView.SelectedItem = null;
        }

        UpdateQuickActionsVisibility();
    }

    private void ClearAllSelections()
    {
        if (UpcomingListView.SelectedItem is null &&
            MissedListView.SelectedItem is null &&
            HistoryListView.SelectedItem is null)
        {
            return;
        }

        UpcomingListView.SelectedItem = null;
        MissedListView.SelectedItem = null;
        HistoryListView.SelectedItem = null;
        UpdateQuickActionsVisibility();
    }

    private void UpdateQuickActionsVisibility()
    {
        var hasUpcomingSelection = UpcomingListView.SelectedItem is Reminder;
        var hasDeleteOnlySelection = !hasUpcomingSelection &&
                                     (MissedListView.SelectedItem is Reminder || HistoryListView.SelectedItem is Reminder);

        UpcomingActionsPanel.Visibility = hasUpcomingSelection
            ? Visibility.Visible
            : Visibility.Collapsed;

        DeleteOnlyActionsPanel.Visibility = hasDeleteOnlySelection
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void ApplyHotKeyConfiguration()
    {
        _hotKeyManager?.Dispose();
        _hotKeyManager = null;
        _hotKeyRegistrationFailed = false;

        var app = GetApp();
        var settingsChanged = NormalizeHotKeySettings(app.Settings);

        if (!app.Settings.HotKeyEnabled)
        {
            if (settingsChanged)
            {
                app.SaveSettings();
            }

            UpdateHotKeyHintText();
            return;
        }

        if (!HotKeyBinding.TryParse(app.Settings.HotKeyGesture, out var modifiers, out var virtualKey, out var normalized))
        {
            UpdateHotKeyHintText();
            return;
        }

        if (!string.Equals(app.Settings.HotKeyGesture, normalized, StringComparison.Ordinal))
        {
            app.Settings.HotKeyGesture = normalized;
            settingsChanged = true;
        }

        var manager = new HotKeyManager(this, HotKeyId, modifiers, virtualKey);
        manager.HotKeyPressed += () =>
        {
            _ = Dispatcher.InvokeAsync(async () =>
            {
                var owner = IsVisible && WindowState != WindowState.Minimized ? this : null;
                await GetApp().AddReminderInteractiveAsync(owner);
            });
        };
        if (!manager.Register())
        {
            _hotKeyRegistrationFailed = true;
            Services.ErrorLogger.Log(
                "Failed to register global hotkey",
                new InvalidOperationException($"Hotkey '{normalized}' is unavailable."));
            manager.Dispose();
            _hotKeyManager = null;
            UpdateHotKeyHintText();
            return;
        }

        _hotKeyManager = manager;
        if (settingsChanged)
        {
            app.SaveSettings();
        }

        UpdateHotKeyHintText();
    }

    private void UpdateHotKeyHintText()
    {
        var app = GetApp();
        if (!app.Settings.HotKeyEnabled)
        {
            HotKeyHintText.Text = Loc.Text("MainHotKeyDisabled");
            return;
        }

        var gesture = HotKeyBinding.TryParse(app.Settings.HotKeyGesture, out _, out _, out var normalized)
            ? normalized
            : HotKeyBinding.DefaultGesture;
        HotKeyHintText.Text = _hotKeyRegistrationFailed
            ? Loc.Format("MainHotKeyBusyTemplate", gesture)
            : Loc.Format("MainHotKeyReadyTemplate", gesture);
    }

    private static bool NormalizeHotKeySettings(AppSettings settings)
    {
        var changed = false;

        if (string.IsNullOrWhiteSpace(settings.HotKeyGesture))
        {
            settings.HotKeyGesture = HotKeyBinding.DefaultGesture;
            changed = true;
        }

        if (!HotKeyBinding.TryParse(settings.HotKeyGesture, out _, out _, out var normalized))
        {
            settings.HotKeyGesture = HotKeyBinding.DefaultGesture;
            changed = true;
        }
        else if (!string.Equals(settings.HotKeyGesture, normalized, StringComparison.Ordinal))
        {
            settings.HotKeyGesture = normalized;
            changed = true;
        }

        return changed;
    }

    private void ToggleMaximizeRestore()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void UpdateMaximizeGlyph()
    {
        MaxRestoreGlyph.Text = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
    }

    private Reminder? GetSelectedUpcoming()
    {
        return UpcomingListView.SelectedItem as Reminder;
    }

    private Reminder? GetSelectedAny()
    {
        if (UpcomingListView.SelectedItem is Reminder upcomingSelected)
        {
            return upcomingSelected;
        }

        if (MissedListView.SelectedItem is Reminder missedSelected)
        {
            return missedSelected;
        }

        return HistoryListView.SelectedItem as Reminder;
    }

    private async Task DeleteReminderAsync(Reminder selected)
    {
        var isHistoryItem = selected.Status is ReminderStatus.Acked or ReminderStatus.Cancelled;
        var confirmTitle = isHistoryItem ? Loc.Text("MainDeleteHistoryTitle") : Loc.Text("MainDeleteReminderTitle");
        var confirmText = isHistoryItem
            ? Loc.Format("MainDeleteHistoryMessageTemplate", selected.Title)
            : Loc.Format("MainDeleteReminderMessageTemplate", selected.Title);
        var confirmDialog = new ConfirmDialogWindow(
            confirmTitle,
            confirmText,
            Loc.Text("CommonDelete"))
        {
            Owner = this
        };

        if (confirmDialog.ShowDialog() != true)
        {
            return;
        }

        var app = GetApp();
        if (isHistoryItem)
        {
            await app.Repository.DeleteAsync(selected.Id);
        }
        else
        {
            await app.Repository.CancelAsync(selected.Id, DateTimeOffset.UtcNow);
        }

        app.Scheduler.SignalReschedule();
        await ReloadDataAsync();
    }

    private void ScheduleColumnAdjust()
    {
        _ = Dispatcher.BeginInvoke(AdjustGridColumns, System.Windows.Threading.DispatcherPriority.Background);
    }

    private void AdjustGridColumns()
    {
        AdjustUpcomingColumns();
        AdjustMissedColumns();
        AdjustHistoryColumns();
    }

    private void AdjustUpcomingColumns()
    {
        if (UpcomingListView.View is not System.Windows.Controls.GridView view || view.Columns.Count < 4)
        {
            return;
        }

        var available = GetListViewportWidth(UpcomingListView);
        if (available <= 0)
        {
            return;
        }

        var widths = CalculateResponsiveWidths(available - 2, UpcomingPreferredWidths, UpcomingMinWidths, expandTextColumnIndex: 2);
        ApplyColumnWidths(view, widths);
    }

    private void AdjustMissedColumns()
    {
        if (MissedListView.View is not System.Windows.Controls.GridView view || view.Columns.Count < 3)
        {
            return;
        }

        var available = GetListViewportWidth(MissedListView);
        if (available <= 0)
        {
            return;
        }

        var widths = CalculateResponsiveWidths(available - 2, MissedPreferredWidths, MissedMinWidths, expandTextColumnIndex: 2);
        ApplyColumnWidths(view, widths);
    }

    private void AdjustHistoryColumns()
    {
        if (HistoryListView.View is not System.Windows.Controls.GridView view || view.Columns.Count < 5)
        {
            return;
        }

        var available = GetListViewportWidth(HistoryListView);
        if (available <= 0)
        {
            return;
        }

        var widths = CalculateResponsiveWidths(available - 2, HistoryPreferredWidths, HistoryMinWidths, expandTextColumnIndex: 2);
        ApplyColumnWidths(view, widths);
    }

    private static double GetListViewportWidth(System.Windows.Controls.ListView listView)
    {
        var scrollViewer = FindVisualChild<System.Windows.Controls.ScrollViewer>(listView);
        if (scrollViewer is not null && scrollViewer.ViewportWidth > 0)
        {
            return scrollViewer.ViewportWidth;
        }

        return Math.Max(0, listView.ActualWidth - 2);
    }

    private static double GetColumnWidth(System.Windows.Controls.GridViewColumn column)
    {
        var width = column.Width;
        if (double.IsNaN(width) || width <= 0)
        {
            width = column.ActualWidth;
        }

        return width > 0 ? width : 0;
    }

    private static double[] CalculateResponsiveWidths(double availableWidth, double[] preferred, double[] min, int expandTextColumnIndex)
    {
        var result = (double[])preferred.Clone();
        if (availableWidth <= 0)
        {
            return result;
        }

        var preferredTotal = preferred.Sum();
        var minTotal = min.Sum();

        if (availableWidth >= preferredTotal)
        {
            result[expandTextColumnIndex] += availableWidth - preferredTotal;
            return result;
        }

        if (availableWidth <= minTotal)
        {
            return (double[])min.Clone();
        }

        var needShrink = preferredTotal - availableWidth;
        var shrinkCapacityTotal = 0d;
        for (var i = 0; i < preferred.Length; i++)
        {
            shrinkCapacityTotal += Math.Max(0, preferred[i] - min[i]);
        }

        if (shrinkCapacityTotal <= 0)
        {
            return (double[])min.Clone();
        }

        for (var i = 0; i < result.Length; i++)
        {
            var capacity = Math.Max(0, preferred[i] - min[i]);
            var shrink = needShrink * (capacity / shrinkCapacityTotal);
            result[i] = Math.Max(min[i], preferred[i] - shrink);
        }

        // Keep exact fit to viewport to avoid right-edge gaps.
        var delta = availableWidth - result.Sum();
        result[^1] = Math.Max(min[^1], result[^1] + delta);
        return result;
    }

    private static void ApplyColumnWidths(System.Windows.Controls.GridView view, double[] widths)
    {
        var count = Math.Min(view.Columns.Count, widths.Length);
        for (var i = 0; i < count; i++)
        {
            view.Columns[i].Width = widths[i];
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T target)
            {
                return target;
            }

            var nested = FindVisualChild<T>(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
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

    private void StartEntranceAnimation()
    {
        AnimateEntrance(TitleBarCard, 0);
        AnimateEntrance(HeaderCard, 70);
        AnimateEntrance(UpcomingCard, 130);
        AnimateEntrance(MissedCard, 190);
        AnimateEntrance(HistoryCard, 250);
        AnimateEntrance(FooterCard, 310);
    }

    private static void PrepareEntranceVisual(FrameworkElement element)
    {
        element.Opacity = 0;
        element.RenderTransform = new TranslateTransform(0, 18);
    }

    private static void AnimateEntrance(FrameworkElement element, int delayMs)
    {
        if (element.RenderTransform is not TranslateTransform transform)
        {
            transform = new TranslateTransform(0, 18);
            element.RenderTransform = transform;
        }

        var fadeAnimation = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(360))
        {
            BeginTime = TimeSpan.FromMilliseconds(delayMs),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        var slideAnimation = new DoubleAnimation(18, 0, TimeSpan.FromMilliseconds(430))
        {
            BeginTime = TimeSpan.FromMilliseconds(delayMs),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        element.BeginAnimation(UIElement.OpacityProperty, fadeAnimation);
        transform.BeginAnimation(TranslateTransform.YProperty, slideAnimation);
    }

    private static void ReplaceCollection(ObservableCollection<Reminder> target, IEnumerable<Reminder> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
    }

    private static App GetApp()
    {
        return (App)System.Windows.Application.Current;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WmGetMinMaxInfo = 0x0024;
        if (msg != WmGetMinMaxInfo)
        {
            return IntPtr.Zero;
        }

        ApplyWorkAreaMaxBounds(hwnd, lParam);
        handled = true;
        return IntPtr.Zero;
    }

    private static void ApplyWorkAreaMaxBounds(IntPtr hwnd, IntPtr lParam)
    {
        const uint MonitorDefaultToNearest = 0x00000002;
        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return;
        }

        var monitorInfo = new MonitorInfo();
        monitorInfo.Size = Marshal.SizeOf<MonitorInfo>();
        if (!GetMonitorInfo(monitor, ref monitorInfo))
        {
            return;
        }

        var minMaxInfo = Marshal.PtrToStructure<MinMaxInfo>(lParam);
        var workArea = monitorInfo.WorkArea;
        var monitorArea = monitorInfo.MonitorArea;

        minMaxInfo.MaxPosition.X = Math.Abs(workArea.Left - monitorArea.Left);
        minMaxInfo.MaxPosition.Y = Math.Abs(workArea.Top - monitorArea.Top);
        minMaxInfo.MaxSize.X = Math.Abs(workArea.Right - workArea.Left);
        minMaxInfo.MaxSize.Y = Math.Abs(workArea.Bottom - workArea.Top);

        Marshal.StructureToPtr(minMaxInfo, lParam, true);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PointInt
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public PointInt Reserved;
        public PointInt MaxSize;
        public PointInt MaxPosition;
        public PointInt MinTrackSize;
        public PointInt MaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RectInt
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int Size;
        public RectInt MonitorArea;
        public RectInt WorkArea;
        public int Flags;
    }

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);
}








