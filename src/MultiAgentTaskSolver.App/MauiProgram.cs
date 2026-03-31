using CommunityToolkit.Maui;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using MultiAgentTaskSolver.App.Pages;
using MultiAgentTaskSolver.App.Services;
using MultiAgentTaskSolver.App.ViewModels;
using MultiAgentTaskSolver.Core.Abstractions;
using MultiAgentTaskSolver.Infrastructure.Configuration;
using MultiAgentTaskSolver.Infrastructure.Execution;
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
        builder.Services.AddSingleton<IUsageNormalizer, OpenAiUsageNormalizer>();
        builder.Services.AddSingleton<IArtifactReferenceResolver, ArtifactReferenceResolver>();
        builder.Services.AddSingleton<IReviewPromptFactory, ReviewPromptFactory>();
        builder.Services.AddSingleton<IWorkerPromptFactory, WorkerPromptFactory>();
        builder.Services.AddHttpClient<OpenAiGatewayAdapter>(static client =>
            {
                client.Timeout = TimeSpan.FromMinutes(11);
            })
            .AddStandardResilienceHandler(static options =>
            {
                options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(10);
                options.AttemptTimeout.Timeout = TimeSpan.FromMinutes(10);
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(20);
                options.Retry.DisableForUnsafeHttpMethods();
            });
        builder.Services.AddTransient<IProviderAdapter>(serviceProvider => serviceProvider.GetRequiredService<OpenAiGatewayAdapter>());
        builder.Services.AddSingleton<IModelCatalog>(serviceProvider =>
            new GatewayBackedModelCatalog(
                Path.Combine(AppContext.BaseDirectory, "config", "providers"),
                serviceProvider.GetRequiredService<IAppSettingsStore>(),
                serviceProvider.GetRequiredService<ISecretStore>(),
                serviceProvider.GetRequiredService<IProviderAdapter>()));
        builder.Services.AddSingleton<ITaskReviewWorkflow, TaskReviewWorkflow>();
        builder.Services.AddSingleton<ITaskWorkerWorkflow, TaskWorkerWorkflow>();
        builder.Services.AddSingleton<IUserDecisionWorkflow, UserDecisionWorkflow>();
        builder.Services.AddSingleton<IAppNavigationService, ShellNavigationService>();
        builder.Services.AddSingleton<IFilePickerService, MauiFilePickerService>();
        builder.Services.AddSingleton<IFolderPickerService, MauiFolderPickerService>();

        builder.Services.AddSingleton<ITaskWorkspaceCoordinator, TaskWorkspaceCoordinator>();

        builder.Services.AddSingleton<TaskListViewModel>();
        builder.Services.AddSingleton<SettingsViewModel>();
        builder.Services.AddTransient<CreateTaskViewModel>();
        builder.Services.AddTransient<TaskDetailsViewModel>();
        builder.Services.AddTransient<RunHistoryViewModel>();

        builder.Services.AddSingleton<TaskWorkspaceHomeView>();
        builder.Services.AddSingleton<SettingsHomeView>();
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
