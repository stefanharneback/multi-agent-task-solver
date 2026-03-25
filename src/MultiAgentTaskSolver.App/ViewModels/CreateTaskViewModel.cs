using MultiAgentTaskSolver.App.Services;
using MultiAgentTaskSolver.Core.Models;

namespace MultiAgentTaskSolver.App.ViewModels;

public sealed partial class CreateTaskViewModel : ViewModelBase
{
    private readonly TaskWorkspaceCoordinator _coordinator;

    public CreateTaskViewModel(TaskWorkspaceCoordinator coordinator)
    {
        _coordinator = coordinator;
        TaskMarkdown = """
# Task

Describe the task, expected outcome, and how attached files should be referenced.
""";
    }

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string Summary { get; set; } = string.Empty;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string AdditionalInputCategories { get; set; } = string.Empty;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string TaskMarkdown { get; set; } = string.Empty;

    public async Task<string?> CreateAsync()
    {
        string? createdTaskId = null;

        await RunBusyAsync(async () =>
        {
            var snapshot = await _coordinator.CreateTaskAsync(new CreateTaskRequest
            {
                Title = Title,
                Summary = Summary,
                TaskMarkdown = TaskMarkdown,
                AdditionalInputCategories = AdditionalInputCategories
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            });

            createdTaskId = snapshot.Manifest.Id;
        });

        return createdTaskId;
    }
}
