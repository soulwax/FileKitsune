using System.Globalization;

namespace FileTransformer.App.Services;

public sealed class LocalizationService : ILocalizationService
{
    private const string LocalizationPrefix = "Localization/Strings.";

    public void ApplyLanguage(string cultureName)
    {
        var application = System.Windows.Application.Current;
        if (application is null)
        {
            return;
        }

        var culture = CultureInfo.GetCultureInfo(string.IsNullOrWhiteSpace(cultureName) ? "de-DE" : cultureName);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        var dictionary = new System.Windows.ResourceDictionary
        {
            Source = new Uri($"{LocalizationPrefix}{culture.Name}.xaml", UriKind.Relative)
        };

        var mergedDictionaries = application.Resources.MergedDictionaries;
        var existingIndex = -1;
        for (var index = 0; index < mergedDictionaries.Count; index++)
        {
            var source = mergedDictionaries[index].Source?.OriginalString;
            if (source is not null && source.StartsWith(LocalizationPrefix, StringComparison.OrdinalIgnoreCase))
            {
                existingIndex = index;
                break;
            }
        }

        if (existingIndex >= 0)
        {
            mergedDictionaries[existingIndex] = dictionary;
        }
        else
        {
            mergedDictionaries.Insert(0, dictionary);
        }
    }

    public string GetString(string resourceKey)
    {
        var application = System.Windows.Application.Current;
        if (application?.TryFindResource(resourceKey) is string value && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return resourceKey;
    }

    public string Format(string resourceKey, params object[] arguments) =>
        string.Format(CultureInfo.CurrentCulture, GetString(resourceKey), arguments);
}
