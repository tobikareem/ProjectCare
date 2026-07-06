using System.Reflection;
using CarePath.WebApi.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace CarePath.Application.Tests.WebApi;

public sealed class Sprint4ControllerContractTests
{
    [Theory]
    [InlineData(typeof(CaregiversController), "GetCaregivers", "Admin,Coordinator")]
    [InlineData(typeof(CaregiversController), "GetCaregiver", "Admin,Coordinator,Caregiver")]
    [InlineData(typeof(CaregiversController), "CreateCaregiver", "Admin,Coordinator")]
    [InlineData(typeof(CaregiversController), "UpdateCaregiver", "Admin,Coordinator")]
    [InlineData(typeof(CaregiversController), "AddCertification", "Admin,Coordinator")]
    [InlineData(typeof(CaregiversController), "GetExpiringCertifications", "Admin,Coordinator")]
    [InlineData(typeof(ClientsController), "GetClients", "Admin,Coordinator,Clinician")]
    [InlineData(typeof(ClientsController), "GetClient", "Admin,Coordinator,Clinician,Client,Caregiver")]
    [InlineData(typeof(ClientsController), "CreateClient", "Admin,Coordinator")]
    [InlineData(typeof(ClientsController), "UpdateClient", "Admin,Coordinator")]
    [InlineData(typeof(ClientsController), "GetCarePlans", "Admin,Coordinator,Clinician,Client,Caregiver")]
    [InlineData(typeof(ClientsController), "CreateCarePlan", "Admin,Coordinator,Clinician")]
    [InlineData(typeof(ClientsController), "GetAccessGrants", "Admin,Coordinator")]
    [InlineData(typeof(ClientsController), "CreateAccessGrant", "Admin,Coordinator")]
    [InlineData(typeof(ClientsController), "RevokeAccessGrant", "Admin,Coordinator")]
    [InlineData(typeof(CarePlansController), "UpdateCarePlan", "Admin,Coordinator,Clinician")]
    [InlineData(typeof(ShiftsController), "GetShifts", "Admin,Coordinator,Caregiver,Client,FacilityManager,Clinician")]
    [InlineData(typeof(ShiftsController), "GetShift", "Admin,Coordinator,Caregiver,Client,FacilityManager,Clinician")]
    [InlineData(typeof(ShiftsController), "CreateShift", "Admin,Coordinator")]
    [InlineData(typeof(ShiftsController), "UpdateShift", "Admin,Coordinator")]
    [InlineData(typeof(ShiftsController), "CheckIn", "Caregiver")]
    [InlineData(typeof(ShiftsController), "CheckOut", "Caregiver")]
    [InlineData(typeof(ShiftsController), "GetVisitNotes", "Admin,Coordinator,Clinician,Client,Caregiver")]
    [InlineData(typeof(ShiftsController), "CreateVisitNote", "Caregiver")]
    [InlineData(typeof(VisitNotesController), "GetVisitNote", "Admin,Coordinator,Clinician,Client,Caregiver")]
    [InlineData(typeof(VisitNotesController), "AddPhoto", "Caregiver")]
    [InlineData(typeof(InvoicesController), "GetInvoices", "Admin,Coordinator")]
    [InlineData(typeof(InvoicesController), "GetInvoice", "Admin,Coordinator,Client")]
    [InlineData(typeof(InvoicesController), "CreateInvoice", "Admin,Coordinator")]
    [InlineData(typeof(InvoicesController), "RecordPayment", "Admin,Coordinator")]
    [InlineData(typeof(BillingMarginsController), "GetMarginSummary", "Admin")]
    [InlineData(typeof(BillingMarginsController), "GetShiftMargins", "Admin")]
    public void ControllerAction_WhenEndpointTouchesSprint4Operations_DeclaresExpectedRoles(Type controllerType, string actionName, string expectedRoles)
    {
        // Arrange
        var method = controllerType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Single(method => method.Name == actionName);

        // Act
        var authorize = method.GetCustomAttribute<AuthorizeAttribute>()
            ?? controllerType.GetCustomAttribute<AuthorizeAttribute>();

        // Assert
        authorize.Should().NotBeNull();
        authorize!.Roles.Should().Be(expectedRoles);
    }

    [Theory]
    [InlineData(typeof(CaregiversController), "api/caregivers")]
    [InlineData(typeof(ClientsController), "api/clients")]
    [InlineData(typeof(CarePlansController), "api/care-plans")]
    [InlineData(typeof(ShiftsController), "api/shifts")]
    [InlineData(typeof(VisitNotesController), "api/visit-notes")]
    [InlineData(typeof(InvoicesController), "api/invoices")]
    [InlineData(typeof(BillingMarginsController), "api/billing/margins")]
    public void Controller_WhenSprint4EndpointSurface_HasApiControllerAndRoute(Type controllerType, string route)
    {
        controllerType.GetCustomAttribute<ApiControllerAttribute>().Should().NotBeNull();
        controllerType.GetCustomAttribute<RouteAttribute>()?.Template.Should().Be(route);
    }

