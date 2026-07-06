using CarePath.Application.Abstractions.Auth;
using CarePath.Domain.Entities.Billing;
using CarePath.Domain.Entities.Clinical;
using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Entities.Scheduling;
using CarePath.Domain.Entities.Transitions;
using CarePath.Domain.Enumerations;
using CarePath.Domain.Interfaces.Repositories;

namespace CarePath.Application.Auth;

public sealed class Sprint4ObjectAuthorizationService : IObjectAuthorizationService
{
    private const string RoleInsufficient = "RoleInsufficient";
    private const string NoGrant = "NoGrant";
    private const string NotAssigned = "NotAssigned";

    private readonly IUnitOfWork unitOfWork;
    private readonly IClientAccessEvaluator clientAccessEvaluator;

    public Sprint4ObjectAuthorizationService(IUnitOfWork unitOfWork, IClientAccessEvaluator clientAccessEvaluator)
    {
        this.unitOfWork = unitOfWork;
        this.clientAccessEvaluator = clientAccessEvaluator;
    }

    public async Task<ObjectAuthorizationResult> AuthorizeAsync(ObjectAccessRequest request, CancellationToken cancellationToken = default)
    {
        if (HasAnyRole(request.User, ApplicationRoles.Admin, ApplicationRoles.Coordinator))
        {
            return ObjectAuthorizationResult.Authorized();
        }

        return request.ResourceType switch
        {
            ProtectedResourceType.Client => await AuthorizeClientAsync(request, request.ResourceId, cancellationToken),
            ProtectedResourceType.CarePlan => await AuthorizeCarePlanAsync(request, cancellationToken),
            ProtectedResourceType.Shift => await AuthorizeShiftAsync(request, cancellationToken),
            ProtectedResourceType.VisitNote => await AuthorizeVisitNoteAsync(request, cancellationToken),
            ProtectedResourceType.VisitPhoto => await AuthorizeVisitPhotoAsync(request, cancellationToken),
            ProtectedResourceType.Invoice => await AuthorizeInvoiceAsync(request, cancellationToken),
            ProtectedResourceType.DischargeDocument => await AuthorizeDischargeDocumentAsync(request, cancellationToken),
            ProtectedResourceType.TransitionPlan => await AuthorizeTransitionPlanAsync(request, request.ResourceId, cancellationToken),
            ProtectedResourceType.TransitionInstruction => await AuthorizeTransitionInstructionAsync(request, cancellationToken),
            ProtectedResourceType.TransitionCheckIn => await AuthorizeTransitionCheckInAsync(request, cancellationToken),
            ProtectedResourceType.TransitionReminder => await AuthorizeTransitionReminderAsync(request, cancellationToken),
            ProtectedResourceType.TransitionEscalation => await AuthorizeTransitionEscalationAsync(request, cancellationToken),
            ProtectedResourceType.Caregiver => await AuthorizeCaregiverAsync(request, cancellationToken),
            ProtectedResourceType.ClientAccessGrant => ObjectAuthorizationResult.Denied(RoleInsufficient),
            _ => ObjectAuthorizationResult.Denied(RoleInsufficient),
        };
    }

    private async Task<ObjectAuthorizationResult> AuthorizeClientAsync(ObjectAccessRequest request, Guid clientId, CancellationToken cancellationToken)
    {
        if (HasRole(request.User, ApplicationRoles.Client) && request.User.UserId.HasValue)
        {
            if (await unitOfWork.Clients.ExistsAsync(client => client.Id == clientId && client.UserId == request.User.UserId.Value, cancellationToken))
            {
                return ObjectAuthorizationResult.Authorized();
            }

            var access = await clientAccessEvaluator.EvaluateAsync(request.User.UserId.Value, clientId, AccessScope.Full, cancellationToken);
            return access.IsAuthorized ? ObjectAuthorizationResult.Authorized() : ObjectAuthorizationResult.Denied(NoGrant);
        }

        if (HasRole(request.User, ApplicationRoles.Caregiver) && request.User.UserId.HasValue)
        {
            var now = DateTime.UtcNow;
            var caregivers = await unitOfWork.Caregivers.FindAsync(caregiver => caregiver.UserId == request.User.UserId.Value, cancellationToken);
            var caregiverIds = caregivers.Select(caregiver => caregiver.Id).ToArray();
            var assigned = caregiverIds.Length > 0 && await unitOfWork.Shifts.ExistsAsync(
                shift => shift.ClientId == clientId
                    && shift.CaregiverId.HasValue
                    && caregiverIds.Contains(shift.CaregiverId.Value)
                    && shift.ScheduledStartTime <= now
                    && shift.ScheduledEndTime >= now
                    && (shift.Status == ShiftStatus.Scheduled || shift.Status == ShiftStatus.InProgress),
                cancellationToken);
            return assigned ? ObjectAuthorizationResult.Authorized() : ObjectAuthorizationResult.Denied(NotAssigned);
        }

        if (HasRole(request.User, ApplicationRoles.Clinician))
        {
            var hasTransitionRelationship = await unitOfWork.TransitionPlans.ExistsAsync(
                plan => plan.ClientId == clientId && plan.Status != TransitionPlanStatus.Cancelled,
                cancellationToken);
            return hasTransitionRelationship
                ? ObjectAuthorizationResult.Authorized()
                : ObjectAuthorizationResult.Denied(NoGrant);
        }

        return ObjectAuthorizationResult.Denied(RoleInsufficient);
    }

