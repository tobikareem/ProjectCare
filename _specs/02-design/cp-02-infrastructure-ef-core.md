# Design Specification: CP-02 Infrastructure & EF Core Layer

**Date**: 2026-02-25
**Author**: Tobi Kareem
**Project**: CarePath Health
**Status**: Draft
**Related Specs**:
- [CP-01 Design](./cp-01-create-domain-entities.md) - Domain entities and repository interfaces
- [CP-01 Requirements](../01-requirements/cp-01-create-domain-entities.md) - Business requirements
- [CLAUDE.md](../../CLAUDE.md) - Coding conventions and workflow
- [Migration Workflow](../../.claude/commands/migration.md) - EF Core migration procedures

---

## Executive Summary

> Design and implement the Infrastructure layer for CarePath Health using Entity Framework Core 9 and SQL Server, providing:
>
> 1. **CarePathDbContext** — EF Core DbContext inheriting from `IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>` with 12 domain entities + ASP.NET Core Identity tables
> 2. **Entity Type Configurations** — Fluent API configurations for all entities (string lengths, decimal precision, relationships, indexes, global soft-delete query filter, UTC DateTime converters)
> 3. **AuditableEntityInterceptor** — `SaveChangesInterceptor` to automatically populate `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy` on all domain entities
> 4. **Generic Repository Implementation** — `Repository<T>` implementing `IRepository<T>` with CRUD, pagination, and soft-delete support
> 5. **Unit of Work Implementation** — `UnitOfWork` implementing `IUnitOfWork` with lazy-initialized repositories and transaction management
> 6. **Dependency Injection** — Extension method to register DbContext, Identity, repositories, and interceptors
> 7. **Initial Migration Strategy** — "InitialCreate" migration with all tables and seed data (default admin user)
> 8. **Testing Strategy** — Integration test examples using in-memory and SQL Server Test Containers

This spec emphasizes **HIPAA compliance**, **soft-delete enforcement**, **UTC datetime handling**, and **clean separation** between domain and infrastructure concerns.

---

## 1. Architecture Overview

### 1.1 Infrastructure Layer in Clean Architecture

```
┌─────────────────────────────────────────────────────┐
│                   WebApi Layer                       │
│            (Controllers, SignalR Hubs, etc.)        │
└──────────────────────┬──────────────────────────────┘
                       │
                       │ Depends on Application & Infrastructure
                       │
┌──────────────────────┴──────────────────────────────┐
│              Application Layer                       │
│      (Services, DTOs, Validators, AutoMapper)      │
└──────────────────────┬──────────────────────────────┘
                       │
                       │ Depends on Domain & Infrastructure
                       │
┌──────────────────────┴──────────────────────────────┐
│        Infrastructure Layer (This Spec)             │
├──────────────────────────────────────────────────────┤
│                                                       │
│  ┌────────────────────────────────────────┐         │
│  │  Persistence (EF Core)                  │         │
│  │                                         │         │
│  │  • CarePathDbContext                   │         │
│  │  • Entity Configurations                │         │
│  │  • AuditableEntityInterceptor           │         │
│  │  • Repository<T>                        │         │
│  │  • UnitOfWork                           │         │
│  │  • Migrations                           │         │
│  └────────────────────────────────────────┘         │
│                                                       │
│  ┌────────────────────────────────────────┐         │
│  │  Identity                               │         │
│  │                                         │         │
│  │  • ApplicationUser (extends             │         │
│  │    IdentityUser<Guid>)                 │         │
│  │  • ApplicationUserConfiguration         │         │
│  └────────────────────────────────────────┘         │
│                                                       │
│  ┌────────────────────────────────────────┐         │
│  │  External Services (Future)             │         │
│  │                                         │         │
│  │  • Email, SMS, Storage, etc.           │         │
│  └────────────────────────────────────────┘         │
│                                                       │
│  DependencyInjection.cs (service registration)       │
└──────────────────────┬───────────────────────────────┘
                       │
                       │ Depends on Domain only
                       │
┌──────────────────────┴───────────────────────────────┐
│               Domain Layer                           │
│  (Entities, Enums, Repository Interfaces)          │
└─────────────────────────────────────────────────────┘
                       │
                       ▼
              SQL Server Database
```

### 1.2 Key Design Principles

- **No Domain Pollution**: Infrastructure imports Domain but Domain never imports Infrastructure
- **Soft Delete Enforcement**: Global query filter on all entities; no hard deletes via `DbSet.Remove()`
- **UTC DateTime Guarantee**: ValueConverter ensures `DateTime.UtcNow` is preserved through SQL Server round-trips
- **HIPAA Compliance**: Role-based authorization, audit logging, encryption-at-rest (SQL Server TDE), no PHI in URLs/logs
- **Lazy Repository Initialization**: Repositories created on first access, not at construction
- **Pattern A Identity Integration**: Separate `ApplicationUser` table linked to domain `User` via `DomainUserId` FK

---

## 2. Project Structure

```
src/CarePath.Infrastructure/
├── Persistence/
│   ├── CarePathDbContext.cs                          # DbContext with all DbSets + Identity
│   ├── Configurations/
│   │   ├── Identity/
│   │   │   ├── ApplicationUserConfiguration.cs      # ApplicationUser FluentAPI config
│   │   │   ├── UserConfiguration.cs                 # Domain User FluentAPI config
│   │   │   ├── CaregiverConfiguration.cs
│   │   │   ├── CaregiverCertificationConfiguration.cs
│   │   │   ├── ClientConfiguration.cs
│   │   │   └── CarePlanConfiguration.cs
│   │   ├── Scheduling/
│   │   │   ├── ShiftConfiguration.cs
│   │   │   ├── VisitNoteConfiguration.cs
│   │   │   └── VisitPhotoConfiguration.cs
│   │   └── Billing/
│   │       ├── InvoiceConfiguration.cs
│   │       ├── InvoiceLineItemConfiguration.cs
│   │       └── PaymentConfiguration.cs
│   ├── Interceptors/
│   │   └── AuditableEntityInterceptor.cs            # SaveChangesInterceptor for audit fields
│   ├── Converters/
│   │   └── UtcDateTimeConverter.cs                  # DateTime UTC round-trip converter
│   ├── Repositories/
│   │   ├── Repository.cs                            # Generic IRepository<T> implementation
│   │   └── UnitOfWork.cs                            # IUnitOfWork implementation
│   └── Migrations/
│       ├── 20260225000000_InitialCreate.cs          # Initial migration
│       ├── 20260225000000_InitialCreate.Designer.cs
│       └── CarePathDbContextModelSnapshot.cs
├── Identity/
│   └── ApplicationUser.cs                            # ASP.NET Core Identity user
├── DependencyInjection.cs                            # Service registration
└── CarePath.Infrastructure.csproj                   # Project file
```

---

## 3. CarePathDbContext

**File**: `src/CarePath.Infrastructure/Persistence/CarePathDbContext.cs`

