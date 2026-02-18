# Design Specification: CP-01 Create Domain Entities

**Date**: 2026-02-16
**Author**: Tobi Kareem
**Project**: CarePath Health
**Status**: Approved
**Related Specs**:
- [Requirements Spec](../01-requirements/cp-01-create-domain-entities.md) - Business requirements and user stories
- [Tasks Spec](../03-tasks/cp-01-create-domain-entities.md) - Implementation tasks (to be created after this is approved)

---

## Executive Summary

> Design and implement the foundational domain layer for CarePath Health using Clean Architecture principles, creating 12 core entities (BaseEntity, User, Caregiver, Client, Shift, VisitNote, Invoice, Payment, etc.) with full XML documentation, computed properties for business logic (margin calculations, age calculation, billable hours), 7 enumerations, and repository interfaces, ensuring zero external dependencies and 100% testability in isolation.

---

## 1. Architecture Overview

### 1.1 Clean Architecture - Domain Layer Only

This spec focuses exclusively on the **Domain Layer** (CarePath.Domain project).

```
┌─────────────────────────────────────────────────────────────────┐
│                    CarePath.Domain (This Spec)                   │
├─────────────────────────────────────────────────────────────────┤
│                                                                   │
│  ┌────────────────┐  ┌────────────────┐  ┌────────────────┐   │
│  │   Entities     │  │  Enumerations  │  │    Interfaces  │   │
│  │                │  │                │  │                │   │
│  │ • BaseEntity   │  │ • UserRole     │  │ • IRepository  │   │
│  │ • User         │  │ • Employment   │  │ • IUnitOfWork  │   │
│  │ • Caregiver    │  │   Type         │  │                │   │
│  │ • Client       │  │ • Service      │  │                │   │
│  │ • Shift        │  │   Type         │  │                │   │
│  │ • VisitNote    │  │ • ShiftStatus  │  │                │   │
│  │ • Invoice      │  │ • Certification│  │                │   │
│  │ • Payment      │  │   Type         │  │                │   │
│  │ • CarePlan     │  │ • InvoiceStatus│  │                │   │
│  │ + 3 more...    │  │ • PaymentMethod│  │                │   │
│  └────────────────┘  └────────────────┘  └────────────────┘   │
│                                                                   │
│  Key Relationships (Navigation Properties):                      │
│  • User ──1:1───▶ Caregiver / Client                            │
│  • Caregiver ──1:many───▶ Shifts, VisitNotes, Certifications   │
│  • Client ──1:many───▶ Shifts, Invoices, CarePlans             │
│  • Shift ──1:many───▶ VisitNotes                               │
│  • Invoice ──1:many───▶ LineItems, Payments                    │
│                                                                   │
│  Computed Properties (Business Logic):                           │
│  • User.FullName: FirstName + LastName                          │
│  • Client.Age: Calculated from DateOfBirth                      │
│  • Shift.BillableHours: (ActualEnd - ActualStart - Breaks)/60  │
│  • Shift.GrossMargin: BillRate - PayRate                        │
│  • Shift.GrossMarginPercentage: (Margin/BillRate) × 100        │
│  • CaregiverCertification.IsExpired: ExpirationDate < Now      │
│  • CaregiverCertification.IsExpiringSoon: < 30 days away       │
│  • Invoice.Subtotal: Sum(LineItems.Total)                      │
│  • Invoice.Balance: Total - AmountPaid                          │
│                                                                   │
└─────────────────────────────────────────────────────────────────┘
        │
        │ Zero Dependencies (Pure .NET 9 / C# 13)
        │ • No EF Core attributes
        │ • No ASP.NET dependencies
        │ • No external service dependencies
        ▼
   Future Phases (Out of Scope for this Spec):
   • Application Layer (DTOs, Services, Validators)
   • Infrastructure Layer (EF Core configurations, Repositories)
   • API Layer (Controllers, SignalR Hubs)
   • UI Layers (MAUI, Blazor)
```

### 1.2 Project Structure

