using Zebl.Application.Abstractions;

namespace Zebl.Infrastructure.Persistence.Entities;

public partial class Payment : IAuditableEntity
{
    public void SetCreated(Guid? userId, string? userName, string? computerName, DateTime dateTime)
    {
        PmtCreatedUserGUID = userId;
        PmtCreatedUserName = userName;
        PmtCreatedComputerName = computerName;
        PmtDateTimeCreated = dateTime;
        PmtDateTimeModified = dateTime;
        PmtLastUserGUID = userId;
        PmtLastUserName = userName;
        PmtLastComputerName = computerName;
    }

    public void SetModified(Guid? userId, string? userName, string? computerName, DateTime dateTime)
    {
        PmtLastUserGUID = userId;
        PmtLastUserName = userName;
        PmtLastComputerName = computerName;
        PmtDateTimeModified = dateTime;
    }
}
