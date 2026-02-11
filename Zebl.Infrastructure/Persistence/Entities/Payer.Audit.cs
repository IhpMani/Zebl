using Zebl.Application.Abstractions;

namespace Zebl.Infrastructure.Persistence.Entities;

public partial class Payer : IAuditableEntity
{
    public void SetCreated(Guid? userId, string? userName, string? computerName, DateTime dateTime)
    {
        PayCreatedUserGUID = userId;
        PayCreatedUserName = userName;
        PayCreatedComputerName = computerName;
        PayDateTimeCreated = dateTime;
        PayDateTimeModified = dateTime;
        PayLastUserGUID = userId;
        PayLastUserName = userName;
        PayLastComputerName = computerName;
    }

    public void SetModified(Guid? userId, string? userName, string? computerName, DateTime dateTime)
    {
        PayLastUserGUID = userId;
        PayLastUserName = userName;
        PayLastComputerName = computerName;
        PayDateTimeModified = dateTime;
    }
}
