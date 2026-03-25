using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using MultiAgentTaskSolver.App.Pages;
using MultiAgentTaskSolver.App.Services;
using MultiAgentTaskSolver.App.ViewModels;
using MultiAgentTaskSolver.Core.Abstractions;
using MultiAgentTaskSolver.Infrastructure.Configuration;
using MultiAgentTaskSolver.Infrastructure.FileSystem;
using MultiAgentTaskSolver.Infrastructure.Gateway;

namespace MultiAgentTaskSolver.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddSingleton<IAppSettingsStore, JsonAppSettingsStore>();
        builder.Services.AddSingleton<ISecretStore, SecureStorageSecretStore>();
        builder.Services.AddSingleton<ITaskWorkspaceStore, FileSystemTaskWorkspaceStore>();
        builder.Services.AddSingleton<IModelCatalog>(_ =>
            new JsonModelCatalog(Path.Combine(AppContext.BaseDirectory, "config", "providers")));
        builder.Services.AddSingleton<IUsageNormalizer, OpenAiUsageNormalizer>();
        builder.Services.AddHttpClient<OpenAiGatewayAdapter>().AddStandardResilienceHandler();
        builder.Services.AddTransient<IProviderAdapter>(serviceProvider => serviceProvider.GetRequiredService<OpenAiGatewayAdapter>());

        builder.Services.AddSingleton<TaskWorkspaceCoordinator>();

        builder.Services.AddSingleton<TaskListViewModel>();
        builder.Services.AddSingleton<SettingsViewModel>();
        builder.Services.AddTransient<CreateTaskViewModel>();
        builder.Services.AddTransient<TaskDetailsViewModel>();
        builder.Services.AddTransient<RunHistoryViewModel>();

        builder.Services.AddSingleton<TaskListPage>();
        builder.Services.AddSingleton<SettingsPage>();
        builder.Services.AddTransient<CreateTaskPage>();
        builder.Services.AddTransient<TaskDetailsPage>();
        builder.Services.AddTransient<RunHistoryPage>();

        builder.Services.AddSingleton<AppShell>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
