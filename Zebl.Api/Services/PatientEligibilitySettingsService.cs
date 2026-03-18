using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Zebl.Application.Abstractions;
using Zebl.Infrastructure.Services;

namespace Zebl.Api.Services;

/// <summary>
/// Handles patient eligibility settings: mask password in API responses,
/// encrypt password before saving. Credentials are for clearinghouse only, not application login.
/// </summary>
public class PatientEligibilitySettingsService : IEligibilitySettingsProvider
{
    private const string SectionName = "patientEligibility";
    private const string PasswordEncryptionPurpose = "Zebl.PatientEligibility.ClearinghousePassword";
    private const string EncryptedPasswordPrefix = "enc:";

    private readonly ProgramSettingsService _programSettings;
    private readonly IDataProtectionProvider _dataProtectionProvider;

    public PatientEligibilitySettingsService(
        ProgramSettingsService programSettings,
        IDataProtectionProvider dataProtectionProvider)
    {
        _programSettings = programSettings;
        _dataProtectionProvider = dataProtectionProvider;
    }

    public async Task<JsonElement> GetForApiAsync(CancellationToken cancellationToken = default)
    {
        var raw = await _programSettings.GetSectionAsync(SectionName, cancellationToken);
        return MaskPasswordInJson(raw);
    }

    public async Task SaveAsync(JsonElement settings, string? updatedBy, CancellationToken cancellationToken = default)
    {
        var raw = await _programSettings.GetSectionAsync(SectionName, cancellationToken);
        var dict = JsonElementToDictionary(raw);
        var incoming = JsonElementToDictionary(settings);

        foreach (var kv in incoming)
        {
            if (kv.Key.Equals("password", System.StringComparison.OrdinalIgnoreCase))
            {
                var plain = kv.Value?.ToString() ?? string.Empty;
                if (string.IsNullOrEmpty(plain) || plain == "********")
                {
                    if (dict.TryGetValue("password", out var existing) && existing != null && !string.IsNullOrEmpty(existing.ToString()))
                        dict["password"] = existing;
                    continue;
                }
                dict["password"] = EncryptPassword(plain);
            }
            else
            {
                dict[kv.Key] = kv.Value ?? string.Empty;
            }
        }

        var json = JsonSerializer.SerializeToUtf8Bytes(dict);
        using var doc = JsonDocument.Parse(json);
        await _programSettings.SaveSectionAsync(SectionName, doc.RootElement.Clone(), updatedBy, cancellationToken);
    }

    public async Task<EligibilitySettingsForCheckDto> GetForEligibilityCheckAsync(CancellationToken cancellationToken = default)
    {
        var raw = await _programSettings.GetSectionAsync(SectionName, cancellationToken);
        var dict = JsonElementToDictionary(raw);
        var dto = new EligibilitySettingsForCheckDto
        {
            Source = GetString(dict, "source") ?? string.Empty,
            ReceiverId = GetStringOrNull(dict, "receiverId"),
            ProviderMode = GetString(dict, "providerMode") ?? "PatientBillingProvider",
            SpecificProviderId = GetIntOrNull(dict, "specificProviderId"),
            Username = GetString(dict, "username") ?? string.Empty,
            Password = DecryptPasswordIfNeeded(GetString(dict, "password") ?? string.Empty),
            Server = GetString(dict, "server") ?? string.Empty,
            ShowEligibilityResponseViewer = GetBool(dict, "showEligibilityResponseViewer", true)
        };
        return dto;
    }

    private static JsonElement MaskPasswordInJson(JsonElement raw)
    {
        var dict = JsonElementToDictionary(raw);
        dict["password"] = string.Empty;
        var json = JsonSerializer.SerializeToUtf8Bytes(dict);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private static Dictionary<string, object> JsonElementToDictionary(JsonElement el)
    {
        var dict = new Dictionary<string, object>(System.StringComparer.OrdinalIgnoreCase);
        if (el.ValueKind != JsonValueKind.Object) return dict;
        foreach (var prop in el.EnumerateObject())
        {
            dict[prop.Name] = prop.Value.ValueKind switch
            {
                JsonValueKind.String => prop.Value.GetString() ?? string.Empty,
                JsonValueKind.Number => prop.Value.TryGetInt32(out var i) ? i : prop.Value.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => (object?)null!,
                _ => prop.Value.GetRawText()
            };
        }
        return dict;
    }

    private static string? GetString(Dictionary<string, object> d, string key)
    {
        if (!d.TryGetValue(key, out var v)) return null;
        return v?.ToString();
    }

    private static string? GetStringOrNull(Dictionary<string, object> d, string key)
    {
        var s = GetString(d, key);
        return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }

    private static int? GetIntOrNull(Dictionary<string, object> d, string key)
    {
        if (!d.TryGetValue(key, out var v) || v == null) return null;
        if (v is int i) return i;
        if (v is long l) return (int)l;
        if (v is double dbl) return (int)dbl;
        if (int.TryParse(v.ToString(), out var parsed)) return parsed;
        return null;
    }

    private static bool GetBool(Dictionary<string, object> d, string key, bool defaultValue)
    {
        if (!d.TryGetValue(key, out var v) || v == null) return defaultValue;
        if (v is bool b) return b;
        if (v is string s) return s.Trim().Equals("true", System.StringComparison.OrdinalIgnoreCase) || s == "1";
        return defaultValue;
    }

    private string EncryptPassword(string plainPassword)
    {
        var protector = _dataProtectionProvider.CreateProtector(PasswordEncryptionPurpose);
        var bytes = protector.Protect(Encoding.UTF8.GetBytes(plainPassword));
        return EncryptedPasswordPrefix + Convert.ToBase64String(bytes);
    }

    private string DecryptPasswordIfNeeded(string stored)
    {
        if (string.IsNullOrEmpty(stored)) return string.Empty;
        if (!stored.StartsWith(EncryptedPasswordPrefix, System.StringComparison.Ordinal))
            return stored;
        try
        {
            var base64 = stored.Substring(EncryptedPasswordPrefix.Length);
            var bytes = Convert.FromBase64String(base64);
            var protector = _dataProtectionProvider.CreateProtector(PasswordEncryptionPurpose);
            return Encoding.UTF8.GetString(protector.Unprotect(bytes));
        }
        catch
        {
            return string.Empty;
        }
    }
}