    private async Task<ObjectAuthorizationResult> AuthorizeCarePlanAsync(ObjectAccessRequest request, CancellationToken cancellationToken)
    {
        var carePlan = await unitOfWork.CarePlans.GetByIdAsync(request.ResourceId, cancellationToken);
        return carePlan is null
            ? ObjectAuthorizationResult.Denied(NoGrant)
            : await AuthorizeClientAsync(request, carePlan.ClientId, cancellationToken);
    }

    private async Task<ObjectAuthorizationResult> AuthorizeShiftAsync(ObjectAccessRequest request, CancellationToken cancellationToken)
    {
        var shift = await unitOfWork.Shifts.GetByIdAsync(request.ResourceId, cancellationToken);
        if (shift is null)
        {
            return ObjectAuthorizationResult.Denied(NoGrant);
        }

        if (HasRole(request.User, ApplicationRoles.Caregiver) && request.User.UserId.HasValue)
        {
            var caregivers = await unitOfWork.Caregivers.FindAsync(caregiver => caregiver.UserId == request.User.UserId.Value, cancellationToken);
            return caregivers.Any(caregiver => shift.CaregiverId == caregiver.Id)
                ? ObjectAuthorizationResult.Authorized()
                : ObjectAuthorizationResult.Denied(NotAssigned);
        }

        return await AuthorizeClientAsync(request, shift.ClientId, cancellationToken);
    }

    private async Task<ObjectAuthorizationResult> AuthorizeVisitNoteAsync(ObjectAccessRequest request, CancellationToken cancellationToken)
    {
        var note = await unitOfWork.VisitNotes.GetByIdAsync(request.ResourceId, cancellationToken);
        if (note is null)
        {
            return ObjectAuthorizationResult.Denied(NoGrant);
        }

        return await AuthorizeShiftByIdAsync(request, note.ShiftId, cancellationToken);
    }

    private async Task<ObjectAuthorizationResult> AuthorizeVisitPhotoAsync(ObjectAccessRequest request, CancellationToken cancellationToken)
    {
        var photo = await unitOfWork.VisitPhotos.GetByIdAsync(request.ResourceId, cancellationToken);
        if (photo is null)
        {
            return ObjectAuthorizationResult.Denied(NoGrant);
        }

        var note = await unitOfWork.VisitNotes.GetByIdAsync(photo.VisitNoteId, cancellationToken);
        return note is null
            ? ObjectAuthorizationResult.Denied(NoGrant)
            : await AuthorizeShiftByIdAsync(request, note.ShiftId, cancellationToken);
    }

    private async Task<ObjectAuthorizationResult> AuthorizeInvoiceAsync(ObjectAccessRequest request, CancellationToken cancellationToken)
    {
        var invoice = await unitOfWork.Invoices.GetByIdAsync(request.ResourceId, cancellationToken);
        return invoice is null
            ? ObjectAuthorizationResult.Denied(NoGrant)
            : await AuthorizeClientAsync(request, invoice.ClientId, cancellationToken);
    }

    private async Task<ObjectAuthorizationResult> AuthorizeDischargeDocumentAsync(ObjectAccessRequest request, CancellationToken cancellationToken)
    {
        if (HasRole(request.User, ApplicationRoles.Clinician))
        {
            return ObjectAuthorizationResult.Authorized();
        }

        var document = await unitOfWork.DischargeDocuments.GetByIdAsync(request.ResourceId, cancellationToken);
        return document is null
            ? ObjectAuthorizationResult.Denied(NoGrant)
            : await AuthorizeClientAsync(request, document.ClientId, cancellationToken);
    }

