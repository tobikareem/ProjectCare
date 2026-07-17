using System.Net;
using System.Text;
using CarePath.Client.Api;
using CarePath.Contracts.Billing;
using CarePath.Contracts.Enumerations;
using FluentAssertions;

namespace CarePath.Application.Tests.WebApi;

/// <summary>
/// Dedicated D-S6-18 billing client pins: every new BillingClient method's verb, template, and
/// body placement is locked to the InvoicesController routes, and no token, period, or filter
/// value ever appears in a URL.
/// </summary>
public sealed class Sprint6BillingClientRouteAlignmentTests
{
    private static readonly Guid FixedId = Guid.Parse("6f9619ff-8b86-d011-b42d-00cf4fc964ff");

    [Fact]
    public async Task PreviewInvoiceAsync_PostsSelectionInBodyToPreviewRoute()
    {
        var handler = new RecordingHandler();
        var client = new BillingClient(CreateHttpClient(handler));

        await client.PreviewInvoiceAsync(new InvoicePreviewRequest
        {
            ClientId = FixedId,
            ServiceType = ServiceType.InHomeCare,
            PeriodStartUtc = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            PeriodEndUtc = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
        });

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/invoices/preview");
    }

    [Fact]
    public async Task CreateInvoiceAsync_NeverPlacesThePreviewTokenInTheUrl()
    {
        var handler = new RecordingHandler();
        var client = new BillingClient(CreateHttpClient(handler));

        await client.CreateInvoiceAsync(new CreateInvoiceRequest
        {
            ClientId = FixedId,
            ServiceType = ServiceType.InHomeCare,
            PeriodStartUtc = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            PeriodEndUtc = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            DueDate = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc),
            PreviewToken = "SECRET-PREVIEW-TOKEN",
        });

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/invoices");
        handler.LastRequest.RequestUri.PathAndQuery.Should().NotContain("SECRET");
    }

    [Fact]
    public async Task SearchReconciliationAsync_PostsFiltersInBodyNotUrl()
    {
        var handler = new RecordingHandler();
        var client = new BillingClient(CreateHttpClient(handler));

        await client.SearchReconciliationAsync(new BillingReconciliationSearchRequest
        {
            PeriodStartUtc = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            PeriodEndUtc = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            ClientId = FixedId,
            Reason = BillingExclusionReason.MissingActualTime,
            PageNumber = 2,
            PageSize = 25,
        });

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/invoices/reconciliation/search");
    }

    [Fact]
    public async Task GetReconciliationDetailAsync_TargetsShiftScopedRouteWithGuidOnly()
    {
        var handler = new RecordingHandler();
        var client = new BillingClient(CreateHttpClient(handler));

        await client.GetReconciliationDetailAsync(FixedId);

        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be(
            $"/api/invoices/reconciliation/shifts/{FixedId}");
    }

    [Fact]
    public async Task ResolveNonBillableAsync_PostsReasonInBodyToResolveRoute()
    {
        var handler = new RecordingHandler();
        var client = new BillingClient(CreateHttpClient(handler));

        await client.ResolveNonBillableAsync(FixedId, new ResolveNonBillableRequest
        {
            Reason = BillingReconciliationReason.TrainingShift,
            Note = "Orientation engagement",
        });

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be(
            $"/api/invoices/reconciliation/shifts/{FixedId}/resolve");
        handler.LastRequest.RequestUri.PathAndQuery.Should().NotContain("Orientation");
    }

    [Fact]
    public async Task ReopenResolutionAsync_PostsToReopenRoute()
    {
        var handler = new RecordingHandler();
        var client = new BillingClient(CreateHttpClient(handler));

        await client.ReopenResolutionAsync(FixedId, new ReopenResolutionRequest());

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be(
            $"/api/invoices/reconciliation/shifts/{FixedId}/reopen");
    }

    [Fact]
    public async Task CorrectShiftTimeAsync_PostsWindowInBodyToCorrectTimeRoute()
    {
        var handler = new RecordingHandler();
        var client = new BillingClient(CreateHttpClient(handler));

        await client.CorrectShiftTimeAsync(FixedId, new CorrectShiftTimeRequest
        {
            ActualStartUtc = new DateTime(2026, 7, 10, 9, 0, 0, DateTimeKind.Utc),
            ActualEndUtc = new DateTime(2026, 7, 10, 13, 0, 0, DateTimeKind.Utc),
            BreakMinutes = 30,
            Reason = BillingTimeCorrectionReason.MissedCheckOut,
        });

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be(
            $"/api/invoices/reconciliation/shifts/{FixedId}/correct-time");
    }

    private static HttpClient CreateHttpClient(RecordingHandler handler) =>
        new(handler) { BaseAddress = new Uri("http://localhost/") };

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            });
        }
    }
}
