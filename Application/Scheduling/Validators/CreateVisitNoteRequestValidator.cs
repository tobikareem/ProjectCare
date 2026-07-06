using CarePath.Contracts.Scheduling;
using FluentValidation;

namespace CarePath.Application.Scheduling.Validators;

public sealed class CreateVisitNoteRequestValidator : AbstractValidator<CreateVisitNoteRequest>
{
    public CreateVisitNoteRequestValidator()
    {
        RuleFor(request => request.VisitDateTime).Must(value => value.Kind == DateTimeKind.Utc);
        RuleFor(request => request.Activities).MaximumLength(4000);
        RuleFor(request => request.ClientCondition).MaximumLength(4000);
        RuleFor(request => request.Concerns).MaximumLength(4000);
        RuleFor(request => request.Medications).MaximumLength(4000);
        RuleFor(request => request.BloodPressureSystolic).InclusiveBetween(40, 260).When(request => request.BloodPressureSystolic.HasValue);
        RuleFor(request => request.BloodPressureDiastolic).InclusiveBetween(30, 180).When(request => request.BloodPressureDiastolic.HasValue);
        RuleFor(request => request.Temperature).InclusiveBetween(90m, 110m).When(request => request.Temperature.HasValue);
        RuleFor(request => request.HeartRate).InclusiveBetween(30, 220).When(request => request.HeartRate.HasValue);
    }
}
