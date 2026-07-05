using CarePath.Contracts.Scheduling;
using CarePath.Domain.Entities.Scheduling;
using ContractServiceType = CarePath.Contracts.Enumerations.ServiceType;
using ContractShiftStatus = CarePath.Contracts.Enumerations.ShiftStatus;

namespace CarePath.Application.Common.Mapping;

internal static class SchedulingContractMapper
{
    internal static ShiftSummaryDto ToSummaryDto(this Shift shift)
    {
        return new ShiftSummaryDto
        {
            Id = shift.Id,
            ClientId = shift.ClientId,
            ClientFullName = shift.Client?.User?.FullName ?? string.Empty,
            CaregiverId = shift.CaregiverId,
            CaregiverFullName = shift.Caregiver?.User?.FullName,
            ScheduledStartTime = shift.ScheduledStartTime,
            ScheduledEndTime = shift.ScheduledEndTime,
            Status = (ContractShiftStatus)(int)shift.Status,
            ServiceType = (ContractServiceType)(int)shift.ServiceType,
        };
    }

    internal static ShiftDetailDto ToDetailDto(this Shift shift)
    {
        return new ShiftDetailDto
        {
            Id = shift.Id,
            ClientId = shift.ClientId,
            ClientFullName = shift.Client?.User?.FullName ?? string.Empty,
            CaregiverId = shift.CaregiverId,
            CaregiverFullName = shift.Caregiver?.User?.FullName,
            ScheduledStartTime = shift.ScheduledStartTime,
            ScheduledEndTime = shift.ScheduledEndTime,
            ActualStartTime = shift.ActualStartTime,
            ActualEndTime = shift.ActualEndTime,
            CheckInTime = shift.CheckInTime,
            CheckOutTime = shift.CheckOutTime,
            BreakMinutes = shift.BreakMinutes,
            BillableHours = shift.BillableHours,
            Status = (ContractShiftStatus)(int)shift.Status,
            ServiceType = (ContractServiceType)(int)shift.ServiceType,
            Notes = shift.Notes,
            CancellationReason = shift.CancellationReason,
        };
    }

    internal static VisitNoteSummaryDto ToSummaryDto(this VisitNote visitNote)
    {
        return new VisitNoteSummaryDto
        {
            Id = visitNote.Id,
            ShiftId = visitNote.ShiftId,
            CaregiverId = visitNote.CaregiverId,
            VisitDateTime = visitNote.VisitDateTime,
            PersonalCare = visitNote.PersonalCare,
            MealPreparation = visitNote.MealPreparation,
            Medication = visitNote.Medication,
            LightHousekeeping = visitNote.LightHousekeeping,
            Companionship = visitNote.Companionship,
            Transportation = visitNote.Transportation,
            Exercise = visitNote.Exercise,
            HasConcerns = !string.IsNullOrWhiteSpace(visitNote.Concerns),
        };
    }

    internal static VisitNoteDetailDto ToDetailDto(this VisitNote visitNote)
    {
        return new VisitNoteDetailDto
        {
            Id = visitNote.Id,
            ShiftId = visitNote.ShiftId,
            CaregiverId = visitNote.CaregiverId,
            VisitDateTime = visitNote.VisitDateTime,
            PersonalCare = visitNote.PersonalCare,
            MealPreparation = visitNote.MealPreparation,
            Medication = visitNote.Medication,
            LightHousekeeping = visitNote.LightHousekeeping,
            Companionship = visitNote.Companionship,
            Transportation = visitNote.Transportation,
            Exercise = visitNote.Exercise,
            Activities = visitNote.Activities,
            ClientCondition = visitNote.ClientCondition,
            Concerns = visitNote.Concerns,
            Medications = visitNote.Medications,
            BloodPressureSystolic = visitNote.BloodPressureSystolic,
            BloodPressureDiastolic = visitNote.BloodPressureDiastolic,
            Temperature = visitNote.Temperature,
            HeartRate = visitNote.HeartRate,
            TransitionPlanId = visitNote.TransitionPlanId,
            Photos = visitNote.Photos.Select(photo => photo.ToDto()).ToList(),
            CaregiverSignatureUrl = null,
            ClientOrFamilySignatureUrl = null,
        };
    }

    internal static VisitPhotoDto ToDto(this VisitPhoto photo)
    {
        return new VisitPhotoDto
        {
            Id = photo.Id,
            VisitNoteId = photo.VisitNoteId,
            TakenAt = photo.TakenAt,
            Caption = null,
            Url = null,
        };
    }
}
