using CommunityToolkit.Mvvm.Input;
using MultiAgentTaskSolver.App.Services;
using MultiAgentTaskSolver.Core.Models;

namespace MultiAgentTaskSolver.App.ViewModels;

public sealed partial class CreateTaskViewModel : ViewModelBase
{
    private readonly ITaskWorkspaceCoordinator _coordinator;
    private readonly IAppNavigationService _navigationService;

    public CreateTaskViewModel(ITaskWorkspaceCoordinator coordinator, IAppNavigationService navigationService)
    {
        _coordinator = coordinator;
        _navigationService = navigationService;
        CreateCommand = new AsyncRelayCommand(CreateAsync);
        CancelCommand = new AsyncRelayCommand(CancelAsync);
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

    public IAsyncRelayCommand CreateCommand { get; }

    public IAsyncRelayCommand CancelCommand { get; }

    public Task CreateAsync()
    {
        return RunBusyAsync(async () =>
        {
            var snapshot = await _coordinator.CreateTaskAsync(new CreateTaskRequest
            {
                Title = Title,
                Summary = Summary,
                TaskMarkdown = TaskMarkdown,
                AdditionalInputCategories = AdditionalInputCategories
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            });

            await _navigationService.GoToTaskDetailsAsync(snapshot.Manifest.Id, replaceCurrentPage: true);
        });
    }

    public Task CancelAsync()
    {
        return RunBusyAsync(() => _navigationService.GoBackAsync());
    }
}
