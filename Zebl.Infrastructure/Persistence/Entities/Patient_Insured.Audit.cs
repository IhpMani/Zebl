using Zebl.Application.Abstractions;

namespace Zebl.Infrastructure.Persistence.Entities;

public partial class Patient_Insured : IAuditableEntity
{
    public void SetCreated(Guid? userId, string? userName, string? computerName, DateTime dateTime)
    {
        PatInsCreatedUserGUID = userId;
        PatInsCreatedUserName = userName;
        PatInsCreatedComputerName = computerName;
        PatInsDateTimeCreated = dateTime;
        PatInsDateTimeModified = dateTime;
        PatInsLastUserGUID = userId;
        PatInsLastUserName = userName;
        PatInsLastComputerName = computerName;
    }

    public void SetModified(Guid? userId, string? userName, string? computerName, DateTime dateTime)
    {
        PatInsLastUserGUID = userId;
        PatInsLastUserName = userName;
        PatInsLastComputerName = computerName;
        PatInsDateTimeModified = dateTime;
    }
}