    private async Task<ObjectAuthorizationResult> AuthorizeTransitionPlanAsync(
        ObjectAccessRequest request,
        Guid planId,
        CancellationToken cancellationToken)
    {
        if (HasRole(request.User, ApplicationRoles.Clinician))
        {
            return ObjectAuthorizationResult.Authorized();
        }

        var plan = await unitOfWork.TransitionPlans.GetByIdAsync(planId, cancellationToken);
        return plan is null
            ? ObjectAuthorizationResult.Denied(NoGrant)
            : await AuthorizeClientAsync(request, plan.ClientId, cancellationToken);
    }

    private async Task<ObjectAuthorizationResult> AuthorizeTransitionInstructionAsync(ObjectAccessRequest request, CancellationToken cancellationToken)
    {
        if (HasRole(request.User, ApplicationRoles.Clinician))
        {
            return ObjectAuthorizationResult.Authorized();
        }

        var instruction = await unitOfWork.TransitionInstructions.GetByIdAsync(request.ResourceId, cancellationToken);
        return instruction is null
            ? ObjectAuthorizationResult.Denied(NoGrant)
            : await AuthorizeTransitionPlanAsync(request, instruction.TransitionPlanId, cancellationToken);
    }

    private async Task<ObjectAuthorizationResult> AuthorizeTransitionCheckInAsync(ObjectAccessRequest request, CancellationToken cancellationToken)
    {
        if (HasRole(request.User, ApplicationRoles.Clinician))
        {
            return ObjectAuthorizationResult.Authorized();
        }

        var checkIn = await unitOfWork.TransitionCheckIns.GetByIdAsync(request.ResourceId, cancellationToken);
        return checkIn is null
            ? ObjectAuthorizationResult.Denied(NoGrant)
            : await AuthorizeTransitionPlanAsync(request, checkIn.TransitionPlanId, cancellationToken);
    }

    private async Task<ObjectAuthorizationResult> AuthorizeTransitionReminderAsync(ObjectAccessRequest request, CancellationToken cancellationToken)
    {
        if (HasRole(request.User, ApplicationRoles.Clinician))
        {
            return ObjectAuthorizationResult.Authorized();
        }

        var reminder = await unitOfWork.TransitionReminders.GetByIdAsync(request.ResourceId, cancellationToken);
        return reminder is null
            ? ObjectAuthorizationResult.Denied(NoGrant)
            : await AuthorizeTransitionPlanAsync(request, reminder.TransitionPlanId, cancellationToken);
    }

    private async Task<ObjectAuthorizationResult> AuthorizeTransitionEscalationAsync(ObjectAccessRequest request, CancellationToken cancellationToken)
    {
        if (HasRole(request.User, ApplicationRoles.Clinician))
        {
            return ObjectAuthorizationResult.Authorized();
        }

        var escalation = await unitOfWork.TransitionEscalations.GetByIdAsync(request.ResourceId, cancellationToken);
        return escalation is null
            ? ObjectAuthorizationResult.Denied(NoGrant)
            : await AuthorizeTransitionPlanAsync(request, escalation.TransitionPlanId, cancellationToken);
    }

    private async Task<ObjectAuthorizationResult> AuthorizeCaregiverAsync(ObjectAccessRequest request, CancellationToken cancellationToken)
    {
        if (!HasRole(request.User, ApplicationRoles.Caregiver) || !request.User.UserId.HasValue)
        {
            return ObjectAuthorizationResult.Denied(RoleInsufficient);
        }

        var caregiver = await unitOfWork.Caregivers.GetByIdAsync(request.ResourceId, cancellationToken);
        return caregiver?.UserId == request.User.UserId.Value
            ? ObjectAuthorizationResult.Authorized()
            : ObjectAuthorizationResult.Denied(RoleInsufficient);
    }

    private async Task<ObjectAuthorizationResult> AuthorizeShiftByIdAsync(ObjectAccessRequest request, Guid shiftId, CancellationToken cancellationToken)
    {
        var shiftedRequest = request with { ResourceType = ProtectedResourceType.Shift, ResourceId = shiftId };
        return await AuthorizeShiftAsync(shiftedRequest, cancellationToken);
    }

    private static bool HasAnyRole(ICurrentUserContext user, params string[] roles) => roles.Any(role => HasRole(user, role));

    private static bool HasRole(ICurrentUserContext user, string role) => user.Roles.Contains(role);
}

