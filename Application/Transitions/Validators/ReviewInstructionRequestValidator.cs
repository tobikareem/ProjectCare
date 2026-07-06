using CarePath.Contracts.Enumerations;
using CarePath.Contracts.Transitions;
using FluentValidation;

namespace CarePath.Application.Transitions.Validators;

public sealed class ReviewInstructionRequestValidator : AbstractValidator<ReviewInstructionRequest>
{
    public const string TerminalStatusRequiredCode = "transition.review_terminal_status_required";
    public const string ModifiedTextRequiredCode = "transition.modified_text_required";

    public ReviewInstructionRequestValidator()
    {
        RuleFor(request => request.Status)
            .Must(status => status is TransitionInstructionStatus.Approved
                or TransitionInstructionStatus.Modified
                or TransitionInstructionStatus.Rejected)
            .WithErrorCode(TerminalStatusRequiredCode);
        RuleFor(request => request.ModifiedInstructionText)
            .Must((request, text) => request.Status != TransitionInstructionStatus.Modified || !string.IsNullOrWhiteSpace(text))
            .WithErrorCode(ModifiedTextRequiredCode);
        RuleFor(request => request.ModifiedInstructionText).MaximumLength(2000);
        RuleFor(request => request.ClinicalNote).MaximumLength(2000);
    }
}
