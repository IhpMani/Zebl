using Zebl.Application.Abstractions;

namespace Zebl.Infrastructure.Persistence.Entities;

public partial class Adjustment : IAuditableEntity
{
    public void SetCreated(Guid? userId, string? userName, string? computerName, DateTime dateTime)
    {
        AdjCreatedUserGUID = userId;
        AdjCreatedUserName = userName;
        AdjCreatedComputerName = computerName;
        AdjDateTimeCreated = dateTime;
        AdjDateTimeModified = dateTime;
        AdjLastUserGUID = userId;
        AdjLastUserName = userName;
        AdjLastComputerName = computerName;
    }

    public void SetModified(Guid? userId, string? userName, string? computerName, DateTime dateTime)
    {
        AdjLastUserGUID = userId;
        AdjLastUserName = userName;
        AdjLastComputerName = computerName;
        AdjDateTimeModified = dateTime;
    }
}
