using System.Text.RegularExpressions;
using CarePath.Contracts.Platform;
using FluentValidation;

namespace CarePath.Application.Platform.Validators;

/// <summary>Validates <see cref="RegisterOrganizationRequest"/>.</summary>
public sealed class RegisterOrganizationRequestValidator : AbstractValidator<RegisterOrganizationRequest>
{
    public RegisterOrganizationRequestValidator()
    {
        RuleFor(request => request.LegalName).NotEmpty().MaximumLength(200);
        RuleFor(request => request.DisplayName).NotEmpty().MaximumLength(100);
        RuleFor(request => request.Slug)
            .NotEmpty()
            .Must(OrganizationSlug.IsValid)
            .WithMessage("Slug must be 3-40 lower-case letters, digits or single hyphens and not a reserved word.");
        RuleFor(request => request.DefaultTimeZone).NotEmpty().MaximumLength(64).Must(PlatformValidationRules.IsKnownTimeZone).WithMessage("Unknown time zone.");
        RuleFor(request => request.DataRegion).NotEmpty().MaximumLength(32);
    }
}

/// <summary>Validates <see cref="UpdateOrganizationRequest"/>.</summary>
public sealed class UpdateOrganizationRequestValidator : AbstractValidator<UpdateOrganizationRequest>
{
    public UpdateOrganizationRequestValidator()
    {
        RuleFor(request => request.LegalName).NotEmpty().MaximumLength(200);
        RuleFor(request => request.DisplayName).NotEmpty().MaximumLength(100);
        RuleFor(request => request.DefaultTimeZone).NotEmpty().MaximumLength(64).Must(PlatformValidationRules.IsKnownTimeZone).WithMessage("Unknown time zone.");
        RuleFor(request => request.DataRegion).NotEmpty().MaximumLength(32);
    }
}

/// <summary>Validates <see cref="SuspendOrganizationRequest"/>.</summary>
public sealed class SuspendOrganizationRequestValidator : AbstractValidator<SuspendOrganizationRequest>
{
    public SuspendOrganizationRequestValidator()
    {
        RuleFor(request => request.Reason).NotEmpty().MaximumLength(200);
    }
}

/// <summary>Validates <see cref="AddOrganizationDomainRequest"/>.</summary>
public sealed class AddOrganizationDomainRequestValidator : AbstractValidator<AddOrganizationDomainRequest>
{
    public AddOrganizationDomainRequestValidator()
    {
        RuleFor(request => request.HostName)
            .NotEmpty()
            .Must(host => HostNameNormalizer.Normalize(host) is not null)
            .WithMessage("Host name is not valid.");
    }
}

/// <summary>Validates <see cref="UpdateOrganizationBrandingRequest"/>.</summary>
public sealed class UpdateOrganizationBrandingRequestValidator : AbstractValidator<UpdateOrganizationBrandingRequest>
{
    public UpdateOrganizationBrandingRequestValidator()
    {
        RuleFor(request => request.LogoStorageKey)
            .MaximumLength(256)
            .Must(PlatformValidationRules.IsSafeStorageKey)
            .WithMessage("Logo storage key must be a relative path of letters, digits, '/', '_', '.' or '-'.")
            .When(request => !string.IsNullOrEmpty(request.LogoStorageKey));
        RuleFor(request => request.PrimaryColorToken).IsInEnum();
        RuleFor(request => request.AccentColorToken).IsInEnum();
        RuleFor(request => request.SupportEmail).EmailAddress().MaximumLength(256).When(request => !string.IsNullOrEmpty(request.SupportEmail));
        RuleFor(request => request.SupportPhone)
            .MaximumLength(20)
            .Must(PlatformValidationRules.IsPhoneNumber)
            .WithMessage("Support phone may contain digits, spaces, '+', '-', '(' and ')'.")
            .When(request => !string.IsNullOrEmpty(request.SupportPhone));
    }
}

internal static partial class PlatformValidationRules
{
    // IANA ids only ("Area/Location" or "UTC"): Windows ids resolve on Windows hosts but would
    // break the same organization on a Linux deployment.
    internal static bool IsKnownTimeZone(string? id) =>
        !string.IsNullOrWhiteSpace(id)
        && (id == "UTC" || id.Contains('/'))
        && !id.Contains("..", StringComparison.Ordinal)
        && TimeZoneInfo.TryFindSystemTimeZoneById(id, out _);

    // A relative blob key under the organization's own prefix: no scheme, no leading slash, no
    // traversal, so a stored key can never point outside the private container (CP-06 serves it).
    internal static bool IsSafeStorageKey(string? key) =>
        !string.IsNullOrEmpty(key)
        && StorageKeyPattern().IsMatch(key)
        && !key.Contains("..", StringComparison.Ordinal)
        && !key.Contains("//", StringComparison.Ordinal);

    internal static bool IsPhoneNumber(string? phone) =>
        !string.IsNullOrEmpty(phone) && PhonePattern().IsMatch(phone);

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9/_.\-]*\z")]
    private static partial Regex StorageKeyPattern();

    [GeneratedRegex(@"^\+?[0-9][0-9 ().\-]{5,18}\z")]
    private static partial Regex PhonePattern();
}
