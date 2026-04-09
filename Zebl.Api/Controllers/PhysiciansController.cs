using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using System.Dynamic;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using Zebl.Application.Abstractions;
using Zebl.Application.Dtos.Common;
using Zebl.Application.Dtos.Physicians;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;

namespace Zebl.Api.Controllers
{
    [ApiController]
    [Route("api/physicians")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "RequireAuth")]
    public class PhysiciansController : ControllerBase
    {
        private readonly ZeblDbContext _db;
        private readonly ICurrentContext _currentContext;
        private readonly ILogger<PhysiciansController> _logger;

        public PhysiciansController(
            ZeblDbContext db,
            ICurrentContext currentContext,
            ILogger<PhysiciansController> logger)
        {
            _db = db;
            _currentContext = currentContext;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetPhysicians(
            [FromQuery] int? page = 1,
            [FromQuery] int? pageSize = 25,
            [FromQuery] string? searchText = null,
            [FromQuery] bool? inactive = null,
            [FromQuery] string? type = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] int? minPhysicianId = null,
            [FromQuery] int? maxPhysicianId = null,
            [FromQuery] string? additionalColumns = null)
        {
            var effectivePage = page.GetValueOrDefault(1);
            var effectivePageSize = pageSize.GetValueOrDefault(25);

            if (effectivePage < 1 || effectivePageSize < 1)
            {
                _logger.LogWarning("GetPhysicians called with invalid paging arguments: page={Page}, pageSize={PageSize}", page, pageSize);
                return BadRequest(new ErrorResponseDto
                {
                    ErrorCode = "INVALID_ARGUMENT",
                    Message = "Page and pageSize must be at least 1"
                });
            }

            // Parse additional columns (Physician has no related columns, but we support the parameter for consistency)
            var requestedColumns = new HashSet<string>();
            if (!string.IsNullOrWhiteSpace(additionalColumns))
            {
                requestedColumns = additionalColumns.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToHashSet();
            }

            var query = _db.Physicians.AsNoTracking();

            // Inactive filter
            if (inactive.HasValue)
            {
                query = query.Where(p => p.PhyInactive == inactive.Value);
            }

            // Type filter
            if (!string.IsNullOrWhiteSpace(type))
            {
                query = query.Where(p => p.PhyType == type);
            }

            // Date range filter
            if (fromDate.HasValue)
            {
                query = query.Where(p => p.PhyDateTimeCreated >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(p => p.PhyDateTimeCreated <= toDate.Value);
            }

            // Physician ID range filter
            if (minPhysicianId.HasValue)
            {
                query = query.Where(p => p.PhyID >= minPhysicianId.Value);
            }

            if (maxPhysicianId.HasValue)
            {
                query = query.Where(p => p.PhyID <= maxPhysicianId.Value);
            }

            // Text search
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var searchLower = searchText.ToLower().Trim();
                if (int.TryParse(searchText, out int searchInt))
                {
                    query = query.Where(p => p.PhyID == searchInt);
                }
                else
                {
                    query = query.Where(p =>
                        (p.PhyFirstName != null && p.PhyFirstName.ToLower().Contains(searchLower)) ||
                        (p.PhyLastName != null && p.PhyLastName.ToLower().Contains(searchLower)) ||
                        (p.PhyFullNameCC != null && p.PhyFullNameCC.ToLower().Contains(searchLower)) ||
                        (p.PhyNPI != null && p.PhyNPI.Contains(searchText)) ||
                        (p.PhyCity != null && p.PhyCity.ToLower().Contains(searchLower)));
                }
            }

            query = query.OrderByDescending(p => p.PhyID);

            // Smart count strategy
            int totalCount;
            bool hasFilters = inactive.HasValue || !string.IsNullOrWhiteSpace(type) ||
                             fromDate.HasValue || toDate.HasValue ||
                             minPhysicianId.HasValue || maxPhysicianId.HasValue || !string.IsNullOrWhiteSpace(searchText);

            if (!hasFilters)
            {
                try
                {
                    totalCount = await GetApproxPhysicianCountAsync();
                }
                catch
                {
                    totalCount = 10000;
                }
            }
            else
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                    totalCount = await query.CountAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Physician count query timed out, using approximate count");
                    try
                    {
                        totalCount = await GetApproxPhysicianCountAsync();
                    }
                    catch
                    {
                        totalCount = effectivePageSize * (effectivePage + 10);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error getting physician count");
                    try
                    {
                        totalCount = await GetApproxPhysicianCountAsync();
                    }
                    catch
                    {
                        totalCount = effectivePageSize * (effectivePage + 10);
                    }
                }
            }

            var data = await query
                .Skip((effectivePage - 1) * effectivePageSize)
                .Take(effectivePageSize)
                .Select(p => new PhysicianListItemDto
                {
                    PhyID = p.PhyID,
                    PhyDateTimeCreated = p.PhyDateTimeCreated,
                    PhyFirstName = p.PhyFirstName,
                    PhyLastName = p.PhyLastName,
                    PhyFullNameCC = p.PhyFullNameCC,
                    PhyName = p.PhyName,
                    PhyType = p.PhyType,
                    PhyRateClass = p.PhyRateClass,
                    PhyNPI = p.PhyNPI,
                    PhySpecialtyCode = p.PhySpecialtyCode,
                    PhyPrimaryCodeType = p.PhyPrimaryCodeType,
                    PhyAddress1 = p.PhyAddress1,
                    PhyCity = p.PhyCity,
                    PhyState = p.PhyState,
                    PhyZip = p.PhyZip,
                    PhyTelephone = p.PhyTelephone,
                    PhyInactive = p.PhyInactive,
                    AdditionalColumns = new Dictionary<string, object?>()
                })
                .ToListAsync();

            return Ok(new ApiResponse<List<PhysicianListItemDto>>
            {
                Data = data,
                Meta = new PaginationMetaDto
                {
                    Page = effectivePage,
                    PageSize = effectivePageSize,
                    TotalCount = totalCount
                }
            });
        }

        [HttpGet("available-columns")]
        public IActionResult GetAvailableColumns()
        {
            var availableColumns = RelatedColumnConfig.GetAvailableColumns()["Physician"];
            return Ok(new ApiResponse<List<RelatedColumnDefinition>>
            {
                Data = availableColumns
            });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetPhysicianById(int id)
        {
            if (id <= 0)
            {
                return BadRequest(new ErrorResponseDto
                {
                    ErrorCode = "INVALID_ARGUMENT",
                    Message = "Invalid physician ID"
                });
            }

            var physician = await _db.Physicians
                .AsNoTracking()
                .Where(p => p.PhyID == id)
                .Select(p => new PhysicianDetailDto
                {
                    PhyID = p.PhyID,
                    PhyName = p.PhyName,
                    PhyPrimaryCodeType = p.PhyPrimaryCodeType,
                    PhyType = p.PhyType,
                    PhyLastName = p.PhyLastName,
                    PhyFirstName = p.PhyFirstName,
                    PhyMiddleName = p.PhyMiddleName,
                    PhyAddress1 = p.PhyAddress1,
                    PhyAddress2 = p.PhyAddress2,
                    PhyCity = p.PhyCity,
                    PhyState = p.PhyState,
                    PhyZip = p.PhyZip,
                    PhyTelephone = p.PhyTelephone,
                    PhyFax = p.PhyFax,
                    PhyEMail = p.PhyEMail,
                    PhySpecialtyCode = p.PhySpecialtyCode,
                    PhyInactive = p.PhyInactive,
                    PhyNPI = p.PhyNPI,
                    PhyEntityType = p.PhyEntityType,
                    PhyPrimaryIDCode = p.PhyPrimaryIDCode,
                    PhyDateTimeCreated = p.PhyDateTimeCreated,
                    PhyDateTimeModified = p.PhyDateTimeModified
                })
                .FirstOrDefaultAsync();

            if (physician == null)
            {
                return NotFound(new ErrorResponseDto
                {
                    ErrorCode = "NOT_FOUND",
                    Message = "Physician not found"
                });
            }

            return Ok(new ApiResponse<PhysicianDetailDto>
            {
                Data = physician
            });
        }

        [HttpPost]
        public async Task<IActionResult> CreatePhysician([FromBody] CreatePhysicianDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ErrorResponseDto
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Invalid physician data"
                });
            }

            if (_currentContext.TenantId <= 0 || _currentContext.FacilityId <= 0)
            {
                return BadRequest(new ErrorResponseDto
                {
                    ErrorCode = "INVALID_CONTEXT",
                    Message = "Tenant and facility scope are required (X-Tenant-Key and X-Facility-Id)."
                });
            }

            var utcNow = DateTime.UtcNow;

            // Check for duplicate NPI if provided
            if (!string.IsNullOrWhiteSpace(dto.PhyNPI))
            {
                var existingByNpi = await _db.Physicians
                    .AnyAsync(p => p.PhyNPI == dto.PhyNPI.Trim());
                
                if (existingByNpi)
                {
                    return Conflict(new ErrorResponseDto
                    {
                        ErrorCode = "DUPLICATE_NPI",
                        Message = "A physician with this NPI already exists"
                    });
                }
            }

            // Normalize and trim all string fields
            var physician = new Infrastructure.Persistence.Entities.Physician
            {
                TenantId = _currentContext.TenantId,
                FacilityId = _currentContext.FacilityId,
                PhyDateTimeCreated = utcNow,
                PhyDateTimeModified = utcNow,
                PhyName = dto.PhyName?.Trim() ?? string.Empty,
                PhyPrimaryCodeType = NormalizeString(dto.PhyPrimaryCodeType, 2),
                PhyType = dto.PhyType?.Trim() ?? "Person",
                PhyLastName = NormalizeString(dto.PhyLastName, 60),
                PhyFirstName = NormalizeString(dto.PhyFirstName, 35),
                PhyMiddleName = NormalizeString(dto.PhyMiddleName, 25),
                PhyAddress1 = NormalizeString(dto.PhyAddress1, 55),
                PhyAddress2 = NormalizeString(dto.PhyAddress2, 55),
                PhyCity = NormalizeString(dto.PhyCity, 50),
                PhyState = NormalizeString(dto.PhyState, 2)?.ToUpperInvariant(),
                PhyZip = NormalizeString(dto.PhyZip, 15),
                PhyTelephone = NormalizeString(dto.PhyTelephone, 80),
                PhyFax = NormalizeString(dto.PhyFax, 80),
                PhyEMail = NormalizeString(dto.PhyEMail, 80),
                PhySpecialtyCode = NormalizeString(dto.PhySpecialtyCode, 30),
                PhyInactive = dto.PhyInactive,
                PhyNPI = NormalizeString(dto.PhyNPI, 20),
                PhyEntityType = NormalizeString(dto.PhyEntityType, 1),
                PhyPrimaryIDCode = NormalizeString(dto.PhyPrimaryIDCode, 80),
                PhyFirstMiddleLastNameCC = string.Empty,
                PhyFullNameCC = string.Empty,
                PhyNameWithInactiveCC = string.Empty,
                PhyCityStateZipCC = string.Empty
            };

            await _db.Physicians.AddAsync(physician);
            await _db.SaveChangesAsync();

            var result = new PhysicianDetailDto
            {
                PhyID = physician.PhyID,
                PhyName = physician.PhyName,
                PhyPrimaryCodeType = physician.PhyPrimaryCodeType,
                PhyType = physician.PhyType,
                PhyLastName = physician.PhyLastName,
                PhyFirstName = physician.PhyFirstName,
                PhyMiddleName = physician.PhyMiddleName,
                PhyAddress1 = physician.PhyAddress1,
                PhyAddress2 = physician.PhyAddress2,
                PhyCity = physician.PhyCity,
                PhyState = physician.PhyState,
                PhyZip = physician.PhyZip,
                PhyTelephone = physician.PhyTelephone,
                PhyFax = physician.PhyFax,
                PhyEMail = physician.PhyEMail,
                PhySpecialtyCode = physician.PhySpecialtyCode,
                PhyInactive = physician.PhyInactive,
                PhyNPI = physician.PhyNPI,
                PhyPrimaryIDCode = physician.PhyPrimaryIDCode,
                PhyDateTimeCreated = physician.PhyDateTimeCreated,
                PhyDateTimeModified = physician.PhyDateTimeModified
            };

            return CreatedAtAction(nameof(GetPhysicianById), new { id = physician.PhyID }, new ApiResponse<PhysicianDetailDto>
            {
                Data = result
            });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdatePhysician(int id, [FromBody] UpdatePhysicianDto dto)
        {
            _logger.LogInformation("UpdatePhysician called with id={Id}", id);

            if (id <= 0)
            {
                return BadRequest(new ErrorResponseDto
                {
                    ErrorCode = "INVALID_ARGUMENT",
                    Message = "Invalid physician ID"
                });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(new ErrorResponseDto
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Invalid physician data"
                });
            }

            var physician = await _db.Physicians.FindAsync(id);
            if (physician == null)
            {
                return NotFound(new ErrorResponseDto
                {
                    ErrorCode = "NOT_FOUND",
                    Message = "Physician not found"
                });
            }

            // Check for duplicate NPI if provided and changed
            if (!string.IsNullOrWhiteSpace(dto.PhyNPI) && physician.PhyNPI != dto.PhyNPI.Trim())
            {
                var existingByNpi = await _db.Physicians
                    .AnyAsync(p => p.PhyNPI == dto.PhyNPI.Trim() && p.PhyID != id);
                
                if (existingByNpi)
                {
                    return Conflict(new ErrorResponseDto
                    {
                        ErrorCode = "DUPLICATE_NPI",
                        Message = "A physician with this NPI already exists"
                    });
                }
            }

            // Update fields with normalization
            physician.PhyName = dto.PhyName?.Trim() ?? string.Empty;
            physician.PhyPrimaryCodeType = NormalizeString(dto.PhyPrimaryCodeType, 2);
            physician.PhyType = dto.PhyType?.Trim() ?? "Person";
            physician.PhyLastName = NormalizeString(dto.PhyLastName, 60);
            physician.PhyFirstName = NormalizeString(dto.PhyFirstName, 35);
            physician.PhyMiddleName = NormalizeString(dto.PhyMiddleName, 25);
            physician.PhyAddress1 = NormalizeString(dto.PhyAddress1, 55);
            physician.PhyAddress2 = NormalizeString(dto.PhyAddress2, 55);
            physician.PhyCity = NormalizeString(dto.PhyCity, 50);
            physician.PhyState = NormalizeString(dto.PhyState, 2)?.ToUpperInvariant();
            physician.PhyZip = NormalizeString(dto.PhyZip, 15);
            physician.PhyTelephone = NormalizeString(dto.PhyTelephone, 80);
            physician.PhyFax = NormalizeString(dto.PhyFax, 80);
            physician.PhyEMail = NormalizeString(dto.PhyEMail, 80);
            physician.PhySpecialtyCode = NormalizeString(dto.PhySpecialtyCode, 30);
            physician.PhyInactive = dto.PhyInactive;
            physician.PhyNPI = NormalizeString(dto.PhyNPI, 20);
            physician.PhyEntityType = NormalizeString(dto.PhyEntityType, 1);
            physician.PhyPrimaryIDCode = NormalizeString(dto.PhyPrimaryIDCode, 80);

            await _db.SaveChangesAsync();

            var result = new PhysicianDetailDto
            {
                PhyID = physician.PhyID,
                PhyName = physician.PhyName,
                PhyPrimaryCodeType = physician.PhyPrimaryCodeType,
                PhyType = physician.PhyType,
                PhyLastName = physician.PhyLastName,
                PhyFirstName = physician.PhyFirstName,
                PhyMiddleName = physician.PhyMiddleName,
                PhyAddress1 = physician.PhyAddress1,
                PhyAddress2 = physician.PhyAddress2,
                PhyCity = physician.PhyCity,
                PhyState = physician.PhyState,
                PhyZip = physician.PhyZip,
                PhyTelephone = physician.PhyTelephone,
                PhyFax = physician.PhyFax,
                PhyEMail = physician.PhyEMail,
                PhySpecialtyCode = physician.PhySpecialtyCode,
                PhyInactive = physician.PhyInactive,
                PhyNPI = physician.PhyNPI,
                PhyEntityType = physician.PhyEntityType,
                PhyPrimaryIDCode = physician.PhyPrimaryIDCode,
                PhyDateTimeCreated = physician.PhyDateTimeCreated,
                PhyDateTimeModified = physician.PhyDateTimeModified
            };

            return Ok(new ApiResponse<PhysicianDetailDto>
            {
                Data = result
            });
        }

        [HttpPost("import")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ImportPhysicians(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("CSV file required");

            if (_currentContext.TenantId <= 0 || _currentContext.FacilityId <= 0)
                return BadRequest(new ErrorResponseDto
                {
                    ErrorCode = "INVALID_CONTEXT",
                    Message = "Tenant and facility scope are required (X-Tenant-Key and X-Facility-Id)."
                });

            using var stream = file.OpenReadStream();
            using var reader = new StreamReader(stream);
            var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            var records = csv.GetRecords<dynamic>().ToList();

            int inserted = 0;
            int skipped = 0;

            foreach (var row in records)
            {
                var dict = row as IDictionary<string, object>;
                if (dict == null)
                {
                    skipped++;
                    continue;
                }

                string displayName = dict.ContainsKey("Display Name") ? dict["Display Name"]?.ToString() : "";
                string firstName = dict.ContainsKey("First Name") ? dict["First Name"]?.ToString() : "";
                string lastName = dict.ContainsKey("Last Name") ? dict["Last Name"]?.ToString() : "";
                string middleName = dict.ContainsKey("Middle Name") ? dict["Middle Name"]?.ToString() : "";
                string npi = dict.ContainsKey("NPI") ? dict["NPI"]?.ToString() : "";
                string type = dict.ContainsKey("Type") ? dict["Type"]?.ToString() : "Rendering";
                string taxonomy = dict.ContainsKey("Taxonomy Code") ? dict["Taxonomy Code"]?.ToString() : "";
                string phone = dict.ContainsKey("Phone #") ? dict["Phone #"]?.ToString() : "";
                string addr1 = dict.ContainsKey("Address 1") ? dict["Address 1"]?.ToString() : "";
                string addr2 = dict.ContainsKey("Address 2") ? dict["Address 2"]?.ToString() : "";
                string city = dict.ContainsKey("City") ? dict["City"]?.ToString() : "";
                string state = dict.ContainsKey("State") ? dict["State"]?.ToString() : "";
                string zip = dict.ContainsKey("Zip") ? dict["Zip"]?.ToString() : "";
                string email = dict.ContainsKey("Email") ? dict["Email"]?.ToString() : "";
                string fax = dict.ContainsKey("Fax") ? dict["Fax"]?.ToString() : "";

                if (string.IsNullOrWhiteSpace(npi))
                {
                    skipped++;
                    continue;
                }

                var utc = DateTime.UtcNow;
                var physician = new Physician
                {
                    TenantId = _currentContext.TenantId,
                    FacilityId = _currentContext.FacilityId,
                    PhyName = displayName,
                    PhyFirstName = firstName,
                    PhyLastName = lastName,
                    PhyMiddleName = middleName,
                    PhyNPI = npi,
                    PhyType = type,
                    PhySpecialtyCode = taxonomy,
                    PhyTelephone = phone,
                    PhyAddress1 = addr1,
                    PhyAddress2 = addr2,
                    PhyCity = city,
                    PhyState = state,
                    PhyZip = zip,
                    PhyEMail = email,
                    PhyFax = fax,
                    PhyInactive = false,
                    PhyDateTimeCreated = utc,
                    PhyDateTimeModified = utc,
                    PhyFirstMiddleLastNameCC = string.Empty,
                    PhyFullNameCC = string.Empty,
                    PhyNameWithInactiveCC = string.Empty,
                    PhyCityStateZipCC = string.Empty
                };

                var existing = await _db.Physicians
                    .FirstOrDefaultAsync(p => p.PhyNPI == physician.PhyNPI);

                if (existing != null)
                {
                    existing.PhyName = physician.PhyName;
                    existing.PhyFirstName = physician.PhyFirstName;
                    existing.PhyLastName = physician.PhyLastName;
                    existing.PhyMiddleName = physician.PhyMiddleName;
                    existing.PhyType = physician.PhyType;
                    existing.PhySpecialtyCode = physician.PhySpecialtyCode;
                    existing.PhyTelephone = physician.PhyTelephone;
                    existing.PhyAddress1 = physician.PhyAddress1;
                    existing.PhyAddress2 = physician.PhyAddress2;
                    existing.PhyCity = physician.PhyCity;
                    existing.PhyState = physician.PhyState;
                    existing.PhyZip = physician.PhyZip;
                    existing.PhyEMail = physician.PhyEMail;
                    existing.PhyFax = physician.PhyFax;
                    existing.PhyInactive = false;
                    existing.PhyDateTimeModified = DateTime.UtcNow;
                }
                else
                {
                    _db.Physicians.Add(physician);
                    inserted++;
                }
            }

            await _db.SaveChangesAsync();

            return Ok(new
            {
                inserted,
                skipped
            });
        }

        private string? NormalizeString(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            
            var trimmed = value.Trim();
            if (trimmed.Length > maxLength)
                trimmed = trimmed.Substring(0, maxLength);
            
            return trimmed.Length == 0 ? null : trimmed;
        }

        private async Task<int> GetApproxPhysicianCountAsync()
        {
            try
            {
                const string sql =
@"SELECT CAST(ISNULL(SUM(p.[rows]), 0) AS int) AS [Value]
  FROM sys.partitions p
  WHERE p.object_id = OBJECT_ID(N'[dbo].[Physician]')
    AND p.index_id IN (0, 1)";

                return await _db.Database.SqlQueryRaw<int>(sql).SingleAsync();
            }
            catch
            {
                return 0;
            }
        }
    }
}
