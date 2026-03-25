using System.Security.Cryptography;
using System.Text.Json;
using MultiAgentTaskSolver.Core;
using MultiAgentTaskSolver.Core.Abstractions;
using MultiAgentTaskSolver.Core.Models;
using MultiAgentTaskSolver.Infrastructure.Serialization;

namespace MultiAgentTaskSolver.Infrastructure.FileSystem;

public sealed class FileSystemTaskWorkspaceStore : ITaskWorkspaceStore
{
    public async Task<TaskWorkspaceSnapshot> CreateTaskAsync(
        string workspaceRootPath,
        CreateTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Title);

        Directory.CreateDirectory(workspaceRootPath);

        var createdAtUtc = DateTimeOffset.UtcNow;
        var taskId = TaskFolderConventions.CreateTaskId(createdAtUtc);
        var folderName = TaskFolderConventions.CreateTaskFolderName(taskId);
        var taskRootPath = Path.Combine(workspaceRootPath, folderName);

        Directory.CreateDirectory(taskRootPath);
        Directory.CreateDirectory(Path.Combine(taskRootPath, TaskFolderConventions.InputsFolderName));
        Directory.CreateDirectory(Path.Combine(taskRootPath, TaskFolderConventions.RunsFolderName));
        Directory.CreateDirectory(Path.Combine(taskRootPath, TaskFolderConventions.OutputsFolderName));
        Directory.CreateDirectory(Path.Combine(taskRootPath, TaskFolderConventions.CacheFolderName));

