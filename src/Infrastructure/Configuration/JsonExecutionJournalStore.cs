using System.Text.Encodings.Web;
using System.Text.Json;
using FileTransformer.Application.Abstractions;
using FileTransformer.Domain.Models;

namespace FileTransformer.Infrastructure.Configuration;

public sealed class JsonExecutionJournalStore : IExecutionJournalStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly AppStoragePaths appStoragePaths;

    public JsonExecutionJournalStore(AppStoragePaths appStoragePaths)
    {
        this.appStoragePaths = appStoragePaths;
    }

    public async Task SaveAsync(ExecutionJournal journal, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(appStoragePaths.JournalDirectory);
        var path = Path.Combine(appStoragePaths.JournalDirectory, $"{journal.CreatedAtUtc:yyyyMMdd_HHmmss}_{journal.JournalId:N}.json");

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, journal, SerializerOptions, cancellationToken);
    }

    public async Task<ExecutionJournal?> LoadLatestAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(appStoragePaths.JournalDirectory);
        var latestFile = Directory.EnumerateFiles(appStoragePaths.JournalDirectory, "*.json")
            .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(latestFile))
        {
            return null;
        }

        await using var stream = File.OpenRead(latestFile);
        return await JsonSerializer.DeserializeAsync<ExecutionJournal>(stream, SerializerOptions, cancellationToken);
    }
}
