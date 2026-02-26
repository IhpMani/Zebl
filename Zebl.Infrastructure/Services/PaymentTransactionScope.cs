using Microsoft.EntityFrameworkCore.Storage;
using Zebl.Application.Abstractions;
using Zebl.Infrastructure.Persistence.Context;

namespace Zebl.Infrastructure.Services;

/// <summary>
/// Provides a database transaction for the payment apply flow. Uses the same DbContext as repositories (scoped).
/// </summary>
public class PaymentTransactionScope : ITransactionScope
{
    private readonly ZeblDbContext _context;

    public PaymentTransactionScope(ZeblDbContext context)
    {
        _context = context;
    }

    public async Task<IPaymentTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        return new PaymentTransaction(transaction);
    }
}

internal sealed class PaymentTransaction : IPaymentTransaction
{
    private readonly IDbContextTransaction _transaction;
    private bool _committed;

    public PaymentTransaction(IDbContextTransaction transaction)
    {
        _transaction = transaction;
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        await _transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        _committed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_committed)
            await _transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
        await _transaction.DisposeAsync().ConfigureAwait(false);
    }
}
