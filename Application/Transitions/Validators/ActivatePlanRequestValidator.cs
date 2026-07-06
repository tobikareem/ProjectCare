using CarePath.Contracts.Transitions;
using FluentValidation;

namespace CarePath.Application.Transitions.Validators;

public sealed class ActivatePlanRequestValidator : AbstractValidator<ActivatePlanRequest>
{
    public const string ESignatureRequiredCode = "transition.esignature_required";

    public ActivatePlanRequestValidator()
    {
        RuleFor(request => request.ConfirmESignature)
            .Equal(true)
            .WithErrorCode(ESignatureRequiredCode);
    }
}
