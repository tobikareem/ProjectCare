using CarePath.Contracts.Transitions;
using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Entities.Transitions;
using ContractDischargeDocumentSourceType = CarePath.Contracts.Enumerations.DischargeDocumentSourceType;
using ContractDischargeDocumentStatus = CarePath.Contracts.Enumerations.DischargeDocumentStatus;
using ContractTransitionInstructionCategory = CarePath.Contracts.Enumerations.TransitionInstructionCategory;
using ContractTransitionInstructionStatus = CarePath.Contracts.Enumerations.TransitionInstructionStatus;
using ContractTransitionPlanStatus = CarePath.Contracts.Enumerations.TransitionPlanStatus;
using ContractTransitionRiskLevel = CarePath.Contracts.Enumerations.TransitionRiskLevel;
using ContractReminderChannel = CarePath.Contracts.Enumerations.ReminderChannel;
using ContractReminderStatus = CarePath.Contracts.Enumerations.ReminderStatus;
using ContractReminderType = CarePath.Contracts.Enumerations.ReminderType;
using ContractEscalationLevel = CarePath.Contracts.Enumerations.EscalationLevel;
using ContractEscalationTriggerType = CarePath.Contracts.Enumerations.EscalationTriggerType;
using DomainTransitionInstructionStatus = CarePath.Domain.Enumerations.TransitionInstructionStatus;

namespace CarePath.Application.Common.Mapping;

internal static class TransitionContractMapper
{
    internal static DischargeDocumentDto ToDto(this DischargeDocument document) => new()
    {
        Id = document.Id,
        ClientId = document.ClientId,
        SourceType = (ContractDischargeDocumentSourceType)(int)document.SourceType,
        SourceReference = document.SourceReference,
        Status = (ContractDischargeDocumentStatus)(int)document.Status,
        UploadedBy = document.UploadedBy,
        UploadedAt = document.UploadedAt,
    };

    internal static DischargeDocumentContentDto ToContentDto(this DischargeDocument document) => new()
    {
        Id = document.Id,
        RawContent = document.RawContent,
    };

    internal static TransitionPlanClinicalDto ToClinicalDto(
        this TransitionPlan plan,
        Client client,
        User user,
        IReadOnlyList<TransitionInstruction> instructions) => new()
    {
        Id = plan.Id,
        ClientId = plan.ClientId,
        ClientFullName = user.FullName,
        DischargeDocumentId = plan.DischargeDocumentId,
        HospitalName = plan.HospitalName,
        DischargeDate = plan.DischargeDate,
        TransitionWindowEnd = plan.TransitionWindowEnd,
        Status = (ContractTransitionPlanStatus)(int)plan.Status,
        RiskLevel = (ContractTransitionRiskLevel)(int)plan.RiskLevel,
        VerifiedBy = plan.VerifiedBy,
        VerifiedAt = plan.VerifiedAt,
        ActivatedAt = plan.ActivatedAt,
        IsActive = plan.IsActive,
        DaysRemaining = plan.DaysRemaining,
        Instructions = instructions.Select(instruction => instruction.ToClinicalDto()).ToArray(),
    };

    internal static TransitionPlanSummaryDto ToSummaryDto(
        this TransitionPlan plan,
        Client client,
        User user,
        int pendingInstructionCount,
        int openEscalationCount) => new()
    {
        Id = plan.Id,
        ClientId = plan.ClientId,
        ClientFullName = user.FullName,
        HospitalName = plan.HospitalName,
        DischargeDate = plan.DischargeDate,
        TransitionWindowEnd = plan.TransitionWindowEnd,
        Status = (ContractTransitionPlanStatus)(int)plan.Status,
        RiskLevel = (ContractTransitionRiskLevel)(int)plan.RiskLevel,
        DaysRemaining = plan.DaysRemaining,
        PendingInstructionCount = pendingInstructionCount,
        OpenEscalationCount = openEscalationCount,
    };

