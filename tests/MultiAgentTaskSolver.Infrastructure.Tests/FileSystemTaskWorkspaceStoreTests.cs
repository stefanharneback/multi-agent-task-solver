using MultiAgentTaskSolver.Core.Models;
using MultiAgentTaskSolver.Infrastructure.FileSystem;

namespace MultiAgentTaskSolver.Infrastructure.Tests;

public sealed class FileSystemTaskWorkspaceStoreTests : IDisposable
{
    private readonly string _tempRootPath = Path.Combine(Path.GetTempPath(), "mats-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task CreateTaskAsyncCreatesDefaultFoldersAndPreservesArbitrarySubfolders()
    {
        var store = new FileSystemTaskWorkspaceStore();

        var snapshot = await store.CreateTaskAsync(_tempRootPath, new CreateTaskRequest
        {
            Title = "Review rules",
            Summary = "Review current rules and notes.",
            TaskMarkdown = "# Task",
            AdditionalInputCategories = ["custom source"],
        });

        Assert.True(File.Exists(Path.Combine(snapshot.TaskRootPath, "task.json")));
        Assert.True(File.Exists(Path.Combine(snapshot.TaskRootPath, "task.md")));
        Assert.True(Directory.Exists(Path.Combine(snapshot.TaskRootPath, "inputs", "documents")));
        Assert.True(Directory.Exists(Path.Combine(snapshot.TaskRootPath, "inputs", "custom-source")));
        Assert.True(Directory.Exists(Path.Combine(snapshot.TaskRootPath, "runs")));
        Assert.True(Directory.Exists(Path.Combine(snapshot.TaskRootPath, "outputs")));
        Assert.True(Directory.Exists(Path.Combine(snapshot.TaskRootPath, "cache")));

        Directory.CreateDirectory(Path.Combine(snapshot.TaskRootPath, "inputs", "custom-source", "deep"));

        var reloaded = await store.LoadTaskAsync(_tempRootPath, snapshot.Manifest.Id);

        Assert.NotNull(reloaded);
        Assert.Contains(reloaded!.Tree, node => node.RelativePath == "inputs");
        Assert.Contains(
            Flatten(reloaded.Tree),
            node => node.RelativePath == "inputs/custom-source/deep" && node.IsDirectory);
    }

    [Fact]
    public async Task ImportArtifactAsyncCopiesFileAndUpdatesManifest()
    {
        var store = new FileSystemTaskWorkspaceStore();
        var snapshot = await store.CreateTaskAsync(_tempRootPath, new CreateTaskRequest
        {
            Title = "Analyze article",
            Summary = "Read imported article.",
            TaskMarkdown = "# Task",
        });

        var sourceFilePath = Path.Combine(_tempRootPath, "source.txt");
        await File.WriteAllTextAsync(sourceFilePath, "hello world");

        var artifact = await store.ImportArtifactAsync(_tempRootPath, snapshot.Manifest.Id, new ArtifactImportRequest
        {
            SourceFilePath = sourceFilePath,
            DestinationRelativeDirectory = Path.Combine("inputs", "articles"),
            Alias = "article-1",
        });

        var reloaded = await store.LoadTaskAsync(_tempRootPath, snapshot.Manifest.Id);

        Assert.Equal("article-1", artifact.Alias);
        Assert.NotNull(reloaded);
        Assert.Single(reloaded!.Manifest.Artifacts);
        Assert.Equal("inputs/articles/source.txt", reloaded.Manifest.Artifacts[0].RelativePath);
        Assert.True(File.Exists(Path.Combine(reloaded.TaskRootPath, "inputs", "articles", "source.txt")));
    }

    [Fact]
    public async Task ImportArtifactAsyncRejectsSiblingTaskPrefixEscape()
    {
        var store = new FileSystemTaskWorkspaceStore();
        var snapshot = await store.CreateTaskAsync(_tempRootPath, new CreateTaskRequest
        {
            Title = "Analyze article",
            Summary = "Read imported article.",
            TaskMarkdown = "# Task",
        });

        var sourceFilePath = Path.Combine(_tempRootPath, "source.txt");
        await File.WriteAllTextAsync(sourceFilePath, "hello world");

        var escapedDestination = Path.Combine("..", $"{snapshot.Manifest.FolderName}-escape", "artifacts");

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await store.ImportArtifactAsync(_tempRootPath, snapshot.Manifest.Id, new ArtifactImportRequest
            {
                SourceFilePath = sourceFilePath,
                DestinationRelativeDirectory = escapedDestination,
            });
        });
    }

    [Fact]
    public async Task SaveRunAsyncRejectsEscapedStepFilePaths()
    {
        var store = new FileSystemTaskWorkspaceStore();
        var snapshot = await store.CreateTaskAsync(_tempRootPath, new CreateTaskRequest
        {
            Title = "Review findings",
            Summary = "Persist a run safely.",
            TaskMarkdown = "# Task",
        });

        var run = new RunManifest
        {
            Id = "run-1",
            Title = "Worker iteration",
            Kind = "worker",
            Status = "planned",
            Sequence = 1,
            StartedAtUtc = DateTimeOffset.UtcNow,
            Steps =
            [
                new StepManifest
                {
                    Id = "step-1",
                    StepType = "worker",
                    Status = "planned",
                    Provider = new ProviderRef
                    {
                        ProviderId = "openai",
                        DisplayName = "OpenAI via Gateway",
                        BaseUrl = "http://localhost:3000",
                    },
                    Model = new ModelRef
                    {
                        ProviderId = "openai",
                        ModelId = "gpt-5.4-mini",
                        DisplayName = "GPT-5.4 Mini",
                    },
                    StepFilePath = Path.Combine("..", "..", "escaped-step.json"),
                },
            ],
        };

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await store.SaveRunAsync(_tempRootPath, snapshot.Manifest.Id, run);
        });
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRootPath))
        {
            Directory.Delete(_tempRootPath, recursive: true);
        }
    }

    private static IEnumerable<TaskTreeNode> Flatten(IEnumerable<TaskTreeNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;

            foreach (var child in Flatten(node.Children))
            {
                yield return child;
            }
        }
    }
}
