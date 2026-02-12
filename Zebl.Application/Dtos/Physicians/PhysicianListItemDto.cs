using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace Zebl.Application.Dtos.Physicians
{
    public class PhysicianListItemDto
    {
        [Required]
        public int PhyID { get; set; }
        
        public DateTime PhyDateTimeCreated { get; set; }
        
        public string? PhyFirstName { get; set; }
        
        public string? PhyLastName { get; set; }
        
        public string? PhyFullNameCC { get; set; }
        
        public string? PhyName { get; set; }
        
        public string PhyType { get; set; } = null!;
        
        public string? PhyRateClass { get; set; }
        
        public string? PhyNPI { get; set; }
        
        public string? PhySpecialtyCode { get; set; }
        
        public string? PhyPrimaryCodeType { get; set; }
        
        public string? PhyAddress1 { get; set; }
        
        public string? PhyCity { get; set; }
        
        public string? PhyState { get; set; }
        
        public string? PhyZip { get; set; }
        
        public string? PhyTelephone { get; set; }
        
        public bool PhyInactive { get; set; }
        
        public Dictionary<string, object?>? AdditionalColumns { get; set; }
    }
}
