using CarePath.Contracts.Admin;
using CarePath.Contracts.Enumerations;
using FluentValidation;

namespace CarePath.Application.Admin.Validators;

public sealed class CreateStaffUserRequestValidator : AbstractValidator<CreateStaffUserRequest>
{
    private static readonly HashSet<UserRole> StaffRoles =
    [
        UserRole.Admin,
        UserRole.Coordinator,
        UserRole.Clinician,
        UserRole.FacilityManager,
    ];

    public CreateStaffUserRequestValidator()
    {
        RuleFor(request => request.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(request => request.LastName).NotEmpty().MaximumLength(100);
        RuleFor(request => request.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(request => request.PhoneNumber).NotEmpty().MaximumLength(20);
        RuleFor(request => request.TemporaryPassword).NotEmpty().MinimumLength(8);
        RuleFor(request => request.Role)
            .IsInEnum()
            .Must(role => StaffRoles.Contains(role))
            .WithMessage("Role must be Admin, Coordinator, Clinician, or FacilityManager.");
    }
}
