# CP-02 — CarePath Transitions: Design

**Status**: Approved  
**Author**: CarePath Health  
**Created**: 2026-06-22  
**Depends on**: CP-01 (Domain layer — complete)

---

## Architecture Overview

CarePath Transitions is a vertical slice built on top of the existing Clean Architecture. No new layers are introduced. New entities live in `Domain/Entities/Transitions/`. New enumerations live in `Domain/Enumerations/`. Application services, validators, and DTOs live in `Application/Transitions/`. Infrastructure adds repositories and external service integrations (Twilio, AI extraction). WebApi adds a `TransitionsController`.

```
CarePath.Domain/
└── Entities/
    └── Transitions/
        ├── DischargeDocument.cs
        ├── TransitionPlan.cs
        ├── TransitionInstruction.cs
        ├── TransitionReminder.cs
        ├── TransitionCheckIn.cs
        └── TransitionEscalation.cs

CarePath.Domain/
└── Enumerations/
    ├── DischargeDocumentSourceType.cs
    ├── DischargeDocumentStatus.cs
    ├── TransitionPlanStatus.cs
    ├── TransitionRiskLevel.cs
    ├── TransitionInstructionCategory.cs
    ├── TransitionInstructionStatus.cs
    ├── ReminderType.cs
    ├── ReminderChannel.cs
    ├── ReminderStatus.cs
    ├── EscalationTriggerType.cs
    └── EscalationLevel.cs

CarePath.Application/
└── Transitions/
    ├── Commands/
    │   ├── UploadDischargeDocumentCommand.cs
    │   ├── ActivateTransitionPlanCommand.cs
    │   ├── RecordCheckInCommand.cs
    │   └── TriggerEscalationCommand.cs
    ├── Queries/
    │   ├── GetActiveTransitionPlansQuery.cs       # coordinator dashboard
    │   ├── GetTransitionPlanDetailQuery.cs
    │   └── GetTransitionPlanForClientQuery.cs
    ├── DTOs/
    └── Validators/

CarePath.Infrastructure/
└── Transitions/
    ├── Repositories/
    └── Services/
        ├── DischargeExtractionService.cs          # AI extraction wrapper
        └── TwilioReminderService.cs               # SMS + Voice delivery

CarePath.WebApi/
└── Controllers/
    └── TransitionsController.cs
```

---

## Entity Designs

### DischargeDocument

```csharp
using CarePath.Domain.Entities.Common;
using CarePath.Domain.Enumerations;

namespace CarePath.Domain.Entities.Transitions;

/// <summary>
/// The source discharge document uploaded or imported for a client.
/// Holds raw content and the AI extraction status.
/// </summary>
public class DischargeDocument : BaseEntity
{
    /// <summary>Client this discharge document belongs to.</summary>
    public Guid ClientId { get; set; }

    /// <summary>How the document was ingested.</summary>
    public DischargeDocumentSourceType SourceType { get; set; }

    /// <summary>
    /// Extracted raw text or FHIR JSON payload.
    /// PHI — never log this field.
    /// </summary>
    public string? RawContent { get; set; }

    /// <summary>Original filename or FHIR resource identifier.</summary>
    public string? SourceReference { get; set; }

    /// <summary>Current processing state of the document.</summary>
    public DischargeDocumentStatus Status { get; set; } = DischargeDocumentStatus.Pending;

    /// <summary>UserId of the coordinator or clinician who uploaded this.</summary>
    public Guid UploadedBy { get; set; }

    /// <summary>UTC timestamp when the document was received.</summary>
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Client? Client { get; set; }
    public IReadOnlyList<TransitionPlan> TransitionPlans { get; private set; } = new List<TransitionPlan>();
}
```

### TransitionPlan