```
src/CarePath.Domain/
├── Entities/
│   ├── Common/
│   │   └── BaseEntity.cs              # Abstract base with audit fields
│   ├── Identity/
│   │   ├── User.cs                    # Base user (all roles)
│   │   ├── Caregiver.cs               # Caregiver profile
│   │   ├── CaregiverCertification.cs  # Certifications (CNA, LPN, RN)
│   │   ├── Client.cs                  # Client/patient profile
│   │   └── CarePlan.cs                # Care plan documentation
│   ├── Scheduling/
│   │   ├── Shift.cs                   # Scheduled care sessions
│   │   ├── VisitNote.cs               # Care activity documentation
│   │   └── VisitPhoto.cs              # Photos attached to notes
│   └── Billing/
│       ├── Invoice.cs                 # Client invoices
│       ├── InvoiceLineItem.cs         # Invoice line items (per shift)
│       └── Payment.cs                 # Payment records
├── Enums/
│   ├── UserRole.cs                    # Admin, Coordinator, Caregiver, Client, FacilityManager
│   ├── EmploymentType.cs              # W2Employee, Contractor1099
│   ├── ServiceType.cs                 # InHomeCare, FacilityStaffing
│   ├── ShiftStatus.cs                 # Scheduled, InProgress, Completed, Cancelled, NoShow
│   ├── CertificationType.cs           # CNA, LPN, RN, HHA, CPR, FirstAid, Dementia, Alzheimers
│   ├── InvoiceStatus.cs               # Draft, Sent, Paid, Overdue, Cancelled
│   └── PaymentMethod.cs               # Cash, Check, CreditCard, BankTransfer, Insurance
└── Interfaces/
    └── Repositories/
        ├── IRepository.cs             # Generic repository interface
        └── IUnitOfWork.cs             # Unit of work pattern
```

### 1.3 Affected Layers

**This Spec (Phase 1 - Domain Entities Only)**:
- ✅ **CarePath.Domain** - Core entities, enumerations, interfaces

**Out of Scope** (Future Phases):
- ❌ CarePath.Application (DTOs, Services, Validators, AutoMapper)
- ❌ CarePath.Infrastructure (EF Core, Migrations, Repository implementations)
- ❌ CarePath.Api (Controllers, SignalR, Middleware)
- ❌ CarePath.MauiApp (Mobile UI)
- ❌ CarePath.Web (Blazor admin dashboard)

---

## 2. Entity Designs (Detailed)

### 2.1 BaseEntity (Abstract Base Class)

**File**: `src/CarePath.Domain/Entities/Common/BaseEntity.cs`

**Purpose**: Provide common audit fields and soft delete functionality for all domain entities.

**Design Rationale**:
- Uses `Guid` primary keys for globally unique identifiers (supports distributed systems)
- All timestamps are UTC to avoid timezone confusion
- Soft delete (`IsDeleted`) preserves audit trail for HIPAA compliance
- `CreatedBy`/`UpdatedBy` track who made changes (audit trail)

```csharp
namespace CarePath.Domain.Entities.Common;

/// <summary>
/// Base entity class providing common audit fields and soft delete support.
/// All domain entities must inherit from this class.
/// </summary>
public abstract class BaseEntity
{
    /// <summary>
    /// Unique identifier (GUID). Initialized to new GUID on creation.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// UTC timestamp when entity was created. Defaults to current UTC time.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// UTC timestamp when entity was last updated. Null if never updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// User ID (or system name) who created the entity. Used for HIPAA audit trail.
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// User ID (or system name) who last updated the entity. Used for HIPAA audit trail.
    /// </summary>
    public string? UpdatedBy { get; set; }

    /// <summary>
    /// Soft delete flag. When true, entity is logically deleted but preserved in database.
    /// Default: false (not deleted). Supports 6-year data retention for Maryland medical records.
    /// </summary>
    public bool IsDeleted { get; set; } = false;
}
```

---

### 2.2 User Entity

**File**: `src/CarePath.Domain/Entities/Identity/User.cs`

**Purpose**: Base user entity for all user roles in the system.

**Business Context**:
- Supports 5 roles: Admin, Coordinator, Caregiver, Client, FacilityManager
- Maryland-focused (State defaults to "Maryland")
- Email is unique identifier for authentication

```csharp
using CarePath.Domain.Enums;

namespace CarePath.Domain.Entities.Identity;

/// <summary>
/// Base user entity representing all users in CarePath (Admin, Coordinator, Caregiver, Client, Facility Manager).
/// Contains contact info, address, and role assignment.
/// </summary>
public class User : BaseEntity
{
    /// <summary>User's first name. Required.</summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>User's last name. Required.</summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>Email address (unique across all users). Used for authentication.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Phone number (US format, preferably Maryland area codes: 301, 410, 443, 667).</summary>
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>Street address. Optional for some user types.</summary>
    public string? Address { get; set; }

    /// <summary>City name.</summary>
    public string? City { get; set; }

    /// <summary>State abbreviation. Defaults to "Maryland" for MVP.</summary>
    public string? State { get; set; } = "Maryland";

    /// <summary>ZIP code (5 or 9 digits).</summary>
    public string? ZipCode { get; set; }

    /// <summary>User role determining access permissions.</summary>
    public UserRole Role { get; set; }

    /// <summary>Indicates if user account is active. Inactive users cannot log in. Default: true.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>UTC timestamp of last successful login. Null if never logged in.</summary>
    public DateTime? LastLoginAt { get; set; }

    // Computed Properties

    /// <summary>Full name (FirstName + LastName) for display. Computed property (not stored).</summary>
    public string FullName => $"{FirstName} {LastName}";
}
```

