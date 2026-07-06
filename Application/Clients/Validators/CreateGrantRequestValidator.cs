using CarePath.Contracts.Clients;
using FluentValidation;

namespace CarePath.Application.Clients.Validators;

public sealed class CreateGrantRequestValidator : AbstractValidator<CreateGrantRequest>
{
    public CreateGrantRequestValidator()
    {
        RuleFor(request => request.GranteeUserId).NotEmpty();
        RuleFor(request => request.Scope).IsInEnum();
    }
}
