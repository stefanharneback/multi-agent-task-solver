using MultiAgentTaskSolver.Core.Models;
using MultiAgentTaskSolver.Infrastructure.Execution;

namespace MultiAgentTaskSolver.Infrastructure.Tests;

public sealed class ReviewPromptFactoryTests
{
    [Fact]
    public void CreateBuildsReviewPromptWithTaskAndArtifactSections()
    {
        var snapshot = new TaskWorkspaceSnapshot
        {
            TaskRootPath = "C:\\tasks\\Task-1",
            Manifest = new TaskManifest
            {
                Id = "task-1",
                FolderName = "Task-task-1",
                Title = "Review policy",
                Summary = "Review attached policy.",
                Status = TaskLifecycleState.UnderReview,
            },
            TaskMarkdown = "# Task\nReview @policy.",
        };

        var resolution = new TaskReferenceResolution
        {
            ReferencedAliases = ["policy", "missing"],
            ResolvedArtifacts =
            [
                new ResolvedArtifactReference
                {
                    Alias = "policy",
                    RelativePath = "inputs/documents/policy.md",
                    MediaType = "text/markdown",
                    IsTextual = true,
                    ContentExcerpt = "Policy text",
                },
            ],
            MissingAliases = ["missing"],
        };

        var factory = new ReviewPromptFactory();
        var prompt = factory.Create(snapshot, resolution);

        Assert.Equal("task-review-v1", prompt.PromptVersion);
        Assert.Contains("## Readiness Verdict", prompt.Instructions, StringComparison.Ordinal);
        Assert.Contains("# Task Metadata", prompt.InputText, StringComparison.Ordinal);
        Assert.Contains("## @policy", prompt.InputText, StringComparison.Ordinal);
        Assert.Contains("@missing", prompt.InputText, StringComparison.Ordinal);
    }
}
