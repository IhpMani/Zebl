using Microsoft.EntityFrameworkCore;
using Zebl.Application.Repositories;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;

namespace Zebl.Infrastructure.Repositories;

public class ImportLogRepository : IImportLogRepository
{
    private readonly ZeblDbContext _context;

    public ImportLogRepository(ZeblDbContext context)
    {
        _context = context;
    }

    public async Task LogEraImportAsync(string fileName, string notes)
    {
        try
        {
            var log = new Interface_Import_Log
            {
                FileName = fileName,
                ImportDate = DateTime.UtcNow,
                NewPatientsCount = 0,
                UpdatedPatientsCount = 0,
                NewClaimsCount = 0,
                DuplicateClaimsCount = 0,
                TotalAmount = 0,
                Notes = notes.Length > 500 ? notes.Substring(0, 500) : notes
            };
            _context.Interface_Import_Logs.Add(log);
            await _context.SaveChangesAsync();
        }
        catch
        {
            // Do not crash; logging is best-effort.
        }
    }
}
