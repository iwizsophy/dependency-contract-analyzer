using System.Text;
using Microsoft.CodeAnalysis;

namespace DependencyContractAnalyzer.Helpers;

internal static class NamespaceNameInference
{
    public static string? InferName(INamespaceSymbol namespaceSymbol)
    {
        if (namespaceSymbol.IsGlobalNamespace ||
            string.IsNullOrWhiteSpace(namespaceSymbol.Name))
        {
            return null;
        }

        return ConvertIdentifierToKebabCase(namespaceSymbol.Name);
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
