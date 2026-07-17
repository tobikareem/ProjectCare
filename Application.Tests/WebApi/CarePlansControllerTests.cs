using CarePath.Application.Abstractions.Auth;
using CarePath.Application.Clients.Services;
using CarePath.Application.Common.Exceptions;
using CarePath.Contracts.Clients;
using CarePath.WebApi.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CarePath.Application.Tests.WebApi;

public sealed class CarePlansControllerTests
{
    [Fact]
    public async Task GetCarePlan_WhenIdorDenies_ThrowsPhiAccessDeniedBeforeServiceCall()
    {
        // Arrange — D-S6-14: object authorization runs before any clinical content loads
        var carePlanId = Guid.NewGuid();
        var service = new Mock<IClientOperationsService>(MockBehavior.Strict);
        var idorGuard = new Mock<IIdorGuard>(MockBehavior.Strict);
        idorGuard.Setup(guard => guard.EnsureAuthorizedAsync(
                ProtectedResourceType.CarePlan,
                carePlanId,
                ObjectAccessAction.Read,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ObjectAccessResult.DeniedWithoutDisclosure("ResourceUnavailable"));
        var controller = new CarePlansController(service.Object, idorGuard.Object);

        // Act
        var act = async () => await controller.GetCarePlan(carePlanId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ResourceAccessDeniedException>()
            .Where(exception => exception.IsPhiResource);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetCarePlan_WhenAuthorized_ReturnsServiceDetail()
    {
        // Arrange
        var carePlanId = Guid.NewGuid();
        var expected = new CarePlanDto { Id = carePlanId, Title = "Post-surgical support" };
        var service = new Mock<IClientOperationsService>(MockBehavior.Strict);
        service.Setup(operations => operations.GetCarePlanAsync(carePlanId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var idorGuard = new Mock<IIdorGuard>(MockBehavior.Strict);
        idorGuard.Setup(guard => guard.EnsureAuthorizedAsync(
                ProtectedResourceType.CarePlan,
                carePlanId,
                ObjectAccessAction.Read,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ObjectAccessResult.Authorized());
        var controller = new CarePlansController(service.Object, idorGuard.Object);

        // Act
        var response = await controller.GetCarePlan(carePlanId, CancellationToken.None);

        // Assert
        var ok = response.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(expected);
    }
}
