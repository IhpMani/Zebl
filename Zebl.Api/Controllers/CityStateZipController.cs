using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;

namespace Zebl.Api.Controllers;

[ApiController]
[Route("api/city-state-zip")]
[Authorize(Policy = "RequireAuth")]
public class CityStateZipController : ControllerBase
{
    private readonly ZeblDbContext _db;
    private readonly ILogger<CityStateZipController> _logger;

    public CityStateZipController(ZeblDbContext db, ILogger<CityStateZipController> logger)
    {
        _db = db;
        _logger = logger;
    }

    public sealed class CityStateZipRowDto
    {
        public int Id { get; set; }
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Zip { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }

    public sealed class PagedResultDto<T>
    {
        public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
        public int Total { get; set; }
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 500,
        [FromQuery] string? search = null,
        [FromQuery] string? state = null)
    {
        if (page < 1 || pageSize < 1 || pageSize > 1000)
        {
            return BadRequest(new { error = "Invalid paging arguments." });
        }

        var query = _db.CityStateZipLibraries.AsNoTracking().Where(x => x.IsActive);

        if (!string.IsNullOrWhiteSpace(state))
        {
            var s = state.Trim().ToUpperInvariant();
            query = query.Where(x => x.State == s);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.City.ToLower().Contains(term) ||
                x.State.ToLower().Contains(term) ||
                x.Zip.ToLower().Contains(term));
        }

        query = query
            .OrderBy(x => x.City)
            .ThenBy(x => x.State)
            .ThenBy(x => x.Zip);

        var total = await query.CountAsync();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new CityStateZipRowDto
            {
                Id = x.Id,
                City = x.City,
                State = x.State,
                Zip = x.Zip,
                IsActive = x.IsActive
            })
            .ToListAsync();

        return Ok(new PagedResultDto<CityStateZipRowDto>
        {
            Items = items,
            Total = total
        });
    }

    public sealed class CreateCityStateZipRequest
    {
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Zip { get; set; } = string.Empty;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCityStateZipRequest request)
    {
        if (request == null)
        {
            return BadRequest(new { error = "Request body is required." });
        }

        var city = (request.City ?? string.Empty).Trim();
        var state = (request.State ?? string.Empty).Trim().ToUpperInvariant();
        var zip = (request.Zip ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(city) || string.IsNullOrWhiteSpace(state) || string.IsNullOrWhiteSpace(zip))
        {
            return BadRequest(new { error = "City, State, and Zip are required." });
        }

        var now = DateTime.UtcNow;
        var entity = new CityStateZipLibrary
        {
            City = city,
            State = state,
            Zip = zip,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.CityStateZipLibraries.Add(entity);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAll), new { id = entity.Id }, new CityStateZipRowDto
        {
            Id = entity.Id,
            City = entity.City,
            State = entity.State,
            Zip = entity.Zip,
            IsActive = entity.IsActive
        });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateCityStateZipRequest request)
    {
        var entity = await _db.CityStateZipLibraries.FindAsync(id);
        if (entity == null)
        {
            return NotFound(new { error = "Record not found." });
        }

        var city = (request.City ?? string.Empty).Trim();
        var state = (request.State ?? string.Empty).Trim().ToUpperInvariant();
        var zip = (request.Zip ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(city) || string.IsNullOrWhiteSpace(state) || string.IsNullOrWhiteSpace(zip))
        {
            return BadRequest(new { error = "City, State, and Zip are required." });
        }

        entity.City = city;
        entity.State = state;
        entity.Zip = zip;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _db.CityStateZipLibraries.FindAsync(id);
        if (entity == null)
        {
            return NotFound(new { error = "Record not found." });
        }

        _db.CityStateZipLibraries.Remove(entity);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    public sealed class BulkDeleteRequest
    {
        public List<int> Ids { get; set; } = new();
    }

    [HttpPost("bulk-delete")]
    public async Task<IActionResult> BulkDelete([FromBody] BulkDeleteRequest request)
    {
        if (request?.Ids == null || request.Ids.Count == 0)
        {
            return BadRequest(new { error = "No ids supplied." });
        }

        var toDelete = await _db.CityStateZipLibraries
            .Where(x => request.Ids.Contains(x.Id))
            .ToListAsync();

        _db.CityStateZipLibraries.RemoveRange(toDelete);
        await _db.SaveChangesAsync();

        return Ok(new { deleted = toDelete.Count });
    }

    public sealed class BulkSaveRow
    {
        public int? Id { get; set; }
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Zip { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }

    public sealed class BulkSaveRequest
    {
        public List<BulkSaveRow> Rows { get; set; } = new();
    }

    [HttpPost("bulk-save")]
    public async Task<IActionResult> BulkSave([FromBody] BulkSaveRequest request)
    {
        if (request?.Rows == null || request.Rows.Count == 0)
        {
            return BadRequest(new { error = "No rows supplied." });
        }

        var now = DateTime.UtcNow;
        foreach (var row in request.Rows)
        {
            var city = (row.City ?? string.Empty).Trim();
            var state = (row.State ?? string.Empty).Trim().ToUpperInvariant();
            var zip = (row.Zip ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(city) || string.IsNullOrWhiteSpace(state) || string.IsNullOrWhiteSpace(zip))
                continue;

            if (row.Id.HasValue && row.Id.Value > 0)
            {
                var existing = await _db.CityStateZipLibraries.FindAsync(row.Id.Value);
                if (existing == null) continue;

                existing.City = city;
                existing.State = state;
                existing.Zip = zip;
                existing.IsActive = row.IsActive;
                existing.UpdatedAt = now;
            }
            else
            {
                var entity = new CityStateZipLibrary
                {
                    City = city,
                    State = state,
                    Zip = zip,
                    IsActive = row.IsActive,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _db.CityStateZipLibraries.Add(entity);
            }
        }

        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }
}

