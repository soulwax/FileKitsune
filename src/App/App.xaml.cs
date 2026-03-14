using System.Windows;
using FileTransformer.App.Services;
using FileTransformer.App.ViewModels;
using FileTransformer.App.Views;
using FileTransformer.Application.Services;
using FileTransformer.Infrastructure;
using FileTransformer.Infrastructure.Configuration;
using FileTransformer.Infrastructure.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace FileTransformer.App;

public partial class App : System.Windows.Application
{
    private IHost? host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var builder = Host.CreateApplicationBuilder();
        var uiLogStore = new UiLogStore();
        var storagePaths = new AppStoragePaths();

        LoggingConfiguration.ConfigureSerilog(storagePaths);

        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(Log.Logger, dispose: true);
        builder.Logging.AddProvider(new UiLoggerProvider(uiLogStore));

        builder.Services.AddSingleton(uiLogStore);
        builder.Services.AddFileTransformerInfrastructure();
        builder.Services.AddSingleton<PathSafetyService>();
        builder.Services.AddSingleton<DateResolutionService>();
        builder.Services.AddSingleton<DuplicateDetectionService>();
        builder.Services.AddSingleton<ProtectionPolicyService>();
        builder.Services.AddSingleton<NamingPolicyService>();
        builder.Services.AddSingleton<ReviewDecisionService>();
        builder.Services.AddSingleton<DestinationPathBuilder>();
        builder.Services.AddSingleton<HeuristicSemanticClassifier>();
        builder.Services.AddSingleton<SemanticClassifierCoordinator>();
        builder.Services.AddSingleton<OrganizationWorkflowService>();
        builder.Services.AddSingleton<PlanExecutionService>();
        builder.Services.AddSingleton<RollbackService>();
        builder.Services.AddSingleton<IFolderPickerService, FolderPickerService>();
        builder.Services.AddSingleton<IDialogService, DialogService>();
        builder.Services.AddSingleton<MainWindowViewModel>();
        builder.Services.AddSingleton<MainWindow>();

        host = builder.Build();
        await host.StartAsync();

        var mainWindow = host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (host is not null)
        {
            await host.StopAsync();
            host.Dispose();
        }

        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
