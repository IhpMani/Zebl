namespace Zebl.Infrastructure.Persistence.Entities;

public class ClaimTemplate
{
    public int Id { get; set; }
    public string TemplateName { get; set; } = null!;

    public int? AvailableToPatientId { get; set; }
    public int? BillingProviderId { get; set; }
    public int? RenderingProviderId { get; set; }
    public int? ServiceFacilityId { get; set; }
    public int? ReferringProviderId { get; set; }
    public int? OrderingProviderId { get; set; }
    public int? SupervisingProviderId { get; set; }
}

