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
}
