using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Bunit;
using CarePath.Client.Api;
using CarePath.Contracts.Admin;
using CarePath.Contracts.Clients;
using CarePath.Contracts.Common;
using CarePath.Contracts.Enumerations;
using CarePath.Contracts.Identity;
using CarePath.Contracts.Scheduling;
using CarePath.Contracts.Transitions;
using CarePath.Web.Pages;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CarePath.Web.Tests;

public sealed class CaregiverWorkflowTests
{
    private static readonly Guid CaregiverId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid UserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid ShiftId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid ClientId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    [Fact]
    public void CaregiversRoster_WhenRendered_UsesWireframeColumnsOnly()
    {
        // Arrange
        using var context = new BunitContext();
        AddClients(context, new FakeApiHandler()
            .Get("api/caregivers", CaregiverPage()));

        // Act
        var component = context.Render<Caregivers>();

        // Assert
        component.WaitForAssertion(() =>
        {
            var headers = component.FindAll("thead th").Select(th => th.TextContent.Trim()).ToArray();
            headers.Should().Equal("Name", "Type", "Rating", "Status", "View");
            component.Markup.Should().NotContain("Pay rate");
            component.Markup.Should().NotContain("Shifts (MTD)");
            component.Markup.Should().NotContain("Certification type");
        });
    }

    [Fact]
    public void CaregiversRoster_WhenNextClicked_RequestsNextPageWithDefaultPageSize()
    {
        // Arrange
        var handler = new FakeApiHandler()
            .Get("api/caregivers", CaregiverPage(totalCount: 12));
        using var context = new BunitContext();
        AddClients(context, handler);

        // Act
        var component = context.Render<Caregivers>();
        component.WaitForElement("button.button-sm:not([disabled])").Click();

        // Assert
        component.WaitForAssertion(() =>
        {
            handler.GetRequestUris.Should().Contain(uri =>
                uri.StartsWith("api/caregivers?", StringComparison.OrdinalIgnoreCase) &&
                uri.Contains("pageNumber=1", StringComparison.OrdinalIgnoreCase) &&
                uri.Contains("pageSize=5", StringComparison.OrdinalIgnoreCase));
            handler.GetRequestUris.Should().Contain(uri =>
                uri.StartsWith("api/caregivers?", StringComparison.OrdinalIgnoreCase) &&
                uri.Contains("pageNumber=2", StringComparison.OrdinalIgnoreCase) &&
                uri.Contains("pageSize=5", StringComparison.OrdinalIgnoreCase));
        });
    }

    [Fact]
    public void CaregiversRoster_WhenViewClicked_LoadsProfilePanel()
    {
        // Arrange
        using var context = new BunitContext();
        AddClients(context, new FakeApiHandler()
            .Get("api/caregivers", CaregiverPage())
            .Get($"api/caregivers/{CaregiverId}", CaregiverDetail()));
        var component = context.Render<Caregivers>();

        // Act
        component.WaitForElement("button.text-link").Click();

        // Assert
        component.WaitForAssertion(() =>
        {
            component.Markup.Should().Contain("Profile detail");
            component.Markup.Should().Contain("Pay rate");
            component.Markup.Should().Contain("18.50");
            component.Markup.Should().Contain("Shifts (MTD)");
            component.Markup.Should().Contain("3 checked in");
            component.Markup.Should().Contain("Billable hours (MTD)");
        });
    }

    [Fact]
    public void AddCaregiverStep1_WhenRendered_ExcludesCertificationFields()
    {
        // Arrange
        using var context = new BunitContext();
        AddClients(context, new FakeApiHandler());

        // Act
        var component = context.Render<CaregiverCreate>();

        // Assert
        component.Markup.Should().Contain("Temporary password");
        component.Markup.Should().Contain("Hourly pay rate");
        component.Markup.Should().NotContain("Certification type");
        component.Markup.Should().NotContain("Issuing authority");
    }

    [Fact]
    public void Clients_WhenAddClientClicked_NavigatesToCreateClient()
    {
        // Arrange
        using var context = new BunitContext();
        AddClients(context, new FakeApiHandler()
            .Get("api/clients", ClientPage()));

        // Act
        var component = context.Render<Clients>();
        component.WaitForElement("button.button-primary").Click();

        // Assert
        context.Services.GetRequiredService<NavigationManager>().Uri.Should().EndWith("/clients/create");
    }

    [Fact]
    public void Clients_WhenNextClicked_RequestsNextPageWithDefaultPageSize()
    {
        // Arrange
        var handler = new FakeApiHandler()
            .Get("api/clients", ClientPage(totalCount: 12));
        using var context = new BunitContext();
        AddClients(context, handler);

        // Act
        var component = context.Render<Clients>();
        component.WaitForElement("button.button-sm:not([disabled])").Click();

        // Assert
        component.WaitForAssertion(() =>
        {
            handler.GetRequestUris.Should().Contain(uri =>
                uri.StartsWith("api/clients?", StringComparison.OrdinalIgnoreCase) &&
                uri.Contains("pageNumber=1", StringComparison.OrdinalIgnoreCase) &&
                uri.Contains("pageSize=5", StringComparison.OrdinalIgnoreCase));
            handler.GetRequestUris.Should().Contain(uri =>
                uri.StartsWith("api/clients?", StringComparison.OrdinalIgnoreCase) &&
                uri.Contains("pageNumber=2", StringComparison.OrdinalIgnoreCase) &&
                uri.Contains("pageSize=5", StringComparison.OrdinalIgnoreCase));
        });
    }

