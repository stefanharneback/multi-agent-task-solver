using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using MultiAgentTaskSolver.App.Services;
using MultiAgentTaskSolver.Core.Models;

namespace MultiAgentTaskSolver.App.ViewModels;

public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly ITaskWorkspaceCoordinator _coordinator;
    private readonly IFolderPickerService _folderPickerService;

    public SettingsViewModel(ITaskWorkspaceCoordinator coordinator, IFolderPickerService folderPickerService)
    {
        _coordinator = coordinator;
        _folderPickerService = folderPickerService;
        BrowseWorkspaceFolderCommand = new AsyncRelayCommand(BrowseWorkspaceFolderAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
    }

    public ObservableCollection<ModelEntryViewModel> OpenAiModels { get; } = [];

    public IAsyncRelayCommand BrowseWorkspaceFolderCommand { get; }

    public IAsyncRelayCommand SaveCommand { get; }

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string WorkspaceRootPath { get; set; } = string.Empty;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string OpenAiGatewayBaseUrl { get; set; } = string.Empty;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string OpenAiBearerToken { get; set; } = string.Empty;

    public Task BrowseWorkspaceFolderAsync()
    {
        return RunBusyAsync(async () =>
        {
            var selectedPath = await _folderPickerService.PickFolderAsync();
            if (!string.IsNullOrWhiteSpace(selectedPath))
            {
                WorkspaceRootPath = selectedPath;
            }
        });
    }

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
