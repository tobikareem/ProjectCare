using CarePath.Contracts.Billing;
using FluentValidation;

namespace CarePath.Application.Billing.Validators;

/// <summary>
/// Validates <see cref="CorrectShiftTimeRequest"/> (D-S6-18): UTC window, positive duration
/// after breaks, in-range reason. Messages never echo submitted values.
/// </summary>
public sealed class CorrectShiftTimeRequestValidator : AbstractValidator<CorrectShiftTimeRequest>
{
    /// <summary>Maximum plausible corrected shift length in hours (money-path sanity bound).</summary>
    public const int MaxWindowHours = 24;

    /// <summary>Creates the validator.</summary>
    public CorrectShiftTimeRequestValidator()
    {
        RuleFor(request => request.ActualStartUtc).Must(BeUtc).WithMessage("Actual start must be UTC.");
        RuleFor(request => request.ActualEndUtc).Must(BeUtc).WithMessage("Actual end must be UTC.");
        RuleFor(request => request.ActualEndUtc)
            .GreaterThan(request => request.ActualStartUtc)
            .WithMessage("Actual end must be after actual start.");
        RuleFor(request => request.BreakMinutes).GreaterThanOrEqualTo(0);
        RuleFor(request => request)
            .Must(request => (decimal)(request.ActualEndUtc - request.ActualStartUtc).TotalMinutes
                - request.BreakMinutes > 0)
            .WithMessage("The corrected window must leave positive billable time after breaks.")
            .WithErrorCode("reconciliation.invalid_billable_time");
        RuleFor(request => request)
            .Must(request => (request.ActualEndUtc - request.ActualStartUtc).TotalHours <= MaxWindowHours)
            .WithMessage("The corrected window exceeds the maximum plausible shift length.")
            .WithErrorCode("reconciliation.window_implausible");
        RuleFor(request => request.Reason).IsInEnum();
    }

    private static bool BeUtc(DateTime value) => value.Kind == DateTimeKind.Utc;
}