    [Fact]
    public void ClientCreate_WhenCreateClicked_CallsClientsCreateAndNavigatesToClients()
    {
        // Arrange
        var handler = new FakeApiHandler()
            .Post("api/clients", ClientDetail());
        using var context = new BunitContext();
        AddClients(context, handler);
        var component = context.Render<ClientCreate>();

        // Act
        component.Find("input[type='email']").Change("jordan.mitchell@example.test");
        component.Find("button.button-primary").Click();

        // Assert
        component.WaitForAssertion(() =>
        {
            handler.PostRequests.Should().ContainSingle(request => request.Path == "api/clients");
            handler.PostRequests.Single().Body.Should().Contain("\"email\":\"jordan.mitchell@example.test\"");
            PostedDateTime(handler.PostRequests.Single().Body, "dateOfBirth").Should().EndWith("Z");
            context.Services.GetRequiredService<NavigationManager>().Uri.Should().EndWith("/clients");
        });
    }

    [Fact]
    public void ClientCreate_WhenCreateFails_RendersApiErrorAlert()
    {
        // Arrange
        var handler = new FakeApiHandler()
            .Post("api/clients", new ApiProblemDetails
            {
                Title = "Validation failed",
                Status = 400,
                ValidationErrors =
                [
                    new ValidationError("Email", "Email is required.", "client.email_required")
                ],
            });
        using var context = new BunitContext();
        AddClients(context, handler);
        var component = context.Render<ClientCreate>();

        // Act
        component.Find("button.button-primary").Click();

        // Assert
        component.WaitForAssertion(() =>
        {
            component.Find("[role='alert']").TextContent.Should().Contain("Email is required.");
            context.Services.GetRequiredService<NavigationManager>().Uri.Should().EndWith("/");
        });
    }

    [Fact]
    public void AddCertificationsStep2_WhenRendered_ShowsSavedListAndMultiCertActions()
    {
        // Arrange
        using var context = new BunitContext();
        AddClients(context, new FakeApiHandler()
            .Get($"api/caregivers/{CaregiverId}", CaregiverDetailWithCertifications()));

        // Act
        var component = context.Render<CaregiverCertifications>(parameters =>
            parameters.Add(p => p.CaregiverId, CaregiverId));

        // Assert
        component.WaitForAssertion(() =>
        {
            component.Markup.Should().Contain("2 saved");
            component.Markup.Should().Contain("Saved certifications");
            component.Markup.Should().Contain("Save and add another");
            component.Markup.Should().Contain("Continue to eligible shifts");
            component.Markup.Should().Contain("Back");
        });
    }

    [Fact]
    public void EligibleShiftsStep3_WhenAssignClicked_UsesShiftUpdatePath()
    {
        // Arrange
        var handler = new FakeApiHandler()
            .Get($"api/caregivers/{CaregiverId}/eligible-shifts", EligibleShiftPage())
            .Put($"api/shifts/{ShiftId}", new ShiftDetailDto { Id = ShiftId, CaregiverId = CaregiverId });
        using var context = new BunitContext();
        AddClients(context, handler);
        var component = context.Render<CaregiverEligibleShifts>(parameters =>
            parameters.Add(p => p.CaregiverId, CaregiverId));

        // Act
        component.WaitForElement("button.text-link").Click();

        // Assert
        component.WaitForAssertion(() =>
        {
            handler.PutRequests.Should().ContainSingle(request => request.Path == $"api/shifts/{ShiftId}");
            handler.PutRequests.Single().Body.Should().Contain(CaregiverId.ToString());
        });
    }

    [Fact]
    public void ShiftAssign_WhenAssignClicked_LoadsShiftByIdAndUsesShiftUpdatePath()
    {
        // Arrange
        var handler = new FakeApiHandler()
            .Get($"api/shifts/{ShiftId}", ShiftDetail())
            .Get($"api/shifts/{ShiftId}/eligible-caregivers", EligibleCaregiverPage())
            .Put($"api/shifts/{ShiftId}", new ShiftDetailDto { Id = ShiftId, CaregiverId = CaregiverId });
        using var context = new BunitContext();
        AddClients(context, handler);
        var component = context.Render<ShiftAssign>(parameters =>
            parameters.Add(p => p.ShiftId, ShiftId));

        // Act
        component.WaitForElement("button.text-link").Click();
        component.Find("button.button-primary").Click();

        // Assert
        component.WaitForAssertion(() =>
        {
            handler.GetRequests.Should().Contain(path => path.StartsWith($"api/shifts/{ShiftId}", StringComparison.OrdinalIgnoreCase));
            handler.PutRequests.Should().ContainSingle(request => request.Path == $"api/shifts/{ShiftId}");
            handler.PutRequests.Single().Body.Should().Contain(CaregiverId.ToString());
        });
    }