```csharp
using CarePath.Domain.Entities.Common;
using CarePath.Domain.Enumerations;

namespace CarePath.Domain.Entities.Transitions;

/// <summary>
/// The clinician-verified, activated 30-day post-discharge care plan.
/// One plan per discharge episode per client.
/// </summary>
public class TransitionPlan : BaseEntity
{
    public Guid ClientId { get; set; }
    public Guid DischargeDocumentId { get; set; }

    public string? HospitalName { get; set; }

    /// <summary>UTC date the patient was discharged.</summary>
    public DateTime DischargeDate { get; set; }

    /// <summary>DischargeDate + 30 days UTC. End of the transition monitoring window.</summary>
    public DateTime TransitionWindowEnd { get; set; }

    public TransitionPlanStatus Status { get; set; } = TransitionPlanStatus.Draft;

    /// <summary>Risk stratification — drives reminder intensity.</summary>
    public TransitionRiskLevel RiskLevel { get; set; } = TransitionRiskLevel.Medium;

    /// <summary>UserId of the clinician who approved and activated this plan.</summary>
    public Guid? VerifiedBy { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public DateTime? ActivatedAt { get; set; }

    // Computed — no EF involvement
    public bool IsActive => Status == TransitionPlanStatus.Active && DateTime.UtcNow <= TransitionWindowEnd;
    public int DaysRemaining => Math.Max(0, (TransitionWindowEnd - DateTime.UtcNow).Days);

    // Navigation
    public Client? Client { get; set; }
    public DischargeDocument? DischargeDocument { get; set; }
    public IReadOnlyList<TransitionInstruction> Instructions { get; private set; } = new List<TransitionInstruction>();
    public IReadOnlyList<TransitionReminder> Reminders { get; private set; } = new List<TransitionReminder>();
    public IReadOnlyList<TransitionCheckIn> CheckIns { get; private set; } = new List<TransitionCheckIn>();
    public IReadOnlyList<TransitionEscalation> Escalations { get; private set; } = new List<TransitionEscalation>();
}
```

### TransitionInstruction

```csharp
using CarePath.Domain.Entities.Common;
using CarePath.Domain.Enumerations;

namespace CarePath.Domain.Entities.Transitions;

/// <summary>
/// A single instruction extracted from a discharge document.
/// Carries AI confidence score and source text for clinical review.
/// </summary>
public class TransitionInstruction : BaseEntity
{
    public Guid TransitionPlanId { get; set; }

    public TransitionInstructionCategory Category { get; set; }

    /// <summary>Plain-language version of the instruction shown to the patient.</summary>
    public string InstructionText { get; set; } = string.Empty;

    /// <summary>
    /// The original text from the discharge document this was extracted from.
    /// PHI — never log this field.
    /// </summary>
    public string? SourceText { get; set; }

    /// <summary>AI extraction confidence (0.0 – 1.0).</summary>
    public decimal ConfidenceScore { get; set; }

    /// <summary>Clinician note added during review (optional).</summary>
    public string? ClinicalNote { get; set; }

    /// <summary>Whether a pharmacist should review this instruction before activation.</summary>
    public bool NeedsPharmacistReview { get; set; }

    public TransitionInstructionStatus Status { get; set; } = TransitionInstructionStatus.Pending;

    // Computed
    public bool IsLowConfidence => ConfidenceScore < 0.75m;

    // Navigation
    public TransitionPlan? TransitionPlan { get; set; }
}
```

### TransitionReminder

```csharp
using CarePath.Domain.Entities.Common;
using CarePath.Domain.Enumerations;

namespace CarePath.Domain.Entities.Transitions;

/// <summary>
/// A scheduled reminder or check-in prompt delivered to the patient.
/// </summary>
public class TransitionReminder : BaseEntity
{
    public Guid TransitionPlanId { get; set; }

    /// <summary>The specific instruction this reminder is for, if applicable.</summary>
    public Guid? TransitionInstructionId { get; set; }

    public ReminderType ReminderType { get; set; }
    public ReminderChannel Channel { get; set; }

    public DateTime ScheduledAt { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? AcknowledgedAt { get; set; }

    public ReminderStatus Status { get; set; } = ReminderStatus.Scheduled;

    /// <summary>Number of delivery retry attempts made.</summary>
    public int RetryCount { get; set; }

    // Computed
    public bool IsOverdue => Status == ReminderStatus.Scheduled && ScheduledAt < DateTime.UtcNow;

    // Navigation
    public TransitionPlan? TransitionPlan { get; set; }
}
```

### TransitionCheckIn