```csharp
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using CarePath.Domain.Entities.Common;
using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Entities.Scheduling;
using CarePath.Domain.Entities.Billing;
using CarePath.Infrastructure.Identity;
using CarePath.Infrastructure.Persistence.Configurations.Identity;
using CarePath.Infrastructure.Persistence.Configurations.Scheduling;
using CarePath.Infrastructure.Persistence.Configurations.Billing;
using CarePath.Infrastructure.Persistence.Interceptors;

namespace CarePath.Infrastructure.Persistence;

/// <summary>
/// Main database context for CarePath Health platform.
/// Inherits from IdentityDbContext to support ASP.NET Core Identity with Guid primary keys.
/// Applies global soft-delete query filter and audit field interceptor.
/// </summary>
public class CarePathDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    private readonly AuditableEntityInterceptor _auditableEntityInterceptor;

    // Domain Entities

    /// <summary>Base users (Admin, Coordinator, Caregiver, Client, FacilityManager).</summary>
    public DbSet<User> Users { get; set; } = null!;

    // Identity
    /// <summary>Caregiver profiles (W-2 employees or 1099 contractors).</summary>
    public DbSet<Caregiver> Caregivers { get; set; } = null!;

    /// <summary>Caregiver certifications (CNA, LPN, RN, etc.) with expiration tracking.</summary>
    public DbSet<CaregiverCertification> CaregiverCertifications { get; set; } = null!;

    /// <summary>Client profiles (care recipients).</summary>
    public DbSet<Client> Clients { get; set; } = null!;

    /// <summary>Care plans documenting goals and interventions.</summary>
    public DbSet<CarePlan> CarePlans { get; set; } = null!;

    // Scheduling
    /// <summary>Scheduled care sessions with GPS tracking and margin data.</summary>
    public DbSet<Shift> Shifts { get; set; } = null!;

    /// <summary>Care activity documentation for shifts.</summary>
    public DbSet<VisitNote> VisitNotes { get; set; } = null!;

    /// <summary>Photos attached to visit notes.</summary>
    public DbSet<VisitPhoto> VisitPhotos { get; set; } = null!;

    // Billing
    /// <summary>Invoices issued to clients.</summary>
    public DbSet<Invoice> Invoices { get; set; } = null!;

    /// <summary>Line items on invoices (typically one per shift).</summary>
    public DbSet<InvoiceLineItem> InvoiceLineItems { get; set; } = null!;

    /// <summary>Payment records toward invoices.</summary>
    public DbSet<Payment> Payments { get; set; } = null!;

    public CarePathDbContext(DbContextOptions<CarePathDbContext> options, AuditableEntityInterceptor auditableEntityInterceptor)
        : base(options)
    {
        _auditableEntityInterceptor = auditableEntityInterceptor;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.AddInterceptors(_auditableEntityInterceptor);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all entity configurations
        modelBuilder.ApplyConfiguration(new ApplicationUserConfiguration());
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new CaregiverConfiguration());
        modelBuilder.ApplyConfiguration(new CaregiverCertificationConfiguration());
        modelBuilder.ApplyConfiguration(new ClientConfiguration());
        modelBuilder.ApplyConfiguration(new CarePlanConfiguration());

        modelBuilder.ApplyConfiguration(new ShiftConfiguration());
        modelBuilder.ApplyConfiguration(new VisitNoteConfiguration());
        modelBuilder.ApplyConfiguration(new VisitPhotoConfiguration());

        modelBuilder.ApplyConfiguration(new InvoiceConfiguration());
        modelBuilder.ApplyConfiguration(new InvoiceLineItemConfiguration());
        modelBuilder.ApplyConfiguration(new PaymentConfiguration());
    }

    /// <summary>
    /// Override SaveChangesAsync to ensure soft-delete and audit fields are set correctly.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await base.SaveChangesAsync(cancellationToken);
    }
}
```

---

## 4. Entity Type Configurations

All configurations use Fluent API (no data annotations). Each configuration demonstrates full setup for its entity.

### 4.1 UTC DateTime Converter

**File**: `src/CarePath.Infrastructure/Persistence/Converters/UtcDateTimeConverter.cs`

```csharp
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CarePath.Infrastructure.Persistence.Converters;

/// <summary>
/// ValueConverter that preserves UTC kind on DateTime round-trips through SQL Server.
/// SQL Server's datetime2 type loses DateTime.Kind information, so we manually restore it.
/// </summary>
public class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter()
        : base(
            v => v,
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
    {
    }
}

/// <summary>
/// ValueConverter for nullable DateTime (DateTime?) UTC preservation.
/// </summary>
public class UtcDateTimeNullableConverter : ValueConverter<DateTime?, DateTime?>
{
    public UtcDateTimeNullableConverter()
        : base(
            v => v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : null)
    {
    }
}
```

### 4.2 AuditableEntityInterceptor

**File**: `src/CarePath.Infrastructure/Persistence/Interceptors/AuditableEntityInterceptor.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using CarePath.Domain.Entities.Common;

namespace CarePath.Infrastructure.Persistence.Interceptors;

/// <summary>
/// SaveChangesInterceptor that automatically populates audit fields (CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
/// on all BaseEntity instances when saving changes.
/// Requires IHttpContextAccessor to get the current user ID.
/// </summary>
public class AuditableEntityInterceptor : SaveChangesInterceptor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditableEntityInterceptor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Gets the current user ID from HttpContext.User.FindFirst("sub")?.Value or HttpContext.User.Identity.Name.
    /// Returns "System" if no authenticated user.
    /// </summary>
    private string GetCurrentUserId()
    {
        var userId = _httpContextAccessor.HttpContext?.User.FindFirst("sub")?.Value
            ?? _httpContextAccessor.HttpContext?.User.Identity?.Name
            ?? "System";

        return userId;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var dbContext = eventData.Context;

        if (dbContext == null)
            return base.SavingChangesAsync(eventData, result, cancellationToken);

        var entries = dbContext.ChangeTracker
            .Entries<BaseEntity>()
            .ToList();

        var currentUserId = GetCurrentUserId();

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = DateTime.UtcNow;
                entry.Entity.CreatedBy = currentUserId;
                entry.Entity.UpdatedAt = null;
                entry.Entity.UpdatedBy = null;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;
                entry.Entity.UpdatedBy = currentUserId;
            }
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
```

### 4.3 User Configuration

