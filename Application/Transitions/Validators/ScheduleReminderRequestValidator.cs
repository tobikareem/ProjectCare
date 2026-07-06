using CarePath.Contracts.Transitions;
using FluentValidation;

namespace CarePath.Application.Transitions.Validators;

public sealed class ScheduleReminderRequestValidator : AbstractValidator<ScheduleReminderRequest>
{
    public const string ScheduledAtUtcCode = "transition.scheduled_at_utc_required";

    public ScheduleReminderRequestValidator()
    {
        RuleFor(request => request.ScheduledAt)
            .Must(scheduledAt => scheduledAt.Kind == DateTimeKind.Utc)
            .WithErrorCode(ScheduledAtUtcCode);
    }
}
