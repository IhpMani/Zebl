using Zebl.Application.Abstractions;

namespace Zebl.Infrastructure.Persistence.Entities;

public partial class Insured : IAuditableEntity
{
    public void SetCreated(Guid? userId, string? userName, string? computerName, DateTime dateTime)
    {
        InsCreatedUserGUID = userId;
        InsCreatedUserName = userName;
        InsCreatedComputerName = computerName;
        InsDateTimeCreated = dateTime;
        InsDateTimeModified = dateTime;
        InsLastUserGUID = userId;
        InsLastUserName = userName;
        InsLastComputerName = computerName;
    }

    public void SetModified(Guid? userId, string? userName, string? computerName, DateTime dateTime)
    {
        InsLastUserGUID = userId;
        InsLastUserName = userName;
        InsLastComputerName = computerName;
        InsDateTimeModified = dateTime;
    }
}