**File**: `src/CarePath.Infrastructure/Persistence/Configurations/Identity/UserConfiguration.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CarePath.Domain.Entities.Identity;
using CarePath.Infrastructure.Persistence.Converters;

namespace CarePath.Infrastructure.Persistence.Configurations.Identity;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(x => x.Id);

        // Properties
        builder.Property(x => x.FirstName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.LastName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.PhoneNumber)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.Address)
            .HasMaxLength(500);

        builder.Property(x => x.City)
            .HasMaxLength(100);

        builder.Property(x => x.State)
            .HasMaxLength(50);

        builder.Property(x => x.ZipCode)
            .HasMaxLength(10);

        builder.Property(x => x.Role)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true);

        // DateTime conversions (preserve UTC)
        builder.Property(x => x.CreatedAt)
            .HasConversion(new UtcDateTimeConverter());

        builder.Property(x => x.UpdatedAt)
            .HasConversion(new UtcDateTimeNullableConverter());

        builder.Property(x => x.LastLoginAt)
            .HasConversion(new UtcDateTimeNullableConverter());

        // Indexes
        builder.HasIndex(x => x.Email)
            .IsUnique()
            .HasDatabaseName("IX_Users_Email");

        builder.HasIndex(x => x.CreatedAt)
            .HasDatabaseName("IX_Users_CreatedAt");

        // Global query filter for soft delete
        builder.HasQueryFilter(x => !x.IsDeleted);

        // Ignore computed properties
        builder.Ignore(x => x.FullName);
    }
}
```

### 4.4 Caregiver Configuration

**File**: `src/CarePath.Infrastructure/Persistence/Configurations/Identity/CaregiverConfiguration.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CarePath.Domain.Entities.Identity;
using CarePath.Infrastructure.Persistence.Converters;

namespace CarePath.Infrastructure.Persistence.Configurations.Identity;

public class CaregiverConfiguration : IEntityTypeConfiguration<Caregiver>
{
    public void Configure(EntityTypeBuilder<Caregiver> builder)
    {
        builder.ToTable("Caregivers");

        builder.HasKey(x => x.Id);

        // Foreign Key to User
        builder.HasOne(x => x.User)
            .WithOne()
            .HasForeignKey<Caregiver>(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Properties
        builder.Property(x => x.HourlyPayRate)
            .HasPrecision(18, 2);

        builder.Property(x => x.MaxWeeklyHours)
            .HasDefaultValue(40);

        builder.Property(x => x.AverageRating)
            .HasPrecision(3, 2);

        // DateTime conversions
        builder.Property(x => x.CreatedAt)
            .HasConversion(new UtcDateTimeConverter());

        builder.Property(x => x.UpdatedAt)
            .HasConversion(new UtcDateTimeNullableConverter());

        builder.Property(x => x.HireDate)
            .HasConversion(new UtcDateTimeConverter());

        builder.Property(x => x.TerminationDate)
            .HasConversion(new UtcDateTimeNullableConverter());

        // Relationships
        builder.HasMany(x => x.Certifications)
            .WithOne(x => x.Caregiver)
            .HasForeignKey(x => x.CaregiverId)
            .OnDelete(DeleteBehavior.Restrict); // PHI entity — HIPAA requires Restrict

        builder.HasMany(x => x.Shifts)
            .WithOne(x => x.Caregiver)
            .HasForeignKey(x => x.CaregiverId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.VisitNotes)
            .WithOne(x => x.Caregiver)
            .HasForeignKey(x => x.CaregiverId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(x => x.UserId)
            .IsUnique()
            .HasDatabaseName("IX_Caregivers_UserId");

        builder.HasIndex(x => x.HireDate)
            .HasDatabaseName("IX_Caregivers_HireDate");

        // Global query filter
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
```

### 4.5 CaregiverCertification Configuration

**File**: `src/CarePath.Infrastructure/Persistence/Configurations/Identity/CaregiverCertificationConfiguration.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CarePath.Domain.Entities.Identity;
using CarePath.Infrastructure.Persistence.Converters;

namespace CarePath.Infrastructure.Persistence.Configurations.Identity;

public class CaregiverCertificationConfiguration : IEntityTypeConfiguration<CaregiverCertification>
{
    public void Configure(EntityTypeBuilder<CaregiverCertification> builder)
    {
        builder.ToTable("CaregiverCertifications");

        builder.HasKey(x => x.Id);

        // Foreign Key to Caregiver
        builder.HasOne(x => x.Caregiver)
            .WithMany(x => x.Certifications)
            .HasForeignKey(x => x.CaregiverId)
            .OnDelete(DeleteBehavior.Restrict); // PHI entity — HIPAA requires Restrict

        // Properties
        builder.Property(x => x.CertificationNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.IssuingAuthority)
            .IsRequired()
            .HasMaxLength(100);

        // DateTime conversions
        builder.Property(x => x.CreatedAt)
            .HasConversion(new UtcDateTimeConverter());

        builder.Property(x => x.UpdatedAt)
            .HasConversion(new UtcDateTimeNullableConverter());

        builder.Property(x => x.IssueDate)
            .HasConversion(new UtcDateTimeConverter());

        builder.Property(x => x.ExpirationDate)
            .HasConversion(new UtcDateTimeConverter());

        // Indexes
        builder.HasIndex(x => x.CaregiverId)
            .HasDatabaseName("IX_CaregiverCertifications_CaregiverId");

        builder.HasIndex(x => x.ExpirationDate)
            .HasDatabaseName("IX_CaregiverCertifications_ExpirationDate");

        // Global query filter
        builder.HasQueryFilter(x => !x.IsDeleted);

        // Ignore computed properties
        builder.Ignore(x => x.IsExpired);
        builder.Ignore(x => x.IsExpiringSoon);
    }
}
```

### 4.6 Client Configuration

**File**: `src/CarePath.Infrastructure/Persistence/Configurations/Identity/ClientConfiguration.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CarePath.Domain.Entities.Identity;
using CarePath.Infrastructure.Persistence.Converters;

namespace CarePath.Infrastructure.Persistence.Configurations.Identity;

public class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("Clients");

        builder.HasKey(x => x.Id);

        // Foreign Key to User (1:1)
        builder.HasOne(x => x.User)
            .WithOne()
            .HasForeignKey<Client>(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Properties
        builder.Property(x => x.EmergencyContactName)
            .HasMaxLength(100);

        builder.Property(x => x.EmergencyContactPhone)
            .HasMaxLength(20);

        builder.Property(x => x.EmergencyContactRelationship)
            .HasMaxLength(100);

        builder.Property(x => x.SpecialInstructions)
            .HasMaxLength(1000);

        builder.Property(x => x.MedicalConditions)
            .HasMaxLength(1000);

        builder.Property(x => x.Allergies)
            .HasMaxLength(500);

        builder.Property(x => x.HourlyBillRate)
            .HasPrecision(18, 2);

        builder.Property(x => x.LocationNotes)
            .HasMaxLength(500);

        builder.Property(x => x.InsuranceProvider)
            .HasMaxLength(100);

        builder.Property(x => x.InsurancePolicyNumber)
            .HasMaxLength(50);

        builder.Property(x => x.MedicaidNumber)
            .HasMaxLength(50);

        // DateTime conversions
        builder.Property(x => x.CreatedAt)
            .HasConversion(new UtcDateTimeConverter());

        builder.Property(x => x.UpdatedAt)
            .HasConversion(new UtcDateTimeNullableConverter());

        builder.Property(x => x.DateOfBirth)
            .HasConversion(new UtcDateTimeConverter());

        // Relationships
        builder.HasMany(x => x.Shifts)
            .WithOne(x => x.Client)
            .HasForeignKey(x => x.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.CarePlans)
            .WithOne(x => x.Client)
            .HasForeignKey(x => x.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Invoices)
            .WithOne(x => x.Client)
            .HasForeignKey(x => x.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(x => x.UserId)
            .IsUnique()
            .HasDatabaseName("IX_Clients_UserId");

        builder.HasIndex(x => x.DateOfBirth)
            .HasDatabaseName("IX_Clients_DateOfBirth");

        // Global query filter
        builder.HasQueryFilter(x => !x.IsDeleted);

        // Ignore computed properties
        builder.Ignore(x => x.Age);
    }
}
```

