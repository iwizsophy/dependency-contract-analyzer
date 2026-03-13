namespace DependencyContractAnalyzer.Helpers;

internal static class ContractNameFormat
{
    public static bool IsLowerKebabCase(string contractName)
    {
        // DCA101 intentionally allows only lowercase ASCII letters, digits, and
        // single interior hyphens so contract names stay stable across APIs/docs.
        var previousWasHyphen = false;

        for (var index = 0; index < contractName.Length; index++)
        {
            var current = contractName[index];
            if ((current >= 'a' && current <= 'z') || (current >= '0' && current <= '9'))
            {
                previousWasHyphen = false;
                continue;
            }

            if (current == '-' && index > 0 && index < contractName.Length - 1 && !previousWasHyphen)
            {
                previousWasHyphen = true;
                continue;
            }

            return false;
        }

        return contractName.Length > 0 && !previousWasHyphen;
    }
}
