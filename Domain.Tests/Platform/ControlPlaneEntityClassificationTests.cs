using System.Reflection;
using CarePath.Domain.Entities.Platform;
using FluentAssertions;

namespace CarePath.Domain.Tests.Platform;

/// <summary>
/// Guards the non-PHI classification of the control plane (CP-04 FR-002): no property on any
/// platform entity may carry clinical, insurance, address, date-of-birth, or care data.
/// </summary>
public sealed class ControlPlaneEntityClassificationTests
{
    private static readonly string[] ForbiddenFragments =
    [
        "DateOfBirth",
        "Dob",
        "Address",
        "City",
        "ZipCode",
        "Diagnosis",
        "Medical",
        "Insurance",
        "Allerg",
        "Latitude",
        "Longitude",
        "RawContent",
        "SourceText",
        "Responses",
        "Notes",
        "CarePlan",
        "VisitNote",
        "Ssn",
        "PayRate",
        "BillRate",
        "Margin"
    ];

    private static readonly Type[] KnownPlatformEntityTypes =
    [
        typeof(Organization),
        typeof(OrganizationDomain),
        typeof(OrganizationBranding),
        typeof(OrganizationMembership),
        typeof(OrganizationDataPlane),
        typeof(PlatformAuditEvent)
    ];

    public static TheoryData<Type> PlatformEntityTypes
    {
        get
        {
            var data = new TheoryData<Type>();
            foreach (var type in KnownPlatformEntityTypes)
            {
                data.Add(type);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(PlatformEntityTypes))]
    public void PlatformEntity_WhenInspected_HasNoPhiPropertyNames(Type entityType)
    {
        // Arrange
        var propertyNames = entityType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .ToList();

        // Act
        var offenders = propertyNames
            .Where(name => ForbiddenFragments.Any(fragment => name.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        // Assert
        offenders.Should().BeEmpty($"{entityType.Name} must not carry PHI-shaped fields");
    }

    [Fact]
    public void PlatformEntities_WhenEnumerated_CoverEveryTypeInThePlatformNamespace()
    {
        // Arrange
        var namespaceTypes = typeof(Organization).Assembly
            .GetTypes()
            .Where(type => type.Namespace == typeof(Organization).Namespace && type.IsClass && !type.IsAbstract)
            .ToList();

        // Assert — a new platform entity must be added to the denylist theory above.
        namespaceTypes.Should().BeEquivalentTo(KnownPlatformEntityTypes);
    }
}