    [Fact]
    public void ShiftCreate_WhenCreateOpenShiftClicked_CallsCreateWithNullCaregiverAndNavigatesToSchedule()
    {
        // Arrange
        var handler = new FakeApiHandler()
            .Get("api/clients", ClientPage())
            .Post("api/shifts", ShiftDetail());
        using var context = new BunitContext();
        AddClients(context, handler);
        var component = context.Render<ShiftCreate>();

        // Act
        component.WaitForElement("select#shift-client");
        component.FindAll("button").Single(button => button.TextContent.Contains("Create open shift")).Click();

        // Assert
        component.WaitForAssertion(() =>
        {
            handler.PostRequests.Should().ContainSingle(request => request.Path == "api/shifts");
            handler.PostRequests.Single().Body.Should().Contain("\"caregiverId\":null");
            PostedDateTime(handler.PostRequests.Single().Body, "scheduledStartUtc").Should().EndWith("Z");
            PostedDateTime(handler.PostRequests.Single().Body, "scheduledEndUtc").Should().EndWith("Z");
            context.Services.GetRequiredService<NavigationManager>().Uri.Should().EndWith("/schedule");
        });
    }

    [Fact]
    public void ShiftCreate_WhenCreateAndAssignClicked_CreatesOpenShiftThenNavigatesToAssignPage()
    {
        // Arrange
        var handler = new FakeApiHandler()
            .Get("api/clients", ClientPage())
            .Post("api/shifts", ShiftDetail());
        using var context = new BunitContext();
        AddClients(context, handler);
        var component = context.Render<ShiftCreate>();

        // Act
        component.WaitForElement("select#shift-client");
        component.FindAll("button").Single(button => button.TextContent.Contains("Create & assign shift")).Click();

        // Assert
        component.WaitForAssertion(() =>
        {
            handler.PostRequests.Should().ContainSingle(request => request.Path == "api/shifts");
            handler.PostRequests.Single().Body.Should().Contain("\"caregiverId\":null");
            context.Services.GetRequiredService<NavigationManager>().Uri.Should().EndWith($"/schedule/assign/{ShiftId}");
        });
    }

    [Fact]
    public void ShiftCreate_WhenCreateFails_RendersApiErrorAlert()
    {
        // Arrange
        var handler = new FakeApiHandler()
            .Get("api/clients", ClientPage())
            .Post("api/shifts", new ApiProblemDetails
            {
                Title = "Validation failed",
                Status = 400,
                ValidationErrors =
                [
                    new ValidationError("ScheduledEndUtc", "End must be after start.", "shift.end_before_start")
                ],
            });
        using var context = new BunitContext();
        AddClients(context, handler);
        var component = context.Render<ShiftCreate>();

        // Act
        component.WaitForElement("select#shift-client");
        component.FindAll("button").Single(button => button.TextContent.Contains("Create open shift")).Click();

        // Assert
        component.WaitForAssertion(() =>
        {
            component.Find("[role='alert']").TextContent.Should().Contain("End must be after start.");
        });
    }

    [Fact]
    public void ShiftCreate_WhenRendered_DoesNotRenderRateValuesOutsideInputs()
    {
        // Arrange
        var handler = new FakeApiHandler()
            .Get("api/clients", ClientPage());
        using var context = new BunitContext();
        AddClients(context, handler);

        // Act
        var component = context.Render<ShiftCreate>();

        // Assert
        component.WaitForAssertion(() =>
        {
            var nonInputText = string.Join(
                " ",
                component.FindAll("body *")
                    .Where(element => !string.Equals(element.TagName, "input", StringComparison.OrdinalIgnoreCase))
                    .Select(element => element.TextContent));
            nonInputText.Should().NotContain("32.00");
            nonInputText.Should().NotContain("18.50");
            component.Find("input#shift-bill-rate").GetAttribute("value").Should().Be("32.00");
            component.Find("input#shift-pay-rate").GetAttribute("value").Should().Be("18.50");
        });
    }

    [Fact]
    public void Home_WhenCreateShiftClicked_NavigatesToCreateShift()
    {
        // Arrange
        using var context = new BunitContext();
        AddClients(context, new FakeApiHandler()
            .Get("api/shifts/coverage", EmptyCoveragePage())
            .Get("api/shifts", EmptyShiftPage()));

        // Act
        var component = context.Render<Home>();
        component.Find("button.button-primary").Click();

        // Assert
        context.Services.GetRequiredService<NavigationManager>().Uri.Should().EndWith("/shifts/create");
    }

