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

        var settingsFileExists = File.Exists(appStoragePaths.SettingsFilePath);
        var envelope = new StoredSettingsEnvelope();
        if (settingsFileExists)
        {
            await using var stream = File.OpenRead(appStoragePaths.SettingsFilePath);
            envelope = await JsonSerializer.DeserializeAsync<StoredSettingsEnvelope>(stream, SerializerOptions, cancellationToken)
                ?? new StoredSettingsEnvelope();
        }

        var env = DotEnv.LoadIfPresent(
            Path.Combine(Directory.GetCurrentDirectory(), ".env"),
            Path.Combine(AppContext.BaseDirectory, ".env"));

        string? GetEnvValue(params string[] keys)
        {
            foreach (var key in keys)
            {
                var value = Environment.GetEnvironmentVariable(key);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }

                if (env.TryGetValue(key, out var envValue) && !string.IsNullOrWhiteSpace(envValue))
                {
                    return envValue;
                }
            }

            return null;
        }

        static bool? ParseBool(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (bool.TryParse(value, out var parsed))
            {
                return parsed;
            }

            return value.Trim() switch
            {
                "1" => true,
                "0" => false,
                _ => null
            };
        }

        static int? ParseInt(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return int.TryParse(value, out var parsed) ? parsed : null;
        }

        var envEnabled = ParseBool(GetEnvValue("GEMINI_ENABLED"));
        var envApiKey = GetEnvValue("GEMINI_API_KEY", "GOOGLE_API_KEY");
        var envModel = GetEnvValue("GEMINI_MODEL");
        var envEndpoint = GetEnvValue("GEMINI_ENDPOINT_BASE_URL");
        var envMaxRequests = ParseInt(GetEnvValue("GEMINI_MAX_REQUESTS_PER_MINUTE"));
        var envTimeoutSeconds = ParseInt(GetEnvValue("GEMINI_REQUEST_TIMEOUT_SECONDS"));
        var envMaxPromptChars = ParseInt(GetEnvValue("GEMINI_MAX_PROMPT_CHARACTERS"));
        var decryptedApiKey = Decrypt(envelope.Gemini.ApiKeyProtected);

        return new AppSettings
        {
            UiLanguage = string.IsNullOrWhiteSpace(envelope.UiLanguage) ? "de-DE" : envelope.UiLanguage,
            Organization = envelope.Organization ?? new(),
            Gemini = new GeminiOptions
            {
                Enabled = settingsFileExists
                    ? envelope.Gemini.Enabled
                    : envEnabled ?? envelope.Gemini.Enabled,
                ApiKey = string.IsNullOrWhiteSpace(decryptedApiKey) ? envApiKey ?? string.Empty : decryptedApiKey,
                Model = string.IsNullOrWhiteSpace(envelope.Gemini.Model)
                    ? envModel ?? "gemini-1.5-flash"
                    : envelope.Gemini.Model,
                EndpointBaseUrl = string.IsNullOrWhiteSpace(envelope.Gemini.EndpointBaseUrl)
                    ? envEndpoint ?? "https://generativelanguage.googleapis.com/v1beta"
                    : envelope.Gemini.EndpointBaseUrl,
                MaxRequestsPerMinute = envelope.Gemini.MaxRequestsPerMinute <= 0
                    ? envMaxRequests ?? 30
                    : envelope.Gemini.MaxRequestsPerMinute,
                RequestTimeoutSeconds = envelope.Gemini.RequestTimeoutSeconds <= 0
                    ? envTimeoutSeconds ?? 30
                    : envelope.Gemini.RequestTimeoutSeconds,
                MaxPromptCharacters = envelope.Gemini.MaxPromptCharacters <= 0
                    ? envMaxPromptChars ?? 4_000
                    : envelope.Gemini.MaxPromptCharacters
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
