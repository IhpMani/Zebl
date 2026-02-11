using Zebl.Application.Abstractions;

namespace Zebl.Infrastructure.Persistence.Entities;

public partial class Physician : IAuditableEntity
{
    public void SetCreated(Guid? userId, string? userName, string? computerName, DateTime dateTime)
    {
        PhyCreatedUserGUID = userId;
        PhyCreatedUserName = userName;
        PhyCreatedComputerName = computerName;
        PhyDateTimeCreated = dateTime;
        PhyDateTimeModified = dateTime;
        PhyLastUserGUID = userId;
        PhyLastUserName = userName;
        PhyLastComputerName = computerName;
    }

    public void SetModified(Guid? userId, string? userName, string? computerName, DateTime dateTime)
    {
        PhyLastUserGUID = userId;
        PhyLastUserName = userName;
        PhyLastComputerName = computerName;
        PhyDateTimeModified = dateTime;
    }
}
