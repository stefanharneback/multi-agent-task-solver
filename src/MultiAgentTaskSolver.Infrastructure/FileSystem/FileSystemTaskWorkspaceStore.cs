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

        var inputPaths = NormalizeInputPaths(request.InputPaths);
        var outputPaths = NormalizeOutputPaths(request.OutputPaths);

        var manifest = new TaskManifest
        {
            Id = taskId,
            FolderName = folderName,
            Title = request.Title.Trim(),
            Slug = TaskFolderConventions.Slugify(request.Title),
            Summary = request.Summary.Trim(),
            Status = TaskLifecycleState.Draft,
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = createdAtUtc,
            InputPaths = inputPaths,
            OutputPaths = outputPaths,
            OutputPathDescriptions = request.OutputPathDescriptions,
            InputCategories = inputPaths.Select(ToLegacyInputCategory).ToArray(),
        };

        EnsureDeclaredPathsExist(taskRootPath, manifest);

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
        var normalizedManifest = NormalizeManifest(manifest);
        EnsureDeclaredPathsExist(taskRootPath, normalizedManifest);

        var updatedManifest = normalizedManifest with
        {
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };

        await WriteManifestAsync(taskRootPath, updatedManifest, cancellationToken);
        await WriteMarkdownAsync(taskRootPath, taskMarkdown, cancellationToken);
    }

    public async Task<IReadOnlyList<ArtifactManifest>> ImportArtifactAsync(
        string workspaceRootPath,
        string taskId,
        ArtifactImportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourcePath);

        var taskRootPath = await GetRequiredTaskRootPathAsync(workspaceRootPath, taskId, cancellationToken);
        var snapshot = await LoadSnapshotAsync(taskRootPath, cancellationToken);

        var destinationRelativeDirectory = NormalizeDestinationDirectory(request.DestinationRelativeDirectory);
        var destinationDirectoryPath = EnsureDirectoryWithinTaskRoot(taskRootPath, destinationRelativeDirectory);
        Directory.CreateDirectory(destinationDirectoryPath);

        var importedAtUtc = DateTimeOffset.UtcNow;
        var reservedAliases = new HashSet<string>(
            snapshot.Manifest.Artifacts
                .Select(static artifact => artifact.Alias)
                .Where(static alias => !string.IsNullOrWhiteSpace(alias)),
            StringComparer.OrdinalIgnoreCase);

        IReadOnlyList<ArtifactManifest> importedArtifacts = File.Exists(request.SourcePath)
            ? [await ImportFileAsync(
                taskRootPath,
                request.SourcePath,
                destinationRelativeDirectory,
                destinationDirectoryPath,
                request.Alias,
                importedAtUtc,
                reservedAliases,
                cancellationToken)]
            : Directory.Exists(request.SourcePath)
                ? await ImportDirectoryAsync(
                    taskRootPath,
                    request.SourcePath,
                    destinationRelativeDirectory,
                    destinationDirectoryPath,
                    importedAtUtc,
                    reservedAliases,
                    cancellationToken)
                : throw new FileNotFoundException("Artifact source path was not found.", request.SourcePath);

        var updatedManifest = snapshot.Manifest with
        {
            UpdatedAtUtc = importedAtUtc,
            Artifacts = snapshot.Manifest.Artifacts.Concat(importedArtifacts).ToArray(),
        };

        await WriteManifestAsync(taskRootPath, updatedManifest, cancellationToken);

        return importedArtifacts;
    }

    public async Task SaveRunAsync(
        string workspaceRootPath,
        string taskId,
        RunManifest run,
        CancellationToken cancellationToken = default)
    {
        var taskRootPath = await GetRequiredTaskRootPathAsync(workspaceRootPath, taskId, cancellationToken);
        var snapshot = await LoadSnapshotAsync(taskRootPath, cancellationToken);

        var runDirectoryName = $"{run.Sequence:0000}-{run.Kind.GetStorageName()}";
        var runDirectoryPath = EnsureDirectoryWithinTaskRoot(
            taskRootPath,
            Path.Combine(TaskFolderConventions.RunsFolderName, runDirectoryName));

        Directory.CreateDirectory(runDirectoryPath);

        var storedSteps = new List<StepManifest>();
        foreach (var step in run.Steps)
        {
            var stepDirectoryName = string.IsNullOrWhiteSpace(step.RelativeDirectory)
                ? $"{step.Attempt:00}-{step.StepType.GetStorageName()}"
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

    public async Task SaveStepArtifactsAsync(
        string workspaceRootPath,
        string taskId,
        string runId,
        string stepId,
        StepArtifactsPayload payload,
        CancellationToken cancellationToken = default)
    {
        var taskRootPath = await GetRequiredTaskRootPathAsync(workspaceRootPath, taskId, cancellationToken);
        var snapshot = await LoadSnapshotAsync(taskRootPath, cancellationToken);

        var run = snapshot.Manifest.Runs.FirstOrDefault(existingRun => string.Equals(existingRun.Id, runId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Run '{runId}' was not found for task '{taskId}'.");

        var step = run.Steps.FirstOrDefault(existingStep => string.Equals(existingStep.Id, stepId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Step '{stepId}' was not found for run '{runId}'.");

        var stepDirectoryPath = EnsureDirectoryWithinTaskRoot(taskRootPath, step.RelativeDirectory);
        Directory.CreateDirectory(stepDirectoryPath);

        var promptPath = EnsurePathWithinDirectory(stepDirectoryPath, step.PromptPath, "Prompt file path escaped the step directory.");
        var responsePath = EnsurePathWithinDirectory(stepDirectoryPath, step.ResponsePath, "Response file path escaped the step directory.");
        var usagePath = EnsurePathWithinDirectory(stepDirectoryPath, step.UsagePath, "Usage file path escaped the step directory.");

        await File.WriteAllTextAsync(promptPath, payload.PromptMarkdown ?? string.Empty, cancellationToken);
        await File.WriteAllTextAsync(responsePath, payload.ResponseMarkdown ?? string.Empty, cancellationToken);
        await WriteJsonFileAsync(usagePath, payload.Usage, cancellationToken);
    }

    public async Task SaveOutputArtifactAsync(
        string workspaceRootPath,
        string taskId,
        OutputArtifactPayload payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payload.RelativePath);

        var taskRootPath = await GetRequiredTaskRootPathAsync(workspaceRootPath, taskId, cancellationToken);
        var outputsRootPath = EnsureDirectoryWithinTaskRoot(taskRootPath, TaskFolderConventions.OutputsFolderName);
        var normalizedRelativePath = TaskFolderConventions.NormalizeOutputPath(payload.RelativePath).Replace('/', Path.DirectorySeparatorChar);
        var outputPath = EnsureDirectoryWithinTaskRoot(taskRootPath, normalizedRelativePath);
        EnsurePathWithinRoot(outputsRootPath, outputPath, "Output artifact path escaped the outputs folder.");

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await File.WriteAllTextAsync(outputPath, payload.Content ?? string.Empty, cancellationToken);
    }

    private static async Task<TaskWorkspaceSnapshot> LoadSnapshotAsync(string taskRootPath, CancellationToken cancellationToken)
    {
        var manifestPath = Path.Combine(taskRootPath, TaskFolderConventions.TaskManifestFileName);
        var markdownPath = Path.Combine(taskRootPath, TaskFolderConventions.TaskMarkdownFileName);
        var manifest = await ReadManifestAsync(manifestPath, cancellationToken);
        EnsureDeclaredPathsExist(taskRootPath, manifest);
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
        return manifest is null
            ? throw new InvalidOperationException($"Task manifest at '{manifestPath}' was empty.")
            : NormalizeManifest(manifest);
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
        return TaskFolderConventions.NormalizeInputPath(destinationRelativeDirectory)
            .Replace('/', Path.DirectorySeparatorChar);
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

    private static string CreateUniqueFileDestinationPath(string destinationDirectoryPath, string sourceFileName)
    {
        var candidatePath = Path.Combine(destinationDirectoryPath, sourceFileName);
        if (!File.Exists(candidatePath) && !Directory.Exists(candidatePath))
        {
            return candidatePath;
        }

        var baseName = Path.GetFileNameWithoutExtension(sourceFileName);
        var extension = Path.GetExtension(sourceFileName);
        var counter = 1;

        while (true)
        {
            candidatePath = Path.Combine(destinationDirectoryPath, $"{baseName}-{counter}{extension}");
            if (!File.Exists(candidatePath) && !Directory.Exists(candidatePath))
            {
                return candidatePath;
            }

            counter++;
        }
    }

    private static string CreateUniqueDirectoryDestinationPath(string destinationDirectoryPath, string directoryName)
    {
        var candidatePath = Path.Combine(destinationDirectoryPath, directoryName);
        if (!Directory.Exists(candidatePath) && !File.Exists(candidatePath))
        {
            return candidatePath;
        }

        var counter = 1;
        while (true)
        {
            candidatePath = Path.Combine(destinationDirectoryPath, $"{directoryName}-{counter}");
            if (!Directory.Exists(candidatePath) && !File.Exists(candidatePath))
            {
                return candidatePath;
            }

            counter++;
        }
    }

    private static TaskManifest NormalizeManifest(TaskManifest manifest)
    {
        var inputPaths = NormalizeInputPaths(
            manifest.InputPaths.Count > 0
                ? manifest.InputPaths
                : manifest.InputCategories.Count > 0
                    ? manifest.InputCategories.Select(static category => $"{TaskFolderConventions.InputsFolderName}/{category}")
                    : []);

        var outputPaths = NormalizeOutputPaths(manifest.OutputPaths);

        return manifest with
        {
            InputPaths = inputPaths,
            OutputPaths = outputPaths,
            InputCategories = inputPaths.Select(ToLegacyInputCategory).ToArray(),
        };
    }

    private static string[] NormalizeInputPaths(IEnumerable<string>? inputPaths)
    {
        var values = inputPaths?
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(TaskFolderConventions.NormalizeInputPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return values is { Length: > 0 } ? values : [];
    }

    private static string[] NormalizeOutputPaths(IEnumerable<string>? outputPaths)
    {
        var values = outputPaths?
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(TaskFolderConventions.NormalizeOutputPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return values is { Length: > 0 } ? values : [];
    }

    private static string ToLegacyInputCategory(string inputPath)
    {
        var normalized = TaskFolderConventions.NormalizeInputPath(inputPath);
        return normalized.StartsWith($"{TaskFolderConventions.InputsFolderName}/", StringComparison.OrdinalIgnoreCase)
            ? normalized[(TaskFolderConventions.InputsFolderName.Length + 1)..]
            : normalized;
    }

    private static void EnsureDeclaredPathsExist(string taskRootPath, TaskManifest manifest)
    {
        foreach (var inputPath in manifest.InputPaths)
        {
            Directory.CreateDirectory(EnsureDirectoryWithinTaskRoot(taskRootPath, inputPath));
        }

        var outputsRootPath = EnsureDirectoryWithinTaskRoot(taskRootPath, TaskFolderConventions.OutputsFolderName);
        foreach (var outputPath in manifest.OutputPaths)
        {
            var fullOutputPath = EnsureDirectoryWithinTaskRoot(taskRootPath, outputPath);
            EnsurePathWithinRoot(outputsRootPath, fullOutputPath, "Output artifact path escaped the outputs folder.");
            Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath)!);
        }
    }

    private static async Task<ArtifactManifest> ImportFileAsync(
        string taskRootPath,
        string sourceFilePath,
        string destinationRelativeDirectory,
        string destinationDirectoryPath,
        string? requestedAlias,
        DateTimeOffset importedAtUtc,
        ISet<string> reservedAliases,
        CancellationToken cancellationToken)
    {
        var sourceFileName = Path.GetFileName(sourceFilePath);
        var destinationFilePath = CreateUniqueFileDestinationPath(destinationDirectoryPath, sourceFileName);
        File.Copy(sourceFilePath, destinationFilePath);

        return await CreateArtifactManifestAsync(
            taskRootPath,
            destinationFilePath,
            destinationRelativeDirectory,
            requestedAlias,
            importedAtUtc,
            reservedAliases,
            cancellationToken);
    }

    private static async Task<IReadOnlyList<ArtifactManifest>> ImportDirectoryAsync(
        string taskRootPath,
        string sourceDirectoryPath,
        string destinationRelativeDirectory,
        string destinationDirectoryPath,
        DateTimeOffset importedAtUtc,
        ISet<string> reservedAliases,
        CancellationToken cancellationToken)
    {
        var sourceDirectoryName = Path.GetFileName(Path.TrimEndingDirectorySeparator(sourceDirectoryPath));
        if (string.IsNullOrWhiteSpace(sourceDirectoryName))
        {
            throw new InvalidOperationException("Imported folders must have a valid name.");
        }

        var destinationRootPath = CreateUniqueDirectoryDestinationPath(destinationDirectoryPath, sourceDirectoryName);
        Directory.CreateDirectory(destinationRootPath);

        foreach (var sourceSubdirectoryPath in Directory.EnumerateDirectories(sourceDirectoryPath, "*", SearchOption.AllDirectories)
                     .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase))
        {
            var relativeSubdirectoryPath = Path.GetRelativePath(sourceDirectoryPath, sourceSubdirectoryPath);
            Directory.CreateDirectory(Path.Combine(destinationRootPath, relativeSubdirectoryPath));
        }

        var artifacts = new List<ArtifactManifest>();
        foreach (var sourceFilePath in Directory.EnumerateFiles(sourceDirectoryPath, "*", SearchOption.AllDirectories)
                     .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase))
        {
            var relativeSubPath = Path.GetRelativePath(sourceDirectoryPath, sourceFilePath);
            var destinationFilePath = Path.Combine(destinationRootPath, relativeSubPath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationFilePath)!);
            File.Copy(sourceFilePath, destinationFilePath);

            artifacts.Add(await CreateArtifactManifestAsync(
                taskRootPath,
                destinationFilePath,
                destinationRelativeDirectory,
                null,
                importedAtUtc,
                reservedAliases,
                cancellationToken));
        }

        return artifacts;
    }

    private static async Task<ArtifactManifest> CreateArtifactManifestAsync(
        string taskRootPath,
        string destinationFilePath,
        string destinationRelativeDirectory,
        string? requestedAlias,
        DateTimeOffset importedAtUtc,
        ISet<string> reservedAliases,
        CancellationToken cancellationToken)
    {
        var relativePath = Path.GetRelativePath(taskRootPath, destinationFilePath).Replace('\\', '/');
        var category = Path.GetDirectoryName(relativePath)?.Replace('\\', '/') ?? destinationRelativeDirectory.Replace('\\', '/');

        return new ArtifactManifest
        {
            Id = Guid.NewGuid().ToString("N"),
            Alias = CreateUniqueArtifactAlias(taskRootPath, destinationFilePath, requestedAlias, reservedAliases),
            DisplayName = Path.GetFileName(destinationFilePath),
            Category = category,
            RelativePath = relativePath,
            MediaType = GuessMediaType(destinationFilePath),
            Sha256 = await ComputeSha256Async(destinationFilePath, cancellationToken),
            SizeBytes = new FileInfo(destinationFilePath).Length,
            ImportedAtUtc = importedAtUtc,
        };
    }

    private static string CreateUniqueArtifactAlias(
        string taskRootPath,
        string destinationFilePath,
        string? requestedAlias,
        ISet<string> reservedAliases)
    {
        var aliasSource = string.IsNullOrWhiteSpace(requestedAlias)
            ? Path.ChangeExtension(Path.GetRelativePath(taskRootPath, destinationFilePath), null)?
                .Replace(Path.DirectorySeparatorChar, '-')
                .Replace(Path.AltDirectorySeparatorChar, '-')
            : requestedAlias;

        var baseAlias = TaskFolderConventions.Slugify(aliasSource ?? Path.GetFileNameWithoutExtension(destinationFilePath));
        var alias = baseAlias;
        var counter = 2;

        while (!reservedAliases.Add(alias))
        {
            alias = $"{baseAlias}-{counter}";
            counter++;
        }

        return alias;
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
