using System.Text.RegularExpressions;
using MultiAgentTaskSolver.Core.Abstractions;
using MultiAgentTaskSolver.Core.Models;

namespace MultiAgentTaskSolver.Infrastructure.Execution;

public sealed partial class ArtifactReferenceResolver : IArtifactReferenceResolver
{
    private const int MaxInlineCharactersPerArtifact = 6_000;

    public async Task<TaskReferenceResolution> ResolveAsync(TaskWorkspaceSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var referencedAliases = AliasPattern()
            .Matches($"{snapshot.Manifest.Summary}\n{snapshot.TaskMarkdown}")
            .Select(match => match.Groups["alias"].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (referencedAliases.Length == 0)
        {
            return new TaskReferenceResolution();
        }

        var artifactsByAlias = snapshot.Manifest.Artifacts
            .Where(static artifact => !string.IsNullOrWhiteSpace(artifact.Alias))
            .GroupBy(artifact => artifact.Alias, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var resolvedArtifacts = new List<ResolvedArtifactReference>();
        var missingAliases = new List<string>();

        foreach (var alias in referencedAliases)
        {
            if (!artifactsByAlias.TryGetValue(alias, out var artifact))
            {
                missingAliases.Add(alias);
                continue;
            }

            resolvedArtifacts.Add(await ResolveArtifactAsync(snapshot.TaskRootPath, artifact, cancellationToken));
        }

        return new TaskReferenceResolution
        {
            ReferencedAliases = referencedAliases,
            ResolvedArtifacts = resolvedArtifacts,
            MissingAliases = missingAliases,
        };
    }

    private static async Task<ResolvedArtifactReference> ResolveArtifactAsync(
        string taskRootPath,
        ArtifactManifest artifact,
        CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(Path.Combine(taskRootPath, artifact.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
        var isTextual = IsTextualArtifact(artifact);

        if (!isTextual || !File.Exists(fullPath))
        {
            return new ResolvedArtifactReference
            {
                Alias = artifact.Alias,
                DisplayName = artifact.DisplayName,
                RelativePath = artifact.RelativePath,
                MediaType = artifact.MediaType,
                IsTextual = false,
                CharacterCount = null,
                ContentExcerpt = "Binary or non-inline artifact. Use the relative path and metadata only.",
            };
        }

        var content = await File.ReadAllTextAsync(fullPath, cancellationToken);
        var wasTruncated = content.Length > MaxInlineCharactersPerArtifact;
        var excerpt = wasTruncated
            ? $"{content[..MaxInlineCharactersPerArtifact]}\n\n[truncated]"
            : content;

        return new ResolvedArtifactReference
        {
            Alias = artifact.Alias,
            DisplayName = artifact.DisplayName,
            RelativePath = artifact.RelativePath,
            MediaType = artifact.MediaType,
            IsTextual = true,
            WasTruncated = wasTruncated,
            CharacterCount = content.Length,
            ContentExcerpt = excerpt,
        };
    }

    private static bool IsTextualArtifact(ArtifactManifest artifact)
    {
        return artifact.MediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
            || string.Equals(artifact.MediaType, "application/json", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex("@(?<alias>[a-z0-9][a-z0-9-]*)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AliasPattern();
}
