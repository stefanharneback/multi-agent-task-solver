using System.Net;
using MultiAgentTaskSolver.Core.Models;
using MultiAgentTaskSolver.Infrastructure.Execution;
using MultiAgentTaskSolver.Infrastructure.FileSystem;

namespace MultiAgentTaskSolver.Infrastructure.Tests;

public sealed class TaskWorkerWorkflowTests : IDisposable
{
    private readonly string _tempRootPath = Path.Combine(Path.GetTempPath(), "mats-worker-workflow-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task RunAsyncPersistsCompletedWorkerRunAndOutputArtifact()
    {
        var store = new FileSystemTaskWorkspaceStore();
        var created = await store.CreateTaskAsync(_tempRootPath, new CreateTaskRequest
        {
            Title = "Draft worker output",
            Summary = "Create a first draft from @policy.",
            TaskMarkdown = "# Task\nUse @policy to draft a first response.",
            OutputPaths = ["deliverables/final-report.md"],
        });

        var sourceFilePath = Path.Combine(_tempRootPath, "policy.md");
        await File.WriteAllTextAsync(sourceFilePath, "Policy draft content.");
        await store.ImportArtifactAsync(_tempRootPath, created.Manifest.Id, new ArtifactImportRequest
        {
            SourcePath = sourceFilePath,
            DestinationRelativeDirectory = Path.Combine("inputs", "documents"),
            Alias = "policy",
        });

        var snapshot = await store.LoadTaskAsync(_tempRootPath, created.Manifest.Id);
        Assert.NotNull(snapshot);

        var approvedManifest = snapshot!.Manifest with
        {
            Status = TaskLifecycleState.WorkApproved,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
        await store.SaveTaskAsync(_tempRootPath, approvedManifest, snapshot.TaskMarkdown);

        snapshot = await store.LoadTaskAsync(_tempRootPath, created.Manifest.Id);
        Assert.NotNull(snapshot);

        var workflow = new TaskWorkerWorkflow(
            store,
            new ArtifactReferenceResolver(),
            new WorkerPromptFactory(),
            [new StubProviderAdapter()]);

        var result = await workflow.RunAsync(_tempRootPath, snapshot!, CreateProvider(), CreateModel(), "client-secret");
        var reloaded = await store.LoadTaskAsync(_tempRootPath, created.Manifest.Id);

        Assert.NotNull(reloaded);
        Assert.Equal(TaskLifecycleState.Working, reloaded!.Manifest.Status);
        Assert.Single(reloaded.Manifest.Runs);
        Assert.Equal(TaskRunKind.Worker, reloaded.Manifest.Runs[0].Kind);
        Assert.Equal(TaskRunStatus.Completed, reloaded.Manifest.Runs[0].Status);
        Assert.Equal(TaskStepStatus.Completed, reloaded.Manifest.Runs[0].Steps[0].Status);
        Assert.Equal(
            ["outputs/0001-worker/worker-output.md", "outputs/deliverables/final-report.md"],
            reloaded.Manifest.Runs[0].Steps[0].OutputArtifactPaths);
        Assert.Equal("outputs/0001-worker/worker-output.md", result.OutputArtifactPaths[0]);
        Assert.Equal("outputs/deliverables/final-report.md", result.OutputArtifactPaths[1]);

        var taskRootPath = Path.Combine(_tempRootPath, created.Manifest.FolderName);
        var historyOutputPath = Path.Combine(taskRootPath, "outputs", "0001-worker", "worker-output.md");
        var declaredOutputPath = Path.Combine(taskRootPath, "outputs", "deliverables", "final-report.md");
        Assert.Contains("## Deliverable", await File.ReadAllTextAsync(historyOutputPath), StringComparison.Ordinal);
        Assert.Contains("## Deliverable", await File.ReadAllTextAsync(declaredOutputPath), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsyncWithoutDeclaredOutputTargetsStillPersistsRunScopedHistoryOutput()
    {
        var store = new FileSystemTaskWorkspaceStore();
        var created = await store.CreateTaskAsync(_tempRootPath, new CreateTaskRequest
        {
            Title = "History-only worker output",
            Summary = "Create a first draft from @policy.",
            TaskMarkdown = "# Task\nUse @policy to draft a first response.",
        });

        var sourceFilePath = Path.Combine(_tempRootPath, "policy-history-only.md");
        await File.WriteAllTextAsync(sourceFilePath, "Policy draft content.");
        await store.ImportArtifactAsync(_tempRootPath, created.Manifest.Id, new ArtifactImportRequest
        {
            SourcePath = sourceFilePath,
            DestinationRelativeDirectory = Path.Combine("inputs", "documents"),
            Alias = "policy",
        });

        var snapshot = await store.LoadTaskAsync(_tempRootPath, created.Manifest.Id);
        Assert.NotNull(snapshot);

        var approvedManifest = snapshot!.Manifest with
        {
            Status = TaskLifecycleState.WorkApproved,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
        await store.SaveTaskAsync(_tempRootPath, approvedManifest, snapshot.TaskMarkdown);

        snapshot = await store.LoadTaskAsync(_tempRootPath, created.Manifest.Id);
        Assert.NotNull(snapshot);

        var workflow = new TaskWorkerWorkflow(
            store,
            new ArtifactReferenceResolver(),
            new WorkerPromptFactory(),
            [new StubProviderAdapter()]);

        var result = await workflow.RunAsync(_tempRootPath, snapshot!, CreateProvider(), CreateModel(), "client-secret");
        var reloaded = await store.LoadTaskAsync(_tempRootPath, created.Manifest.Id);

        Assert.NotNull(reloaded);
        Assert.Empty(reloaded!.Manifest.OutputPaths);
        Assert.Equal(["outputs/0001-worker/worker-output.md"], reloaded.Manifest.Runs[0].Steps[0].OutputArtifactPaths);
        Assert.Equal(["outputs/0001-worker/worker-output.md"], result.OutputArtifactPaths);

        var historyOutputPath = Path.Combine(_tempRootPath, created.Manifest.FolderName, "outputs", "0001-worker", "worker-output.md");
        Assert.Contains("## Deliverable", await File.ReadAllTextAsync(historyOutputPath), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsyncRestoresPreviousStatusWhenWorkerFails()
    {
        var store = new FileSystemTaskWorkspaceStore();
        var created = await store.CreateTaskAsync(_tempRootPath, new CreateTaskRequest
        {
            Title = "Fail worker",
            Summary = "Worker should fail cleanly.",
            TaskMarkdown = "# Task\nDo the work.",
        });

        var snapshot = await store.LoadTaskAsync(_tempRootPath, created.Manifest.Id);
        Assert.NotNull(snapshot);

        var approvedManifest = snapshot!.Manifest with
        {
            Status = TaskLifecycleState.WorkApproved,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
        await store.SaveTaskAsync(_tempRootPath, approvedManifest, snapshot.TaskMarkdown);

        snapshot = await store.LoadTaskAsync(_tempRootPath, created.Manifest.Id);
        Assert.NotNull(snapshot);

        var workflow = new TaskWorkerWorkflow(
            store,
            new ArtifactReferenceResolver(),
            new WorkerPromptFactory(),
            [new FailingProviderAdapter()]);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await workflow.RunAsync(_tempRootPath, snapshot!, CreateProvider(), CreateModel(), "client-secret"));

        var reloaded = await store.LoadTaskAsync(_tempRootPath, created.Manifest.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(TaskLifecycleState.WorkApproved, reloaded!.Manifest.Status);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRootPath))
        {
            Directory.Delete(_tempRootPath, recursive: true);
        }
    }

    private static ProviderRef CreateProvider()
    {
        return new ProviderRef
        {
            ProviderId = "openai",
            DisplayName = "OpenAI via Gateway",
            BaseUrl = "http://localhost:3000",
        };
    }

    private static ModelRef CreateModel()
    {
        return new ModelRef
        {
            ProviderId = "openai",
            ModelId = "gpt-5.4-mini",
            DisplayName = "GPT-5.4 Mini",
            Capabilities = new ModelCapabilities
            {
                SupportsTextInput = true,
            },
        };
    }

    private sealed class StubProviderAdapter : MultiAgentTaskSolver.Core.Abstractions.IProviderAdapter
    {
        public string ProviderId => "openai";

        public Task<IReadOnlyList<string>> GetModelsAsync(
            ProviderRef provider,
            string bearerToken,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ProviderTextResponse> SendTextAsync(
            ProviderRef provider,
            LlmRequest request,
            string bearerToken,
            CancellationToken cancellationToken = default)
        {
            Assert.Equal("client-secret", bearerToken);
            Assert.Contains("@policy", request.InputText, StringComparison.Ordinal);

            return Task.FromResult(new ProviderTextResponse
            {
                ProviderId = provider.ProviderId,
                ModelId = request.ModelId,
                OutputText = """
                ## Outcome Summary
                Draft complete.

                ## Deliverable
                First worker output.

                ## Open Questions
                None.

                ## Follow-up Notes
                Ready for critique.
                """,
                RawResponseBody = """
                {
                  "output_text": "## Outcome Summary\nDraft complete.\n\n## Deliverable\nFirst worker output.",
                  "usage": {
                    "input_tokens": 20,
                    "output_tokens": 10,
                    "total_tokens": 30
                  }
                }
                """,
                Usage = new UsageRecord
                {
                    ProviderId = provider.ProviderId,
                    ModelId = request.ModelId,
                    InputTokens = 20,
                    OutputTokens = 10,
                    TotalTokens = 30,
                    HttpStatusCode = (int)HttpStatusCode.OK,
                },
                HttpStatusCode = (int)HttpStatusCode.OK,
            });
        }

        public Task<IReadOnlyList<UsageRecord>> GetUsageAsync(
            ProviderRef provider,
            string bearerToken,
            UsageQuery query,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<TranscriptionResponse> TranscribeAsync(
            ProviderRef provider,
            TranscriptionRequest request,
            string bearerToken,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FailingProviderAdapter : MultiAgentTaskSolver.Core.Abstractions.IProviderAdapter
    {
        public string ProviderId => "openai";

        public Task<IReadOnlyList<string>> GetModelsAsync(
            ProviderRef provider,
            string bearerToken,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ProviderTextResponse> SendTextAsync(
            ProviderRef provider,
            LlmRequest request,
            string bearerToken,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Gateway rejected worker output.");
        }

        public Task<IReadOnlyList<UsageRecord>> GetUsageAsync(
            ProviderRef provider,
            string bearerToken,
            UsageQuery query,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<TranscriptionResponse> TranscribeAsync(
            ProviderRef provider,
            TranscriptionRequest request,
            string bearerToken,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
