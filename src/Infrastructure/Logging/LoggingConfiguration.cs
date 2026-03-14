using System.Text;
using FileTransformer.Infrastructure.Configuration;
using Serilog;
using Serilog.Formatting.Compact;

namespace FileTransformer.Infrastructure.Logging;

public static class LoggingConfiguration
{
    public static void ConfigureSerilog(AppStoragePaths appStoragePaths)
    {
        Directory.CreateDirectory(appStoragePaths.LogsDirectory);

        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .MinimumLevel.Information()
            .WriteTo.File(
                formatter: new RenderedCompactJsonFormatter(),
                path: Path.Combine(appStoragePaths.LogsDirectory, "app-.json"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true,
                encoding: Encoding.UTF8)
            .CreateLogger();
    }
}