        var categories = TaskFolderConventions.DefaultInputCategories
            .Concat(request.AdditionalInputCategories ?? [])
            .Select(TaskFolderConventions.Slugify)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var category in categories)
        {
            Directory.CreateDirectory(Path.Combine(taskRootPath, TaskFolderConventions.InputsFolderName, category));
        }

        var manifest = new TaskManifest
        {
            Id = taskId,
            FolderName = folderName,
            Title = request.Title.Trim(),
            Slug = TaskFolderConventions.Slugify(request.Title),
            Summary = request.Summary.Trim(),
            Status = "draft",
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = createdAtUtc,
            InputCategories = categories,
        };

        await WriteManifestAsync(taskRootPath, manifest, cancellationToken);
        await WriteMarkdownAsync(taskRootPath, request.TaskMarkdown, cancellationToken);

        return await LoadSnapshotAsync(taskRootPath, cancellationToken);
    }

    public async Task<IReadOnlyList<TaskManifest>> ListTasksAsync(
        string workspaceRootPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspaceRootPath) || !Directory.Exists(workspaceRootPath))
        {
            return [];
        }

        var manifests = new List<TaskManifest>();
        foreach (var directoryPath in Directory.EnumerateDirectories(workspaceRootPath))
        {
            var manifestPath = Path.Combine(directoryPath, TaskFolderConventions.TaskManifestFileName);
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            manifests.Add(await ReadManifestAsync(manifestPath, cancellationToken));
        }

        return manifests
            .OrderByDescending(static manifest => manifest.UpdatedAtUtc)
            .ToArray();
    }

    public async Task<TaskWorkspaceSnapshot?> LoadTaskAsync(
        string workspaceRootPath,
        string taskId,
        CancellationToken cancellationToken = default)
    {
        var taskRootPath = await ResolveTaskRootPathAsync(workspaceRootPath, taskId, cancellationToken);
        if (taskRootPath is null)
        {
            return null;
        }

        return await LoadSnapshotAsync(taskRootPath, cancellationToken);
    }

    public async Task SaveTaskAsync(
        string workspaceRootPath,
        TaskManifest manifest,
        string taskMarkdown,
        CancellationToken cancellationToken = default)
    {
        var taskRootPath = await GetRequiredTaskRootPathAsync(workspaceRootPath, manifest.Id, cancellationToken);
        var updatedManifest = manifest with
        {
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };

        await WriteManifestAsync(taskRootPath, updatedManifest, cancellationToken);
        await WriteMarkdownAsync(taskRootPath, taskMarkdown, cancellationToken);
    }

    public async Task<ArtifactManifest> ImportArtifactAsync(
        string workspaceRootPath,
        string taskId,
        ArtifactImportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourceFilePath);

        var taskRootPath = await GetRequiredTaskRootPathAsync(workspaceRootPath, taskId, cancellationToken);
        var snapshot = await LoadSnapshotAsync(taskRootPath, cancellationToken);

        if (!File.Exists(request.SourceFilePath))
        {
            throw new FileNotFoundException("Artifact source file was not found.", request.SourceFilePath);
        }

        var destinationRelativeDirectory = NormalizeDestinationDirectory(request.DestinationRelativeDirectory);
        var destinationDirectoryPath = EnsureDirectoryWithinTaskRoot(taskRootPath, destinationRelativeDirectory);
        Directory.CreateDirectory(destinationDirectoryPath);

        var sourceFileName = Path.GetFileName(request.SourceFilePath);
        var destinationFilePath = CreateUniqueDestinationPath(destinationDirectoryPath, sourceFileName);
        File.Copy(request.SourceFilePath, destinationFilePath);

        var importedAtUtc = DateTimeOffset.UtcNow;
        var artifact = new ArtifactManifest
        {
            Id = Guid.NewGuid().ToString("N"),
            Alias = string.IsNullOrWhiteSpace(request.Alias)
                ? TaskFolderConventions.Slugify(Path.GetFileNameWithoutExtension(destinationFilePath))
                : TaskFolderConventions.Slugify(request.Alias),
            DisplayName = Path.GetFileName(destinationFilePath),
            Category = destinationRelativeDirectory.Replace('\\', '/'),
            RelativePath = Path.GetRelativePath(taskRootPath, destinationFilePath).Replace('\\', '/'),
            MediaType = GuessMediaType(destinationFilePath),
            Sha256 = await ComputeSha256Async(destinationFilePath, cancellationToken),
            SizeBytes = new FileInfo(destinationFilePath).Length,
            ImportedAtUtc = importedAtUtc,
        };

        var updatedManifest = snapshot.Manifest with
        {
            UpdatedAtUtc = importedAtUtc,
            Artifacts = snapshot.Manifest.Artifacts.Concat([artifact]).ToArray(),
        };

        await WriteManifestAsync(taskRootPath, updatedManifest, cancellationToken);

        return artifact;
    }

    public async Task SaveRunAsync(
        string workspaceRootPath,
        string taskId,
        RunManifest run,
        CancellationToken cancellationToken = default)
    {
        var taskRootPath = await GetRequiredTaskRootPathAsync(workspaceRootPath, taskId, cancellationToken);
        var snapshot = await LoadSnapshotAsync(taskRootPath, cancellationToken);

        var runDirectoryName = $"{run.Sequence:0000}-{TaskFolderConventions.Slugify(run.Kind)}";
        var runDirectoryPath = EnsureDirectoryWithinTaskRoot(
            taskRootPath,
            Path.Combine(TaskFolderConventions.RunsFolderName, runDirectoryName));

        Directory.CreateDirectory(runDirectoryPath);

        var storedSteps = new List<StepManifest>();
        foreach (var step in run.Steps)
        {
            var stepDirectoryName = string.IsNullOrWhiteSpace(step.RelativeDirectory)
                ? $"{step.Attempt:00}-{TaskFolderConventions.Slugify(step.StepType)}"
                : step.RelativeDirectory;

            var stepDirectoryPath = EnsureDirectoryWithinTaskRoot(
                taskRootPath,
                Path.Combine(TaskFolderConventions.RunsFolderName, runDirectoryName, stepDirectoryName));

            Directory.CreateDirectory(stepDirectoryPath);

            var storedStep = step with
            {
                RelativeDirectory = Path.GetRelativePath(taskRootPath, stepDirectoryPath).Replace('\\', '/'),
            };

            var stepManifestPath = EnsurePathWithinDirectory(stepDirectoryPath, storedStep.StepFilePath, "Step file path escaped the step directory.");
            var promptPath = EnsurePathWithinDirectory(stepDirectoryPath, storedStep.PromptPath, "Prompt file path escaped the step directory.");
            var responsePath = EnsurePathWithinDirectory(stepDirectoryPath, storedStep.ResponsePath, "Response file path escaped the step directory.");
            var usagePath = EnsurePathWithinDirectory(stepDirectoryPath, storedStep.UsagePath, "Usage file path escaped the step directory.");

            await WriteJsonFileAsync(
                stepManifestPath,
                storedStep,
                cancellationToken);

            await EnsureTextFileExistsAsync(promptPath, cancellationToken);
            await EnsureTextFileExistsAsync(responsePath, cancellationToken);
            await EnsureTextFileExistsAsync(usagePath, cancellationToken);

            storedSteps.Add(storedStep);
        }

        var storedRun = run with
        {
            Steps = storedSteps,
        };

        var remainingRuns = snapshot.Manifest.Runs
            .Where(existingRun => !string.Equals(existingRun.Id, run.Id, StringComparison.OrdinalIgnoreCase));

        var updatedManifest = snapshot.Manifest with
        {
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            Runs = remainingRuns
                .Append(storedRun)
                .OrderBy(static savedRun => savedRun.Sequence)
                .ToArray(),
        };

        await WriteManifestAsync(taskRootPath, updatedManifest, cancellationToken);
    }

    private static async Task<TaskWorkspaceSnapshot> LoadSnapshotAsync(string taskRootPath, CancellationToken cancellationToken)
    {
        var manifestPath = Path.Combine(taskRootPath, TaskFolderConventions.TaskManifestFileName);
        var markdownPath = Path.Combine(taskRootPath, TaskFolderConventions.TaskMarkdownFileName);
        var manifest = await ReadManifestAsync(manifestPath, cancellationToken);
        var taskMarkdown = File.Exists(markdownPath)
            ? await File.ReadAllTextAsync(markdownPath, cancellationToken)
            : string.Empty;

        return new TaskWorkspaceSnapshot
        {
            TaskRootPath = taskRootPath,
            Manifest = manifest,
            TaskMarkdown = taskMarkdown,
            Tree = BuildTree(taskRootPath, taskRootPath),
        };
    }

    private static async Task<TaskManifest> ReadManifestAsync(string manifestPath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(manifestPath);
        var manifest = await JsonSerializer.DeserializeAsync<TaskManifest>(stream, JsonDefaults.SerializerOptions, cancellationToken);
        return manifest ?? throw new InvalidOperationException($"Task manifest at '{manifestPath}' was empty.");
    }

    private static Task WriteManifestAsync(string taskRootPath, TaskManifest manifest, CancellationToken cancellationToken)
    {
        return WriteJsonFileAsync(Path.Combine(taskRootPath, TaskFolderConventions.TaskManifestFileName), manifest, cancellationToken);
    }

    private static Task WriteMarkdownAsync(string taskRootPath, string taskMarkdown, CancellationToken cancellationToken)
    {
        return File.WriteAllTextAsync(
            Path.Combine(taskRootPath, TaskFolderConventions.TaskMarkdownFileName),
            taskMarkdown ?? string.Empty,
            cancellationToken);
    }

    private static async Task WriteJsonFileAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, value, JsonDefaults.SerializerOptions, cancellationToken);
    }

    private static string NormalizeDestinationDirectory(string destinationRelativeDirectory)
    {
        var normalized = string.IsNullOrWhiteSpace(destinationRelativeDirectory)
            ? Path.Combine(TaskFolderConventions.InputsFolderName, "documents")
            : destinationRelativeDirectory.Replace('/', Path.DirectorySeparatorChar);

        return normalized.TrimStart(Path.DirectorySeparatorChar);
    }

    private static string EnsureDirectoryWithinTaskRoot(string taskRootPath, string relativePath)
    {
        var fullRoot = NormalizeFullPath(taskRootPath);
        var fullPath = NormalizeFullPath(Path.Combine(fullRoot, relativePath));
        EnsurePathWithinRoot(fullRoot, fullPath, "Relative path escaped the task root.");
        return fullPath;
    }

    private static string EnsurePathWithinDirectory(string directoryPath, string relativePath, string errorMessage)
    {
        var fullDirectory = NormalizeFullPath(directoryPath);
        var fullPath = NormalizeFullPath(Path.Combine(fullDirectory, relativePath));
        EnsurePathWithinRoot(fullDirectory, fullPath, errorMessage);
        return fullPath;
    }

    private static string NormalizeFullPath(string path)
    {
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    private static void EnsurePathWithinRoot(string fullRoot, string fullPath, string errorMessage)
    {
        var rootWithSeparator = $"{fullRoot}{Path.DirectorySeparatorChar}";
        if (!string.Equals(fullPath, fullRoot, StringComparison.OrdinalIgnoreCase)
            && !fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(errorMessage);
        }
    }

    private static string CreateUniqueDestinationPath(string destinationDirectoryPath, string sourceFileName)
    {
        var candidatePath = Path.Combine(destinationDirectoryPath, sourceFileName);
        if (!File.Exists(candidatePath))
        {
            return candidatePath;
        }

        var baseName = Path.GetFileNameWithoutExtension(sourceFileName);
        var extension = Path.GetExtension(sourceFileName);
        var counter = 1;

        while (true)
        {
            candidatePath = Path.Combine(destinationDirectoryPath, $"{baseName}-{counter}{extension}");
            if (!File.Exists(candidatePath))
            {
                return candidatePath;
            }

            counter++;
        }
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        var hashBytes = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static async Task EnsureTextFileExistsAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            await File.WriteAllTextAsync(path, string.Empty, cancellationToken);
        }
    }

    private static List<TaskTreeNode> BuildTree(string directoryPath, string taskRootPath)
    {
        var nodes = new List<TaskTreeNode>();

        foreach (var childDirectory in Directory.EnumerateDirectories(directoryPath).OrderBy(static path => path, StringComparer.OrdinalIgnoreCase))
        {
            nodes.Add(new TaskTreeNode
            {
                Name = Path.GetFileName(childDirectory),
                RelativePath = Path.GetRelativePath(taskRootPath, childDirectory).Replace('\\', '/'),
                IsDirectory = true,
                Children = BuildTree(childDirectory, taskRootPath),
            });
        }

        foreach (var childFile in Directory.EnumerateFiles(directoryPath).OrderBy(static path => path, StringComparer.OrdinalIgnoreCase))
        {
            nodes.Add(new TaskTreeNode
            {
                Name = Path.GetFileName(childFile),
                RelativePath = Path.GetRelativePath(taskRootPath, childFile).Replace('\\', '/'),
                IsDirectory = false,
                Children = [],
            });
        }

        return nodes;
    }

    private static string GuessMediaType(string filePath)
    {
        return Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".md" => "text/markdown",
            ".txt" => "text/plain",
            ".json" => "application/json",
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".wav" => "audio/wav",
            ".mp3" => "audio/mpeg",
            _ => "application/octet-stream",
        };
    }

    private static async Task<string?> ResolveTaskRootPathAsync(
        string workspaceRootPath,
        string taskId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(workspaceRootPath) || string.IsNullOrWhiteSpace(taskId) || !Directory.Exists(workspaceRootPath))
        {
            return null;
        }

        var directPath = Path.Combine(workspaceRootPath, TaskFolderConventions.CreateTaskFolderName(taskId));
        if (File.Exists(Path.Combine(directPath, TaskFolderConventions.TaskManifestFileName)))
        {
            return directPath;
        }

        foreach (var directoryPath in Directory.EnumerateDirectories(workspaceRootPath))
        {
            var manifestPath = Path.Combine(directoryPath, TaskFolderConventions.TaskManifestFileName);
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            var manifest = await ReadManifestAsync(manifestPath, cancellationToken);
            if (string.Equals(manifest.Id, taskId, StringComparison.OrdinalIgnoreCase))
            {
                return directoryPath;
            }
        }

        return null;
    }

    private static async Task<string> GetRequiredTaskRootPathAsync(
        string workspaceRootPath,
        string taskId,
        CancellationToken cancellationToken)
    {
        return await ResolveTaskRootPathAsync(workspaceRootPath, taskId, cancellationToken)
            ?? throw new DirectoryNotFoundException($"Task '{taskId}' was not found under '{workspaceRootPath}'.");
    }
}
