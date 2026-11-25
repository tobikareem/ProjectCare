using CarePath.Application.Abstractions.Auth;
using CarePath.Contracts.Organization;
using CarePath.Domain.Entities.Platform;
using ContractMembershipStatus = CarePath.Contracts.Enumerations.MembershipStatus;
using ContractUserRole = CarePath.Contracts.Enumerations.UserRole;

namespace CarePath.Application.Organization.Mapping;

/// <summary>Domain → Contracts mapping for memberships.</summary>
public static class MembershipContractMapper
{
    /// <summary>Maps a membership with its identity summary.</summary>
    public static MembershipDto ToDto(OrganizationMembership membership, PlatformUserSummary? user) => new()
    {
        Id = membership.Id,
        PlatformUserId = membership.PlatformUserId,
        TenantUserId = membership.TenantUserId,
        Email = user?.Email ?? string.Empty,
        DisplayName = user?.DisplayName ?? string.Empty,
        Role = (ContractUserRole)(int)membership.Role,
        Status = (ContractMembershipStatus)membership.Status,
        LastLoginAtUtc = membership.LastLoginAtUtc,
    };
}
