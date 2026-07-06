using CarePath.Contracts.Transitions;
using FluentValidation;

namespace CarePath.Application.Transitions.Validators;

public sealed class ActivatePlanRequestValidator : AbstractValidator<ActivatePlanRequest>
{
    public const string ESignatureRequiredCode = "transition.esignature_required";
    public const string RiskLevelInvalidCode = "transition.risk_level_invalid";

    public ActivatePlanRequestValidator()
    {
        RuleFor(request => request.RiskLevel)
            .IsInEnum()
            .WithErrorCode(RiskLevelInvalidCode);
        RuleFor(request => request.ConfirmESignature)
            .Equal(true)
            .WithErrorCode(ESignatureRequiredCode);
    }
}
