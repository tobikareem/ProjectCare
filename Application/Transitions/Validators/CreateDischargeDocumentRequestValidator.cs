using CarePath.Contracts.Enumerations;
using CarePath.Contracts.Transitions;
using FluentValidation;

namespace CarePath.Application.Transitions.Validators;

public sealed class CreateDischargeDocumentRequestValidator : AbstractValidator<CreateDischargeDocumentRequest>
{
    public const string RawContentRequiredCode = "transition.raw_content_required";
    public const string SourceTypeDeferredCode = "transition.intake_source_deferred";
    public const string DischargeDateUtcCode = "transition.discharge_date_utc_required";

    public CreateDischargeDocumentRequestValidator()
    {
        RuleFor(request => request.ClientId).NotEmpty();
        RuleFor(request => request.RawContent)
            .Must(content => !string.IsNullOrWhiteSpace(content))
            .WithErrorCode(RawContentRequiredCode);
        RuleFor(request => request.SourceType)
            .Equal(DischargeDocumentSourceType.PdfUpload)
            .WithErrorCode(SourceTypeDeferredCode);
        RuleFor(request => request.SourceReference).MaximumLength(200);
        RuleFor(request => request.HospitalName).MaximumLength(100);
        RuleFor(request => request.DischargeDate)
            .Must(dischargeDate => dischargeDate.Kind == DateTimeKind.Utc)
            .WithErrorCode(DischargeDateUtcCode);
    }
}
