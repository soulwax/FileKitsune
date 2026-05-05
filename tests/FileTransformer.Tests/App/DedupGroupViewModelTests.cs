using FileTransformer.App.ViewModels;
using Xunit;

namespace FileTransformer.Tests.App;

public sealed class DedupGroupViewModelTests
{
    [Fact]
    public void SetKeeper_MarksSelectedFileAndClearsOtherKeepers()
    {
        var first = new DedupFileItemViewModel
        {
            FullPath = @"C:\Root\a.txt",
            RelativePath = "a.txt",
            SizeBytes = 100,
            ModifiedUtc = DateTimeOffset.UtcNow,
            IsKeeper = true
        };
        var second = new DedupFileItemViewModel
        {
            FullPath = @"C:\Root\b.txt",
            RelativePath = "b.txt",
            SizeBytes = 100,
            ModifiedUtc = DateTimeOffset.UtcNow
        };
        var third = new DedupFileItemViewModel
        {
            FullPath = @"C:\Root\c.txt",
            RelativePath = "c.txt",
            SizeBytes = 100,
            ModifiedUtc = DateTimeOffset.UtcNow
        };
        var group = new DedupGroupViewModel([first, second, third]);

        group.MarkResolved();
        group.SetKeeper(second);

        Assert.False(first.IsKeeper);
        Assert.True(second.IsKeeper);
        Assert.False(third.IsKeeper);
        Assert.False(group.IsResolved);
        Assert.False(group.IsSkipped);
        Assert.Same(second, group.Keeper);
        Assert.Equal(200, group.WastedBytes);
    }
}
