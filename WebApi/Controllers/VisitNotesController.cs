using CarePath.Application.Abstractions.Auth;
using CarePath.Application.Common.Exceptions;
using CarePath.Application.Scheduling.Services;
using CarePath.Contracts.Scheduling;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarePath.WebApi.Controllers;

[ApiController]
[Route("api/visit-notes")]
public sealed class VisitNotesController : ControllerBase
{
    private readonly IVisitDocumentationService service;
    private readonly IIdorGuard idorGuard;

    public VisitNotesController(IVisitDocumentationService service, IIdorGuard idorGuard)
    {
        this.service = service;
        this.idorGuard = idorGuard;
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin,Coordinator,Clinician,Client,Caregiver")]
    public async Task<ActionResult<VisitNoteDetailDto>> GetVisitNote(Guid id, CancellationToken cancellationToken)
    {
        await EnsureAuthorizedAsync(ProtectedResourceType.VisitNote, id, ObjectAccessAction.Read, cancellationToken);
        return Ok(await service.GetVisitNoteAsync(id, cancellationToken));
    }

    [HttpPost("{id:guid}/photos")]
    [Authorize(Roles = "Caregiver")]
    public async Task<ActionResult<VisitPhotoDto>> AddPhoto(
        Guid id,
        IFormFile file,
        [FromForm] string? caption,
        [FromForm] DateTime takenAtUtc,
        CancellationToken cancellationToken)
    {
        await EnsureAuthorizedAsync(ProtectedResourceType.VisitNote, id, ObjectAccessAction.Update, cancellationToken);
        await using var stream = file.OpenReadStream();
        return Ok(await service.AddVisitPhotoAsync(id, file.FileName, file.ContentType, stream, caption, takenAtUtc, cancellationToken));
    }

    private async Task EnsureAuthorizedAsync(ProtectedResourceType resourceType, Guid resourceId, ObjectAccessAction action, CancellationToken cancellationToken)
    {
        var result = await idorGuard.EnsureAuthorizedAsync(resourceType, resourceId, action, cancellationToken);
        if (!result.IsAuthorized)
        {
            throw new ResourceAccessDeniedException(result.DenialCode ?? "ResourceUnavailable", isPhiResource: true);
        }
    }
}
