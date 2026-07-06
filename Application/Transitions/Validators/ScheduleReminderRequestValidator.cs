using CarePath.Contracts.Transitions;
using FluentValidation;

namespace CarePath.Application.Transitions.Validators;

public sealed class ScheduleReminderRequestValidator : AbstractValidator<ScheduleReminderRequest>
{
    public const string ScheduledAtUtcCode = "transition.scheduled_at_utc_required";
    public const string ReminderTypeInvalidCode = "transition.reminder_type_invalid";
    public const string ChannelInvalidCode = "transition.channel_invalid";

    public ScheduleReminderRequestValidator()
    {
        RuleFor(request => request.ReminderType)
            .IsInEnum()
            .WithErrorCode(ReminderTypeInvalidCode);
        RuleFor(request => request.Channel)
            .IsInEnum()
            .WithErrorCode(ChannelInvalidCode);
        RuleFor(request => request.ScheduledAt)
            .Must(scheduledAt => scheduledAt.Kind == DateTimeKind.Utc)
            .WithErrorCode(ScheduledAtUtcCode);
    }
}
