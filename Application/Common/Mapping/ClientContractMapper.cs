using CarePath.Contracts.Clients;
using CarePath.Domain.Entities.Clinical;
using CarePath.Domain.Entities.Identity;
using ContractAccessScope = CarePath.Contracts.Enumerations.AccessScope;
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

    internal static ClientDetailDto ToDetailDto(this Client client) => client.ToDetailDto(includeOperationalFields: true);

    internal static ClientDetailDto ToDetailDto(this Client client, bool includeOperationalFields)
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
            EstimatedWeeklyHours = includeOperationalFields ? client.EstimatedWeeklyHours : 0,
        };
    }

    internal static ClientAccessGrantDto ToDto(this ClientAccessGrant grant)
    {
        return new ClientAccessGrantDto
        {
            Id = grant.Id,
            ClientId = grant.ClientId,
            GranteeUserId = grant.GranteeUserId,
            GranteeFullName = grant.GranteeUser?.FullName ?? string.Empty,
            Scope = (ContractAccessScope)(int)grant.AccessScope,
            GrantedByUserId = grant.GrantedByUserId,
            GrantedAtUtc = grant.GrantedAtUtc,
            RevokedAtUtc = grant.RevokedAtUtc,
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
