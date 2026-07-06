using CarePath.Application.Abstractions.Auth;
using CarePath.Application.Abstractions.Billing;
using CarePath.Application.Auth;
using CarePath.Application.Billing.Services;
using CarePath.Application.Billing.Validators;
using CarePath.Application.Clients.Services;
using CarePath.Application.Clients.Validators;
using CarePath.Application.Identity.Services;
using CarePath.Application.Identity.Validators;
using CarePath.Application.Scheduling.Services;
using CarePath.Application.Scheduling.Validators;
using CarePath.Application.Transitions.Services;
using CarePath.Application.Transitions.Validators;
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
        services.AddScoped<IIdorGuard, IdorGuard>();
        services.AddScoped<ICaregiverOperationsService, CaregiverOperationsService>();
        services.AddScoped<IClientOperationsService, ClientOperationsService>();
        services.AddScoped<IClientAccessGrantService, ClientAccessGrantService>();
        services.AddScoped<IShiftOperationsService, ShiftOperationsService>();
        services.AddScoped<IVisitDocumentationService, VisitDocumentationService>();
        services.AddScoped<IBillingOperationsService, BillingOperationsService>();
        services.AddScoped<ITransitionsService, TransitionsService>();

        services.AddScoped<IValidator<CreateCaregiverRequest>, CreateCaregiverRequestValidator>();
        services.AddScoped<IValidator<UpdateCaregiverRequest>, UpdateCaregiverRequestValidator>();
        services.AddScoped<IValidator<AddCertificationRequest>, AddCertificationRequestValidator>();
        services.AddScoped<IValidator<CreateClientRequest>, CreateClientRequestValidator>();
        services.AddScoped<IValidator<CreateGrantRequest>, CreateGrantRequestValidator>();
        services.AddScoped<IValidator<UpdateClientRequest>, UpdateClientRequestValidator>();
        services.AddScoped<IValidator<CreateCarePlanRequest>, CreateCarePlanRequestValidator>();
        services.AddScoped<IValidator<UpdateCarePlanRequest>, UpdateCarePlanRequestValidator>();
        services.AddScoped<IValidator<CreateShiftRequest>, CreateShiftRequestValidator>();
        services.AddScoped<IValidator<UpdateShiftRequest>, UpdateShiftRequestValidator>();
        services.AddScoped<IValidator<CheckInRequest>, CheckInRequestValidator>();
        services.AddScoped<IValidator<CheckOutRequest>, CheckOutRequestValidator>();
        services.AddScoped<IValidator<CreateVisitNoteRequest>, CreateVisitNoteRequestValidator>();
        services.AddScoped<IValidator<CreateInvoiceRequest>, CreateInvoiceRequestValidator>();
        services.AddScoped<IValidator<RecordPaymentRequest>, RecordPaymentRequestValidator>();
        services.AddScoped<IValidator<CreateDischargeDocumentRequest>, CreateDischargeDocumentRequestValidator>();

        return services;
    }
}
