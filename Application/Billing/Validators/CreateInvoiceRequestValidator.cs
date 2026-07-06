using FluentValidation;
using CarePath.Contracts.Billing;

namespace CarePath.Application.Billing.Validators;

public sealed class CreateInvoiceRequestValidator : AbstractValidator<CreateInvoiceRequest>
{
    public CreateInvoiceRequestValidator()
    {
        RuleFor(request => request.ClientId).NotEmpty();
        RuleFor(request => request.ServiceType).IsInEnum();
        RuleFor(request => request.PeriodStartUtc).Must(BeUtc).WithMessage("Period start must be UTC.");
        RuleFor(request => request.PeriodEndUtc).Must(BeUtc).WithMessage("Period end must be UTC.");
        RuleFor(request => request.PeriodEndUtc)
            .GreaterThan(request => request.PeriodStartUtc)
            .WithMessage("Period end must be after period start.");
        RuleFor(request => request.DueDate).Must(BeUtc).WithMessage("Due date must be UTC.");
        RuleFor(request => request.TaxAmount).GreaterThanOrEqualTo(0m);
        RuleFor(request => request.Notes).MaximumLength(1000);
    }

    private static bool BeUtc(DateTime value) => value.Kind == DateTimeKind.Utc;
}
