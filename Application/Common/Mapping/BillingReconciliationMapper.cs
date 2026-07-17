using CarePath.Application.Abstractions.Billing;
using CarePath.Contracts.Billing;
using CarePath.Domain.Entities.Billing;
using ContractBillingCorrectiveDestination = CarePath.Contracts.Enumerations.BillingCorrectiveDestination;
using ContractBillingExclusionReason = CarePath.Contracts.Enumerations.BillingExclusionReason;
using ContractBillingReconciliationReason = CarePath.Contracts.Enumerations.BillingReconciliationReason;
using ContractServiceType = CarePath.Contracts.Enumerations.ServiceType;
using DomainBillingExclusionReason = CarePath.Domain.Enumerations.BillingExclusionReason;

namespace CarePath.Application.Common.Mapping;

/// <summary>
/// Explicit projections from internal billing eligibility rows to the D-S6-18 contract shapes.
/// The minimum-necessary boundary is enforced here: preview rows drop every identifier; the
/// internal <c>PayRate</c> is never mapped anywhere.
/// </summary>
internal static class BillingReconciliationMapper
{
    internal static InvoicePreviewRowDto ToPreviewRowDto(this BillingEligibilityRow row) => new()
    {
        ServiceDateUtc = row.ScheduledStartUtc.Date,
        ServiceStartUtc = row.ActualStartUtc ?? row.ScheduledStartUtc,
        ServiceEndUtc = row.ActualEndUtc ?? row.ScheduledEndUtc,
        BillableHours = BillingMath.BillableHours(row) ?? 0m,
        BillRate = row.BillRate,
        LineTotal = BillingMath.LineTotal(row) ?? 0m,
        CaregiverDisplayName = row.CaregiverDisplayName,
        QualificationLabel = row.QualificationLabel,
    };

    internal static BillingReconciliationRowDto ToReconciliationRowDto(
        this BillingEligibilityRow row,
        DateTime utcNow) => new()
    {
        ShiftId = row.ShiftId,
        ServiceDateUtc = row.ScheduledStartUtc.Date,
        ScheduledStartUtc = row.ScheduledStartUtc,
        ScheduledEndUtc = row.ScheduledEndUtc,
        ClientDisplayName = row.ClientDisplayName,
        CaregiverDisplayName = row.CaregiverDisplayName,
        ServiceType = (ContractServiceType)(int)row.ServiceType,
        Reason = (ContractBillingExclusionReason)(int)row.Reason,
        AgeDays = BillingMath.AgeDays(row, utcNow),
        IsRevenueAtRisk = BillingMath.IsRevenueAtRisk(row),
        EstimatedValue = BillingMath.EstimatedValue(row),
        OwningInvoiceId = row.OwningInvoiceId,
        CorrectiveDestination = DestinationFor(row.Reason),
    };

    internal static BillingResolutionRecordDto ToRecordDto(
        this BillingReconciliationResolution resolution,
        string resolvedByDisplayName,
        bool isSuperseded) => new()
    {
        Id = resolution.Id,
        Reason = (ContractBillingReconciliationReason)(int)resolution.Reason,
        ResolvedByDisplayName = resolvedByDisplayName,
        ResolvedAtUtc = resolution.ResolvedAtUtc,
        Note = resolution.Note,
        IsSuperseded = isSuperseded,
    };

    internal static ContractBillingCorrectiveDestination DestinationFor(DomainBillingExclusionReason reason) => reason switch
    {
        DomainBillingExclusionReason.AlreadyInvoiced => ContractBillingCorrectiveDestination.InvoiceDetail,
        DomainBillingExclusionReason.NonBillableResolved => ContractBillingCorrectiveDestination.None,
        DomainBillingExclusionReason.CancelledOrNoShow => ContractBillingCorrectiveDestination.NonBillableResolution,
        DomainBillingExclusionReason.NotCompleted => ContractBillingCorrectiveDestination.ShiftLifecycle,
        DomainBillingExclusionReason.MissingActualTime => ContractBillingCorrectiveDestination.ShiftTimeCorrection,
        DomainBillingExclusionReason.InvalidBillableTime => ContractBillingCorrectiveDestination.ShiftTimeCorrection,
        DomainBillingExclusionReason.MissingBillRate => ContractBillingCorrectiveDestination.ShiftRateUpdate,
        _ => ContractBillingCorrectiveDestination.None,
    };
}
