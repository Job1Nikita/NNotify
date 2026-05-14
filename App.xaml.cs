using System.Media;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using NNotify.Data;
using NNotify.Localization;
using NNotify.Models;
using NNotify.Services;
using NNotify.Windows;
using WF = System.Windows.Forms;

namespace NNotify;

public partial class App : System.Windows.Application
{
    private const string SingleInstanceMutexName = @"Local\NNotify.SingleInstance.Mutex";
    private const string SingleInstanceShowSignalName = @"Local\NNotify.SingleInstance.ShowSignal";
    private const string ReminderSoundResourceName = "NNotify.Assets.Sounds.WindowsNotifyCalendar.wav";

    private static readonly object ReminderSoundSync = new();
    private static SoundPlayer? _reminderSoundPlayer;
    private static MemoryStream? _reminderSoundStream;

    private WF.NotifyIcon? _notifyIcon;
    private TrayMenuWindow? _trayMenuWindow;
    private System.Drawing.Icon? _trayIcon;
    private bool _ownsTrayIcon;
    private bool _isExitRequested;
    private Mutex? _singleInstanceMutex;
    private bool _singleInstanceMutexOwned;
    private EventWaitHandle? _singleInstanceShowSignal;
    private CancellationTokenSource? _singleInstanceSignalCts;
    private Task? _singleInstanceSignalTask;
    private bool _pendingExternalShowRequest;
    private AddEditReminderWindow? _activeAddReminderDialog;
    private readonly Dictionary<string, ReminderOverlayWindow> _activeOverlays = [];

    public ReminderRepository Repository { get; private set; } = null!;
    public SettingsService SettingsService { get; private set; } = null!;
    public StartupRegistrationService StartupRegistrationService { get; private set; } = null!;
    public TelegramService TelegramService { get; private set; } = null!;
    public SyncAuthService SyncAuthService { get; private set; } = null!;
    public SyncReminderService SyncReminderService { get; private set; } = null!;
    public UpdateService UpdateService { get; private set; } = null!;
    public SchedulerService Scheduler { get; private set; } = null!;
    public AppSettings Settings { get; private set; } = new();
    public MainWindow? MainAppWindow { get; private set; }

    public bool IsExitRequested => _isExitRequested;

