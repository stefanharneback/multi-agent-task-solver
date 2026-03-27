using System.Net;
using MultiAgentTaskSolver.Core.Models;
using MultiAgentTaskSolver.Infrastructure.Execution;
using MultiAgentTaskSolver.Infrastructure.FileSystem;

namespace MultiAgentTaskSolver.Infrastructure.Tests;

public sealed class TaskReviewWorkflowTests : IDisposable
{
    private readonly string _tempRootPath = Path.Combine(Path.GetTempPath(), "mats-review-workflow-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task RunAsyncPersistsCompletedReviewRunAndArtifacts()
    {
        var store = new FileSystemTaskWorkspaceStore();
        var created = await store.CreateTaskAsync(_tempRootPath, new CreateTaskRequest
        {
            Title = "Review policy task",
            Summary = "Review the draft policy.",
            TaskMarkdown = "# Task\nPlease review @policy and say if the task is ready.",
        });

        var sourceFilePath = Path.Combine(_tempRootPath, "policy.md");
        await File.WriteAllTextAsync(sourceFilePath, "Policy draft content.");
        await store.ImportArtifactAsync(_tempRootPath, created.Manifest.Id, new ArtifactImportRequest
        {
            SourceFilePath = sourceFilePath,
            DestinationRelativeDirectory = Path.Combine("inputs", "documents"),
            Alias = "policy",
        });

        var snapshot = await store.LoadTaskAsync(_tempRootPath, created.Manifest.Id);
        Assert.NotNull(snapshot);

        var workflow = new TaskReviewWorkflow(
            store,
            new ArtifactReferenceResolver(),
            new ReviewPromptFactory(),
            [new StubProviderAdapter()]);

        var provider = new ProviderRef
        {
            ProviderId = "openai",
            DisplayName = "OpenAI via Gateway",
            BaseUrl = "http://localhost:3000",
        };

        var model = new ModelRef
        {
            ProviderId = "openai",
            ModelId = "gpt-5.4-mini",
            DisplayName = "GPT-5.4 Mini",
            Capabilities = new ModelCapabilities
            {
                SupportsTextInput = true,
            },
        };

        var result = await workflow.RunAsync(_tempRootPath, snapshot!, provider, model, "client-secret");
        var reloaded = await store.LoadTaskAsync(_tempRootPath, created.Manifest.Id);

        Assert.NotNull(reloaded);
        Assert.Equal(TaskLifecycleState.ReviewReady, reloaded!.Manifest.Status);
        Assert.Single(reloaded.Manifest.Runs);
        Assert.Equal(TaskRunStatus.Completed, reloaded.Manifest.Runs[0].Status);
        Assert.Equal(TaskStepStatus.Completed, reloaded.Manifest.Runs[0].Steps[0].Status);
        Assert.Contains("Task is review-ready", result.OutputText, StringComparison.Ordinal);

        var stepDirectoryPath = Path.Combine(reloaded.TaskRootPath, "runs", "0001-task-review", "01-task-review");
        Assert.Contains("task-review-v1", await File.ReadAllTextAsync(Path.Combine(stepDirectoryPath, "prompt.md")), StringComparison.Ordinal);
        Assert.Contains("Task is review-ready", await File.ReadAllTextAsync(Path.Combine(stepDirectoryPath, "response.md")), StringComparison.Ordinal);
        Assert.Contains("\"totalTokens\": 18", await File.ReadAllTextAsync(Path.Combine(stepDirectoryPath, "usage.json")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsyncPreservesConcurrentTaskMarkdownEditsOnSuccess()
    {
        var store = new FileSystemTaskWorkspaceStore();
        var created = await store.CreateTaskAsync(_tempRootPath, new CreateTaskRequest
        {
            Title = "Concurrent edit task",
            Summary = "Preserve edits while reviewing.",
            TaskMarkdown = "# Task\nOriginal content.",
        });

        var snapshot = await store.LoadTaskAsync(_tempRootPath, created.Manifest.Id);
        Assert.NotNull(snapshot);

        var workflow = new TaskReviewWorkflow(
            store,
            new ArtifactReferenceResolver(),
            new ReviewPromptFactory(),
            [new ConcurrentEditProviderAdapter(async () =>
            {
                var reloaded = await store.LoadTaskAsync(_tempRootPath, created.Manifest.Id);
                Assert.NotNull(reloaded);
                var editedManifest = reloaded!.Manifest with
                {
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                };

                await store.SaveTaskAsync(_tempRootPath, editedManifest, "# Task\nEdited while review was running.");
            })]);

        var result = await workflow.RunAsync(_tempRootPath, snapshot!, CreateProvider(), CreateModel(), "client-secret");
        var reloadedAfterRun = await store.LoadTaskAsync(_tempRootPath, created.Manifest.Id);

        Assert.NotNull(reloadedAfterRun);
        Assert.Equal(TaskLifecycleState.ReviewReady, reloadedAfterRun!.Manifest.Status);
        Assert.Equal("# Task\nEdited while review was running.", reloadedAfterRun.TaskMarkdown);
        Assert.Contains("Task is review-ready", result.OutputText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsyncRestoresPreviousStatusWhenReviewFails()
    {
        var store = new FileSystemTaskWorkspaceStore();
        var created = await store.CreateTaskAsync(_tempRootPath, new CreateTaskRequest
        {
            Title = "Failure restore task",
            Summary = "Restore previous state on failure.",
            TaskMarkdown = "# Task\nNeeds review.",
        });

        var updatedManifest = created.Manifest with
        {
            Status = TaskLifecycleState.ReviewReady,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
        await store.SaveTaskAsync(_tempRootPath, updatedManifest, created.TaskMarkdown);

        var snapshot = await store.LoadTaskAsync(_tempRootPath, created.Manifest.Id);
        Assert.NotNull(snapshot);

        var workflow = new TaskReviewWorkflow(
            store,
            new ArtifactReferenceResolver(),
            new ReviewPromptFactory(),
            [new FailingProviderAdapter()]);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await workflow.RunAsync(_tempRootPath, snapshot!, CreateProvider(), CreateModel(), "client-secret"));

        var reloaded = await store.LoadTaskAsync(_tempRootPath, created.Manifest.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(TaskLifecycleState.ReviewReady, reloaded!.Manifest.Status);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRootPath))
        {
            Directory.Delete(_tempRootPath, recursive: true);
        }
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
                OutputText = "Task is review-ready, but acceptance criteria should be sharper.",
                RawResponseBody = """
                {
                  "output_text": "Task is review-ready, but acceptance criteria should be sharper.",
                  "usage": {
                    "input_tokens": 12,
                    "output_tokens": 6,
                    "total_tokens": 18
                  }
                }
                """,
                Usage = new UsageRecord
                {
                    ProviderId = provider.ProviderId,
                    ModelId = request.ModelId,
                    InputTokens = 12,
                    OutputTokens = 6,
                    TotalTokens = 18,
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

    private sealed class ConcurrentEditProviderAdapter : MultiAgentTaskSolver.Core.Abstractions.IProviderAdapter
    {
        private readonly Func<Task> _beforeReturn;

        public ConcurrentEditProviderAdapter(Func<Task> beforeReturn)
        {
            _beforeReturn = beforeReturn;
        }

        public string ProviderId => "openai";

        public Task<IReadOnlyList<string>> GetModelsAsync(
            ProviderRef provider,
            string bearerToken,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public async Task<ProviderTextResponse> SendTextAsync(
            ProviderRef provider,
            LlmRequest request,
            string bearerToken,
            CancellationToken cancellationToken = default)
        {
            await _beforeReturn();
            return BuildSuccessfulResponse(provider, request);
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
            throw new InvalidOperationException("Gateway unavailable.");
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

    private static ProviderTextResponse BuildSuccessfulResponse(ProviderRef provider, LlmRequest request)
    {
        return new ProviderTextResponse
        {
            ProviderId = provider.ProviderId,
            ModelId = request.ModelId,
            OutputText = "Task is review-ready, but acceptance criteria should be sharper.",
            RawResponseBody = """
            {
              "output_text": "Task is review-ready, but acceptance criteria should be sharper.",
              "usage": {
                "input_tokens": 12,
                "output_tokens": 6,
                "total_tokens": 18
              }
            }
            """,
            Usage = new UsageRecord
            {
                ProviderId = provider.ProviderId,
                ModelId = request.ModelId,
                InputTokens = 12,
                OutputTokens = 6,
                TotalTokens = 18,
                HttpStatusCode = (int)HttpStatusCode.OK,
            },
            HttpStatusCode = (int)HttpStatusCode.OK,
        };
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
}
