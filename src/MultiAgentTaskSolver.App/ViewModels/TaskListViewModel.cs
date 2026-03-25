using System.Collections.ObjectModel;
using System.Globalization;
using MultiAgentTaskSolver.App.Services;

namespace MultiAgentTaskSolver.App.ViewModels;

public sealed partial class TaskListViewModel : ViewModelBase
{
    private readonly TaskWorkspaceCoordinator _coordinator;

    public TaskListViewModel(TaskWorkspaceCoordinator coordinator)
    {
        _coordinator = coordinator;
    }

    public ObservableCollection<TaskListItemViewModel> Tasks { get; } = [];

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string WorkspaceRootPath { get; set; } = string.Empty;

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
