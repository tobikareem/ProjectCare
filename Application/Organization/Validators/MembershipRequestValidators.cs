using CarePath.Contracts.Organization;
using FluentValidation;

namespace CarePath.Application.Organization.Validators;

/// <summary>
/// Validates <see cref="CreateMembershipRequest"/>. The temporary password is always required so a
/// request can never reveal whether the email already belongs to a platform identity (it is
/// ignored when one exists).
/// </summary>
public sealed class CreateMembershipRequestValidator : AbstractValidator<CreateMembershipRequest>
{
    public CreateMembershipRequestValidator()
    {
        RuleFor(request => request.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(request => request.Role).IsInEnum();
        RuleFor(request => request.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(request => request.LastName).NotEmpty().MaximumLength(100);
        RuleFor(request => request.PhoneNumber).NotEmpty().MaximumLength(20);
        RuleFor(request => request.TemporaryPassword).NotEmpty().MinimumLength(8).MaximumLength(128);
    }
}

/// <summary>Validates <see cref="UpdateMembershipRoleRequest"/>.</summary>
public sealed class UpdateMembershipRoleRequestValidator : AbstractValidator<UpdateMembershipRoleRequest>
{
    public UpdateMembershipRoleRequestValidator()
    {
        RuleFor(request => request.Role).IsInEnum();
    }
}
