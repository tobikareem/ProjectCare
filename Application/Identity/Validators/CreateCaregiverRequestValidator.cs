using CarePath.Contracts.Identity;
using FluentValidation;

namespace CarePath.Application.Identity.Validators;

public sealed class CreateCaregiverRequestValidator : AbstractValidator<CreateCaregiverRequest>
{
    public CreateCaregiverRequestValidator()
    {
        RuleFor(request => request.FirstName).NotEmpty().MaximumLength(100).WithMessage("First name is required and must be 100 characters or fewer.");
        RuleFor(request => request.LastName).NotEmpty().MaximumLength(100).WithMessage("Last name is required and must be 100 characters or fewer.");
        RuleFor(request => request.Email).NotEmpty().EmailAddress().MaximumLength(256).WithMessage("A valid email address is required.");
        RuleFor(request => request.PhoneNumber).NotEmpty().MaximumLength(20).WithMessage("A valid phone number is required.");
        RuleFor(request => request.TemporaryPassword).NotEmpty().MinimumLength(8).WithMessage("A temporary password is required and must satisfy password policy.");
        RuleFor(request => request.EmploymentType).IsInEnum();
        RuleFor(request => request.HourlyPayRate).GreaterThanOrEqualTo(0m);
        RuleFor(request => request.HireDate).Must(BeUtc).WithMessage("Hire date must be UTC.");
        RuleFor(request => request.MaxWeeklyHours).InclusiveBetween(1, 80);
    }

    private static bool BeUtc(DateTime value) => value.Kind == DateTimeKind.Utc;
}
