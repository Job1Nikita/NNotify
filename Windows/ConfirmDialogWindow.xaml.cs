using System.Windows;
using System.Windows.Input;
using NNotify.Localization;

namespace NNotify.Windows;

public partial class ConfirmDialogWindow : Window
{
    public ConfirmDialogWindow(
        string titleText,
        string messageText,
        string? confirmText = null,
        bool showCancel = true,
        bool destructive = true)
    {
        InitializeComponent();

        WindowTitleText.Text = titleText;
        QuestionTitleText.Text = titleText;
        QuestionMessageText.Text = messageText;
        ConfirmButton.Content = string.IsNullOrWhiteSpace(confirmText) ? Loc.Text("ConfirmDefaultDelete") : confirmText;
        Title = titleText;

        CancelButton.Visibility = showCancel ? Visibility.Visible : Visibility.Collapsed;
        CancelButton.IsCancel = showCancel;
        ConfirmButton.IsDefault = true;

        if (!destructive)
        {
            ConfirmButton.Style = (Style)FindResource("AccentButtonStyle");
        }
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

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
