using CarePath.Contracts.Identity;
using CarePath.Domain.Entities.Identity;
using ContractCertificationType = CarePath.Contracts.Enumerations.CertificationType;
using ContractEmploymentType = CarePath.Contracts.Enumerations.EmploymentType;
using ContractUserRole = CarePath.Contracts.Enumerations.UserRole;

namespace CarePath.Application.Common.Mapping;

internal static class IdentityContractMapper
{
    internal static UserSummaryDto ToSummaryDto(this User user)
    {
        return new UserSummaryDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Role = (ContractUserRole)(int)user.Role,
            IsActive = user.IsActive,
        };
    }

    internal static CaregiverSummaryDto ToSummaryDto(this Caregiver caregiver)
    {
        return new CaregiverSummaryDto
        {
            Id = caregiver.Id,
            UserId = caregiver.UserId,
            FullName = caregiver.User?.FullName ?? string.Empty,
            EmploymentType = (ContractEmploymentType)(int)caregiver.EmploymentType,
            AverageRating = caregiver.AverageRating,
            IsActive = caregiver.User?.IsActive ?? false,
        };
    }

    internal static CaregiverDetailDto ToDetailDto(
        this Caregiver caregiver,
        int shiftsMtd = 0,
        decimal billableHoursMtd = 0m)
    {
        return new CaregiverDetailDto
        {
            Id = caregiver.Id,
            UserId = caregiver.UserId,
            FullName = caregiver.User?.FullName ?? string.Empty,
            Email = caregiver.User?.Email ?? string.Empty,
            PhoneNumber = caregiver.User?.PhoneNumber ?? string.Empty,
            EmploymentType = (ContractEmploymentType)(int)caregiver.EmploymentType,
            IsActive = caregiver.User?.IsActive ?? false,
            HourlyPayRate = caregiver.HourlyPayRate,
            HireDate = caregiver.HireDate,
            TerminationDate = caregiver.TerminationDate,
            HasDementiaCare = caregiver.HasDementiaCare,
            HasAlzheimersCare = caregiver.HasAlzheimersCare,
            HasMobilityAssistance = caregiver.HasMobilityAssistance,
            HasMedicationManagement = caregiver.HasMedicationManagement,
            AvailableWeekdays = caregiver.AvailableWeekdays,
            AvailableWeekends = caregiver.AvailableWeekends,
            AvailableNights = caregiver.AvailableNights,
            MaxWeeklyHours = caregiver.MaxWeeklyHours,
            AverageRating = caregiver.AverageRating,
            ShiftsMtd = shiftsMtd,
            BillableHoursMtd = billableHoursMtd,
            TotalShiftsCompleted = caregiver.TotalShiftsCompleted,
            NoShowCount = caregiver.NoShowCount,
            Certifications = caregiver.Certifications
                .Select(certification => certification.ToDto())
                .ToArray(),
        };
    }

    internal static CertificationDto ToDto(this CaregiverCertification certification)
    {
        return new CertificationDto
        {
            Id = certification.Id,
            CaregiverId = certification.CaregiverId,
            Type = (ContractCertificationType)(int)certification.Type,
            CertificationNumber = certification.CertificationNumber,
            IssueDate = certification.IssueDate,
            ExpirationDate = certification.ExpirationDate,
            IssuingAuthority = certification.IssuingAuthority,
            IsExpired = certification.IsExpired,
            IsExpiringSoon = certification.IsExpiringSoon,
        };
    }
}
