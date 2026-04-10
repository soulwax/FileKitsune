using System.Net;
using System.Net.Http.Json;
using FileTransformer.Application.Abstractions;
using FileTransformer.Application.Models;
using FileTransformer.Domain.Models;
using Microsoft.Extensions.Logging;

namespace FileTransformer.Infrastructure.Classification;

public sealed class GeminiOrganizationAdvisor : IGeminiOrganizationAdvisor
{
    private readonly IHttpClientFactory httpClientFactory;
    private readonly GeminiOrganizationGuidancePromptBuilder promptBuilder;
    private readonly GeminiOrganizationGuidanceParser parser;
    private readonly ILogger<GeminiOrganizationAdvisor> logger;

    public GeminiOrganizationAdvisor(
        IHttpClientFactory httpClientFactory,
        GeminiOrganizationGuidancePromptBuilder promptBuilder,
        GeminiOrganizationGuidanceParser parser,
        ILogger<GeminiOrganizationAdvisor> logger)
    {
        this.httpClientFactory = httpClientFactory;
        this.promptBuilder = promptBuilder;
        this.parser = parser;
        this.logger = logger;
    }

    public async Task<OrganizationGuidance?> AdviseAsync(
        IReadOnlyList<FileAnalysisContext> contexts,
        OrganizationSettings settings,
        GeminiOptions options,
        CancellationToken cancellationToken)
    {
        if (contexts.Count == 0)
        {
            return null;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, options.RequestTimeoutSeconds)));

        var prompt = promptBuilder.BuildPrompt(contexts, settings, Math.Min(options.MaxPromptCharacters, 2_400));
        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.1,
                responseMimeType = "application/json"
            }
        };

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                var client = httpClientFactory.CreateClient("GeminiClassifier");
                var endpoint =
                    $"{options.EndpointBaseUrl.TrimEnd('/')}/models/{options.Model}:generateContent?key={Uri.EscapeDataString(options.ApiKey)}";

                using var response = await client.PostAsJsonAsync(endpoint, requestBody, timeoutCts.Token);
                var responseContent = await response.Content.ReadAsStringAsync(timeoutCts.Token);

                if (IsTransient(response.StatusCode) && attempt < 2)
                {
                    await Task.Delay(TimeSpan.FromSeconds(attempt), timeoutCts.Token);
                    continue;
                }

                response.EnsureSuccessStatusCode();
                return parser.ParseApiResponse(responseContent);
            }
            catch (Exception exception) when (attempt < 2 && IsTransient(exception))
            {
                logger.LogWarning(exception, "Transient Gemini organization guidance failure. Retrying attempt {Attempt}.", attempt);
                await Task.Delay(TimeSpan.FromSeconds(attempt), timeoutCts.Token);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Gemini organization guidance failed.");
                return null;
            }
        }

        return null;
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
