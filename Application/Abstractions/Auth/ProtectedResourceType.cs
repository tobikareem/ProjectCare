namespace CarePath.Application.Abstractions.Auth;

public enum ProtectedResourceType
{
    Client = 1,
    CarePlan = 2,
    Shift = 3,
    VisitNote = 4,
    VisitPhoto = 5,
    CaregiverCertification = 6,
    DischargeDocument = 7,
    TransitionPlan = 8,
    TransitionInstruction = 9,
    TransitionCheckIn = 10,
    TransitionReminder = 11,
    TransitionEscalation = 12,
    Invoice = 13,
    Payment = 14
}