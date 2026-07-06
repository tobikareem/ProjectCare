using CarePath.Domain.Entities.Billing;
using CarePath.Domain.Entities.Clinical;
using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Entities.Scheduling;
using CarePath.Domain.Entities.Transitions;
using CarePath.Infrastructure.Identity;
using CarePath.Infrastructure.Persistence;
using CarePath.Infrastructure.Persistence.Interceptors;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CarePath.Infrastructure.Tests.Persistence;

public class EntityConfigurationTests
{
    private readonly IModel _model = CreateModel();

    [Fact]
    public void Model_WhenBuilt_IncludesTransitionsEntities()
    {
        // Assert
        _model.FindEntityType(typeof(DischargeDocument)).Should().NotBeNull();
        _model.FindEntityType(typeof(TransitionPlan)).Should().NotBeNull();
        _model.FindEntityType(typeof(TransitionInstruction)).Should().NotBeNull();
        _model.FindEntityType(typeof(TransitionReminder)).Should().NotBeNull();
        _model.FindEntityType(typeof(TransitionCheckIn)).Should().NotBeNull();
        _model.FindEntityType(typeof(TransitionEscalation)).Should().NotBeNull();
    }

    [Fact]
    public void PhiRelationships_WhenConfigured_UseRetentionSafeDeleteBehavior()
    {
        // Assert
        GetDeleteBehavior<CaregiverCertification>(nameof(CaregiverCertification.CaregiverId))
            .Should().Be(DeleteBehavior.Restrict);
        GetDeleteBehavior<Client>(nameof(Client.UserId)).Should().Be(DeleteBehavior.Restrict);
        GetDeleteBehavior<ClientAccessGrant>(nameof(ClientAccessGrant.GranteeUserId)).Should().Be(DeleteBehavior.Restrict);
        GetDeleteBehavior<ClientAccessGrant>(nameof(ClientAccessGrant.ClientId)).Should().Be(DeleteBehavior.Restrict);
        GetDeleteBehavior<ClientAccessGrant>(nameof(ClientAccessGrant.GrantedByUserId)).Should().Be(DeleteBehavior.Restrict);
        GetDeleteBehavior<ClientAccessGrant>(nameof(ClientAccessGrant.RevokedByUserId)).Should().Be(DeleteBehavior.Restrict);
        GetDeleteBehavior<CarePlan>(nameof(CarePlan.ClientId)).Should().Be(DeleteBehavior.Restrict);
        GetDeleteBehavior<Shift>(nameof(Shift.ClientId)).Should().Be(DeleteBehavior.Restrict);
        GetDeleteBehavior<Shift>(nameof(Shift.CaregiverId)).Should().Be(DeleteBehavior.Restrict);
        GetDeleteBehavior<VisitNote>(nameof(VisitNote.ShiftId)).Should().Be(DeleteBehavior.Restrict);
        GetDeleteBehavior<VisitNote>(nameof(VisitNote.CaregiverId)).Should().Be(DeleteBehavior.Restrict);
        GetDeleteBehavior<VisitPhoto>(nameof(VisitPhoto.VisitNoteId)).Should().Be(DeleteBehavior.Restrict);
        GetDeleteBehavior<Invoice>(nameof(Invoice.ClientId)).Should().Be(DeleteBehavior.Restrict);
        GetDeleteBehavior<InvoiceLineItem>(nameof(InvoiceLineItem.InvoiceId)).Should().Be(DeleteBehavior.Restrict);
        GetDeleteBehavior<InvoiceLineItem>(nameof(InvoiceLineItem.ShiftId)).Should().Be(DeleteBehavior.Restrict);
        GetDeleteBehavior<Payment>(nameof(Payment.InvoiceId)).Should().Be(DeleteBehavior.Restrict);
        GetDeleteBehavior<DischargeDocument>(nameof(DischargeDocument.ClientId)).Should().Be(DeleteBehavior.Restrict);
        GetDeleteBehavior<TransitionPlan>(nameof(TransitionPlan.ClientId)).Should().Be(DeleteBehavior.Restrict);
        GetDeleteBehavior<TransitionPlan>(nameof(TransitionPlan.DischargeDocumentId)).Should().Be(DeleteBehavior.Restrict);
        GetDeleteBehavior<TransitionInstruction>(nameof(TransitionInstruction.TransitionPlanId)).Should().Be(DeleteBehavior.Restrict);
        GetDeleteBehavior<TransitionReminder>(nameof(TransitionReminder.TransitionPlanId)).Should().Be(DeleteBehavior.Restrict);
        GetDeleteBehavior<TransitionReminder>(nameof(TransitionReminder.TransitionInstructionId)).Should().Be(DeleteBehavior.Restrict);
        GetDeleteBehavior<TransitionCheckIn>(nameof(TransitionCheckIn.TransitionPlanId)).Should().Be(DeleteBehavior.Restrict);
        GetDeleteBehavior<TransitionEscalation>(nameof(TransitionEscalation.TransitionPlanId)).Should().Be(DeleteBehavior.Restrict);
        GetDeleteBehavior<VisitNote>(nameof(VisitNote.TransitionPlanId)).Should().Be(DeleteBehavior.Restrict);
    }

