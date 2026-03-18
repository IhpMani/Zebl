using Microsoft.EntityFrameworkCore;
using Zebl.Application.Domain;
using Zebl.Application.Services;
using Zebl.Application.Repositories;
using Zebl.Infrastructure.Persistence.Context;

namespace Zebl.Infrastructure.Services;

public class ClaimScrubService : IClaimScrubService
{
    private readonly ZeblDbContext _context;
    private readonly IScrubRuleRepository _ruleRepository;

    public ClaimScrubService(ZeblDbContext context, IScrubRuleRepository ruleRepository)
    {
        _context = context;
        _ruleRepository = ruleRepository;
    }

    public async Task<IReadOnlyList<ScrubResult>> ScrubClaimAsync(int claimId)
    {
        var claim = await _context.Claims
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ClaID == claimId);

        if (claim == null)
            return Array.Empty<ScrubResult>();

        var primaryInsured = await _context.Claim_Insureds
            .AsNoTracking()
            .Where(ci => ci.ClaInsClaFID == claimId && ci.ClaInsSequence == 1)
            .Select(ci => (int?)ci.ClaInsPayFID)
            .FirstOrDefaultAsync();
        int? payerId = primaryInsured;

        var serviceLines = await _context.Service_Lines
            .AsNoTracking()
            .Where(s => s.SrvClaFID == claimId)
            .ToListAsync();

        // ProgramId is not yet modeled; pass null for now.
        var rules = await _ruleRepository.GetActiveAsync(payerId, null);
        var results = new List<ScrubResult>();

        foreach (var rule in rules)
        {
            if (string.Equals(rule.Scope, "Claim", StringComparison.OrdinalIgnoreCase))
            {
                if (EvaluateCondition(rule.Condition, claim, serviceLines))
                {
                    results.Add(new ScrubResult
                    {
                        RuleName = rule.Name,
                        Severity = rule.Severity,
                        Message = rule.Condition,
                        AffectedField = "Claim"
                    });
                }
            }
            else if (string.Equals(rule.Scope, "ServiceLine", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var srv in serviceLines)
                {
                    if (EvaluateCondition(rule.Condition, claim, new[] { srv }))
                    {
                        results.Add(new ScrubResult
                        {
                            RuleName = rule.Name,
                            Severity = rule.Severity,
                            Message = rule.Condition,
                            AffectedField = $"ServiceLine:{srv.SrvID}"
                        });
                    }
                }
            }
        }

        return results;
    }

    private static bool EvaluateCondition(string condition, Persistence.Entities.Claim claim, IEnumerable<Persistence.Entities.Service_Line> serviceLines)
    {
        if (string.IsNullOrWhiteSpace(condition))
            return false;

        // Simple DSL: FIELD OP VALUE, where FIELD ∈ {TotalCharge, TotalBalance, ServiceLineCount}
        var parts = condition.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
            return false;

        var field = parts[0];
        var op = parts[1];
        var valueText = parts[2];

        decimal leftDecimal;
        int leftInt;

        switch (field)
        {
            case "TotalCharge":
                leftDecimal = claim.ClaTotalChargeTRIG;
                if (!decimal.TryParse(valueText, out var rightDec)) return false;
                return CompareDecimal(leftDecimal, rightDec, op);

            case "TotalBalance":
                leftDecimal = claim.ClaTotalBalanceCC ?? 0m;
                if (!decimal.TryParse(valueText, out rightDec)) return false;
                return CompareDecimal(leftDecimal, rightDec, op);

            case "ServiceLineCount":
                leftInt = serviceLines.Count();
                if (!int.TryParse(valueText, out var rightInt)) return false;
                return CompareInt(leftInt, rightInt, op);

            default:
                return false;
        }
    }

    private static bool CompareDecimal(decimal left, decimal right, string op) =>
        op switch
        {
            ">" => left > right,
            ">=" => left >= right,
            "<" => left < right,
            "<=" => left <= right,
            "==" => left == right,
            "!=" => left != right,
            _ => false
        };

    private static bool CompareInt(int left, int right, string op) =>
        op switch
        {
            ">" => left > right,
            ">=" => left >= right,
            "<" => left < right,
            "<=" => left <= right,
            "==" => left == right,
            "!=" => left != right,
            _ => false
        };
}