    internal static TransitionPlanPatientFacingDto ToPatientFacingDto(
        this TransitionPlan plan,
        IReadOnlyList<TransitionInstruction> instructions) => new()
    {
        Id = plan.Id,
        HospitalName = plan.HospitalName,
        DischargeDate = plan.DischargeDate,
        TransitionWindowEnd = plan.TransitionWindowEnd,
        IsActive = plan.IsActive,
        DaysRemaining = plan.DaysRemaining,
        Instructions = instructions
            .Where(IsPatientVisible)
            .Select(instruction => instruction.ToPatientFacingDto())
            .ToArray(),
    };

    internal static TransitionPlanCareTeamDto ToCareTeamDto(
        this TransitionPlan plan,
        IReadOnlyList<TransitionInstruction> instructions) => new()
    {
        Id = plan.Id,
        ClientId = plan.ClientId,
        HospitalName = plan.HospitalName,
        DischargeDate = plan.DischargeDate,
        TransitionWindowEnd = plan.TransitionWindowEnd,
        Status = (ContractTransitionPlanStatus)(int)plan.Status,
        RiskLevel = (ContractTransitionRiskLevel)(int)plan.RiskLevel,
        IsActive = plan.IsActive,
        DaysRemaining = plan.DaysRemaining,
        Instructions = instructions
            .Where(IsPatientVisible)
            .Select(instruction => instruction.ToPatientFacingDto())
            .ToArray(),
    };

    internal static TransitionInstructionClinicalDto ToClinicalDto(this TransitionInstruction instruction) => new()
    {
        Id = instruction.Id,
        TransitionPlanId = instruction.TransitionPlanId,
        Category = (ContractTransitionInstructionCategory)(int)instruction.Category,
        InstructionText = instruction.InstructionText,
        SourceText = instruction.SourceText,
        ConfidenceScore = instruction.ConfidenceScore,
        IsLowConfidence = instruction.IsLowConfidence,
        ClinicalNote = instruction.ClinicalNote,
        NeedsPharmacistReview = instruction.NeedsPharmacistReview,
        Status = (ContractTransitionInstructionStatus)(int)instruction.Status,
    };

    internal static TransitionInstructionPatientFacingDto ToPatientFacingDto(this TransitionInstruction instruction) => new()
    {
        Id = instruction.Id,
        Category = (ContractTransitionInstructionCategory)(int)instruction.Category,
        InstructionText = instruction.InstructionText,
    };

    internal static TransitionReminderDto ToDto(this TransitionReminder reminder) => new()
    {
        Id = reminder.Id,
        TransitionPlanId = reminder.TransitionPlanId,
        TransitionInstructionId = reminder.TransitionInstructionId,
        ReminderType = (ContractReminderType)(int)reminder.ReminderType,
        Channel = (ContractReminderChannel)(int)reminder.Channel,
        ScheduledAt = reminder.ScheduledAt,
        SentAt = reminder.SentAt,
        AcknowledgedAt = reminder.AcknowledgedAt,
        Status = (ContractReminderStatus)(int)reminder.Status,
        IsOverdue = reminder.IsOverdue,
    };

    internal static TransitionCheckInDto ToDto(this TransitionCheckIn checkIn) => new()
    {
        Id = checkIn.Id,
        TransitionPlanId = checkIn.TransitionPlanId,
        CheckInDate = checkIn.CheckInDate,
        Channel = (ContractReminderChannel)(int)checkIn.Channel,
        ContainsWarningSymptom = checkIn.ContainsWarningSymptom,
        ReviewedBy = checkIn.ReviewedBy,
        ReviewedAt = checkIn.ReviewedAt,
    };

    internal static TransitionEscalationDto ToDto(this TransitionEscalation escalation) => new()
    {
        Id = escalation.Id,
        TransitionPlanId = escalation.TransitionPlanId,
        TriggerType = (ContractEscalationTriggerType)(int)escalation.TriggerType,
        TriggerDetails = escalation.TriggerDetails,
        EscalationLevel = (ContractEscalationLevel)(int)escalation.EscalationLevel,
        EscalatedAt = escalation.EscalatedAt,
        AcknowledgedBy = escalation.AcknowledgedBy,
        AcknowledgedAt = escalation.AcknowledgedAt,
        ResolutionNote = escalation.ResolutionNote,
    };

    private static bool IsPatientVisible(TransitionInstruction instruction) =>
        instruction.Status is DomainTransitionInstructionStatus.Approved or DomainTransitionInstructionStatus.Modified;
}
