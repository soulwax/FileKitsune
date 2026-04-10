namespace FileTransformer.App.Services;

public interface ILocalizationService
{
    void ApplyLanguage(string cultureName);

    string GetString(string resourceKey);

    string Format(string resourceKey, params object[] arguments);
}
