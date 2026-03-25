using System.Globalization;
using System.Text.Json;
using MultiAgentTaskSolver.Core.Models;

namespace MultiAgentTaskSolver.Core.Tests;

public sealed class TaskManifestTests
{
    [Fact]
    public void TaskManifestRoundTripsWithArtifactsAndRuns()
    {
        var manifest = new TaskManifest
        {
            Id = "task-1",
            FolderName = "Task-task-1",
            Title = "Draft policy summary",
            Slug = "draft-policy-summary",
            Summary = "Summarize the attached policy files.",
            Status = "draft",
            CreatedAtUtc = DateTimeOffset.Parse("2026-03-23T20:00:00Z", CultureInfo.InvariantCulture),
            UpdatedAtUtc = DateTimeOffset.Parse("2026-03-23T20:30:00Z", CultureInfo.InvariantCulture),
            InputCategories = ["documents", "notes"],
            Artifacts =
            [
                new ArtifactManifest
                {
                    Id = "artifact-1",
                    Alias = "policy",
                    DisplayName = "policy.md",
                    Category = "inputs/documents",
                    RelativePath = "inputs/documents/policy.md",
                    MediaType = "text/markdown",
                    Sha256 = "abc",
                    SizeBytes = 123,
                    ImportedAtUtc = DateTimeOffset.Parse("2026-03-23T20:10:00Z", CultureInfo.InvariantCulture),
                },
            ],
            Runs =
            [
                new RunManifest
                {
                    Id = "run-1",
                    Title = "Review draft",
                    Kind = "review",
                    Status = "planned",
                    Sequence = 1,
                    StartedAtUtc = DateTimeOffset.Parse("2026-03-23T20:15:00Z", CultureInfo.InvariantCulture),
                    Steps =
                    [
                        new StepManifest
                        {
                            Id = "step-1",
                            StepType = "task-review",
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
                            RelativeDirectory = "runs/0001-review/01-task-review",
                        },
                    ],
                },
            ],
        };

        var json = JsonSerializer.Serialize(manifest);
        var roundTripped = JsonSerializer.Deserialize<TaskManifest>(json);

        Assert.NotNull(roundTripped);
        Assert.Equal(manifest.Title, roundTripped!.Title);
        Assert.Single(roundTripped.Artifacts);
        Assert.Single(roundTripped.Runs);
        Assert.Equal("policy", roundTripped.Artifacts[0].Alias);
        Assert.Equal("task-review", roundTripped.Runs[0].Steps[0].StepType);
    }
}
