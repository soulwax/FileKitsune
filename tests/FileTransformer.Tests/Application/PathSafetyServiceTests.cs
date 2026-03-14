using FileTransformer.Application.Services;
using Xunit;

namespace FileTransformer.Tests.Application;

public sealed class PathSafetyServiceTests
{
    [Fact]
    public void ValidateDestination_RejectsTraversal()
    {
        var service = new PathSafetyService();

        var result = service.ValidateDestination(@"C:\OrganizeRoot", @"..\outside\file.txt");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("traversal", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateDestination_AllowsSafeRelativePath()
    {
        var service = new PathSafetyService();

        var result = service.ValidateDestination(@"C:\OrganizeRoot", @"Invoices\2025\invoice-001.pdf");

        Assert.True(result.IsValid);
        Assert.Equal($@"Invoices{Path.DirectorySeparatorChar}2025{Path.DirectorySeparatorChar}invoice-001.pdf", result.NormalizedRelativePath);
    }
}
