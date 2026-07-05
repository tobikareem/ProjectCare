using CarePath.Application.Scheduling.Commands;
using FluentValidation;

namespace CarePath.Application.Scheduling.Validators;

public sealed class CreateShiftCommandValidator : AbstractValidator<CreateShiftCommand>
{
    public CreateShiftCommandValidator()
    {
        RuleFor(command => command.ClientId).NotEmpty();
        RuleFor(command => command.CaregiverId).NotEmpty();
        RuleFor(command => command.ScheduledStartUtc).Must(BeUtc);
        RuleFor(command => command.ScheduledEndUtc).Must(BeUtc);
        RuleFor(command => command.ScheduledEndUtc)
            .GreaterThan(command => command.ScheduledStartUtc);
        RuleFor(command => command.BreakMinutes)
            .GreaterThanOrEqualTo(0)
            .When(command => command.BreakMinutes.HasValue);
        RuleFor(command => command)
            .Must(HaveBreakShorterThanShift)
            .WithMessage("BreakMinutes must be shorter than the scheduled shift duration.");
        RuleFor(command => command.BillRate).GreaterThan(0m);
        RuleFor(command => command.PayRate).GreaterThanOrEqualTo(0m);
        RuleFor(command => command.ServiceType).IsInEnum();
    }

    private static bool BeUtc(DateTime value) => value.Kind == DateTimeKind.Utc;

    private static bool HaveBreakShorterThanShift(CreateShiftCommand command)
    {
        if (command.BreakMinutes is null || command.ScheduledEndUtc <= command.ScheduledStartUtc)
        {
            return true;
        }

        var shiftMinutes = (command.ScheduledEndUtc - command.ScheduledStartUtc).TotalMinutes;
        return command.BreakMinutes.Value < shiftMinutes;
    }
}