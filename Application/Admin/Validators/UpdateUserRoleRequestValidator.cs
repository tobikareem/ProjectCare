using CarePath.Contracts.Admin;
using FluentValidation;

namespace CarePath.Application.Admin.Validators;

public sealed class UpdateUserRoleRequestValidator : AbstractValidator<UpdateUserRoleRequest>
{
    public UpdateUserRoleRequestValidator()
    {
        RuleFor(request => request.Role).IsInEnum();
    }
}
