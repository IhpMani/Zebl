using System;
using System.Collections.Generic;

namespace Zebl.Infrastructure.Persistence.Entities;

public partial class Insured
{
    public Guid InsGUID { get; set; }

    public string? InsFirstName { get; set; }

    public string? InsLastName { get; set; }

    public string? InsMI { get; set; }

    public DateTime InsDateTimeCreated { get; set; }

    public DateTime InsDateTimeModified { get; set; }

    public Guid? InsCreatedUserGUID { get; set; }

    public Guid? InsLastUserGUID { get; set; }

    public string? InsCreatedUserName { get; set; }

    public string? InsLastUserName { get; set; }

    public string? InsCreatedComputerName { get; set; }

    public string? InsLastComputerName { get; set; }

    public short InsAcceptAssignment { get; set; }

    public string? InsAdditionalRefID { get; set; }

    public string? InsAddress { get; set; }

    public DateOnly? InsBirthDate { get; set; }

    public string? InsCity { get; set; }

    public string? InsClaimFilingIndicator { get; set; }

    public string? InsEmployer { get; set; }

    public string? InsGroupNumber { get; set; }

    public string? InsIDNumber { get; set; }

    public int InsPayID { get; set; }

    public string? InsPlanName { get; set; }

    public string? InsPhone { get; set; }

    public string? InsSex { get; set; }

    public string? InsSSN { get; set; }

    public string? InsState { get; set; }

    public string? InsZip { get; set; }

    public string InsCityStateZipCC { get; set; } = null!;

    public virtual Payer InsPay { get; set; } = null!;

    public virtual ICollection<Patient_Insured> Patient_Insureds { get; set; } = new List<Patient_Insured>();
}