    [Fact]
    public void ShiftCaregiverRelationship_WhenConfigured_KeepsNullableForeignKeyButRestrictsDatabaseDelete()
    {
        // Assert
        GetProperty<Shift>(nameof(Shift.CaregiverId)).IsNullable.Should().BeTrue();
        GetDeleteBehavior<Shift>(nameof(Shift.CaregiverId)).Should().Be(DeleteBehavior.Restrict);
    }

    [Fact]
    public void StringProperties_WhenConfigured_HaveExpectedMaxLengths()
    {
        // Assert
        GetProperty<User>(nameof(User.Email)).GetMaxLength().Should().Be(256);
        GetProperty<User>(nameof(User.FirstName)).GetMaxLength().Should().Be(100);
        GetProperty<User>(nameof(User.LastName)).GetMaxLength().Should().Be(100);
        GetProperty<Client>(nameof(Client.MedicalConditions)).GetMaxLength().Should().Be(1000);
        GetProperty<Client>(nameof(Client.Allergies)).GetMaxLength().Should().Be(500);
        GetProperty<CarePlan>(nameof(CarePlan.Goals)).GetMaxLength().Should().Be(2000);
        GetProperty<VisitNote>(nameof(VisitNote.Activities)).GetMaxLength().Should().Be(4000);
        GetProperty<VisitPhoto>(nameof(VisitPhoto.PhotoUrl)).GetMaxLength().Should().Be(500);
        GetProperty<Invoice>(nameof(Invoice.InvoiceNumber)).GetMaxLength().Should().Be(50);
        GetProperty<InvoiceLineItem>(nameof(InvoiceLineItem.Description)).GetMaxLength().Should().Be(500);
        GetProperty<Payment>(nameof(Payment.ReferenceNumber)).GetMaxLength().Should().Be(100);
        GetProperty<DischargeDocument>(nameof(DischargeDocument.SourceReference)).GetMaxLength().Should().Be(200);
        GetProperty<TransitionPlan>(nameof(TransitionPlan.HospitalName)).GetMaxLength().Should().Be(100);
        GetProperty<TransitionInstruction>(nameof(TransitionInstruction.InstructionText)).GetMaxLength().Should().Be(2000);
        GetProperty<TransitionInstruction>(nameof(TransitionInstruction.ClinicalNote)).GetMaxLength().Should().Be(2000);
        GetProperty<TransitionEscalation>(nameof(TransitionEscalation.TriggerDetails)).GetMaxLength().Should().Be(1000);
        GetProperty<TransitionEscalation>(nameof(TransitionEscalation.ResolutionNote)).GetMaxLength().Should().Be(2000);
    }

    [Fact]
    public void TransitionPhiContentColumns_WhenConfigured_UseOnlyApprovedUnboundedStorage()
    {
        // Assert
        GetProperty<DischargeDocument>(nameof(DischargeDocument.RawContent)).GetColumnType().Should().Be("nvarchar(max)");
        GetProperty<TransitionInstruction>(nameof(TransitionInstruction.SourceText)).GetColumnType().Should().Be("nvarchar(max)");
        GetProperty<TransitionCheckIn>(nameof(TransitionCheckIn.ResponsesJson)).GetColumnType().Should().Be("nvarchar(max)");
    }

