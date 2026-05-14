using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NNotify.Localization;

namespace NNotify.Windows;

public partial class ConfirmDialogWindow : Window
{
    public ConfirmDialogWindow(
        string titleText,
        string messageText,
        string? confirmText = null,
        bool showCancel = true,
        bool destructive = true,
        string? cancelText = null)
    {
        InitializeComponent();
        Loaded += (_, _) => ModalDialogAnimator.StartEntrance(this, DialogScale, DialogTranslate);

        QuestionTitleText.Text = titleText;
        QuestionMessageText.Text = messageText;
        var resolvedConfirmText = string.IsNullOrWhiteSpace(confirmText) ? Loc.Text("ConfirmDefaultDelete") : confirmText;
        CancelButton.Content = string.IsNullOrWhiteSpace(cancelText) ? Loc.Text("CommonCancel") : cancelText;
        Title = titleText;

        CancelButton.Visibility = showCancel ? Visibility.Visible : Visibility.Collapsed;
        CancelButton.IsCancel = showCancel;
        CancelButton.Margin = showCancel ? new Thickness(0, 0, 14, 0) : new Thickness(0);
        ConfirmButton.IsDefault = true;

        if (destructive)
        {
            ConfirmButton.Foreground = System.Windows.Media.Brushes.White;
            ConfirmButton.Content = new TextBlock
            {
                Text = resolvedConfirmText,
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
        }
        else
        {
            ConfirmButton.Style = (Style)FindResource("AccentButtonStyle");
            ConfirmButton.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF4, 0xFA, 0xFF));
            ConfirmButton.Content = new TextBlock
            {
                Text = resolvedConfirmText,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF4, 0xFA, 0xFF)),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
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


