using CarePath.Contracts.Admin;
using CarePath.Contracts.Common;
using CarePath.Contracts.Enumerations;

namespace CarePath.Application.Admin.Services;

public interface IAdminUserManagementService
{
    Task<PagedResult<UserAccountDto>> GetUsersAsync(
        PagedRequest request,
        UserRole? role = null,
        bool? isActive = null,
        string? search = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserRole>> GetAvailableRolesAsync(CancellationToken cancellationToken = default);

    Task<UserAccountDto> CreateStaffUserAsync(
        CreateStaffUserRequest request,
        CancellationToken cancellationToken = default);

    Task<UserAccountDto> UpdateRoleAsync(
        Guid userId,
        UpdateUserRoleRequest request,
        CancellationToken cancellationToken = default);

    Task<UserAccountDto> UpdateStatusAsync(
        Guid userId,
        UpdateUserStatusRequest request,
        CancellationToken cancellationToken = default);
}
