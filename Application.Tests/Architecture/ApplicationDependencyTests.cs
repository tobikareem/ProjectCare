using FluentAssertions;

namespace CarePath.Application.Tests.Architecture;

public sealed class ApplicationDependencyTests
{
    [Fact]
    public void ApplicationAssembly_WhenReferencedAssembliesAreInspected_DoesNotDependOnInfrastructureOrWebApi()
    {
        // Arrange
        var applicationAssembly = typeof(CarePath.Application.Abstractions.Auth.ICurrentUserContext).Assembly;

        // Act
        var referencedAssemblies = applicationAssembly.GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .ToList();

        // Assert
        referencedAssemblies.Should().Contain("CarePath.Domain");
        referencedAssemblies.Should().NotContain("CarePath.Infrastructure");
        referencedAssemblies.Should().NotContain("CarePath.WebApi");
        referencedAssemblies.Should().NotContain("Microsoft.AspNetCore.Identity");
        referencedAssemblies.Should().NotContain("Microsoft.EntityFrameworkCore");
    }
}