using Zebl.Application.Repositories;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;

namespace Zebl.Infrastructure.Repositories;

public class PaymentRepository : IPaymentRepository
{
    private readonly ZeblDbContext _context;

    public PaymentRepository(ZeblDbContext context)
    {
        _context = context;
    }

    public async Task<int> AddAsync(int payId, int patientId, int billingPhysicianId, decimal amount, DateOnly paymentDate, string? ref835 = null)
    {
        var now = DateTime.UtcNow;
        var payment = new Payment
        {
            PmtAmount = amount,
            PmtPayFID = payId,
            PmtPatFID = patientId,
            PmtBFEPFID = billingPhysicianId,
            PmtDate = paymentDate,
            Pmt835Ref = ref835,
            PmtDateTimeCreated = now,
            PmtDateTimeModified = now,
            PmtDisbursedTRIG = 0,
            PmtChargedPlatformFee = 0
        };
        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();
        return payment.PmtID;
    }
}
