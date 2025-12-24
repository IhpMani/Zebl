using System;
using System.Collections.Generic;

namespace Zebl.Infrastructure.Persistence.Entities;

public partial class Physician
{
    public int PhyID { get; set; }

    public DateTime PhyDateTimeCreated { get; set; }

    public DateTime PhyDateTimeModified { get; set; }

    public Guid? PhyCreatedUserGUID { get; set; }

    public Guid? PhyLastUserGUID { get; set; }

    public string? PhyCreatedUserName { get; set; }

    public string? PhyLastUserName { get; set; }

    public string? PhyCreatedComputerName { get; set; }

    public string? PhyLastComputerName { get; set; }

    public string? PhyAddress1 { get; set; }

    public string? PhyAddress2 { get; set; }

    public string? PhyCity { get; set; }

    public string? PhyEMail { get; set; }

    public string? PhyEntityType { get; set; }

    public string? PhyFax { get; set; }

    public string? PhyFirstName { get; set; }

    public bool PhyInactive { get; set; }

    public string? PhyLastName { get; set; }

    public string? PhyMiddleName { get; set; }

    public string? PhyName { get; set; }

    public string? PhyNotes { get; set; }

    public string? PhyNPI { get; set; }

    public string? PhyPrimaryCodeType { get; set; }

    public string? PhyPrimaryIDCode { get; set; }

    public string? PhyRateClass { get; set; }

    public bool PhySignatureOnFile { get; set; }

    public string? PhySpecialtyCode { get; set; }

    public string? PhyState { get; set; }

    public string? PhySuffix { get; set; }

    public string? PhyTelephone { get; set; }

    public string PhyType { get; set; } = null!;

    public string? PhyZip { get; set; }

    public string PhyFirstMiddleLastNameCC { get; set; } = null!;

    public string PhyFullNameCC { get; set; } = null!;

    public string PhyNameWithInactiveCC { get; set; } = null!;

    public string PhyCityStateZipCC { get; set; } = null!;

    public virtual ICollection<Claim> ClaimClaAttendingPhyFs { get; set; } = new List<Claim>();

    public virtual ICollection<Claim> ClaimClaBillingPhyFs { get; set; } = new List<Claim>();

    public virtual ICollection<Claim> ClaimClaFacilityPhyFs { get; set; } = new List<Claim>();

    public virtual ICollection<Claim> ClaimClaOperatingPhyFs { get; set; } = new List<Claim>();

    public virtual ICollection<Claim> ClaimClaOrderingPhyFs { get; set; } = new List<Claim>();

    public virtual ICollection<Claim> ClaimClaReferringPhyFs { get; set; } = new List<Claim>();

    public virtual ICollection<Claim> ClaimClaRenderingPhyFs { get; set; } = new List<Claim>();

    public virtual ICollection<Claim> ClaimClaSupervisingPhyFs { get; set; } = new List<Claim>();

    public virtual ICollection<Patient> PatientPatBillingPhyFs { get; set; } = new List<Patient>();

    public virtual ICollection<Patient> PatientPatFacilityPhyFs { get; set; } = new List<Patient>();

    public virtual ICollection<Patient> PatientPatOrderingPhyFs { get; set; } = new List<Patient>();

    public virtual ICollection<Patient> PatientPatReferringPhyFs { get; set; } = new List<Patient>();

    public virtual ICollection<Patient> PatientPatRenderingPhyFs { get; set; } = new List<Patient>();

    public virtual ICollection<Patient> PatientPatSupervisingPhyFs { get; set; } = new List<Patient>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ICollection<Procedure_Code> Procedure_Codes { get; set; } = new List<Procedure_Code>();
}