    [Fact]
    public void Home_WhenAddClientQuickActionClicked_NavigatesToCreateClient()
    {
        // Arrange
        using var context = new BunitContext();
        AddClients(context, new FakeApiHandler()
            .Get("api/shifts/coverage", EmptyCoveragePage())
            .Get("api/shifts", EmptyShiftPage()));

        // Act
        var component = context.Render<Home>();
        component.FindAll("button").Single(button => button.TextContent.Contains("Add client")).Click();

        // Assert
        context.Services.GetRequiredService<NavigationManager>().Uri.Should().EndWith("/clients/create");
    }

    [Fact]
    public void Home_WhenRendered_UsesShiftAndCoverageDataForOverview()
    {
        // Arrange
        var handler = new FakeApiHandler()
            .Get("api/shifts/coverage", CoveragePage(
                CoverageShift(dayOffset: 0, startHour: DateTime.UtcNow.ToLocalTime().Hour + 1, clientName: "Harborview Center"),
                CoverageShift(dayOffset: 0, startHour: 14, clientName: "Northside Rehab"),
                CoverageShift(dayOffset: 1, startHour: 8, clientName: "Tomorrow Center"),
                CoverageShift(dayOffset: 2, startHour: 8, clientName: "Weekend Center")))
            .Get("api/shifts", ShiftPage(
                BoardShift(dayOffset: 0, startHour: 8, clientName: "Jordan M."),
                BoardShift(dayOffset: 0, startHour: 10, clientName: "Casey R."),
                BoardShift(dayOffset: 0, startHour: 13, clientName: "Harborview Center", caregiverName: null)));
        using var context = new BunitContext();
        AddClients(context, handler);

        // Act
        var component = context.Render<Home>();

        // Assert
        component.WaitForAssertion(() =>
        {
            component.Markup.Should().Contain("Shifts today");
            component.Markup.Should().Contain(">3<");
            component.Markup.Should().Contain("2 staffed");
            component.Markup.Should().Contain("1 open");
            component.Markup.Should().Contain("Jordan M.");
            component.Markup.Should().Contain("Casey R.");
            component.Markup.Should().Contain("Harborview Center");
            component.Markup.Should().Contain("4 shifts need coverage");
            component.Markup.Should().Contain("9 items");
        });
    }

    [Fact]
    public void Schedule_WhenNewShiftClicked_NavigatesToCreateShift()
    {
        // Arrange
        var handler = new FakeApiHandler()
            .Get("api/shifts/coverage", EmptyCoveragePage())
            .Get("api/shifts", EmptyShiftPage());
        using var context = new BunitContext();
        AddClients(context, handler);

        // Act
        var component = context.Render<Schedule>();
        component.WaitForElement("button.button-primary").Click();

        // Assert
        context.Services.GetRequiredService<NavigationManager>().Uri.Should().EndWith("/shifts/create");
    }

    [Fact]
    public void Schedule_WhenShiftInVisibleWeek_RendersInCorrectDayAndTimeCell()
    {
        // Arrange
        var handler = new FakeApiHandler()
            .Get("api/shifts/coverage", EmptyCoveragePage())
            .Get("api/shifts", ShiftPage(BoardShift(dayOffset: 2, startHour: 10, clientName: "Northside Rehab")));
        using var context = new BunitContext();
        AddClients(context, handler);

        // Act
        var component = context.Render<Schedule>();

        // Assert
        component.WaitForAssertion(() =>
        {
            var cell = component.Find("[data-day-index='2'][data-hour='10']");
            cell.TextContent.Should().Contain("Northside Rehab");
            cell.TextContent.Should().Contain("Amara Williams");
        });
    }

    [Fact]
    public void Schedule_WhenShiftIsOpen_RendersOpenStyling()
    {
        // Arrange
        var handler = new FakeApiHandler()
            .Get("api/shifts/coverage", EmptyCoveragePage())
            .Get("api/shifts", ShiftPage(BoardShift(
                dayOffset: 1,
                startHour: 13,
                clientName: "Harborview",
                caregiverId: null,
                caregiverName: null,
                serviceType: ServiceType.FacilityStaffing)));
        using var context = new BunitContext();
        AddClients(context, handler);

        // Act
        var component = context.Render<Schedule>();

        // Assert
        component.WaitForAssertion(() =>
        {
            var button = component.Find("button.schedule-shift-block");
            button.GetAttribute("class").Should().Contain("shift-open");
            button.TextContent.Should().Contain("Harborview - Unassigned");
        });
    }

