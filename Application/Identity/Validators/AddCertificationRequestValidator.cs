using CarePath.Contracts.Identity;
using CarePath.Domain.Enumerations;
using FluentValidation;

namespace CarePath.Application.Identity.Validators;

public sealed class AddCertificationRequestValidator : AbstractValidator<AddCertificationRequest>
{
    private static readonly HashSet<CertificationType> BoardCredentials =
    [
        CertificationType.CNA,
        CertificationType.LPN,
        CertificationType.RN,
        CertificationType.HHA,
        CertificationType.GNA,
        CertificationType.CRMA
    ];

    public AddCertificationRequestValidator()
    {
        RuleFor(request => request.Type).IsInEnum();
        RuleFor(request => request.IssueDate).Must(BeUtc).WithMessage("Issue date must be UTC.");
        RuleFor(request => request.ExpirationDate).Must(BeUtc).WithMessage("Expiration date must be UTC.");
        RuleFor(request => request.ExpirationDate).GreaterThan(request => request.IssueDate).WithMessage("Expiration date must be after issue date.");
        RuleFor(request => request.CertificationNumber).NotEmpty().MaximumLength(100).When(request => BoardCredentials.Contains((CertificationType)(int)request.Type));
        RuleFor(request => request.IssuingAuthority).NotEmpty().MaximumLength(150).When(request => BoardCredentials.Contains((CertificationType)(int)request.Type));
        RuleFor(request => request.CertificationNumber).MaximumLength(100).When(request => !string.IsNullOrWhiteSpace(request.CertificationNumber));
        RuleFor(request => request.IssuingAuthority).MaximumLength(150).When(request => !string.IsNullOrWhiteSpace(request.IssuingAuthority));
    }

    private static bool BeUtc(DateTime value) => value.Kind == DateTimeKind.Utc;
}
