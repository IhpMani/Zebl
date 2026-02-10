using System.ComponentModel.DataAnnotations;

namespace Zebl.Application.Dtos.Physicians;

public class UpdatePhysicianDto
{
    [Required]
    [MaxLength(100)]
    public string PhyName { get; set; } = null!;
    
    [MaxLength(2)]
    public string? PhyPrimaryCodeType { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string PhyType { get; set; } = null!;
    
    [MaxLength(60)]
    public string? PhyLastName { get; set; }
    
    [MaxLength(35)]
    public string? PhyFirstName { get; set; }
    
    [MaxLength(25)]
    public string? PhyMiddleName { get; set; }
    
    [MaxLength(55)]
    public string? PhyAddress1 { get; set; }
    
    [MaxLength(55)]
    public string? PhyAddress2 { get; set; }
    
    [MaxLength(50)]
    public string? PhyCity { get; set; }
    
    [MaxLength(2)]
    public string? PhyState { get; set; }
    
    [MaxLength(15)]
    public string? PhyZip { get; set; }
    
    [MaxLength(80)]
    public string? PhyTelephone { get; set; }
    
    [MaxLength(80)]
    public string? PhyFax { get; set; }
    
    [MaxLength(80)]
    public string? PhyEMail { get; set; }
    
    [MaxLength(30)]
    public string? PhySpecialtyCode { get; set; }
    
    public bool PhyInactive { get; set; }
    
    [MaxLength(20)]
    public string? PhyNPI { get; set; }
    
    [MaxLength(1)]
    public string? PhyEntityType { get; set; }
    
    [MaxLength(80)]
    public string? PhyPrimaryIDCode { get; set; }
}
