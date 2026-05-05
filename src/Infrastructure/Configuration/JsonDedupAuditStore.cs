using System.Text.Encodings.Web;
using System.Text.Json;
using FileTransformer.Application.Abstractions;
using FileTransformer.Application.Models;

namespace FileTransformer.Infrastructure.Configuration;

public sealed class JsonDedupAuditStore : IDedupAuditStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly AppStoragePaths paths;

    public JsonDedupAuditStore(AppStoragePaths paths)
    {
        this.paths = paths;
    }

    public async Task<DedupAuditRunHandle> StartRunAsync(DedupAuditRunStarted run, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(paths.DedupAuditDirectory);
        var auditFilePath = Path.Combine(paths.DedupAuditDirectory, $"{run.RunId:N}.jsonl");

        await AppendEnvelopeAsync(
            auditFilePath,
            new DedupAuditEnvelope<DedupAuditRunStarted>
            {
                Event = "run-started",
                RunId = run.RunId,
                Payload = run
            },
            cancellationToken);

        return new DedupAuditRunHandle
        {
            RunId = run.RunId,
            AuditFilePath = auditFilePath
        };
    }

    public Task AppendEntryAsync(Guid runId, DedupAuditEntry entry, CancellationToken cancellationToken)
    {
        var auditFilePath = ResolveExistingAuditPath(runId);
        return AppendEnvelopeAsync(
            auditFilePath,
            new DedupAuditEnvelope<DedupAuditEntry>
            {
                Event = "entry",
                RunId = runId,
                Payload = entry
            },
            cancellationToken);
    }

    public Task CompleteRunAsync(Guid runId, DedupAuditRunCompleted completed, CancellationToken cancellationToken)
    {
        var auditFilePath = ResolveExistingAuditPath(runId);
        return AppendEnvelopeAsync(
            auditFilePath,
            new DedupAuditEnvelope<DedupAuditRunCompleted>
            {
                Event = "run-completed",
                RunId = runId,
                Payload = completed
            },
            cancellationToken);
    }

    private string ResolveExistingAuditPath(Guid runId)
    {
        var auditFilePath = Path.Combine(paths.DedupAuditDirectory, $"{runId:N}.jsonl");
        if (!File.Exists(auditFilePath))
        {
            throw new FileNotFoundException("Dedup audit run has not been started.", auditFilePath);
        }

        return auditFilePath;
    }

    private static async Task AppendEnvelopeAsync<T>(
        string auditFilePath,
        DedupAuditEnvelope<T> envelope,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            auditFilePath,
            FileMode.Append,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);

        await JsonSerializer.SerializeAsync(stream, envelope, SerializerOptions, cancellationToken);
        await stream.WriteAsync("\n"u8.ToArray(), cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private sealed class DedupAuditEnvelope<T>
    {
        public required string Event { get; init; }

        public required Guid RunId { get; init; }

        public required T Payload { get; init; }
    }
}
