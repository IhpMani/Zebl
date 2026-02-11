using Zebl.Application.Abstractions;

namespace Zebl.Infrastructure.Persistence.Entities;

public partial class Patient : IAuditableEntity
{
    public void SetCreated(Guid? userId, string? userName, string? computerName, DateTime dateTime)
    {
        PatCreatedUserGUID = userId;
        PatCreatedUserName = userName;
        PatCreatedComputerName = computerName;
        PatDateTimeCreated = dateTime;
        PatDateTimeModified = dateTime;
        PatLastUserGUID = userId;
        PatLastUserName = userName;
        PatLastComputerName = computerName;
    }

    public void SetModified(Guid? userId, string? userName, string? computerName, DateTime dateTime)
    {
        PatLastUserGUID = userId;
        PatLastUserName = userName;
        PatLastComputerName = computerName;
        PatDateTimeModified = dateTime;
    }
}
