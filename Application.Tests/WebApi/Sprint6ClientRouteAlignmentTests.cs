using System.Net;
using System.Text;
using CarePath.Client.Api;
using CarePath.Contracts.Common;
using FluentAssertions;

namespace CarePath.Application.Tests.WebApi;

/// <summary>
/// Pins the Sprint 6 caregiver/scheduling typed-client request shapes to the WebApi route
/// templates asserted in <see cref="Sprint4ControllerContractTests"/>, so client and server
/// cannot drift apart silently (S6-TASK-038/039).
/// </summary>
public sealed class Sprint6ClientRouteAlignmentTests
{
    private static readonly Guid FixedId = Guid.Parse("6f9619ff-8b86-d011-b42d-00cf4fc964ff");

    [Fact]
    public async Task GetCoverageQueueAsync_WhenCalled_TargetsShiftsCoverageRouteWithPaging()
    {
        // Arrange
        var handler = new RecordingHandler();
        var client = new ShiftsClient(CreateHttpClient(handler));

        // Act
        await client.GetCoverageQueueAsync(new PagedRequest { PageNumber = 2, PageSize = 25 });

        // Assert
        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/shifts/coverage?pageNumber=2&pageSize=25");
    }

    [Fact]
    public async Task GetEligibleCaregiversAsync_WhenCalled_TargetsShiftScopedRouteWithGuidOnly()
    {
        // Arrange
        var handler = new RecordingHandler();
        var client = new ShiftsClient(CreateHttpClient(handler));

        // Act
        await client.GetEligibleCaregiversAsync(FixedId, new PagedRequest());

        // Assert
        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be(
            $"/api/shifts/{FixedId}/eligible-caregivers?pageNumber=1&pageSize=20");
    }

    [Fact]
    public async Task GetEligibleOpenShiftsAsync_WhenCalled_TargetsCaregiverScopedRouteWithGuidOnly()
    {
        // Arrange
        var handler = new RecordingHandler();
        var client = new CaregiversClient(CreateHttpClient(handler));

        // Act
        await client.GetEligibleOpenShiftsAsync(FixedId, new PagedRequest());

        // Assert
        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be(
            $"/api/caregivers/{FixedId}/eligible-shifts?pageNumber=1&pageSize=20");
    }

    [Fact]
    public async Task GetAsync_WhenLoadingCaregiverProfileDetail_TargetsCaregiverDetailRoute()
    {
        // Arrange
        var handler = new RecordingHandler();
        var client = new CaregiversClient(CreateHttpClient(handler));

        // Act
        await client.GetAsync(FixedId);

        // Assert
        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be($"/api/caregivers/{FixedId}");
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
