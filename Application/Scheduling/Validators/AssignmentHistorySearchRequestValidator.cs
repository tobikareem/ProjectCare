using CarePath.Contracts.Scheduling;
using FluentValidation;

namespace CarePath.Application.Scheduling.Validators;

public sealed class AssignmentHistorySearchRequestValidator : AbstractValidator<AssignmentHistorySearchRequest>
{
    public AssignmentHistorySearchRequestValidator()
    {
        RuleFor(request => request.SearchTerm).MaximumLength(100);
        RuleFor(request => request)
            .Must(request => !request.FromUtc.HasValue || !request.ToUtc.HasValue || request.FromUtc < request.ToUtc)
            .WithName(nameof(AssignmentHistorySearchRequest.ToUtc))
            .WithErrorCode("assignment.invalid_range")
            .WithMessage("The request is invalid.");
    }
}
