using CarePath.Contracts.Scheduling;
using FluentValidation;

namespace CarePath.Application.Scheduling.Validators;

public sealed class CheckInRequestValidator : AbstractValidator<CheckInRequest>
{
    public CheckInRequestValidator()
    {
        RuleFor(request => request.ShiftId).NotEmpty();
        RuleFor(request => request.TimestampUtc).Must(value => value.Kind == DateTimeKind.Utc);
        RuleFor(request => request.Latitude).InclusiveBetween(-90d, 90d);
        RuleFor(request => request.Longitude).InclusiveBetween(-180d, 180d);
    }
}
