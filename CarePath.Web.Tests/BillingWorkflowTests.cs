using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Bunit;
using CarePath.Client.Api;
using CarePath.Contracts.Billing;
using CarePath.Contracts.Clients;
using CarePath.Contracts.Common;
using CarePath.Contracts.Enumerations;
using CarePath.Web.Pages;
using CarePath.Web.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace CarePath.Web.Tests;

public sealed class BillingWorkflowTests
{
    private static readonly Guid ClientId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ShiftId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid InvoiceId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Theory]
    [InlineData(typeof(BillingGenerate))]
    [InlineData(typeof(BillingReconciliation))]
    public void BillingWorkflowPage_Authorization_IsAdminCoordinatorOnly(Type pageType)
    {
        var attribute = pageType.GetCustomAttributes<AuthorizeAttribute>().Single(item => !string.IsNullOrWhiteSpace(item.Roles));

        attribute.Roles.Should().Be("Admin,Coordinator");
    }

    [Fact]
    public void Billing_WhenGenerateClicked_NavigatesToGenerateWorkflow()
    {
        using var context = CreateContext(new BillingApiHandler().Get("api/invoices", InvoicePage()));
        var component = context.Render<Billing>();

        component.WaitForElement("button.button-primary").Click();

        context.Services.GetRequiredService<NavigationManager>().Uri.Should().EndWith("/billing/generate");
    }

    [Fact]
    public void GenerateInvoice_WhenPreviewed_PostsBodyAndRendersMinimumNecessaryRows()
    {
        var handler = new BillingApiHandler()
            .Get("api/clients", ClientPage())
            .Post("api/invoices/preview", Preview());
        using var context = CreateContext(handler);
        var component = context.Render<BillingGenerate>();

        component.WaitForElement("#invoice-client");
        component.FindAll("button").Single(button => button.TextContent.Contains("Preview billable shifts")).Click();

        component.WaitForAssertion(() =>
        {
            handler.PostRequests.Should().ContainSingle(request => request.Path == "api/invoices/preview");
            handler.PostRequests[0].Body.Should().Contain(ClientId.ToString());
            handler.PostRequests[0].Body.Should().Contain("\"serviceType\":1");
            component.Markup.Should().Contain("Casey Morgan");
            component.Markup.Should().Contain("RN");
            component.Markup.Should().Contain("$262.50");
            component.Markup.Should().NotContain("Pay rate");
            component.Markup.Should().NotContain("Latitude");
            component.Markup.Should().NotContain("Diagnosis");
            component.Markup.Should().NotContain("opaque-preview-token");
        });
    }

    [Fact]
    public void GenerateInvoice_WhenCreated_EchoesTokenAndNavigatesToGuardedInvoiceRoute()
    {
        var handler = new BillingApiHandler()
            .Get("api/clients", ClientPage())
            .Post("api/invoices/preview", Preview())
            .Post("api/invoices", new InvoiceDetailDto { Id = InvoiceId, InvoiceNumber = "INV-TEST" });
        using var context = CreateContext(handler);
        var component = context.Render<BillingGenerate>();

        component.WaitForElement("#invoice-client");
        component.FindAll("button").Single(button => button.TextContent.Contains("Preview billable shifts")).Click();
        component.WaitForElement(".billing-generate-action button").Click();

        component.WaitForAssertion(() =>
        {
            var create = handler.PostRequests.Single(request => request.Path == "api/invoices");
            create.Body.Should().Contain("\"previewToken\":\"opaque-preview-token\"");
            context.Services.GetRequiredService<NavigationManager>().Uri.Should().EndWith($"/billing/invoices/{InvoiceId}");
        });
    }

    [Fact]
    public void GenerateInvoice_WhenExclusionOpened_ReconciliationRetainsBodyOnlyPreviewScope()
    {
        var handler = new BillingApiHandler()
            .Get("api/clients", ClientPage())
            .Post("api/invoices/preview", Preview())
            .Post("api/invoices/reconciliation/search", ReconciliationSearch());
        using var context = CreateContext(handler);
        var generate = context.Render<BillingGenerate>();
        generate.WaitForElement("#invoice-client");
        generate.FindAll("button").Single(button => button.TextContent.Contains("Preview billable shifts")).Click();
        generate.WaitForElement(".exclusion-links button").Click();

        context.Render<BillingReconciliation>();

        context.Services.GetRequiredService<NavigationManager>().Uri.Should().EndWith("/billing/reconciliation");
        var searchBody = handler.PostRequests.Last(request => request.Path == "api/invoices/reconciliation/search").Body;
        searchBody.Should().Contain(ClientId.ToString());
        searchBody.Should().Contain("\"serviceType\":1");
        searchBody.Should().Contain("\"reason\":5");
    }

    [Fact]
    public void GenerateInvoice_WhenPreviewIsStale_RequiresExplicitRepreviewAndDoesNotLeakToken()
    {
        var stale = new ApiProblemDetails
        {
            Title = "Preview must be refreshed",
            Status = 409,
            ValidationErrors = [new ValidationError("PreviewToken", "Preview must be refreshed.", "invoice.preview_stale")],
        };
        var handler = new BillingApiHandler()
            .Get("api/clients", ClientPage())
            .Post("api/invoices/preview", Preview())
            .Post("api/invoices", stale);
        using var context = CreateContext(handler);
        var component = context.Render<BillingGenerate>();

        component.WaitForElement("#invoice-client");
        component.FindAll("button").Single(button => button.TextContent.Contains("Preview billable shifts")).Click();
        component.WaitForElement(".billing-generate-action button").Click();

        component.WaitForAssertion(() =>
        {
            component.Markup.Should().Contain("preview changed or expired");
            component.Markup.Should().Contain("Preview billable shifts");
            component.Markup.Should().NotContain("Generate $262.50 invoice");
            component.Markup.Should().NotContain("opaque-preview-token");
        });
    }

    [Fact]
    public void Reconciliation_WhenLoaded_UsesBodySearchAndServerKpis()
    {
        var handler = new BillingApiHandler()
            .Get("api/clients", ClientPage())
            .Post("api/invoices/reconciliation/search", ReconciliationSearch());
        using var context = CreateContext(handler);
        var component = context.Render<BillingReconciliation>();

        component.WaitForAssertion(() =>
        {
            handler.PostRequests.Should().ContainSingle(request => request.Path == "api/invoices/reconciliation/search");
            handler.PostRequests[0].Body.Should().Contain("\"pageSize\":10");
            component.Markup.Should().Contain("$6,842.00");
            component.Markup.Should().Contain("31");
            component.Markup.Should().Contain("Harborview Center");
            component.Markup.Should().Contain("Casey Morgan");
            component.Markup.Should().NotContain("HourlyPayRate");
            component.Markup.Should().NotContain("Care plan");
        });
    }

    [Fact]
    public void Reconciliation_WhenMissingTimeSelected_PostsAuditedCorrectionAndRefreshes()
    {
        var detail = ReconciliationDetail(BillingCorrectiveDestination.ShiftTimeCorrection, BillingExclusionReason.MissingActualTime);
        var handler = new BillingApiHandler()
            .Get("api/clients", ClientPage())
            .Post("api/invoices/reconciliation/search", ReconciliationSearch())
            .Get($"api/invoices/reconciliation/shifts/{ShiftId}", detail)
            .Post($"api/invoices/reconciliation/shifts/{ShiftId}/correct-time", detail);
        using var context = CreateContext(handler);
        var component = context.Render<BillingReconciliation>();

        component.WaitForElement("table button.text-link").Click();
        component.WaitForElement("#time-correction-heading");
        component.FindAll("button").Single(button => button.TextContent.Contains("Save corrected time")).Click();

        component.WaitForAssertion(() =>
        {
            var correction = handler.PostRequests.Single(request => request.Path.EndsWith("/correct-time", StringComparison.Ordinal));
            correction.Body.Should().Contain("\"reason\":1");
            correction.Body.Should().Contain("\"breakMinutes\":0");
            handler.PostRequests.Count(request => request.Path == "api/invoices/reconciliation/search").Should().BeGreaterThan(1);
        });
    }

    [Fact]
    public void Reconciliation_WhenRateUpdateSelected_DoesNotGuessProtectedRates()
    {
        var search = ReconciliationSearch(BillingCorrectiveDestination.ShiftRateUpdate, BillingExclusionReason.MissingBillRate);
        var handler = new BillingApiHandler()
            .Get("api/clients", ClientPage())
            .Post("api/invoices/reconciliation/search", search)
            .Get($"api/invoices/reconciliation/shifts/{ShiftId}", ReconciliationDetail(BillingCorrectiveDestination.ShiftRateUpdate, BillingExclusionReason.MissingBillRate));
        using var context = CreateContext(handler);
        var component = context.Render<BillingReconciliation>();

        component.WaitForElement("table button.text-link").Click();

        component.WaitForAssertion(() =>
        {
            component.Markup.Should().Contain("protected shift-rate editor");
            component.Markup.Should().NotContain("Pay rate");
            handler.PutRequests.Should().BeEmpty();
        });
    }

    [Fact]
    public void Reconciliation_ResolveThenReopen_AppendsThroughDedicatedCommands()
    {
        var unresolved = ReconciliationDetail(BillingCorrectiveDestination.NonBillableResolution, BillingExclusionReason.InvalidBillableTime);
        var resolved = ReconciliationDetail(BillingCorrectiveDestination.None, BillingExclusionReason.NonBillableResolved);
        var handler = new BillingApiHandler()
            .Get("api/clients", ClientPage())
            .Post("api/invoices/reconciliation/search", ReconciliationSearch(BillingCorrectiveDestination.NonBillableResolution, BillingExclusionReason.InvalidBillableTime))
            .Get($"api/invoices/reconciliation/shifts/{ShiftId}", unresolved)
            .Post($"api/invoices/reconciliation/shifts/{ShiftId}/resolve", resolved)
            .Post($"api/invoices/reconciliation/shifts/{ShiftId}/reopen", unresolved);
        using var context = CreateContext(handler);
        var component = context.Render<BillingReconciliation>();

        component.WaitForElement("table button.text-link").Click();
        component.WaitForElement("#resolution-heading");
        component.FindAll("button").Single(button => button.TextContent.Contains("Record resolution")).Click();
        component.WaitForAssertion(() => component.FindAll("button").Should().Contain(button => button.TextContent.Contains("Reopen service")));
        component.FindAll("button").Single(button => button.TextContent.Contains("Reopen service")).Click();

        component.WaitForAssertion(() =>
        {
            handler.PostRequests.Should().Contain(request => request.Path.EndsWith("/resolve", StringComparison.Ordinal));
            handler.PostRequests.Should().Contain(request => request.Path.EndsWith("/reopen", StringComparison.Ordinal));
            handler.PostRequests.Single(request => request.Path.EndsWith("/resolve", StringComparison.Ordinal)).Body.Should().Contain("\"reason\":1");
        });
    }

    private static BunitContext CreateContext(BillingApiHandler handler)
    {
        var context = new BunitContext();
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test/") };
        context.Services.AddSingleton(new BillingClient(client));
        context.Services.AddSingleton(new ClientsClient(client));
        context.Services.AddSingleton(new BillingReconciliationNavigationState());
        context.Services.AddAuthorizationCore();
        context.Services.AddSingleton<AuthenticationStateProvider>(new AdminAuthStateProvider());
        return context;
    }

    private static PagedResult<ClientSummaryDto> ClientPage() => new()
    {
        Items = [new ClientSummaryDto { Id = ClientId, FullName = "Harborview Center", ServiceType = ServiceType.InHomeCare, IsActive = true }],
        PageNumber = 1, PageSize = 20, TotalCount = 1,
    };

    private static PagedResult<InvoiceSummaryDto> InvoicePage() => new() { Items = [], PageNumber = 1, PageSize = 8, TotalCount = 0 };

    private static InvoicePreviewResponseDto Preview() => new()
    {
        Rows = [new InvoicePreviewRowDto { ServiceDateUtc = new DateTime(2026, 6, 28, 0, 0, 0, DateTimeKind.Utc), ServiceStartUtc = new DateTime(2026, 6, 28, 14, 0, 0, DateTimeKind.Utc), ServiceEndUtc = new DateTime(2026, 6, 28, 21, 30, 0, DateTimeKind.Utc), BillableHours = 7.5m, BillRate = 35m, LineTotal = 262.5m, CaregiverDisplayName = "Casey Morgan", QualificationLabel = "RN" }],
        PageNumber = 1, PageSize = 10, EligibleShiftCount = 1, TotalBillableHours = 7.5m, Subtotal = 262.5m,
        ExclusionCounts = [new InvoiceExclusionCountDto { Reason = BillingExclusionReason.MissingActualTime, Count = 1 }],
        PreviewToken = "opaque-preview-token", PreviewTokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(15),
    };

    private static BillingReconciliationSearchResponseDto ReconciliationSearch(
        BillingCorrectiveDestination destination = BillingCorrectiveDestination.ShiftTimeCorrection,
        BillingExclusionReason reason = BillingExclusionReason.MissingActualTime) => new()
    {
        Rows = [new BillingReconciliationRowDto { ShiftId = ShiftId, ServiceDateUtc = DateTime.UtcNow.AddDays(-32), ScheduledStartUtc = DateTime.UtcNow.AddDays(-32), ScheduledEndUtc = DateTime.UtcNow.AddDays(-32).AddHours(8), ClientDisplayName = "Harborview Center", CaregiverDisplayName = "Casey Morgan", ServiceType = ServiceType.FacilityStaffing, Reason = reason, AgeDays = 32, IsRevenueAtRisk = true, EstimatedValue = 280m, CorrectiveDestination = destination }],
        PageNumber = 1, PageSize = 10, TotalCount = 31,
        Kpis = new BillingReconciliationKpiDto { UnresolvedCount = 31, RevenueAtRiskValue = 6842m, AgedCount = 6, AgedValue = 1680m },
    };

    private static BillingReconciliationDetailDto ReconciliationDetail(BillingCorrectiveDestination destination, BillingExclusionReason reason) => new()
    {
        Row = ReconciliationSearch(destination, reason).Rows[0], ResolutionHistory = [],
    };

    private sealed class AdminAuthStateProvider : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "Admin User"), new Claim(ClaimTypes.Role, "Admin")], "Test"))));
    }

    private sealed class BillingApiHandler : HttpMessageHandler
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private readonly Dictionary<(HttpMethod Method, string Path), Queue<object>> responses = [];
        public List<(string Path, string Body)> PostRequests { get; } = [];
        public List<(string Path, string Body)> PutRequests { get; } = [];

        public BillingApiHandler Get(string path, object response) => Add(HttpMethod.Get, path, response);
        public BillingApiHandler Post(string path, object response) => Add(HttpMethod.Post, path, response);

        private BillingApiHandler Add(HttpMethod method, string path, object response)
        {
            var key = (method, path);
            if (!responses.TryGetValue(key, out var queue)) responses[key] = queue = new Queue<object>();
            queue.Enqueue(response);
            return this;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.PathAndQuery.TrimStart('/').Split('?')[0] ?? string.Empty;
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            if (request.Method == HttpMethod.Post) PostRequests.Add((path, body));
            if (request.Method == HttpMethod.Put) PutRequests.Add((path, body));
            var match = responses.Where(pair => pair.Key.Method == request.Method && path.StartsWith(pair.Key.Path, StringComparison.OrdinalIgnoreCase)).OrderByDescending(pair => pair.Key.Path.Length).FirstOrDefault();
            if (match.Value is null || match.Value.Count == 0) return JsonResponse(new ApiProblemDetails { Title = "Not found", Status = 404 });
            var payload = match.Value.Count > 1 ? match.Value.Dequeue() : match.Value.Peek();
            return JsonResponse(payload);
        }

        private static HttpResponseMessage JsonResponse(object payload)
        {
            var status = payload is ApiProblemDetails problem ? (HttpStatusCode)problem.Status : HttpStatusCode.OK;
            return new HttpResponseMessage(status) { Content = JsonContent.Create(payload, options: JsonOptions) };
        }
    }
}
