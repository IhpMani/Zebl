using Zebl.Application.Abstractions;

namespace Zebl.Infrastructure.Persistence.Entities;

public partial class Claim : IAuditableEntity
{
    public void SetCreated(Guid? userId, string? userName, string? computerName, DateTime dateTime)
    {
        ClaCreatedUserGUID = userId;
        ClaCreatedUserName = userName;
        ClaCreatedComputerName = computerName;
        ClaDateTimeCreated = dateTime;
        ClaDateTimeModified = dateTime;
        ClaLastUserGUID = userId;
        ClaLastUserName = userName;
        ClaLastComputerName = computerName;
    }

    public void SetModified(Guid? userId, string? userName, string? computerName, DateTime dateTime)
    {
        ClaLastUserGUID = userId;
        ClaLastUserName = userName;
        ClaLastComputerName = computerName;
        ClaDateTimeModified = dateTime;
    }
}
