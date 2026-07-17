using CarePath.Contracts.Billing;
using CarePath.Contracts.Enumerations;
using FluentValidation;

namespace CarePath.Application.Billing.Validators;

/// <summary>
/// Validates <see cref="ResolveNonBillableRequest"/> (D-S6-18). The reserved
/// <see cref="BillingReconciliationReason.Reopened"/> value is rejected — reopening is a
/// dedicated command. Notes are bounded and must stay PHI-free by policy.
/// </summary>
public sealed class ResolveNonBillableRequestValidator : AbstractValidator<ResolveNonBillableRequest>
{
    /// <summary>Creates the validator.</summary>
    public ResolveNonBillableRequestValidator()
    {
        RuleFor(request => request.Reason)
            .IsInEnum()
            .NotEqual(BillingReconciliationReason.Reopened)
            .WithMessage("The reason is not valid for a resolution.")
            .WithErrorCode("reconciliation.invalid_reason");
        RuleFor(request => request.Note).MaximumLength(ResolveNonBillableRequest.NoteMaxLength);
    }
}
