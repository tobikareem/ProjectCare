using CarePath.Application.Abstractions.Auth;
using CarePath.Application.Abstractions.Billing;
using CarePath.Application.Admin.Services;
using CarePath.Application.Admin.Validators;
using CarePath.Application.Auth;
using CarePath.Application.Auth.Services;
using CarePath.Application.Billing.Services;
using CarePath.Application.Billing.Validators;
using CarePath.Application.Clients.Services;
using CarePath.Application.Clients.Validators;
using CarePath.Application.Identity.Services;
using CarePath.Application.Identity.Validators;
using CarePath.Application.Organization.Services;
using CarePath.Application.Organization.Validators;
using CarePath.Application.Platform.Services;
using CarePath.Application.Platform.Validators;
using CarePath.Contracts.Organization;
using CarePath.Contracts.Platform;
using CarePath.Application.Scheduling.Services;
using CarePath.Application.Scheduling.Queries;
using CarePath.Application.Scheduling.Validators;
using CarePath.Application.Transitions.Services;
using CarePath.Application.Transitions.Validators;
using CarePath.Contracts.Admin;
using CarePath.Contracts.Clients;
using CarePath.Contracts.Identity;
using CarePath.Contracts.Billing;
using CarePath.Contracts.Scheduling;
using CarePath.Contracts.Transitions;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CarePath.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddScoped<IObjectAuthorizationService, Sprint4ObjectAuthorizationService>();
        services.TryAddScoped<IPersistenceConflictDetector, NoOpPersistenceConflictDetector>();
        services.TryAddScoped<IShiftBillingQuery, NoOpShiftBillingQuery>();
        services.TryAddScoped<IAssignmentHistoryQuery, NoOpAssignmentHistoryQuery>();
        services.AddScoped<IIdorGuard, IdorGuard>();
        services.AddScoped<ICaregiverOperationsService, CaregiverOperationsService>();
        services.AddScoped<IClientOperationsService, ClientOperationsService>();
        services.AddScoped<IClientAccessGrantService, ClientAccessGrantService>();
        services.AddScoped<IShiftOperationsService, ShiftOperationsService>();
        services.AddScoped<IAssignmentHistoryService, AssignmentHistoryService>();
        services.AddScoped<IVisitDocumentationService, VisitDocumentationService>();
        services.AddScoped<IBillingOperationsService, BillingOperationsService>();
        services.AddScoped<IBillingReconciliationService, BillingReconciliationService>();
        services.AddScoped<ITransitionsService, TransitionsService>();
        services.AddScoped<IAdminUserManagementService, AdminUserManagementService>();
        services.AddScoped<IOrganizationBrandingQuery, OrganizationBrandingQuery>();
        services.AddScoped<IOrganizationMembershipService, OrganizationMembershipService>();
        services.AddScoped<ILoginService, LoginService>();
        services.AddScoped<IPlatformOrganizationService, PlatformOrganizationService>();
        services.AddScoped<IValidator<RegisterOrganizationRequest>, RegisterOrganizationRequestValidator>();
        services.AddScoped<IValidator<UpdateOrganizationRequest>, UpdateOrganizationRequestValidator>();
        services.AddScoped<IValidator<SuspendOrganizationRequest>, SuspendOrganizationRequestValidator>();
        services.AddScoped<IValidator<AddOrganizationDomainRequest>, AddOrganizationDomainRequestValidator>();
        services.AddScoped<IValidator<UpdateOrganizationBrandingRequest>, UpdateOrganizationBrandingRequestValidator>();
        services.AddScoped<IValidator<CreateMembershipRequest>, CreateMembershipRequestValidator>();
        services.AddScoped<IValidator<UpdateMembershipRoleRequest>, UpdateMembershipRoleRequestValidator>();

        services.AddScoped<IValidator<CreateCaregiverRequest>, CreateCaregiverRequestValidator>();
        services.AddScoped<IValidator<CreateStaffUserRequest>, CreateStaffUserRequestValidator>();
        services.AddScoped<IValidator<UpdateUserRoleRequest>, UpdateUserRoleRequestValidator>();
        services.AddScoped<IValidator<UpdateCaregiverRequest>, UpdateCaregiverRequestValidator>();
        services.AddScoped<IValidator<AddCertificationRequest>, AddCertificationRequestValidator>();
        services.AddScoped<IValidator<CreateClientRequest>, CreateClientRequestValidator>();
        services.AddScoped<IValidator<CreateGrantRequest>, CreateGrantRequestValidator>();
        services.AddScoped<IValidator<UpdateClientRequest>, UpdateClientRequestValidator>();
        services.AddScoped<IValidator<CreateCarePlanRequest>, CreateCarePlanRequestValidator>();
        services.AddScoped<IValidator<UpdateCarePlanRequest>, UpdateCarePlanRequestValidator>();
        services.AddScoped<IValidator<CreateShiftRequest>, CreateShiftRequestValidator>();
        services.AddScoped<IValidator<UpdateShiftRequest>, UpdateShiftRequestValidator>();
        services.AddScoped<IValidator<AssignmentHistorySearchRequest>, AssignmentHistorySearchRequestValidator>();
        services.AddScoped<IValidator<CheckInRequest>, CheckInRequestValidator>();
        services.AddScoped<IValidator<CheckOutRequest>, CheckOutRequestValidator>();
        services.AddScoped<IValidator<CreateVisitNoteRequest>, CreateVisitNoteRequestValidator>();
        services.AddScoped<IValidator<CreateInvoiceRequest>, CreateInvoiceRequestValidator>();
        services.AddScoped<IValidator<InvoicePreviewRequest>, InvoicePreviewRequestValidator>();
        services.AddScoped<IValidator<BillingReconciliationSearchRequest>, BillingReconciliationSearchRequestValidator>();
        services.AddScoped<IValidator<ResolveNonBillableRequest>, ResolveNonBillableRequestValidator>();
        services.AddScoped<IValidator<ReopenResolutionRequest>, ReopenResolutionRequestValidator>();
        services.AddScoped<IValidator<CorrectShiftTimeRequest>, CorrectShiftTimeRequestValidator>();
        services.AddScoped<IValidator<RecordPaymentRequest>, RecordPaymentRequestValidator>();
        services.AddScoped<IValidator<CreateDischargeDocumentRequest>, CreateDischargeDocumentRequestValidator>();
        services.AddScoped<IValidator<ReviewInstructionRequest>, ReviewInstructionRequestValidator>();
        services.AddScoped<IValidator<ActivatePlanRequest>, ActivatePlanRequestValidator>();
        services.AddScoped<IValidator<ScheduleReminderRequest>, ScheduleReminderRequestValidator>();
        services.AddScoped<IValidator<CreateCheckInRequest>, CreateCheckInRequestValidator>();
        services.AddScoped<IValidator<AcknowledgeEscalationRequest>, AcknowledgeEscalationRequestValidator>();

        return services;
    }
}
