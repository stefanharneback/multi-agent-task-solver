using System.Collections.ObjectModel;
using System.Globalization;
using MultiAgentTaskSolver.App.Services;

namespace MultiAgentTaskSolver.App.ViewModels;

public sealed partial class RunHistoryViewModel : ViewModelBase
{
    private readonly TaskWorkspaceCoordinator _coordinator;

    public RunHistoryViewModel(TaskWorkspaceCoordinator coordinator)
    {
        _coordinator = coordinator;
    }

    public ObservableCollection<RunEntryViewModel> Runs { get; } = [];

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string TaskId { get; set; } = string.Empty;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string TaskTitle { get; set; } = string.Empty;

    public Task LoadAsync(string taskId)
    {
        return RunBusyAsync(async () =>
        {
            var snapshot = await _coordinator.LoadTaskAsync(taskId);
            if (snapshot is null)
            {
                throw new InvalidOperationException($"Task '{taskId}' was not found.");
            }

            TaskId = snapshot.Manifest.Id;
            TaskTitle = snapshot.Manifest.Title;

            Runs.Clear();
            foreach (var run in snapshot.Manifest.Runs.OrderByDescending(static run => run.Sequence))
            {
                Runs.Add(new RunEntryViewModel(
                    run.Id,
                    string.IsNullOrWhiteSpace(run.Title) ? run.Kind : run.Title,
                    run.Status,
                    run.StartedAtUtc.LocalDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                    $"{run.Steps.Count} step(s)"));
            }
        });
    }
}
