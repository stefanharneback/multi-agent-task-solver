using System.Text;
using MultiAgentTaskSolver.Core;
using MultiAgentTaskSolver.Core.Abstractions;
using MultiAgentTaskSolver.Core.Models;

namespace MultiAgentTaskSolver.Infrastructure.Execution;

public sealed class ReviewPromptFactory : IReviewPromptFactory
{
    public ReviewPromptPackage Create(TaskWorkspaceSnapshot snapshot, TaskReferenceResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(resolution);

        var builder = new StringBuilder();
        builder.AppendLine("# Task Metadata");
        builder.Append("- Title: ").AppendLine(snapshot.Manifest.Title);
        builder.Append("- Summary: ").AppendLine(snapshot.Manifest.Summary);
        builder.Append("- Status: ").AppendLine(snapshot.Manifest.Status.GetDisplayName());
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
                if (artifact.CharacterCount is not null)
                {
                    builder.Append("- Characters: ").AppendLine(artifact.CharacterCount.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }

                builder.AppendLine();
                builder.AppendLine("```text");
                builder.AppendLine(string.IsNullOrWhiteSpace(artifact.ContentExcerpt) ? "[no inline content]" : artifact.ContentExcerpt);
                builder.AppendLine("```");
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
        }

        return new ReviewPromptPackage
        {
            PromptVersion = "task-review-v1",
            Instructions =
                """
                You are the task review agent for a multi-agent task solver.
                Be critical and specific.
                Review whether the task is clear enough to hand over to a worker agent.
                Return markdown with these sections:
                ## Readiness Verdict
                ## Strengths
                ## Gaps And Risks
                ## Missing Inputs
                ## Recommended Revisions
                ## Suggested Next Step
                Call out missing or unresolved @alias references explicitly.
                """,
            InputText = builder.ToString().Trim(),
            ReferencedAliases = resolution.ReferencedAliases,
        };
    }
}
