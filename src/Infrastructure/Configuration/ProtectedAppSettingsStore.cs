using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Runtime.Versioning;
using FileTransformer.Application.Abstractions;
using FileTransformer.Application.Models;

namespace FileTransformer.Infrastructure.Configuration;

[SupportedOSPlatform("windows")]
public sealed class ProtectedAppSettingsStore : IAppSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly AppStoragePaths appStoragePaths;

    public ProtectedAppSettingsStore(AppStoragePaths appStoragePaths)
    {
        this.appStoragePaths = appStoragePaths;
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(appStoragePaths.RootDirectory);

        if (!File.Exists(appStoragePaths.SettingsFilePath))
        {
            return new AppSettings();
        }

        await using var stream = File.OpenRead(appStoragePaths.SettingsFilePath);
        var envelope = await JsonSerializer.DeserializeAsync<StoredSettingsEnvelope>(stream, SerializerOptions, cancellationToken)
            ?? new StoredSettingsEnvelope();

        return new AppSettings
        {
            UiLanguage = string.IsNullOrWhiteSpace(envelope.UiLanguage) ? "de-DE" : envelope.UiLanguage,
            Organization = envelope.Organization ?? new(),
            Gemini = new GeminiOptions
            {
                Enabled = envelope.Gemini.Enabled,
                ApiKey = Decrypt(envelope.Gemini.ApiKeyProtected),
                Model = string.IsNullOrWhiteSpace(envelope.Gemini.Model) ? "gemini-1.5-flash" : envelope.Gemini.Model,
                EndpointBaseUrl = string.IsNullOrWhiteSpace(envelope.Gemini.EndpointBaseUrl)
                    ? "https://generativelanguage.googleapis.com/v1beta"
                    : envelope.Gemini.EndpointBaseUrl,
                MaxRequestsPerMinute = envelope.Gemini.MaxRequestsPerMinute <= 0 ? 30 : envelope.Gemini.MaxRequestsPerMinute,
                RequestTimeoutSeconds = envelope.Gemini.RequestTimeoutSeconds <= 0 ? 30 : envelope.Gemini.RequestTimeoutSeconds,
                MaxPromptCharacters = envelope.Gemini.MaxPromptCharacters <= 0 ? 4_000 : envelope.Gemini.MaxPromptCharacters
            }
        };
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(appStoragePaths.RootDirectory);

        var envelope = new StoredSettingsEnvelope
        {
            UiLanguage = string.IsNullOrWhiteSpace(settings.UiLanguage) ? "de-DE" : settings.UiLanguage,
            Organization = settings.Organization,
            Gemini = new StoredGeminiSettings
            {
                Enabled = settings.Gemini.Enabled,
                ApiKeyProtected = Encrypt(settings.Gemini.ApiKey),
                Model = settings.Gemini.Model,
                EndpointBaseUrl = settings.Gemini.EndpointBaseUrl,
                MaxRequestsPerMinute = settings.Gemini.MaxRequestsPerMinute,
                RequestTimeoutSeconds = settings.Gemini.RequestTimeoutSeconds,
                MaxPromptCharacters = settings.Gemini.MaxPromptCharacters
            }
        };

        await using var stream = File.Create(appStoragePaths.SettingsFilePath);
        await JsonSerializer.SerializeAsync(stream, envelope, SerializerOptions, cancellationToken);
    }

    private static string Encrypt(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var input = Encoding.UTF8.GetBytes(value);
        var protectedBytes = ProtectedData.Protect(input, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    private static string Decrypt(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        try
        {
            var protectedBytes = Convert.FromBase64String(value);
            var clearBytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(clearBytes);
        }
        catch
        {
            return string.Empty;
        }
    }

    private sealed class StoredSettingsEnvelope
    {
        public string UiLanguage { get; set; } = "de-DE";

        public FileTransformer.Domain.Models.OrganizationSettings? Organization { get; set; }

        public StoredGeminiSettings Gemini { get; set; } = new();
    }

    private sealed class StoredGeminiSettings
    {
        public bool Enabled { get; set; } = true;

        public string ApiKeyProtected { get; set; } = string.Empty;

        public string Model { get; set; } = string.Empty;

        public string EndpointBaseUrl { get; set; } = string.Empty;

        public int MaxRequestsPerMinute { get; set; } = 30;

        public int RequestTimeoutSeconds { get; set; } = 30;

        public int MaxPromptCharacters { get; set; } = 4_000;
    }
}