    [Fact]
    public void MoneyProperties_WhenConfigured_UseDecimalPrecision()
    {
        // Assert
        AssertPrecision<Client>(nameof(Client.HourlyBillRate), 18, 2);
        AssertPrecision<Caregiver>(nameof(Caregiver.HourlyPayRate), 18, 2);
        AssertPrecision<Shift>(nameof(Shift.BillRate), 18, 2);
        AssertPrecision<Shift>(nameof(Shift.PayRate), 18, 2);
        AssertPrecision<Invoice>(nameof(Invoice.TaxAmount), 18, 2);
        AssertPrecision<InvoiceLineItem>(nameof(InvoiceLineItem.BillableHours), 18, 2);
        AssertPrecision<InvoiceLineItem>(nameof(InvoiceLineItem.RatePerHour), 18, 2);
        AssertPrecision<Payment>(nameof(Payment.Amount), 18, 2);
        AssertPrecision<TransitionInstruction>(nameof(TransitionInstruction.ConfidenceScore), 5, 4);
    }

    [Fact]
    public void ComputedProperties_WhenConfigured_AreNotMapped()
    {
        // Assert
        AssertPropertyNotMapped<User>(nameof(User.FullName));
        AssertPropertyNotMapped<Client>(nameof(Client.Age));
        AssertPropertyNotMapped<ClientAccessGrant>(nameof(ClientAccessGrant.IsRevoked));
        AssertPropertyNotMapped<CaregiverCertification>(nameof(CaregiverCertification.IsExpired));
        AssertPropertyNotMapped<CaregiverCertification>(nameof(CaregiverCertification.IsExpiringSoon));
        AssertPropertyNotMapped<Shift>(nameof(Shift.BillableHours));
        AssertPropertyNotMapped<Shift>(nameof(Shift.GrossMargin));
        AssertPropertyNotMapped<Shift>(nameof(Shift.GrossMarginPercentage));
        AssertPropertyNotMapped<Invoice>(nameof(Invoice.Subtotal));
        AssertPropertyNotMapped<Invoice>(nameof(Invoice.Total));
        AssertPropertyNotMapped<Invoice>(nameof(Invoice.AmountPaid));
        AssertPropertyNotMapped<Invoice>(nameof(Invoice.Balance));
        AssertPropertyNotMapped<InvoiceLineItem>(nameof(InvoiceLineItem.Total));
        AssertPropertyNotMapped<InvoiceLineItem>(nameof(InvoiceLineItem.TotalCost));
        AssertPropertyNotMapped<InvoiceLineItem>(nameof(InvoiceLineItem.GrossProfit));
        AssertPropertyNotMapped<InvoiceLineItem>(nameof(InvoiceLineItem.GrossMarginPercentage));
        AssertPropertyNotMapped<TransitionPlan>(nameof(TransitionPlan.IsActive));
        AssertPropertyNotMapped<TransitionPlan>(nameof(TransitionPlan.DaysRemaining));
        AssertPropertyNotMapped<TransitionInstruction>(nameof(TransitionInstruction.IsLowConfidence));
        AssertPropertyNotMapped<TransitionReminder>(nameof(TransitionReminder.IsOverdue));
    }


    [Fact]
    public void EntityTypes_WhenConfigured_HaveExpectedTableNames()
    {
        // Assert
        AssertTableName<User>("Users");
        AssertTableName<Caregiver>("Caregivers");
        AssertTableName<CaregiverCertification>("CaregiverCertifications");
        AssertTableName<Client>("Clients");
        AssertTableName<ClientAccessGrant>("ClientAccessGrants");
        AssertTableName<CarePlan>("CarePlans");
        AssertTableName<Shift>("Shifts");
        AssertTableName<VisitNote>("VisitNotes");
        AssertTableName<VisitPhoto>("VisitPhotos");
        AssertTableName<Invoice>("Invoices");
        AssertTableName<InvoiceLineItem>("InvoiceLineItems");
        AssertTableName<Payment>("Payments");
        AssertTableName<DischargeDocument>("DischargeDocuments");
        AssertTableName<TransitionPlan>("TransitionPlans");
        AssertTableName<TransitionInstruction>("TransitionInstructions");
        AssertTableName<TransitionReminder>("TransitionReminders");
        AssertTableName<TransitionCheckIn>("TransitionCheckIns");
        AssertTableName<TransitionEscalation>("TransitionEscalations");
    }

