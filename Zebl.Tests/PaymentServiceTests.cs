using Moq;
using Xunit;
using Zebl.Application.Domain;
using Zebl.Application.Dtos.Payments;
using Zebl.Application.Repositories;
using Zebl.Application.Services;
using Zebl.Application.Abstractions;

namespace Zebl.Tests;

/// <summary>
/// TASK 8: Unit tests for manual payment / EZClaim-style apply flow.
/// Scenario A: Charge 1000, Paid 700, CO 200, PR 100 → Final balance 0.
/// Scenario B: Overpayment attempt → reject.
/// Scenario C: PR unbundling mismatch → bundle automatically (service line gets one PR amount).
/// </summary>
public class PaymentServiceTests
{
    private static ServiceLineTotals Line(int srvId, int claimId, decimal charge, decimal insPaid = 0, decimal patPaid = 0, decimal co = 0, decimal pr = 0)
    {
        return new ServiceLineTotals
        {
            SrvID = srvId,
            SrvGUID = Guid.NewGuid(),
            ClaID = claimId,
            Charges = charge,
            TotalInsAmtPaid = insPaid,
            TotalPatAmtPaid = patPaid,
            TotalCOAdj = co,
            TotalCRAdj = 0,
            TotalOAAdj = 0,
            TotalPIAdj = 0,
            TotalPRAdj = pr
        };
    }

    [Fact]
    public async Task Scenario_A_Charge1000_Paid700_CO200_PR100_FinalBalanceZero()
    {
        var serviceLineRepo = new Mock<IServiceLineRepository>();
        var claimRepo = new Mock<IClaimRepository>();
        var paymentRepo = new Mock<IPaymentRepository>();
        var adjustmentRepo = new Mock<IAdjustmentRepository>();
        var disbursementRepo = new Mock<IDisbursementRepository>();
        var payerRepo = new Mock<IPayerRepository>();
        var claimTotalsService = new ClaimTotalsService();
        var claimAuditService = new Mock<Zebl.Application.Abstractions.IClaimAuditService>();
        var transactionScope = new Mock<ITransactionScope>();
        var reconciliationService = new Mock<IReconciliationService>();

        int claimId = 1;
        int srvId = 10;
        serviceLineRepo.Setup(x => x.GetTotalsByIdAsync(srvId)).ReturnsAsync(Line(srvId, claimId, 1000m));
        serviceLineRepo.Setup(x => x.GetTotalsByClaimIdAsync(claimId)).ReturnsAsync(new List<ServiceLineTotals> { Line(srvId, claimId, 1000m, 700, 0, 200, 100) });
        paymentRepo.Setup(x => x.ExistsDuplicateAsync(It.IsAny<decimal>(), It.IsAny<string?>())).ReturnsAsync(false);
        paymentRepo.Setup(x => x.CreatePaymentAsync(It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<DateOnly>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>())).ReturnsAsync(1);
        paymentRepo.Setup(x => x.SetDisbursedAsync(1, It.IsAny<decimal>())).Returns(Task.CompletedTask);
        claimRepo.Setup(x => x.GetBillingPhysicianIdAsync(claimId)).ReturnsAsync(1);
        claimRepo.Setup(x => x.UpdateTotalsAsync(claimId, It.IsAny<ClaimTotals>())).Returns(Task.CompletedTask);
        payerRepo.Setup(x => x.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((Payer?)null);
        adjustmentRepo.Setup(x => x.AddAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<decimal>(), It.IsAny<decimal>())).Returns(Task.CompletedTask);
        serviceLineRepo.Setup(x => x.AddInsPaidAsync(srvId, It.IsAny<decimal>())).ReturnsAsync(claimId);
        serviceLineRepo.Setup(x => x.AddAdjustmentAmountAsync(srvId, It.IsAny<string>(), It.IsAny<decimal>())).ReturnsAsync(claimId);
        var transaction = new Mock<IPaymentTransaction>();
        transaction.Setup(x => x.CommitAsync(CancellationToken.None)).Returns(Task.CompletedTask);
        transaction.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);
        transactionScope.Setup(x => x.BeginTransactionAsync(CancellationToken.None)).ReturnsAsync(transaction.Object);
        reconciliationService.Setup(x => x.VerifyClaimAsync(claimId, CancellationToken.None)).ReturnsAsync(new ReconciliationResult { Success = true });

