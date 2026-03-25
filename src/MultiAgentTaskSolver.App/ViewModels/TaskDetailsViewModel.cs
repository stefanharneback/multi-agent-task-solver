using System.Collections.ObjectModel;
using MultiAgentTaskSolver.App.Services;
using MultiAgentTaskSolver.Core.Models;

namespace MultiAgentTaskSolver.App.ViewModels;

public sealed partial class TaskDetailsViewModel : ViewModelBase
{
    private readonly TaskWorkspaceCoordinator _coordinator;

    private TaskManifest? _manifest;

    public TaskDetailsViewModel(TaskWorkspaceCoordinator coordinator)
    {
        _coordinator = coordinator;
    }

    public ObservableCollection<string> ImportDestinations { get; } = [];

    public ObservableCollection<TaskTreeEntryViewModel> TreeEntries { get; } = [];

    public ObservableCollection<ArtifactEntryViewModel> Artifacts { get; } = [];

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

    public Task LoadAsync(string taskId)
    {
        return RunBusyAsync(async () =>
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

            ImportDestinations.Clear();
            foreach (var destination in GetImportDestinations(snapshot.Manifest))
            {
                ImportDestinations.Add(destination);
            }

            if (ImportDestinations.Count > 0)
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
        });
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

    public async Task ImportFilesAsync(IEnumerable<string> filePaths)
    {
        await RunBusyAsync(async () =>
        {
            var paths = filePaths.Where(static path => !string.IsNullOrWhiteSpace(path)).ToArray();
            if (paths.Length == 0)
            {
                return;
            }

            await _coordinator.ImportArtifactsAsync(TaskId, SelectedImportDestination, paths);
            await LoadAsync(TaskId);
        });
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
}
