using System.Windows;
using System.Windows.Input;

namespace NNotify.Windows;

public partial class UpdateAvailableWindow : Window
{
    public UpdateAvailableWindow(string currentVersion, string latestVersion)
    {
        InitializeComponent();
        Loaded += (_, _) => ModalDialogAnimator.StartEntrance(this, DialogScale, DialogTranslate);

        CurrentVersionText.Text = currentVersion;
        LatestVersionText.Text = latestVersion;
    }

    public UpdateDialogAction Action { get; private set; } = UpdateDialogAction.RemindLater;

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
        Action = UpdateDialogAction.RemindLater;
        DialogResult = false;
        Close();
    }

    private void OnLaterClick(object sender, RoutedEventArgs e)
    {
        Action = UpdateDialogAction.RemindLater;
        DialogResult = false;
        Close();
    }

    private void OnIgnoreClick(object sender, RoutedEventArgs e)
    {
        Action = UpdateDialogAction.IgnoreVersion;
        DialogResult = true;
        Close();
    }

    private void OnOpenClick(object sender, RoutedEventArgs e)
    {
        Action = UpdateDialogAction.OpenReleasePage;
        DialogResult = true;
        Close();
    }
}

public enum UpdateDialogAction
{
    RemindLater,
    OpenReleasePage,
    IgnoreVersion
}
