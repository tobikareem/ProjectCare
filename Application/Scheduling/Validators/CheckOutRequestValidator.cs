using CarePath.Contracts.Scheduling;
using FluentValidation;

namespace CarePath.Application.Scheduling.Validators;

public sealed class CheckOutRequestValidator : AbstractValidator<CheckOutRequest>
{
    public CheckOutRequestValidator()
    {
        RuleFor(request => request.ShiftId).NotEmpty();
        RuleFor(request => request.TimestampUtc).Must(value => value.Kind == DateTimeKind.Utc);
        RuleFor(request => request.Latitude).InclusiveBetween(-90d, 90d);
        RuleFor(request => request.Longitude).InclusiveBetween(-180d, 180d);
        RuleFor(request => request.BreakMinutes).GreaterThanOrEqualTo(0).When(request => request.BreakMinutes.HasValue);
    }
}
