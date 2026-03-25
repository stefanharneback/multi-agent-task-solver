using MultiAgentTaskSolver.Core.Models;
using MultiAgentTaskSolver.Infrastructure.Execution;

namespace MultiAgentTaskSolver.Infrastructure.Tests;

public sealed class ArtifactReferenceResolverTests : IDisposable
{
    private readonly string _tempRootPath = Path.Combine(Path.GetTempPath(), "mats-reference-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ResolveAsyncResolvesReferencedAliasesAndMissingReferences()
    {
        var taskRootPath = Path.Combine(_tempRootPath, "Task-task-1");
        Directory.CreateDirectory(Path.Combine(taskRootPath, "inputs", "documents"));
        await File.WriteAllTextAsync(Path.Combine(taskRootPath, "inputs", "documents", "policy.md"), "Policy text");

        var snapshot = new TaskWorkspaceSnapshot
        {
            TaskRootPath = taskRootPath,
            Manifest = new TaskManifest
            {
                Id = "task-1",
                FolderName = "Task-task-1",
                Title = "Review policy",
                Summary = "Review @policy and check @missing.",
                Artifacts =
                [
                    new ArtifactManifest
                    {
                        Id = "artifact-1",
                        Alias = "policy",
                        DisplayName = "policy.md",
                        RelativePath = "inputs/documents/policy.md",
                        MediaType = "text/markdown",
                    },
                ],
            },
            TaskMarkdown = "Use @policy for the task.",
        };

        var resolver = new ArtifactReferenceResolver();
        var resolution = await resolver.ResolveAsync(snapshot);

        Assert.Equal(["policy", "missing"], resolution.ReferencedAliases);
        Assert.Single(resolution.ResolvedArtifacts);
        Assert.Equal("policy", resolution.ResolvedArtifacts[0].Alias);
        Assert.Contains("Policy text", resolution.ResolvedArtifacts[0].ContentExcerpt, StringComparison.Ordinal);
        Assert.Equal(["missing"], resolution.MissingAliases);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRootPath))
        {
            Directory.Delete(_tempRootPath, recursive: true);
        }
    }
}
