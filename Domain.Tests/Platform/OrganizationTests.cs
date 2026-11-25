using CarePath.Domain.Entities.Platform;
using CarePath.Domain.Enumerations;
using FluentAssertions;

namespace CarePath.Domain.Tests.Platform;

public sealed class OrganizationTests
{
    [Theory]
    [InlineData(OrganizationStatus.Active, ProvisioningState.Provisioned, true)]
    [InlineData(OrganizationStatus.Active, ProvisioningState.Registered, false)]
    [InlineData(OrganizationStatus.Active, ProvisioningState.Provisioning, false)]
    [InlineData(OrganizationStatus.Active, ProvisioningState.Failed, false)]
    [InlineData(OrganizationStatus.Pending, ProvisioningState.Provisioned, false)]
    [InlineData(OrganizationStatus.Suspended, ProvisioningState.Provisioned, false)]
    [InlineData(OrganizationStatus.Offboarded, ProvisioningState.Provisioned, false)]
    public void IsAdmissible_ForStatusAndProvisioningState_ReturnsExpected(
        OrganizationStatus status,
        ProvisioningState provisioningState,
        bool expected)
    {
        // Arrange
        var organization = new Organization { Status = status, ProvisioningState = provisioningState };

        // Act
        var admissible = organization.IsAdmissible;

        // Assert
        admissible.Should().Be(expected);
    }

    [Fact]
    public void Organization_WhenCreated_DefaultsToPendingAndRegistered()
    {
        // Arrange / Act
        var organization = new Organization();

        // Assert
        organization.Status.Should().Be(OrganizationStatus.Pending);
        organization.ProvisioningState.Should().Be(ProvisioningState.Registered);
        organization.IsAdmissible.Should().BeFalse();
    }

    [Theory]
    [InlineData(DomainVerificationStatus.Verified, false, true)]
    [InlineData(DomainVerificationStatus.Verified, true, false)]
    [InlineData(DomainVerificationStatus.Pending, false, false)]
    public void OrganizationDomain_IsResolvable_RequiresVerifiedAndNotDeleted(
        DomainVerificationStatus status,
        bool isDeleted,
        bool expected)
    {
        // Arrange
        var domain = new OrganizationDomain { VerificationStatus = status, IsDeleted = isDeleted };

        // Act / Assert
        domain.IsResolvable.Should().Be(expected);
    }

    [Theory]
    [InlineData(MembershipStatus.Active, false, true)]
    [InlineData(MembershipStatus.Active, true, false)]
    [InlineData(MembershipStatus.Inactive, false, false)]
    public void OrganizationMembership_IsActive_RequiresActiveAndNotDeleted(
        MembershipStatus status,
        bool isDeleted,
        bool expected)
    {
        // Arrange
        var membership = new OrganizationMembership { Status = status, IsDeleted = isDeleted };

        // Act / Assert
        membership.IsActive.Should().Be(expected);
        membership.SecurityVersion.Should().Be(0);
    }
}
