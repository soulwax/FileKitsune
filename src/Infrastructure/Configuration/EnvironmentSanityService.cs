using System.Net;
using System.Security.Cryptography;
using System.Text;
using FileTransformer.Application.Abstractions;
using FileTransformer.Application.Models;

namespace FileTransformer.Infrastructure.Configuration;

public sealed class EnvironmentSanityService : IEnvironmentSanityService
{
    private const string DefaultGeminiModel = "gemini-2.0-flash";
    private const string DefaultGeminiEndpoint = "https://generativelanguage.googleapis.com/v1beta";
    private static readonly TimeSpan GeminiPingCooldown = TimeSpan.FromHours(24);
    private readonly AppEnvironmentResolver environmentResolver;
    private readonly IHttpClientFactory httpClientFactory;

    public EnvironmentSanityService(AppEnvironmentResolver environmentResolver, IHttpClientFactory httpClientFactory)
    {
        this.environmentResolver = environmentResolver;
        this.httpClientFactory = httpClientFactory;
    }

    public Task<IReadOnlyList<EnvironmentSanityItem>> GetChecklistAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<EnvironmentSanityItem> items =
        [
            BuildBooleanItem("GEMINI_ENABLED", "Gemini enabled", optional: true),
            BuildSecretItem(["GEMINI_API_KEY", "GOOGLE_API_KEY"], "Gemini API key"),
            BuildRequiredStringItem("GEMINI_MODEL", "Gemini model", DefaultGeminiModel, optional: true),
            BuildUriItem("GEMINI_ENDPOINT_BASE_URL", "Gemini endpoint", DefaultGeminiEndpoint, optional: true),
            BuildPositiveIntegerItem("GEMINI_MAX_REQUESTS_PER_MINUTE", "Gemini max requests/minute", "30", optional: true),
            BuildPositiveIntegerItem("GEMINI_REQUEST_TIMEOUT_SECONDS", "Gemini timeout (seconds)", "30", optional: true),
            BuildPositiveIntegerItem("GEMINI_MAX_PROMPT_CHARACTERS", "Gemini max prompt characters", "4000", optional: true),
            BuildBooleanItem(["FILEKITSUNE_OFFLINE_MODE", "FILETRANSFORMER_OFFLINE_MODE"], "Offline mode", optional: true),
            BuildConnectionStringItem(["NILEDB_URL", "POSTGRES_URL", "DATABASE_URL"], "Shared persistence connection", optional: true)
        ];

