using System.Net;
using System.Text;
using CarePath.Client.Api;
using CarePath.Contracts.Common;
using CarePath.Contracts.Enumerations;
using FluentAssertions;

namespace CarePath.Application.Tests.WebApi;

/// <summary>
/// Pins the Sprint 6 Transitions typed-client request shapes to the WebApi route templates
/// asserted in <see cref="Sprint4ControllerContractTests"/>, so client and server cannot
/// drift apart silently (S6-TASK-022 + the Transitions wireframe read surface).
/// </summary>
public sealed class Sprint6TransitionsClientRouteAlignmentTests
{
    private static readonly Guid FixedId = Guid.Parse("6f9619ff-8b86-d011-b42d-00cf4fc964ff");

    [Fact]
    public async Task GetEscalationQueueAsync_WhenOpenOnly_TargetsEscalationsRouteWithPagingAndFlag()
    {
        // Arrange
        var handler = new RecordingHandler("{}");
        var client = new TransitionsClient(CreateHttpClient(handler));

        // Act
        await client.GetEscalationQueueAsync(new PagedRequest { PageNumber = 2, PageSize = 25 });

        // Assert
        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be(
            "/api/transitions/escalations?pageNumber=2&pageSize=25&openOnly=true");
    }

    [Fact]
    public async Task GetEscalationQueueAsync_WhenIncludingAcknowledged_SendsOpenOnlyFalse()
    {
        // Arrange
        var handler = new RecordingHandler("{}");
        var client = new TransitionsClient(CreateHttpClient(handler));

        // Act
        await client.GetEscalationQueueAsync(new PagedRequest(), openOnly: false);

        // Assert
        handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be(
            "/api/transitions/escalations?pageNumber=1&pageSize=20&openOnly=false");
    }

    [Fact]
    public async Task GetPlansAsync_WhenNoStatusFilter_TargetsPlansRouteWithPagingOnly()
    {
        // Arrange
        var handler = new RecordingHandler("{}");
        var client = new TransitionsClient(CreateHttpClient(handler));

        // Act
        await client.GetPlansAsync(new PagedRequest());

        // Assert
        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be(
            "/api/transitions/plans?pageNumber=1&pageSize=20");
    }

    [Fact]
    public async Task GetPlansAsync_WhenStatusFilterProvided_AppendsStatusName()
    {
        // Arrange
        var handler = new RecordingHandler("{}");
        var client = new TransitionsClient(CreateHttpClient(handler));

        // Act
        await client.GetPlansAsync(new PagedRequest(), TransitionPlanStatus.PendingVerification);

        // Assert
        handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be(
            "/api/transitions/plans?pageNumber=1&pageSize=20&status=PendingVerification");
    }

    [Fact]
    public async Task GetRemindersAsync_WhenCalled_TargetsPlanScopedRemindersRouteWithGuidOnly()
    {
        // Arrange
        var handler = new RecordingHandler("[]");
        var client = new TransitionsClient(CreateHttpClient(handler));

        // Act
        await client.GetRemindersAsync(FixedId);

        // Assert
        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be(
            $"/api/transitions/plans/{FixedId}/reminders");
    }

    [Fact]
    public async Task GetCheckInsAsync_WhenCalled_TargetsPlanScopedCheckInsRouteWithGuidOnly()
    {
        // Arrange
        var handler = new RecordingHandler("[]");
        var client = new TransitionsClient(CreateHttpClient(handler));

        // Act
        await client.GetCheckInsAsync(FixedId);

        // Assert
        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be(
            $"/api/transitions/plans/{FixedId}/check-ins");
    }

    [Fact]
    public async Task GetDischargeDocumentsAsync_WhenCalled_TargetsDocumentsRouteWithPaging()
    {
        // Arrange
        var handler = new RecordingHandler("{}");
        var client = new TransitionsClient(CreateHttpClient(handler));

        // Act
        await client.GetDischargeDocumentsAsync(new PagedRequest { PageNumber = 3, PageSize = 5 });

        // Assert
        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be(
            "/api/transitions/documents?pageNumber=3&pageSize=5");
    }

    private static HttpClient CreateHttpClient(RecordingHandler handler) =>
        new(handler) { BaseAddress = new Uri("http://localhost/") };

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly string responseBody;

        public RecordingHandler(string responseBody) => this.responseBody = responseBody;

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
            });
        }
    }
}
