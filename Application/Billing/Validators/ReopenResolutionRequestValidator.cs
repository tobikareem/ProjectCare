using CarePath.Contracts.Billing;
using FluentValidation;

namespace CarePath.Application.Billing.Validators;

/// <summary>Validates <see cref="ReopenResolutionRequest"/> (D-S6-18).</summary>
public sealed class ReopenResolutionRequestValidator : AbstractValidator<ReopenResolutionRequest>
{
    /// <summary>Creates the validator.</summary>
    public ReopenResolutionRequestValidator()
    {
        RuleFor(request => request.Note).MaximumLength(ReopenResolutionRequest.NoteMaxLength);
    }
}
