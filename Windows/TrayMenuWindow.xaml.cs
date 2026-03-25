using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace NNotify.Windows;

public partial class TrayMenuWindow : Window
{
    private bool _isClosing;

    public event Action? OpenRequested;
    public event Action? AddRequested;
    public event Action? ExitRequested;

    public TrayMenuWindow()
    {
        InitializeComponent();
        Deactivated += OnWindowDeactivated;
    }

    public void ShowNearCursor(int cursorX, int cursorY)
    {
        // Show first to get actual measured size, then position near tray cursor.
        Left = -10000;
        Top = -10000;
        Show();
        UpdateLayout();

        var dpi = VisualTreeHelper.GetDpi(this);
        var x = cursorX / dpi.DpiScaleX;
        var y = cursorY / dpi.DpiScaleY;

        var workArea = SystemParameters.WorkArea;
        var targetLeft = x - ActualWidth + 8;
        var targetTop = y - ActualHeight - 8;

        Left = Math.Min(Math.Max(workArea.Left + 8, targetLeft), workArea.Right - ActualWidth - 8);
        Top = Math.Min(Math.Max(workArea.Top + 8, targetTop), workArea.Bottom - ActualHeight - 8);

        Activate();
        OpenButton.Focus();
    }

    private void OnOpenClick(object sender, RoutedEventArgs e)
    {
        OpenRequested?.Invoke();
        RequestClose();
    }

    private void OnAddClick(object sender, RoutedEventArgs e)
    {
        AddRequested?.Invoke();
        RequestClose();
    }

    private void OnExitClick(object sender, RoutedEventArgs e)
    {
        ExitRequested?.Invoke();
        RequestClose();
    }

    protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            RequestClose();
            return;
        }

        base.OnPreviewKeyDown(e);
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        _isClosing = true;
        base.OnClosing(e);
    }

    private void OnWindowDeactivated(object? sender, EventArgs e)
    {
        if (_isClosing || !IsLoaded)
        {
            return;
        }

        _ = Dispatcher.BeginInvoke(() =>
        {
            if (_isClosing || !IsLoaded || !IsVisible)
            {
                return;
            }

            RequestClose();
        }, DispatcherPriority.Background);
    }

    private void RequestClose()
    {
        if (_isClosing)
        {
            return;
        }

        _isClosing = true;
        try
        {
            Close();
        }
        catch (InvalidOperationException)
        {
            // Window is already in closing pipeline.
        }
    }
}
