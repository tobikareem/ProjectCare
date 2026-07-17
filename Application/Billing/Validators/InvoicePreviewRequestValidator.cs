using CarePath.Contracts.Billing;
using FluentValidation;

namespace CarePath.Application.Billing.Validators;

/// <summary>Validates <see cref="InvoicePreviewRequest"/> (D-S6-18). Messages never echo values.</summary>
public sealed class InvoicePreviewRequestValidator : AbstractValidator<InvoicePreviewRequest>
{
    /// <summary>Creates the validator.</summary>
    public InvoicePreviewRequestValidator()
    {
        RuleFor(request => request.ClientId).NotEmpty();
        RuleFor(request => request.ServiceType).IsInEnum();
        RuleFor(request => request.PeriodStartUtc).Must(BeUtc).WithMessage("Period start must be UTC.");
        RuleFor(request => request.PeriodEndUtc).Must(BeUtc).WithMessage("Period end must be UTC.");
        RuleFor(request => request.PeriodEndUtc)
            .GreaterThan(request => request.PeriodStartUtc)
            .WithMessage("Period end must be after period start.");
    }

    private static bool BeUtc(DateTime value) => value.Kind == DateTimeKind.Utc;
}