    [Fact]
    public void Schedule_WhenListViewClicked_RendersWeeklyShiftListAndCanReturnToCalendar()
    {
        // Arrange
        var handler = new FakeApiHandler()
            .Get("api/shifts/coverage", EmptyCoveragePage())
            .Get("api/shifts", ShiftPage(
                BoardShift(dayOffset: 0, startHour: 8, clientName: "Jordan M."),
                BoardShift(dayOffset: 1, startHour: 13, clientName: "Harborview", caregiverName: null)));
        using var context = new BunitContext();
        AddClients(context, handler);
        var component = context.Render<Schedule>();

        // Act
        component.FindAll("button").Single(button => button.TextContent.Contains("List view")).Click();

        // Assert
        component.WaitForAssertion(() =>
        {
            component.Markup.Should().Contain("Weekly shifts");
            var headers = component.FindAll("section[aria-labelledby='schedule-list-heading'] thead th")
                .Select(header => header.TextContent.Trim())
                .ToArray();
            headers.Should().Equal("Shift", "When", "Caregiver", "Service", "Status", string.Empty);
            component.Markup.Should().Contain("Jordan M.");
            component.Markup.Should().Contain("Harborview");
            component.Markup.Should().Contain("Unassigned");
            component.Markup.Should().NotContain("32.00");
            component.Markup.Should().NotContain("18.50");
        });

        component.FindAll("button").Single(button => button.TextContent.Contains("Calendar view")).Click();
        component.WaitForAssertion(() => component.Find(".calendar-frame").Should().NotBeNull());
    }

    [Fact]
    public void Schedule_WhenShiftFocused_RendersDetailCardWithoutRates()
    {
        // Arrange
        var handler = new FakeApiHandler()
            .Get("api/shifts/coverage", EmptyCoveragePage())
            .Get("api/shifts", ShiftPage(BoardShift(dayOffset: 0, startHour: 8, endHour: 12)));
        using var context = new BunitContext();
        AddClients(context, handler);
        var component = context.Render<Schedule>();

        // Act
        component.WaitForElement("button.schedule-shift-block").Focus();

        // Assert
        component.WaitForAssertion(() =>
        {
            var tooltip = component.Find("[role='tooltip']");
            tooltip.TextContent.Should().Contain("Jordan M.");
            tooltip.TextContent.Should().Contain("Amara Williams");
            tooltip.TextContent.Should().Contain("8:00 AM");
            tooltip.TextContent.Should().Contain("12:00 PM");
            tooltip.TextContent.Should().Contain("4 h");
            tooltip.TextContent.Should().Contain("In-home care");
            tooltip.TextContent.Should().Contain("Scheduled");
            tooltip.TextContent.Should().NotContain("32.00");
            tooltip.TextContent.Should().NotContain("18.50");
            component.Find("button.schedule-shift-block").GetAttribute("aria-describedby").Should().StartWith("shift-detail-");
        });
    }

