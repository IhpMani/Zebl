using Zebl.Application.Domain;
using Zebl.Application.Repositories;

namespace Zebl.Application.Services;

public class EdiExportService : IEdiExportService
{
    private readonly IReceiverLibraryRepository _receiverRepo;
    private readonly IClaimRepository _claimRepo;

    public EdiExportService(
        IReceiverLibraryRepository receiverRepo,
        IClaimRepository claimRepo)
    {
        _receiverRepo = receiverRepo;
        _claimRepo = claimRepo;
    }

    public async Task<string> GenerateAsync(Guid receiverLibraryId, int claimId)
    {
        var receiver = await _receiverRepo.GetByIdAsync(receiverLibraryId)
            ?? throw new Exception("Receiver not found");

        var claim = await _claimRepo.GetByIdAsync(claimId)
            ?? throw new Exception("Claim not found");

        return receiver.ExportFormat switch
        {
            ExportFormat.Ansi837_wTilde => Generate837(receiver, claim),
            ExportFormat.Eligibility270 => Generate270(receiver, claim),
            _ => throw new NotSupportedException("Unsupported ExportFormat")
        };
    }

    private string Generate837(Domain.ReceiverLibrary receiver, ClaimData claim)
    {
        var sb = new System.Text.StringBuilder();
        var now = DateTime.Now;
        
        // ISA segment - using ~ as segment terminator
        sb.Append(BuildIsaSegment(receiver));

        // GS segment - HC for 837
        var senderCode = receiver.SenderCode ?? receiver.SenderId ?? "";
        var receiverCode = receiver.ReceiverCode ?? receiver.InterchangeReceiverId ?? "";
        sb.Append("GS*HC*")
          .Append(senderCode.PadRight(15))
          .Append("*")
          .Append(receiverCode.PadRight(15))
          .Append("*")
          .Append(now.ToString("yyyyMMdd"))
          .Append("*")
          .Append(now.ToString("HHmm"))
          .Append("*1*X*005010X222A1~");

        // ST segment - 837 transaction
        sb.Append("ST*837*0001~");

        // Minimal claim structure (placeholder - would need full implementation)
        // SE segment
        sb.Append("SE*5*0001~");
        
        // GE segment
        sb.Append("GE*1*1~");
        
        // IEA segment
        sb.Append("IEA*1*000000001~");

        return sb.ToString();
    }

    private string Generate270(Domain.ReceiverLibrary receiver, ClaimData claim)
    {
        var sb = new System.Text.StringBuilder();
        var now = DateTime.Now;

        // ISA segment - reuse BuildIsaSegment
        sb.Append(BuildIsaSegment(receiver));

        // GS segment with HS functional identifier for 270
        var senderCode = receiver.SenderCode ?? receiver.SenderId ?? "";
        var receiverCode = receiver.ReceiverCode ?? receiver.InterchangeReceiverId ?? "";
        sb.Append("GS*HS*")
          .Append(senderCode.PadRight(15))
          .Append("*")
          .Append(receiverCode.PadRight(15))
          .Append("*")
          .Append(now.ToString("yyyyMMdd"))
          .Append("*")
          .Append(now.ToString("HHmm"))
          .Append("*1*X*005010X279A1~");

        // ST segment for 270
        sb.Append("ST*270*0001~");

        // SE segment
        sb.Append("SE*5*0001~");
        
        // GE segment
        sb.Append("GE*1*1~");
        
        // IEA segment
        sb.Append("IEA*1*000000001~");

        return sb.ToString();
    }

    private string BuildIsaSegment(Domain.ReceiverLibrary receiver)
    {
        var now = DateTime.Now;
        var testProd = receiver.TestProdIndicator ?? "T";
        
        return string.Join("*", new[]
        {
            "ISA",
            receiver.AuthorizationInfoQualifier ?? "00",
            (receiver.AuthorizationInfo ?? "").PadRight(10),
            receiver.SecurityInfoQualifier ?? "00",
            (receiver.SecurityInfo ?? "").PadRight(10),
            receiver.SenderQualifier ?? "ZZ",
            (receiver.SenderId ?? "").PadRight(15),
            receiver.ReceiverQualifier ?? "ZZ",
            (receiver.InterchangeReceiverId ?? "").PadRight(15),
            now.ToString("yyMMdd"),
            now.ToString("HHmm"),
            "^",
            "00501",
            "000000001",
            "0",
            testProd,
            ":~"
        });
    }
}
