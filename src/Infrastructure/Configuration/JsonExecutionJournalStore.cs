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
        var path = GetJournalPath(journal);
        var tmpPath = path + ".tmp";

        await using (var stream = File.Create(tmpPath))
        {
            await JsonSerializer.SerializeAsync(stream, journal, SerializerOptions, cancellationToken);
        }

        File.Move(tmpPath, path, overwrite: true);
    }

    public async Task<ExecutionJournal?> LoadLatestAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(appStoragePaths.JournalDirectory);
        var latestFile = Directory.EnumerateFiles(appStoragePaths.JournalDirectory, "*.json")
            .OrderByDescending(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(latestFile))
        {
            return null;
        }

        return await LoadFromPathAsync(latestFile, cancellationToken);
    }

    public async Task<ExecutionJournal?> LoadAsync(Guid journalId, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(appStoragePaths.JournalDirectory);
        var journalFile = Directory.EnumerateFiles(appStoragePaths.JournalDirectory, $"*_{journalId:N}.json")
            .OrderByDescending(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        return string.IsNullOrWhiteSpace(journalFile)
            ? null
            : await LoadFromPathAsync(journalFile, cancellationToken);
    }

    public async Task<IReadOnlyList<ExecutionJournal>> LoadAllAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(appStoragePaths.JournalDirectory);

        var files = Directory.EnumerateFiles(appStoragePaths.JournalDirectory, "*.json")
            .OrderByDescending(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .ToList();

        var journals = new List<ExecutionJournal>(files.Count);
        foreach (var file in files)
        {
            var journal = await LoadFromPathAsync(file, cancellationToken);
            if (journal is not null)
            {
                journals.Add(journal);
            }
        }

        return journals;
    }

    private string GetJournalPath(ExecutionJournal journal) =>
        Path.Combine(appStoragePaths.JournalDirectory, $"{journal.CreatedAtUtc:yyyyMMdd_HHmmss}_{journal.JournalId:N}.json");

    private async Task<ExecutionJournal?> LoadFromPathAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<ExecutionJournal>(stream, SerializerOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
