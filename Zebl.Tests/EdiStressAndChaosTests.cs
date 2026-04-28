using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Zebl.Application.Abstractions;
using Zebl.Application.Domain;
using Zebl.Application.Edi.Parsing;
using Zebl.Application.Repositories;
using Zebl.Application.Services;
using Xunit;

namespace Zebl.Tests;

public class EdiStressAndChaosTests
{
    [Fact]
    public async Task Parse835Async_LargeInput_RemainsDeterministic()
    {
        var sb = new StringBuilder();
        sb.Append("ISA*00*          *00*          *ZZ*SENDER         *ZZ*RECEIVER       *260423*1200*^*00501*000000123*0*T*:~");
        sb.Append("GS*HP*SENDER*RECEIVER*20260423*1200*1*X*005010X221A1~");
        sb.Append("ST*835*0001~N1*PR*PAYER~");
        for (var i = 0; i < 2000; i++)
        {
            sb.Append($"CLP*C{i}*1*100*80~");
            sb.Append("CAS*CO*45*20~");
        }
        sb.Append("SE*4004*0001~GE*1*1~IEA*1*000000123~");

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));
        var parsed = await Edi835Parser.ParseAsync(stream);
        Assert.Equal(2000, parsed.ClaimGroups.Count);
        Assert.Equal(2000, parsed.CasAdjustments.Count);
    }

    [Fact]
    public void Parse999_FuzzInput_DoesNotCrash()
    {
        var fuzz = "%%%~ST*999*ABC~AK2*837*CTRL~IK3*CLM*1**8~AK9*R*1*1*0~";
        var result = Edi999Parser.Parse(fuzz);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task CreateReceivedAsync_Handles50ParallelWithout500Equivalent()
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
            .Returns<int, Guid, string>((_, id, _) => $"1/{id:N}.edi");
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
            .Setup(s => s.Ingest835Async(It.IsAny<Edi835ParseResult>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClaimPaymentIngestionResult(0, 0, 0, 0, 0));

        var sut = new EdiReportService(
            repo.Object,
            limiter.Object,
            claimPaymentIngestion.Object,
            fileStore.Object,
            currentContext.Object,
            Array.Empty<IEdiInboundPostProcessor>(),
            NullLogger<EdiReportService>.Instance);

        var tasks = Enumerable.Range(0, 50)
            .Select(_ => sut.CreateReceivedAsync(
                existing.ReceiverLibraryId!.Value,
                existing.ConnectionLibraryId,
                "incoming.edi",
                "835",
                Encoding.UTF8.GetBytes("ST*835*1~"),
                Guid.NewGuid().ToString("N")))
            .ToArray();

        var results = await Task.WhenAll(tasks);
        Assert.All(results, r => Assert.False(r.IsDuplicate));
    }

    private sealed class AsyncNoopDisposer : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

