using CarePath.Application.Abstractions.Auth;
using CarePath.Application.Common.Exceptions;
using CarePath.Application.Transitions.Services;
using CarePath.Contracts.Transitions;
using CarePath.WebApi.Controllers;
using FluentAssertions;
using Moq;

namespace CarePath.Application.Tests.WebApi;

public sealed class TransitionsControllerTests
{
    [Theory]
    [InlineData(nameof(TransitionsController.GetDischargeDocument), ProtectedResourceType.DischargeDocument)]
    [InlineData(nameof(TransitionsController.GetDischargeDocumentContent), ProtectedResourceType.DischargeDocument)]
    [InlineData(nameof(TransitionsController.ExtractDischargeDocument), ProtectedResourceType.DischargeDocument)]
    [InlineData(nameof(TransitionsController.GetPlan), ProtectedResourceType.TransitionPlan)]
    [InlineData(nameof(TransitionsController.ReviewInstruction), ProtectedResourceType.TransitionPlan)]
    [InlineData(nameof(TransitionsController.ActivatePlan), ProtectedResourceType.TransitionPlan)]
    [InlineData(nameof(TransitionsController.ScheduleReminder), ProtectedResourceType.TransitionPlan)]
    [InlineData(nameof(TransitionsController.GetEscalations), ProtectedResourceType.TransitionPlan)]
    [InlineData(nameof(TransitionsController.AcknowledgeEscalation), ProtectedResourceType.TransitionEscalation)]
    [InlineData(nameof(TransitionsController.GetPlanForClient), ProtectedResourceType.Client)]
    public async Task ClinicalIdRoutes_WhenIdorDenies_ThrowPhiAccessDeniedBeforeServiceCall(
        string actionName,
        ProtectedResourceType expectedResourceType)
    {
        // Arrange
        var id = Guid.NewGuid();
        var service = new Mock<ITransitionsService>(MockBehavior.Strict);
        var idorGuard = new Mock<IIdorGuard>(MockBehavior.Strict);
        idorGuard.Setup(guard => guard.EnsureAuthorizedAsync(
                expectedResourceType,
                id,
                It.IsAny<ObjectAccessAction>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ObjectAccessResult.DeniedWithoutDisclosure("ResourceUnavailable"));
        var controller = new TransitionsController(service.Object, idorGuard.Object);

        // Act
        Func<Task> act = actionName switch
        {
            nameof(TransitionsController.GetDischargeDocument) => async () => await controller.GetDischargeDocument(id, CancellationToken.None),
            nameof(TransitionsController.GetDischargeDocumentContent) => async () => await controller.GetDischargeDocumentContent(id, CancellationToken.None),
            nameof(TransitionsController.ExtractDischargeDocument) => async () => await controller.ExtractDischargeDocument(id, CancellationToken.None),
            nameof(TransitionsController.GetPlan) => async () => await controller.GetPlan(id, CancellationToken.None),
            nameof(TransitionsController.ReviewInstruction) => async () => await controller.ReviewInstruction(id, Guid.NewGuid(), new ReviewInstructionRequest(), CancellationToken.None),
            nameof(TransitionsController.ActivatePlan) => async () => await controller.ActivatePlan(id, new ActivatePlanRequest { ConfirmESignature = true }, CancellationToken.None),
            nameof(TransitionsController.ScheduleReminder) => async () => await controller.ScheduleReminder(id, new ScheduleReminderRequest(), CancellationToken.None),
            nameof(TransitionsController.GetEscalations) => async () => await controller.GetEscalations(id, CancellationToken.None),
            nameof(TransitionsController.AcknowledgeEscalation) => async () => await controller.AcknowledgeEscalation(id, new AcknowledgeEscalationRequest { ResolutionNote = "Reviewed.", EscalationLevel = CarePath.Contracts.Enumerations.EscalationLevel.CoordinatorAlert }, CancellationToken.None),
            nameof(TransitionsController.GetPlanForClient) => async () => await controller.GetPlanForClient(id, CancellationToken.None),
            _ => throw new InvalidOperationException("Unsupported action."),
        };

        // Assert
        await act.Should().ThrowAsync<ResourceAccessDeniedException>();
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task PatientRoutes_DoNotUseGenericIdorGuard()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var service = new Mock<ITransitionsService>(MockBehavior.Strict);
        service.Setup(transitions => transitions.GetPatientPlanAsync(planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TransitionPlanPatientFacingDto { Id = planId });
        service.Setup(transitions => transitions.CreateCheckInAsync(planId, It.IsAny<CreateCheckInRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TransitionCheckInDto { TransitionPlanId = planId });
        var idorGuard = new Mock<IIdorGuard>(MockBehavior.Strict);
        var controller = new TransitionsController(service.Object, idorGuard.Object);

        // Act
        await controller.GetPatientPlan(planId, CancellationToken.None);
        await controller.CreateCheckIn(planId, new CreateCheckInRequest { ResponsesJson = "{}" }, CancellationToken.None);

        // Assert
        idorGuard.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CreateDischargeDocument_UsesClientScopeGuard()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var request = new CreateDischargeDocumentRequest
        {
            ClientId = clientId,
            RawContent = "sensitive discharge content",
        };
        var service = new Mock<ITransitionsService>(MockBehavior.Strict);
        var idorGuard = new Mock<IIdorGuard>(MockBehavior.Strict);
        idorGuard.Setup(guard => guard.EnsureAuthorizedAsync(
                ProtectedResourceType.Client,
                clientId,
                ObjectAccessAction.Create,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ObjectAccessResult.DeniedWithoutDisclosure("ResourceUnavailable"));
        var controller = new TransitionsController(service.Object, idorGuard.Object);

        // Act
        var act = async () => await controller.CreateDischargeDocument(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ResourceAccessDeniedException>();
        service.VerifyNoOtherCalls();
    }
}