---

### 2.3 Caregiver Entity & CaregiverCertification

**File**: `src/CarePath.Domain/Entities/Identity/Caregiver.cs`

**Purpose**: Represents caregivers (W-2 employees or 1099 contractors) providing care services.

**Business Context**:
- **EmploymentType determines margin targets**:
  - W-2 Employee: In-home care (40-45% target margin)
  - 1099 Contractor: Facility staffing (25-30% target margin)
- Tracks certifications, skills, availability, performance metrics

```csharp
using CarePath.Domain.Enums;

namespace CarePath.Domain.Entities.Identity;

/// <summary>
/// Caregiver entity representing care providers (W-2 employees or 1099 contractors).
/// Tracks employment details, certifications, skills, availability, and performance.
/// </summary>
public class Caregiver : BaseEntity
{
    // Foreign Keys & Navigation
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    // Employment Details
    /// <summary>W-2 employee (in-home, 40-45% margin) or 1099 contractor (facility, 25-30% margin).</summary>
    public EmploymentType EmploymentType { get; set; } = EmploymentType.W2Employee;

    /// <summary>Hourly pay rate (USD). Used for labor cost and margin calculations.</summary>
    public decimal HourlyPayRate { get; set; }

    public DateTime HireDate { get; set; } = DateTime.UtcNow;
    public DateTime? TerminationDate { get; set; }

    // Certifications
    public ICollection<CaregiverCertification> Certifications { get; set; } = new List<CaregiverCertification>();

    // Skills & Specialties
    public bool HasDementiaCare { get; set; }
    public bool HasAlzheimersCare { get; set; }
    public bool HasMobilityAssistance { get; set; }
    public bool HasMedicationManagement { get; set; }

    // Availability
    public bool AvailableWeekdays { get; set; } = true;
    public bool AvailableWeekends { get; set; }
    public bool AvailableNights { get; set; }
    public int MaxWeeklyHours { get; set; } = 40;

    // Performance Metrics
    public decimal? AverageRating { get; set; }
    public int TotalShiftsCompleted { get; set; }
    public int NoShowCount { get; set; }

    // Relationships
    public ICollection<Shift> Shifts { get; set; } = new List<Shift>();
    public ICollection<VisitNote> VisitNotes { get; set; } = new List<VisitNote>();
}

/// <summary>
/// Caregiver certification (CNA, LPN, RN, etc.) with expiration tracking for Maryland compliance.
/// </summary>
public class CaregiverCertification : BaseEntity
{
    public Guid CaregiverId { get; set; }
    public Caregiver Caregiver { get; set; } = null!;

    public CertificationType Type { get; set; }
    public string CertificationNumber { get; set; } = string.Empty;
    public DateTime IssueDate { get; set; }
    public DateTime ExpirationDate { get; set; }
    public string IssuingAuthority { get; set; } = string.Empty;

    // Computed Properties
    /// <summary>True if certification has expired.</summary>
    public bool IsExpired => ExpirationDate < DateTime.UtcNow;

    /// <summary>True if certification expires within 30 days (alert administrators).</summary>
    public bool IsExpiringSoon => ExpirationDate < DateTime.UtcNow.AddDays(30);
}
```

---

### 2.4 Client Entity & CarePlan

**File**: `src/CarePath.Domain/Entities/Identity/Client.cs`

**Purpose**: Represents clients (care recipients) receiving services.

**Business Context**:
- Stores care requirements for matching with qualified caregivers
- GPS coordinates enable geofencing for shift check-in verification
- Medical info (PHI) requires encryption at rest (Infrastructure layer)
- Supports insurance/Medicaid billing

