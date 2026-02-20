using Zebl.Application.Domain;

namespace Zebl.Application.Dtos.ReceiverLibrary;

public class ReceiverLibraryDto
{
    public Guid Id { get; set; }
    public string LibraryEntryName { get; set; } = null!;
    public ExportFormat ExportFormat { get; set; }
    public string? ClaimType { get; set; }

    // Submitter Information
    public int SubmitterType { get; set; }
    public string? BusinessOrLastName { get; set; }
    public string? FirstName { get; set; }
    public string? SubmitterId { get; set; }
    public string? ContactName { get; set; }
    public string? ContactType { get; set; }
    public string? ContactValue { get; set; }

    // Receiver Information
    public string? ReceiverName { get; set; }
    public string? ReceiverId { get; set; }

    // Header Information (ISA/GS)
    // ISA01-ISA04
    public string? AuthorizationInfoQualifier { get; set; } // ISA01
    public string? AuthorizationInfo { get; set; } // ISA02
    public string? SecurityInfoQualifier { get; set; } // ISA03
    public string? SecurityInfo { get; set; } // ISA04
    
    // ISA05-ISA08
    public string? SenderQualifier { get; set; } // ISA05
    public string? SenderId { get; set; } // ISA06
    public string? ReceiverQualifier { get; set; } // ISA07
    public string? InterchangeReceiverId { get; set; } // ISA08
    
    public bool AcknowledgeRequested { get; set; }
    public string? TestProdIndicator { get; set; }
    public string? SenderCode { get; set; }
    public string? ReceiverCode { get; set; }

    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
}
