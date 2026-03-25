using Microsoft.Win32;
using NNotify.Localization;

namespace NNotify.Services;

public sealed class StartupRegistrationService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "NNotify";

    public bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            var command = key?.GetValue(ValueName) as string;
            return !string.IsNullOrWhiteSpace(command);
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("Failed to read startup registration", ex);
            return false;
        }
    }

    public bool TryEnable(out string errorMessage)
    {
        errorMessage = string.Empty;

        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            errorMessage = Loc.Text("StartupErrorProcessPath");
            return false;
        }

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (key is null)
            {
                errorMessage = Loc.Text("StartupErrorRegistryKey");
                return false;
            }

            key.SetValue(ValueName, $"\"{processPath}\"", RegistryValueKind.String);
            return true;
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("Failed to enable startup registration", ex);
            errorMessage = Loc.Text("StartupErrorEnable");
            return false;
        }
    }

    public bool TryDisable(out string errorMessage)
    {
        errorMessage = string.Empty;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
            return true;
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("Failed to disable startup registration", ex);
            errorMessage = Loc.Text("StartupErrorDisable");
            return false;
        }
    }
}
