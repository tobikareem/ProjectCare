using System.Reflection;
using CarePath.Contracts.Transitions;
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
    [InlineData(typeof(CaregiversController), "GetCaregiver", "Admin,Coordinator")]
    [InlineData(typeof(CaregiversController), "CreateCaregiver", "Admin,Coordinator")]
    [InlineData(typeof(CaregiversController), "UpdateCaregiver", "Admin,Coordinator")]
    [InlineData(typeof(CaregiversController), "AddCertification", "Admin,Coordinator")]
    [InlineData(typeof(CaregiversController), "GetExpiringCertifications", "Admin,Coordinator")]
    [InlineData(typeof(CaregiversController), "GetEligibleOpenShifts", "Admin,Coordinator")]
    [InlineData(typeof(CaregiversController), "SearchClientAssignments", "Admin,Coordinator")]
    [InlineData(typeof(CaregiversController), "SearchMyClients", "Caregiver")]
    [InlineData(typeof(ClientsController), "GetClients", "Admin,Coordinator,Clinician")]
    [InlineData(typeof(ClientsController), "GetClient", "Admin,Coordinator,Clinician,Client,Caregiver")]
    [InlineData(typeof(ClientsController), "SearchCaregiverAssignments", "Admin,Coordinator")]
    [InlineData(typeof(ClientsController), "SearchMyCaregivers", "Client")]
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
    [InlineData(typeof(ShiftsController), "GetCoverageQueue", "Admin,Coordinator")]
    [InlineData(typeof(ShiftsController), "GetEligibleCaregivers", "Admin,Coordinator")]
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
    [InlineData(typeof(AdminUsersController), "GetUsers", "Admin")]
    [InlineData(typeof(AdminUsersController), "GetAvailableRoles", "Admin")]
    [InlineData(typeof(AdminUsersController), "CreateStaffUser", "Admin")]
    [InlineData(typeof(AdminUsersController), "UpdateRole", "Admin")]
    [InlineData(typeof(AdminUsersController), "UpdateStatus", "Admin")]
    [InlineData(typeof(TransitionsController), "CreateDischargeDocument", "Admin,Coordinator")]
    [InlineData(typeof(TransitionsController), "GetDischargeDocument", "Admin,Coordinator,Clinician")]
    [InlineData(typeof(TransitionsController), "GetDischargeDocumentContent", "Admin,Coordinator,Clinician")]
    [InlineData(typeof(TransitionsController), "GetDischargeDocuments", "Admin,Coordinator,Clinician")]
    [InlineData(typeof(TransitionsController), "ExtractDischargeDocument", "Admin,Coordinator")]
    [InlineData(typeof(TransitionsController), "GetPlans", "Admin,Coordinator,Clinician")]
    [InlineData(typeof(TransitionsController), "GetPlan", "Admin,Coordinator,Clinician")]
    [InlineData(typeof(TransitionsController), "GetPatientPlan", "Client")]
    [InlineData(typeof(TransitionsController), "ReviewInstruction", "Admin,Clinician")]
    [InlineData(typeof(TransitionsController), "ActivatePlan", "Admin,Clinician")]
    [InlineData(typeof(TransitionsController), "ScheduleReminder", "Admin,Coordinator")]
    [InlineData(typeof(TransitionsController), "GetReminders", "Admin,Coordinator,Clinician")]
    [InlineData(typeof(TransitionsController), "CreateCheckIn", "Client")]
    [InlineData(typeof(TransitionsController), "GetCheckIns", "Admin,Coordinator,Clinician")]
    [InlineData(typeof(TransitionsController), "GetEscalations", "Admin,Coordinator")]
    [InlineData(typeof(TransitionsController), "GetEscalationQueue", "Admin,Coordinator")]
    [InlineData(typeof(TransitionsController), "AcknowledgeEscalation", "Admin,Coordinator")]
    [InlineData(typeof(TransitionsController), "GetPlanForClient", "Admin,Coordinator,Clinician,Caregiver")]
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
    [InlineData(typeof(AdminUsersController), "api/admin/users")]
    [InlineData(typeof(TransitionsController), "api/transitions")]
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
    [InlineData(typeof(CaregiversController), "GetEligibleOpenShifts", typeof(HttpGetAttribute), "{id:guid}/eligible-shifts")]
    [InlineData(typeof(CaregiversController), "SearchClientAssignments", typeof(HttpPostAttribute), "{id:guid}/client-assignments/search")]
    [InlineData(typeof(CaregiversController), "SearchMyClients", typeof(HttpPostAttribute), "me/client-assignments/search")]
    [InlineData(typeof(ClientsController), "GetClients", typeof(HttpGetAttribute), null)]
    [InlineData(typeof(ClientsController), "GetClient", typeof(HttpGetAttribute), "{id:guid}")]
    [InlineData(typeof(ClientsController), "SearchCaregiverAssignments", typeof(HttpPostAttribute), "{id:guid}/caregiver-assignments/search")]
    [InlineData(typeof(ClientsController), "SearchMyCaregivers", typeof(HttpPostAttribute), "me/caregiver-assignments/search")]
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
    [InlineData(typeof(ShiftsController), "GetCoverageQueue", typeof(HttpGetAttribute), "coverage")]
    [InlineData(typeof(ShiftsController), "GetEligibleCaregivers", typeof(HttpGetAttribute), "{id:guid}/eligible-caregivers")]
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
    [InlineData(typeof(AdminUsersController), "GetUsers", typeof(HttpGetAttribute), null)]
    [InlineData(typeof(AdminUsersController), "GetAvailableRoles", typeof(HttpGetAttribute), "roles")]
    [InlineData(typeof(AdminUsersController), "CreateStaffUser", typeof(HttpPostAttribute), null)]
    [InlineData(typeof(AdminUsersController), "UpdateRole", typeof(HttpPutAttribute), "{id:guid}/role")]
    [InlineData(typeof(AdminUsersController), "UpdateStatus", typeof(HttpPutAttribute), "{id:guid}/status")]
    [InlineData(typeof(TransitionsController), "CreateDischargeDocument", typeof(HttpPostAttribute), "documents")]
    [InlineData(typeof(TransitionsController), "GetDischargeDocument", typeof(HttpGetAttribute), "documents/{id:guid}")]
    [InlineData(typeof(TransitionsController), "GetDischargeDocumentContent", typeof(HttpGetAttribute), "documents/{id:guid}/content")]
    [InlineData(typeof(TransitionsController), "GetDischargeDocuments", typeof(HttpGetAttribute), "documents")]
    [InlineData(typeof(TransitionsController), "ExtractDischargeDocument", typeof(HttpPostAttribute), "documents/{id:guid}/extract")]
    [InlineData(typeof(TransitionsController), "GetPlans", typeof(HttpGetAttribute), "plans")]
    [InlineData(typeof(TransitionsController), "GetPlan", typeof(HttpGetAttribute), "plans/{id:guid}")]
    [InlineData(typeof(TransitionsController), "GetPatientPlan", typeof(HttpGetAttribute), "plans/{id:guid}/patient-view")]
    [InlineData(typeof(TransitionsController), "ReviewInstruction", typeof(HttpPutAttribute), "plans/{id:guid}/instructions/{instructionId:guid}")]
    [InlineData(typeof(TransitionsController), "ActivatePlan", typeof(HttpPostAttribute), "plans/{id:guid}/activate")]
    [InlineData(typeof(TransitionsController), "ScheduleReminder", typeof(HttpPostAttribute), "plans/{id:guid}/reminders")]
    [InlineData(typeof(TransitionsController), "GetReminders", typeof(HttpGetAttribute), "plans/{id:guid}/reminders")]
    [InlineData(typeof(TransitionsController), "CreateCheckIn", typeof(HttpPostAttribute), "plans/{id:guid}/check-ins")]
    [InlineData(typeof(TransitionsController), "GetCheckIns", typeof(HttpGetAttribute), "plans/{id:guid}/check-ins")]
    [InlineData(typeof(TransitionsController), "GetEscalations", typeof(HttpGetAttribute), "plans/{id:guid}/escalations")]
    [InlineData(typeof(TransitionsController), "GetEscalationQueue", typeof(HttpGetAttribute), "escalations")]
    [InlineData(typeof(TransitionsController), "AcknowledgeEscalation", typeof(HttpPostAttribute), "escalations/{id:guid}/acknowledge")]
    [InlineData(typeof(TransitionsController), "GetPlanForClient", typeof(HttpGetAttribute), "plans/client/{clientId:guid}")]
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
            typeof(AdminUsersController),
            typeof(TransitionsController),
        };

        // Act
        var returnTypes = controllerTypes
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .Select(method => method.ReturnType.FullName ?? string.Empty)
            .ToArray();

        // Assert
        returnTypes.Should().NotContain(typeName => typeName.Contains("CarePath.Domain.", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(typeof(TransitionPlanPatientFacingDto))]
    [InlineData(typeof(TransitionPlanCareTeamDto))]
    [InlineData(typeof(TransitionInstructionPatientFacingDto))]
    [InlineData(typeof(TransitionCheckInDto))]
    public void TransitionsPatientAndCareTeamDtos_DoNotExposeClinicalSourceOrRawPayloadFields(Type dtoType)
    {
        var propertyNames = dtoType.GetProperties().Select(property => property.Name).ToArray();

        propertyNames.Should().NotContain("RawContent");
        propertyNames.Should().NotContain("SourceText");
        propertyNames.Should().NotContain("ResponsesJson");
        propertyNames.Should().NotContain("ConfidenceScore");
        propertyNames.Should().NotContain("ClinicalNote");
        propertyNames.Should().NotContain("VerifiedBy");
        propertyNames.Should().NotContain("VerifiedAt");
    }
}