    protected override async void OnStartup(StartupEventArgs e)
    {
        LocalizationService.Instance.UseSystemCulture();
        base.OnStartup(e);

        if (!TryAcquireSingleInstanceLock())
        {
            SignalRunningInstanceToShow();
            Shutdown();
            return;
        }

        try
        {
            SettingsService = new SettingsService();
            StartupRegistrationService = new StartupRegistrationService();
            Settings = SettingsService.Load();
            MigrateLegacyPlainTokenIfNeeded();
            ApplyTheme(Settings.UseDarkTheme);

            Repository = new ReminderRepository();
            await Repository.InitializeAsync();
            await Repository.MarkMissedAtStartupAsync(DateTimeOffset.UtcNow);

            TelegramService = new TelegramService();
            SyncAuthService = new SyncAuthService();
            UpdateService = new UpdateService();
            SyncReminderService = new SyncReminderService(Repository, SettingsService, SyncAuthService, () => Settings, SaveSettings);
            SyncReminderService.RemoteRemindersApplied += OnRemoteRemindersApplied;
            Scheduler = new SchedulerService(Repository, TelegramService, SettingsService, () => Settings);
            Scheduler.ReminderDueHandlerAsync = ShowReminderOverlayAsync;
            Scheduler.RemindersChanged += OnSchedulerRemindersChanged;

            MainAppWindow = new MainWindow();
            MainWindow = MainAppWindow;
            MainAppWindow.Topmost = Settings.MainWindowTopmost;
            MainAppWindow.Show();
            if (_pendingExternalShowRequest)
            {
                _pendingExternalShowRequest = false;
                ShowMainWindow();
            }

            SetupNotifyIcon();

            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            SystemEvents.TimeChanged += OnSystemTimeChanged;

            Scheduler.Start();
            SyncReminderService.Start();
            ScheduleStartupUpdateCheck();
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("Application startup failed", ex);
            System.Windows.MessageBox.Show(
                Loc.Text("AppStartupFailedBody"),
                Loc.Text("AppName"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    public async Task AddReminderInteractiveAsync(Window? owner = null)
    {
        var ownsActiveDialogSlot = false;
        try
        {
            if (_activeAddReminderDialog is not null)
            {
                BringWindowToFront(_activeAddReminderDialog);
                return;
            }

            var dialog = new AddEditReminderWindow();
            _activeAddReminderDialog = dialog;
            ownsActiveDialogSlot = true;
            dialog.Closed += (_, _) =>
            {
                if (ReferenceEquals(_activeAddReminderDialog, dialog))
                {
                    _activeAddReminderDialog = null;
                }
            };

            var dialogOwner = owner;
            if (dialogOwner is null &&
                MainAppWindow is { IsVisible: true, WindowState: not WindowState.Minimized })
            {
                dialogOwner = MainAppWindow;
            }

            var shouldForceForeground = dialogOwner is null;
            if (dialogOwner is not null)
            {
                dialog.Owner = dialogOwner;
            }
            else
            {
                dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            if (shouldForceForeground)
            {
                dialog.Topmost = true;
                dialog.Loaded += (_, _) => BringWindowToFront(dialog);
            }

            if (ShowDialogWithOptionalBackdrop(dialog) != true || dialog.EditedReminder is null)
            {
                return;
            }

            await Repository.AddReminderAsync(dialog.EditedReminder);
            SyncReminderService.SignalSync();
            Scheduler.SignalReschedule();
            await RefreshMainWindowAsync();
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("Failed to add reminder", ex);
        }
        finally
        {
            if (ownsActiveDialogSlot)
            {
                _activeAddReminderDialog = null;
            }
        }
    }

    public async Task RepeatReminderInteractiveAsync(Reminder sourceReminder, Window? owner = null)
    {
        try
        {
            var dialog = new AddEditReminderWindow(sourceReminder, createFromTemplate: true);
            var dialogOwner = owner;
            if (dialogOwner is null &&
                MainAppWindow is { IsVisible: true, WindowState: not WindowState.Minimized })
            {
                dialogOwner = MainAppWindow;
            }

            if (dialogOwner is not null)
            {
                dialog.Owner = dialogOwner;
            }

            if (ShowDialogWithOptionalBackdrop(dialog) != true || dialog.EditedReminder is null)
            {
                return;
            }

            await Repository.AddReminderAsync(dialog.EditedReminder);
            SyncReminderService.SignalSync();
            Scheduler.SignalReschedule();
            await RefreshMainWindowAsync();
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("Failed to repeat reminder", ex);
        }
    }

    public async Task<bool> OpenEditReminderDialogAsync(Reminder reminder, Window? owner = null)
    {
        try
        {
            var latest = await Repository.GetByIdAsync(reminder.Id) ?? reminder;
            var dialog = new AddEditReminderWindow(latest)
            {
                Owner = owner ?? MainAppWindow
            };

            if (ShowDialogWithOptionalBackdrop(dialog) != true || dialog.EditedReminder is null)
            {
                return false;
            }

            await Repository.UpdateReminderAsync(dialog.EditedReminder);
            SyncReminderService.SignalSync();
            Scheduler.SignalReschedule();
            await RefreshMainWindowAsync();
            return true;
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("Failed to edit reminder", ex);
            return false;
        }
    }

    public void ShowMainWindow()
    {
        CloseTrayMenu();

        if (MainAppWindow is null)
        {
            return;
        }

        if (!MainAppWindow.IsVisible)
        {
            MainAppWindow.Show();
        }

        if (MainAppWindow.WindowState == WindowState.Minimized)
        {
            MainAppWindow.WindowState = WindowState.Normal;
        }

        MainAppWindow.Topmost = true;
        MainAppWindow.Activate();
        MainAppWindow.Topmost = Settings.MainWindowTopmost;
    }

    private static void BringWindowToFront(Window window)
    {
        if (!window.IsVisible)
        {
            return;
        }

        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        var keepTopmost = window.Topmost;
        window.Topmost = true;
        window.Activate();
        window.Focus();

        var handle = new WindowInteropHelper(window).Handle;
        if (handle != IntPtr.Zero)
        {
            SetForegroundWindow(handle);
        }

        window.Topmost = keepTopmost;
    }

    public void SetMainWindowTopmost(bool isTopmost)
    {
        Settings.MainWindowTopmost = isTopmost;
        SaveSettings();
    }

    public void SetDarkTheme(bool isDark)
    {
        Settings.UseDarkTheme = isDark;
        SaveSettings();
        ApplyTheme(isDark);
    }

    public void SaveSettings()
    {
        SettingsService.Save(Settings);
    }

    public async Task CheckForUpdatesInteractiveAsync(bool showUpToDateMessage)
    {
        try
        {
            var result = await UpdateService.CheckLatestAsync();
            Settings.LastUpdateCheckUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            SaveSettings();

            if (!result.Success)
            {
                if (showUpToDateMessage)
                {
                    ShowInfoDialog(Loc.Text("UpdateCheckFailedTitle"), Loc.Text("UpdateCheckFailedBody"));
                }

                return;
            }

            if (!result.IsUpdateAvailable || string.IsNullOrWhiteSpace(result.LatestVersion))
            {
                if (showUpToDateMessage)
                {
                    ShowInfoDialog(
                        Loc.Text("UpdateNoUpdatesTitle"),
                        Loc.Format("UpdateNoUpdatesBodyTemplate", result.CurrentVersion));
                }

                return;
            }

            await ShowUpdateAvailableDialogAsync(result, respectIgnoredVersion: !showUpToDateMessage);
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("Failed to check for updates", ex);
            if (showUpToDateMessage)
            {
                ShowInfoDialog(Loc.Text("UpdateCheckFailedTitle"), Loc.Text("UpdateCheckFailedBody"));
            }
        }
    }

    private void ScheduleStartupUpdateCheck()
    {
        if (!Settings.CheckUpdatesAutomatically)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (Settings.LastUpdateCheckUtc > 0 &&
            now - Settings.LastUpdateCheckUtc < (long)TimeSpan.FromHours(24).TotalSeconds)
        {
            return;
        }

        _ = Dispatcher.InvokeAsync(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(3));
            await CheckForUpdatesInteractiveAsync(showUpToDateMessage: false);
        });
    }

    private async Task ShowUpdateAvailableDialogAsync(UpdateCheckResult result, bool respectIgnoredVersion)
    {
        if (string.IsNullOrWhiteSpace(result.LatestVersion))
        {
            return;
        }

        if (respectIgnoredVersion &&
            string.Equals(Settings.IgnoredUpdateVersion, result.LatestVersion, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await Dispatcher.InvokeAsync(() =>
        {
            var dialog = new UpdateAvailableWindow(result.CurrentVersion, result.LatestVersion)
            {
                Owner = MainAppWindow
            };

            ShowDialogWithOptionalBackdrop(dialog);

            if (dialog.Action == UpdateDialogAction.IgnoreVersion)
            {
                Settings.IgnoredUpdateVersion = result.LatestVersion;
                SaveSettings();
                return;
            }

            if (dialog.Action == UpdateDialogAction.OpenReleasePage)
            {
                OpenExternalUrl(string.IsNullOrWhiteSpace(result.ReleaseUrl)
                    ? UpdateService.ReleasesPageUrl
                    : result.ReleaseUrl);
            }
        });
    }

    private void ShowInfoDialog(string title, string message)
    {
        var dialog = new ConfirmDialogWindow(
            title,
            message,
            confirmText: Loc.Text("CommonOk"),
            showCancel: false,
            destructive: false)
        {
            Owner = MainAppWindow
        };

        ShowDialogWithOptionalBackdrop(dialog);
    }

    private static bool? ShowDialogWithOptionalBackdrop(Window dialog)
    {
        return dialog.Owner is MainWindow mainWindow
            ? mainWindow.ShowDialogWithBackdrop(dialog)
            : dialog.ShowDialog();
    }

    private static void OpenExternalUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url)
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("Failed to open update release page", ex);
        }
    }

    private bool TryAcquireSingleInstanceLock()
    {
        try
        {
            _singleInstanceMutex = new Mutex(initiallyOwned: false, SingleInstanceMutexName);

            var hasHandle = false;
            try
            {
                hasHandle = _singleInstanceMutex.WaitOne(0, false);
            }
            catch (AbandonedMutexException)
            {
                hasHandle = true;
            }

            if (!hasHandle)
            {
                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
                return false;
            }

            _singleInstanceMutexOwned = true;
            StartSingleInstanceSignalListener();
            return true;
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("Failed to initialize single-instance lock", ex);
            return true;
        }
    }

    private void StartSingleInstanceSignalListener()
    {
        try
        {
            _singleInstanceShowSignal = new EventWaitHandle(
                initialState: false,
                mode: EventResetMode.AutoReset,
                name: SingleInstanceShowSignalName);
            _singleInstanceSignalCts = new CancellationTokenSource();
            _singleInstanceSignalTask = Task.Run(() => ListenForExternalActivationAsync(_singleInstanceSignalCts.Token));
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("Failed to initialize activation signal listener", ex);
        }
    }

    private async Task ListenForExternalActivationAsync(CancellationToken cancellationToken)
    {
        if (_singleInstanceShowSignal is null)
        {
            return;
        }

        var handles = new WaitHandle[] { _singleInstanceShowSignal, cancellationToken.WaitHandle };
        while (!cancellationToken.IsCancellationRequested)
        {
            var signaled = WaitHandle.WaitAny(handles);
            if (signaled == 1)
            {
                return;
            }

            await Dispatcher.InvokeAsync(RequestShowFromExternalActivation);
        }
    }

    private void RequestShowFromExternalActivation()
    {
        if (MainAppWindow is null)
        {
            _pendingExternalShowRequest = true;
            return;
        }

        ShowMainWindow();
    }

    private static void SignalRunningInstanceToShow()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                using var signal = EventWaitHandle.OpenExisting(SingleInstanceShowSignalName);
                signal.Set();
                return;
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                Thread.Sleep(80);
            }
            catch
            {
                return;
            }
        }
    }

    private void ReleaseSingleInstanceInfrastructure()
    {
        try
        {
            if (_singleInstanceSignalCts is not null)
            {
                _singleInstanceSignalCts.Cancel();
            }

            _singleInstanceShowSignal?.Set();
            _singleInstanceSignalTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("Failed to release single-instance infrastructure", ex);
        }
        finally
        {
            _singleInstanceSignalCts?.Dispose();
            _singleInstanceSignalCts = null;
            _singleInstanceSignalTask = null;

            _singleInstanceShowSignal?.Dispose();
            _singleInstanceShowSignal = null;

            if (_singleInstanceMutexOwned && _singleInstanceMutex is not null)
            {
                try
                {
                    _singleInstanceMutex.ReleaseMutex();
                }
                catch
                {
                    // Ignore release races.
                }
            }

            _singleInstanceMutexOwned = false;
            _singleInstanceMutex?.Dispose();
            _singleInstanceMutex = null;
        }
    }

    public async Task ExitApplicationAsync()
    {
        if (_isExitRequested)
        {
            return;
        }

        _isExitRequested = true;

        try
        {
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            SystemEvents.TimeChanged -= OnSystemTimeChanged;
            CloseTrayMenu();

            if (_notifyIcon is not null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }
            DisposeTrayIcon();

            if (Scheduler is not null)
            {
                await Scheduler.StopAsync();
            }

            if (SyncReminderService is not null)
            {
                await SyncReminderService.StopAsync();
            }

            MainAppWindow?.Close();
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("Failed to exit app", ex);
        }
        finally
        {
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            SystemEvents.TimeChanged -= OnSystemTimeChanged;
            CloseTrayMenu();

            if (_notifyIcon is not null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }
            DisposeTrayIcon();

            if (Scheduler is not null)
            {
                Scheduler.StopAsync().GetAwaiter().GetResult();
                Scheduler.Dispose();
            }

            if (SyncReminderService is not null)
            {
                SyncReminderService.StopAsync().GetAwaiter().GetResult();
                SyncReminderService.Dispose();
            }
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("OnExit cleanup failed", ex);
        }
        finally
        {
            DisposeReminderSoundResources();
            ReleaseSingleInstanceInfrastructure();
        }

        base.OnExit(e);
    }

    private async Task<OverlayAction> ShowReminderOverlayAsync(Reminder reminder)
    {
        try
        {
            PlayReminderSound();
            return await Dispatcher.InvokeAsync(() => ShowReminderOverlayOnUiAsync(reminder)).Task.Unwrap();
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("Failed to show overlay", ex);
            return OverlayAction.Timeout;
        }
    }

    private static void PlayReminderSound()
    {
        try
        {
            var player = EnsureReminderSoundPlayer();
            if (player is not null)
            {
                if (player.Stream is { CanSeek: true })
                {
                    player.Stream.Position = 0;
                }

                player.Play();
                return;
            }

            SystemSounds.Asterisk.Play();
        }
        catch
        {
            // Ignore sound playback failures.
        }
    }

    private static SoundPlayer? EnsureReminderSoundPlayer()
    {
        if (_reminderSoundPlayer is not null)
        {
            return _reminderSoundPlayer;
        }

        lock (ReminderSoundSync)
        {
            if (_reminderSoundPlayer is not null)
            {
                return _reminderSoundPlayer;
            }

            try
            {
                using var resourceStream = typeof(App).Assembly.GetManifestResourceStream(ReminderSoundResourceName);
                if (resourceStream is null)
                {
                    ErrorLogger.Log(
                        "Embedded reminder sound resource not found",
                        new FileNotFoundException($"Resource '{ReminderSoundResourceName}' was not found."));
                    return null;
                }

                _reminderSoundStream = new MemoryStream();
                resourceStream.CopyTo(_reminderSoundStream);
                _reminderSoundStream.Position = 0;

                var player = new SoundPlayer(_reminderSoundStream);
                player.Load();
                _reminderSoundStream.Position = 0;
                _reminderSoundPlayer = player;
                return _reminderSoundPlayer;
            }
            catch (Exception ex)
            {
                ErrorLogger.Log("Failed to initialize embedded reminder sound", ex);
                _reminderSoundStream?.Dispose();
                _reminderSoundStream = null;
                _reminderSoundPlayer = null;
                return null;
            }
        }
    }

    private static void DisposeReminderSoundResources()
    {
        lock (ReminderSoundSync)
        {
            _reminderSoundPlayer = null;
            _reminderSoundStream?.Dispose();
            _reminderSoundStream = null;
        }
    }

    private async Task<OverlayAction> ShowReminderOverlayOnUiAsync(Reminder reminder)
    {
        ReminderOverlayWindow? overlay = null;
        overlay = new ReminderOverlayWindow(
            reminder,
            async r => await OpenEditReminderDialogAsync(r, overlay),
            IsTelegramEscalationConfigured());

        _activeOverlays[reminder.Id] = overlay;
        overlay.Closed += (_, _) =>
        {
            if (_activeOverlays.TryGetValue(reminder.Id, out var current) && ReferenceEquals(current, overlay))
            {
                _activeOverlays.Remove(reminder.Id);
            }
        };

        overlay.Show();
        overlay.Activate();
        return await overlay.WaitForActionAsync(TimeSpan.FromSeconds(60));
    }

    private void SetupNotifyIcon()
    {
        _trayIcon = GetTrayIcon();
        _notifyIcon = new WF.NotifyIcon
        {
            Visible = true,
            Icon = _trayIcon ?? System.Drawing.SystemIcons.Information,
            Text = "NNotify"
        };

        _notifyIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowMainWindow);
        _notifyIcon.MouseUp += OnNotifyIconMouseUp;
    }

    private void OnNotifyIconMouseUp(object? sender, WF.MouseEventArgs e)
    {
        if (e.Button == WF.MouseButtons.Left)
        {
            Dispatcher.Invoke(ShowMainWindow);
            return;
        }

        if (e.Button != WF.MouseButtons.Right)
        {
            return;
        }

        _ = Dispatcher.InvokeAsync(ShowTrayMenuAtCursor);
    }

    private void ShowTrayMenuAtCursor()
    {
        if (MainAppWindow is null)
        {
            return;
        }

        CloseTrayMenu();

        var cursor = WF.Cursor.Position;
        var menuWindow = new TrayMenuWindow
        {
            Owner = MainAppWindow
        };
        menuWindow.OpenRequested += ShowMainWindow;
        menuWindow.AddRequested += () =>
        {
            _ = Dispatcher.InvokeAsync(async () => await AddReminderInteractiveAsync());
        };
        menuWindow.ExitRequested += () =>
        {
            _ = Dispatcher.InvokeAsync(async () => await ExitApplicationAsync());
        };
        menuWindow.Closed += (_, _) =>
        {
            if (ReferenceEquals(_trayMenuWindow, menuWindow))
            {
                _trayMenuWindow = null;
            }
        };

        _trayMenuWindow = menuWindow;
        menuWindow.ShowNearCursor(cursor.X, cursor.Y);
    }

    private void CloseTrayMenu()
    {
        if (_trayMenuWindow is null)
        {
            return;
        }

        try
        {
            _trayMenuWindow.Close();
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("Failed to close tray menu window", ex);
        }
        finally
        {
            _trayMenuWindow = null;
        }
    }

    private System.Drawing.Icon GetTrayIcon()
    {
        try
        {
            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(processPath))
            {
                var icon = System.Drawing.Icon.ExtractAssociatedIcon(processPath);
                if (icon is not null)
                {
                    _ownsTrayIcon = true;
                    return icon;
                }
            }
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("Failed to load tray icon from executable", ex);
        }

        _ownsTrayIcon = false;
        return System.Drawing.SystemIcons.Information;
    }

    private void DisposeTrayIcon()
    {
        if (_ownsTrayIcon && _trayIcon is not null)
        {
            _trayIcon.Dispose();
        }

        _trayIcon = null;
        _ownsTrayIcon = false;
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            Scheduler.SignalReschedule();
        }
    }

    private void OnSystemTimeChanged(object? sender, EventArgs e)
    {
        Scheduler.SignalReschedule();
    }

    private void OnSchedulerRemindersChanged()
    {
        SyncReminderService.SignalSync();

        if (MainAppWindow is null)
        {
            return;
        }

        _ = MainAppWindow.Dispatcher.InvokeAsync(async () => await MainAppWindow.ReloadDataAsync());
    }

    private void OnRemoteRemindersApplied(IReadOnlyList<Reminder> reminders)
    {
        if (MainAppWindow is not null)
        {
            _ = MainAppWindow.Dispatcher.InvokeAsync(async () => await MainAppWindow.ReloadDataAsync());
        }

        foreach (var reminder in reminders)
        {
            if (reminder.Status is ReminderStatus.Acked or ReminderStatus.Cancelled or ReminderStatus.Snoozed &&
                _activeOverlays.TryGetValue(reminder.Id, out var overlay))
            {
                _ = Dispatcher.InvokeAsync(() => overlay.DismissAsSynced());
            }
        }

        Scheduler.SignalReschedule();
    }

    private async Task RefreshMainWindowAsync()
    {
        if (MainAppWindow is null)
        {
            return;
        }

        if (MainAppWindow.Dispatcher.CheckAccess())
        {
            await MainAppWindow.ReloadDataAsync();
            return;
        }

        await MainAppWindow.Dispatcher.InvokeAsync(() => MainAppWindow.ReloadDataAsync()).Task.Unwrap();
    }

    private void ApplyTheme(bool dark)
    {
        if (dark)
        {
            SetBrush("WindowBackgroundBrush", "#101827");
            SetBrush("PanelBackgroundBrush", "#182235");
            SetBrush("PrimaryTextBrush", "#EAF0FA");
            SetBrush("SecondaryTextBrush", "#9AACCB");
            SetBrush("AccentBrush", "#57A6FF");
            SetBrush("AccentTextBrush", "#081522");
            SetBrush("BorderBrushLight", "#2A3A50");
            SetBrush("HoverBrush", "#223047");
            SetBrush("ButtonBackgroundBrush", "#192438");
            SetBrush("ButtonHoverBackgroundBrush", "#21314A");
            SetBrush("ButtonPressedBackgroundBrush", "#172238");
            SetBrush("ButtonBorderBrush", "#30435C");
            SetBrush("ButtonHoverBorderBrush", "#5C9BEA");
            SetBrush("ListRowBorderBrush", "#2F3D54");
            SetBrush("ListRowHoverBrush", "#28364B");
            SetBrush("ListRowSelectedBrush", "#1D3D63");
            SetBrush("SelectedRowBorderBrush", "#46658C");
            SetBrush("ListHeaderBackgroundBrush", "#263246");
            SetBrush("ListHeaderHoverBrush", "#32425A");
            SetBrush("ContextMenuBackgroundBrush", "#1F2735");
            SetBrush("ContextMenuBorderBrush", "#3A4A63");
            SetBrush("ContextMenuHoverBrush", "#2B3B52");
            SetBrush("CheckBoxBoxBackgroundBrush", "#1C2533");
            SetBrush("CheckBoxBoxBorderBrush", "#50627F");
            SetBrush("CheckBoxBoxHoverBrush", "#273449");
            SetBrush("TitleBarButtonHoverBrush", "#2F3C53");
            SetBrush("TitleBarCloseHoverBrush", "#D34B4B");
            SetBrush("BadgeTextBrush", "#EAF0FA");
            SetBrush("PriorityP0BadgeBrush", "#5A2F3B");
            SetBrush("PriorityP1BadgeBrush", "#27476A");
            SetBrush("PriorityP2BadgeBrush", "#24513B");
            SetBrush("StatusDefaultBadgeBrush", "#203B5F");
            SetBrush("StatusAckBadgeBrush", "#24513B");
            SetBrush("StatusMissedBadgeBrush", "#5A462E");
            SetBrush("StatusFiredBadgeBrush", "#5A2F3B");
            SetBrush("ScrollBarTrackBrush", "#121D2E");
            SetBrush("ScrollBarThumbBrush", "#3B4D68");
            SetBrush("ScrollBarThumbHoverBrush", "#647A98");
            SetBrush("ScrollBarThumbPressedBrush", "#7690B4");
            SetBrush("ModalBackdropBrush", "#66000000");
            SetSystemBrush(System.Windows.SystemColors.ControlBrushKey, "#1B2738");
            SetSystemBrush(System.Windows.SystemColors.ControlLightBrushKey, "#314058");
            SetSystemBrush(System.Windows.SystemColors.ControlDarkBrushKey, "#314058");
            SetSystemBrush(System.Windows.SystemColors.ControlLightLightBrushKey, "#314058");
            SetSystemBrush(System.Windows.SystemColors.ControlDarkDarkBrushKey, "#1B2738");
            SetSystemBrush(System.Windows.SystemColors.WindowBrushKey, "#1F2735");
            SetSystemBrush(System.Windows.SystemColors.ControlTextBrushKey, "#EAF0FA");
            SetSystemBrush(System.Windows.SystemColors.WindowTextBrushKey, "#EAF0FA");
            SetSystemBrush(System.Windows.SystemColors.GrayTextBrushKey, "#8FA5C6");
            SetSystemBrush(System.Windows.SystemColors.InactiveCaptionTextBrushKey, "#B7C7DE");
            SetSystemBrush(System.Windows.SystemColors.InactiveBorderBrushKey, "#314058");
            SetSystemBrush(System.Windows.SystemColors.ActiveBorderBrushKey, "#314058");
            SetGradient("MainBackgroundBrush", "#101723", "#172033", "#0F2A27");
            SetGradient("AccentGradientBrush", "#25BAFF", "#377CFF");
            SetGradient("AccentGradientHoverBrush", "#37C2FF", "#4C8AFF");
        }
        else
        {
            SetBrush("WindowBackgroundBrush", "#F6F9FE");
            SetBrush("PanelBackgroundBrush", "#FFFFFF");
            SetBrush("PrimaryTextBrush", "#1B2635");
            SetBrush("SecondaryTextBrush", "#617189");
            SetBrush("AccentBrush", "#2C78FF");
            SetBrush("AccentTextBrush", "#FFFFFF");
            SetBrush("BorderBrushLight", "#D9E4F2");
            SetBrush("HoverBrush", "#EEF6FF");
            SetBrush("ButtonBackgroundBrush", "#FFFFFF");
            SetBrush("ButtonHoverBackgroundBrush", "#F6FAFF");
            SetBrush("ButtonPressedBackgroundBrush", "#EDF5FF");
            SetBrush("ButtonBorderBrush", "#CBD8EA");
            SetBrush("ButtonHoverBorderBrush", "#87B6FF");
            SetBrush("ListRowBorderBrush", "#E8EEF6");
            SetBrush("ListRowHoverBrush", "#F1F6FF");
            SetBrush("ListRowSelectedBrush", "#EAF3FF");
            SetBrush("SelectedRowBorderBrush", "#C9DAF3");
            SetBrush("ListHeaderBackgroundBrush", "#F6FAFF");
            SetBrush("ListHeaderHoverBrush", "#FFFFFF");
            SetBrush("ContextMenuBackgroundBrush", "#FFFFFF");
            SetBrush("ContextMenuBorderBrush", "#D7E0ED");
            SetBrush("ContextMenuHoverBrush", "#EEF4FF");
            SetBrush("CheckBoxBoxBackgroundBrush", "#FFFFFF");
            SetBrush("CheckBoxBoxBorderBrush", "#C8D5E8");
            SetBrush("CheckBoxBoxHoverBrush", "#EEF4FF");
            SetBrush("TitleBarButtonHoverBrush", "#E8F0FC");
            SetBrush("TitleBarCloseHoverBrush", "#E24444");
            SetBrush("BadgeTextBrush", "#1B2635");
            SetBrush("PriorityP0BadgeBrush", "#FFE1E5");
            SetBrush("PriorityP1BadgeBrush", "#E7F1FF");
            SetBrush("PriorityP2BadgeBrush", "#E8F9EC");
            SetBrush("StatusDefaultBadgeBrush", "#EDF4FF");
            SetBrush("StatusAckBadgeBrush", "#E8F9EC");
            SetBrush("StatusMissedBadgeBrush", "#FFE9D6");
            SetBrush("StatusFiredBadgeBrush", "#FFE1E5");
            SetBrush("ScrollBarTrackBrush", "#F2F6FB");
            SetBrush("ScrollBarThumbBrush", "#B8C6DA");
            SetBrush("ScrollBarThumbHoverBrush", "#AEBFD8");
            SetBrush("ScrollBarThumbPressedBrush", "#9CB1CE");
            SetBrush("ModalBackdropBrush", "#330F172A");
            SetSystemBrush(System.Windows.SystemColors.ControlBrushKey, "#EEF3FA");
            SetSystemBrush(System.Windows.SystemColors.ControlLightBrushKey, "#D7E0ED");
            SetSystemBrush(System.Windows.SystemColors.ControlDarkBrushKey, "#C4D1E3");
            SetSystemBrush(System.Windows.SystemColors.ControlLightLightBrushKey, "#FFFFFF");
            SetSystemBrush(System.Windows.SystemColors.ControlDarkDarkBrushKey, "#AEBFD8");
            SetSystemBrush(System.Windows.SystemColors.WindowBrushKey, "#FFFFFF");
            SetSystemBrush(System.Windows.SystemColors.ControlTextBrushKey, "#1B2635");
            SetSystemBrush(System.Windows.SystemColors.WindowTextBrushKey, "#1B2635");
            SetSystemBrush(System.Windows.SystemColors.GrayTextBrushKey, "#7E8EA8");
            SetSystemBrush(System.Windows.SystemColors.InactiveCaptionTextBrushKey, "#667A98");
            SetSystemBrush(System.Windows.SystemColors.InactiveBorderBrushKey, "#D7E0ED");
            SetSystemBrush(System.Windows.SystemColors.ActiveBorderBrushKey, "#C4D1E3");
            SetGradient("MainBackgroundBrush", "#F2F8FF", "#F8F4FF", "#F3FFFA");
            SetGradient("AccentGradientBrush", "#25BAFF", "#377CFF");
            SetGradient("AccentGradientHoverBrush", "#37C2FF", "#4C8AFF");
        }
    }

    private void SetBrush(string key, string hexColor)
    {
        Resources[key] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hexColor));
    }

    private void SetSystemBrush(object key, string hexColor)
    {
        Resources[key] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hexColor));
    }

    private void SetGradient(string key, string colorStart, string colorMiddle, string colorEnd)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new System.Windows.Point(0, 0),
            EndPoint = new System.Windows.Point(1, 1)
        };
        brush.GradientStops.Add(new GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorStart), 0));
        brush.GradientStops.Add(new GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorMiddle), 0.58));
        brush.GradientStops.Add(new GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorEnd), 1));
        Resources[key] = brush;
    }

    private void SetGradient(string key, string colorStart, string colorEnd)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new System.Windows.Point(0, 0),
            EndPoint = new System.Windows.Point(1, 1)
        };
        brush.GradientStops.Add(new GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorStart), 0));
        brush.GradientStops.Add(new GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorEnd), 1));
        Resources[key] = brush;
    }

    private void MigrateLegacyPlainTokenIfNeeded()
    {
        if (string.IsNullOrWhiteSpace(Settings.TelegramBotTokenPlain))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(Settings.TelegramBotTokenEncrypted))
        {
            Settings.TelegramBotTokenPlain = null;
            SaveSettings();
            return;
        }

        if (SettingsService.SetBotToken(Settings, Settings.TelegramBotTokenPlain))
        {
            SaveSettings();
        }
    }

    private bool IsTelegramEscalationConfigured()
    {
        var token = SettingsService.GetBotToken(Settings);
        var hasTarget =
            !string.IsNullOrWhiteSpace(Settings.TelegramChatId) ||
            !string.IsNullOrWhiteSpace(Settings.TelegramUserId);
        var hasLocalTelegram = !string.IsNullOrWhiteSpace(token) && hasTarget;
        var hasServerTelegram = SyncReminderService?.IsSyncActive() == true &&
                                (!string.IsNullOrWhiteSpace(Settings.SyncTelegramUserId) ||
                                 !string.IsNullOrWhiteSpace(Settings.TelegramUserId));
        return hasLocalTelegram || hasServerTelegram;
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}


