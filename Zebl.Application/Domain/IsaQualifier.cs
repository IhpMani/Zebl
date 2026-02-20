namespace Zebl.Application.Domain;

/// <summary>
/// Helper class for common ISA qualifier values. These are NOT enforced - database stores raw qualifier strings.
/// </summary>
public static class IsaQualifier
{
    public const string Duns = "01";
    public const string FederalTaxId = "30";
    public const string Naic = "33";
    public const string MutuallyDefined = "ZZ";
}
