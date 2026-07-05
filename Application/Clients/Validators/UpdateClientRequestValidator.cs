using CarePath.Contracts.Clients;
using FluentValidation;

namespace CarePath.Application.Clients.Validators;

public sealed class UpdateClientRequestValidator : AbstractValidator<UpdateClientRequest>
{
    public UpdateClientRequestValidator()
    {
        RuleFor(request => request.PhoneNumber).NotEmpty().MaximumLength(20).WithMessage("A valid phone number is required.");
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
}
