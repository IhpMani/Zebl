namespace Zebl.Application.Abstractions;

/// <summary>
/// Implemented by DB-first entities (via partial classes) so that
/// ZeblDbContext can apply Created/Modified audit in SaveChanges/SaveChangesAsync.
/// </summary>
public interface IAuditableEntity
{
    void SetCreated(Guid? userId, string? userName, string? computerName, DateTime dateTime);
    void SetModified(Guid? userId, string? userName, string? computerName, DateTime dateTime);
}
