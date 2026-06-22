namespace CarePath.Domain.Enumerations;

/// <summary>
/// The delivery channel used to send a <see cref="Entities.Transitions.TransitionReminder"/>
/// to the patient. Channel is configured per patient based on preference and capability.
/// </summary>
public enum ReminderChannel
{
    /// <summary>
    /// Delivered as a push notification within the CarePath mobile app.
    /// Requires the patient to have the app installed.
    /// </summary>
    App = 1,

    /// <summary>
    /// Delivered as an SMS text message via Twilio.
    /// Preferred for patients without a smartphone or who prefer text.
    /// </summary>
    Sms = 2,

    /// <summary>
    /// Delivered as an automated voice call via Twilio Programmable Voice.
    /// Used for patients who cannot receive or read SMS (e.g. vision impaired, elderly).
    /// </summary>
    Voice = 3
}
