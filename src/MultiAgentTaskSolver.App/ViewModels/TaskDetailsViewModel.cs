using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using MultiAgentTaskSolver.App.Services;
using MultiAgentTaskSolver.Core;
using MultiAgentTaskSolver.Core.Models;

namespace MultiAgentTaskSolver.App.ViewModels;

public sealed partial class TaskDetailsViewModel : ViewModelBase
{
    private readonly ITaskWorkspaceCoordinator _coordinator;
    private readonly IAppNavigationService _navigationService;
    private readonly IFilePickerService _filePickerService;

    private TaskManifest? _manifest;

    public TaskDetailsViewModel(
        ITaskWorkspaceCoordinator coordinator,
        IAppNavigationService navigationService,
        IFilePickerService filePickerService)
    {
        _coordinator = coordinator;
        _navigationService = navigationService;
        _filePickerService = filePickerService;
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        ImportPickedFilesCommand = new AsyncRelayCommand(ImportPickedFilesAsync);
        OpenRunHistoryCommand = new AsyncRelayCommand(OpenRunHistoryAsync);
        RunTaskReviewCommand = new AsyncRelayCommand(RunTaskReviewAsync);
    }

    public ObservableCollection<string> ImportDestinations { get; } = [];

    public ObservableCollection<TaskTreeEntryViewModel> TreeEntries { get; } = [];

    public ObservableCollection<ArtifactEntryViewModel> Artifacts { get; } = [];

    public ObservableCollection<ModelEntryViewModel> ReviewModels { get; } = [];

    public IAsyncRelayCommand SaveCommand { get; }

    public IAsyncRelayCommand ImportPickedFilesCommand { get; }

    public IAsyncRelayCommand OpenRunHistoryCommand { get; }

    public IAsyncRelayCommand RunTaskReviewCommand { get; }

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string TaskId { get; set; } = string.Empty;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string TaskRootPath { get; set; } = string.Empty;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string Summary { get; set; } = string.Empty;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string TaskMarkdown { get; set; } = string.Empty;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string SelectedImportDestination { get; set; } = string.Empty;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string TaskStatusText { get; set; } = string.Empty;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial ModelEntryViewModel? SelectedReviewModel { get; set; }

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string LatestReviewSummary { get; set; } = string.Empty;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string LatestReviewOutput { get; set; } = string.Empty;

    public Task LoadAsync(string taskId)
    {
        return RunBusyAsync(() => LoadCoreAsync(taskId));
    }

    public Task SaveAsync()
    {
        return RunBusyAsync(async () =>
        {
            if (_manifest is null)
            {
                return;
            }

            var updatedManifest = _manifest with
            {
                Title = Title.Trim(),
                Summary = Summary.Trim(),
            };

            await _coordinator.SaveTaskAsync(updatedManifest, TaskMarkdown);
            _manifest = updatedManifest;
        });
    }

    public Task ImportPickedFilesAsync()
    {
        return RunBusyAsync(async () =>
        {
            var filePaths = await _filePickerService.PickFilesAsync();
            if (filePaths.Count == 0)
            {
                return;
            }

            await ImportFilesCoreAsync(filePaths);
            await LoadCoreAsync(TaskId);
        });
    }

    public async Task ImportFilesAsync(IEnumerable<string> filePaths)
    {
        await RunBusyAsync(async () =>
        {
            var paths = filePaths.Where(static path => !string.IsNullOrWhiteSpace(path)).ToArray();
            if (paths.Length == 0)
            {
                return;
            }

            await ImportFilesCoreAsync(paths);
            await LoadCoreAsync(TaskId);
        });
    }

    public Task OpenRunHistoryAsync()
    {
        return RunBusyAsync(() =>
        {
            if (string.IsNullOrWhiteSpace(TaskId))
            {
                throw new InvalidOperationException("Load a task before viewing run history.");
            }

            return _navigationService.GoToRunHistoryAsync(TaskId);
        });
    }

    public Task RunTaskReviewAsync()
    {
        return RunBusyAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(TaskId))
            {
                throw new InvalidOperationException("Load a task before running review.");
            }

            if (SelectedReviewModel is null)
            {
                throw new InvalidOperationException("Select a review model before running review.");
            }

            var result = await _coordinator.RunTaskReviewAsync(
                new TaskReviewRequest
                {
                    ProviderId = "openai",
                    ModelId = SelectedReviewModel.ModelId,
                },
                TaskId);