### 4.7 CarePlan Configuration

**File**: `src/CarePath.Infrastructure/Persistence/Configurations/Identity/CarePlanConfiguration.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CarePath.Domain.Entities.Identity;
using CarePath.Infrastructure.Persistence.Converters;

namespace CarePath.Infrastructure.Persistence.Configurations.Identity;

public class CarePlanConfiguration : IEntityTypeConfiguration<CarePlan>
{
    public void Configure(EntityTypeBuilder<CarePlan> builder)
    {
        builder.ToTable("CarePlans");

        builder.HasKey(x => x.Id);

        // Foreign Key to Client
        builder.HasOne(x => x.Client)
            .WithMany(x => x.CarePlans)
            .HasForeignKey(x => x.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        // Properties
        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .HasMaxLength(2000);

        builder.Property(x => x.Goals)
            .HasMaxLength(2000);

        builder.Property(x => x.Interventions)
            .HasMaxLength(2000);

        builder.Property(x => x.Notes)
            .HasMaxLength(2000);

        // DateTime conversions
        builder.Property(x => x.CreatedAt)
            .HasConversion(new UtcDateTimeConverter());

        builder.Property(x => x.UpdatedAt)
            .HasConversion(new UtcDateTimeNullableConverter());

        builder.Property(x => x.StartDate)
            .HasConversion(new UtcDateTimeConverter());

        builder.Property(x => x.EndDate)
            .HasConversion(new UtcDateTimeNullableConverter());

        // Indexes
        builder.HasIndex(x => x.ClientId)
            .HasDatabaseName("IX_CarePlans_ClientId");

        builder.HasIndex(x => x.IsActive)
            .HasDatabaseName("IX_CarePlans_IsActive");

        // Global query filter
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
```

### 4.8 Shift Configuration

**File**: `src/CarePath.Infrastructure/Persistence/Configurations/Scheduling/ShiftConfiguration.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CarePath.Domain.Entities.Scheduling;
using CarePath.Infrastructure.Persistence.Converters;

namespace CarePath.Infrastructure.Persistence.Configurations.Scheduling;

public class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> builder)
    {
        builder.ToTable("Shifts");

        builder.HasKey(x => x.Id);

        // Foreign Keys
        builder.HasOne(x => x.Client)
            .WithMany(x => x.Shifts)
            .HasForeignKey(x => x.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Caregiver)
            .WithMany(x => x.Shifts)
            .HasForeignKey(x => x.CaregiverId)
            .OnDelete(DeleteBehavior.SetNull);

        // Properties
        builder.Property(x => x.BillRate)
            .HasPrecision(18, 2);

        builder.Property(x => x.PayRate)
            .HasPrecision(18, 2);

        builder.Property(x => x.OvertimePayRate)
            .HasPrecision(18, 2);

        builder.Property(x => x.WeekendPremium)
            .HasPrecision(18, 2);

        builder.Property(x => x.HolidayPremium)
            .HasPrecision(18, 2);

        builder.Property(x => x.Notes)
            .HasMaxLength(500);

        builder.Property(x => x.CancellationReason)
            .HasMaxLength(500);

        // GPS coordinates (double? — no precision needed for EF Core double mapping)
        // CheckInLatitude, CheckInLongitude, CheckOutLatitude, CheckOutLongitude
        // are nullable doubles and map to float in SQL Server by default — no explicit config needed

        // DateTime conversions
        builder.Property(x => x.CreatedAt)
            .HasConversion(new UtcDateTimeConverter());

        builder.Property(x => x.UpdatedAt)
            .HasConversion(new UtcDateTimeNullableConverter());

        builder.Property(x => x.ScheduledStartTime)
            .HasConversion(new UtcDateTimeConverter());

        builder.Property(x => x.ScheduledEndTime)
            .HasConversion(new UtcDateTimeConverter());

        builder.Property(x => x.ActualStartTime)
            .HasConversion(new UtcDateTimeNullableConverter());

        builder.Property(x => x.ActualEndTime)
            .HasConversion(new UtcDateTimeNullableConverter());

        builder.Property(x => x.CheckInTime)
            .HasConversion(new UtcDateTimeNullableConverter());

        builder.Property(x => x.CheckOutTime)
            .HasConversion(new UtcDateTimeNullableConverter());

        builder.Property(x => x.CancelledAt)
            .HasConversion(new UtcDateTimeNullableConverter());

        // Relationships
        builder.HasMany(x => x.VisitNotes)
            .WithOne(x => x.Shift)
            .HasForeignKey(x => x.ShiftId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes (critical for querying shifts by date, caregiver, client)
        builder.HasIndex(x => x.ClientId)
            .HasDatabaseName("IX_Shifts_ClientId");

        builder.HasIndex(x => x.CaregiverId)
            .HasDatabaseName("IX_Shifts_CaregiverId");

        builder.HasIndex(x => x.ScheduledStartTime)
            .HasDatabaseName("IX_Shifts_ScheduledStartTime");

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("IX_Shifts_Status");

        // Global query filter
        builder.HasQueryFilter(x => !x.IsDeleted);

        // Ignore computed properties
        builder.Ignore(x => x.ScheduledDuration);
        builder.Ignore(x => x.ActualDuration);
        builder.Ignore(x => x.BillableHours);
        builder.Ignore(x => x.GrossMargin);
        builder.Ignore(x => x.GrossMarginPercentage);
    }
}
```

### 4.9 VisitNote Configuration