    [Theory]
    [InlineData(typeof(CaregiversController), "GetCaregivers", typeof(HttpGetAttribute), null)]
    [InlineData(typeof(CaregiversController), "GetCaregiver", typeof(HttpGetAttribute), "{id:guid}")]
    [InlineData(typeof(CaregiversController), "CreateCaregiver", typeof(HttpPostAttribute), null)]
    [InlineData(typeof(CaregiversController), "UpdateCaregiver", typeof(HttpPutAttribute), "{id:guid}")]
    [InlineData(typeof(CaregiversController), "AddCertification", typeof(HttpPostAttribute), "{id:guid}/certifications")]
    [InlineData(typeof(CaregiversController), "GetExpiringCertifications", typeof(HttpGetAttribute), "certifications/expiring")]
    [InlineData(typeof(ClientsController), "GetClients", typeof(HttpGetAttribute), null)]
    [InlineData(typeof(ClientsController), "GetClient", typeof(HttpGetAttribute), "{id:guid}")]
    [InlineData(typeof(ClientsController), "CreateClient", typeof(HttpPostAttribute), null)]
    [InlineData(typeof(ClientsController), "UpdateClient", typeof(HttpPutAttribute), "{id:guid}")]
    [InlineData(typeof(ClientsController), "GetCarePlans", typeof(HttpGetAttribute), "{clientId:guid}/care-plans")]
    [InlineData(typeof(ClientsController), "CreateCarePlan", typeof(HttpPostAttribute), "{clientId:guid}/care-plans")]
    [InlineData(typeof(ClientsController), "GetAccessGrants", typeof(HttpGetAttribute), "{clientId:guid}/access-grants")]
    [InlineData(typeof(ClientsController), "CreateAccessGrant", typeof(HttpPostAttribute), "{clientId:guid}/access-grants")]
    [InlineData(typeof(ClientsController), "RevokeAccessGrant", typeof(HttpDeleteAttribute), "{clientId:guid}/access-grants/{grantId:guid}")]
    [InlineData(typeof(CarePlansController), "UpdateCarePlan", typeof(HttpPutAttribute), "{id:guid}")]
    [InlineData(typeof(ShiftsController), "GetShifts", typeof(HttpGetAttribute), null)]
    [InlineData(typeof(ShiftsController), "GetShift", typeof(HttpGetAttribute), "{id:guid}")]
    [InlineData(typeof(ShiftsController), "CreateShift", typeof(HttpPostAttribute), null)]
    [InlineData(typeof(ShiftsController), "UpdateShift", typeof(HttpPutAttribute), "{id:guid}")]
    [InlineData(typeof(ShiftsController), "CheckIn", typeof(HttpPostAttribute), "{id:guid}/check-in")]
    [InlineData(typeof(ShiftsController), "CheckOut", typeof(HttpPostAttribute), "{id:guid}/check-out")]
    [InlineData(typeof(ShiftsController), "GetVisitNotes", typeof(HttpGetAttribute), "{shiftId:guid}/visit-notes")]
    [InlineData(typeof(ShiftsController), "CreateVisitNote", typeof(HttpPostAttribute), "{shiftId:guid}/visit-notes")]
    [InlineData(typeof(VisitNotesController), "GetVisitNote", typeof(HttpGetAttribute), "{id:guid}")]
    [InlineData(typeof(VisitNotesController), "AddPhoto", typeof(HttpPostAttribute), "{id:guid}/photos")]
    [InlineData(typeof(InvoicesController), "GetInvoices", typeof(HttpGetAttribute), null)]
    [InlineData(typeof(InvoicesController), "GetInvoice", typeof(HttpGetAttribute), "{id:guid}")]
    [InlineData(typeof(InvoicesController), "CreateInvoice", typeof(HttpPostAttribute), null)]
    [InlineData(typeof(InvoicesController), "RecordPayment", typeof(HttpPostAttribute), "{id:guid}/payments")]
    [InlineData(typeof(BillingMarginsController), "GetMarginSummary", typeof(HttpGetAttribute), null)]
    [InlineData(typeof(BillingMarginsController), "GetShiftMargins", typeof(HttpGetAttribute), "shifts")]
    public void ControllerAction_WhenEndpointIsInSprint4Matrix_DeclaresExpectedHttpMethodAndTemplate(
        Type controllerType,
        string actionName,
        Type httpAttributeType,
        string? routeTemplate)
    {
        // Arrange
        var method = controllerType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Single(method => method.Name == actionName);

        // Act
        var httpAttribute = method.GetCustomAttributes()
            .SingleOrDefault(attribute => attribute.GetType() == httpAttributeType);

        // Assert
        httpAttribute.Should().NotBeNull();
        ((HttpMethodAttribute)httpAttribute!).Template.Should().Be(routeTemplate);
    }

    [Fact]
    public void PublicControllerActions_DoNotReturnDomainEntityTypes()
    {
        // Arrange
        var controllerTypes = new[]
        {
            typeof(CaregiversController),
            typeof(ClientsController),
            typeof(CarePlansController),
            typeof(ShiftsController),
            typeof(VisitNotesController),
            typeof(InvoicesController),
            typeof(BillingMarginsController),
        };

        // Act
        var returnTypes = controllerTypes
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .Select(method => method.ReturnType.FullName ?? string.Empty)
            .ToArray();

        // Assert
        returnTypes.Should().NotContain(typeName => typeName.Contains("CarePath.Domain.", StringComparison.Ordinal));
    }
}