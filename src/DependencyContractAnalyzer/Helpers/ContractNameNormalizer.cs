namespace DependencyContractAnalyzer.Helpers;

internal static class ContractNameNormalizer
{
    public static string? Normalize(string? contractName)
    {
        if (string.IsNullOrWhiteSpace(contractName))
        {
            return null;
        }

        return contractName!.Trim();
    }
}
