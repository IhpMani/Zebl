using Zebl.Application.Abstractions;

namespace Zebl.Infrastructure.Persistence.Entities;

public partial class Procedure_Code : IAuditableEntity
{
    public void SetCreated(Guid? userId, string? userName, string? computerName, DateTime dateTime)
    {
        ProcCreatedUserGUID = userId;
        ProcCreatedUserName = userName;
        ProcCreatedComputerName = computerName;
        ProcDateTimeCreated = dateTime;
        ProcDateTimeModified = dateTime;
        ProcLastUserGUID = userId;
        ProcLastUserName = userName;
        ProcLastComputerName = computerName;
    }

    public void SetModified(Guid? userId, string? userName, string? computerName, DateTime dateTime)
    {
        ProcLastUserGUID = userId;
        ProcLastUserName = userName;
        ProcLastComputerName = computerName;
        ProcDateTimeModified = dateTime;
    }
}
