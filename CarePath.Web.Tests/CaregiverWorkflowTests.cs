using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bunit;
using CarePath.Client.Api;
using CarePath.Contracts.Common;
using CarePath.Contracts.Enumerations;
using CarePath.Contracts.Identity;
using CarePath.Contracts.Scheduling;
using CarePath.Web.Pages;
using FluentAssertions;
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

    private static void AddClients(BunitContext context, FakeApiHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.example.test/")
        };
        context.Services.AddSingleton(new CaregiversClient(httpClient));
        context.Services.AddSingleton(new ShiftsClient(httpClient));
    }

    private static PagedResult<CaregiverSummaryDto> CaregiverPage() => new()
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
        PageSize = 10,
        TotalCount = 1,
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
        private readonly Dictionary<string, object> putResponses = new(StringComparer.OrdinalIgnoreCase);

        public List<string> GetRequests { get; } = [];

        public List<(string Path, string Body)> PutRequests { get; } = [];

        public FakeApiHandler Get(string pathPrefix, object response)
        {
            getResponses[pathPrefix] = response;
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
                GetRequests.Add(path.Split('?')[0]);
                return JsonResponse(Match(getResponses, path));
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
}