    [Fact]
    public void Schedule_WhenNextWeekClicked_RequestsNewVisibleRange()
    {
        // Arrange
        var handler = new FakeApiHandler()
            .Get("api/shifts/coverage", EmptyCoveragePage())
            .Get("api/shifts", EmptyShiftPage());
        using var context = new BunitContext();
        AddClients(context, handler);
        var component = context.Render<Schedule>();

        // Act
        component.WaitForElement("button[aria-label='Next week']").Click();

        // Assert
        component.WaitForAssertion(() =>
        {
            var shiftRequests = handler.GetRequestUris
                .Where(path => path.StartsWith("api/shifts?", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            shiftRequests.Should().HaveCountGreaterThanOrEqualTo(2);
            shiftRequests[0].Should().Contain("fromUtc=");
            shiftRequests[0].Should().Contain("toUtc=");
            shiftRequests[1].Should().Contain("fromUtc=");
            shiftRequests[1].Should().Contain("toUtc=");
            shiftRequests[1].Should().NotBe(shiftRequests[0]);
        });
    }

    [Fact]
    public void Schedule_WhenShiftCancelled_DoesNotRenderOnBoard()
    {
        // Arrange
        var handler = new FakeApiHandler()
            .Get("api/shifts/coverage", EmptyCoveragePage())
            .Get("api/shifts", ShiftPage(
                BoardShift(dayOffset: 0, startHour: 8, clientName: "Cancelled Client", status: ShiftStatus.Cancelled),
                BoardShift(dayOffset: 0, startHour: 8, clientName: "Active Client")));
        using var context = new BunitContext();
        AddClients(context, handler);

        // Act
        var component = context.Render<Schedule>();

        // Assert
        component.WaitForAssertion(() =>
        {
            component.Markup.Should().NotContain("Cancelled Client");
            component.Markup.Should().Contain("Active Client");
        });
    }

    [Fact]
    public void UserManagement_WhenNextClicked_RequestsNextPageWithDefaultPageSize()
    {
        // Arrange
        var handler = new FakeApiHandler()
            .Get("api/admin/users/roles", new[] { UserRole.Admin, UserRole.Coordinator })
            .Get("api/admin/users", UserAccountPage(totalCount: 12));
        using var context = new BunitContext();
        AddClients(context, handler);

        // Act
        var component = context.Render<UserManagement>();
        component.WaitForElement("button.button-sm:not([disabled])").Click();

        // Assert
        component.WaitForAssertion(() =>
        {
            handler.GetRequestUris.Should().Contain(uri =>
                uri.StartsWith("api/admin/users?", StringComparison.OrdinalIgnoreCase) &&
                uri.Contains("pageNumber=1", StringComparison.OrdinalIgnoreCase) &&
                uri.Contains("pageSize=5", StringComparison.OrdinalIgnoreCase));
            handler.GetRequestUris.Should().Contain(uri =>
                uri.StartsWith("api/admin/users?", StringComparison.OrdinalIgnoreCase) &&
                uri.Contains("pageNumber=2", StringComparison.OrdinalIgnoreCase) &&
                uri.Contains("pageSize=5", StringComparison.OrdinalIgnoreCase));
        });
    }

    [Fact]
    public void TransitionsReview_WhenNextClicked_RequestsNextPageWithDefaultPageSize()
    {
        // Arrange
        var handler = new FakeApiHandler()
            .Get("api/transitions/plans", TransitionPlanPage(totalCount: 12));
        using var context = new BunitContext();
        AddClients(context, handler);

        // Act
        var component = context.Render<TransitionsReview>();
        component.WaitForElement("button.button-sm:not([disabled])").Click();

        // Assert
        component.WaitForAssertion(() =>
        {
            handler.GetRequestUris.Should().Contain(uri =>
                uri.StartsWith("api/transitions/plans?", StringComparison.OrdinalIgnoreCase) &&
                uri.Contains("pageNumber=1", StringComparison.OrdinalIgnoreCase) &&
                uri.Contains("pageSize=5", StringComparison.OrdinalIgnoreCase));
            handler.GetRequestUris.Should().Contain(uri =>
                uri.StartsWith("api/transitions/plans?", StringComparison.OrdinalIgnoreCase) &&
                uri.Contains("pageNumber=2", StringComparison.OrdinalIgnoreCase) &&
                uri.Contains("pageSize=5", StringComparison.OrdinalIgnoreCase));
        });
    }

    private static void AddClients(BunitContext context, FakeApiHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.example.test/")
        };
        context.Services.AddSingleton(new CaregiversClient(httpClient));
        context.Services.AddSingleton(new ClientsClient(httpClient));
        context.Services.AddSingleton(new ShiftsClient(httpClient));
        context.Services.AddSingleton(new AdminUsersClient(httpClient));
        context.Services.AddSingleton(new TransitionsClient(httpClient));
        context.Services.AddAuthorizationCore();
        context.Services.AddSingleton<AuthenticationStateProvider>(new TestAuthStateProvider());
    }

    private static PagedResult<ClientSummaryDto> ClientPage(int totalCount = 1) => new()
    {
        Items =
        [
            new ClientSummaryDto
            {
                Id = ClientId,
                UserId = UserId,
                FullName = "Jordan M.",
                ServiceType = ServiceType.InHomeCare,
                Age = 72,
                IsActive = true,
            }
        ],
        PageNumber = 1,
        PageSize = 5,
        TotalCount = totalCount,
    };

    private static ClientDetailDto ClientDetail() => new()
    {
        Id = ClientId,
        UserId = UserId,
        FullName = "Jordan Mitchell",
        PhoneNumber = "410-555-0199",
        DateOfBirth = new DateTime(1951, 6, 2, 0, 0, 0, DateTimeKind.Utc),
        Age = 75,
        ServiceType = ServiceType.InHomeCare,
        EstimatedWeeklyHours = 40,
    };

    private static string PostedDateTime(string body, string propertyName)
    {
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty(propertyName).GetString() ?? string.Empty;
    }

    private static PagedResult<ShiftSummaryDto> EmptyShiftPage() => new()
    {
        Items = [],
        PageNumber = 1,
        PageSize = 100,
        TotalCount = 0,
    };

    private static PagedResult<ShiftSummaryDto> ShiftPage(params ShiftSummaryDto[] shifts) => new()
    {
        Items = shifts,
        PageNumber = 1,
        PageSize = 100,
        TotalCount = shifts.Length,
    };

    private static ShiftSummaryDto BoardShift(
        int dayOffset,
        int startHour,
        int? endHour = null,
        string clientName = "Jordan M.",
        Guid? caregiverId = null,
        string? caregiverName = "Amara Williams",
        ServiceType serviceType = ServiceType.InHomeCare,
        ShiftStatus status = ShiftStatus.Scheduled)
    {
        var resolvedCaregiverId = caregiverName is null ? caregiverId : caregiverId ?? CaregiverId;
        return new ShiftSummaryDto
        {
            Id = Guid.NewGuid(),
            ClientId = ClientId,
            ClientFullName = clientName,
            CaregiverId = resolvedCaregiverId,
            CaregiverFullName = caregiverName,
            ScheduledStartTime = BoardDateUtc(dayOffset, startHour),
            ScheduledEndTime = BoardDateUtc(dayOffset, endHour ?? startHour + 4),
            Status = status,
            ServiceType = serviceType,
        };
    }

    private static DateTime BoardDateUtc(int dayOffset, int hour)
    {
        var local = CurrentWeekStartLocal().AddDays(dayOffset).AddHours(hour);
        return DateTime.SpecifyKind(local, DateTimeKind.Local).ToUniversalTime();
    }

    private static DateTime CurrentWeekStartLocal()
    {
        var date = DateTime.UtcNow.ToLocalTime().Date;
        var offset = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-offset);
    }

