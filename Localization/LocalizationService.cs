using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Threading;

namespace NNotify.Localization;

public sealed class LocalizationService : INotifyPropertyChanged
{
    private static readonly HashSet<string> SupportedLanguages = ["ru", "en", "de"];
    private readonly ResourceManager _resourceManager = new("NNotify.Localization.Strings", typeof(LocalizationService).Assembly);
    private CultureInfo _culture = CultureInfo.CurrentUICulture;

    private LocalizationService()
    {
    }

    public static LocalizationService Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public string this[string key] => GetString(key);

    public CultureInfo CurrentCulture => _culture;

    public void UseSystemCulture()
    {
        SetCulture(ResolveSupportedCulture(CultureInfo.CurrentUICulture));
    }

    public void SetCulture(CultureInfo culture)
    {
        var supported = ResolveSupportedCulture(culture);
        if (Equals(supported, _culture))
        {
            return;
        }

        _culture = supported;

        CultureInfo.DefaultThreadCurrentCulture = supported;
        CultureInfo.DefaultThreadCurrentUICulture = supported;
        Thread.CurrentThread.CurrentCulture = supported;
        Thread.CurrentThread.CurrentUICulture = supported;

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }

    public string GetString(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        return _resourceManager.GetString(key, _culture) ??
               _resourceManager.GetString(key, CultureInfo.InvariantCulture) ??
               key;
    }

    public string Format(string key, params object[] args)
    {
        var format = GetString(key);
        return string.Format(_culture, format, args);
    }

    private static CultureInfo ResolveSupportedCulture(CultureInfo culture)
    {
        var language = culture.TwoLetterISOLanguageName.ToLowerInvariant();
        if (!SupportedLanguages.Contains(language))
        {
            language = "en";
        }

        return CultureInfo.GetCultureInfo(language);
    }
}

public static class Loc
{
    public static string Text(string key)
    {
        return LocalizationService.Instance.GetString(key);
    }

    public static string Format(string key, params object[] args)
    {
        return LocalizationService.Instance.Format(key, args);
    }
}
