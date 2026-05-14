using System.Windows;
using System.Windows.Input;
using NNotify.Localization;
using NNotify.Services;

namespace NNotify.Windows;

public partial class SyncRegisterWindow : Window
{
    private static readonly System.Windows.Media.SolidColorBrush StatusNeutralBrush =
        new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#617189"));
    private static readonly System.Windows.Media.SolidColorBrush StatusErrorBrush =
        new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#D14A4A"));

    public SyncRegisterWindow(string serverUrl, string username)
    {
        InitializeComponent();
        ServerTextBox.Text = serverUrl;
        UsernameTextBox.Text = username;
        StatusText.Text = string.Empty;

        Loaded += (_, _) =>
        {
            ModalDialogAnimator.StartEntrance(this, DialogScale, DialogTranslate);

            if (string.IsNullOrWhiteSpace(UsernameTextBox.Text))
            {
                UsernameTextBox.Focus();
                return;
            }

            PasswordInput.Focus();
        };
    }

    public Uri? ServerBaseUri { get; private set; }
    public string? SuccessMessage { get; private set; }

    public string ServerUrl => ServerTextBox.Text.Trim();

    public string Username => UsernameTextBox.Text.Trim();

    public string Password => PasswordInput.Password.Trim();

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

    private async void OnSubmitClick(object sender, RoutedEventArgs e)
    {
        if (!TryResolveSyncServerUri(ServerUrl, out var serverUri, out var errorKey))
        {
            SetStatus(Loc.Text(errorKey), isError: true);
            return;
        }

        if (string.IsNullOrWhiteSpace(Username) || Username.Length < 3)
        {
            SetStatus(Loc.Text("SettingsSyncStatusInvalidUsername"), isError: true);
            return;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            SetStatus(Loc.Text("SettingsSyncStatusEnterPassword"), isError: true);
            return;
        }

        var app = System.Windows.Application.Current as App;
        if (app is null)
        {
            SetStatus(Loc.Text("SyncRegisterStatusRequestFailed"), isError: true);
            return;
        }

        SetBusyState(true);
        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
            var result = await app.SyncAuthService.RegisterAsync(serverUri, Username, Password, timeoutCts.Token);
            if (!result.Success)
            {
                SetStatus(LocalizeRegisterMessage(result), isError: true);
                return;
            }

            ServerBaseUri = serverUri;
            ServerTextBox.Text = serverUri.GetLeftPart(UriPartial.Authority);
            SuccessMessage = LocalizeRegisterMessage(result);
            DialogResult = true;
            Close();
        }
        finally
        {
            SetBusyState(false);
        }
    }

    private static bool TryResolveSyncServerUri(string raw, out Uri serverBaseUri, out string errorKey)
    {
        serverBaseUri = null!;
        errorKey = "SettingsSyncStatusInvalidServer";

        if (string.IsNullOrWhiteSpace(raw))
        {
            errorKey = "SettingsSyncStatusEnterServer";
            return false;
        }

        var normalized = raw.Contains("://", StringComparison.Ordinal)
            ? raw
            : $"https://{raw}";

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var parsed))
        {
            return false;
        }

        if (!string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            errorKey = "SettingsSyncStatusHttpsRequired";
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
        return true;
    }

    private void SetStatus(string message, bool isError)
    {
        StatusText.Text = message;
        StatusText.Foreground = isError ? StatusErrorBrush : StatusNeutralBrush;
    }

    private void SetBusyState(bool busy)
    {
        SubmitButton.IsEnabled = !busy;
        CancelButton.IsEnabled = !busy;
        ServerTextBox.IsEnabled = !busy;
        UsernameTextBox.IsEnabled = !busy;
        PasswordInput.IsEnabled = !busy;
    }

    private static string LocalizeRegisterMessage(SyncAuthResult result)
    {
        if (string.IsNullOrWhiteSpace(result.Message))
        {
            return Loc.Text("SyncCommonStatusUnknownError");
        }

        if (string.Equals(result.Message, "Check login and password format.", StringComparison.Ordinal))
        {
            return Loc.Text("SyncRegisterStatusInvalidCredentialsFormat");
        }

        if (result.Message.StartsWith("Invalid username.", StringComparison.Ordinal))
        {
            return Loc.Text("SyncRegisterStatusInvalidUsernameFormat");
        }

        if (result.Message.StartsWith("Weak password.", StringComparison.Ordinal))
        {
            return Loc.Text("SyncRegisterStatusWeakPassword");
        }

        return result.Message switch
        {
            "Registration request was submitted. Wait for admin approval." => Loc.Text("SyncRegisterStatusSubmitted"),
            "User with this login already exists." => Loc.Text("SyncRegisterStatusUserExists"),
            "Access denied. Contact your server administrator." => Loc.Text("SyncLoginStatusAccessDenied"),
            "Connection timeout." => Loc.Text("SyncRegisterStatusTimeout"),
            "Registration request failed. Check server URL and TLS certificate." => Loc.Text("SyncRegisterStatusRequestFailed"),
            _ => Loc.Text("SyncCommonStatusUnknownError")
        };
    }
}
