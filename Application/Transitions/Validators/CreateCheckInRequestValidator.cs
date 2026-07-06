using CarePath.Contracts.Transitions;
using FluentValidation;

namespace CarePath.Application.Transitions.Validators;

public sealed class CreateCheckInRequestValidator : AbstractValidator<CreateCheckInRequest>
{
    public const string ResponsesRequiredCode = "transition.responses_required";
    public const string ChannelInvalidCode = "transition.channel_invalid";

    public CreateCheckInRequestValidator()
    {
        RuleFor(request => request.Channel)
            .IsInEnum()
            .WithErrorCode(ChannelInvalidCode);
        RuleFor(request => request.ResponsesJson)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithErrorCode(ResponsesRequiredCode);
    }
}
