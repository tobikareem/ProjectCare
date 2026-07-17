using CarePath.Contracts.Billing;
using FluentValidation;

namespace CarePath.Application.Billing.Validators;

/// <summary>
/// Validates <see cref="BillingReconciliationSearchRequest"/> (D-S6-18): UTC half-open window
/// bounded to 92 days, in-range enum filters. Messages never echo values.
/// </summary>
public sealed class BillingReconciliationSearchRequestValidator : AbstractValidator<BillingReconciliationSearchRequest>
{
    /// <summary>Creates the validator.</summary>
    public BillingReconciliationSearchRequestValidator()
    {
        RuleFor(request => request.PeriodStartUtc).Must(BeUtc).WithMessage("Window start must be UTC.");
        RuleFor(request => request.PeriodEndUtc).Must(BeUtc).WithMessage("Window end must be UTC.");
        RuleFor(request => request.PeriodEndUtc)
            .GreaterThan(request => request.PeriodStartUtc)
            .WithMessage("Window end must be after window start.");
        RuleFor(request => request)
            .Must(request => (request.PeriodEndUtc - request.PeriodStartUtc).TotalDays
                <= BillingReconciliationSearchRequest.MaxRangeDays)
            .WithMessage("The window may span at most 92 days.")
            .WithErrorCode("reconciliation.range_too_large");
        RuleFor(request => request.ServiceType!.Value)
            .IsInEnum()
            .When(request => request.ServiceType.HasValue);
        RuleFor(request => request.Reason!.Value)
            .IsInEnum()
            .When(request => request.Reason.HasValue);
    }

    private static bool BeUtc(DateTime value) => value.Kind == DateTimeKind.Utc;
}
