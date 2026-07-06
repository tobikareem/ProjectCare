using CarePath.Contracts.Scheduling;
using FluentValidation;

namespace CarePath.Application.Scheduling.Validators;

public sealed class CreateShiftRequestValidator : AbstractValidator<CreateShiftRequest>
{
    public CreateShiftRequestValidator()
    {
        RuleFor(request => request.ClientId).NotEmpty();
        RuleFor(request => request.CaregiverId).NotEmpty();
        RuleFor(request => request.ScheduledStartUtc).Must(BeUtc);
        RuleFor(request => request.ScheduledEndUtc).Must(BeUtc);
        RuleFor(request => request.ScheduledEndUtc).GreaterThan(request => request.ScheduledStartUtc);
        RuleFor(request => request.BreakMinutes).GreaterThanOrEqualTo(0).When(request => request.BreakMinutes.HasValue);
        RuleFor(request => request).Must(HaveBreakShorterThanShift).WithMessage("BreakMinutes must be shorter than the scheduled shift duration.");
        RuleFor(request => request.BillRate).GreaterThan(0m);
        RuleFor(request => request.PayRate).GreaterThanOrEqualTo(0m);
        RuleFor(request => request.ServiceType).IsInEnum();
    }

    private static bool BeUtc(DateTime value) => value.Kind == DateTimeKind.Utc;

    private static bool HaveBreakShorterThanShift(CreateShiftRequest request)
    {
        if (request.BreakMinutes is null || request.ScheduledEndUtc <= request.ScheduledStartUtc)
        {
            return true;
        }

        return request.BreakMinutes.Value < (request.ScheduledEndUtc - request.ScheduledStartUtc).TotalMinutes;
    }
}