```csharp
using CarePath.Domain.Enums;

namespace CarePath.Domain.Entities.Identity;

/// <summary>
/// Client entity representing care recipients.
/// Stores care requirements, medical info (PHI), billing details, GPS coordinates.
/// </summary>
public class Client : BaseEntity
{
    // Foreign Keys & Navigation
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    // Personal Details
    public DateTime DateOfBirth { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string? EmergencyContactRelationship { get; set; }

    // Care Requirements (for caregiver matching)
    public bool RequiresDementiaCare { get; set; }
    public bool RequiresMobilityAssistance { get; set; }
    public bool RequiresMedicationManagement { get; set; }
    public bool RequiresCompanionship { get; set; }

    /// <summary>Special instructions (e.g., "Use lift for transfers"). Free text.</summary>
    public string? SpecialInstructions { get; set; }

    /// <summary>Medical conditions (PHI - encrypt at rest). E.g., "Diabetes, Hypertension".</summary>
    public string? MedicalConditions { get; set; }

    /// <summary>Allergies (PHI - encrypt at rest). E.g., "Penicillin, Peanuts".</summary>
    public string? Allergies { get; set; }

    // Service Details
    /// <summary>InHomeCare (40-45% margin) or FacilityStaffing (25-30% margin).</summary>
    public ServiceType ServiceType { get; set; } = ServiceType.InHomeCare;

    /// <summary>Hourly rate charged to client (USD). Range: $30-90 depending on service type and role.</summary>
    public decimal HourlyBillRate { get; set; }

    public int EstimatedWeeklyHours { get; set; }

    // GPS for Check-In Verification
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? LocationNotes { get; set; }

    // Billing
    public string? InsuranceProvider { get; set; }
    public string? InsurancePolicyNumber { get; set; }
    public string? MedicaidNumber { get; set; }

    // Relationships
    public ICollection<Shift> Shifts { get; set; } = new List<Shift>();
    public ICollection<CarePlan> CarePlans { get; set; } = new List<CarePlan>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    // Computed Property
    /// <summary>Age in years, computed from DateOfBirth.</summary>
    public int Age => DateTime.UtcNow.Year - DateOfBirth.Year - (DateTime.UtcNow.DayOfYear < DateOfBirth.DayOfYear ? 1 : 0);
}

/// <summary>
/// Care plan documenting care goals and interventions. Clients may have multiple plans over time.
/// </summary>
public class CarePlan : BaseEntity
{
    public Guid ClientId { get; set; }
    public Client Client { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; } = true;

    public string? Goals { get; set; }
    public string? Interventions { get; set; }
    public string? Notes { get; set; }
}
```

---

### 2.5 Shift Entity

**File**: `src/CarePath.Domain/Entities/Scheduling/Shift.cs`

**Purpose**: Represents scheduled care sessions with margin tracking and GPS check-in/out.

**Business Context**:
- **Critical for margin tracking**: Bill rate, pay rate, computed gross margin %
- GPS tracking for in-home care (geofencing)
- Break time tracking (unpaid, labor law compliance)
- Status lifecycle: Scheduled → InProgress → Completed (or Cancelled/NoShow)

```csharp
using CarePath.Domain.Enums;

namespace CarePath.Domain.Entities.Scheduling;

/// <summary>
/// Shift entity representing a scheduled care session.
/// Tracks scheduling, GPS check-in/out, financial details, and margin calculations.
/// </summary>
public class Shift : BaseEntity
{
    // Foreign Keys
    public Guid ClientId { get; set; }
    public Client Client { get; set; } = null!;

    /// <summary>Nullable if shift is unassigned.</summary>
    public Guid? CaregiverId { get; set; }
    public Caregiver? Caregiver { get; set; }

    // Scheduling
    public DateTime ScheduledStartTime { get; set; }
    public DateTime ScheduledEndTime { get; set; }
    public DateTime? ActualStartTime { get; set; }
    public DateTime? ActualEndTime { get; set; }

    public ShiftStatus Status { get; set; } = ShiftStatus.Scheduled;
    public ServiceType ServiceType { get; set; }

    // Financial Details (copied at shift creation to preserve historical rates)
    public decimal BillRate { get; set; }  // Copied from Client.HourlyBillRate
    public decimal PayRate { get; set; }   // Copied from Caregiver.HourlyPayRate
    public decimal? OvertimePayRate { get; set; }  // 1.5x for W-2 overtime
    public decimal? WeekendPremium { get; set; }
    public decimal? HolidayPremium { get; set; }

    // GPS Tracking (for in-home care)
    public double? CheckInLatitude { get; set; }
    public double? CheckInLongitude { get; set; }
    public DateTime? CheckInTime { get; set; }

    public double? CheckOutLatitude { get; set; }
    public double? CheckOutLongitude { get; set; }
    public DateTime? CheckOutTime { get; set; }

    // Break Time (unpaid minutes, subtracted from billable hours)
    public int BreakMinutes { get; set; }

    // Notes & Cancellation
    public string? Notes { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime? CancelledAt { get; set; }

    // Relationships
    public ICollection<VisitNote> VisitNotes { get; set; } = new List<VisitNote>();

    // Computed Properties (Business Logic)

    /// <summary>Scheduled duration of shift.</summary>
    public TimeSpan ScheduledDuration => ScheduledEndTime - ScheduledStartTime;

    /// <summary>Actual duration (null if not completed).</summary>
    public TimeSpan? ActualDuration => ActualEndTime.HasValue && ActualStartTime.HasValue
        ? ActualEndTime.Value - ActualStartTime.Value
        : null;

    /// <summary>
    /// Billable hours = (ActualEnd - ActualStart - BreakMinutes) / 60.
    /// Returns 0 if shift not completed.
    /// </summary>
    public decimal BillableHours
    {
        get
        {
            if (!ActualStartTime.HasValue || !ActualEndTime.HasValue)
                return 0;

            var totalMinutes = (ActualEndTime.Value - ActualStartTime.Value).TotalMinutes - BreakMinutes;
            return (decimal)(totalMinutes / 60.0);
        }
    }

    /// <summary>Gross margin per hour (BillRate - PayRate). Used for profitability tracking.</summary>
    public decimal GrossMargin => BillRate - PayRate;

    /// <summary>
    /// Gross margin percentage: (GrossMargin / BillRate) × 100.
    /// Target: 40-45% for in-home care, 25-30% for facility staffing.
    /// </summary>
    public decimal GrossMarginPercentage => BillRate > 0 ? (GrossMargin / BillRate) * 100 : 0;
}
```

