namespace DependencyContractAnalyzer.Helpers;

internal static class ContractNameNormalizer
{
    public static string? Normalize(string? contractName)
    {
        // Empty or whitespace-only names are treated as missing so diagnostics can
        // distinguish invalid declarations from normalized case-insensitive matches.
        if (string.IsNullOrWhiteSpace(contractName))
        {
            return null;
        }

        return contractName!.Trim();
    }
}
