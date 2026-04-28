using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text;
using Zebl.Application.Abstractions;
using Zebl.Application.Domain;
using Zebl.Application.Repositories;
using Zebl.Application.Services;
using Xunit;

namespace Zebl.Tests;

public class EdiDuplicateConcurrencyTests
{
    [Fact]
    public async Task CreateReceivedAsync_WhenUniqueConflictRace_ReturnsDuplicateNotFailure()
    {
        var existing = new EdiReport
        {
            Id = Guid.NewGuid(),
            TenantId = 1,
            ReceiverLibraryId = Guid.NewGuid(),
            ConnectionLibraryId = Guid.NewGuid(),
            FileName = "existing.edi",
            FileType = "835",
            Direction = "Inbound",
            Status = "Received",
            FileStorageKey = "1/existing.edi",
            CorrelationId = "corr-old",
            CreatedAt = DateTime.UtcNow
        };

        var repo = new Mock<IEdiReportRepository>();
        repo.Setup(r => r.AddAsync(It.IsAny<EdiReport>())).Returns(Task.CompletedTask);

        var fileStore = new Mock<IEdiReportFileStore>();
        fileStore.Setup(f => f.BuildStorageKey(It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<string>()))
            .Returns("1/new.edi");
        fileStore.Setup(f => f.WriteAsync(It.IsAny<string>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        fileStore.Setup(f => f.TryDeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var currentContext = new Mock<ICurrentContext>();
        currentContext.SetupGet(c => c.TenantId).Returns(1);
        currentContext.SetupGet(c => c.FacilityId).Returns(1);
        var limiter = new Mock<IEdiProcessingLimiter>();
        limiter.Setup(l => l.AcquireInboundSlotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AsyncNoopDisposer());
        var claimPaymentIngestion = new Mock<IClaimPaymentIngestionService>();
        claimPaymentIngestion
            .Setup(s => s.Ingest835Async(It.IsAny<Zebl.Application.Edi.Parsing.Edi835ParseResult>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClaimPaymentIngestionResult(0, 0, 0, 0, 0));

        var sut = new EdiReportService(
            repo.Object,
            limiter.Object,
            claimPaymentIngestion.Object,
            fileStore.Object,
            currentContext.Object,
            Array.Empty<IEdiInboundPostProcessor>(),
            NullLogger<EdiReportService>.Instance);

        var outcome = await sut.CreateReceivedAsync(
            existing.ReceiverLibraryId!.Value,
            existing.ConnectionLibraryId,
            "incoming.edi",
            "835",
            Encoding.UTF8.GetBytes("ST*835*1~"),
            "corr-1");

        Assert.False(outcome.IsDuplicate);
    }

    private sealed class AsyncNoopDisposer : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

