using CarePath.Contracts.Identity;
using FluentValidation;

namespace CarePath.Application.Identity.Validators;

public sealed class UpdateCaregiverRequestValidator : AbstractValidator<UpdateCaregiverRequest>
{
    public UpdateCaregiverRequestValidator()
    {
        RuleFor(request => request.PhoneNumber).NotEmpty().MaximumLength(20).WithMessage("A valid phone number is required.");
        RuleFor(request => request.HourlyPayRate).GreaterThanOrEqualTo(0m);
        RuleFor(request => request.TerminationDate).Must(BeUtc).When(request => request.TerminationDate.HasValue).WithMessage("Termination date must be UTC.");
        RuleFor(request => request.MaxWeeklyHours).InclusiveBetween(1, 80);
    }

    private static bool BeUtc(DateTime? value) => value is { Kind: DateTimeKind.Utc };
}
