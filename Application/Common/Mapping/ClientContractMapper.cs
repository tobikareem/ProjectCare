using CarePath.Contracts.Clients;
using CarePath.Domain.Entities.Clinical;
using CarePath.Domain.Entities.Identity;
using ContractServiceType = CarePath.Contracts.Enumerations.ServiceType;

namespace CarePath.Application.Common.Mapping;

internal static class ClientContractMapper
{
    internal static ClientSummaryDto ToSummaryDto(this Client client)
    {
        return new ClientSummaryDto
        {
            Id = client.Id,
            UserId = client.UserId,
            FullName = client.User?.FullName ?? string.Empty,
            ServiceType = (ContractServiceType)(int)client.ServiceType,
            Age = client.Age,
            IsActive = client.User?.IsActive ?? false,
        };
    }

    internal static ClientDetailDto ToDetailDto(this Client client)
    {
        return new ClientDetailDto
        {
            Id = client.Id,
            UserId = client.UserId,
            FullName = client.User?.FullName ?? string.Empty,
            PhoneNumber = client.User?.PhoneNumber ?? string.Empty,
            DateOfBirth = client.DateOfBirth,
            Age = client.Age,
            EmergencyContactName = client.EmergencyContactName,
            EmergencyContactPhone = client.EmergencyContactPhone,
            EmergencyContactRelationship = client.EmergencyContactRelationship,
            RequiresDementiaCare = client.RequiresDementiaCare,
            RequiresMobilityAssistance = client.RequiresMobilityAssistance,
            RequiresMedicationManagement = client.RequiresMedicationManagement,
            RequiresCompanionship = client.RequiresCompanionship,
            SpecialInstructions = client.SpecialInstructions,
            MedicalConditions = client.MedicalConditions,
            Allergies = client.Allergies,
            ServiceType = (ContractServiceType)(int)client.ServiceType,
            HourlyBillRate = client.HourlyBillRate,
            EstimatedWeeklyHours = client.EstimatedWeeklyHours,
        };
    }

    internal static CarePlanDto ToDto(this CarePlan carePlan)
    {
        return new CarePlanDto
        {
            Id = carePlan.Id,
            ClientId = carePlan.ClientId,
            Title = carePlan.Title,
            Description = carePlan.Description,
            StartDate = carePlan.StartDate,
            EndDate = carePlan.EndDate,
            IsActive = carePlan.IsActive,
            Goals = carePlan.Goals,
            Interventions = carePlan.Interventions,
            Notes = carePlan.Notes,
        };
    }
}
