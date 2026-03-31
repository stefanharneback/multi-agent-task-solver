using CommunityToolkit.Mvvm.Input;
using MultiAgentTaskSolver.App.Services;
using MultiAgentTaskSolver.Core;
using MultiAgentTaskSolver.Core.Models;

namespace MultiAgentTaskSolver.App.ViewModels;

public sealed partial class CreateTaskViewModel : ViewModelBase
{
    private readonly ITaskWorkspaceCoordinator _coordinator;
    private readonly IAppNavigationService _navigationService;
    private readonly IFolderPickerService _folderPickerService;

    public CreateTaskViewModel(ITaskWorkspaceCoordinator coordinator, IAppNavigationService navigationService)
        : this(coordinator, navigationService, new NullFolderPickerService())
    {
    }

    public CreateTaskViewModel(
        ITaskWorkspaceCoordinator coordinator,
        IAppNavigationService navigationService,
        IFolderPickerService folderPickerService)
    {
        _coordinator = coordinator;
        _navigationService = navigationService;
        _folderPickerService = folderPickerService;
        CreateCommand = new AsyncRelayCommand(CreateAsync);
        CancelCommand = new AsyncRelayCommand(CancelAsync);
        AddInputFolderCommand = new AsyncRelayCommand(AddInputFolderAsync);
    }

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string Summary { get; set; } = string.Empty;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string InputPathsText { get; set; } = string.Empty;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string OutputPathsText { get; set; } = string.Empty;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string TaskMarkdown { get; set; } = string.Empty;

    public IAsyncRelayCommand CreateCommand { get; }

    public IAsyncRelayCommand CancelCommand { get; }

    public IAsyncRelayCommand AddInputFolderCommand { get; }

    public Task CreateAsync()
    {
        return RunBusyAsync(async () =>
        {
            var snapshot = await _coordinator.CreateTaskAsync(new CreateTaskRequest
            {
                Title = Title,
                Summary = Summary,
                TaskMarkdown = TaskMarkdown,
                InputPaths = TaskFolderConventions.ParseInputPaths(InputPathsText),
                OutputPaths = TaskFolderConventions.ParseOutputPaths(OutputPathsText),
            });

            await _navigationService.GoToTaskDetailsAsync(snapshot.Manifest.Id, replaceCurrentPage: true);
        });
    }

    public Task CancelAsync()
    {
        return RunBusyAsync(() => _navigationService.GoBackAsync());
    }

    public Task AddInputFolderAsync()
    {
        return RunBusyAsync(async () =>
        {
            var selectedFolderPath = await _folderPickerService.PickFolderAsync();
            if (string.IsNullOrWhiteSpace(selectedFolderPath))
            {
                return;
            }

            var folderName = Path.GetFileName(Path.TrimEndingDirectorySeparator(selectedFolderPath));
            if (string.IsNullOrWhiteSpace(folderName))
            {
                return;
            }

            InputPathsText = AppendDeclaredPath(InputPathsText, TaskFolderConventions.NormalizeInputPath(folderName));
        });
    }

    private static string AppendDeclaredPath(string existingValue, string path)
    {
        var paths = string.IsNullOrWhiteSpace(existingValue)
            ? []
            : existingValue
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

        if (!paths.Contains(path, StringComparer.OrdinalIgnoreCase))
        {
            paths.Add(path);
        }

        return string.Join(Environment.NewLine, paths);
    }

    private sealed class NullFolderPickerService : IFolderPickerService
    {
        public Task<string?> PickFolderAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>(null);
        }
    }
}
