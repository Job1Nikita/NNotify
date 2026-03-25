using System.Windows;
using System.Windows.Input;
using System.Reflection;
using System.IO;
using System.Media;
using NNotify.Localization;
using NNotify.Native;
using NNotify.Services;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace NNotify.Windows;

public partial class SettingsWindow : Window
{
    private const string MaskPlaceholder = "********";
    private const string EasterEggSoundResourceName = "NNotify.Assets.Sounds.WindowsNotifyCalendar.wav";
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

    public SettingsWindow(
        bool hasStoredToken,
        string chatId,
        string userId,
        bool hotKeyEnabled,
        string hotKeyGesture,
        string syncServerUrl,
        string syncUsername,
        bool hasStoredSyncSession)
    {
        InitializeComponent();
        ApplyWindowSizingConstraints();

        _startupRegistrationService = (System.Windows.Application.Current as App)?.StartupRegistrationService
            ?? new StartupRegistrationService();

        _hasStoredToken = hasStoredToken;
        ChatIdTextBox.Text = chatId;
        UserIdTextBox.Text = userId;
        EnableHotKeyCheckBox.IsChecked = hotKeyEnabled;
        HotKeyGestureTextBox.Text = string.IsNullOrWhiteSpace(hotKeyGesture)
            ? HotKeyBinding.DefaultGesture
            : hotKeyGesture.Trim();
        SyncServerUrlTextBox.Text = syncServerUrl;
        SyncUsernameTextBox.Text = syncUsername;
        _hasStoredSyncSession = hasStoredSyncSession;

        if (!string.IsNullOrWhiteSpace(ChatIdTextBox.Text) && !string.IsNullOrWhiteSpace(UserIdTextBox.Text))
        {
            UserIdTextBox.Text = string.Empty;
        }

        UpdateTargetFieldsState();
        UpdateHotKeyControlsState();
        _startupEnabled = _startupRegistrationService.IsEnabled();
        UpdateStartupControlsState();
        UpdateSyncControlsState();
        AppVersionValueText.Text = ResolveAppVersion();

        if (!hasStoredToken)
        {
            return;
        }

        _suppressPasswordChanged = true;
        BotTokenPasswordBox.Password = MaskPlaceholder;
        _suppressPasswordChanged = false;
        _placeholderActive = true;
    }

    private void ApplyWindowSizingConstraints()
    {
        var workAreaHeight = SystemParameters.WorkArea.Height;
        if (workAreaHeight <= 0)
        {
            return;
        }

        var maxVisibleHeight = Math.Max(620, workAreaHeight - 8);
        MaxHeight = maxVisibleHeight;

        if (MinHeight > maxVisibleHeight)
        {
            MinHeight = maxVisibleHeight;
        }
    }

    public string? BotTokenForSave => KeepExistingToken ? null : BotTokenPasswordBox.Password.Trim();
    public string ChatId => ChatIdTextBox.Text.Trim();
    public string UserId => UserIdTextBox.Text.Trim();
    public string SyncServerUrl => SyncServerUrlTextBox.Text.Trim();
    public string SyncUsername => SyncUsernameTextBox.Text.Trim();
    public bool HotKeyEnabled => EnableHotKeyCheckBox.IsChecked == true;
    public string HotKeyGesture => string.IsNullOrWhiteSpace(HotKeyGestureTextBox.Text)
        ? HotKeyBinding.DefaultGesture
        : HotKeyGestureTextBox.Text.Trim();
    public bool KeepExistingToken => _hasStoredToken && !_tokenChanged;

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
    }

    private void OnTargetTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        UpdateTargetFieldsState();
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

    private async void OnSyncRegisterClick(object sender, RoutedEventArgs e)
    {
        if (!TryResolveSyncServerUri(out var serverBaseUri))
        {
            return;
        }

        var username = SyncUsername;
        if (string.IsNullOrWhiteSpace(username) || username.Length < 3)
        {
            SetSyncStatus(Loc.Text("SettingsSyncStatusInvalidUsername"), isError: true);
            return;
        }

        var password = SyncPasswordBox.Password.Trim();
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            SetSyncStatus(Loc.Text("SettingsSyncStatusInvalidPassword"), isError: true);
            return;
        }

        var app = (App)System.Windows.Application.Current;
        SetSyncBusyState(true);
        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
            var result = await app.SyncAuthService.RegisterAsync(serverBaseUri, username, password, timeoutCts.Token);
            SetSyncStatus(result.Message, isError: !result.Success);
            PersistSyncIdentityToSettings(app, serverBaseUri, username);
        }
        finally
        {
            SetSyncBusyState(false);
        }
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
                SetSyncStatus(result.Message, isError: true);
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
        if (!string.IsNullOrWhiteSpace(ChatId) && !string.IsNullOrWhiteSpace(UserId))
        {
            UserIdTextBox.Text = string.Empty;
        }

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
        if (!string.IsNullOrWhiteSpace(ChatId))
        {
            return ChatId;
        }

        if (!string.IsNullOrWhiteSpace(UserId))
        {
            return UserId;
        }

        return null;
    }

    private void UpdateTargetFieldsState()
    {
        var hasChat = !string.IsNullOrWhiteSpace(ChatId);
        var hasUser = !string.IsNullOrWhiteSpace(UserId);

        UserIdTextBox.IsEnabled = !hasChat;
        ChatIdTextBox.IsEnabled = !hasUser;

        UserIdTextBox.ToolTip = hasChat ? Loc.Text("SettingsTooltipClearChatId") : null;
        ChatIdTextBox.ToolTip = hasUser ? Loc.Text("SettingsTooltipClearUserId") : null;
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
            uriBuilder.Port = 5334;
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
}
