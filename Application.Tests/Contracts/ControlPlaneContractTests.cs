using System.Reflection;
using CarePath.Contracts.Auth;
using CarePath.Contracts.Organization;
using CarePath.Contracts.Platform;
using FluentAssertions;

namespace CarePath.Application.Tests.Contracts;

/// <summary>Control-plane DTOs carry organization and staff-directory data only: never PHI, rates, secrets, or Domain types.</summary>
public sealed class ControlPlaneContractTests
{
    private static readonly string[] ForbiddenFragments =
    [
        "DateOfBirth", "Address", "City", "ZipCode", "Diagnosis", "Medical", "Insurance", "Allerg", "Latitude", "Longitude",
        "RawContent", "SourceText", "Responses", "Notes", "CarePlan", "VisitNote", "Ssn", "PayRate", "BillRate", "Margin",
        "ConnectionString", "SecretReference", "PasswordHash", "SecurityStamp", "TokenHash",
    ];

    private static readonly Type[] KnownContractTypes =
    [
        typeof(OrganizationSummaryDto),
        typeof(OrganizationDto),
        typeof(OrganizationDataPlaneDto),
        typeof(OrganizationDomainDto),
        typeof(OrganizationBrandingDetailDto),
        typeof(OrganizationBrandingDto),
        typeof(MembershipDto),
        typeof(CreateMembershipRequest),
        typeof(RegisterOrganizationRequest),
        typeof(UpdateOrganizationRequest),
        typeof(AuthTokenResponse),
    ];

    public static TheoryData<Type> ContractTypes
    {
        get
        {
            var data = new TheoryData<Type>();
            foreach (var type in KnownContractTypes)
            {
                data.Add(type);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(ContractTypes))]
    public void ContractType_WhenInspected_HasNoPhiFinancialOrSecretFieldsAndNoDomainTypes(Type type)
    {
        // Arrange
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        // Act
        var offenders = properties
            .Where(property => ForbiddenFragments.Any(fragment => property.Name.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
            .Select(property => property.Name)
            .ToList();
        var domainTyped = properties
            .Where(property => property.PropertyType.FullName?.StartsWith("CarePath.Domain.", StringComparison.Ordinal) == true)
            .Select(property => property.Name)
            .ToList();

        // Assert
        offenders.Should().BeEmpty();
        domainTyped.Should().BeEmpty();
    }

    [Fact]
    public void ContractsAssembly_WhenInspected_ContainsEveryControlPlaneDtoInTheTheory()
    {
        // Arrange — a new DTO under Platform/ or Organization/ must be added to the theory above.
        var namespaces = new[] { "CarePath.Contracts.Platform", "CarePath.Contracts.Organization" };
        var types = typeof(OrganizationDto).Assembly.GetTypes()
            .Where(type => type.IsClass && namespaces.Contains(type.Namespace))
            .ToList();

        // Assert
        types.Should().OnlyContain(type => KnownContractTypes.Contains(type)
            || type.Name.EndsWith("Request", StringComparison.Ordinal));
    }
}