---

### 2.6 VisitNote & VisitPhoto

**File**: `src/CarePath.Domain/Entities/Scheduling/VisitNote.cs`

**Purpose**: Documents care activities and client condition during shifts (HIPAA compliance).

```csharp
namespace CarePath.Domain.Entities.Scheduling;

/// <summary>
/// Visit note documenting care activities, client condition, vital signs during a shift.
/// Required for HIPAA compliance and quality assurance.
/// </summary>
public class VisitNote : BaseEntity
{
    public Guid ShiftId { get; set; }
    public Shift Shift { get; set; } = null!;

    public Guid CaregiverId { get; set; }
    public Caregiver Caregiver { get; set; } = null!;

    public DateTime VisitDateTime { get; set; } = DateTime.UtcNow;

    // Activities (Checkboxes for quick entry)
    public bool PersonalCare { get; set; }
    public bool MealPreparation { get; set; }
    public bool Medication { get; set; }
    public bool LightHousekeeping { get; set; }
    public bool Companionship { get; set; }
    public bool Transportation { get; set; }
    public bool Exercise { get; set; }

    // Detailed Notes (PHI - encrypt at rest)
    public string? Activities { get; set; }
    public string? ClientCondition { get; set; }
    public string? Concerns { get; set; }
    public string? Medications { get; set; }

    // Optional Vital Signs
    public int? BloodPressureSystolic { get; set; }
    public int? BloodPressureDiastolic { get; set; }
    public decimal? Temperature { get; set; }  // Fahrenheit
    public int? HeartRate { get; set; }        // BPM

    // Photos
    public ICollection<VisitPhoto> Photos { get; set; } = new List<VisitPhoto>();

    // Signatures (base64 encoded)
    public string? CaregiverSignature { get; set; }
    public string? ClientOrFamilySignature { get; set; }
}

/// <summary>Photos attached to visit notes (stored as URLs to external blob storage).</summary>
public class VisitPhoto : BaseEntity
{
    public Guid VisitNoteId { get; set; }
    public VisitNote VisitNote { get; set; } = null!;

    public string PhotoUrl { get; set; } = string.Empty;
    public string? Caption { get; set; }
    public DateTime TakenAt { get; set; } = DateTime.UtcNow;
}
```

---

### 2.7 Invoice, InvoiceLineItem, Payment

**File**: `src/CarePath.Domain/Entities/Billing/Invoice.cs`

**Purpose**: Invoicing with computed totals and payment tracking.

