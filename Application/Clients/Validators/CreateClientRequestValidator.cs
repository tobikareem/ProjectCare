using CarePath.Contracts.Clients;
using FluentValidation;

namespace CarePath.Application.Clients.Validators;

public sealed class CreateClientRequestValidator : AbstractValidator<CreateClientRequest>
{
    public CreateClientRequestValidator()
    {
        RuleFor(request => request.FirstName).NotEmpty().MaximumLength(100).WithMessage("First name is required and must be 100 characters or fewer.");
        RuleFor(request => request.LastName).NotEmpty().MaximumLength(100).WithMessage("Last name is required and must be 100 characters or fewer.");
        RuleFor(request => request.Email).NotEmpty().EmailAddress().MaximumLength(256).WithMessage("A valid email address is required.");
        RuleFor(request => request.PhoneNumber).NotEmpty().MaximumLength(20).WithMessage("A valid phone number is required.");
        RuleFor(request => request.TemporaryPassword).NotEmpty().MinimumLength(8).WithMessage("A temporary password is required and must satisfy password policy.");
        RuleFor(request => request.DateOfBirth).Must(BeUtc).WithMessage("Date of birth must be UTC.");
        RuleFor(request => request.DateOfBirth).LessThan(DateTime.UtcNow).WithMessage("Date of birth must be in the past.");
        RuleFor(request => request.EmergencyContactName).MaximumLength(100);
        RuleFor(request => request.EmergencyContactPhone).MaximumLength(20);
        RuleFor(request => request.EmergencyContactRelationship).MaximumLength(100);
        RuleFor(request => request.SpecialInstructions).MaximumLength(1000);
        RuleFor(request => request.MedicalConditions).MaximumLength(1000);
        RuleFor(request => request.Allergies).MaximumLength(500);
        RuleFor(request => request.ServiceType).IsInEnum();
        RuleFor(request => request.HourlyBillRate).GreaterThanOrEqualTo(0m);
        RuleFor(request => request.EstimatedWeeklyHours).InclusiveBetween(0, 168);
    }

    private static bool BeUtc(DateTime value) => value.Kind == DateTimeKind.Utc;
}
