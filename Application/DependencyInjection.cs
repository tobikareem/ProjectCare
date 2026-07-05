using CarePath.Application.Abstractions.Auth;
using CarePath.Application.Auth;
using CarePath.Application.Clients.Services;
using CarePath.Application.Clients.Validators;
using CarePath.Application.Identity.Services;
using CarePath.Application.Identity.Validators;
using CarePath.Contracts.Clients;
using CarePath.Contracts.Identity;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CarePath.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddScoped<IObjectAuthorizationService, DenyByDefaultObjectAuthorizationService>();
        services.AddScoped<IIdorGuard, IdorGuard>();
        services.AddScoped<ICaregiverOperationsService, CaregiverOperationsService>();
        services.AddScoped<IClientOperationsService, ClientOperationsService>();

        services.AddScoped<IValidator<CreateCaregiverRequest>, CreateCaregiverRequestValidator>();
        services.AddScoped<IValidator<UpdateCaregiverRequest>, UpdateCaregiverRequestValidator>();
        services.AddScoped<IValidator<AddCertificationRequest>, AddCertificationRequestValidator>();
        services.AddScoped<IValidator<CreateClientRequest>, CreateClientRequestValidator>();
        services.AddScoped<IValidator<UpdateClientRequest>, UpdateClientRequestValidator>();
        services.AddScoped<IValidator<CreateCarePlanRequest>, CreateCarePlanRequestValidator>();
        services.AddScoped<IValidator<UpdateCarePlanRequest>, UpdateCarePlanRequestValidator>();

        return services;
    }
}

