using System;
using System.Collections.Generic;

namespace Zebl.Infrastructure.Persistence.Entities;

public partial class Claim_Insured
{
    public Guid ClaInsGUID { get; set; }

    public string? ClaInsFirstName { get; set; }

    public string? ClaInsLastName { get; set; }

    public string? ClaInsMI { get; set; }

    public DateTime ClaInsDateTimeCreated { get; set; }

    public DateTime ClaInsDateTimeModified { get; set; }

    public Guid? ClaInsCreatedUserGUID { get; set; }

    public Guid? ClaInsLastUserGUID { get; set; }

    public string? ClaInsCreatedUserName { get; set; }

    public string? ClaInsLastUserName { get; set; }

    public string? ClaInsCreatedComputerName { get; set; }

    public string? ClaInsLastComputerName { get; set; }

    public short? ClaInsAcceptAssignment { get; set; }

    public string? ClaInsAdditionalRefID { get; set; }

    public string? ClaInsAddress { get; set; }

    public DateOnly? ClaInsBirthDate { get; set; }

    public string? ClaInsCity { get; set; }

    public int ClaInsClaFID { get; set; }

    public string? ClaInsClaimFilingIndicator { get; set; }

    public string? ClaInsEmployer { get; set; }

    public string? ClaInsGroupNumber { get; set; }

    public string? ClaInsIDNumber { get; set; }

    public int ClaInsPayFID { get; set; }

    public int ClaInsPatFID { get; set; }

    public string? ClaInsPhone { get; set; }

    public string? ClaInsPlanName { get; set; }

    public string? ClaInsPriorAuthorizationNumber { get; set; }

    public int ClaInsRelationToInsured { get; set; }

    public int? ClaInsSequence { get; set; }

    public string? ClaInsSex { get; set; }

    public string? ClaInsSSN { get; set; }

    public string? ClaInsState { get; set; }

    public string? ClaInsZip { get; set; }

    public string ClaInsSequenceDescriptionCC { get; set; } = null!;

    public string ClaInsCityStateZipCC { get; set; } = null!;

    public virtual Claim ClaInsClaF { get; set; } = null!;

    public virtual Patient ClaInsPatF { get; set; } = null!;

    public virtual Payer ClaInsPayF { get; set; } = null!;
}
