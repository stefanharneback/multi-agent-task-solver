using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using MultiAgentTaskSolver.App.Services;

namespace MultiAgentTaskSolver.App.ViewModels;

public sealed partial class TaskListViewModel : ViewModelBase
{
    private readonly ITaskWorkspaceCoordinator _coordinator;
    private readonly IAppNavigationService _navigationService;

    public TaskListViewModel(ITaskWorkspaceCoordinator coordinator, IAppNavigationService navigationService)
    {
        _coordinator = coordinator;
        _navigationService = navigationService;
        OpenCreateTaskCommand = new AsyncRelayCommand(OpenCreateTaskAsync);
    }

    public ObservableCollection<TaskListItemViewModel> Tasks { get; } = [];

    public IAsyncRelayCommand OpenCreateTaskCommand { get; }

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string WorkspaceRootPath { get; set; } = string.Empty;

    public Task OpenCreateTaskAsync()
    {
        return RunBusyAsync(() => _navigationService.GoToCreateTaskAsync());
    }

    public Task OpenTaskAsync(string taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId))
        {
            return Task.CompletedTask;
        }

        return RunBusyAsync(() => _navigationService.GoToTaskDetailsAsync(taskId));
    }

    public Task LoadAsync()
    {
        return RunBusyAsync(async () =>
        {
            var settings = await _coordinator.GetSettingsAsync();
            WorkspaceRootPath = settings.WorkspaceRootPath;

            var manifests = await _coordinator.ListTasksAsync();
            Tasks.Clear();

            foreach (var manifest in manifests)
            {
                Tasks.Add(new TaskListItemViewModel(
                    manifest.Id,
                    manifest.Title,
                    manifest.Summary,
                    manifest.UpdatedAtUtc.LocalDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)));
            }
        });
    }
}