    [Fact]
    public void EntityTypes_WhenConfigured_HaveKeyIndexes()
    {
        // Assert
        AssertHasIndex<User>(nameof(User.Email));
        AssertHasIndex<Client>(nameof(Client.UserId));
        AssertHasIndex<ClientAccessGrant>(nameof(ClientAccessGrant.GranteeUserId));
        AssertHasIndex<ClientAccessGrant>(nameof(ClientAccessGrant.ClientId));
        AssertHasIndex<Caregiver>(nameof(Caregiver.UserId));
        AssertHasIndex<CaregiverCertification>(nameof(CaregiverCertification.ExpirationDate));
        AssertHasIndex<Shift>(nameof(Shift.ScheduledStartTime));
        AssertHasIndex<VisitNote>(nameof(VisitNote.VisitDateTime));
        AssertHasIndex<VisitPhoto>(nameof(VisitPhoto.TakenAt));
        AssertHasIndex<Invoice>(nameof(Invoice.InvoiceNumber));
        AssertHasIndex<InvoiceLineItem>(nameof(InvoiceLineItem.ServiceDate));
        AssertHasIndex<Payment>(nameof(Payment.PaymentDate));
        AssertHasIndex<DischargeDocument>(nameof(DischargeDocument.ClientId));
        AssertHasIndex<DischargeDocument>(nameof(DischargeDocument.Status));
        AssertHasIndex<TransitionPlan>(nameof(TransitionPlan.ClientId));
        AssertHasIndex<TransitionPlan>(nameof(TransitionPlan.Status));
        AssertHasIndex<TransitionInstruction>(nameof(TransitionInstruction.Status));
        AssertHasIndex<TransitionReminder>(nameof(TransitionReminder.Status));
        AssertHasIndex<TransitionReminder>(nameof(TransitionReminder.ScheduledAt));
        AssertHasIndex<VisitNote>(nameof(VisitNote.TransitionPlanId));
    }

    [Fact]
    public void DateTimeProperties_WhenConfigured_UseDateTime2ColumnsAndConverters()
    {
        // Assert
        AssertDateTimeConfigured<User>(nameof(User.CreatedAt));
        AssertDateTimeConfigured<User>(nameof(User.LastLoginAt));
        AssertDateTimeConfigured<Client>(nameof(Client.DateOfBirth));
        AssertDateTimeConfigured<ClientAccessGrant>(nameof(ClientAccessGrant.GrantedAtUtc));
        AssertDateTimeConfigured<ClientAccessGrant>(nameof(ClientAccessGrant.RevokedAtUtc));
        AssertDateTimeConfigured<Shift>(nameof(Shift.ScheduledStartTime));
        AssertDateTimeConfigured<Shift>(nameof(Shift.ActualStartTime));
        AssertDateTimeConfigured<VisitNote>(nameof(VisitNote.VisitDateTime));
        AssertDateTimeConfigured<Invoice>(nameof(Invoice.InvoiceDate));
        AssertDateTimeConfigured<Payment>(nameof(Payment.PaymentDate));
        AssertDateTimeConfigured<DischargeDocument>(nameof(DischargeDocument.UploadedAt));
        AssertDateTimeConfigured<TransitionPlan>(nameof(TransitionPlan.DischargeDate));
        AssertDateTimeConfigured<TransitionPlan>(nameof(TransitionPlan.TransitionWindowEnd));
        AssertDateTimeConfigured<TransitionReminder>(nameof(TransitionReminder.ScheduledAt));
        AssertDateTimeConfigured<TransitionCheckIn>(nameof(TransitionCheckIn.CheckInDate));
        AssertDateTimeConfigured<TransitionEscalation>(nameof(TransitionEscalation.EscalatedAt));
    }