```csharp
using CarePath.Domain.Entities.Common;
using CarePath.Domain.Enumerations;

namespace CarePath.Domain.Entities.Transitions;

/// <summary>
/// A patient symptom/adherence check-in response.
/// </summary>
public class TransitionCheckIn : BaseEntity
{
    public Guid TransitionPlanId { get; set; }

    public DateTime CheckInDate { get; set; } = DateTime.UtcNow;
    public ReminderChannel Channel { get; set; }

    /// <summary>
    /// JSON serialization of the patient's answers to check-in questions.
    /// PHI — never log this field.
    /// </summary>
    public string ResponsesJson { get; set; } = "{}";

    /// <summary>
    /// True if any response matches a clinician-defined warning symptom.
    /// Triggers an immediate escalation evaluation when set.
    /// </summary>
    public bool ContainsWarningSymptom { get; set; }

    /// <summary>UserId of the coordinator who reviewed this check-in, if reviewed.</summary>
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }

    // Navigation
    public TransitionPlan? TransitionPlan { get; set; }
}
```

### TransitionEscalation

```csharp
using CarePath.Domain.Entities.Common;
using CarePath.Domain.Enumerations;

namespace CarePath.Domain.Entities.Transitions;

/// <summary>
/// An escalation event triggered by a missed task, warning symptom, or failed contact.
/// </summary>
public class TransitionEscalation : BaseEntity
{
    public Guid TransitionPlanId { get; set; }

    public EscalationTriggerType TriggerType { get; set; }

    /// <summary>Human-readable description of what triggered this escalation.</summary>
    public string TriggerDetails { get; set; } = string.Empty;

    public EscalationLevel EscalationLevel { get; set; }

    public DateTime EscalatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UserId of the coordinator who acknowledged this escalation.</summary>
    public Guid? AcknowledgedBy { get; set; }
    public DateTime? AcknowledgedAt { get; set; }

    /// <summary>What action was taken (documented by coordinator).</summary>
    public string? ResolutionNote { get; set; }

    // Navigation
    public TransitionPlan? TransitionPlan { get; set; }
}
```

---

## Enumerations

```csharp
// DischargeDocumentSourceType.cs
namespace CarePath.Domain.Enumerations;
public enum DischargeDocumentSourceType { PdfUpload, PhotoUpload, FhirImport }

// DischargeDocumentStatus.cs
namespace CarePath.Domain.Enumerations;
public enum DischargeDocumentStatus { Pending, Extracting, AwaitingReview, Approved, Rejected }

// TransitionPlanStatus.cs
namespace CarePath.Domain.Enumerations;
public enum TransitionPlanStatus { Draft, PendingVerification, Active, Completed, Cancelled }

// TransitionRiskLevel.cs
namespace CarePath.Domain.Enumerations;
public enum TransitionRiskLevel { Low, Medium, High }

// TransitionInstructionCategory.cs
namespace CarePath.Domain.Enumerations;
public enum TransitionInstructionCategory
{ Medication, Appointment, Diet, Activity, WoundCare, WarningSigns, Equipment, Other }

// TransitionInstructionStatus.cs
namespace CarePath.Domain.Enumerations;
public enum TransitionInstructionStatus { Pending, Approved, Rejected, Modified }

// ReminderType.cs
namespace CarePath.Domain.Enumerations;
public enum ReminderType
{ Medication, Appointment, SymptomCheckIn, Refill, Equipment, Activity, Diet }

// ReminderChannel.cs
namespace CarePath.Domain.Enumerations;
public enum ReminderChannel { App, Sms, Voice }

// ReminderStatus.cs
namespace CarePath.Domain.Enumerations;
public enum ReminderStatus { Scheduled, Sent, Acknowledged, Missed, Failed }

// EscalationTriggerType.cs
namespace CarePath.Domain.Enumerations;
public enum EscalationTriggerType
{ MissedCriticalTask, WarningSymptomsReported, FailedContact, CaregiverAlert }

// EscalationLevel.cs
namespace CarePath.Domain.Enumerations;
public enum EscalationLevel { CoordinatorAlert, FamilyNotification, UrgentCare, Emergency911 }
```

---

## Existing Entity Additions

### VisitNote — add optional TransitionPlanId

Add to `CarePath.Domain/Entities/Scheduling/VisitNote.cs`:

```csharp
/// <summary>
/// Optional link to an active transition plan.
/// When set, this VisitNote contributes caregiver adherence observations
/// to the patient's 30-day post-discharge monitoring.
/// </summary>
public Guid? TransitionPlanId { get; set; }
```

