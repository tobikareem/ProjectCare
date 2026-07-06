using CarePath.Contracts.Transitions;
using FluentValidation;

namespace CarePath.Application.Transitions.Validators;

public sealed class AcknowledgeEscalationRequestValidator : AbstractValidator<AcknowledgeEscalationRequest>
{
    public const string ResolutionRequiredCode = "transition.resolution_required";
    public const string EscalationLevelInvalidCode = "transition.escalation_level_invalid";

    public AcknowledgeEscalationRequestValidator()
    {
        RuleFor(request => request.EscalationLevel)
            .IsInEnum()
            .WithErrorCode(EscalationLevelInvalidCode);
        RuleFor(request => request.ResolutionNote)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithErrorCode(ResolutionRequiredCode)
            .MaximumLength(2000);
    }
}
