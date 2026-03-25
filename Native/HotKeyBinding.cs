using System.Globalization;
using System.Windows.Input;

namespace NNotify.Native;

public static class HotKeyBinding
{
    public const string DefaultGesture = "Ctrl+Alt+R";

    public static bool TryParse(string? gesture, out uint modifiers, out uint virtualKey, out string normalized)
    {
        modifiers = 0;
        virtualKey = 0;
        normalized = DefaultGesture;

        if (string.IsNullOrWhiteSpace(gesture))
        {
            return false;
        }

        var parts = gesture
            .Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        Key key = Key.None;
        uint parsedModifiers = 0;

        foreach (var part in parts)
        {
            var token = part.Trim();
            if (token.Length == 0)
            {
                continue;
            }

            if (TryParseModifier(token, out var modifier))
            {
                parsedModifiers |= modifier;
                continue;
            }

            if (key != Key.None)
            {
                return false;
            }

            if (!TryParseKey(token, out key))
            {
                return false;
            }
        }

        if (parsedModifiers == 0 || key == Key.None || IsModifierKey(key))
        {
            return false;
        }

        var vk = KeyInterop.VirtualKeyFromKey(key);
        if (vk == 0)
        {
            return false;
        }

        modifiers = parsedModifiers;
        virtualKey = (uint)vk;
        normalized = Format(parsedModifiers, key);
        return true;
    }

    public static bool TryCapture(Key key, ModifierKeys modifiers, out string gesture)
    {
        gesture = DefaultGesture;

        if (IsModifierKey(key))
        {
            return false;
        }

        var nativeModifiers = ToNativeModifiers(modifiers);
        if (nativeModifiers == 0)
        {
            return false;
        }

        var vk = KeyInterop.VirtualKeyFromKey(key);
        if (vk == 0)
        {
            return false;
        }

        gesture = Format(nativeModifiers, key);
        return true;
    }

    private static bool TryParseModifier(string token, out uint modifier)
    {
        modifier = 0;
        var normalized = token.Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "ctrl":
            case "control":
                modifier = HotKeyManager.Modifiers.Control;
                return true;
            case "shift":
                modifier = HotKeyManager.Modifiers.Shift;
                return true;
            case "alt":
                modifier = HotKeyManager.Modifiers.Alt;
                return true;
            case "win":
            case "windows":
                modifier = HotKeyManager.Modifiers.Win;
                return true;
            default:
                return false;
        }
    }

    private static bool TryParseKey(string token, out Key key)
    {
        key = Key.None;

        try
        {
            var converted = new KeyConverter().ConvertFromString(null, CultureInfo.InvariantCulture, token);
            if (converted is Key parsed && parsed != Key.None)
            {
                key = parsed;
                return true;
            }
        }
        catch
        {
            // Ignore and try fallback parsing below.
        }

        if (token.Length == 1)
        {
            var ch = token[0];
            if (char.IsLetter(ch))
            {
                if (Enum.TryParse<Key>(char.ToUpperInvariant(ch).ToString(), out var letterKey))
                {
                    key = letterKey;
                    return true;
                }
            }

            if (char.IsDigit(ch) && Enum.TryParse<Key>($"D{ch}", out var digitKey))
            {
                key = digitKey;
                return true;
            }
        }

        return false;
    }

    private static uint ToNativeModifiers(ModifierKeys modifiers)
    {
        uint result = 0;

        if ((modifiers & ModifierKeys.Control) != 0)
        {
            result |= HotKeyManager.Modifiers.Control;
        }

        if ((modifiers & ModifierKeys.Shift) != 0)
        {
            result |= HotKeyManager.Modifiers.Shift;
        }

        if ((modifiers & ModifierKeys.Alt) != 0)
        {
            result |= HotKeyManager.Modifiers.Alt;
        }

        if ((modifiers & ModifierKeys.Windows) != 0)
        {
            result |= HotKeyManager.Modifiers.Win;
        }

        return result;
    }

    private static string Format(uint modifiers, Key key)
    {
        var tokens = new List<string>(5);
        if ((modifiers & HotKeyManager.Modifiers.Control) != 0)
        {
            tokens.Add("Ctrl");
        }

        if ((modifiers & HotKeyManager.Modifiers.Shift) != 0)
        {
            tokens.Add("Shift");
        }

        if ((modifiers & HotKeyManager.Modifiers.Alt) != 0)
        {
            tokens.Add("Alt");
        }

        if ((modifiers & HotKeyManager.Modifiers.Win) != 0)
        {
            tokens.Add("Win");
        }

        tokens.Add(GetKeyDisplay(key));
        return string.Join("+", tokens);
    }

    private static string GetKeyDisplay(Key key)
    {
        if (key is >= Key.D0 and <= Key.D9)
        {
            return ((int)(key - Key.D0)).ToString(CultureInfo.InvariantCulture);
        }

        var converted = new KeyConverter().ConvertToString(null, CultureInfo.InvariantCulture, key);
        return string.IsNullOrWhiteSpace(converted) ? key.ToString() : converted;
    }

    private static bool IsModifierKey(Key key)
    {
        return key is Key.LeftAlt or Key.RightAlt
            or Key.LeftCtrl or Key.RightCtrl
            or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin
            or Key.System;
    }
}
