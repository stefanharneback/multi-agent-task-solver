using System.Text;
using MultiAgentTaskSolver.Core;
using MultiAgentTaskSolver.Core.Abstractions;
using MultiAgentTaskSolver.Core.Models;

namespace MultiAgentTaskSolver.Infrastructure.Execution;

public sealed class WorkerPromptFactory : IWorkerPromptFactory
{
    public WorkerPromptPackage Create(TaskWorkspaceSnapshot snapshot, TaskReferenceResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(resolution);

        var builder = new StringBuilder();
        builder.AppendLine("# Task Metadata");
        builder.Append("- Title: ").AppendLine(snapshot.Manifest.Title);
        builder.Append("- Summary: ").AppendLine(snapshot.Manifest.Summary);
        builder.Append("- Status: ").AppendLine(snapshot.Manifest.Status.GetDisplayName());

        var latestReviewSummary = snapshot.Manifest.Runs
            .Where(static run => run.Kind == TaskRunKind.TaskReview)
            .OrderByDescending(static run => run.Sequence)
            .Select(static run => run.Summary)
            .FirstOrDefault(static summary => !string.IsNullOrWhiteSpace(summary));

        if (!string.IsNullOrWhiteSpace(latestReviewSummary))
        {
            builder.Append("- Latest review summary: ").AppendLine(latestReviewSummary);
        }

        var latestDecisionSummary = snapshot.Manifest.Runs
            .Where(static run => run.Kind == TaskRunKind.UserDecision)
            .OrderByDescending(static run => run.Sequence)
            .Select(static run => run.Summary)
            .FirstOrDefault(static summary => !string.IsNullOrWhiteSpace(summary));

        if (!string.IsNullOrWhiteSpace(latestDecisionSummary))
        {
            builder.Append("- Latest decision: ").AppendLine(latestDecisionSummary);
        }

        builder.AppendLine();
        builder.AppendLine("# Task Description");
        builder.AppendLine(snapshot.TaskMarkdown);

        if (resolution.ReferencedAliases.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("# Referenced Artifacts");

            foreach (var artifact in resolution.ResolvedArtifacts.OrderBy(static item => item.Alias, StringComparer.OrdinalIgnoreCase))
            {
                builder.AppendLine();
                builder.Append("## @").AppendLine(artifact.Alias);
                builder.Append("- Path: ").AppendLine(artifact.RelativePath);
                builder.Append("- Media type: ").AppendLine(artifact.MediaType);
                builder.Append("- Textual: ").AppendLine(artifact.IsTextual ? "yes" : "no");
                builder.AppendLine();
                builder.AppendLine("```text");
                builder.AppendLine(string.IsNullOrWhiteSpace(artifact.ContentExcerpt) ? "[no inline content]" : artifact.ContentExcerpt);
                builder.AppendLine("```");
            }
        }

        if (resolution.MissingAliases.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("# Missing Artifact References");
            foreach (var alias in resolution.MissingAliases.OrderBy(static item => item, StringComparer.OrdinalIgnoreCase))
            {
                builder.Append("- @").AppendLine(alias);
            }
        }

        return new WorkerPromptPackage
        {
            PromptVersion = "worker-v1",
            Instructions =
                """
                You are the worker agent for a multi-agent task solver.
                Execute the task directly and return a human-readable markdown deliverable.
                Be explicit when required inputs are missing, contradictory, or weak.
                Return markdown with these sections:
                ## Outcome Summary
                ## Deliverable
                ## Open Questions
                ## Follow-up Notes
                The Deliverable section should be ready to persist as the worker output artifact.
                """,
            InputText = builder.ToString().Trim(),
            ReferencedAliases = resolution.ReferencedAliases,
        };
    }
}
