using FluentValidation;
using CarePath.Contracts.Billing;

namespace CarePath.Application.Billing.Validators;

public sealed class RecordPaymentRequestValidator : AbstractValidator<RecordPaymentRequest>
{
    public RecordPaymentRequestValidator()
    {
        RuleFor(request => request.Amount).GreaterThan(0m);
        RuleFor(request => request.Method).IsInEnum();
        RuleFor(request => request.PaymentDate).Must(BeUtc).WithMessage("Payment date must be UTC.");
        RuleFor(request => request.ReferenceNumber).MaximumLength(100);
        RuleFor(request => request.Notes).MaximumLength(1000);
    }

    private static bool BeUtc(DateTime value) => value.Kind == DateTimeKind.Utc;
}
