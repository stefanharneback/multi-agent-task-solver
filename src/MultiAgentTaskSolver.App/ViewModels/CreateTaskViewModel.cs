using System.Collections.ObjectModel;
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
        AddInputFolderByNameCommand = new RelayCommand<string>(AddInputFolderByName);
        RemoveInputFolderCommand = new RelayCommand<string>(RemoveInputFolder);
        AddOutputTargetCommand = new RelayCommand<string>(AddOutputTarget);
        RemoveOutputTargetCommand = new RelayCommand<string>(RemoveOutputTarget);
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

    public ObservableCollection<string> InputFolders { get; } = [];

    public ObservableCollection<OutputTargetViewModel> OutputTargets { get; } = [];

    public IAsyncRelayCommand CreateCommand { get; }

    public IAsyncRelayCommand CancelCommand { get; }

    public IAsyncRelayCommand AddInputFolderCommand { get; }

    public IRelayCommand<string> AddInputFolderByNameCommand { get; }

    public IRelayCommand<string> RemoveInputFolderCommand { get; }

    public IRelayCommand<string> AddOutputTargetCommand { get; }

    public IRelayCommand<string> RemoveOutputTargetCommand { get; }

    public Task CreateAsync()
    {
        return RunBusyAsync(async () =>
        {
            var inputPaths = InputFolders.Count > 0
                ? InputFolders.Select(TaskFolderConventions.NormalizeInputPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
                : TaskFolderConventions.ParseInputPaths(InputPathsText);

            var outputTargetsList = OutputTargets.Count > 0
                ? OutputTargets.ToArray()
                : TaskFolderConventions.ParseOutputPaths(OutputPathsText)
                    .Select(static path => new OutputTargetViewModel(path, string.Empty))
                    .ToArray();

            var outputPaths = outputTargetsList.Select(static t => t.Path).ToArray();
            var outputDescriptions = outputTargetsList
                .Where(static t => !string.IsNullOrWhiteSpace(t.Description))
                .ToDictionary(static t => t.Path, static t => t.Description, StringComparer.OrdinalIgnoreCase);

            var snapshot = await _coordinator.CreateTaskAsync(new CreateTaskRequest
            {
                Title = Title,
                Summary = Summary,
                TaskMarkdown = TaskMarkdown,
                InputPaths = inputPaths,
                OutputPaths = outputPaths,
                OutputPathDescriptions = outputDescriptions,
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

            var normalizedPath = TaskFolderConventions.NormalizeInputPath(folderName);
            if (!InputFolders.Contains(normalizedPath, StringComparer.OrdinalIgnoreCase))
            {
                InputFolders.Add(normalizedPath);
            }

            InputPathsText = string.Join(Environment.NewLine, InputFolders);
        });
    }

    public void AddInputFolderByName(string? folderName)
    {
        if (string.IsNullOrWhiteSpace(folderName))
        {
            return;
        }

        var normalizedPath = TaskFolderConventions.NormalizeInputPath(folderName.Trim());
        if (!InputFolders.Contains(normalizedPath, StringComparer.OrdinalIgnoreCase))
        {
            InputFolders.Add(normalizedPath);
        }

        InputPathsText = string.Join(Environment.NewLine, InputFolders);
    }

    public void RemoveInputFolder(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var match = InputFolders.FirstOrDefault(f => string.Equals(f, path, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            InputFolders.Remove(match);
        }

        InputPathsText = string.Join(Environment.NewLine, InputFolders);
    }

    public void AddOutputTarget(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var normalizedPath = TaskFolderConventions.NormalizeOutputPath(path.Trim());
        if (OutputTargets.All(t => !string.Equals(t.Path, normalizedPath, StringComparison.OrdinalIgnoreCase)))
        {
            OutputTargets.Add(new OutputTargetViewModel(normalizedPath, string.Empty));
        }

        OutputPathsText = string.Join(Environment.NewLine, OutputTargets.Select(static t => t.Path));
    }

    public void RemoveOutputTarget(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var match = OutputTargets.FirstOrDefault(t => string.Equals(t.Path, path, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            OutputTargets.Remove(match);
        }

        OutputPathsText = string.Join(Environment.NewLine, OutputTargets.Select(static t => t.Path));
    }

    private sealed class NullFolderPickerService : IFolderPickerService
    {
        public Task<string?> PickFolderAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>(null);
        }
    }
}
