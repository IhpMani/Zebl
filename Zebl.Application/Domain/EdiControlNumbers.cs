namespace Zebl.Application.Domain;

public sealed class EdiControlNumbers
{
    public string InterchangeControlNumber { get; set; } = string.Empty; // ISA13
    public string GroupControlNumber { get; set; } = string.Empty; // GS06
    public string TransactionControlNumber { get; set; } = string.Empty; // ST02
}
