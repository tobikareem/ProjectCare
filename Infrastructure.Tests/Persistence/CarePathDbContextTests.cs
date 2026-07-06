using CarePath.Infrastructure.Persistence;
using CarePath.Infrastructure.Persistence.Interceptors;
using CarePath.Domain.Entities.Transitions;
using CarePath.Domain.Enumerations;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace CarePath.Infrastructure.Tests.Persistence;

public class CarePathDbContextTests
{

    [Fact]
    public async Task DomainQueries_WhenEntityIsSoftDeleted_ExcludeDeletedRows()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<CarePathDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var interceptor = new AuditableEntityInterceptor(new HttpContextAccessor());
        await using var context = new CarePathDbContext(options, interceptor);
        var user = new Domain.Entities.Identity.User
        {
            FirstName = "Synthetic",
            LastName = "Deleted",
            Email = $"synthetic-{Guid.NewGuid():N}@carepath.local",
            PhoneNumber = "555-0100",
            Role = Domain.Enumerations.UserRole.Admin,
            IsDeleted = true
        };

        // Act
        await context.DomainUsers.AddAsync(user);
        await context.SaveChangesAsync();
        var users = await context.DomainUsers.ToListAsync();

        // Assert
        users.Should().BeEmpty();
    }

    [Fact]
    public void Model_WhenContextIsCreated_BuildsSuccessfully()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<CarePathDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var interceptor = new AuditableEntityInterceptor(new HttpContextAccessor());

        // Act
        using var context = new CarePathDbContext(options, interceptor);
        var entityTypes = context.Model.GetEntityTypes().Select(entity => entity.ClrType).ToList();

        // Assert
        entityTypes.Should().Contain(typeof(Domain.Entities.Identity.User));
        entityTypes.Should().Contain(typeof(TransitionPlan));
    }

    [Fact]
    public async Task TransitionPlanNavigationCollections_WhenIncluded_MaterializeReadOnlyLists()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<CarePathDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var interceptor = new AuditableEntityInterceptor(new HttpContextAccessor());
        var planId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using (var context = new CarePathDbContext(options, interceptor))
        {
            context.DischargeDocuments.Add(new DischargeDocument
            {
                Id = documentId,
                ClientId = Guid.NewGuid(),
                SourceType = DischargeDocumentSourceType.PdfUpload,
                Status = DischargeDocumentStatus.AwaitingReview,
                UploadedBy = Guid.NewGuid(),
                UploadedAt = now
            });
            context.TransitionPlans.Add(new TransitionPlan
            {
                Id = planId,
                ClientId = Guid.NewGuid(),
                DischargeDocumentId = documentId,
                DischargeDate = now.Date,
                TransitionWindowEnd = now.Date.AddDays(30),
                Status = TransitionPlanStatus.PendingVerification
            });
            context.TransitionInstructions.Add(new TransitionInstruction
            {
                TransitionPlanId = planId,
                Category = TransitionInstructionCategory.Medication,
                InstructionText = "Synthetic medication instruction",
                ConfidenceScore = 0.9000m,
                Status = TransitionInstructionStatus.Pending
            });
            context.TransitionReminders.Add(new TransitionReminder
            {
                TransitionPlanId = planId,
                ReminderType = ReminderType.Medication,
                Channel = ReminderChannel.App,
                ScheduledAt = now.AddDays(1),
                Status = ReminderStatus.Scheduled
            });
            context.TransitionCheckIns.Add(new TransitionCheckIn
            {
                TransitionPlanId = planId,
                Channel = ReminderChannel.App,
                ResponsesJson = "{}"
            });
            context.TransitionEscalations.Add(new TransitionEscalation
            {
                TransitionPlanId = planId,
                TriggerType = EscalationTriggerType.WarningSymptomsReported,
                TriggerDetails = "Synthetic warning symptom",
                EscalationLevel = EscalationLevel.CoordinatorAlert,
                EscalatedAt = now
            });
            await context.SaveChangesAsync();
        }

        // Act
        await using var verifyContext = new CarePathDbContext(options, interceptor);
        var plan = await verifyContext.TransitionPlans
            .Include(transitionPlan => transitionPlan.Instructions)
            .Include(transitionPlan => transitionPlan.Reminders)
            .Include(transitionPlan => transitionPlan.CheckIns)
            .Include(transitionPlan => transitionPlan.Escalations)
            .SingleAsync(transitionPlan => transitionPlan.Id == planId);

        // Assert
        plan.Instructions.Should().ContainSingle();
        plan.Reminders.Should().ContainSingle();
        plan.CheckIns.Should().ContainSingle();
        plan.Escalations.Should().ContainSingle();
    }
}