**File**: `src/CarePath.Infrastructure/Persistence/Configurations/Scheduling/VisitNoteConfiguration.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CarePath.Domain.Entities.Scheduling;
using CarePath.Infrastructure.Persistence.Converters;

namespace CarePath.Infrastructure.Persistence.Configurations.Scheduling;

public class VisitNoteConfiguration : IEntityTypeConfiguration<VisitNote>
{
    public void Configure(EntityTypeBuilder<VisitNote> builder)
    {
        builder.ToTable("VisitNotes");

        builder.HasKey(x => x.Id);

        // Foreign Keys
        builder.HasOne(x => x.Shift)
            .WithMany(x => x.VisitNotes)
            .HasForeignKey(x => x.ShiftId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Caregiver)
            .WithMany(x => x.VisitNotes)
            .HasForeignKey(x => x.CaregiverId)
            .OnDelete(DeleteBehavior.Restrict);

        // Properties
        builder.Property(x => x.Activities)
            .HasMaxLength(2000);

        builder.Property(x => x.ClientCondition)
            .HasMaxLength(2000);

        builder.Property(x => x.Concerns)
            .HasMaxLength(2000);

        builder.Property(x => x.Medications)
            .HasMaxLength(1000);

        builder.Property(x => x.Temperature)
            .HasPrecision(5, 2);

        builder.Property(x => x.CaregiverSignature)
            .HasMaxLength(4000); // Base64 encoded signature

        builder.Property(x => x.ClientOrFamilySignature)
            .HasMaxLength(4000);

        // DateTime conversions
        builder.Property(x => x.CreatedAt)
            .HasConversion(new UtcDateTimeConverter());

        builder.Property(x => x.UpdatedAt)
            .HasConversion(new UtcDateTimeNullableConverter());

        builder.Property(x => x.VisitDateTime)
            .HasConversion(new UtcDateTimeConverter());

        // Relationships
        builder.HasMany(x => x.Photos)
            .WithOne(x => x.VisitNote)
            .HasForeignKey(x => x.VisitNoteId)
            .OnDelete(DeleteBehavior.Restrict); // PHI entity — HIPAA requires Restrict

        // Indexes
        builder.HasIndex(x => x.ShiftId)
            .HasDatabaseName("IX_VisitNotes_ShiftId");

        builder.HasIndex(x => x.CaregiverId)
            .HasDatabaseName("IX_VisitNotes_CaregiverId");

        builder.HasIndex(x => x.VisitDateTime)
            .HasDatabaseName("IX_VisitNotes_VisitDateTime");

        // Global query filter
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
```

### 4.10 VisitPhoto Configuration

**File**: `src/CarePath.Infrastructure/Persistence/Configurations/Scheduling/VisitPhotoConfiguration.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CarePath.Domain.Entities.Scheduling;
using CarePath.Infrastructure.Persistence.Converters;

namespace CarePath.Infrastructure.Persistence.Configurations.Scheduling;

public class VisitPhotoConfiguration : IEntityTypeConfiguration<VisitPhoto>
{
    public void Configure(EntityTypeBuilder<VisitPhoto> builder)
    {
        builder.ToTable("VisitPhotos");

        builder.HasKey(x => x.Id);

        // Foreign Key to VisitNote
        builder.HasOne(x => x.VisitNote)
            .WithMany(x => x.Photos)
            .HasForeignKey(x => x.VisitNoteId)
            .OnDelete(DeleteBehavior.Restrict); // PHI entity — HIPAA requires Restrict

        // Properties
        builder.Property(x => x.PhotoUrl)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.Caption)
            .HasMaxLength(500);

        // DateTime conversions
        builder.Property(x => x.CreatedAt)
            .HasConversion(new UtcDateTimeConverter());

        builder.Property(x => x.UpdatedAt)
            .HasConversion(new UtcDateTimeNullableConverter());

        builder.Property(x => x.TakenAt)
            .HasConversion(new UtcDateTimeConverter());

        // Indexes
        builder.HasIndex(x => x.VisitNoteId)
            .HasDatabaseName("IX_VisitPhotos_VisitNoteId");

        // Global query filter
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
```

### 4.11 Invoice Configuration

**File**: `src/CarePath.Infrastructure/Persistence/Configurations/Billing/InvoiceConfiguration.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CarePath.Domain.Entities.Billing;
using CarePath.Infrastructure.Persistence.Converters;

namespace CarePath.Infrastructure.Persistence.Configurations.Billing;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices");

        builder.HasKey(x => x.Id);

        // Foreign Key to Client
        builder.HasOne(x => x.Client)
            .WithMany(x => x.Invoices)
            .HasForeignKey(x => x.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        // Properties
        builder.Property(x => x.InvoiceNumber)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.TaxAmount)
            .HasPrecision(18, 2);

        builder.Property(x => x.Notes)
            .HasMaxLength(1000);

        // DateTime conversions
        builder.Property(x => x.CreatedAt)
            .HasConversion(new UtcDateTimeConverter());

        builder.Property(x => x.UpdatedAt)
            .HasConversion(new UtcDateTimeNullableConverter());

        builder.Property(x => x.InvoiceDate)
            .HasConversion(new UtcDateTimeConverter());

        builder.Property(x => x.DueDate)
            .HasConversion(new UtcDateTimeConverter());

        builder.Property(x => x.PaidDate)
            .HasConversion(new UtcDateTimeNullableConverter());

        // Relationships
        builder.HasMany(x => x.LineItems)
            .WithOne(x => x.Invoice)
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Payments)
            .WithOne(x => x.Invoice)
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(x => x.ClientId)
            .HasDatabaseName("IX_Invoices_ClientId");

        builder.HasIndex(x => x.InvoiceNumber)
            .IsUnique()
            .HasDatabaseName("IX_Invoices_InvoiceNumber");

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("IX_Invoices_Status");

        builder.HasIndex(x => x.InvoiceDate)
            .HasDatabaseName("IX_Invoices_InvoiceDate");

        // Global query filter
        builder.HasQueryFilter(x => !x.IsDeleted);

        // Ignore computed properties
        builder.Ignore(x => x.Subtotal);
        builder.Ignore(x => x.Total);
        builder.Ignore(x => x.AmountPaid);
        builder.Ignore(x => x.Balance);
        builder.Ignore(x => x.IsFullyPaid);
    }
}
```

### 4.12 InvoiceLineItem Configuration

**File**: `src/CarePath.Infrastructure/Persistence/Configurations/Billing/InvoiceLineItemConfiguration.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CarePath.Domain.Entities.Billing;
using CarePath.Infrastructure.Persistence.Converters;

namespace CarePath.Infrastructure.Persistence.Configurations.Billing;

public class InvoiceLineItemConfiguration : IEntityTypeConfiguration<InvoiceLineItem>
{
    public void Configure(EntityTypeBuilder<InvoiceLineItem> builder)
    {
        builder.ToTable("InvoiceLineItems");

        builder.HasKey(x => x.Id);

        // Foreign Keys
        builder.HasOne(x => x.Invoice)
            .WithMany(x => x.LineItems)
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Shift)
            .WithMany()
            .HasForeignKey(x => x.ShiftId)
            .OnDelete(DeleteBehavior.SetNull);

        // Properties
        builder.Property(x => x.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.Hours)
            .HasPrecision(10, 2);

        builder.Property(x => x.RatePerHour)
            .HasPrecision(18, 2);

        builder.Property(x => x.CostPerHour)
            .HasPrecision(18, 2);

        // DateTime conversions
        builder.Property(x => x.CreatedAt)
            .HasConversion(new UtcDateTimeConverter());

        builder.Property(x => x.UpdatedAt)
            .HasConversion(new UtcDateTimeNullableConverter());

        builder.Property(x => x.ServiceDate)
            .HasConversion(new UtcDateTimeConverter());

        // Indexes
        builder.HasIndex(x => x.InvoiceId)
            .HasDatabaseName("IX_InvoiceLineItems_InvoiceId");

        builder.HasIndex(x => x.ShiftId)
            .HasDatabaseName("IX_InvoiceLineItems_ShiftId");

        // Global query filter
        builder.HasQueryFilter(x => !x.IsDeleted);

        // Ignore computed properties
        builder.Ignore(x => x.Total);
        builder.Ignore(x => x.TotalCost);
        builder.Ignore(x => x.GrossProfit);
        builder.Ignore(x => x.GrossMarginPercentage);
    }
}
```

