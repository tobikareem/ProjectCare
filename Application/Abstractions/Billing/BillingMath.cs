using System.Security.Cryptography;
using System.Text;
using CarePath.Domain.Enumerations;

namespace CarePath.Application.Abstractions.Billing;

/// <summary>
/// Shared billing arithmetic (D-S6-18): break-aware billable minutes, away-from-zero currency
/// rounding, aging/risk rules, and the preview input fingerprint. One implementation so
/// preview, creation, and reconciliation can never disagree.
/// </summary>
public static class BillingMath
{
    /// <summary>Aged-risk threshold in days (documented D-S6-18 implementation constant).</summary>
    public const int AgedRiskThresholdDays = 7;

    /// <summary>Hours after scheduled end before a not-completed shift becomes a leakage candidate.</summary>
    public const int NotCompletedGraceHours = 24;

    /// <summary>Billable hours after unpaid breaks for captured actual times; null when not computable.</summary>
    /// <remarks>
    /// Deliberately pre-rounds to two decimals to match the persisted (18,2) column precision,
    /// so previewed hours equal stored line hours exactly. <c>Shift.BillableHours</c> stays
    /// full-precision for non-billing surfaces — do NOT "unify" the two.
    /// </remarks>
    public static decimal? BillableHours(BillingEligibilityRow row)
    {
        if (!row.ActualStartUtc.HasValue || !row.ActualEndUtc.HasValue)
        {
            return null;
        }

        var minutes = (decimal)(row.ActualEndUtc.Value - row.ActualStartUtc.Value).TotalMinutes - row.BreakMinutes;
        return minutes <= 0 ? null : Math.Round(minutes / 60m, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>Rounded line total for an eligible row; null when hours are not computable.</summary>
    public static decimal? LineTotal(BillingEligibilityRow row)
    {
        var hours = BillableHours(row);
        return hours.HasValue ? RoundCurrency(hours.Value * row.BillRate) : null;
    }

    /// <summary>
    /// Estimated billable value for reconciliation rows: actual-time math when valid, otherwise
    /// the scheduled window minus break; null when the rate is missing or time is not computable.
    /// </summary>
    public static decimal? EstimatedValue(BillingEligibilityRow row)
    {
        if (row.BillRate <= 0)
        {
            return null;
        }

        var actual = LineTotal(row);
        if (actual.HasValue)
        {
            return actual;
        }

        var scheduledMinutes = (decimal)(row.ScheduledEndUtc - row.ScheduledStartUtc).TotalMinutes - row.BreakMinutes;
        return scheduledMinutes <= 0
            ? null
            : RoundCurrency(Math.Round(scheduledMinutes / 60m, 2, MidpointRounding.AwayFromZero) * row.BillRate);
    }

    /// <summary>Rounds a currency amount to two decimals, away from zero.</summary>
    public static decimal RoundCurrency(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>True when the row counts toward revenue at risk (D-S6-18 rules).</summary>
    public static bool IsRevenueAtRisk(BillingEligibilityRow row) => row.Reason is
        BillingExclusionReason.NotCompleted
        or BillingExclusionReason.MissingActualTime
        or BillingExclusionReason.InvalidBillableTime
        or BillingExclusionReason.MissingBillRate;

    /// <summary>Whole days between the service date and now (UTC), never negative.</summary>
    public static int AgeDays(BillingEligibilityRow row, DateTime utcNow) =>
        Math.Max(0, (int)(utcNow.Date - row.ScheduledStartUtc.Date).TotalDays);

    /// <summary>True when an at-risk row has aged past <see cref="AgedRiskThresholdDays"/>.</summary>
    public static bool IsAgedRisk(BillingEligibilityRow row, DateTime utcNow) =>
        IsRevenueAtRisk(row) && AgeDays(row, utcNow) >= AgedRiskThresholdDays;

    /// <summary>
    /// Deterministic SHA-256 fingerprint over the billable inputs of the eligible rows
    /// (ordered by shift ID): shift ID, actual window, break, rate, and shift update stamp.
    /// Any change to any input changes the hash and stales the preview token.
    /// </summary>
    public static string ComputeInputsHash(IEnumerable<BillingEligibilityRow> eligibleRows)
    {
        var builder = new StringBuilder();
        foreach (var row in eligibleRows.OrderBy(row => row.ShiftId))
        {
            builder.Append(row.ShiftId.ToString("N"))
                .Append('|').Append(row.ActualStartUtc?.Ticks ?? 0)
                .Append('|').Append(row.ActualEndUtc?.Ticks ?? 0)
                .Append('|').Append(row.BreakMinutes)
                .Append('|').Append(row.BillRate.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .Append('|').Append(row.ShiftUpdatedAtUtc?.Ticks ?? 0)
                .Append('\n');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }
}