```csharp
using CarePath.Domain.Enums;

namespace CarePath.Domain.Entities.Billing;

/// <summary>
/// Invoice issued to clients, aggregating multiple shifts into line items.
/// </summary>
public class Invoice : BaseEntity
{
    /// <summary>Auto-generated invoice number (format: INV-YYYYMMDD-XXXX).</summary>
    public string InvoiceNumber { get; set; } = string.Empty;

    public Guid ClientId { get; set; }
    public Client Client { get; set; } = null!;

    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;
    public DateTime DueDate { get; set; }
    public DateTime? PaidDate { get; set; }

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

    public decimal TaxAmount { get; set; }
    public string? Notes { get; set; }

    // Relationships
    public ICollection<InvoiceLineItem> LineItems { get; set; } = new List<InvoiceLineItem>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();

    // Computed Properties
    public decimal Subtotal => LineItems.Sum(i => i.Total);
    public decimal Total => Subtotal + TaxAmount;
    public decimal AmountPaid => Payments.Where(p => p.IsSuccessful).Sum(p => p.Amount);
    public decimal Balance => Total - AmountPaid;
    public bool IsFullyPaid => Balance <= 0;
}

/// <summary>Line item on invoice (typically one per shift).</summary>
public class InvoiceLineItem : BaseEntity
{
    public Guid InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;

    public Guid? ShiftId { get; set; }
    public Shift? Shift { get; set; }

    public string Description { get; set; } = string.Empty;
    public DateTime ServiceDate { get; set; }
    public decimal Hours { get; set; }
    public decimal RatePerHour { get; set; }

    // Internal margin tracking (not shown on client invoice)
    public decimal? CostPerHour { get; set; }

    // Computed
    public decimal Total => Hours * RatePerHour;
    public decimal? TotalCost => CostPerHour.HasValue ? Hours * CostPerHour.Value : null;
    public decimal? GrossProfit => TotalCost.HasValue ? Total - TotalCost.Value : null;
    public decimal? GrossMarginPercentage => Total > 0 && GrossProfit.HasValue ? (GrossProfit.Value / Total) * 100 : null;
}

/// <summary>Payment made toward an invoice. Supports partial payments.</summary>
public class Payment : BaseEntity
{
    public Guid InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;

    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }

    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }

    public bool IsSuccessful { get; set; } = true;
    public string? FailureReason { get; set; }
}
```

---

## 3. Enumeration Designs

### 3.1 All Enumerations

**File**: `src/CarePath.Domain/Enums/[EnumName].cs`

```csharp
namespace CarePath.Domain.Enums;

/// <summary>User roles determining access permissions.</summary>
public enum UserRole
{
    Admin = 1,
    Coordinator = 2,
    Caregiver = 3,
    Client = 4,
    FacilityManager = 5
}

/// <summary>Employment type (affects margin calculations).</summary>
public enum EmploymentType
{
    W2Employee = 1,      // In-home care, 40-45% margin
    Contractor1099 = 2   // Facility staffing, 25-30% margin
}

/// <summary>Service type (determines pricing and margins).</summary>
public enum ServiceType
{
    InHomeCare = 1,       // $30-45/hr, W-2, 40-45% margin
    FacilityStaffing = 2  // $30-90/hr, 1099, 25-30% margin
}

/// <summary>Shift status lifecycle.</summary>
public enum ShiftStatus
{
    Scheduled = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4,
    NoShow = 5
}

/// <summary>Caregiver certifications (Maryland compliance).</summary>
public enum CertificationType
{
    CNA = 1,        // Certified Nursing Assistant
    LPN = 2,        // Licensed Practical Nurse
    RN = 3,         // Registered Nurse
    HHA = 4,        // Home Health Aide
    CPR = 5,
    FirstAid = 6,
    Dementia = 7,
    Alzheimers = 8
}

/// <summary>Invoice status lifecycle.</summary>
public enum InvoiceStatus
{
    Draft = 1,
    Sent = 2,
    Paid = 3,
    Overdue = 4,
    Cancelled = 5
}

/// <summary>Payment methods for accounting.</summary>
public enum PaymentMethod
{
    Cash = 1,
    Check = 2,
    CreditCard = 3,
    BankTransfer = 4,
    Insurance = 5
}
```

---

## 4. Repository Interfaces

### 4.1 IRepository<T> & IUnitOfWork

**File**: `src/CarePath.Domain/Interfaces/Repositories/IRepository.cs`

```csharp
using System.Linq.Expressions;

namespace CarePath.Domain.Interfaces.Repositories;

/// <summary>Generic repository for CRUD operations.</summary>
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(T entity, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default);
}

/// <summary>Unit of work for transactional operations.</summary>
public interface IUnitOfWork : IDisposable
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
```

---

## 5. Testing Strategy

### 5.1 Unit Test Examples

