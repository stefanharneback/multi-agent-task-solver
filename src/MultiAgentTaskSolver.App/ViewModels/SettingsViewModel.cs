using System.Collections.ObjectModel;
using MultiAgentTaskSolver.App.Services;
using MultiAgentTaskSolver.Core.Models;

namespace MultiAgentTaskSolver.App.ViewModels;

public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly TaskWorkspaceCoordinator _coordinator;

    public SettingsViewModel(TaskWorkspaceCoordinator coordinator)
    {
        _coordinator = coordinator;
    }

    public ObservableCollection<ModelEntryViewModel> OpenAiModels { get; } = [];

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string WorkspaceRootPath { get; set; } = string.Empty;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string OpenAiGatewayBaseUrl { get; set; } = string.Empty;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string OpenAiBearerToken { get; set; } = string.Empty;

    public Task LoadAsync()
    {
        return RunBusyAsync(async () =>
        {
            var settings = await _coordinator.GetSettingsAsync();
            WorkspaceRootPath = settings.WorkspaceRootPath;
            OpenAiGatewayBaseUrl = settings.OpenAiGatewayBaseUrl;
            OpenAiBearerToken = await _coordinator.GetOpenAiBearerTokenAsync() ?? string.Empty;

            OpenAiModels.Clear();
            foreach (var model in await _coordinator.GetModelsAsync("openai"))
            {
                OpenAiModels.Add(new ModelEntryViewModel(model.ModelId, model.DisplayName, model.Description));
            }
        });
    }

    public Task SaveAsync()
    {
        return RunBusyAsync(async () =>
        {
            var settings = new AppSettings
            {
                WorkspaceRootPath = WorkspaceRootPath.Trim(),
                OpenAiGatewayBaseUrl = OpenAiGatewayBaseUrl.Trim(),
                DefaultProviderId = "openai",
            };

            await _coordinator.SaveSettingsAsync(settings, OpenAiBearerToken);
        });
    }
}