### 4.13 Payment Configuration

**File**: `src/CarePath.Infrastructure/Persistence/Configurations/Billing/PaymentConfiguration.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CarePath.Domain.Entities.Billing;
using CarePath.Infrastructure.Persistence.Converters;

namespace CarePath.Infrastructure.Persistence.Configurations.Billing;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");

        builder.HasKey(x => x.Id);

        // Foreign Key to Invoice
        builder.HasOne(x => x.Invoice)
            .WithMany(x => x.Payments)
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        // Properties
        builder.Property(x => x.Amount)
            .HasPrecision(18, 2);

        builder.Property(x => x.ReferenceNumber)
            .HasMaxLength(50);

        builder.Property(x => x.Notes)
            .HasMaxLength(500);

        builder.Property(x => x.FailureReason)
            .HasMaxLength(500);

        builder.Property(x => x.IsSuccessful)
            .HasDefaultValue(true);

        // DateTime conversions
        builder.Property(x => x.CreatedAt)
            .HasConversion(new UtcDateTimeConverter());

        builder.Property(x => x.UpdatedAt)
            .HasConversion(new UtcDateTimeNullableConverter());

        builder.Property(x => x.PaymentDate)
            .HasConversion(new UtcDateTimeConverter());

        // Indexes
        builder.HasIndex(x => x.InvoiceId)
            .HasDatabaseName("IX_Payments_InvoiceId");

        builder.HasIndex(x => x.PaymentDate)
            .HasDatabaseName("IX_Payments_PaymentDate");

        // Global query filter
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
```

### 4.14 ApplicationUser Configuration

**File**: `src/CarePath.Infrastructure/Persistence/Configurations/Identity/ApplicationUserConfiguration.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CarePath.Infrastructure.Identity;
using CarePath.Infrastructure.Persistence.Converters;

namespace CarePath.Infrastructure.Persistence.Configurations.Identity;

/// <summary>
/// Fluent API configuration for ApplicationUser (ASP.NET Core Identity user).
/// Configures the link to the domain User entity via DomainUserId foreign key (Pattern A).
/// </summary>
public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        // Table name (inherited from Identity, but explicit for clarity)
        builder.ToTable("AspNetUsers");

        // Primary key (inherited from IdentityUser<Guid>, but explicit for clarity)
        builder.HasKey(x => x.Id);

        // Required properties (inherited from IdentityUser)
        builder.Property(x => x.UserName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.PhoneNumber)
            .HasMaxLength(20);

        // Domain User foreign key
        builder.Property(x => x.DomainUserId)
            .IsRequired();

        // Relationship to domain User (one-to-one)
        builder.HasOne(x => x.DomainUser)
            .WithOne()
            .HasForeignKey<ApplicationUser>(x => x.DomainUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(x => x.DomainUserId)
            .IsUnique()
            .HasDatabaseName("IX_AspNetUsers_DomainUserId");

        builder.HasIndex(x => x.Email)
            .HasDatabaseName("IX_AspNetUsers_Email");

        builder.HasIndex(x => x.UserName)
            .HasDatabaseName("IX_AspNetUsers_UserName");
    }
}
```

---

## 5. ApplicationUser Entity

**File**: `src/CarePath.Infrastructure/Identity/ApplicationUser.cs`

```csharp
using Microsoft.AspNetCore.Identity;
using CarePath.Domain.Entities.Identity;

namespace CarePath.Infrastructure.Identity;

/// <summary>
/// ASP.NET Core Identity user (extends IdentityUser with Guid primary key).
/// Linked to domain User entity via DomainUserId foreign key (Pattern A: Separate Tables).
/// Handles authentication (login, JWT tokens, password reset).
/// Domain User handles business logic (role, contact info, audit trail).
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>
    /// Foreign key to the domain User entity.
    /// Required: every ApplicationUser must have a corresponding domain User.
    /// </summary>
    public Guid DomainUserId { get; set; }

    /// <summary>
    /// Navigation property to the domain User entity.
    /// </summary>
    public User DomainUser { get; set; } = null!;
}
```

---

## 6. Generic Repository Implementation

**File**: `src/CarePath.Infrastructure/Persistence/Repositories/Repository.cs`

```csharp
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using CarePath.Domain.Entities.Common;
using CarePath.Domain.Interfaces.Repositories;

namespace CarePath.Infrastructure.Persistence.Repositories;

/// <summary>
/// Generic repository implementation for all domain entities.
/// Automatically applies soft-delete query filter from DbContext configuration.
/// All methods return IReadOnlyList<T> (fully materialized, not lazy).
/// </summary>
/// <typeparam name="T">Entity type constrained to BaseEntity.</typeparam>
public class Repository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly CarePathDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public Repository(CarePathDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<T>> FindAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet.Where(predicate).ToListAsync(cancellationToken);
    }

    public async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddAsync(entity, cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        _dbSet.Update(entity);
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        // Soft delete: set IsDeleted flag
        entity.IsDeleted = true;
        await UpdateAsync(entity, cancellationToken);
    }

    public async Task<bool> ExistsAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet.AnyAsync(predicate, cancellationToken);
    }

    public async Task<int> CountAsync(
        Expression<Func<T, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        if (predicate == null)
            return await _dbSet.CountAsync(cancellationToken);

        return await _dbSet.CountAsync(predicate, cancellationToken);
    }

    public async Task<(IReadOnlyList<T> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var totalCount = await _dbSet.CountAsync(cancellationToken);

        var items = await _dbSet
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
```

---

## 7. Unit of Work Implementation

**File**: `src/CarePath.Infrastructure/Persistence/Repositories/UnitOfWork.cs`