```csharp
using CarePath.Domain.Entities.Scheduling;
using FluentAssertions;
using Xunit;

namespace CarePath.Domain.Tests.Entities;

public class ShiftTests
{
    [Fact]
    public void BillableHours_WithBreaks_CalculatesCorrectly()
    {
        var shift = new Shift
        {
            ActualStartTime = new DateTime(2026, 2, 16, 9, 0, 0, DateTimeKind.Utc),
            ActualEndTime = new DateTime(2026, 2, 16, 17, 0, 0, DateTimeKind.Utc),
            BreakMinutes = 30
        };

        shift.BillableHours.Should().Be(7.5m); // 8 hours - 0.5 break
    }

    [Fact]
    public void GrossMarginPercentage_CalculatesCorrectly()
    {
        var shift = new Shift
        {
            BillRate = 35m,  // $35/hr
            PayRate = 18m    // $18/hr
        };

        shift.GrossMargin.Should().Be(17m);
        shift.GrossMarginPercentage.Should().BeApproximately(48.57m, 0.01m);
    }
}

public class ClientTests
{
    [Fact]
    public void Age_CalculatesCorrectly()
    {
        var client = new Client
        {
            DateOfBirth = new DateTime(1950, 5, 15, 0, 0, 0, DateTimeKind.Utc)
        };

        client.Age.Should().Be(75); // Assuming current date is 2026-02-16
    }
}

public class InvoiceTests
{
    [Fact]
    public void Balance_WithPartialPayment_CalculatesCorrectly()
    {
        var invoice = new Invoice { TaxAmount = 0 };
        invoice.LineItems.Add(new InvoiceLineItem { Hours = 8, RatePerHour = 35 }); // $280
        invoice.Payments.Add(new Payment { Amount = 100, IsSuccessful = true });
        invoice.Payments.Add(new Payment { Amount = 50, IsSuccessful = false }); // Failed

        invoice.Total.Should().Be(280m);
        invoice.AmountPaid.Should().Be(100m); // Only successful payments
        invoice.Balance.Should().Be(180m);
        invoice.IsFullyPaid.Should().BeFalse();
    }
}
```

### 5.2 Test Coverage Goals

- ✅ **100% coverage** of computed properties
- ✅ **100% coverage** of enumerations
- ✅ **90% coverage** of entity classes
- ✅ Zero circular dependencies

---

## 6. Open Questions & Decisions

- [ ] **Value Objects**: Should GPS coordinates be a `record GpsCoordinates(double Lat, double Lon)` value object? **Recommendation**: Phase 2 (keep simple for MVP).
- [ ] **Domain Events**: Implement events like "ShiftCompleted", "CertificationExpiring"? **Recommendation**: Phase 3 (not needed for MVP).
- [ ] **Invoice Number Generation**: Domain layer or Application layer? **Recommendation**: Application layer (requires database sequence).

---

## 7. Success Criteria

✅ All 12 entities created with full XML documentation
✅ All 7 enumerations defined
✅ All computed properties tested (Age, BillableHours, GrossMargin, etc.)
✅ Repository interfaces defined (IRepository<T>, IUnitOfWork)
✅ Zero external dependencies (pure .NET 9)
✅ 100% testable in isolation

---

## 8. Future Considerations: ASP.NET Core Identity Integration

### 8.1 Overview

The current User entity is designed with **PasswordHash** and **RefreshToken** to support custom JWT authentication. However, ASP.NET Core Identity integration is planned for future phases (Infrastructure layer).

### 8.2 Integration Timing

**✅ Current Phase (Domain Layer - Phase 1):**
- Keep User entity pure with zero external dependencies
- Use PasswordHash for custom authentication
- Domain remains testable in isolation

**🔄 Future Phase (Infrastructure Layer - Phase 2+):**
- Integrate `Microsoft.AspNetCore.Identity` in Infrastructure project
- Choose one of two architectural patterns below
- Implement in CP-XX (future spec for Identity integration)

### 8.3 Architectural Patterns for Identity Integration

#### Pattern A: Separate Tables (Clean Architecture - Recommended)

**Domain Layer** (stays unchanged):
```csharp
public class User : BaseEntity
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    // ... rest of domain properties
}
```

**Infrastructure Layer** (new class):
```csharp
using Microsoft.AspNetCore.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>
    /// Foreign key to domain User entity
    /// </summary>
    public Guid DomainUserId { get; set; }

    /// <summary>
    /// Navigation property to domain User
    /// </summary>
    public User DomainUser { get; set; } = null!;
}
```