---

## API Endpoints (planned)

All endpoints require JWT auth. PHI endpoints require role-based authorization.

| Method | Route | Role | Description |
|---|---|---|---|
| POST | `/api/transitions/documents` | Coordinator | Upload discharge document |
| GET | `/api/transitions/documents/{id}` | Coordinator, Clinician | Get document with extraction status |
| GET | `/api/transitions/plans` | Coordinator | Get all active plans (dashboard) |
| GET | `/api/transitions/plans/{id}` | Coordinator, Clinician, Caregiver | Get plan detail |
| POST | `/api/transitions/plans/{id}/activate` | Clinician | E-sign and activate plan |
| GET | `/api/transitions/plans/client/{clientId}` | Coordinator, Caregiver | Get plan for a specific client |
| POST | `/api/transitions/plans/{id}/checkins` | System (Twilio webhook) | Record patient check-in response |
| GET | `/api/transitions/plans/{id}/escalations` | Coordinator | Get escalation history |
| POST | `/api/transitions/escalations/{id}/acknowledge` | Coordinator | Acknowledge and document resolution |

---

## Infrastructure Services

### DischargeExtractionService (Interface)

```csharp
namespace CarePath.Application.Transitions.Interfaces;

public interface IDischargeExtractionService
{
    Task<IReadOnlyList<ExtractedInstructionDto>> ExtractAsync(
        string rawContent,
        DischargeDocumentSourceType sourceType,
        CancellationToken cancellationToken = default);
}
```

Implementations in Infrastructure will wrap the AI provider (TBD — OpenAI GPT-4o or Azure OpenAI). The interface lives in Application to keep the dependency rule clean.

### IReminderDeliveryService (Interface)

```csharp
namespace CarePath.Application.Transitions.Interfaces;

public interface IReminderDeliveryService
{
    Task<bool> SendSmsAsync(string toPhoneNumber, string message, CancellationToken ct = default);
    Task<bool> InitiateVoiceCallAsync(string toPhoneNumber, string twimlUrl, CancellationToken ct = default);
}
```

`TwilioReminderService` in Infrastructure implements this interface. Never inject Twilio SDK directly into Application.

---

## HIPAA Considerations

- `DischargeDocument.RawContent` and `TransitionInstruction.SourceText` must never appear in logs, exception messages, or URLs.
- Every endpoint that reads or writes a `TransitionPlan` or its child entities must emit an audit log entry.
- `TransitionCheckIn.ResponsesJson` is PHI — store encrypted at rest (handled by SQL Server TDE); never deserialize into logs.
- Role checks: Coordinator and Clinician can read any plan. Caregiver can only read plans for clients on their own shifts. Family Proxy role reads only the plans they are explicitly linked to.

---

## Architectural Decisions

### ADR-001 — ResponsesJson as a JSON column, not a normalized table
**Decision**: Store check-in answers as a JSON string in a single column rather than a `TransitionCheckInAnswer` child table.  
**Reason**: The check-in question set may evolve without migrations; JSON column is flexible and EF Core 9 supports JSON querying. Coordinator review is done via the UI, not SQL queries.  
**Trade-off**: Cannot query individual answers in SQL without JSON functions; acceptable at MVP scale.

### ADR-002 — DischargeDocument extraction is asynchronous
**Decision**: Document upload returns `202 Accepted` immediately. Extraction runs as a background job (Hangfire or .NET BackgroundService). Status is polled via GET endpoint.  
**Reason**: AI extraction may take 5–30 seconds. Blocking the HTTP request is unacceptable.

### ADR-003 — No autonomous escalation beyond CoordinatorAlert
**Decision**: The system will never automatically contact a patient's family, recommend urgent care, or call 911. It only creates a `TransitionEscalation` record and surfaces it on the coordinator dashboard. The coordinator makes all human contact decisions.  
**Reason**: Regulatory safety. Autonomous clinical guidance without a licensed human in the loop is a liability.

### ADR-004 — Twilio as SMS/Voice provider
**Decision**: Twilio for all outbound SMS and voice call delivery.  
**Reason**: Already referenced in the CarePath stack. Twilio has HIPAA Business Associate Agreements available. Voice IVR is critical for elderly patients without smartphones.
