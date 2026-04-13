using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Zebl.Api.Configuration;
using SecurityClaim = System.Security.Claims.Claim;

namespace Zebl.Api.Services;

public interface IJwtTokenIssuer
{
    string IssueSuperAdminToken(Guid userGuid, string userName, bool isAdmin, string sessionStamp);

    string IssueOperationalToken(
        Guid userGuid,
        string userName,
        bool isAdmin,
        int tenantId,
        int? facilityId,
        string tenantKey,
        bool impersonation,
        string sessionStamp);

    DateTime GetUtcExpiry();
}

public sealed class JwtTokenIssuer : IJwtTokenIssuer
{
    private readonly JwtSettings _jwt;

    public JwtTokenIssuer(JwtSettings jwt)
    {
        _jwt = jwt;
    }

    public DateTime GetUtcExpiry() =>
        DateTime.UtcNow.AddMinutes(_jwt.ExpirationMinutes);

    public string IssueSuperAdminToken(Guid userGuid, string userName, bool isAdmin, string sessionStamp) =>
        Issue(
            userGuid,
            userName,
            isAdmin,
            isSuperAdmin: true,
            tenantId: null,
            facilityId: null,
            tenantKey: null,
            impersonation: false,
            sessionStamp);

    public string IssueOperationalToken(
        Guid userGuid,
        string userName,
        bool isAdmin,
        int tenantId,
        int? facilityId,
        string tenantKey,
        bool impersonation,
        string sessionStamp)
    {
        var tk = string.IsNullOrWhiteSpace(tenantKey) ? null : tenantKey.Trim().ToLowerInvariant();
        return Issue(
            userGuid,
            userName,
            isAdmin,
            isSuperAdmin: false,
            tenantId,
            facilityId,
            tk,
            impersonation,
            sessionStamp);
    }

    private string Issue(
        Guid userGuid,
        string userName,
        bool isAdmin,
        bool isSuperAdmin,
        int? tenantId,
        int? facilityId,
        string? tenantKey,
        bool impersonation,
        string sessionStamp)
    {
        if (string.IsNullOrWhiteSpace(_jwt.SecretKey))
            throw new InvalidOperationException("JWT SecretKey is not configured.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claimList = new List<SecurityClaim>
        {
            new(JwtRegisteredClaimNames.Sub, userGuid.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, userName),
            new(ClaimTypes.Name, userName),
            new("UserGuid", userGuid.ToString()),
            new("UserName", userName),
            new("isSuperAdmin", isSuperAdmin ? "true" : "false"),
            new("IsAdmin", isAdmin ? "true" : "false"),
            new("sessionStamp", sessionStamp)
        };

        if (impersonation)
            claimList.Add(new SecurityClaim("impersonation", "true"));

        if (!isSuperAdmin && tenantId is > 0)
            claimList.Add(new SecurityClaim("tenantId", tenantId.Value.ToString()));

        if (!isSuperAdmin && facilityId is > 0)
            claimList.Add(new SecurityClaim("facilityId", facilityId.Value.ToString()));

        if (!isSuperAdmin && !string.IsNullOrWhiteSpace(tenantKey))
            claimList.Add(new SecurityClaim("tenantKey", tenantKey!));

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claimList,
            notBefore: DateTime.UtcNow,
            expires: GetUtcExpiry(),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