```csharp
using CarePath.Domain.Entities.Common;
using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Entities.Scheduling;
using CarePath.Domain.Entities.Billing;
using CarePath.Domain.Interfaces.Repositories;

namespace CarePath.Infrastructure.Persistence.Repositories;

/// <summary>
/// Unit of Work implementation grouping all repository instances under a single transactional boundary.
/// Repositories are lazily initialized (created on first access).
/// Supports manual transaction management via BeginTransactionAsync, CommitTransactionAsync, RollbackTransactionAsync.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly CarePathDbContext _context;

    // Lazy repository instances
    private IRepository<User>? _users;
    private IRepository<Caregiver>? _caregivers;
    private IRepository<CaregiverCertification>? _caregiverCertifications;
    private IRepository<Client>? _clients;
    private IRepository<CarePlan>? _carePlans;
    private IRepository<Shift>? _shifts;
    private IRepository<VisitNote>? _visitNotes;
    private IRepository<VisitPhoto>? _visitPhotos;
    private IRepository<Invoice>? _invoices;
    private IRepository<InvoiceLineItem>? _invoiceLineItems;
    private IRepository<Payment>? _payments;

    public UnitOfWork(CarePathDbContext context)
    {
        _context = context;
    }

    // Lazy-initialized repository properties
    public IRepository<User> Users => _users ??= new Repository<User>(_context);
    public IRepository<Caregiver> Caregivers => _caregivers ??= new Repository<Caregiver>(_context);
    public IRepository<CaregiverCertification> CaregiverCertifications =>
        _caregiverCertifications ??= new Repository<CaregiverCertification>(_context);
    public IRepository<Client> Clients => _clients ??= new Repository<Client>(_context);
    public IRepository<CarePlan> CarePlans => _carePlans ??= new Repository<CarePlan>(_context);
    public IRepository<Shift> Shifts => _shifts ??= new Repository<Shift>(_context);
    public IRepository<VisitNote> VisitNotes => _visitNotes ??= new Repository<VisitNote>(_context);
    public IRepository<VisitPhoto> VisitPhotos => _visitPhotos ??= new Repository<VisitPhoto>(_context);
    public IRepository<Invoice> Invoices => _invoices ??= new Repository<Invoice>(_context);
    public IRepository<InvoiceLineItem> InvoiceLineItems =>
        _invoiceLineItems ??= new Repository<InvoiceLineItem>(_context);
    public IRepository<Payment> Payments => _payments ??= new Repository<Payment>(_context);

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await _context.Database.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _context.Database.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.Database.RollbackTransactionAsync(cancellationToken);
        }
        finally
        {
            // Dispose of current transaction
            await _context.Database.GetCurrentTransactionAsync()?.DisposeAsync() ?? ValueTask.CompletedTask;
        }
    }

    public void Dispose()
    {
        _context?.Dispose();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_context != null)
        {
            await _context.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }
}
```

---

## 8. Dependency Injection

**File**: `src/CarePath.Infrastructure/DependencyInjection.cs`

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using CarePath.Domain.Interfaces.Repositories;
using CarePath.Infrastructure.Identity;
using CarePath.Infrastructure.Persistence;
using CarePath.Infrastructure.Persistence.Interceptors;
using CarePath.Infrastructure.Persistence.Repositories;

namespace CarePath.Infrastructure;

/// <summary>
/// Extension method to register Infrastructure layer services (EF Core, Identity, Repositories, Interceptors).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register DbContext with SQL Server
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddDbContext<CarePathDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(CarePathDbContext).Assembly.GetName().Name);
                sqlOptions.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null);
            });
        });

        // Register ASP.NET Core Identity
        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
        {
            // Password requirements
            options.Password.RequiredLength = 8;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = false;

            // Lockout policy
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;

            // User options
            options.User.RequireUniqueEmail = true;

            // Sign-in options
            options.SignIn.RequireConfirmedEmail = false;
        })
        .AddEntityFrameworkStores<CarePathDbContext>()
        .AddDefaultTokenProviders();

        // Register Interceptors
        services.AddScoped<AuditableEntityInterceptor>();

        // Register Repository & Unit of Work
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Register HttpContextAccessor (required by AuditableEntityInterceptor)
        services.AddHttpContextAccessor();

        return services;
    }
}
```

---

## 9. Connection String & Configuration

### 9.1 appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=CarePathHealth;Integrated Security=true;TrustServerCertificate=true;Encrypt=true;"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.EntityFrameworkCore.Database.Command": "Debug"
    }
  },
  "AllowedHosts": "*"
}
```

### 9.2 appsettings.Development.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=CarePathHealth_Dev;Integrated Security=true;TrustServerCertificate=true;Encrypt=true;"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.EntityFrameworkCore.Database.Command": "Debug"
    }
  }
}
```

---

## 10. Initial Migration

**Strategy**: Generate "InitialCreate" migration after all entity configurations are complete.

```bash
dotnet ef migrations add InitialCreate \
  --project src/CarePath.Infrastructure \
  --startup-project src/CarePath.WebApi
```

**Migration will create tables**:
- AspNetUsers, AspNetRoles, AspNetUserRoles (Identity)
- Users, Caregivers, CaregiverCertifications, Clients, CarePlans (Identity domain entities)
- Shifts, VisitNotes, VisitPhotos (Scheduling domain entities)
- Invoices, InvoiceLineItems, Payments (Billing domain entities)

**Indexes created** (by entity configurations):
- Email (unique) on Users, AspNetUsers
- CreatedAt on Users
- UserId (unique) on Caregivers, Clients
- HireDate on Caregivers
- ExpirationDate on CaregiverCertifications
- DateOfBirth on Clients
- ClientId on Shifts, CarePlans, Invoices
- CaregiverId on Shifts, VisitNotes
- ScheduledStartTime on Shifts
- Status on Shifts, Invoices
- ShiftId on VisitNotes
- VisitNoteId on VisitPhotos
- VisitDateTime on VisitNotes
- InvoiceNumber (unique) on Invoices
- ServiceDate on InvoiceLineItems
- PaymentDate on Payments
- DomainUserId (unique) on AspNetUsers

---

## 11. Testing Strategy

### 11.1 Integration Test Example (In-Memory Database)

```csharp
using Microsoft.EntityFrameworkCore;
using Xunit;
using FluentAssertions;
using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Enums;
using CarePath.Infrastructure.Persistence;
using CarePath.Infrastructure.Persistence.Repositories;

namespace CarePath.Infrastructure.Tests.Repositories;

public class RepositoryIntegrationTests : IAsyncLifetime
{
    private CarePathDbContext _context = null!;
    private IUnitOfWork _unitOfWork = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<CarePathDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new CarePathDbContext(options, new TestAuditableEntityInterceptor());
        await _context.Database.EnsureCreatedAsync();