    private static PagedResult<OpenShiftCoverageDto> EmptyCoveragePage() => new()
    {
        Items = [],
        PageNumber = 1,
        PageSize = 20,
        TotalCount = 0,
    };

    private static PagedResult<OpenShiftCoverageDto> CoveragePage(params OpenShiftCoverageDto[] shifts) => new()
    {
        Items = shifts,
        PageNumber = 1,
        PageSize = 100,
        TotalCount = shifts.Length,
    };

    private static OpenShiftCoverageDto CoverageShift(int dayOffset, int startHour, string clientName) => new()
    {
        ShiftId = Guid.NewGuid(),
        ClientId = ClientId,
        ClientDisplayName = clientName,
        ScheduledStartTime = BoardDateUtc(dayOffset, Math.Clamp(startHour, 0, 23)),
        ScheduledEndTime = BoardDateUtc(dayOffset, Math.Clamp(startHour + 4, 1, 23)),
        ServiceType = ServiceType.FacilityStaffing,
        Status = ShiftStatus.Scheduled,
        RequirementLabels = ["Coverage needed"],
        BestMatches = [],
    };

    private static PagedResult<CaregiverSummaryDto> CaregiverPage(int totalCount = 1) => new()
    {
        Items =
        [
            new CaregiverSummaryDto
            {
                Id = CaregiverId,
                UserId = UserId,
                FullName = "Amara Williams",
                EmploymentType = EmploymentType.W2Employee,
                AverageRating = 4.8m,
                IsActive = true,
            }
        ],
        PageNumber = 1,
        PageSize = 5,
        TotalCount = totalCount,
    };

    private static PagedResult<UserAccountDto> UserAccountPage(int totalCount = 1) => new()
    {
        Items =
        [
            new UserAccountDto
            {
                Id = UserId,
                Email = "admin@example.test",
                DisplayName = "Demo Administrator",
                Role = UserRole.Admin,
                IsActive = true,
                CanChangeRole = true,
                CanDeactivate = true,
            }
        ],
        PageNumber = 1,
        PageSize = 5,
        TotalCount = totalCount,
    };

    private static PagedResult<TransitionPlanSummaryDto> TransitionPlanPage(int totalCount = 1) => new()
    {
        Items =
        [
            new TransitionPlanSummaryDto
            {
                Id = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
                ClientId = ClientId,
                ClientFullName = "Jordan M.",
                HospitalName = "Harborview",
                DischargeDate = new DateTime(2026, 7, 8, 0, 0, 0, DateTimeKind.Utc),
                TransitionWindowEnd = new DateTime(2026, 8, 7, 0, 0, 0, DateTimeKind.Utc),
                Status = TransitionPlanStatus.Active,
                RiskLevel = TransitionRiskLevel.Medium,
                DaysRemaining = 28,
                PendingInstructionCount = 0,
                OpenEscalationCount = 0,
            }
        ],
        PageNumber = 1,
        PageSize = 5,
        TotalCount = totalCount,
    };

