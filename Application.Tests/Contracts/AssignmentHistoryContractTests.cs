using CarePath.Contracts.Scheduling;
using FluentAssertions;

namespace CarePath.Application.Tests.Contracts;

public sealed class AssignmentHistoryContractTests
{
    private static readonly string[] ForbiddenNames = ["DateOfBirth", "Address", "Diagnosis", "MedicalConditions", "Allergies", "CarePlan", "VisitNote", "Notes", "Latitude", "Longitude", "PayRate", "BillRate", "Margin", "Email", "Phone"];

    [Theory]
    [InlineData(typeof(CaregiverAssignmentSummaryDto))]
    [InlineData(typeof(ClientAssignmentSummaryDto))]
    [InlineData(typeof(MyClientAssignmentSummaryDto))]
    [InlineData(typeof(MyCaregiverAssignmentSummaryDto))]
    public void AssignmentSummaryDto_PublicShape_ExcludesSensitiveAndFinancialFields(Type dtoType)
    {
        var names = dtoType.GetProperties().Select(property => property.Name).ToArray();

        names.Should().NotContain(name => ForbiddenNames.Any(forbidden => name.Contains(forbidden, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void MyClientAssignmentSummaryDto_IsDistinctFromStaffClientDto()
    {
        typeof(MyClientAssignmentSummaryDto).Should().NotBe(typeof(ClientAssignmentSummaryDto));
        typeof(MyClientAssignmentSummaryDto).GetProperties().Select(property => property.Name)
            .Should().NotContain(["ClientId", "LastShiftAtUtc"]);
    }

    [Fact]
    public void MyCaregiverAssignmentSummaryDto_ContainsOnlyApprovedClientFacingFields()
    {
        typeof(MyCaregiverAssignmentSummaryDto).GetProperties().Select(property => property.Name)
            .Should().BeEquivalentTo(["CaregiverDisplayName", "FirstAssignedAtUtc", "LastAssignedAtUtc", "NextShiftAtUtc", "Status"]);
    }
}
