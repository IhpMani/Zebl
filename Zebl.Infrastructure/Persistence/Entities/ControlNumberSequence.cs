namespace Zebl.Infrastructure.Persistence.Entities;

public class ControlNumberSequence : ITenantFacilityEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int FacilityId { get; set; }
    public long LastInterchangeNumber { get; set; }
    public long LastGroupNumber { get; set; }
    public long LastTransactionNumber { get; set; }
}
