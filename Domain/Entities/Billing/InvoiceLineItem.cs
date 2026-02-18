using CarePath.Domain.Entities.Common;
using CarePath.Domain.Entities.Scheduling;

namespace CarePath.Domain.Entities.Billing;

/// <summary>
/// A single line item on an <see cref="Invoice"/>, representing the billable portion of one shift
/// or a manual adjustment (e.g., mileage reimbursement, holiday premium).
/// </summary>
/// <remarks>
/// <para>
/// <b>Shift linkage:</b> <see cref="ShiftId"/> is optional to allow manual (non-shift) line items.
/// When set, <see cref="BillableHours"/> and <see cref="RatePerHour"/> should correspond to
/// <c>Shift.BillableHours</c> and <c>Shift.BillRate</c> at the time the line item is created.
/// </para>
/// <para>
/// <b>Internal margin fields:</b> <see cref="CostPerHour"/>, <see cref="TotalCost"/>,
/// <see cref="GrossProfit"/>, and <see cref="GrossMarginPercentage"/> are internal tracking fields
/// and must not appear on the client-facing invoice PDF.
/// </para>
/// </remarks>
public class InvoiceLineItem : BaseEntity
{
    // Foreign Keys and Navigation

    /// <summary>Foreign key to the parent <see cref="Invoice"/>.</summary>
    public Guid InvoiceId { get; set; }

    /// <summary>Navigation to the parent <see cref="Invoice"/>. Required.</summary>
    public Invoice Invoice { get; set; } = null!;

    /// <summary>
    /// Optional foreign key to the <see cref="Shift"/> this line item represents.
    /// <c>null</c> for manual line items (adjustments, fees, premiums).
    /// </summary>
    public Guid? ShiftId { get; set; }

    /// <summary>Navigation to the associated <see cref="Shift"/>. <c>null</c> if not shift-linked.</summary>
    public Shift? Shift { get; set; }

    // Billable Details

    /// <summary>
    /// Description shown on the client invoice (e.g., "Personal Care — 8 hrs @ $35.00/hr",
    /// "Holiday Premium — 2 hrs"). Required.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>UTC date the service was delivered (matches <c>Shift.ScheduledStartTime</c> for shift line items).</summary>
    public DateTime ServiceDate { get; set; }

    /// <summary>Billable hours for this line item (actual shift duration minus unpaid breaks).</summary>
    public decimal BillableHours { get; set; }

    /// <summary>Hourly rate charged to the client for this line item (USD).</summary>
    public decimal RatePerHour { get; set; }

    // Internal Margin Tracking (not shown on client invoice)

    /// <summary>
    /// Internal cost per hour for this line item (USD) — typically the caregiver's pay rate.
    /// Used to compute internal gross profit. Not displayed on the client-facing invoice.
    /// </summary>
    public decimal? CostPerHour { get; set; }

    // Computed Properties

    /// <summary>Billable total for this line item: <see cref="BillableHours"/> × <see cref="RatePerHour"/>.</summary>
    public decimal Total => BillableHours * RatePerHour;

    /// <summary>
    /// Internal labour cost: <see cref="BillableHours"/> × <see cref="CostPerHour"/>.
    /// <c>null</c> when <see cref="CostPerHour"/> is not set.
    /// </summary>
    public decimal? TotalCost => CostPerHour.HasValue ? BillableHours * CostPerHour.Value : null;

    /// <summary>
    /// Gross profit for this line item: <see cref="Total"/> − <see cref="TotalCost"/>.
    /// <c>null</c> when <see cref="TotalCost"/> is not available.
    /// </summary>
    public decimal? GrossProfit => TotalCost.HasValue ? Total - TotalCost.Value : null;

    /// <summary>
    /// Gross margin as a percentage of the billable total: (<see cref="GrossProfit"/> / <see cref="Total"/>) × 100.
    /// <c>null</c> when <see cref="GrossProfit"/> is unavailable or <see cref="Total"/> is zero.
    /// </summary>
    public decimal? GrossMarginPercentage =>
        Total > 0 && GrossProfit.HasValue
            ? (GrossProfit.Value / Total) * 100
            : null;
}
