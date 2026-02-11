using Zebl.Application.Abstractions;

namespace Zebl.Infrastructure.Persistence.Entities;

public partial class Service_Line : IAuditableEntity
{
    public void SetCreated(Guid? userId, string? userName, string? computerName, DateTime dateTime)
    {
        SrvCreatedUserGUID = userId;
        SrvCreatedUserName = userName;
        SrvCreatedComputerName = computerName;
        SrvDateTimeCreated = dateTime;
        SrvDateTimeModified = dateTime;
        SrvLastUserGUID = userId;
        SrvLastUserName = userName;
        SrvLastComputerName = computerName;
    }

    public void SetModified(Guid? userId, string? userName, string? computerName, DateTime dateTime)
    {
        SrvLastUserGUID = userId;
        SrvLastUserName = userName;
        SrvLastComputerName = computerName;
        SrvDateTimeModified = dateTime;
    }
}
