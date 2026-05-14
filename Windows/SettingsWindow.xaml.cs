using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Reflection;
using System.IO;
using System.Media;
using NNotify.Localization;
using NNotify.Native;
using NNotify.Services;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Interop;
using System.Runtime.InteropServices;

namespace NNotify.Windows;

public partial class SettingsWindow : Window
{
    private const string MaskPlaceholder = "********";
    private const string EasterEggSoundResourceName = "NNotify.Assets.Sounds.WindowsNotifyCalendar.wav";
    private const int WmSysCommand = 0x0112;
    private const int WmNcLeftButtonDown = 0x00A1;
    private const int HtCaption = 2;
    private const int ScSize = 0xF000;
    private const int WmszBottomRight = 8;
    private static readonly SolidColorBrush SyncStatusNeutralBrush = new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#617189"));
    private static readonly SolidColorBrush SyncStatusSuccessBrush = new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2C78FF"));
    private static readonly SolidColorBrush SyncStatusErrorBrush = new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#D14A4A"));
    private static readonly object EasterEggSoundSync = new();
    private static SoundPlayer? _easterEggSoundPlayer;
    private static MemoryStream? _easterEggSoundStream;

    private readonly bool _hasStoredToken;
    private readonly StartupRegistrationService _startupRegistrationService;
    private bool _hasStoredSyncSession;
    private bool _startupEnabled;
    private bool _syncBusy;
    private bool _tokenChanged;
    private bool _placeholderActive;
    private bool _suppressPasswordChanged;
    private DateTime _lastAboutSignatureTapAt = DateTime.MinValue;
    private int _aboutSignatureTapCount;
    private bool _botTokenVisible;
    private bool _syncingRevealText;
    private bool _allowCloseWithoutSettingsPrompt;
    private SettingsSnapshot? _initialSettingsSnapshot;

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    public SettingsWindow(
        bool hasStoredToken,
        string chatId,
        string userId,
        bool hotKeyEnabled,
        string hotKeyGesture,
        string syncServerUrl,
        string syncUsername,
        string syncTelegramUserId,
        bool hasStoredSyncSession,
        bool checkUpdatesAutomatically)
    {
        InitializeComponent();
        ApplyWindowSizingConstraints();
        Loaded += OnLoaded;
        Closing += OnWindowClosing;
        PreviewKeyDown += OnWindowPreviewKeyDown;
        SizeChanged += (_, _) => UpdateRoundedShellClip();
        SettingsShell.SizeChanged += (_, _) => UpdateRoundedShellClip();

        _startupRegistrationService = (System.Windows.Application.Current as App)?.StartupRegistrationService
            ?? new StartupRegistrationService();

        _hasStoredToken = hasStoredToken;
        TelegramTargetTextBox.Text = !string.IsNullOrWhiteSpace(chatId)
            ? chatId.Trim()
            : userId.Trim();
        EnableHotKeyCheckBox.IsChecked = hotKeyEnabled;
        HotKeyGestureTextBox.Text = string.IsNullOrWhiteSpace(hotKeyGesture)
            ? HotKeyBinding.DefaultGesture
            : hotKeyGesture.Trim();
        SyncServerUrlTextBox.Text = syncServerUrl;
        SyncUsernameTextBox.Text = syncUsername;
        SyncTelegramUserIdTextBox.Text = syncTelegramUserId;
        _hasStoredSyncSession = hasStoredSyncSession;
        EnableUpdateCheckBox.IsChecked = checkUpdatesAutomatically;

        UpdateTargetFieldsState();
        UpdateHotKeyControlsState();
        _startupEnabled = _startupRegistrationService.IsEnabled();
        UpdateStartupControlsState();
        UpdateSyncControlsState();
        UpdateVisualStatusSummaries();
        AppVersionValueText.Text = ResolveAppVersion();

        if (hasStoredToken)
        {
            _suppressPasswordChanged = true;
            BotTokenPasswordBox.Password = MaskPlaceholder;
            BotTokenRevealTextBox.Text = MaskPlaceholder;
            _suppressPasswordChanged = false;
            _placeholderActive = true;
        }

        _initialSettingsSnapshot = CaptureSettingsSnapshot();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateRoundedShellClip();
        StartEntranceAnimation();
    }

