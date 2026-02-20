using System.Text;
using Zebl.Application.Domain;
using Zebl.Application.Repositories;

namespace Zebl.Application.Services;

/// <summary>
/// Application service for EDI Reports. No SFTP, no DbContext.
/// </summary>
public class EdiReportService
{
    private readonly IEdiReportRepository _repository;

    public EdiReportService(IEdiReportRepository repository)
    {
        _repository = repository;
    }

    public Task<List<EdiReport>> GetAllAsync(bool? isArchived = null) => _repository.GetAllAsync(isArchived);

    public Task<EdiReport?> GetByIdAsync(Guid id) => _repository.GetByIdAsync(id);

    /// <summary>
    /// Creates a new EdiReport with Status = "Generated". FileContent and FileSize must be set by caller.
    /// </summary>
    public async Task<EdiReport> CreateGeneratedAsync(
        Guid receiverLibraryId,
        Guid? connectionLibraryId,
        string fileName,
        string fileType,
        byte[] fileContent,
        string direction = "Outbound")
    {
        var report = new EdiReport
        {
            Id = Guid.NewGuid(),
            ReceiverLibraryId = receiverLibraryId,
            ConnectionLibraryId = connectionLibraryId,
            FileName = fileName ?? throw new ArgumentNullException(nameof(fileName)),
            FileType = fileType ?? throw new ArgumentNullException(nameof(fileType)),
            Direction = direction,
            Status = "Generated",
            FileContent = fileContent ?? throw new ArgumentNullException(nameof(fileContent)),
            FileSize = fileContent.Length,
            IsRead = false,
            IsArchived = false,
            CreatedAt = DateTime.UtcNow
        };
        await _repository.AddAsync(report);
        return report;
    }

    /// <summary>
    /// Creates an inbound EdiReport (e.g. after download) with Status = "Received".
    /// Auto-processes 835 and 999 files to extract payment/payer info and notes.
    /// </summary>
    public async Task<EdiReport> CreateReceivedAsync(
        Guid receiverLibraryId,
        Guid? connectionLibraryId,
        string fileName,
        string fileType,
        byte[] fileContent)
    {
        var content = Encoding.UTF8.GetString(fileContent);
        var (payerName, paymentAmount, note) = ParseInboundFile(fileType, content);

        var report = new EdiReport
        {
            Id = Guid.NewGuid(),
            ReceiverLibraryId = receiverLibraryId,
            ConnectionLibraryId = connectionLibraryId,
            FileName = fileName ?? throw new ArgumentNullException(nameof(fileName)),
            FileType = fileType ?? throw new ArgumentNullException(nameof(fileType)),
            Direction = "Inbound",
            Status = "Received",
            FileContent = fileContent,
            FileSize = fileContent.Length,
            PayerName = payerName,
            PaymentAmount = paymentAmount,
            Note = note,
            IsRead = false,
            IsArchived = false,
            CreatedAt = DateTime.UtcNow,
            ReceivedAt = DateTime.UtcNow
        };
        await _repository.AddAsync(report);
        return report;
    }

    /// <summary>
    /// Parses inbound EDI files to extract payment/payer info and generate notes.
    /// </summary>
    private static (string? payerName, decimal? paymentAmount, string? note) ParseInboundFile(string fileType, string content)
    {
        string? payerName = null;
        decimal? paymentAmount = null;
        string? note = null;

        if (fileType == "835")
        {
            // Extract payer name from N1*PR segment
            var n1Match = System.Text.RegularExpressions.Regex.Match(content, @"N1\*PR\*([^*~]+)");
            if (n1Match.Success)
                payerName = n1Match.Groups[1].Value.Trim();

            // Extract payment amount from CLP segment (CLP*...*...*amount*...)
            var clpMatch = System.Text.RegularExpressions.Regex.Match(content, @"CLP\*[^*~]*\*[^*~]*\*([^*~]+)");
            if (clpMatch.Success && decimal.TryParse(clpMatch.Groups[1].Value.Trim(), out var amount))
                paymentAmount = amount;

            if (payerName != null || paymentAmount != null)
            {
                var parts = new List<string>();
                if (payerName != null) parts.Add($"Payment from {payerName}");
                if (paymentAmount != null) parts.Add($"${paymentAmount:F2}");
                note = string.Join(" - ", parts);
            }
        }
        else if (fileType == "999")
        {
            // Check for AK9 segment (functional acknowledgment)
            var ak9Match = System.Text.RegularExpressions.Regex.Match(content, @"AK9\*([^*~]+)");
            if (ak9Match.Success)
            {
                var ak901 = ak9Match.Groups[1].Value.Trim();
                note = ak901 == "A" ? "Batch Accepted" : "Batch Rejected";
            }
        }

        return (payerName, paymentAmount, note);
    }

    public async Task MarkSentAsync(Guid id)
    {
        var report = await _repository.GetByIdAsync(id) ?? throw new InvalidOperationException("EdiReport not found.");
        report.Status = "Sent";
        report.SentAt = DateTime.UtcNow;
        await _repository.UpdateAsync(report);
    }

    public async Task MarkFailedAsync(Guid id)
    {
        var report = await _repository.GetByIdAsync(id) ?? throw new InvalidOperationException("EdiReport not found.");
        report.Status = "Failed";
        await _repository.UpdateAsync(report);
    }

    public async Task ArchiveAsync(Guid id)
    {
        var report = await _repository.GetByIdAsync(id) ?? throw new InvalidOperationException("EdiReport not found.");
        report.IsArchived = true;
        await _repository.UpdateAsync(report);
    }

    public async Task MarkAsReadAsync(Guid id)
    {
        var report = await _repository.GetByIdAsync(id) ?? throw new InvalidOperationException("EdiReport not found.");
        report.IsRead = true;
        await _repository.UpdateAsync(report);
    }

    public async Task UpdateNoteAsync(Guid id, string? note)
    {
        var report = await _repository.GetByIdAsync(id) ?? throw new InvalidOperationException("EdiReport not found.");
        if (note != null && note.Length > 255)
            note = note.Substring(0, 255);
        report.Note = note;
        await _repository.UpdateAsync(report);
    }

    public async Task DeleteAsync(Guid id)
    {
        var report = await _repository.GetByIdAsync(id) ?? throw new InvalidOperationException("EdiReport not found.");
        await _repository.DeleteAsync(id);
    }
}