            await LoadCoreAsync(TaskId);
            LatestReviewSummary = result.Summary;
            LatestReviewOutput = result.OutputText;
        });
    }

    private async Task LoadCoreAsync(string taskId)
    {
        var snapshot = await _coordinator.LoadTaskAsync(taskId);
        if (snapshot is null)
        {
            throw new InvalidOperationException($"Task '{taskId}' was not found.");
        }

        _manifest = snapshot.Manifest;
        TaskId = snapshot.Manifest.Id;
        TaskRootPath = snapshot.TaskRootPath;
        Title = snapshot.Manifest.Title;
        Summary = snapshot.Manifest.Summary;
        TaskMarkdown = snapshot.TaskMarkdown;
        TaskStatusText = snapshot.Manifest.Status.GetDisplayName();

        ImportDestinations.Clear();
        foreach (var destination in GetImportDestinations(snapshot.Manifest))
        {
            ImportDestinations.Add(destination);
        }

        if (ImportDestinations.Count > 0
            && !ImportDestinations.Contains(SelectedImportDestination, StringComparer.OrdinalIgnoreCase))
        {
            SelectedImportDestination = ImportDestinations[0];
        }

        TreeEntries.Clear();
        foreach (var entry in FlattenTree(snapshot.Tree, depth: 0))
        {
            TreeEntries.Add(entry);
        }

        Artifacts.Clear();
        foreach (var artifact in snapshot.Manifest.Artifacts.OrderBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase))
        {
            Artifacts.Add(new ArtifactEntryViewModel(
                artifact.Alias,
                artifact.RelativePath,
                artifact.MediaType,
                $"{artifact.SizeBytes:n0} bytes"));
        }

        ReviewModels.Clear();
        foreach (var model in await _coordinator.GetTextModelsAsync("openai"))
        {
            ReviewModels.Add(new ModelEntryViewModel(model.ModelId, model.DisplayName, model.Description));
        }

        SelectedReviewModel = SelectReviewModel(snapshot.Manifest, ReviewModels, SelectedReviewModel);
        await PopulateLatestReviewAsync(snapshot);
    }

    private Task ImportFilesCoreAsync(IEnumerable<string> filePaths)
    {
        return _coordinator.ImportArtifactsAsync(TaskId, SelectedImportDestination, filePaths);
    }

    private static string[] GetImportDestinations(TaskManifest manifest)
    {
        return manifest.InputCategories
            .Select(category => $"inputs/{category}")
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static List<TaskTreeEntryViewModel> FlattenTree(IEnumerable<TaskTreeNode> nodes, int depth)
    {
        var flattened = new List<TaskTreeEntryViewModel>();

        foreach (var node in nodes)
        {
            flattened.Add(new TaskTreeEntryViewModel(
                node.RelativePath,
                $"{new string(' ', depth * 2)}{node.Name}",
                node.IsDirectory));

            if (node.Children.Count > 0)
            {
                flattened.AddRange(FlattenTree(node.Children, depth + 1));
            }
        }

        return flattened;
    }

    private static ModelEntryViewModel? SelectReviewModel(
        TaskManifest manifest,
        IEnumerable<ModelEntryViewModel> reviewModels,
        ModelEntryViewModel? currentSelection)
    {
        var latestReviewModelId = manifest.Runs
            .Where(static run => run.Kind == TaskRunKind.TaskReview)
            .OrderByDescending(static run => run.Sequence)
            .SelectMany(static run => run.Steps)
            .Select(static step => step.Model.ModelId)
            .FirstOrDefault(static modelId => !string.IsNullOrWhiteSpace(modelId));

        if (!string.IsNullOrWhiteSpace(latestReviewModelId))
        {
            return reviewModels.FirstOrDefault(model => string.Equals(model.ModelId, latestReviewModelId, StringComparison.OrdinalIgnoreCase));
        }

        if (currentSelection is not null
            && reviewModels.Any(model => string.Equals(model.ModelId, currentSelection.ModelId, StringComparison.OrdinalIgnoreCase)))
        {
            return currentSelection;
        }

        return reviewModels.FirstOrDefault();
    }

    private async Task PopulateLatestReviewAsync(TaskWorkspaceSnapshot snapshot)
    {
        var latestReview = snapshot.Manifest.Runs
            .Where(static run => run.Kind == TaskRunKind.TaskReview)
            .OrderByDescending(static run => run.Sequence)
            .FirstOrDefault();

        LatestReviewSummary = latestReview?.Summary ?? string.Empty;
        LatestReviewOutput = await LoadLatestReviewOutputAsync(snapshot, latestReview);
    }

    private static async Task<string> LoadLatestReviewOutputAsync(TaskWorkspaceSnapshot snapshot, RunManifest? latestReview)
    {
        var latestStep = latestReview?.Steps
            .OrderByDescending(static step => step.Attempt)
            .FirstOrDefault();

        if (latestStep is null || string.IsNullOrWhiteSpace(latestStep.RelativeDirectory))
        {
            return latestReview?.Summary ?? string.Empty;
        }

        var responsePath = Path.Combine(
            snapshot.TaskRootPath,
            latestStep.RelativeDirectory.Replace('/', Path.DirectorySeparatorChar),
            latestStep.ResponsePath);

        if (!File.Exists(responsePath))
        {
            return latestReview?.Summary ?? string.Empty;
        }

        var responseMarkdown = await File.ReadAllTextAsync(responsePath);
        if (string.IsNullOrWhiteSpace(responseMarkdown))
        {
            return string.Empty;
        }

        const string outputHeading = "# Output";
        const string rawResponseHeading = "\n## Raw Response";
        var outputStart = responseMarkdown.IndexOf(outputHeading, StringComparison.Ordinal);
        if (outputStart < 0)
        {
            return responseMarkdown.Trim();
        }

        var contentStart = outputStart + outputHeading.Length;
        var rawResponseStart = responseMarkdown.IndexOf(rawResponseHeading, contentStart, StringComparison.Ordinal);
        var outputText = rawResponseStart >= 0
            ? responseMarkdown[contentStart..rawResponseStart]
            : responseMarkdown[contentStart..];

        var normalizedOutput = outputText.Trim();
        return string.Equals(normalizedOutput, "_No output text returned._", StringComparison.Ordinal)
            ? string.Empty
            : normalizedOutput;
    }
}