**Pros:**
- ✅ True Clean Architecture - domain stays pure
- ✅ Domain entities have zero dependency on Identity
- ✅ Can swap Identity framework without touching domain
- ✅ Clear separation of concerns

**Cons:**
- ❌ Two user tables in database (AspNetUsers + Users)
- ❌ Requires synchronization logic between tables
- ❌ More complex queries (joins required)

---

#### Pattern B: Single Table (Pragmatic Approach)

**Domain Layer** (interface only):
```csharp
namespace CarePath.Domain.Entities;

public interface IUser
{
    Guid Id { get; }
    string FirstName { get; set; }
    string LastName { get; set; }
    string Email { get; set; }
    UserRole Role { get; set; }
    bool IsActive { get; set; }
    // ... other domain properties
}
```

**Infrastructure Layer** (concrete implementation):
```csharp
using Microsoft.AspNetCore.Identity;

public class User : IdentityUser<Guid>, IUser
{
    // Implement IUser properties
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public UserRole Role { get; set; }

    // Add BaseEntity audit properties manually
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; } = false;

    // ... rest of domain properties
}
```

**Pros:**
- ✅ Single user table (AspNetUsers only)
- ✅ Simpler database schema
- ✅ No synchronization needed
- ✅ Easier queries (no joins)

**Cons:**
- ❌ Domain depends on Identity abstractions (IUser interface)
- ❌ Harder to swap authentication frameworks
- ❌ Cannot inherit from BaseEntity (IdentityUser already has inheritance)
- ❌ Slightly less "pure" Clean Architecture

---

### 8.4 Recommended Approach for CarePath Health

**Recommendation: Pattern A (Separate Tables)**

**Rationale:**
1. **HIPAA Compliance**: Domain User entity contains PHI (First/Last Name, DOB for linked Client). Keeping it separate from AspNetUsers provides clearer audit trail.
2. **Clean Architecture**: Matches project's strict layering (Domain → Application → Infrastructure → API).
3. **Flexibility**: Can add other authentication providers (Azure AD, OAuth) without touching domain.
4. **Testability**: Domain entities remain 100% testable without mocking Identity framework.

**Migration Path:**
1. **Phase 1 (Current)**: Use custom JWT with PasswordHash
2. **Phase 2 (Infrastructure)**: Add Identity tables alongside existing Users table
3. **Phase 3 (Migration)**: Create ApplicationUser for each User, link via DomainUserId
4. **Phase 4 (Cutover)**: Switch authentication to use Identity, keep domain User for business logic

### 8.5 Design Decisions Deferred to Infrastructure Phase

The following decisions will be made in **CP-XX: Implement ASP.NET Core Identity** (future spec):

❓ **Identity Configuration:**
- Password requirements (length, complexity)
- Lockout policy (failed login attempts)
- Two-factor authentication (2FA) strategy
- Email confirmation requirements

❓ **Database Tables:**
- Use all Identity tables (AspNetUsers, AspNetRoles, AspNetUserRoles, etc.)
- Or minimal set (AspNetUsers only, map to domain UserRole enum)

❓ **Claims Strategy:**
- Map UserRole enum to Role claims
- Add custom claims (EmploymentType, FacilityAccess, etc.)
- Claims transformation pipeline

❓ **Token Strategy:**
- Continue with custom JWT (current RefreshToken approach)
- Or switch to Identity's token providers
- Or hybrid approach

### 8.6 Current Design Compatibility

The current User entity design is **100% compatible** with future Identity integration:

✅ **Email as Username**: Identity can use Email as UserName
✅ **Guid Primary Key**: Identity supports `IdentityUser<Guid>`
✅ **UserRole Enum**: Can map to AspNetRoles or custom claims
✅ **PasswordHash**: Can migrate existing hashes or re-hash on first login
✅ **RefreshToken**: Can coexist with Identity tokens during migration
✅ **IsActive**: Maps to Identity's `LockoutEnabled`
✅ **Soft Deletes**: Compatible with Identity (just filter deleted users)

**No changes needed to domain User entity for future Identity integration.**

---

## 9. Related Documents

- **[Requirements Spec](../01-requirements/cp-01-create-domain-entities.md)**
- **[Tasks Spec](../03-tasks/cp-01-create-domain-entities.md)** (to be created)
- **[Architecture.md](../../Documentation/Architecture.md)**
- **[CarePath_Health.pdf](../../Documentation/CarePath_Health.pdf)**

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-02-16 | Tobi Kareem | Initial comprehensive design |
| 1.1 | 2026-02-16 | Tobi Kareem | Added Section 8: Future Considerations for ASP.NET Core Identity integration |
