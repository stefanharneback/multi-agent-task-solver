using System.Text;
using System.Text.Json;
using MultiAgentTaskSolver.Core;
using MultiAgentTaskSolver.Core.Abstractions;
using MultiAgentTaskSolver.Core.Models;

namespace MultiAgentTaskSolver.Infrastructure.Execution;

public sealed class TaskWorkerWorkflow : ITaskWorkerWorkflow
{
    private static readonly JsonSerializerOptions PrettyPrintOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly ITaskWorkspaceStore _taskWorkspaceStore;
    private readonly IArtifactReferenceResolver _artifactReferenceResolver;
    private readonly IWorkerPromptFactory _workerPromptFactory;
    private readonly Dictionary<string, IProviderAdapter> _providerAdapters;

    public TaskWorkerWorkflow(
        ITaskWorkspaceStore taskWorkspaceStore,
        IArtifactReferenceResolver artifactReferenceResolver,
        IWorkerPromptFactory workerPromptFactory,
        IEnumerable<IProviderAdapter> providerAdapters)
    {
        _taskWorkspaceStore = taskWorkspaceStore;
        _artifactReferenceResolver = artifactReferenceResolver;
        _workerPromptFactory = workerPromptFactory;
        _providerAdapters = providerAdapters.ToDictionary(adapter => adapter.ProviderId, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<TaskWorkerResult> RunAsync(
        string workspaceRootPath,
        TaskWorkspaceSnapshot snapshot,
        ProviderRef provider,
        ModelRef model,
        string bearerToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(bearerToken);

        if (!model.Capabilities.SupportsTextInput)
        {
            throw new InvalidOperationException($"Model '{model.ModelId}' does not support text input.");
        }

        var startedAtUtc = DateTimeOffset.UtcNow;
        var workingStatus = TaskWorkflowStateMachine.StartWorker(snapshot.Manifest.Status);
        var workingManifest = snapshot.Manifest with
        {
            Status = workingStatus,
            UpdatedAtUtc = startedAtUtc,
        };

        await _taskWorkspaceStore.SaveTaskAsync(workspaceRootPath, workingManifest, snapshot.TaskMarkdown, cancellationToken);

        var resolution = await _artifactReferenceResolver.ResolveAsync(snapshot, cancellationToken);
        var promptPackage = _workerPromptFactory.Create(snapshot with { Manifest = workingManifest }, resolution);
        var sequence = NextSequence(snapshot.Manifest);
        var historyOutputRelativePath = TaskFolderConventions.CreateRunScopedWorkerOutputPath(sequence);
        var declaredOutputPaths = workingManifest.OutputPaths
            .Select(TaskFolderConventions.NormalizeOutputPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(path => !string.Equals(path, historyOutputRelativePath, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var runId = Guid.NewGuid().ToString("N");
        var stepId = Guid.NewGuid().ToString("N");
        var step = new StepManifest
        {
            Id = stepId,
            StepType = TaskStepType.Worker,
            Status = TaskStepStatus.Running,
            Attempt = 1,
            Provider = provider,
            Model = model,
            PromptVersion = promptPackage.PromptVersion,
            ReferencedArtifactAliases = resolution.ReferencedAliases,
            StartedAtUtc = startedAtUtc,
        };

        var run = new RunManifest
        {
            Id = runId,
            Title = TaskRunKind.Worker.GetDisplayName(),
            Kind = TaskRunKind.Worker,
            Status = TaskRunStatus.Running,
            Sequence = sequence,
            StartedAtUtc = startedAtUtc,
            Steps = [step],
        };

        await _taskWorkspaceStore.SaveRunAsync(workspaceRootPath, snapshot.Manifest.Id, run, cancellationToken);
        await _taskWorkspaceStore.SaveStepArtifactsAsync(
            workspaceRootPath,
            snapshot.Manifest.Id,
            runId,
            stepId,
            new StepArtifactsPayload
            {
                PromptMarkdown = BuildPromptMarkdown(promptPackage),
            },
            cancellationToken);

        try
        {
            var adapter = GetProviderAdapter(provider.ProviderId);
            var response = await adapter.SendTextAsync(
                provider,
                new LlmRequest
                {
                    ModelId = model.ModelId,
                    InputText = promptPackage.InputText,
                    Instructions = promptPackage.Instructions,
                },
                bearerToken,
                cancellationToken);

            var completedAtUtc = DateTimeOffset.UtcNow;
            var outputText = response.OutputText?.Trim() ?? string.Empty;
            var summary = BuildSummary(outputText, resolution);
            var completedStep = step with
            {
                Status = TaskStepStatus.Completed,
                Summary = summary,
                OutputArtifactPaths = [historyOutputRelativePath, .. declaredOutputPaths],
                CompletedAtUtc = completedAtUtc,
            };

            var completedRun = run with
            {
                Status = TaskRunStatus.Completed,
                Summary = summary,
                CompletedAtUtc = completedAtUtc,
                Steps = [completedStep],
            };

            await _taskWorkspaceStore.SaveRunAsync(workspaceRootPath, snapshot.Manifest.Id, completedRun, cancellationToken);
            await _taskWorkspaceStore.SaveStepArtifactsAsync(
                workspaceRootPath,
                snapshot.Manifest.Id,
                runId,
                stepId,
                new StepArtifactsPayload
                {
                    PromptMarkdown = BuildPromptMarkdown(promptPackage),
                    ResponseMarkdown = BuildResponseMarkdown(response),
                    Usage = response.Usage,
                },
                cancellationToken);
            await _taskWorkspaceStore.SaveOutputArtifactAsync(
                workspaceRootPath,
                snapshot.Manifest.Id,
                new OutputArtifactPayload
                {
                    RelativePath = historyOutputRelativePath,
                    Content = BuildOutputArtifactContent(outputText, summary),
                },
                cancellationToken);
            foreach (var declaredOutputPath in declaredOutputPaths)
            {
                await _taskWorkspaceStore.SaveOutputArtifactAsync(
                    workspaceRootPath,
                    snapshot.Manifest.Id,
                    new OutputArtifactPayload
                    {
                        RelativePath = declaredOutputPath,
                        Content = BuildOutputArtifactContent(outputText, summary),
                    },
                    cancellationToken);
            }

            var latestSnapshot = await _taskWorkspaceStore.LoadTaskAsync(workspaceRootPath, snapshot.Manifest.Id, cancellationToken)
                ?? throw new InvalidOperationException($"Task '{snapshot.Manifest.Id}' was not found after saving the worker run.");

            var updatedManifest = latestSnapshot.Manifest with
            {
                Status = TaskWorkflowStateMachine.CompleteWorker(workingManifest.Status),
                UpdatedAtUtc = completedAtUtc,
            };

            await _taskWorkspaceStore.SaveTaskAsync(workspaceRootPath, updatedManifest, latestSnapshot.TaskMarkdown, cancellationToken);

            return new TaskWorkerResult
            {
                TaskId = snapshot.Manifest.Id,
                RunId = completedRun.Id,
                StepId = completedStep.Id,
                TaskStatus = updatedManifest.Status,
                OutputText = outputText,
                Summary = summary,
                PromptVersion = promptPackage.PromptVersion,
                ReferencedAliases = resolution.ReferencedAliases,
                OutputArtifactPaths = completedStep.OutputArtifactPaths,
                Usage = response.Usage,
            };
        }
        catch (Exception ex)
        {
            var failedAtUtc = DateTimeOffset.UtcNow;
            var failureSummary = $"Worker execution failed: {ex.Message}";
            var failedStep = step with
            {
                Status = TaskStepStatus.Failed,
                Summary = failureSummary,
                CompletedAtUtc = failedAtUtc,
            };

            var failedRun = run with
            {
                Status = TaskRunStatus.Failed,
                Summary = failureSummary,
                CompletedAtUtc = failedAtUtc,
                Steps = [failedStep],
            };

            await _taskWorkspaceStore.SaveRunAsync(workspaceRootPath, snapshot.Manifest.Id, failedRun, cancellationToken);
            await _taskWorkspaceStore.SaveStepArtifactsAsync(
                workspaceRootPath,
                snapshot.Manifest.Id,
                runId,
                stepId,
                new StepArtifactsPayload
                {
                    PromptMarkdown = BuildPromptMarkdown(promptPackage),
                    ResponseMarkdown = BuildFailureMarkdown(ex),
                },
                cancellationToken);

            var latestSnapshot = await _taskWorkspaceStore.LoadTaskAsync(workspaceRootPath, snapshot.Manifest.Id, cancellationToken)
                ?? throw new InvalidOperationException($"Task '{snapshot.Manifest.Id}' was not found after saving the failed worker run.");

            var revertedManifest = latestSnapshot.Manifest with
            {
                Status = TaskWorkflowStateMachine.FailWorker(workingManifest.Status, snapshot.Manifest.Status),
                UpdatedAtUtc = failedAtUtc,
            };

            await _taskWorkspaceStore.SaveTaskAsync(workspaceRootPath, revertedManifest, latestSnapshot.TaskMarkdown, cancellationToken);
            throw;
        }
    }

    private IProviderAdapter GetProviderAdapter(string providerId)
    {
        return _providerAdapters.TryGetValue(providerId, out var adapter)
            ? adapter
            : throw new InvalidOperationException($"No provider adapter is registered for '{providerId}'.");
    }

    private static int NextSequence(TaskManifest manifest)
    {
        return manifest.Runs.Count == 0 ? 1 : manifest.Runs.Max(static run => run.Sequence) + 1;
    }

    private static string BuildPromptMarkdown(WorkerPromptPackage promptPackage)
    {
        var builder = new StringBuilder();
        builder.Append("# Prompt Package (").Append(promptPackage.PromptVersion).AppendLine(")");
        builder.AppendLine();
        builder.AppendLine("## Instructions");
        builder.AppendLine(promptPackage.Instructions);
        builder.AppendLine();
        builder.AppendLine("## Input");
        builder.AppendLine(promptPackage.InputText);
        return builder.ToString().TrimEnd();
    }

    private static string BuildResponseMarkdown(ProviderTextResponse response)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Output");
        builder.AppendLine(string.IsNullOrWhiteSpace(response.OutputText) ? "_No output text returned._" : response.OutputText.Trim());
        builder.AppendLine();
        builder.AppendLine("## Raw Response");
        builder.AppendLine("```json");
        builder.AppendLine(PrettyPrintJson(response.RawResponseBody));
        builder.AppendLine("```");
        return builder.ToString().TrimEnd();
    }

    private static string BuildOutputArtifactContent(string outputText, string summary)
    {
        if (!string.IsNullOrWhiteSpace(outputText))
        {
            return outputText;
        }

        return
            $"""
            # Worker Output

            {summary}
            """;
    }

    private static string BuildFailureMarkdown(Exception exception)
    {
        return
            $"""
            # Failure

            {exception.GetType().Name}: {exception.Message}
            """;
    }

    private static string BuildSummary(string outputText, TaskReferenceResolution resolution)
    {
        if (!string.IsNullOrWhiteSpace(outputText))
        {
            var singleLine = outputText.ReplaceLineEndings(" ").Trim();
            return singleLine.Length <= 180 ? singleLine : $"{singleLine[..180]}...";
        }

        if (resolution.MissingAliases.Count > 0)
        {
            return $"Worker completed with missing references: {string.Join(", ", resolution.MissingAliases.Select(alias => $"@{alias}"))}.";
        }

        return "Worker completed.";
    }

    private static string PrettyPrintJson(string rawJson)
    {
        try
        {
            using var document = JsonDocument.Parse(rawJson);
            return JsonSerializer.Serialize(document.RootElement, PrettyPrintOptions);
        }
        catch (JsonException)
        {
            return rawJson;
        }
    }
}
