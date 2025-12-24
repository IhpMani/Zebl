using System;
using System.Collections.Generic;

namespace Zebl.Infrastructure.Persistence.Entities;

public partial class Patient_Insured
{
    public Guid PatInsGUID { get; set; }

    public DateTime PatInsDateTimeCreated { get; set; }

    public DateTime PatInsDateTimeModified { get; set; }

    public Guid? PatInsCreatedUserGUID { get; set; }

    public Guid? PatInsLastUserGUID { get; set; }

    public string? PatInsCreatedUserName { get; set; }

    public string? PatInsLastUserName { get; set; }

    public string? PatInsCreatedComputerName { get; set; }

    public string? PatInsLastComputerName { get; set; }

    public string? PatInsEligANSI { get; set; }

    public DateOnly? PatInsEligDate { get; set; }

    public string? PatInsEligStatus { get; set; }

    public Guid PatInsInsGUID { get; set; }

    public int PatInsPatFID { get; set; }

    public int PatInsRelationToInsured { get; set; }

    public int PatInsSequence { get; set; }

    public string PatInsSequenceDescriptionCC { get; set; } = null!;

    public string? PatInsEligStatusDisplayTextCC { get; set; }

    public virtual Insured PatInsIns { get; set; } = null!;

    public virtual Patient PatInsPatF { get; set; } = null!;
}
