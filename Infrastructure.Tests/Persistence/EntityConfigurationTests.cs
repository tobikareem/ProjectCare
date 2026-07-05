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
    public void Model_WhenBuilt_DoesNotIncludeTransitionsEntities()
    {
        // Assert
        _model.FindEntityType(typeof(DischargeDocument)).Should().BeNull();
        _model.FindEntityType(typeof(TransitionPlan)).Should().BeNull();
        _model.FindEntityType(typeof(TransitionInstruction)).Should().BeNull();
        _model.FindEntityType(typeof(TransitionReminder)).Should().BeNull();
        _model.FindEntityType(typeof(TransitionCheckIn)).Should().BeNull();
        _model.FindEntityType(typeof(TransitionEscalation)).Should().BeNull();
    }

    [Fact]
    public void PhiRelationships_WhenConfigured_UseRetentionSafeDeleteBehavior()
    {
        // Assert
        GetDeleteBehavior<CaregiverCertification>(nameof(CaregiverCertification.CaregiverId))
            .Should().Be(DeleteBehavior.Restrict);
        GetDeleteBehavior<Client>(nameof(Client.UserId)).Should().Be(DeleteBehavior.Restrict);
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
    }

    [Fact]
    public void ComputedProperties_WhenConfigured_AreNotMapped()
    {
        // Assert
        AssertPropertyNotMapped<User>(nameof(User.FullName));
        AssertPropertyNotMapped<Client>(nameof(Client.Age));
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
        AssertPropertyNotMapped<VisitNote>(nameof(VisitNote.TransitionPlanId));
    }


    [Fact]
    public void EntityTypes_WhenConfigured_HaveExpectedTableNames()
    {
        // Assert
        AssertTableName<User>("Users");
        AssertTableName<Caregiver>("Caregivers");
        AssertTableName<CaregiverCertification>("CaregiverCertifications");
        AssertTableName<Client>("Clients");
        AssertTableName<CarePlan>("CarePlans");
        AssertTableName<Shift>("Shifts");
        AssertTableName<VisitNote>("VisitNotes");
        AssertTableName<VisitPhoto>("VisitPhotos");
        AssertTableName<Invoice>("Invoices");
        AssertTableName<InvoiceLineItem>("InvoiceLineItems");
        AssertTableName<Payment>("Payments");
    }

    [Fact]
    public void EntityTypes_WhenConfigured_HaveKeyIndexes()
    {
        // Assert
        AssertHasIndex<User>(nameof(User.Email));
        AssertHasIndex<Client>(nameof(Client.UserId));
        AssertHasIndex<Caregiver>(nameof(Caregiver.UserId));
        AssertHasIndex<CaregiverCertification>(nameof(CaregiverCertification.ExpirationDate));
        AssertHasIndex<Shift>(nameof(Shift.ScheduledStartTime));
        AssertHasIndex<VisitNote>(nameof(VisitNote.VisitDateTime));
        AssertHasIndex<VisitPhoto>(nameof(VisitPhoto.TakenAt));
        AssertHasIndex<Invoice>(nameof(Invoice.InvoiceNumber));
        AssertHasIndex<InvoiceLineItem>(nameof(InvoiceLineItem.ServiceDate));
        AssertHasIndex<Payment>(nameof(Payment.PaymentDate));
    }

    [Fact]
    public void DateTimeProperties_WhenConfigured_UseDateTime2ColumnsAndConverters()
    {
        // Assert
        AssertDateTimeConfigured<User>(nameof(User.CreatedAt));
        AssertDateTimeConfigured<User>(nameof(User.LastLoginAt));
        AssertDateTimeConfigured<Client>(nameof(Client.DateOfBirth));
        AssertDateTimeConfigured<Shift>(nameof(Shift.ScheduledStartTime));
        AssertDateTimeConfigured<Shift>(nameof(Shift.ActualStartTime));
        AssertDateTimeConfigured<VisitNote>(nameof(VisitNote.VisitDateTime));
        AssertDateTimeConfigured<Invoice>(nameof(Invoice.InvoiceDate));
        AssertDateTimeConfigured<Payment>(nameof(Payment.PaymentDate));
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
            typeof(CarePlan),
            typeof(Shift),
            typeof(VisitNote),
            typeof(VisitPhoto),
            typeof(Invoice),
            typeof(InvoiceLineItem),
            typeof(Payment)
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


