namespace Zebl.Application.Abstractions;

/// <summary>
/// Provides a database transaction for payment apply flow. Commit only after validation and reconciliation.
/// </summary>
public interface ITransactionScope
{
    /// <summary>Begins a transaction on the shared DbContext. Caller must CommitAsync or dispose (rollback).</summary>
    Task<IPaymentTransaction> BeginTransactionAsync(CancellationToken cancellationToken);
}

/// <summary>Active transaction. CommitAsync to persist; dispose without commit rolls back.</summary>
public interface IPaymentTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}