        _unitOfWork = new UnitOfWork(_context);
    }

    public async Task DisposeAsync()
    {
        await _context.Database.EnsureDeletedAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task AddUser_WithValidData_PersistsSuccessfully()
    {
        // Arrange
        var user = new User
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            PhoneNumber = "301-555-0123",
            Role = UserRole.Caregiver,
            IsActive = true
        };

        // Act
        await _unitOfWork.Users.AddAsync(user);
        var result = await _unitOfWork.SaveChangesAsync();

        // Assert
        result.Should().Be(1);
        var retrievedUser = await _unitOfWork.Users.GetByIdAsync(user.Id);
        retrievedUser.Should().NotBeNull();
        retrievedUser!.Email.Should().Be("john@example.com");
    }

    [Fact]
    public async Task DeleteUser_UsesSoftDelete_SetsIsDeletedTrue()
    {
        // Arrange
        var user = new User
        {
            FirstName = "Jane",
            LastName = "Smith",
            Email = "jane@example.com",
            PhoneNumber = "410-555-0124",
            Role = UserRole.Client,
            IsActive = true
        };
        await _unitOfWork.Users.AddAsync(user);
        await _unitOfWork.SaveChangesAsync();

        // Act
        await _unitOfWork.Users.DeleteAsync(user);
        await _unitOfWork.SaveChangesAsync();

        // Assert
        // Note: Query filter excludes soft-deleted users
        var retrievedUser = await _unitOfWork.Users.GetByIdAsync(user.Id);
        retrievedUser.Should().BeNull();
    }

    [Fact]
    public async Task FindByPredicate_ReturnsFilteredResults()
    {
        // Arrange
        var caregivers = new[]
        {
            new User { FirstName = "Alice", LastName = "Johnson", Email = "alice@example.com", PhoneNumber = "443-555-0125", Role = UserRole.Caregiver },
            new User { FirstName = "Bob", LastName = "Lee", Email = "bob@example.com", PhoneNumber = "667-555-0126", Role = UserRole.Caregiver }
        };

        foreach (var caregiver in caregivers)
        {
            await _unitOfWork.Users.AddAsync(caregiver);
        }
        await _unitOfWork.SaveChangesAsync();

        // Act
        var results = await _unitOfWork.Users.FindAsync(x => x.Role == UserRole.Caregiver);

        // Assert
        results.Should().HaveCount(2);
    }
}

// Mock interceptor for tests (no-op implementation)
public class TestAuditableEntityInterceptor : AuditableEntityInterceptor
{
    public TestAuditableEntityInterceptor() : base(new TestHttpContextAccessor()) { }
}

public class TestHttpContextAccessor : IHttpContextAccessor
{
    public HttpContext? HttpContext { get; set; }
}
```

### 11.2 Integration Test with SQL Server Test Containers

For production-like integration tests, use TestContainers:

```bash
dotnet add package Testcontainers.MsSql
```

```csharp
using Testcontainers.MsSql;
using Xunit;
using FluentAssertions;

public class RepositoryIntegrationTestsWithSqlServer : IAsyncLifetime
{
    private readonly MsSqlContainer _msSqlContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    private CarePathDbContext _context = null!;

    public async Task InitializeAsync()
    {
        await _msSqlContainer.StartAsync();

        var connectionString = _msSqlContainer.GetConnectionString();
        var options = new DbContextOptionsBuilder<CarePathDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        _context = new CarePathDbContext(options, new TestAuditableEntityInterceptor());
        await _context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _msSqlContainer.StopAsync();
    }

    [Fact]
    public async Task Migration_CreatesAllTables()
    {
        // Query to verify all expected tables exist
        var tables = new[]
        {
            "Users", "Caregivers", "CaregiverCertifications", "Clients", "CarePlans",
            "Shifts", "VisitNotes", "VisitPhotos",
            "Invoices", "InvoiceLineItems", "Payments",
            "AspNetUsers", "AspNetRoles"
        };

        foreach (var table in tables)
        {
            var exists = await _context.Database
                .ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '{table}'");
            exists.Should().Be(1, $"Table {table} should exist");
        }
    }
}
```

---

## 12. HIPAA Compliance Checklist

- [ ] **Encryption at Rest**: SQL Server TDE enabled (`ALTER DATABASE CarePathHealth SET ENCRYPTION ON`)
- [ ] **Role-Based Authorization**: Controllers use `[Authorize(Roles = "...")]` on PHI endpoints
- [ ] **Audit Logging**: AuditableEntityInterceptor logs CreatedBy, UpdatedBy on all changes
- [ ] **No PHI in Logs**: Configure Serilog to exclude MedicalConditions, Allergies from context
- [ ] **No PHI in URLs**: Repository methods accept ID only, never include patient names in URLs
- [ ] **Soft Delete Enforcement**: Global query filter on all entities; no hard deletes via DbSet.Remove()
- [ ] **Data Retention**: 6-year retention via IsDeleted flag, with archive/purge procedures (future task)
- [ ] **Connection String Security**: Never commit production connection strings; use Azure Key Vault

---

## 13. Open Questions & Decisions

- [ ] **Pagination Strategy**: `GetPagedAsync(int pageNumber, int pageSize)` deferred to TASK-019a
- [ ] **Soft-Delete Implementation**: Should soft-deleted records be queryable via separate `IncludeDeleted()` extension? **Recommendation**: Phase 2
- [ ] **Computed Property Caching**: Should `Shift.BillableHours`, `Invoice.Total` be cached in separate columns? **Recommendation**: Phase 2 after performance testing
- [ ] **Event Sourcing**: Should Shift status transitions trigger domain events? **Recommendation**: Phase 3
- [ ] **Encryption at Column Level**: Should MedicalConditions, Allergies, SSN be encrypted at column level (EF Core .HasConversion)? **Recommendation**: Phase 2 with Azure Key Vault integration
- [x] **UTC DateTime Preservation**: **Decision: ValueConverter on all DateTime properties.** EF Core cannot preserve DateTimeKind through SQL Server round-trips; manual conversion required in configurations.
- [x] **Identity Pattern**: **Decision: Pattern A (Separate Tables).** Domain User remains pure; ApplicationUser handles authentication. Supports future Identity framework swaps.

---

## 14. Success Criteria

✅ CarePathDbContext created with 12 domain entities + Identity DbSets
✅ All entity configurations implement Fluent API (no data annotations)
✅ UTC DateTime converters on all DateTime properties
✅ Global soft-delete query filter on all domain entities
✅ AuditableEntityInterceptor auto-populates audit fields
✅ Generic Repository<T> with CRUD and soft-delete support
✅ UnitOfWork with lazy repository initialization
✅ DependencyInjection extension method
✅ InitialCreate migration generated and validated
✅ Integration tests with in-memory and SQL Server Test Containers
✅ All HIPAA compliance checks documented

---

## 15. Related Documents

- [CP-01 Design Spec](./cp-01-create-domain-entities.md) - Domain entities and repository interfaces
- [CP-01 Requirements](../01-requirements/cp-01-create-domain-entities.md) - Business requirements
- [Migration Workflow](../../.claude/commands/migration.md) - EF Core migration procedures
- [CLAUDE.md](../../CLAUDE.md) - Coding conventions and architecture rules

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-02-25 | Tobi Kareem | Initial comprehensive Infrastructure design spec with all C# code, configurations, and testing strategy |