    [Fact]
    public void BaseEntityTypes_WhenConfigured_HaveSoftDeleteQueryFilters()
    {
        // Assert
        var entityTypes = new[]
        {
            typeof(User),
            typeof(Caregiver),
            typeof(CaregiverCertification),
            typeof(Client),
            typeof(ClientAccessGrant),
            typeof(CarePlan),
            typeof(Shift),
            typeof(VisitNote),
            typeof(VisitPhoto),
            typeof(Invoice),
            typeof(InvoiceLineItem),
            typeof(Payment),
            typeof(DischargeDocument),
            typeof(TransitionPlan),
            typeof(TransitionInstruction),
            typeof(TransitionReminder),
            typeof(TransitionCheckIn),
            typeof(TransitionEscalation)
        };

        foreach (var entityType in entityTypes)
        {
            _model.FindEntityType(entityType)!.GetQueryFilter().Should().NotBeNull(entityType.Name);
        }
    }


    [Fact]
    public void ApplicationUser_WhenConfigured_HasMatchingDomainUserQueryFilter()
    {
        // Assert
        _model.FindEntityType(typeof(ApplicationUser))!
            .GetQueryFilter()
            .Should()
            .NotBeNull();
    }

    [Fact]
    public void TransitionPlanNavigationCollections_WhenConfigured_UseFieldAccess()
    {
        // Arrange
        var entityType = _model.FindEntityType(typeof(TransitionPlan))!;

        // Assert
        entityType.FindNavigation(nameof(TransitionPlan.Instructions))!.GetPropertyAccessMode()
            .Should().Be(PropertyAccessMode.Field);
        entityType.FindNavigation(nameof(TransitionPlan.Reminders))!.GetPropertyAccessMode()
            .Should().Be(PropertyAccessMode.Field);
        entityType.FindNavigation(nameof(TransitionPlan.CheckIns))!.GetPropertyAccessMode()
            .Should().Be(PropertyAccessMode.Field);
        entityType.FindNavigation(nameof(TransitionPlan.Escalations))!.GetPropertyAccessMode()
            .Should().Be(PropertyAccessMode.Field);
    }

    private DeleteBehavior GetDeleteBehavior<TEntity>(string foreignKeyPropertyName)
    {
        var entityType = _model.FindEntityType(typeof(TEntity))!;
        var foreignKey = entityType
            .GetForeignKeys()
            .Single(key => key.Properties.Any(property => property.Name == foreignKeyPropertyName));

        return foreignKey.DeleteBehavior;
    }

    private IProperty GetProperty<TEntity>(string propertyName) =>
        _model.FindEntityType(typeof(TEntity))!.FindProperty(propertyName)!;

    private void AssertPrecision<TEntity>(string propertyName, int precision, int scale)
    {
        var property = GetProperty<TEntity>(propertyName);

        property.GetPrecision().Should().Be(precision);
        property.GetScale().Should().Be(scale);
    }

    private void AssertPropertyNotMapped<TEntity>(string propertyName) =>
        _model.FindEntityType(typeof(TEntity))!.FindProperty(propertyName).Should().BeNull();

    private void AssertTableName<TEntity>(string tableName) =>
        _model.FindEntityType(typeof(TEntity))!.GetTableName().Should().Be(tableName);

    private void AssertHasIndex<TEntity>(string propertyName)
    {
        var entityType = _model.FindEntityType(typeof(TEntity))!;

        entityType.GetIndexes()
            .Should()
            .Contain(index => index.Properties.Any(property => property.Name == propertyName));
    }

    private void AssertDateTimeConfigured<TEntity>(string propertyName)
    {
        var property = GetProperty<TEntity>(propertyName);

        property.GetColumnType().Should().Be("datetime2");
        property.GetValueConverter().Should().NotBeNull();
    }

    private static IModel CreateModel()
    {
        var options = new DbContextOptionsBuilder<CarePathDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=CarePath_MetadataOnly;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True")
            .Options;
        var interceptor = new AuditableEntityInterceptor(new HttpContextAccessor());

        using var context = new CarePathDbContext(options, interceptor);
        return context.Model;
    }
}
