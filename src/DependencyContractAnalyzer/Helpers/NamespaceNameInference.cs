using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;

namespace DependencyContractAnalyzer.Helpers;

internal static class NamespaceNameInference
{
    public static ImmutableArray<string> InferNames(INamespaceSymbol namespaceSymbol, int maxSegments)
    {
        if (namespaceSymbol.IsGlobalNamespace ||
            maxSegments < 1)
        {
            return ImmutableArray<string>.Empty;
        }

        var normalizedSegments = ImmutableArray.CreateBuilder<string?>(maxSegments);
        for (var current = namespaceSymbol;
             !current.IsGlobalNamespace && normalizedSegments.Count < maxSegments;
             current = current.ContainingNamespace)
        {
            // Collect leaf-first namespace segments so later passes can emit both
            // single-segment and trailing multi-segment fallback names.
            normalizedSegments.Add(
                string.IsNullOrWhiteSpace(current.Name)
                    ? null
                    : ConvertIdentifierToKebabCase(current.Name));
        }

        if (normalizedSegments.Count == 0)
        {
            return ImmutableArray<string>.Empty;
        }

        var inferredNames = ImmutableHashSet.CreateBuilder<string>();
        for (var segmentCount = 1; segmentCount <= normalizedSegments.Count; segmentCount++)
        {
            var segments = ImmutableArray.CreateBuilder<string>(segmentCount);
            var hasInvalidSegment = false;

            for (var index = segmentCount - 1; index >= 0; index--)
            {
                if (normalizedSegments[index] is not { } segment)
                {
                    hasInvalidSegment = true;
                    break;
                }

                segments.Add(segment);
            }

            if (hasInvalidSegment)
            {
                // If any segment cannot be normalized to kebab-case, skip only that
                // candidate name and keep evaluating shorter alternatives.
                continue;
            }

            inferredNames.Add(string.Join("-", segments));
        }

        return inferredNames.ToImmutableArray();
    }

    private static string? ConvertIdentifierToKebabCase(string segment)
    {
        var builder = new StringBuilder(segment.Length * 2);

        for (var index = 0; index < segment.Length; index++)
        {
            var current = segment[index];
            if (char.IsLower(current) || char.IsDigit(current))
            {
                builder.Append(current);
                continue;
            }

            if (!char.IsUpper(current))
            {
                return null;
            }

            if (index > 0)
            {
                var previous = segment[index - 1];
                var nextIsLower = index < segment.Length - 1 && char.IsLower(segment[index + 1]);
                // Treat acronym-to-word boundaries like "ReadModel" -> "read-model"
                // without splitting runs such as "IO" into "i-o".
                if (char.IsLower(previous) ||
                    char.IsDigit(previous) ||
                    (char.IsUpper(previous) && nextIsLower))
                {
                    builder.Append('-');
                }
            }

            builder.Append(char.ToLowerInvariant(current));
        }

        return builder.Length == 0 ? null : builder.ToString();
    }
}