        return Task.FromResult(items);
    }

    public Task<EnvironmentPingAvailability> GetGeminiPingAvailabilityAsync(GeminiOptions settings, CancellationToken cancellationToken)
    {
        var context = ResolveGeminiPingContext();
        if (!context.CanAttempt)
        {
            return Task.FromResult(new EnvironmentPingAvailability
            {
                CanPing = false,
                CurrentFingerprint = context.Fingerprint,
                Message = context.BlockedReason
            });
        }

        var currentFingerprint = context.Fingerprint;
        var validatedAtUtc = settings.EnvironmentPingValidatedAtUtc;
        var persistedFingerprint = settings.EnvironmentPingFingerprint ?? string.Empty;

        if (validatedAtUtc is null || string.IsNullOrWhiteSpace(persistedFingerprint))
        {
            return Task.FromResult(new EnvironmentPingAvailability
            {
                CanPing = true,
                CurrentFingerprint = currentFingerprint,
                Message = "No successful Gemini environment ping has been cached yet."
            });
        }

        if (!string.Equals(persistedFingerprint, currentFingerprint, StringComparison.Ordinal))
        {
            return Task.FromResult(new EnvironmentPingAvailability
            {
                CanPing = true,
                CurrentFingerprint = currentFingerprint,
                Message = "The Gemini environment changed since the last successful ping."
            });
        }

        var nextAllowedAtUtc = validatedAtUtc.Value.Add(GeminiPingCooldown);
        if (DateTimeOffset.UtcNow >= nextAllowedAtUtc)
        {
            return Task.FromResult(new EnvironmentPingAvailability
            {
                CanPing = true,
                CurrentFingerprint = currentFingerprint,
                NextAllowedAtUtc = nextAllowedAtUtc,
                Message = "The Gemini ping cooldown has expired."
            });
        }

        return Task.FromResult(new EnvironmentPingAvailability
        {
            CanPing = false,
            CurrentFingerprint = currentFingerprint,
            NextAllowedAtUtc = nextAllowedAtUtc,
            Message = $"Gemini was already validated successfully. The next ping is available after {nextAllowedAtUtc.ToLocalTime():g}."
        });
    }

    public async Task<EnvironmentPingResult> PingGeminiAsync(GeminiOptions settings, CancellationToken cancellationToken)
    {
        var availability = await GetGeminiPingAvailabilityAsync(settings, cancellationToken);
        if (!availability.CanPing)
        {
            return new EnvironmentPingResult
            {
                Status = EnvironmentSanityStatus.Optional,
                Message = availability.Message,
                SuccessfulFingerprint = availability.CurrentFingerprint
            };
        }

        var context = ResolveGeminiPingContext();
        if (!context.CanAttempt)
        {
            return new EnvironmentPingResult
            {
                Status = context.ApiKey is null ? EnvironmentSanityStatus.Missing : EnvironmentSanityStatus.Invalid,
                Message = context.BlockedReason,
                SuccessfulFingerprint = context.Fingerprint
            };
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, context.TimeoutSeconds)));

        var requestUri = string.IsNullOrWhiteSpace(context.Model)
            ? new Uri(
                context.EndpointUri!,
                $"models?key={Uri.EscapeDataString(context.ApiKey!.Value)}")
            : new Uri(
                context.EndpointUri!,
                $"models/{Uri.EscapeDataString(context.Model)}?key={Uri.EscapeDataString(context.ApiKey!.Value)}");

        try
        {
            var client = httpClientFactory.CreateClient("GeminiClassifier");
            using var response = await client.GetAsync(requestUri, timeoutCts.Token);
            var responseContent = await response.Content.ReadAsStringAsync(timeoutCts.Token);

            if (response.IsSuccessStatusCode)
            {
                return new EnvironmentPingResult
                {
                    Status = EnvironmentSanityStatus.Valid,
                    Message = string.IsNullOrWhiteSpace(context.Model)
                        ? "Gemini responded successfully and the API key is valid."
                        : $"Gemini responded successfully for model '{context.Model}'.",
                    SuccessfulFingerprint = context.Fingerprint,
                    SuccessfulAtUtc = DateTimeOffset.UtcNow
                };
            }

            return new EnvironmentPingResult
            {
                Status = response.StatusCode switch
                {
                    HttpStatusCode.BadRequest => EnvironmentSanityStatus.Invalid,
                    HttpStatusCode.Unauthorized => EnvironmentSanityStatus.Invalid,
                    HttpStatusCode.Forbidden => EnvironmentSanityStatus.Invalid,
                    HttpStatusCode.NotFound => EnvironmentSanityStatus.Invalid,
                    _ => EnvironmentSanityStatus.Invalid
                },
                Message = BuildFailureMessage(response.StatusCode, responseContent),
                SuccessfulFingerprint = context.Fingerprint
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new EnvironmentPingResult
            {
                Status = EnvironmentSanityStatus.Invalid,
                Message = "The Gemini ping timed out.",
                SuccessfulFingerprint = context.Fingerprint
            };
        }
        catch (Exception exception)
        {
            return new EnvironmentPingResult
            {
                Status = EnvironmentSanityStatus.Invalid,
                Message = $"The Gemini ping failed: {exception.Message}",
                SuccessfulFingerprint = context.Fingerprint
            };
        }
    }

    private GeminiPingContext ResolveGeminiPingContext()
    {
        var apiKey = environmentResolver.GetValue("GEMINI_API_KEY", "GOOGLE_API_KEY");
        if (apiKey is null)
        {
            return GeminiPingContext.Blocked("No Gemini API key was found in the environment.");
        }

        var modelValue = environmentResolver.GetValue("GEMINI_MODEL");
        var endpointValue = environmentResolver.GetValue("GEMINI_ENDPOINT_BASE_URL");
        var timeoutValue = environmentResolver.GetValue("GEMINI_REQUEST_TIMEOUT_SECONDS");
        var model = modelValue?.Value?.Trim() ?? string.Empty;
        var endpoint = string.IsNullOrWhiteSpace(endpointValue?.Value) ? DefaultGeminiEndpoint : endpointValue.Value.Trim();

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri))
        {
            return GeminiPingContext.Blocked("The Gemini endpoint URL is not a valid absolute URI.", apiKey, CreateGeminiFingerprint(apiKey.Value, model, endpoint));
        }

        var timeoutSeconds = 30;
        if (!string.IsNullOrWhiteSpace(timeoutValue?.Value) &&
            (!int.TryParse(timeoutValue.Value, out timeoutSeconds) || timeoutSeconds <= 0))
        {
            return GeminiPingContext.Blocked("The Gemini timeout value must be a positive integer.", apiKey, CreateGeminiFingerprint(apiKey.Value, model, endpoint));
        }

        return new GeminiPingContext(
            true,
            string.Empty,
            apiKey,
            model,
            endpointUri,
            timeoutSeconds,
            CreateGeminiFingerprint(apiKey.Value, model, endpoint));
    }

    private EnvironmentSanityItem BuildBooleanItem(string key, string displayName, bool optional) =>
        BuildBooleanItem([key], displayName, optional);

    private EnvironmentSanityItem BuildBooleanItem(string[] keys, string displayName, bool optional)
    {
        var resolved = environmentResolver.GetValue(keys);
        if (resolved is null)
        {
            return BuildMissingItem(keys[0], displayName, optional, "No environment value was provided.");
        }

        if (TryParseBoolean(resolved.Value, out var parsed))
        {
            return new EnvironmentSanityItem
            {
                Key = resolved.Key,
                DisplayName = displayName,
                SourceLabel = resolved.Source,
                Status = EnvironmentSanityStatus.Valid,
                ValuePreview = parsed ? "true" : "false",
                Message = "Boolean value parsed successfully."
            };
        }

        return new EnvironmentSanityItem
        {
            Key = resolved.Key,
            DisplayName = displayName,
            SourceLabel = resolved.Source,
            Status = EnvironmentSanityStatus.Invalid,
            ValuePreview = resolved.Value,
            Message = "Expected true/false or 1/0."
        };
    }

    private EnvironmentSanityItem BuildSecretItem(string[] keys, string displayName)
    {
        var resolved = environmentResolver.GetValue(keys);
        if (resolved is null)
        {
            return BuildMissingItem(keys[0], displayName, optional: false, "No secret value was provided.");
        }

        return new EnvironmentSanityItem
        {
            Key = resolved.Key,
            DisplayName = displayName,
            SourceLabel = resolved.Source,
            Status = EnvironmentSanityStatus.Valid,
            ValuePreview = MaskSecret(resolved.Value),
            Message = "Secret value is present."
        };
    }

    private EnvironmentSanityItem BuildRequiredStringItem(string key, string displayName, string fallbackValue, bool optional)
    {
        var resolved = environmentResolver.GetValue(key);
        if (resolved is null)
        {
            return BuildMissingItem(key, displayName, optional, $"Not set. The app default is '{fallbackValue}'.");
        }

        return new EnvironmentSanityItem
        {
            Key = resolved.Key,
            DisplayName = displayName,
            SourceLabel = resolved.Source,
            Status = EnvironmentSanityStatus.Valid,
            ValuePreview = resolved.Value,
            Message = "String value is present."
        };
    }

    private EnvironmentSanityItem BuildUriItem(string key, string displayName, string fallbackValue, bool optional)
    {
        var resolved = environmentResolver.GetValue(key);
        if (resolved is null)
        {
            return BuildMissingItem(key, displayName, optional, $"Not set. The app default is '{fallbackValue}'.");
        }

        if (Uri.TryCreate(resolved.Value, UriKind.Absolute, out _))
        {
            return new EnvironmentSanityItem
            {
                Key = resolved.Key,
                DisplayName = displayName,
                SourceLabel = resolved.Source,
                Status = EnvironmentSanityStatus.Valid,
                ValuePreview = resolved.Value,
                Message = "Absolute URL parsed successfully."
            };
        }

        return new EnvironmentSanityItem
        {
            Key = resolved.Key,
            DisplayName = displayName,
            SourceLabel = resolved.Source,
            Status = EnvironmentSanityStatus.Invalid,
            ValuePreview = resolved.Value,
            Message = "Expected an absolute URL."
        };
    }

    private EnvironmentSanityItem BuildPositiveIntegerItem(string key, string displayName, string fallbackValue, bool optional)
    {
        var resolved = environmentResolver.GetValue(key);
        if (resolved is null)
        {
            return BuildMissingItem(key, displayName, optional, $"Not set. The app default is '{fallbackValue}'.");
        }

        if (int.TryParse(resolved.Value, out var parsed) && parsed > 0)
        {
            return new EnvironmentSanityItem
            {
                Key = resolved.Key,
                DisplayName = displayName,
                SourceLabel = resolved.Source,
                Status = EnvironmentSanityStatus.Valid,
                ValuePreview = parsed.ToString(),
                Message = "Positive integer parsed successfully."
            };
        }

        return new EnvironmentSanityItem
        {
            Key = resolved.Key,
            DisplayName = displayName,
            SourceLabel = resolved.Source,
            Status = EnvironmentSanityStatus.Invalid,
            ValuePreview = resolved.Value,
            Message = "Expected a positive integer."
        };
    }

    private EnvironmentSanityItem BuildConnectionStringItem(string[] keys, string displayName, bool optional)
    {
        var resolved = environmentResolver.GetValue(keys);
        if (resolved is null)
        {
            return BuildMissingItem(keys[0], displayName, optional, "No shared persistence connection string is configured.");
        }

        return new EnvironmentSanityItem
        {
            Key = resolved.Key,
            DisplayName = displayName,
            SourceLabel = resolved.Source,
            Status = EnvironmentSanityStatus.Valid,
            ValuePreview = MaskConnectionString(resolved.Value),
            Message = "Connection string is present."
        };
    }

    private static EnvironmentSanityItem BuildMissingItem(string key, string displayName, bool optional, string message) =>
        new()
        {
            Key = key,
            DisplayName = displayName,
            SourceLabel = "-",
            Status = optional ? EnvironmentSanityStatus.Optional : EnvironmentSanityStatus.Missing,
            ValuePreview = "-",
            Message = message
        };

    private static bool TryParseBoolean(string value, out bool parsed)
    {
        if (bool.TryParse(value, out parsed))
        {
            return true;
        }

        switch (value.Trim())
        {
            case "1":
                parsed = true;
                return true;
            case "0":
                parsed = false;
                return true;
            default:
                parsed = false;
                return false;
        }
    }

    private static string MaskSecret(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length <= 8)
        {
            return new string('*', trimmed.Length);
        }

        return $"{trimmed[..4]}...{trimmed[^4..]}";
    }

    private static string MaskConnectionString(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return MaskSecret(value);
        }

        return $"{uri.Scheme}://{uri.Host}";
    }

    private static string BuildFailureMessage(HttpStatusCode statusCode, string responseContent)
    {
        var normalized = string.IsNullOrWhiteSpace(responseContent)
            ? string.Empty
            : responseContent.Replace(Environment.NewLine, " ").Trim();

        if (statusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return "Gemini rejected the API key.";
        }

        if (statusCode == HttpStatusCode.NotFound)
        {
            return "Gemini could not find the configured model or endpoint. Try setting GEMINI_MODEL to a valid current Gemini API model such as gemini-2.0-flash.";
        }

        if (statusCode == HttpStatusCode.BadRequest)
        {
            return string.IsNullOrWhiteSpace(normalized)
                ? "Gemini rejected the request as invalid."
                : $"Gemini rejected the request: {normalized}";
        }

        return string.IsNullOrWhiteSpace(normalized)
            ? $"Gemini ping failed with status {(int)statusCode}."
            : $"Gemini ping failed with status {(int)statusCode}: {normalized}";
    }

    private static string CreateGeminiFingerprint(string apiKey, string model, string endpoint)
    {
        var material = $"{apiKey}\n{model}\n{endpoint.Trim()}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return Convert.ToHexString(bytes);
    }

    private sealed record GeminiPingContext(
        bool CanAttempt,
        string BlockedReason,
        ResolvedEnvironmentValue? ApiKey,
        string Model,
        Uri? EndpointUri,
        int TimeoutSeconds,
        string Fingerprint)
    {
        public static GeminiPingContext Blocked(string reason, ResolvedEnvironmentValue? apiKey = null, string? fingerprint = null) =>
            new(false, reason, apiKey, string.Empty, null, 30, fingerprint ?? string.Empty);
    }
}
