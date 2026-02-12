using Microsoft.EntityFrameworkCore;
using Zebl.Application.Dtos.Schema;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;

namespace Zebl.Api.Services;

/// <summary>
/// Enterprise-grade metadata service using EF Core model introspection
/// Source of truth: EF Core entity model and DbContext configuration
/// </summary>
public class EntityMetadataService
{
    private readonly ZeblDbContext _dbContext;

    // Audit field patterns to exclude
    private static readonly HashSet<string> AuditFieldPatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        "DateTimeCreated", "DateTimeModified",
        "CreatedUserGUID", "LastUserGUID",
        "CreatedUserName", "LastUserName",
        "CreatedComputerName", "LastComputerName"
    };

    // Map entity types to their names
    private static readonly Dictionary<Type, string> EntityTypeMap = new()
    {
        [typeof(Claim)] = "Claim",
        [typeof(Patient)] = "Patient",
        [typeof(Payment)] = "Payment",
        [typeof(Adjustment)] = "Adjustment",
        [typeof(Disbursement)] = "Disbursement",
        [typeof(Payer)] = "Payer",
        [typeof(Physician)] = "Physician",
        [typeof(Service_Line)] = "Service_Line",
        [typeof(Procedure_Code)] = "Procedure_Code"
    };

    // Configure display columns for referenced entities
    private static readonly Dictionary<string, string> EntityDisplayColumns = new()
    {
        ["Patient"] = "PatLastName",
        ["Physician"] = "PhyLastName",
        ["Payer"] = "PayName",
        ["Procedure_Code"] = "ProcCode"
    };

    public EntityMetadataService(ZeblDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public EntityColumnsResponse GetEntityColumns(string entityName)
    {
        // Check if entity is in the UI whitelist
        if (!UIColumnRegistry.IsEntitySupported(entityName))
        {
            throw new ArgumentException($"Entity '{entityName}' is not supported in UI column registry");
        }

        var entityType = EntityTypeMap.FirstOrDefault(kvp => kvp.Value == entityName).Key;
        
        if (entityType == null)
        {
            throw new ArgumentException($"Entity '{entityName}' is not mapped");
        }

        var efEntityType = _dbContext.Model.FindEntityType(entityType);
        if (efEntityType == null)
        {
            throw new InvalidOperationException($"Entity type '{entityName}' not found in EF model");
        }

        // Get the whitelist of allowed columns for this entity
        var allowedColumns = UIColumnRegistry.GetAllowedColumns(entityName);
        var foreignKeys = efEntityType.GetForeignKeys().ToList();

        var columns = new List<ColumnMetadataDto>();

        // Only process properties that are in the whitelist
        foreach (var allowedColumnName in allowedColumns)
        {
            var property = efEntityType.GetProperties()
                .FirstOrDefault(p => p.Name.Equals(allowedColumnName, StringComparison.OrdinalIgnoreCase));

            if (property == null)
            {
                // Column is in whitelist but not found in EF model - log warning but continue
                continue;
            }

            // Skip shadow properties
            if (property.IsShadowProperty())
                continue;

            // Check if this property is part of a foreign key
            var fk = foreignKeys.FirstOrDefault(f => 
                f.Properties.Any(p => p.Name == property.Name));

            var columnMeta = new ColumnMetadataDto
            {
                ColumnName = ToCamelCase(property.Name),
                DisplayName = ToDisplayName(property.Name),
                DataType = GetFrontendDataType(property.ClrType),
                IsNullable = property.IsNullable,
                IsSortable = true,
                IsFilterable = true,
                Category = DetermineCategory(property.Name, fk != null)
            };

            if (fk != null)
            {
                var principalEntityType = fk.PrincipalEntityType;
                var referencedEntityName = EntityTypeMap.FirstOrDefault(kvp => 
                    kvp.Key == principalEntityType.ClrType).Value;

                if (referencedEntityName != null)
                {
                    columnMeta.IsForeignKey = true;
                    columnMeta.ReferenceTable = referencedEntityName;
                    columnMeta.ReferenceDisplayColumn = EntityDisplayColumns.ContainsKey(referencedEntityName)
                        ? ToCamelCase(EntityDisplayColumns[referencedEntityName])
                        : null;
                }
            }

            columns.Add(columnMeta);
        }

        return new EntityColumnsResponse
        {
            EntityName = entityName,
            Columns = columns.OrderBy(c => GetCategoryOrder(c.Category))
                            .ThenBy(c => c.DisplayName)
                            .ToList()
        };
    }

    public List<string> GetAvailableEntities()
    {
        // Only return entities that are in the UI whitelist
        return UIColumnRegistry.GetSupportedEntities();
    }

    public bool IsEntitySupported(string entityName)
    {
        // Check against UI whitelist, not just EF model
        return UIColumnRegistry.IsEntitySupported(entityName);
    }

    private bool IsAuditField(string propertyName)
    {
        return AuditFieldPatterns.Any(pattern => 
            propertyName.EndsWith(pattern, StringComparison.OrdinalIgnoreCase));
    }

    private string ToCamelCase(string pascalCase)
    {
        if (string.IsNullOrEmpty(pascalCase))
            return pascalCase;
        
        return char.ToLowerInvariant(pascalCase[0]) + pascalCase.Substring(1);
    }

    private string ToDisplayName(string propertyName)
    {
        // Split on capital letters
        var result = System.Text.RegularExpressions.Regex.Replace(
            propertyName, 
            "([A-Z])", 
            " $1", 
            System.Text.RegularExpressions.RegexOptions.Compiled);
        
        return result.Trim();
    }

    private string GetFrontendDataType(Type clrType)
    {
        var underlyingType = Nullable.GetUnderlyingType(clrType) ?? clrType;

        if (underlyingType == typeof(int) || underlyingType == typeof(long) || underlyingType == typeof(short) || underlyingType == typeof(byte))
            return "int";
        if (underlyingType == typeof(decimal) || underlyingType == typeof(double) || underlyingType == typeof(float))
            return "decimal";
        if (underlyingType == typeof(bool))
            return "bool";
        if (underlyingType == typeof(DateTime))
            return "datetime";
        if (underlyingType == typeof(DateOnly))
            return "date";
        if (underlyingType == typeof(Guid))
            return "guid";
        if (underlyingType == typeof(string))
            return "string";

        return "string";
    }

    private string DetermineCategory(string propertyName, bool isForeignKey)
    {
        var lower = propertyName.ToLowerInvariant();

        if (lower.EndsWith("id") && lower.Length <= 6)
            return "Identity";
        if (isForeignKey || lower.EndsWith("fid"))
            return "References";
        if (lower.Contains("charge") || lower.Contains("payment") || lower.Contains("balance") || 
            lower.Contains("amount") || lower.Contains("paid") || lower.Contains("disburs"))
            return "Financial";
        if (lower.Contains("date") || lower.Contains("time"))
            return "Dates";
        if (lower.Contains("status") || lower.Contains("classification") || lower.Contains("active") ||
            lower.Contains("locked") || lower.Contains("archived"))
            return "Status";
        if (lower.Contains("diagnosis") || lower.Contains("procedure") || lower.Contains("icd") ||
            lower.Contains("patient") && (lower.Contains("status") || lower.Contains("condition")))
            return "Clinical";
        if (lower.Contains("bill") || lower.Contains("invoice") || lower.Contains("submission"))
            return "Billing";
        if (lower.Contains("name") || lower.Contains("birth") || lower.Contains("sex") || lower.Contains("ssn"))
            return "Demographics";
        if (lower.Contains("address") || lower.Contains("phone") || lower.Contains("email") ||
            lower.Contains("city") || lower.Contains("state") || lower.Contains("zip"))
            return "Contact";
        if (lower.Contains("custom"))
            return "Custom";
        if (lower.Contains("trig") || lower.Contains("cc"))
            return "Computed";

        return "General";
    }

    private int GetCategoryOrder(string category)
    {
        return category switch
        {
            "Identity" => 1,
            "Demographics" => 2,
            "Contact" => 3,
            "Status" => 4,
            "Financial" => 5,
            "Dates" => 6,
            "Clinical" => 7,
            "Billing" => 8,
            "References" => 9,
            "Custom" => 10,
            "Computed" => 11,
            "General" => 12,
            _ => 99
        };
    }
}
