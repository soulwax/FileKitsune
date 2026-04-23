using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FileTransformer.Application.Models;
using FileTransformer.Infrastructure.Configuration;
using Xunit;

namespace FileTransformer.Tests.Infrastructure;

public sealed class EnvironmentSanityServiceTests
{
    [Fact]
    public async Task Checklist_marks_required_gemini_key_missing()
    {
        var resolver = new AppEnvironmentResolver(
            processEnvironment: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            dotEnvValues: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            currentDirectory: "C:\\Workspace",
            baseDirectory: "C:\\Workspace");
        var service = new EnvironmentSanityService(resolver, new StubHttpClientFactory(_ => new HttpResponseMessage(HttpStatusCode.OK)));

        var items = await service.GetChecklistAsync(CancellationToken.None);

        var apiKey = Assert.Single(items, item => item.Key == "GEMINI_API_KEY");
        Assert.Equal(EnvironmentSanityStatus.Missing, apiKey.Status);
    }

    [Fact]
    public async Task Checklist_accepts_valid_integer_and_boolean_values()
    {
        var resolver = new AppEnvironmentResolver(
            processEnvironment: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["GEMINI_ENABLED"] = "1",
                ["GEMINI_API_KEY"] = "abcd1234secret",
                ["GEMINI_REQUEST_TIMEOUT_SECONDS"] = "45"
            },
            dotEnvValues: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            currentDirectory: "C:\\Workspace",
            baseDirectory: "C:\\Workspace");
        var service = new EnvironmentSanityService(resolver, new StubHttpClientFactory(_ => new HttpResponseMessage(HttpStatusCode.OK)));

        var items = await service.GetChecklistAsync(CancellationToken.None);

        Assert.Equal(EnvironmentSanityStatus.Valid, Assert.Single(items, item => item.Key == "GEMINI_ENABLED").Status);
        Assert.Equal(EnvironmentSanityStatus.Valid, Assert.Single(items, item => item.Key == "GEMINI_REQUEST_TIMEOUT_SECONDS").Status);
    }

    [Fact]
    public async Task PingGeminiAsync_returns_valid_when_model_lookup_succeeds()
    {
        var resolver = new AppEnvironmentResolver(
            processEnvironment: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["GEMINI_API_KEY"] = "abcd1234secret",
                ["GEMINI_MODEL"] = "gemini-3.1-flash-lite-preview"
            },
            dotEnvValues: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            currentDirectory: "C:\\Workspace",
            baseDirectory: "C:\\Workspace");
        var service = new EnvironmentSanityService(resolver, new StubHttpClientFactory(request =>
        {
            Assert.Contains("models/gemini-3.1-flash-lite-preview", request.RequestUri!.ToString(), StringComparison.Ordinal);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));

        var result = await service.PingGeminiAsync(new GeminiOptions(), CancellationToken.None);

        Assert.Equal(EnvironmentSanityStatus.Valid, result.Status);
    }

    [Fact]
    public async Task PingGeminiAsync_returns_invalid_when_key_is_rejected()
    {
        var resolver = new AppEnvironmentResolver(
            processEnvironment: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["GEMINI_API_KEY"] = "bad-key"
            },
            dotEnvValues: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            currentDirectory: "C:\\Workspace",
            baseDirectory: "C:\\Workspace");
        var service = new EnvironmentSanityService(resolver, new StubHttpClientFactory(_ => new HttpResponseMessage(HttpStatusCode.Forbidden)));

        var result = await service.PingGeminiAsync(new GeminiOptions(), CancellationToken.None);

        Assert.Equal(EnvironmentSanityStatus.Invalid, result.Status);
        Assert.Contains("API key", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PingGeminiAsync_is_blocked_during_cooldown_when_fingerprint_matches()
    {
        var resolver = new AppEnvironmentResolver(
            processEnvironment: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["GEMINI_API_KEY"] = "abcd1234secret",
                ["GEMINI_MODEL"] = "gemini-3.1-flash-lite-preview"
            },
            dotEnvValues: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            currentDirectory: "C:\\Workspace",
            baseDirectory: "C:\\Workspace");
        var service = new EnvironmentSanityService(resolver, new StubHttpClientFactory(_ => new HttpResponseMessage(HttpStatusCode.OK)));
        var firstResult = await service.PingGeminiAsync(new GeminiOptions(), CancellationToken.None);

        var blockedResult = await service.PingGeminiAsync(new GeminiOptions
        {
            EnvironmentPingFingerprint = firstResult.SuccessfulFingerprint,
            EnvironmentPingValidatedAtUtc = DateTimeOffset.UtcNow
        }, CancellationToken.None);

        Assert.Equal(EnvironmentSanityStatus.Optional, blockedResult.Status);
        Assert.Contains("next ping", blockedResult.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> handler;

        public StubHttpClientFactory(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            this.handler = handler;
        }

        public HttpClient CreateClient(string name) => new(new StubMessageHandler(handler));
    }

    private sealed class StubMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> handler;

        public StubMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            this.handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(handler(request));
    }
}
