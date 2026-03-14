using System.Net;
using System.Net.Http.Json;
using FileTransformer.Application.Abstractions;
using FileTransformer.Application.Models;
using FileTransformer.Domain.Models;
using Microsoft.Extensions.Logging;

namespace FileTransformer.Infrastructure.Classification;

public sealed class GeminiSemanticClassifier : IGeminiSemanticClassifier
{
    private readonly IHttpClientFactory httpClientFactory;
    private readonly GeminiPromptBuilder promptBuilder;
    private readonly GeminiResponseParser responseParser;
    private readonly ILogger<GeminiSemanticClassifier> logger;
    private readonly SemaphoreSlim rateGate = new(1, 1);
    private DateTimeOffset nextAllowedRequestUtc = DateTimeOffset.MinValue;

    public GeminiSemanticClassifier(
        IHttpClientFactory httpClientFactory,
        GeminiPromptBuilder promptBuilder,
        GeminiResponseParser responseParser,
        ILogger<GeminiSemanticClassifier> logger)
    {
        this.httpClientFactory = httpClientFactory;
        this.promptBuilder = promptBuilder;
        this.responseParser = responseParser;
        this.logger = logger;
    }

    public async Task<SemanticInsight?> ClassifyAsync(
        SemanticAnalysisRequest request,
        GeminiOptions options,
        CancellationToken cancellationToken)
    {
        await WaitForRateLimitAsync(options, cancellationToken);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, options.RequestTimeoutSeconds)));

        var prompt = promptBuilder.BuildPrompt(request, options.MaxPromptCharacters);
        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[]
                    {
                        new
                        {
                            text = prompt
                        }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.1,
                responseMimeType = "application/json",
                responseJsonSchema = new Dictionary<string, object?>
                {
                    ["type"] = "object",
                    ["required"] = new[]
                    {
                        "category",
                        "projectTopic",
                        "detectedLanguageContext",
                        "confidence",
                        "suggestedFolderPathFragment",
                        "explanation"
                    },
                    ["additionalProperties"] = false,
                    ["properties"] = new Dictionary<string, object?>
                    {
                        ["category"] = new Dictionary<string, object?>
                        {
                            ["type"] = "string",
                            ["description"] = "Semantic category label or stable key."
                        },
                        ["projectTopic"] = new Dictionary<string, object?>
                        {
                            ["type"] = new[] { "string", "null" },
                            ["description"] = "Project, topic, or workstream hint."
                        },
                        ["detectedLanguageContext"] = new Dictionary<string, object?>
                        {
                            ["type"] = "string",
                            ["enum"] = new[] { "German", "English", "Mixed", "Unclear" }
                        },
                        ["confidence"] = new Dictionary<string, object?>
                        {
                            ["type"] = "number",
                            ["minimum"] = 0,
                            ["maximum"] = 1
                        },
                        ["suggestedFolderPathFragment"] = new Dictionary<string, object?>
                        {
                            ["type"] = new[] { "string", "null" },
                            ["description"] = "Advisory folder path fragment only."
                        },
                        ["explanation"] = new Dictionary<string, object?>
                        {
                            ["type"] = "string",
                            ["description"] = "Short explanation of the classification."
                        }
                    }
                }
            }
        };

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                var client = httpClientFactory.CreateClient("GeminiClassifier");
                var endpoint =
                    $"{options.EndpointBaseUrl.TrimEnd('/')}/models/{options.Model}:generateContent?key={Uri.EscapeDataString(options.ApiKey)}";

                using var response = await client.PostAsJsonAsync(endpoint, requestBody, timeoutCts.Token);
                var responseContent = await response.Content.ReadAsStringAsync(timeoutCts.Token);

                if (IsTransient(response.StatusCode) && attempt < 3)
                {
                    logger.LogWarning("Transient Gemini error {StatusCode} for {File}. Retrying attempt {Attempt}.",
                        response.StatusCode,
                        request.File.RelativePath,
                        attempt);
                    await Task.Delay(TimeSpan.FromSeconds(attempt), timeoutCts.Token);
                    continue;
                }

                response.EnsureSuccessStatusCode();
                return responseParser.ParseApiResponse(responseContent);
            }
            catch (Exception exception) when (attempt < 3 && IsTransient(exception))
            {
                logger.LogWarning(exception, "Transient Gemini failure for {File}. Retrying attempt {Attempt}.",
                    request.File.RelativePath,
                    attempt);
                await Task.Delay(TimeSpan.FromSeconds(attempt), timeoutCts.Token);
            }
        }

        return null;
    }

    private async Task WaitForRateLimitAsync(GeminiOptions options, CancellationToken cancellationToken)
    {
        var minimumSpacing = TimeSpan.FromMinutes(1d / Math.Max(1, options.MaxRequestsPerMinute));

        await rateGate.WaitAsync(cancellationToken);
        try
        {
            var now = DateTimeOffset.UtcNow;
            if (nextAllowedRequestUtc > now)
            {
                await Task.Delay(nextAllowedRequestUtc - now, cancellationToken);
            }

            nextAllowedRequestUtc = DateTimeOffset.UtcNow.Add(minimumSpacing);
        }
        finally
        {
            rateGate.Release();
        }
    }

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode == HttpStatusCode.TooManyRequests ||
        statusCode == HttpStatusCode.BadGateway ||
        statusCode == HttpStatusCode.GatewayTimeout ||
        statusCode == HttpStatusCode.ServiceUnavailable ||
        statusCode == HttpStatusCode.RequestTimeout ||
        statusCode == HttpStatusCode.InternalServerError;

    private static bool IsTransient(Exception exception) =>
        exception is HttpRequestException or TaskCanceledException or TimeoutException;
}