        var sut = new PaymentService(
            paymentRepo.Object,
            adjustmentRepo.Object,
            serviceLineRepo.Object,
            disbursementRepo.Object,
            claimRepo.Object,
            payerRepo.Object,
            claimTotalsService,
            claimAuditService.Object,
            transactionScope.Object,
            reconciliationService.Object);

        var command = new CreatePaymentCommand
        {
            PaymentSource = PaymentSourceKind.Payer,
            PayerId = 1,
            PatientId = 1,
            Amount = 700m,
            Date = DateOnly.FromDateTime(DateTime.Today),
            ServiceLineApplications = new List<ServiceLineApplicationDto>
            {
                new()
                {
                    ServiceLineId = srvId,
                    PaymentAmount = 700m,
                    Adjustments = new List<AdjustmentInputDto>
                    {
                        new() { GroupCode = "CO", Amount = 200m },
                        new() { GroupCode = "PR", Amount = 100m }
                    }
                }
            }
        };

        int paymentId = await sut.CreatePaymentAsync(command);

        Assert.Equal(1, paymentId);
        serviceLineRepo.Verify(x => x.AddInsPaidAsync(srvId, 700m), Times.Once);
        serviceLineRepo.Verify(x => x.AddAdjustmentAmountAsync(srvId, "CO", 200m), Times.Once);
        serviceLineRepo.Verify(x => x.AddAdjustmentAmountAsync(srvId, "PR", 100m), Times.Once);
        reconciliationService.Verify(x => x.VerifyClaimAsync(claimId, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Scenario_B_OverpaymentAttempt_Rejected()
    {
        var serviceLineRepo = new Mock<IServiceLineRepository>();
        var paymentRepo = new Mock<IPaymentRepository>();
        var adjustmentRepo = new Mock<IAdjustmentRepository>();
        var disbursementRepo = new Mock<IDisbursementRepository>();
        var claimRepo = new Mock<IClaimRepository>();
        var payerRepo = new Mock<IPayerRepository>();
        var claimTotalsService = new ClaimTotalsService();
        var claimAuditService = new Mock<Zebl.Application.Abstractions.IClaimAuditService>();
        var transactionScope = new Mock<ITransactionScope>();
        var reconciliationService = new Mock<IReconciliationService>();

        int srvId = 20;
        int claimId = 2;
        serviceLineRepo.Setup(x => x.GetTotalsByIdAsync(srvId)).ReturnsAsync(Line(srvId, claimId, 1000m)); // remaining = 1000
        paymentRepo.Setup(x => x.ExistsDuplicateAsync(It.IsAny<decimal>(), It.IsAny<string?>())).ReturnsAsync(false);
        payerRepo.Setup(x => x.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((Payer?)null);

        var sut = new PaymentService(
            paymentRepo.Object,
            adjustmentRepo.Object,
            serviceLineRepo.Object,
            disbursementRepo.Object,
            claimRepo.Object,
            payerRepo.Object,
            claimTotalsService,
            claimAuditService.Object,
            transactionScope.Object,
            reconciliationService.Object);

        var command = new CreatePaymentCommand
        {
            PaymentSource = PaymentSourceKind.Payer,
            PayerId = 1,
            PatientId = 1,
            Amount = 1500m,
            Date = DateOnly.FromDateTime(DateTime.Today),
            AllowOverApply = false,
            ServiceLineApplications = new List<ServiceLineApplicationDto>
            {
                new() { ServiceLineId = srvId, PaymentAmount = 1500m, Adjustments = new List<AdjustmentInputDto>() }
            }
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.CreatePaymentAsync(command));
        serviceLineRepo.Verify(x => x.AddInsPaidAsync(It.IsAny<int>(), It.IsAny<decimal>()), Times.Never);
    }

    [Fact]
    public async Task Scenario_C_PRUnbundlingMismatch_Bundled_ServiceLineGetsOneAmount()
    {
        var serviceLineRepo = new Mock<IServiceLineRepository>();
        var paymentRepo = new Mock<IPaymentRepository>();
        var adjustmentRepo = new Mock<IAdjustmentRepository>();
        var disbursementRepo = new Mock<IDisbursementRepository>();
        var claimRepo = new Mock<IClaimRepository>();
        var payerRepo = new Mock<IPayerRepository>();
        var claimTotalsService = new ClaimTotalsService();
        var claimAuditService = new Mock<Zebl.Application.Abstractions.IClaimAuditService>();
        var transactionScope = new Mock<ITransactionScope>();
        var reconciliationService = new Mock<IReconciliationService>();

        int srvId = 30;
        int claimId = 3;
        decimal charge = 100m;
        serviceLineRepo.Setup(x => x.GetTotalsByIdAsync(srvId)).ReturnsAsync(Line(srvId, claimId, charge));
        serviceLineRepo.Setup(x => x.GetTotalsByClaimIdAsync(claimId)).ReturnsAsync(new List<ServiceLineTotals> { Line(srvId, claimId, charge, 0.01m, 0, 0, 70) });
        paymentRepo.Setup(x => x.ExistsDuplicateAsync(It.IsAny<decimal>(), It.IsAny<string?>())).ReturnsAsync(false);
        paymentRepo.Setup(x => x.CreatePaymentAsync(It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<DateOnly>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>())).ReturnsAsync(1);
        paymentRepo.Setup(x => x.SetDisbursedAsync(1, It.IsAny<decimal>())).Returns(Task.CompletedTask);
        claimRepo.Setup(x => x.GetBillingPhysicianIdAsync(claimId)).ReturnsAsync(1);
        claimRepo.Setup(x => x.UpdateTotalsAsync(claimId, It.IsAny<ClaimTotals>())).Returns(Task.CompletedTask);
        var payer = new Payer { PayID = 1, PayExportTrackedPRAdjs = true };
        payerRepo.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(payer);
        adjustmentRepo.Setup(x => x.AddAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<decimal>(), It.IsAny<decimal>())).Returns(Task.CompletedTask);
        var transaction = new Mock<IPaymentTransaction>();
        transaction.Setup(x => x.CommitAsync(CancellationToken.None)).Returns(Task.CompletedTask);
        transaction.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);
        transactionScope.Setup(x => x.BeginTransactionAsync(CancellationToken.None)).ReturnsAsync(transaction.Object);
        reconciliationService.Setup(x => x.VerifyClaimAsync(claimId, CancellationToken.None)).ReturnsAsync(new ReconciliationResult { Success = true });
        serviceLineRepo.Setup(x => x.AddAdjustmentAmountAsync(srvId, It.IsAny<string>(), It.IsAny<decimal>())).ReturnsAsync(claimId);

        var sut = new PaymentService(
            paymentRepo.Object,
            adjustmentRepo.Object,
            serviceLineRepo.Object,
            disbursementRepo.Object,
            claimRepo.Object,
            payerRepo.Object,
            claimTotalsService,
            claimAuditService.Object,
            transactionScope.Object,
            reconciliationService.Object);

        var command = new CreatePaymentCommand
        {
            PaymentSource = PaymentSourceKind.Payer,
            PayerId = 1,
            PatientId = 1,
            Amount = 0.01m,
            Date = DateOnly.FromDateTime(DateTime.Today),
            ServiceLineApplications = new List<ServiceLineApplicationDto>
            {
                new()
                {
                    ServiceLineId = srvId,
                    PaymentAmount = 0.01m,
                    Adjustments = new List<AdjustmentInputDto>
                    {
                        new() { GroupCode = "PR", ReasonAmount = 30m, Amount = 30m },
                        new() { GroupCode = "PR", ReasonAmount = 40m, Amount = 40m }
                    }
                }
            }
        };

        await sut.CreatePaymentAsync(command);

        // Bundled: one call with PR sum (70), one with 0 for the second PR row
        serviceLineRepo.Verify(x => x.AddAdjustmentAmountAsync(srvId, "PR", 70m), Times.Once);
        serviceLineRepo.Verify(x => x.AddAdjustmentAmountAsync(srvId, "PR", 0m), Times.Once);
    }
}
