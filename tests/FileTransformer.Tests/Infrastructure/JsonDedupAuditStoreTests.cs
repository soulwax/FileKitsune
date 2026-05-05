using FileTransformer.Application.Models;
using FileTransformer.Infrastructure.Configuration;
using Xunit;

namespace FileTransformer.Tests.Infrastructure;

public sealed class JsonDedupAuditStoreTests
{
    [Fact]
    public async Task Store_WritesStartedEntryAndAppendsResultEntries()
    {
        var root = Path.Combine(Path.GetTempPath(), $"FileKitsuneDedupAudit_{Guid.NewGuid():N}");
        try
        {
            var store = new JsonDedupAuditStore(new AppStoragePaths(root));
            var runId = Guid.NewGuid();

            var handle = await store.StartRunAsync(
                new DedupAuditRunStarted
                {
                    RunId = runId,
                    RootDirectory = @"C:\Root",
                    TotalGroups = 1,
                    ResolvedGroups = 1,
                    FilesPlannedForRecycle = 1,
                    FilesPlannedForQuarantine = 1,
                    BytesPlannedForRecycle = 128,
                    BytesPlannedForQuarantine = 128,
                    Groups =
                    [
                        new DedupAuditGroup
                        {
                            KeeperFullPath = @"C:\Root\keeper.txt",
                            KeeperRelativePath = "keeper.txt",
                            DuplicateFullPaths = [@"C:\Root\copy.txt"],
                            DuplicateRelativePaths = ["copy.txt"]
                        }
                    ]
                },
                CancellationToken.None);

            await store.AppendEntryAsync(
                runId,
                new DedupAuditEntry
                {
                    Action = "quarantine-attempt",
                    FullPath = @"C:\Root\copy.txt",
                    RelativePath = "copy.txt",
                    SizeBytes = 128,
                    QuarantinePath = @"C:\AppData\FileKitsune\quarantine\run\copy.txt",
                    Status = "Pending"
                },
                CancellationToken.None);

            await store.CompleteRunAsync(
                runId,
                new DedupAuditRunCompleted
                {
                    Status = "Completed",
                    FilesRecycled = 1,
                    FilesQuarantined = 1,
                    BytesFreed = 128,
                    BytesQuarantined = 128
                },
                CancellationToken.None);

            var lines = await File.ReadAllLinesAsync(handle.AuditFilePath);

            Assert.Equal(3, lines.Length);
            Assert.Contains("\"Event\":\"run-started\"", lines[0]);
            Assert.Contains("\"KeeperRelativePath\":\"keeper.txt\"", lines[0]);
            Assert.Contains("\"FilesPlannedForQuarantine\":1", lines[0]);
            Assert.Contains("\"Event\":\"entry\"", lines[1]);
            Assert.Contains("\"Action\":\"quarantine-attempt\"", lines[1]);
            Assert.Contains("\"QuarantinePath\":\"C:\\\\AppData\\\\FileKitsune\\\\quarantine\\\\run\\\\copy.txt\"", lines[1]);
            Assert.Contains("\"Event\":\"run-completed\"", lines[2]);
            Assert.Contains("\"Status\":\"Completed\"", lines[2]);
            Assert.Contains("\"FilesQuarantined\":1", lines[2]);
            Assert.Contains("\"BytesQuarantined\":128", lines[2]);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