    private void StartEntranceAnimation()
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = TimeSpan.FromMilliseconds(220);

        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration)
        {
            EasingFunction = ease
        });

        SettingsShellScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.985, 1, duration)
        {
            EasingFunction = ease
        });
        SettingsShellScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.985, 1, duration)
        {
            EasingFunction = ease
        });
        SettingsShellTranslate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(10, 0, duration)
        {
            EasingFunction = ease
        });
    }

    private void ApplyWindowSizingConstraints()
    {
        var workArea = SystemParameters.WorkArea;
        if (workArea.Width <= 0 || workArea.Height <= 0)
        {
            return;
        }

        MaxWidth = Math.Max(MinWidth, workArea.Width - 32);
        MaxHeight = Math.Max(MinHeight, workArea.Height - 32);

        if (Width > MaxWidth)
        {
            Width = MaxWidth;
        }

        if (Height > MaxHeight)
        {
            Height = MaxHeight;
        }
    }

    public string? BotTokenForSave => KeepExistingToken ? null : BotTokenPasswordBox.Password.Trim();
    public string ChatId => string.Empty;
    public string UserId => TelegramTargetTextBox.Text.Trim();
    public string SyncServerUrl => SyncServerUrlTextBox.Text.Trim();
    public string SyncUsername => SyncUsernameTextBox.Text.Trim();
    public string SyncTelegramUserId => SyncTelegramUserIdTextBox.Text.Trim();
    public bool HotKeyEnabled => EnableHotKeyCheckBox.IsChecked == true;
    public string HotKeyGesture => string.IsNullOrWhiteSpace(HotKeyGestureTextBox.Text)
        ? HotKeyBinding.DefaultGesture
        : HotKeyGestureTextBox.Text.Trim();
    public bool CheckUpdatesAutomatically => EnableUpdateCheckBox.IsChecked == true;
    public bool KeepExistingToken => _hasStoredToken && !_tokenChanged;

    private void UpdateRoundedShellClip()
    {
        if (SettingsShell.ActualWidth <= 0 || SettingsShell.ActualHeight <= 0)
        {
            return;
        }

        const double radius = 22.0;
        var rect = new Rect(0, 0, SettingsShell.ActualWidth, SettingsShell.ActualHeight);
        SettingsShell.Clip = new RectangleGeometry(rect, radius, radius);

        if (SettingsShellContent.ActualWidth > 0 && SettingsShellContent.ActualHeight > 0)
        {
            SettingsShellContent.Clip = new RectangleGeometry(
                new Rect(0, 0, SettingsShellContent.ActualWidth, SettingsShellContent.ActualHeight),
                radius,
                radius);
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // Do not use SetWindowRgn/CreateRoundRectRgn here. GDI regions are binary masks and
        // create jagged, pixelated rounded corners on dark backgrounds. The transparent WPF
        // shell below draws the rounded corners with anti-aliasing instead.
    }

    private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || e.ButtonState != MouseButtonState.Pressed)
        {
            return;
        }

        if (IsInteractiveElement(e.OriginalSource as DependencyObject))
        {
            return;
        }

        // For a transparent, borderless WPF window DragMove() is more reliable than
        // emulating the native caption message. Keep the native path only as fallback.
        try
        {
            DragMove();
            e.Handled = true;
            return;
        }
        catch
        {
            // Fall back to native caption dragging below.
        }

        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        ReleaseCapture();
        SendMessage(handle, WmNcLeftButtonDown, (IntPtr)HtCaption, IntPtr.Zero);
        e.Handled = true;
    }

    private void OnResizeGripMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed)
        {
            return;
        }

        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        ReleaseCapture();
        SendMessage(handle, WmSysCommand, (IntPtr)(ScSize + WmszBottomRight), IntPtr.Zero);
        e.Handled = true;
    }

    private static bool IsInteractiveElement(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is System.Windows.Controls.Button
                or System.Windows.Controls.TextBox
                or System.Windows.Controls.PasswordBox
                or System.Windows.Controls.CheckBox
                or System.Windows.Controls.RadioButton
                or System.Windows.Controls.ComboBox
                or System.Windows.Controls.Primitives.ScrollBar
                or System.Windows.Controls.Primitives.ResizeGrip)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private void OnCloseButtonClick(object sender, RoutedEventArgs e)
    {
        RequestCloseWithoutSave();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        RequestCloseWithoutSave();
    }

    private void OnWindowPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        e.Handled = true;
        RequestCloseWithoutSave();
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_allowCloseWithoutSettingsPrompt || !HasUnsavedSettingsChanges())
        {
            return;
        }

        e.Cancel = true;
        if (!CanDiscardSettingsChanges())
        {
            return;
        }

        _allowCloseWithoutSettingsPrompt = true;
        DialogResult = false;
        Close();
    }

    private void OnNavigationChecked(object sender, RoutedEventArgs e)
    {
        if (TelegramPage is null || SyncPage is null || HotKeysPage is null || StartupPage is null || AboutPage is null)
        {
            return;
        }

        TelegramPage.Visibility = ReferenceEquals(sender, TelegramNavButton) ? Visibility.Visible : Visibility.Collapsed;
        SyncPage.Visibility = ReferenceEquals(sender, SyncNavButton) ? Visibility.Visible : Visibility.Collapsed;
        HotKeysPage.Visibility = ReferenceEquals(sender, HotKeysNavButton) ? Visibility.Visible : Visibility.Collapsed;
        StartupPage.Visibility = ReferenceEquals(sender, StartupNavButton) ? Visibility.Visible : Visibility.Collapsed;
        AboutPage.Visibility = ReferenceEquals(sender, AboutNavButton) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnOpenSyncSettingsClick(object sender, RoutedEventArgs e)
    {
        SyncNavButton.IsChecked = true;
    }

    private void OnAboutSignatureMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var now = DateTime.UtcNow;
        if (now - _lastAboutSignatureTapAt > TimeSpan.FromMilliseconds(900))
        {
            _aboutSignatureTapCount = 0;
        }

        _lastAboutSignatureTapAt = now;
        _aboutSignatureTapCount++;
        if (_aboutSignatureTapCount < 3)
        {
            return;
        }

        _aboutSignatureTapCount = 0;
        RunAboutSignatureEasterEgg();
        e.Handled = true;
    }

    private void OnBotTokenFocus(object sender, RoutedEventArgs e)
    {
        if (!_placeholderActive)
        {
            return;
        }

        BotTokenPasswordBox.SelectAll();
    }

    private void OnBotTokenPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressPasswordChanged)
        {
            return;
        }

        if (_placeholderActive && BotTokenPasswordBox.Password == MaskPlaceholder)
        {
            return;
        }

        _placeholderActive = false;
        _tokenChanged = true;
        if (!_syncingRevealText)
        {
            _syncingRevealText = true;
            BotTokenRevealTextBox.Text = BotTokenPasswordBox.Password;
            _syncingRevealText = false;
        }
        UpdateVisualStatusSummaries();
    }

    private void OnBotTokenRevealTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_syncingRevealText || _suppressPasswordChanged)
        {
            return;
        }

        if (_placeholderActive && BotTokenRevealTextBox.Text == MaskPlaceholder)
        {
            return;
        }

        _placeholderActive = false;
        _tokenChanged = true;
        _syncingRevealText = true;
        BotTokenPasswordBox.Password = BotTokenRevealTextBox.Text;
        _syncingRevealText = false;
        UpdateVisualStatusSummaries();
    }

    private void OnBotTokenVisibilityClick(object sender, RoutedEventArgs e)
    {
        _botTokenVisible = !_botTokenVisible;
        if (_botTokenVisible)
        {
            _syncingRevealText = true;
            BotTokenRevealTextBox.Text = BotTokenPasswordBox.Password;
            _syncingRevealText = false;
            BotTokenPasswordBox.Visibility = Visibility.Collapsed;
            BotTokenRevealTextBox.Visibility = Visibility.Visible;
            BotTokenRevealTextBox.Focus();
            BotTokenRevealTextBox.SelectAll();
            BotTokenVisibilityButton.Content = "";
            return;
        }

        BotTokenPasswordBox.Visibility = Visibility.Visible;
        BotTokenRevealTextBox.Visibility = Visibility.Collapsed;
        BotTokenPasswordBox.Focus();
        BotTokenVisibilityButton.Content = "";
    }

    private void OnTargetTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        UpdateTargetFieldsState();
        UpdateVisualStatusSummaries();
    }

    private void OnHotKeyEnabledClick(object sender, RoutedEventArgs e)
    {
        UpdateHotKeyControlsState();
    }

    private void OnHotKeyGesturePreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Tab)
        {
            return;
        }

        if (e.Key is Key.Back or Key.Delete)
        {
            HotKeyGestureTextBox.Text = string.Empty;
            e.Handled = true;
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (HotKeyBinding.TryCapture(key, Keyboard.Modifiers, out var gesture))
        {
            HotKeyGestureTextBox.Text = gesture;
        }

        e.Handled = true;
    }

    private async void OnTestClick(object sender, RoutedEventArgs e)
    {
        var app = (App)System.Windows.Application.Current;
        var token = ResolveTokenForTest(app);
        if (string.IsNullOrWhiteSpace(token))
        {
            ShowInfoDialog(Loc.Text("CommonCheckDataTitle"), Loc.Text("SettingsDialogCheckDataEnterToken"));
            return;
        }

        var targetId = GetTargetId();
        if (string.IsNullOrWhiteSpace(targetId))
        {
            ShowInfoDialog(Loc.Text("CommonCheckDataTitle"), Loc.Text("SettingsDialogCheckDataEnterTarget"));
            return;
        }

        SetTestingState(true);
        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var sent = await app.TelegramService.SendEscalationAsync(token, targetId, BuildTestMessage(), timeoutCts.Token);
            if (sent)
            {
                ShowInfoDialog(Loc.Text("SettingsDialogTestSentTitle"), Loc.Text("SettingsDialogTestSentBody"));
                return;
            }

            ShowInfoDialog(
                Loc.Text("SettingsDialogTestFailedTitle"),
                Loc.Text("SettingsDialogTestFailedBody"));
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("Telegram test send failed", ex);
            ShowInfoDialog(Loc.Text("SettingsDialogSendErrorTitle"), Loc.Text("SettingsDialogSendErrorBody"));
        }
        finally
        {
            SetTestingState(false);
        }
    }

    private void OnSyncRegisterClick(object sender, RoutedEventArgs e)
    {
        var registerDialog = new SyncRegisterWindow(SyncServerUrl, SyncUsername)
        {
            Owner = this
        };

        if (registerDialog.ShowDialog() != true || registerDialog.ServerBaseUri is null)
        {
            return;
        }

        var app = (App)System.Windows.Application.Current;
        var serverBaseUri = registerDialog.ServerBaseUri;
        var username = registerDialog.Username;

        SyncServerUrlTextBox.Text = serverBaseUri.GetLeftPart(UriPartial.Authority);
        SyncUsernameTextBox.Text = username;
        SetSyncStatus(registerDialog.SuccessMessage ?? Loc.Text("SyncRegisterStatusSubmitted"), isError: false, useAccent: true);
        PersistSyncIdentityToSettings(app, serverBaseUri, username);
        _initialSettingsSnapshot = CaptureSettingsSnapshot();
    }

    private async void OnSyncLoginClick(object sender, RoutedEventArgs e)
    {
        if (!TryResolveSyncServerUri(out var serverBaseUri))
        {
            return;
        }

        var username = SyncUsername;
        if (string.IsNullOrWhiteSpace(username))
        {
            SetSyncStatus(Loc.Text("SettingsSyncStatusEnterUsername"), isError: true);
            return;
        }

        var password = SyncPasswordBox.Password.Trim();
        if (string.IsNullOrWhiteSpace(password))
        {
            SetSyncStatus(Loc.Text("SettingsSyncStatusEnterPassword"), isError: true);
            return;
        }

        var app = (App)System.Windows.Application.Current;
        var deviceId = EnsureSyncDeviceId(app.Settings);
        var deviceName = Environment.MachineName;

        SetSyncBusyState(true);
        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
            var result = await app.SyncAuthService.LoginAsync(
                serverBaseUri,
                username,
                password,
                deviceId,
                deviceName,
                timeoutCts.Token);

            if (!result.Success || string.IsNullOrWhiteSpace(result.AccessToken) || string.IsNullOrWhiteSpace(result.RefreshToken))
            {
                SetSyncStatus(LocalizeSyncAuthMessage(result.Message), isError: true);
                return;
            }

            var encrypted = app.SettingsService.SetSyncSession(
                app.Settings,
                result.AccessToken,
                result.RefreshToken,
                result.AccessTokenExpiresAtUtc);

            if (!encrypted)
            {
                SetSyncStatus(Loc.Text("SettingsSyncStatusTokenSaveFailed"), isError: true);
                return;
            }

            PersistSyncIdentityToSettings(app, serverBaseUri, username);
            _initialSettingsSnapshot = CaptureSettingsSnapshot();
            await app.SyncReminderService.EnsureInitialLocalPublishAsync();
            app.SyncReminderService.SignalSync();
            app.SaveSettings();
            _hasStoredSyncSession = true;
            SyncPasswordBox.Password = string.Empty;
            SetSyncStatus(Loc.Text("SettingsSyncStatusLoggedIn"), isError: false);
            UpdateSyncControlsState();
        }
        finally
        {
            SetSyncBusyState(false);
        }
    }

    private async void OnSyncLogoutClick(object sender, RoutedEventArgs e)
    {
        var app = (App)System.Windows.Application.Current;
        var refreshToken = app.SettingsService.GetSyncRefreshToken(app.Settings);
        var deviceId = EnsureSyncDeviceId(app.Settings);

        SetSyncBusyState(true);
        try
        {
            if (TryResolveSyncServerUri(out var serverBaseUri) && !string.IsNullOrWhiteSpace(refreshToken))
            {
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
                await app.SyncAuthService.LogoutAsync(serverBaseUri, refreshToken, deviceId, timeoutCts.Token);
            }

            app.SettingsService.ClearSyncSession(app.Settings);
            app.Settings.SyncInitialLocalPublishCompleted = false;
            app.Settings.SyncLastPulledChangeUtc = 0;
            app.SaveSettings();
            _hasStoredSyncSession = false;
            SetSyncStatus(Loc.Text("SettingsSyncStatusLoggedOut"), isError: false);
            UpdateSyncControlsState();
        }
        finally
        {
            SetSyncBusyState(false);
        }
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (HotKeyEnabled)
        {
            if (!HotKeyBinding.TryParse(HotKeyGestureTextBox.Text, out _, out _, out var normalized))
            {
                ShowInfoDialog(Loc.Text("CommonCheckDataTitle"), Loc.Text("SettingsDialogInvalidHotKeyBody"));
                return;
            }

            HotKeyGestureTextBox.Text = normalized;
        }
        else if (string.IsNullOrWhiteSpace(HotKeyGestureTextBox.Text))
        {
            HotKeyGestureTextBox.Text = HotKeyBinding.DefaultGesture;
        }

        if (!string.IsNullOrWhiteSpace(SyncServerUrl) || !string.IsNullOrWhiteSpace(SyncUsername))
        {
            if (!TryResolveSyncServerUri(out _))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(SyncUsername))
            {
                SetSyncStatus(Loc.Text("SettingsSyncStatusEnterUsername"), isError: true);
                return;
            }
        }

        _allowCloseWithoutSettingsPrompt = true;
        _initialSettingsSnapshot = CaptureSettingsSnapshot();
        DialogResult = true;
        Close();
    }

    private void OnStartupToggleClick(object sender, RoutedEventArgs e)
    {
        StartupToggleButton.IsEnabled = false;

        try
        {
            var success = _startupEnabled
                ? _startupRegistrationService.TryDisable(out var errorMessage)
                : _startupRegistrationService.TryEnable(out errorMessage);

            if (!success)
            {
                ShowInfoDialog(Loc.Text("SettingsDialogOperationNotDoneTitle"), errorMessage);
                return;
            }

            _startupEnabled = _startupRegistrationService.IsEnabled();
            UpdateStartupControlsState();

            ShowInfoDialog(
                Loc.Text("SettingsDialogStartupUpdatedTitle"),
                _startupEnabled
                    ? Loc.Text("SettingsDialogStartupEnabledBody")
                    : Loc.Text("SettingsDialogStartupDisabledBody"));
        }
        finally
        {
            StartupToggleButton.IsEnabled = true;
        }
    }

    private async void OnCheckUpdatesClick(object sender, RoutedEventArgs e)
    {
        var app = (App)System.Windows.Application.Current;
        CheckUpdatesButton.IsEnabled = false;
        try
        {
            await app.CheckForUpdatesInteractiveAsync(showUpToDateMessage: true);
        }
        finally
        {
            CheckUpdatesButton.IsEnabled = true;
        }
    }

    private void RequestCloseWithoutSave()
    {
        if (!CanDiscardSettingsChanges())
        {
            return;
        }

        _allowCloseWithoutSettingsPrompt = true;
        DialogResult = false;
        Close();
    }

    private bool CanDiscardSettingsChanges()
    {
        if (_allowCloseWithoutSettingsPrompt || !HasUnsavedSettingsChanges())
        {
            return true;
        }

        var dialog = new ConfirmDialogWindow(
            "Закрыть без сохранения?",
            "В настройках есть несохранённые изменения. Закрыть окно и потерять их?",
            "Закрыть без сохранения",
            destructive: true,
            cancelText: "Продолжить настройку")
        {
            Owner = this
        };

        return dialog.ShowDialog() == true;
    }

    private bool HasUnsavedSettingsChanges()
    {
        return _initialSettingsSnapshot is not null
            && !_initialSettingsSnapshot.Equals(CaptureSettingsSnapshot());
    }

    private SettingsSnapshot CaptureSettingsSnapshot()
    {
        return new SettingsSnapshot(
            Token: ResolveTokenForSnapshot(),
            TelegramTarget: TelegramTargetTextBox.Text.Trim(),
            HotKeyEnabled: HotKeyEnabled,
            HotKeyGesture: HotKeyGesture,
            SyncServerUrl: SyncServerUrl,
            SyncUsername: SyncUsername,
            SyncTelegramUserId: SyncTelegramUserId,
            CheckUpdatesAutomatically: CheckUpdatesAutomatically);
    }

    private string? ResolveTokenForSnapshot()
    {
        if (_hasStoredToken && !_tokenChanged)
        {
            return null;
        }

        return BotTokenPasswordBox.Password.Trim();
    }

    private string? ResolveTokenForTest(App app)
    {
        if (_tokenChanged)
        {
            var enteredToken = BotTokenPasswordBox.Password.Trim();
            return string.IsNullOrWhiteSpace(enteredToken) ? null : enteredToken;
        }

        if (_hasStoredToken)
        {
            return app.SettingsService.GetBotToken(app.Settings);
        }

        var freshToken = BotTokenPasswordBox.Password.Trim();
        return string.IsNullOrWhiteSpace(freshToken) ? null : freshToken;
    }

    private string? GetTargetId()
    {
        var target = TelegramTargetTextBox.Text.Trim();
        return string.IsNullOrWhiteSpace(target) ? null : target;
    }

    private void UpdateTargetFieldsState()
    {
        TelegramTargetTextBox.ToolTip = Loc.Text("SettingsHintTelegramTarget");
    }

    private void UpdateHotKeyControlsState()
    {
        HotKeyGestureTextBox.IsEnabled = HotKeyEnabled;
    }

    private void UpdateStartupControlsState()
    {
        StartupToggleButton.Content = _startupEnabled
            ? Loc.Text("SettingsStartupDisable")
            : Loc.Text("SettingsStartupEnable");
        StartupStatusText.Text = _startupEnabled
            ? Loc.Text("SettingsStartupStatusOn")
            : Loc.Text("SettingsStartupStatusOff");
        UpdateVisualStatusSummaries();
    }

    private void UpdateSyncControlsState()
    {
        if (_hasStoredSyncSession)
        {
            SetSyncStatus(Loc.Text("SettingsSyncStatusSessionActive"), isError: false, useAccent: true);
        }
        else
        {
            SetSyncStatus(Loc.Text("SettingsSyncStatusSessionMissing"), isError: false, useAccent: false);
        }

        SyncLogoutButton.IsEnabled = !_syncBusy && _hasStoredSyncSession;
        SyncLoginButton.IsEnabled = !_syncBusy;
        SyncRegisterButton.IsEnabled = !_syncBusy;
        UpdateVisualStatusSummaries();
    }

    private void UpdateVisualStatusSummaries()
    {
        if (TelegramStatusBadge is null)
        {
            return;
        }

        var telegramConfigured = IsTelegramConfiguredForDisplay();
        SetBadge(
            TelegramStatusBadge,
            TelegramStatusText,
            telegramConfigured ? Loc.Text("SettingsTelegramStatusConfigured") : Loc.Text("SettingsTelegramStatusMissing"),
            telegramConfigured);

        SetBadge(
            SyncSummaryStatusBadge,
            SyncSummaryStatusText,
            _hasStoredSyncSession ? Loc.Text("SettingsSyncSummaryAvailable") : Loc.Text("SettingsSyncStatusSessionMissing"),
            _hasStoredSyncSession);

        SetBadge(
            SyncPageStatusBadge,
            SyncPageStatusBadgeText,
            _hasStoredSyncSession ? Loc.Text("SettingsSyncStatusSessionActive") : Loc.Text("SettingsSyncStatusSessionMissing"),
            _hasStoredSyncSession);

        SetBadge(
            StartupStatusBadge,
            StartupStatusText,
            _startupEnabled ? Loc.Text("SettingsStartupStatusOn") : Loc.Text("SettingsStartupStatusOff"),
            _startupEnabled);

        SyncSummaryServerText.Text = MaskDisplayValue(SyncServerUrlTextBox?.Text);
        SyncSummaryLoginText.Text = MaskDisplayValue(SyncUsernameTextBox?.Text);
        SyncSummaryAlertIdText.Text = MaskDisplayValue(SyncTelegramUserIdTextBox?.Text);
    }

    private bool IsTelegramConfiguredForDisplay()
    {
        var hasToken = _hasStoredToken || (!string.IsNullOrWhiteSpace(BotTokenPasswordBox.Password) && !_placeholderActive);
        var hasTarget = !string.IsNullOrWhiteSpace(TelegramTargetTextBox.Text);
        return hasToken && hasTarget;
    }

    private void SetBadge(Border badge, TextBlock textBlock, string text, bool success)
    {
        badge.Background = (System.Windows.Media.Brush)FindResource(success ? "StatusAckBadgeBrush" : "StatusDefaultBadgeBrush");
        badge.BorderBrush = (System.Windows.Media.Brush)FindResource(success ? "StatusAckBadgeBrush" : "BorderBrushLight");
        textBlock.Text = text;
    }

    private static string MaskDisplayValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Loc.Text("SettingsValueNotSet");
        }

        var trimmed = value.Trim();
        if (trimmed.Length <= 6)
        {
            return trimmed;
        }

        return $"{trimmed[..Math.Min(8, trimmed.Length)]}••••";
    }

    private void SetSyncBusyState(bool busy)
    {
        _syncBusy = busy;
        SyncRegisterButton.IsEnabled = !busy;
        SyncLoginButton.IsEnabled = !busy;
        SyncLogoutButton.IsEnabled = !busy && _hasStoredSyncSession;
        SaveButton.IsEnabled = !busy;
        CancelButton.IsEnabled = !busy;
    }

    private void SetSyncStatus(string message, bool isError, bool useAccent = false)
    {
        SyncStatusText.Text = message;
        SyncStatusText.Foreground = isError
            ? SyncStatusErrorBrush
            : useAccent
                ? SyncStatusSuccessBrush
                : SyncStatusNeutralBrush;
    }

    private static string EnsureSyncDeviceId(Models.AppSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.SyncDeviceId))
        {
            return settings.SyncDeviceId;
        }

        settings.SyncDeviceId = Guid.NewGuid().ToString("N");
        return settings.SyncDeviceId;
    }

    private void PersistSyncIdentityToSettings(App app, Uri serverBaseUri, string username)
    {
        app.Settings.SyncServerUrl = serverBaseUri.GetLeftPart(UriPartial.Authority);
        app.Settings.SyncUsername = username;
        EnsureSyncDeviceId(app.Settings);
        app.SaveSettings();
    }

    private bool TryResolveSyncServerUri(out Uri serverBaseUri)
    {
        serverBaseUri = null!;

        var raw = SyncServerUrl;
        if (string.IsNullOrWhiteSpace(raw))
        {
            SetSyncStatus(Loc.Text("SettingsSyncStatusEnterServer"), isError: true);
            return false;
        }

        var normalized = raw.Contains("://", StringComparison.Ordinal)
            ? raw
            : $"https://{raw}";

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var parsed))
        {
            SetSyncStatus(Loc.Text("SettingsSyncStatusInvalidServer"), isError: true);
            return false;
        }

        if (!string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            SetSyncStatus(Loc.Text("SettingsSyncStatusHttpsRequired"), isError: true);
            return false;
        }

        var uriBuilder = new UriBuilder(parsed)
        {
            Path = "/",
            Query = string.Empty,
            Fragment = string.Empty
        };

        if (uriBuilder.Port <= 0)
        {
            uriBuilder.Port = 443;
        }

        serverBaseUri = uriBuilder.Uri;
        SyncServerUrlTextBox.Text = serverBaseUri.GetLeftPart(UriPartial.Authority);
        return true;
    }

    private static string BuildTestMessage()
    {
        return $"{Loc.Text("SettingsTestHeader")}\n{Loc.Format("SettingsTestAtTemplate", DateTime.Now)}";
    }

    private static string ResolveAppVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(SettingsWindow).Assembly;
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            var normalized = informational.Split('+', 2)[0].Trim();
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                return normalized;
            }
        }

        var version = assembly.GetName().Version;
        if (version is null)
        {
            return "1.0.0";
        }

        return version.Build >= 0
            ? $"{version.Major}.{version.Minor}.{version.Build}"
            : $"{version.Major}.{version.Minor}";
    }

    private void RunAboutSignatureEasterEgg()
    {
        var pulseX = new DoubleAnimationUsingKeyFrames();
        pulseX.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        pulseX.KeyFrames.Add(new EasingDoubleKeyFrame(1.05, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(130))));
        pulseX.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(340))));

        var pulseY = new DoubleAnimationUsingKeyFrames();
        pulseY.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        pulseY.KeyFrames.Add(new EasingDoubleKeyFrame(1.05, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(130))));
        pulseY.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(340))));

        var opacityPulse = new DoubleAnimationUsingKeyFrames();
        opacityPulse.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        opacityPulse.KeyFrames.Add(new EasingDoubleKeyFrame(0.72, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(130))));
        opacityPulse.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(340))));

        if (AboutSignatureText.RenderTransform is not ScaleTransform scaleTransform)
        {
            scaleTransform = new ScaleTransform(1, 1);
            AboutSignatureText.RenderTransform = scaleTransform;
        }
        AboutSignatureText.RenderTransformOrigin = new System.Windows.Point(0, 0.5);

        scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, pulseX);
        scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, pulseY);
        AboutSignatureText.BeginAnimation(UIElement.OpacityProperty, opacityPulse);
        PlayEasterEggSound();
    }

    private static void PlayEasterEggSound()
    {
        try
        {
            var player = EnsureEasterEggSoundPlayer();
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
            // Ignore easter egg sound playback failures.
        }
    }

    private static SoundPlayer? EnsureEasterEggSoundPlayer()
    {
        if (_easterEggSoundPlayer is not null)
        {
            return _easterEggSoundPlayer;
        }

        lock (EasterEggSoundSync)
        {
            if (_easterEggSoundPlayer is not null)
            {
                return _easterEggSoundPlayer;
            }

            try
            {
                using var resourceStream = typeof(App).Assembly.GetManifestResourceStream(EasterEggSoundResourceName);
                if (resourceStream is null)
                {
                    return null;
                }

                _easterEggSoundStream = new MemoryStream();
                resourceStream.CopyTo(_easterEggSoundStream);
                _easterEggSoundStream.Position = 0;

                var player = new SoundPlayer(_easterEggSoundStream);
                player.Load();
                _easterEggSoundStream.Position = 0;
                _easterEggSoundPlayer = player;
                return _easterEggSoundPlayer;
            }
            catch
            {
                _easterEggSoundStream?.Dispose();
                _easterEggSoundStream = null;
                _easterEggSoundPlayer = null;
                return null;
            }
        }
    }

    private void SetTestingState(bool testing)
    {
        TestButton.IsEnabled = !testing;
        SaveButton.IsEnabled = !testing;
        CancelButton.IsEnabled = !testing;
        StartupToggleButton.IsEnabled = !testing;
        SyncRegisterButton.IsEnabled = !testing && !_syncBusy;
        SyncLoginButton.IsEnabled = !testing && !_syncBusy;
        SyncLogoutButton.IsEnabled = !testing && !_syncBusy && _hasStoredSyncSession;
        CheckUpdatesButton.IsEnabled = !testing;
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
            Owner = this
        };

        dialog.ShowDialog();
    }

    private static string LocalizeSyncAuthMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return Loc.Text("SyncCommonStatusUnknownError");
        }

        if (string.Equals(message, "Invalid username or password.", StringComparison.Ordinal))
        {
            return Loc.Text("SyncLoginStatusInvalidCredentials");
        }

        if (string.Equals(message, "Access denied. Account is pending admin approval.", StringComparison.Ordinal))
        {
            return Loc.Text("SyncLoginStatusPendingApproval");
        }

        if (string.Equals(message, "Access denied. Contact your server administrator.", StringComparison.Ordinal))
        {
            return Loc.Text("SyncLoginStatusAccessDenied");
        }

        if (string.Equals(message, "Too many failed attempts. Try again later.", StringComparison.Ordinal))
        {
            return Loc.Text("SyncLoginStatusRateLimited");
        }

        if (string.Equals(message, "Connection timeout.", StringComparison.Ordinal))
        {
            return Loc.Text("SyncLoginStatusTimeout");
        }

        if (string.Equals(message, "Login request failed. Check server URL and TLS certificate.", StringComparison.Ordinal))
        {
            return Loc.Text("SyncLoginStatusRequestFailed");
        }

        return Loc.Text("SyncCommonStatusUnknownError");
    }

    private sealed record SettingsSnapshot(
        string? Token,
        string TelegramTarget,
        bool HotKeyEnabled,
        string HotKeyGesture,
        string SyncServerUrl,
        string SyncUsername,
        string SyncTelegramUserId,
        bool CheckUpdatesAutomatically);

}