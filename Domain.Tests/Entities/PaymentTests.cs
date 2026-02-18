using CarePath.Domain.Entities.Billing;
using CarePath.Domain.Enumerations;
using FluentAssertions;

namespace CarePath.Domain.Tests.Entities;

public class PaymentTests
{
    [Fact]
    public void Status_DefaultsToPending()
    {
        var payment = new Payment();
        payment.Status.Should().Be(PaymentStatus.Pending);
    }

    [Fact]
    public void FailureReason_DefaultsToNull()
    {
        var payment = new Payment();
        payment.FailureReason.Should().BeNull();
    }

    [Fact]
    public void ReferenceNumber_DefaultsToNull()
    {
        var payment = new Payment();
        payment.ReferenceNumber.Should().BeNull();
    }

    [Fact]
    public void Notes_DefaultsToNull()
    {
        var payment = new Payment();
        payment.Notes.Should().BeNull();
    }

    [Fact]
    public void PaymentDate_DefaultsToRecentUtcNow()
    {
        var before = DateTime.UtcNow;
        var payment = new Payment();

        payment.PaymentDate.Should().BeOnOrAfter(before)
            .And.BeCloseTo(DateTime.UtcNow, precision: TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Status_CanBeSetToSettled()
    {
        var payment = new Payment { Status = PaymentStatus.Settled };
        payment.Status.Should().Be(PaymentStatus.Settled);
    }

    [Fact]
    public void Status_CanBeSetToFailed()
    {
        var payment = new Payment
        {
            Status = PaymentStatus.Failed,
            FailureReason = "Insufficient funds"
        };
        payment.Status.Should().Be(PaymentStatus.Failed);
        payment.FailureReason.Should().Be("Insufficient funds");
    }

    [Fact]
    public void Method_CanBeAssigned()
    {
        var payment = new Payment { Method = PaymentMethod.Check };
        payment.Method.Should().Be(PaymentMethod.Check);
    }
}
