using CarePath.Contracts.Clients;
using FluentValidation;

namespace CarePath.Application.Clients.Validators;

public sealed class CreateCarePlanRequestValidator : AbstractValidator<CreateCarePlanRequest>
{
    public CreateCarePlanRequestValidator()
    {
        RuleFor(request => request.Title).NotEmpty().MaximumLength(200).WithMessage("Care plan title is required and must be 200 characters or fewer.");
        RuleFor(request => request.Description).MaximumLength(2000);
        RuleFor(request => request.StartDate).Must(BeUtc).WithMessage("Start date must be UTC.");
        RuleFor(request => request.EndDate).Must(BeUtc).When(request => request.EndDate.HasValue).WithMessage("End date must be UTC.");
        RuleFor(request => request.EndDate).GreaterThan(request => request.StartDate).When(request => request.EndDate.HasValue).WithMessage("End date must be after start date.");
        RuleFor(request => request.Goals).MaximumLength(2000);
        RuleFor(request => request.Interventions).MaximumLength(2000);
        RuleFor(request => request.Notes).MaximumLength(2000);
    }

    private static bool BeUtc(DateTime value) => value.Kind == DateTimeKind.Utc;

    private static bool BeUtc(DateTime? value) => value is { Kind: DateTimeKind.Utc };
}
