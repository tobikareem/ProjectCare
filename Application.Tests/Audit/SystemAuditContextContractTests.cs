using CarePath.Application.Abstractions.Audit;
using CarePath.Application.Abstractions.Auth;
using FluentAssertions;

namespace CarePath.Application.Tests.Audit;

public sealed class SystemAuditContextContractTests
{
    [Fact]
    public void CreateBackgroundJobEntry_WhenImplemented_MustAcceptEntityIdentityAndReturnPhiAuditEntry()
    {
        // Arrange
        var method = typeof(ISystemAuditContext).GetMethod(nameof(ISystemAuditContext.CreateBackgroundJobEntry));

        // Act
        var parameters = method?.GetParameters()
            .Select(parameter => (parameter.Name, parameter.ParameterType))
            .ToList();

        // Assert
        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(PhiAuditEntry));
        parameters.Should().Contain(("entityType", typeof(ProtectedResourceType)));
        parameters.Should().Contain(("entityId", typeof(Guid)));
        parameters.Should().Contain(("backgroundJobName", typeof(string)));
        parameters.Should().Contain(("correlationId", typeof(string)));
    }
}