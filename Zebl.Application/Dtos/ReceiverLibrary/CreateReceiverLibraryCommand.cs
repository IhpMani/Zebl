using System.ComponentModel.DataAnnotations;
using Zebl.Application.Domain;

namespace Zebl.Application.Dtos.ReceiverLibrary;

public class CreateReceiverLibraryCommand
{
    [Required(ErrorMessage = "LibraryEntryName is required")]
    [MaxLength(255)]
    public string LibraryEntryName { get; set; } = null!;

    [Required(ErrorMessage = "ExportFormat is required")]
    public ExportFormat ExportFormat { get; set; }

    [MaxLength(100)]
    public string? ClaimType { get; set; }

    // Submitter Information
    public int SubmitterType { get; set; }
    
    [MaxLength(255)]
    public string? BusinessOrLastName { get; set; }
    
    [MaxLength(255)]
    public string? FirstName { get; set; }
    
    [MaxLength(100)]
    public string? SubmitterId { get; set; }
    
    [MaxLength(255)]
    public string? ContactName { get; set; }
    
    [MaxLength(50)]
    public string? ContactType { get; set; }
    
    [MaxLength(255)]
    public string? ContactValue { get; set; }

    // Receiver Information
    [MaxLength(255)]
    public string? ReceiverName { get; set; }
    
    [MaxLength(100)]
    public string? ReceiverId { get; set; }

    // Header Information (ISA/GS)
    // ISA01-ISA04
    [MaxLength(2)]
    public string? AuthorizationInfoQualifier { get; set; } // ISA01
    
    [MaxLength(10)]
    public string? AuthorizationInfo { get; set; } // ISA02
    
    [MaxLength(2)]
    public string? SecurityInfoQualifier { get; set; } // ISA03
    
    [MaxLength(10)]
    public string? SecurityInfo { get; set; } // ISA04
    
    // ISA05-ISA08
    [MaxLength(2)]
    public string? SenderQualifier { get; set; } // ISA05
    
    [MaxLength(15)]
    public string? SenderId { get; set; } // ISA06
    
    [MaxLength(2)]
    public string? ReceiverQualifier { get; set; } // ISA07
    
    [MaxLength(15)]
    public string? InterchangeReceiverId { get; set; } // ISA08
    
    public bool AcknowledgeRequested { get; set; }
    
    [MaxLength(1)]
    public string? TestProdIndicator { get; set; }
    
    [MaxLength(50)]
    public string? SenderCode { get; set; }
    
    [MaxLength(50)]
    public string? ReceiverCode { get; set; }

    public bool IsActive { get; set; } = true;
}
