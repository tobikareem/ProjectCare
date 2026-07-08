using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Entities.Scheduling;
using CarePath.Domain.Enumerations;

namespace CarePath.Application.Scheduling.Services;

internal static class CaregiverShiftEligibility
{
    private static readonly CertificationType[] RequiredCertificationTypes =
    [
        CertificationType.HHA,
        CertificationType.CNA,
        CertificationType.GNA,
        CertificationType.LPN,
        CertificationType.RN,
    ];

    internal static CaregiverShiftEligibilityResult Evaluate(
        Caregiver caregiver,
        Shift shift,
        IReadOnlyCollection<CaregiverCertification> certifications,
        IReadOnlyCollection<Shift> assignedShifts)
    {
        var matches = new List<string>();
        var blocks = new List<string>();

        if (caregiver.User?.IsActive != true || caregiver.TerminationDate.HasValue)
        {
            blocks.Add("Inactive caregiver");
        }
        else
        {
            matches.Add("Active caregiver");
        }

        var requiredCredentialTypes = GetRequiredCertificationTypes(shift.ServiceType);
        var hasValidCredential = certifications.Any(certification =>
            requiredCredentialTypes.Contains(certification.Type)
            && certification.ExpirationDate.Date >= shift.ScheduledStartTime.Date);
        if (hasValidCredential)
        {
            matches.Add("Credential fit");
        }
        else
        {
            blocks.Add("Credential expired or missing");
        }

        if (HasAvailability(caregiver, shift.ScheduledStartTime))
        {
            matches.Add("Availability fit");
        }
        else
        {
            blocks.Add("Availability mismatch");
        }

        if (IsDoubleBooked(shift, assignedShifts))
        {
            blocks.Add("Double-booked");
        }
        else
        {
            matches.Add("No schedule overlap");
        }

        var scheduledHoursThisWeek = assignedShifts
            .Where(existing => IsSameUtcWeek(existing.ScheduledStartTime, shift.ScheduledStartTime)
                && existing.Status != ShiftStatus.Cancelled)
            .Sum(existing => Math.Max(0, (existing.ScheduledEndTime - existing.ScheduledStartTime).TotalHours));
        var requestedHours = Math.Max(0, (shift.ScheduledEndTime - shift.ScheduledStartTime).TotalHours);
        if ((decimal)(scheduledHoursThisWeek + requestedHours) <= caregiver.MaxWeeklyHours)
        {
            matches.Add("Within weekly capacity");
        }
        else
        {
            blocks.Add("Weekly capacity exceeded");
        }

        if (HasSpecialtySkills(caregiver))
        {
            matches.Add("Specialty skills recorded");
        }
        else
        {
            matches.Add("No specialty skill requirement recorded");
        }

        return new CaregiverShiftEligibilityResult(blocks.Count == 0, matches, blocks);
    }

    internal static IReadOnlyList<string> RequirementLabels(Shift shift)
    {
        var labels = new List<string>
        {
            shift.ServiceType == ServiceType.FacilityStaffing ? "Facility staffing" : "In-home care",
            shift.ServiceType == ServiceType.FacilityStaffing
                ? "Valid facility credential"
                : "Valid direct-care credential",
        };

        labels.Add(shift.ScheduledStartTime.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
            ? "Weekend availability"
            : "Weekday availability");

        if (shift.ScheduledStartTime.Hour < 6 || shift.ScheduledStartTime.Hour >= 20)
        {
            labels.Add("Night availability");
        }

        return labels;
    }

    private static bool HasAvailability(Caregiver caregiver, DateTime scheduledStartUtc)
    {
        if (scheduledStartUtc.Hour < 6 || scheduledStartUtc.Hour >= 20)
        {
            return caregiver.AvailableNights;
        }

        return scheduledStartUtc.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
            ? caregiver.AvailableWeekends
            : caregiver.AvailableWeekdays;
    }

    private static IReadOnlyList<CertificationType> GetRequiredCertificationTypes(ServiceType serviceType)
    {
        return serviceType == ServiceType.FacilityStaffing
            ? [CertificationType.CNA, CertificationType.GNA, CertificationType.LPN, CertificationType.RN]
            : RequiredCertificationTypes;
    }

    private static bool HasSpecialtySkills(Caregiver caregiver) =>
        caregiver.HasDementiaCare
        || caregiver.HasAlzheimersCare
        || caregiver.HasMobilityAssistance
        || caregiver.HasMedicationManagement;

    private static bool IsDoubleBooked(Shift candidate, IEnumerable<Shift> assignedShifts)
    {
        return assignedShifts.Any(existing =>
            existing.Id != candidate.Id
            && existing.Status != ShiftStatus.Cancelled
            && existing.Status != ShiftStatus.Completed
            && existing.ScheduledStartTime < candidate.ScheduledEndTime
            && candidate.ScheduledStartTime < existing.ScheduledEndTime);
    }

    private static bool IsSameUtcWeek(DateTime first, DateTime second)
    {
        var firstStart = StartOfWeek(first);
        var secondStart = StartOfWeek(second);
        return firstStart == secondStart;
    }

    private static DateTime StartOfWeek(DateTime value)
    {
        var date = value.Date;
        var offset = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-offset);
    }
}

internal sealed record CaregiverShiftEligibilityResult(
    bool IsAssignable,
    IReadOnlyList<string> MatchReasons,
    IReadOnlyList<string> BlockingReasons);
