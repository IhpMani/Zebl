using Zebl.Application.Abstractions;

namespace Zebl.Infrastructure.Persistence.Entities;

public partial class Claim_Insured : IAuditableEntity
{
    public void SetCreated(Guid? userId, string? userName, string? computerName, DateTime dateTime)
    {
        ClaInsCreatedUserGUID = userId;
        ClaInsCreatedUserName = userName;
        ClaInsCreatedComputerName = computerName;
        ClaInsDateTimeCreated = dateTime;
        ClaInsDateTimeModified = dateTime;
        ClaInsLastUserGUID = userId;
        ClaInsLastUserName = userName;
        ClaInsLastComputerName = computerName;
    }

    public void SetModified(Guid? userId, string? userName, string? computerName, DateTime dateTime)
    {
        ClaInsLastUserGUID = userId;
        ClaInsLastUserName = userName;
        ClaInsLastComputerName = computerName;
        ClaInsDateTimeModified = dateTime;
    }
}