    private static CaregiverDetailDto CaregiverDetail() => new()
    {
        Id = CaregiverId,
        UserId = UserId,
        FullName = "Amara Williams",
        Email = "amara.williams@example.test",
        PhoneNumber = "410-555-0148",
        EmploymentType = EmploymentType.W2Employee,
        IsActive = true,
        HourlyPayRate = 18.50m,
        HireDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc),
        HasDementiaCare = true,
        HasMobilityAssistance = true,
        HasMedicationManagement = true,
        AvailableWeekdays = true,
        AvailableWeekends = true,
        MaxWeeklyHours = 40,
        AverageRating = 4.8m,
        ShiftsMtd = 3,
        BillableHoursMtd = 12m,
        TotalShiftsCompleted = 128,
        NoShowCount = 0,
    };

    private static CaregiverDetailDto CaregiverDetailWithCertifications()
    {
        return new CaregiverDetailDto
        {
            Id = CaregiverId,
            UserId = UserId,
            FullName = "Amara Williams",
            Email = "amara.williams@example.test",
            PhoneNumber = "410-555-0148",
            EmploymentType = EmploymentType.W2Employee,
            IsActive = true,
            HourlyPayRate = 18.50m,
            HireDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            HasDementiaCare = true,
            HasMobilityAssistance = true,
            HasMedicationManagement = true,
            AvailableWeekdays = true,
            AvailableWeekends = true,
            MaxWeeklyHours = 40,
            AverageRating = 4.8m,
            ShiftsMtd = 3,
            BillableHoursMtd = 12m,
            TotalShiftsCompleted = 128,
            NoShowCount = 0,
            Certifications =
            [
                CertificationDto(CertificationType.CNA, "MD-CNA-20481"),
                CertificationDto(CertificationType.CPR, "CPR-1000"),
            ]
        };
    }

    private static CertificationDto CertificationDto(CertificationType type, string number) => new()
    {
        Id = Guid.NewGuid(),
        CaregiverId = CaregiverId,
        Type = type,
        CertificationNumber = number,
        IssueDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        ExpirationDate = new DateTime(2028, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        IssuingAuthority = "Synthetic Training Authority",
    };

    private static PagedResult<EligibleOpenShiftDto> EligibleShiftPage() => new()
    {
        Items =
        [
            new EligibleOpenShiftDto
            {
                ShiftId = ShiftId,
                ClientId = ClientId,
                ClientDisplayName = "Jordan M.",
                ScheduledStartTime = new DateTime(2026, 7, 10, 9, 0, 0, DateTimeKind.Utc),
                ScheduledEndTime = new DateTime(2026, 7, 10, 13, 0, 0, DateTimeKind.Utc),
                BreakMinutes = 0,
                ServiceType = ServiceType.InHomeCare,
                Status = ShiftStatus.Scheduled,
                RequirementLabels = ["Valid HHA/CNA/GNA/LPN/RN credential"],
                IsAssignable = true,
                MatchReasons = ["Credential fit"],
            }
        ],
        PageNumber = 1,
        PageSize = 20,
        TotalCount = 1,
    };

    private static ShiftDetailDto ShiftDetail() => new()
    {
        Id = ShiftId,
        ClientId = ClientId,
        ClientFullName = "Jordan M.",
        ScheduledStartTime = new DateTime(2026, 7, 10, 9, 0, 0, DateTimeKind.Utc),
        ScheduledEndTime = new DateTime(2026, 7, 10, 13, 0, 0, DateTimeKind.Utc),
        BreakMinutes = 0,
        Status = ShiftStatus.Scheduled,
        ServiceType = ServiceType.InHomeCare,
    };

    private static PagedResult<EligibleCaregiverDto> EligibleCaregiverPage() => new()
    {
        Items =
        [
            new EligibleCaregiverDto
            {
                CaregiverId = CaregiverId,
                FullName = "Amara Williams",
                EmploymentType = EmploymentType.W2Employee,
                AverageRating = 4.8m,
                ShiftsMtd = 3,
                IsAssignable = true,
                MatchReasons = ["Credential fit"],
            }
        ],
        PageNumber = 1,
        PageSize = 20,
        TotalCount = 1,
    };

    private sealed class FakeApiHandler : HttpMessageHandler
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private readonly Dictionary<string, object> getResponses = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, object> postResponses = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, object> putResponses = new(StringComparer.OrdinalIgnoreCase);

        public List<string> GetRequests { get; } = [];

        public List<string> GetRequestUris { get; } = [];

        public List<(string Path, string Body)> PostRequests { get; } = [];

        public List<(string Path, string Body)> PutRequests { get; } = [];

        public FakeApiHandler Get(string pathPrefix, object response)
        {
            getResponses[pathPrefix] = response;
            return this;
        }

        public FakeApiHandler Post(string pathPrefix, object response)
        {
            postResponses[pathPrefix] = response;
            return this;
        }

        public FakeApiHandler Put(string pathPrefix, object response)
        {
            putResponses[pathPrefix] = response;
            return this;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.PathAndQuery.TrimStart('/') ?? string.Empty;
            if (request.Method == HttpMethod.Get)
            {
                GetRequestUris.Add(path);
                GetRequests.Add(path.Split('?')[0]);
                return JsonResponse(Match(getResponses, path));
            }

            if (request.Method == HttpMethod.Post)
            {
                var body = request.Content is null
                    ? string.Empty
                    : await request.Content.ReadAsStringAsync(cancellationToken);
                PostRequests.Add((path.Split('?')[0], body));
                return JsonResponse(Match(postResponses, path));
            }

            if (request.Method == HttpMethod.Put)
            {
                var body = request.Content is null
                    ? string.Empty
                    : await request.Content.ReadAsStringAsync(cancellationToken);
                PutRequests.Add((path.Split('?')[0], body));
                return JsonResponse(Match(putResponses, path));
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        private static object Match(Dictionary<string, object> responses, string path)
        {
            foreach (var (prefix, response) in responses.OrderByDescending(pair => pair.Key.Length))
            {
                if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return response;
                }
            }

            return new ApiProblemDetails
            {
                Title = "Not found",
                Status = 404,
            };
        }

        private static HttpResponseMessage JsonResponse(object payload)
        {
            var statusCode = payload is ApiProblemDetails problem
                ? (HttpStatusCode)problem.Status
                : HttpStatusCode.OK;
            var response = new HttpResponseMessage(statusCode)
            {
                Content = JsonContent.Create(payload, options: JsonOptions),
            };
            return response;
        }
    }

    private sealed class TestAuthStateProvider : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var identity = new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.Name, "Test Admin"),
                    new Claim(ClaimTypes.Role, "Admin"),
                    new Claim(ClaimTypes.Role, "Coordinator"),
                    new Claim(ClaimTypes.Role, "Clinician")
                ],
                "Test");
            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
        }
    }
}
