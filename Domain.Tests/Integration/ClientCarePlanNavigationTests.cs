using CarePath.Domain.Entities.Billing;
using CarePath.Domain.Entities.Clinical;
using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Entities.Scheduling;
using FluentAssertions;

namespace CarePath.Domain.Tests.Integration;

public class ClientCarePlanNavigationTests
{
    [Fact]
    public void Client_LinksToUser_ViaNavigationProperty()
    {
        var user = new User { FirstName = "Robert", LastName = "Johnson" };
        var client = new Client { UserId = user.Id, User = user };

        client.User.Should().BeSameAs(user);
        client.User.FullName.Should().Be("Robert Johnson");
    }

    [Fact]
    public void CarePlan_LinksToClient_ViaNavigationProperty()
    {
        var client = new Client();
        var plan = new CarePlan
        {
            ClientId = client.Id,
            Client = client,
            Title = "Standard In-Home Care Plan",
            IsActive = true
        };

        plan.Client.Should().BeSameAs(client);
        plan.Title.Should().Be("Standard In-Home Care Plan");
    }

    [Fact]
    public void Client_CarePlansCollection_CanContainMultiplePlans()
    {
        var client = new Client();
        var plan1 = new CarePlan { ClientId = client.Id, IsActive = false };
        var plan2 = new CarePlan { ClientId = client.Id, IsActive = true };
        client.CarePlans.Add(plan1);
        client.CarePlans.Add(plan2);

        client.CarePlans.Should().HaveCount(2);
        client.CarePlans.Count(p => p.IsActive).Should().Be(1);
    }

    [Fact]
    public void Client_ShiftsCollection_CanContainMultipleShifts()
    {
        var client = new Client();
        var shift1 = new Shift { ClientId = client.Id };
        var shift2 = new Shift { ClientId = client.Id };
        client.Shifts.Add(shift1);
        client.Shifts.Add(shift2);

        client.Shifts.Should().HaveCount(2);
    }

    [Fact]
    public void Client_InvoicesCollection_CanContainInvoices()
    {
        var client = new Client();
        var invoice = new Invoice { ClientId = client.Id };
        client.Invoices.Add(invoice);

        client.Invoices.Should().HaveCount(1);
    }
}
