using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace Zebl.Application.Dtos.Patients
{
    public class PatientListItemDto
    {
        [Required]
        public int PatID { get; set; }
        
        public string? PatFirstName { get; set; }
        
        public string? PatLastName { get; set; }
        
        public string? PatFullNameCC { get; set; }
        
        public DateTime PatDateTimeCreated { get; set; }
        
        public bool PatActive { get; set; }
        
        public string? PatAccountNo { get; set; }
        
        public DateOnly? PatBirthDate { get; set; }
        
        public string? PatSSN { get; set; }
        
        public string? PatSex { get; set; }
        
        public string? PatAddress { get; set; }
        
        public string? PatCity { get; set; }
        
        public string? PatState { get; set; }
        
        public string? PatZip { get; set; }
        
        public string? PatPhoneNo { get; set; }
        
        public string? PatCellPhoneNo { get; set; }
        
        public string? PatPriEmail { get; set; }
        
        public int PatBillingPhyFID { get; set; }
        
        public string? PatClassification { get; set; }
        
        public decimal? PatTotalBalanceCC { get; set; }
        
        public Dictionary<string, object?>? AdditionalColumns { get; set; }
    }
}
