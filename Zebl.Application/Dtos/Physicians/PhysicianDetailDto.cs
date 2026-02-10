namespace Zebl.Application.Dtos.Physicians;

public class PhysicianDetailDto
{
    public int PhyID { get; set; }
    public string? PhyName { get; set; }
    public string? PhyPrimaryCodeType { get; set; }
    public string PhyType { get; set; } = null!;
    public string? PhyLastName { get; set; }
    public string? PhyFirstName { get; set; }
    public string? PhyMiddleName { get; set; }
    public string? PhyAddress1 { get; set; }
    public string? PhyAddress2 { get; set; }
    public string? PhyCity { get; set; }
    public string? PhyState { get; set; }
    public string? PhyZip { get; set; }
    public string? PhyTelephone { get; set; }
    public string? PhyFax { get; set; }
    public string? PhyEMail { get; set; }
    public string? PhySpecialtyCode { get; set; }
    public bool PhyInactive { get; set; }
    public string? PhyNPI { get; set; }
    public string? PhyEntityType { get; set; }
    public string? PhyPrimaryIDCode { get; set; }
    public DateTime PhyDateTimeCreated { get; set; }
    public DateTime PhyDateTimeModified { get; set; }
}
