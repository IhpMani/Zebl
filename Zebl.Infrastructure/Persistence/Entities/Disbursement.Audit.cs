using Zebl.Application.Abstractions;

namespace Zebl.Infrastructure.Persistence.Entities;

public partial class Disbursement : IAuditableEntity
{
    public void SetCreated(Guid? userId, string? userName, string? computerName, DateTime dateTime)
    {
        DisbCreatedUserGUID = userId;
        DisbCreatedUserName = userName;
        DisbCreatedComputerName = computerName;
        DisbDateTimeCreated = dateTime;
        DisbDateTimeModified = dateTime;
        DisbLastUserGUID = userId;
        DisbLastUserName = userName;
        DisbLastComputerName = computerName;
    }

    public void SetModified(Guid? userId, string? userName, string? computerName, DateTime dateTime)
    {
        DisbLastUserGUID = userId;
        DisbLastUserName = userName;
        DisbLastComputerName = computerName;
        DisbDateTimeModified = dateTime;
    }
}
