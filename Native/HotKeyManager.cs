using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace NNotify.Native;

public sealed class HotKeyManager : IDisposable
{
    private readonly Window _window;
    private readonly int _hotKeyId;
    private readonly uint _modifiers;
    private readonly uint _virtualKey;
    private HwndSource? _source;
    private bool _registered;

    public event Action? HotKeyPressed;

    public HotKeyManager(Window window, int hotKeyId, uint modifiers, uint virtualKey)
    {
        _window = window;
        _hotKeyId = hotKeyId;
        _modifiers = modifiers;
        _virtualKey = virtualKey;
    }

    public bool Register()
    {
        if (_registered)
        {
            return true;
        }

        var windowInteropHelper = new WindowInteropHelper(_window);
        _source = HwndSource.FromHwnd(windowInteropHelper.Handle);
        if (_source is null)
        {
            return false;
        }

        _source.AddHook(HwndHook);
        _registered = RegisterHotKey(windowInteropHelper.Handle, _hotKeyId, _modifiers, _virtualKey);
        return _registered;
    }

    public void Unregister()
    {
        if (!_registered)
        {
            return;
        }

        var helper = new WindowInteropHelper(_window);
        UnregisterHotKey(helper.Handle, _hotKeyId);
        _registered = false;

        if (_source is not null)
        {
            _source.RemoveHook(HwndHook);
            _source = null;
        }
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WmHotKey = 0x0312;

        if (msg == WmHotKey && wParam.ToInt32() == _hotKeyId)
        {
            HotKeyPressed?.Invoke();
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Unregister();
    }

    public static class Modifiers
    {
        public const uint Alt = 0x0001;
        public const uint Control = 0x0002;
        public const uint Shift = 0x0004;
        public const uint Win = 0x0008;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
