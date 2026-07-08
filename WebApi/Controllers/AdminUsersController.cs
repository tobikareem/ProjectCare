using CarePath.Application.Admin.Services;
using CarePath.Contracts.Admin;
using CarePath.Contracts.Common;
using CarePath.Contracts.Enumerations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarePath.WebApi.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "Admin")]
public sealed class AdminUsersController : ControllerBase
{
    private readonly IAdminUserManagementService service;

    public AdminUsersController(IAdminUserManagementService service)
    {
        this.service = service;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<UserAccountDto>>> GetUsers(
        [FromQuery] PagedRequest request,
        [FromQuery] UserRole? role,
        [FromQuery] bool? isActive,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        return Ok(await service.GetUsersAsync(request, role, isActive, search, cancellationToken));
    }

    [HttpGet("roles")]
    public async Task<ActionResult<IReadOnlyList<UserRole>>> GetAvailableRoles(CancellationToken cancellationToken)
    {
        return Ok(await service.GetAvailableRolesAsync(cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<UserAccountDto>> CreateStaffUser(
        [FromBody] CreateStaffUserRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateStaffUserAsync(request, cancellationToken);
        return Created($"/api/admin/users/{result.Id}", result);
    }

    [HttpPut("{id:guid}/role")]
    public async Task<ActionResult<UserAccountDto>> UpdateRole(
        Guid id,
        [FromBody] UpdateUserRoleRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await service.UpdateRoleAsync(id, request, cancellationToken));
    }

    [HttpPut("{id:guid}/status")]
    public async Task<ActionResult<UserAccountDto>> UpdateStatus(
        Guid id,
        [FromBody] UpdateUserStatusRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await service.UpdateStatusAsync(id, request, cancellationToken));
    }
}
