using CarePath.Infrastructure.ControlPlane.Audit;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;

namespace CarePath.Infrastructure.Tests.ControlPlane;

public sealed class HostDenialAuditThrottleTests
{
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(60);

    [Fact]
    public void ShouldWrite_WhenCalledTwiceInsideTheWindow_AllowsOnlyTheFirst()
    {
        // Arrange
        var clock = new FakeTimeProvider();
        var throttle = new HostDenialAuditThrottle(clock);

        // Act
        var first = throttle.ShouldWrite("hash-a", Window);
        clock.Advance(TimeSpan.FromSeconds(30));
        var second = throttle.ShouldWrite("hash-a", Window);
        clock.Advance(TimeSpan.FromSeconds(31));
        var third = throttle.ShouldWrite("hash-a", Window);

        // Assert
        first.Should().BeTrue();
        second.Should().BeFalse();
        third.Should().BeTrue();
    }

    [Fact]
    public void ShouldWrite_WhenHashesDiffer_TracksThemIndependently()
    {
        // Arrange
        var throttle = new HostDenialAuditThrottle(new FakeTimeProvider());

        // Act / Assert
        throttle.ShouldWrite("hash-a", Window).Should().BeTrue();
        throttle.ShouldWrite("hash-b", Window).Should().BeTrue();
        throttle.ShouldWrite("hash-a", Window).Should().BeFalse();
    }

    [Fact]
    public void ShouldWrite_UnderConcurrentCallsForOneHash_AllowsExactlyOne()
    {
        // Arrange
        var throttle = new HostDenialAuditThrottle(new FakeTimeProvider());
        var allowed = 0;

        // Act
        Parallel.For(0, 2_000, _ =>
        {
            if (throttle.ShouldWrite("contended", Window))
            {
                Interlocked.Increment(ref allowed);
            }
        });

        // Assert
        allowed.Should().Be(1);
    }

    [Fact]
    public void ShouldWrite_WhenHostsRotateBeyondTheGlobalBudget_RefusesTheRest()
    {
        // Arrange
        var clock = new FakeTimeProvider();
        var throttle = new HostDenialAuditThrottle(clock);

        // Act
        var allowed = Enumerable.Range(0, HostDenialAuditThrottle.MaxWritesPerWindow + 50)
            .Count(index => throttle.ShouldWrite($"hash-{index}", Window));
        clock.Advance(Window);
        var afterWindow = throttle.ShouldWrite("hash-new", Window);

        // Assert
        allowed.Should().Be(HostDenialAuditThrottle.MaxWritesPerWindow);
        afterWindow.Should().BeTrue();
    }

    [Fact]
    public void ShouldWrite_WhenWindowIsZero_AlwaysAllows()
    {
        // Arrange
        var throttle = new HostDenialAuditThrottle(new FakeTimeProvider());

        // Act / Assert
        throttle.ShouldWrite("hash-a", TimeSpan.Zero).Should().BeTrue();
        throttle.ShouldWrite("hash-a", TimeSpan.Zero).Should().BeTrue();
    }
}
