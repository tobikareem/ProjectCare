# Requirements Specification: CP-01 Create Domain Entities

**Date**: 2026-02-16
**Author**: Tobi Kareem
**Project**: CarePath Health
**Status**: Approved
**Related Specs**:
- [Architecture.md](../../Documentation/Architecture.md) - System architecture
- [CarePath_Health.pdf](../../Documentation/CarePath_Health.pdf) - Business model & playbook
- [Design Spec](../02-design/cp-01-create-domain-entities.md) - To be created after approval

---

## Executive Summary

> Establish the foundational domain entities for CarePath Health's dual-service healthcare business model (In-Home Care and Healthcare Staffing), implementing Clean Architecture principles with comprehensive support for users, caregivers, clients, shifts, visit documentation, invoicing, and margin tracking to achieve target gross margins of 40-45% for in-home care and 25-30% for facility staffing.

---

## 1. Problem Statement

### 1.1 Current State
- **No domain model exists**: The CarePath Health platform requires a foundational domain layer to model the healthcare business, but currently lacks the core entities needed to represent users, caregivers, clients, shifts, care documentation, and financial transactions.
- **Business model not captured**: The dual-service model (In-Home Care W-2 employees vs Healthcare Staffing 1099 contractors) with different margin targets needs to be encoded in the domain layer.
- **No data structure for operations**: Without domain entities, the system cannot track caregivers, schedule shifts, document care activities, or generate invoices for the Maryland healthcare market.
- **Compliance requirements unaddressed**: HIPAA-compliant data models for Protected Health Information (PHI) are not yet defined.

### 1.2 Business Impact
**CarePath Health Dual-Service Model**:
1. **In-Home Care Service Line** (40-45% target margin)
   - W-2 employees providing care in client homes
   - Requires comprehensive caregiver scheduling, GPS tracking, visit documentation
   - Bill Rate: $30-45/hour | Pay Rate: $16-20/hour + employer taxes (~25%)
   - Target margin: **40-45%**

2. **Healthcare Staffing Service Line** (25-30% target margin)
   - 1099 contractors staffing healthcare facilities (hospitals, nursing homes)
   - CNAs, LPNs, RNs placed at facilities
   - Bill Rate: CNA $30-40/hr, LPN $50-65/hr, RN $70-90/hr
   - Pay Rate: 70-75% of bill rate (1099 contractors)
   - Target margin: **25-30%**

**Without proper domain entities**:
- ❌ Cannot track caregiver employment types (W-2 vs 1099)
- ❌ Cannot calculate margins per shift or per service line
- ❌ Cannot enforce business rules (e.g., certification requirements)
- ❌ Cannot scale operations beyond manual tracking
- ❌ Risk non-compliance with HIPAA and Maryland healthcare regulations

**Revenue Impact**:
- Target: 75 caregivers by Month 4
- Average caregiver works 30 hrs/week at $35/hour bill rate = $1,050/week revenue
- 75 caregivers × $1,050 × 4 weeks = **$315,000 monthly revenue**
- With proper margin tracking, gross profit = ~$125,000/month at 40% margin

### 1.3 User Impact

**Affected User Roles**:
1. **Administrators** (Business Owners)
   - Need to manage caregivers, clients, and track business metrics
   - Require margin visibility per service line
   - Must ensure compliance with certifications and regulations

2. **Care Coordinators**
   - Schedule caregivers to client shifts
   - Monitor shift completion and visit notes
   - Handle billing and invoicing

3. **Caregivers** (W-2 and 1099)
   - Must have profiles with certifications, availability, skills
   - Need ability to document care activities (visit notes)
   - Require shift assignments and time tracking

4. **Clients** (Care Recipients)
   - Need profiles with care requirements, medical conditions, insurance info
   - Require care plans documenting their specific needs
   - Family members need visibility into care provided

5. **Facility Managers** (for Staffing service line)
   - Request caregivers with specific certifications
   - Track staffing coverage at their facilities

**Current Pain Points Without Domain Entities**:
- No centralized caregiver database
- Manual scheduling via spreadsheets/text messages
- Paper-based visit notes (compliance risk)
- Manual invoice creation (time-consuming, error-prone)
- No visibility into which caregivers are certified for which services
- Cannot track margins in real-time

---

## 2. User Stories

### 2.1 Primary User Stories - Administrator

```gherkin
As an Administrator
I want to manage a database of caregivers with their certifications and employment types
So that I can assign qualified caregivers to appropriate shifts and track labor costs

Acceptance Criteria:
- Given I am logged in as an Administrator
- When I create a new caregiver profile
- Then the system captures: name, contact info, employment type (W-2 or 1099), hourly pay rate, certifications (CNA, LPN, RN, etc.), skills (dementia care, mobility assistance), availability (weekdays, weekends, nights), and hire date
- And the system validates that required certifications are not expired
- And the system calculates potential margin based on employment type and service line
```

```gherkin
As an Administrator
I want to track gross margins for each shift and service line
So that I can ensure we meet our target margins (40-45% in-home, 25-30% staffing)

Acceptance Criteria:
- Given a shift has been completed
- When I view the shift details
- Then the system displays: bill rate, pay rate, gross margin ($), gross margin (%)
- And the system indicates if margin is below target for that service line
- And I can generate a report showing margins by service line for any date range
```

```gherkin
As an Administrator
I want to manage client profiles with their care requirements and billing information
So that I can match them with qualified caregivers and bill correctly

Acceptance Criteria:
- Given I am creating a new client profile
- When I enter client information
- Then the system captures: name, DOB, address with GPS coordinates, emergency contacts, medical conditions, care requirements (dementia care, mobility assistance, medication management), service type (in-home or facility), hourly bill rate, insurance information
- And the system calculates the client's age automatically
- And the system stores GPS coordinates for check-in/out verification
```

### 2.2 Primary User Stories - Care Coordinator

```gherkin
As a Care Coordinator
I want to schedule caregivers to client shifts
So that clients receive care and caregivers have work assignments

Acceptance Criteria:
- Given I am scheduling a shift
- When I create a new shift
- Then the system captures: client, caregiver (optional if unassigned), scheduled start/end time, service type, bill rate, pay rate, and shift status (Scheduled)
- And the system validates that the caregiver has required certifications for the client's needs
- And the system calculates the expected duration and margin for the shift
- And the system prevents double-booking a caregiver for overlapping shifts
```

```gherkin
As a Care Coordinator
I want to generate invoices for clients based on completed shifts
So that we can bill clients accurately and maintain cash flow

Acceptance Criteria:
- Given multiple shifts have been completed for a client
- When I generate an invoice
- Then the system creates an invoice with: invoice number (auto-generated), client info, invoice date, due date, line items (one per shift), subtotal, tax, total
- And each line item shows: service date, description, hours worked, rate per hour, total
- And the system calculates gross margin per line item (for internal tracking)
- And I can mark the invoice status as Draft, Sent, Paid, Overdue, or Cancelled
```

### 2.3 Primary User Stories - Caregiver

```gherkin
As a Caregiver
I want to view my assigned shifts
So that I know when and where to provide care

Acceptance Criteria:
- Given I am logged in as a Caregiver
- When I view my schedule
- Then I see all shifts assigned to me with: client name (or facility name), address, scheduled date/time, service type, any special instructions
- And I can filter by date range
- And I see shifts ordered by start time
```

```gherkin
As a Caregiver
I want to document care activities with visit notes after each shift
So that there is a record of services provided and client condition

Acceptance Criteria:
- Given I have completed a shift
- When I create a visit note
- Then the system captures: visit date/time, activities performed (checkboxes: personal care, meal prep, medication, housekeeping, companionship, transportation, exercise), detailed notes (activities, client condition, concerns, medications administered), optional vital signs (blood pressure, temperature, heart rate), photos (if applicable), caregiver signature
- And the visit note is linked to the specific shift and client
- And the system timestamps when the note was created
```

### 2.4 Primary User Stories - Client/Family

```gherkin
As a Client or Family Member
I want to have a care plan documenting my care goals and interventions
So that all caregivers understand my specific needs

Acceptance Criteria:
- Given I am a client with a profile in the system
- When a care plan is created for me
- Then it includes: title, description, start date, end date (if applicable), goals, interventions, notes, active status
- And the care plan is accessible to all caregivers assigned to my shifts
- And care coordinators can update the care plan as my needs change
```

### 2.5 Secondary User Stories

```gherkin
As an Administrator
I want to track caregiver performance metrics
So that I can identify top performers and address issues

Acceptance Criteria:
- Given a caregiver has completed multiple shifts
- When I view their profile
- Then I see: average rating (if ratings implemented), total shifts completed, no-show count
- And I can use these metrics to make staffing decisions
```

```gherkin
As an Administrator
I want to ensure caregivers maintain valid certifications
So that we remain compliant with Maryland healthcare regulations

Acceptance Criteria:
- Given a caregiver has certifications on file
- When I view their certification details
- Then I see: certification type, number, issue date, expiration date, issuing authority
- And the system flags certifications expiring within 30 days
- And the system prevents assigning caregivers to shifts requiring expired certifications
```

### 2.6 Edge Cases

**Employment Type Scenarios**:
- What happens if a caregiver's employment type changes from W-2 to 1099 (or vice versa)?
  - Historical shifts retain original employment type and pay rate
  - Future shifts use new employment type and updated pay rate

**Certification Expiration**:
- What if a caregiver's CNA certification expires mid-contract?
  - System flags the caregiver as non-compliant
  - Care coordinator is notified to reassign future shifts
  - Caregiver cannot be assigned to shifts requiring that certification

**Shift Cancellation**:
- What if a client cancels a shift or a caregiver is a no-show?
  - Shift status changes to "Cancelled" or "NoShow"
  - Cancellation reason is captured
  - No invoice line item is created for that shift (or it's marked as non-billable)

**Dual Service Lines**:
- Can a caregiver work both in-home care and facility staffing?
  - Yes, but shifts must clearly indicate service type
  - Margins are calculated differently based on service type
  - Employment type (W-2 vs 1099) may differ by service line

**Invoice Payments**:
- What if a client makes partial payments or payment fails?
  - System tracks payments separately from invoices
  - Each payment has: amount, date, method, reference number, success status
  - Invoice balance = total - sum of successful payments
  - Invoice status updates to "Paid" when balance ≤ 0

---

## 3. Functional Requirements

### 3.1 Core Functionality

| ID | Requirement | Priority | User Role(s) | Service Line |
|----|-------------|----------|--------------|--------------|
| **Users & Authentication** | | | | |
| FR-001 | System must support user registration with role-based access (Admin, Coordinator, Caregiver, Client, Facility Manager) | Critical | All | Both |
| FR-002 | User entity must store: first/last name, email, phone, address (street, city, state, zip), role, active status, last login timestamp | Critical | Admin | Both |
| FR-003 | System must track who created/updated records (audit trail) | High | Admin | Both |
| FR-004 | System must support soft deletes (IsDeleted flag) to preserve data integrity | High | Admin | Both |
| **Caregivers** | | | | |
| FR-005 | System must differentiate W-2 employees from 1099 contractors | Critical | Admin, Coordinator | Both |
| FR-006 | Caregiver entity must store: employment type, hourly pay rate, hire date, termination date (if applicable) | Critical | Admin | Both |
| FR-007 | System must track caregiver certifications: type (CNA, LPN, RN, HHA, CPR, FirstAid, Dementia, Alzheimers), number, issue date, expiration date, issuing authority | Critical | Admin, Coordinator | Both |
| FR-008 | System must flag certifications expiring within 30 days | High | Admin, Coordinator | Both |
| FR-009 | Caregiver entity must store skills/specialties: dementia care, Alzheimer's care, mobility assistance, medication management | High | Coordinator | Both |
| FR-010 | System must track caregiver availability: weekdays, weekends, nights, max weekly hours | High | Coordinator | Both |
| FR-011 | System must track performance metrics: average rating, total shifts completed, no-show count | Medium | Admin | Both |
| **Clients** | | | | |
| FR-012 | Client entity must store: date of birth (with auto-calculated age), emergency contact info, medical conditions, allergies | Critical | Admin, Coordinator | In-Home Care |
| FR-013 | System must store client care requirements: dementia care, mobility assistance, medication management, companionship | Critical | Coordinator | In-Home Care |
| FR-014 | System must store client service details: service type (in-home or facility), hourly bill rate, estimated weekly hours | Critical | Admin, Coordinator | Both |
| FR-015 | System must store GPS coordinates (latitude/longitude) for client addresses for check-in verification | High | Coordinator | In-Home Care |
| FR-016 | System must store insurance information: provider, policy number, Medicaid number | High | Admin, Coordinator | In-Home Care |
| FR-017 | System must support multiple care plans per client with start/end dates, goals, interventions | Medium | Coordinator, Caregiver | In-Home Care |
| **Shifts** | | | | |
| FR-018 | Shift entity must store: client, caregiver (nullable if unassigned), scheduled start/end time, actual start/end time, status (Scheduled, InProgress, Completed, Cancelled, NoShow) | Critical | Coordinator, Caregiver | Both |
| FR-019 | Shift entity must store financial details: bill rate, pay rate, overtime pay rate, weekend premium, holiday premium | Critical | Admin, Coordinator | Both |
| FR-020 | System must calculate billable hours: (actual end - actual start - break minutes) / 60 | Critical | Admin, Coordinator | Both |
| FR-021 | System must calculate gross margin: bill rate - pay rate, and gross margin %: (margin / bill rate) × 100 | Critical | Admin | Both |
| FR-022 | System must store GPS tracking for check-in/out: latitude, longitude, timestamp | High | Caregiver | In-Home Care |
| FR-023 | System must track break time (unpaid minutes) per shift | Medium | Caregiver, Coordinator | Both |
| FR-024 | System must store cancellation reason and timestamp if shift is cancelled | Medium | Coordinator | Both |
| FR-025 | System must prevent double-booking (caregiver assigned to overlapping shifts) | High | Coordinator | Both |
| **Visit Notes** | | | | |
| FR-026 | Visit note entity must store: shift, caregiver, visit date/time, activities performed (checkboxes), detailed notes (activities, client condition, concerns, medications) | Critical | Caregiver | In-Home Care |
| FR-027 | System must optionally capture vital signs: blood pressure (systolic/diastolic), temperature, heart rate | Medium | Caregiver | In-Home Care |
| FR-028 | System must support photo attachments (URLs) with captions and timestamps | Medium | Caregiver | In-Home Care |
| FR-029 | System must capture digital signatures: caregiver signature, client/family signature | Low | Caregiver, Client | In-Home Care |
| **Invoices** | | | | |
| FR-030 | Invoice entity must store: invoice number (auto-generated), client, invoice date, due date, paid date, status (Draft, Sent, Paid, Overdue, Cancelled) | Critical | Admin, Coordinator | Both |
| FR-031 | Invoice line items must reference shifts and include: description, service date, hours, rate per hour, calculated total | Critical | Coordinator | Both |
| FR-032 | System must calculate invoice totals: subtotal (sum of line items), tax amount, total (subtotal + tax) | Critical | Admin, Coordinator | Both |
| FR-033 | System must track internal costs per line item for margin analysis: cost per hour, total cost, gross profit, gross margin % | High | Admin | Both |
| FR-034 | Payment entity must store: invoice, payment date, amount, method (Cash, Check, CreditCard, BankTransfer, Insurance), reference number, success status, failure reason | High | Admin, Coordinator | Both |
| FR-035 | System must calculate invoice balance: total - sum of successful payments | Critical | Admin, Coordinator | Both |
| FR-036 | System must automatically update invoice status to "Paid" when balance ≤ 0 | High | System | Both |

### 3.2 Data Requirements

**Entities to Create**:
1. **BaseEntity** (abstract base class)
   - Id (Guid, primary key)
   - CreatedAt (DateTime, UTC)
   - UpdatedAt (DateTime?, UTC)
   - CreatedBy (string?, user ID)
   - UpdatedBy (string?, user ID)
   - IsDeleted (bool, soft delete flag)

2. **User** (extends BaseEntity)
   - FirstName, LastName, Email, PhoneNumber
   - Address, City, State (default "Maryland"), ZipCode
   - Role (enum: Admin, Coordinator, Caregiver, Client, FacilityManager)
   - IsActive, LastLoginAt
   - Computed: FullName

3. **Caregiver** (extends BaseEntity)
   - UserId (FK to User)
   - EmploymentType (enum: W2Employee, Contractor1099)
   - HourlyPayRate, HireDate, TerminationDate
   - Skills: HasDementiaCare, HasAlzheimersCare, HasMobilityAssistance, HasMedicationManagement
   - Availability: AvailableWeekdays, AvailableWeekends, AvailableNights, MaxWeeklyHours
   - Performance: AverageRating, TotalShiftsCompleted, NoShowCount
   - Collections: Certifications, Shifts, VisitNotes

4. **CaregiverCertification** (extends BaseEntity)
   - CaregiverId (FK)
   - Type (enum: CNA, LPN, RN, HHA, CPR, FirstAid, Dementia, Alzheimers)
   - CertificationNumber, IssueDate, ExpirationDate, IssuingAuthority
   - Computed: IsExpired, IsExpiringSoon (< 30 days)

5. **Client** (extends BaseEntity)
   - UserId (FK to User)
   - DateOfBirth, EmergencyContactName, EmergencyContactPhone, EmergencyContactRelationship
   - Care Requirements: RequiresDementiaCare, RequiresMobilityAssistance, RequiresMedicationManagement, RequiresCompanionship
   - MedicalConditions, Allergies, SpecialInstructions
   - ServiceType (enum: InHomeCare, FacilityStaffing)
   - HourlyBillRate, EstimatedWeeklyHours
   - GPS: Latitude, Longitude, LocationNotes
   - Insurance: InsuranceProvider, InsurancePolicyNumber, MedicaidNumber
   - Collections: Shifts, CarePlans, Invoices
   - Computed: Age

6. **CarePlan** (extends BaseEntity)
   - ClientId (FK)
   - Title, Description, StartDate, EndDate, IsActive
   - Goals, Interventions, Notes

7. **Shift** (extends BaseEntity)
   - ClientId (FK), CaregiverId (FK, nullable)
   - ScheduledStartTime, ScheduledEndTime, ActualStartTime, ActualEndTime
   - Status (enum: Scheduled, InProgress, Completed, Cancelled, NoShow)
   - ServiceType (enum: InHomeCare, FacilityStaffing)
   - Financial: BillRate, PayRate, OvertimePayRate, WeekendPremium, HolidayPremium
   - GPS: CheckInLatitude, CheckInLongitude, CheckInTime, CheckOutLatitude, CheckOutLongitude, CheckOutTime
   - BreakMinutes, Notes, CancellationReason, CancelledAt
   - Collections: VisitNotes
   - Computed: ScheduledDuration, ActualDuration, BillableHours, GrossMargin, GrossMarginPercentage

8. **VisitNote** (extends BaseEntity)
   - ShiftId (FK), CaregiverId (FK)
   - VisitDateTime
   - Activities: PersonalCare, MealPreparation, Medication, LightHousekeeping, Companionship, Transportation, Exercise
   - Detailed: Activities, ClientCondition, Concerns, Medications
   - Vitals: BloodPressureSystolic, BloodPressureDiastolic, Temperature, HeartRate
   - Collections: Photos
   - Signatures: CaregiverSignature, ClientOrFamilySignature

9. **VisitPhoto** (extends BaseEntity)
   - VisitNoteId (FK)
   - PhotoUrl, Caption, TakenAt

10. **Invoice** (extends BaseEntity)
    - InvoiceNumber (auto-generated)
    - ClientId (FK)
    - InvoiceDate, DueDate, PaidDate
    - Status (enum: Draft, Sent, Paid, Overdue, Cancelled)
    - TaxAmount, Notes
    - Collections: LineItems, Payments
    - Computed: Subtotal, Total, AmountPaid, Balance, IsFullyPaid

11. **InvoiceLineItem** (extends BaseEntity)
    - InvoiceId (FK), ShiftId (FK, nullable)
    - Description, ServiceDate, Hours, RatePerHour
    - CostPerHour (for internal margin tracking)
    - Computed: Total, TotalCost, GrossProfit, GrossMarginPercentage

12. **Payment** (extends BaseEntity)
    - InvoiceId (FK)
    - PaymentDate, Amount
    - Method (enum: Cash, Check, CreditCard, BankTransfer, Insurance)
    - ReferenceNumber, Notes
    - IsSuccessful, FailureReason

**Enumerations**:
- UserRole: Admin, Coordinator, Caregiver, Client, FacilityManager
- EmploymentType: W2Employee, Contractor1099
- CertificationType: CNA, LPN, RN, HHA, CPR, FirstAid, Dementia, Alzheimers
- ServiceType: InHomeCare, FacilityStaffing
- ShiftStatus: Scheduled, InProgress, Completed, Cancelled, NoShow
- InvoiceStatus: Draft, Sent, Paid, Overdue, Cancelled
- PaymentMethod: Cash, Check, CreditCard, BankTransfer, Insurance

**Validation Rules**:
- Email must be valid format and unique per user
- Phone numbers must be valid US format (preferably Maryland area codes)
- Hourly rates (pay rate, bill rate) must be > 0
- Pay rate must be < bill rate (to ensure positive margin)
- Shift scheduled end time must be > scheduled start time
- Certification expiration date must be > issue date
- Invoice due date must be >= invoice date
- Payment amount must be > 0 and <= invoice balance
- GPS coordinates: latitude (-90 to 90), longitude (-180 to 180)

### 3.3 Integration Requirements

**Layers Involved**:
1. **Domain Layer** (CarePath.Domain)
   - Define all entities, enums, value objects
   - Define repository interfaces (IRepository<T>, ICaregiverRepository, etc.)
   - No dependencies on other layers

2. **Application Layer** (CarePath.Application) - Future Phase
   - Will use domain entities
   - Map entities to DTOs

3. **Infrastructure Layer** (CarePath.Infrastructure) - Future Phase
   - EF Core entity configurations
   - Repository implementations
   - Database migrations

4. **API Layer** (CarePath.Api) - Future Phase
   - Expose entities via REST API

**No External Services for Domain Layer**:
- Domain entities are pure C# classes with no external dependencies
- External services (GPS, Email, SMS) will be integrated in later phases via Infrastructure layer

### 3.4 Security & Authorization

**HIPAA Compliance Requirements**:
1. **Protected Health Information (PHI)** stored in entities:
   - Client: DateOfBirth, MedicalConditions, Allergies, InsuranceInfo
   - CarePlan: Goals, Interventions, Notes (may contain PHI)
   - VisitNote: ClientCondition, Concerns, Medications, Vitals, Photos

2. **Audit Trail**:
   - BaseEntity tracks CreatedBy, UpdatedBy, CreatedAt, UpdatedAt
   - All PHI access must be logged (implemented in Application/Infrastructure layers)

3. **Data Retention**:
   - Soft deletes (IsDeleted flag) preserve data for compliance
   - Never hard delete records containing PHI

4. **Encryption**:
   - PHI fields should be encrypted at rest (SQL Server TDE)
   - Implemented at Infrastructure layer, not in domain entities

**Role-Based Access** (enforced in Application/API layers):
- **Admin**: Full access to all entities
- **Coordinator**: Create/update Caregivers, Clients, Shifts, Invoices; Read-only for Visit Notes
- **Caregiver**: Read own profile and shifts; Create/update own Visit Notes
- **Client**: Read own profile, shifts, visit notes, invoices; Update own CarePlan
- **FacilityManager**: Create shift requests; View assigned caregivers

---

## 4. Non-Functional Requirements

### 4.1 Performance
- **Entity Creation**: < 100ms to create any entity in-memory (no database calls)
- **Computed Properties**: < 1ms to calculate (e.g., Age, BillableHours, GrossMargin)
- **Validation**: < 50ms for all validation rules on an entity
- **Database Considerations** (future phase):
  - Queries for caregivers with valid certifications: < 500ms
  - Queries for client shifts in date range: < 500ms
  - Invoice generation for 100+ shifts: < 2 seconds

### 4.2 Scalability
- **Data Volume Expectations**:
  - Caregivers: 75 by Month 4, 200+ by Year 1
  - Clients: 50-100 in Year 1
  - Shifts: 100,000+ annually (75 caregivers × 30 hrs/wk × 52 weeks ≈ 117,000 shifts)
  - Visit Notes: 1:1 with shifts = 100,000+ annually
  - Invoices: 1,200+ annually (50 clients × monthly invoicing × 12 months)

- **Entity Relationships**:
  - Caregiver → Shifts: 1-to-many (1 caregiver, thousands of shifts)
  - Client → Shifts: 1-to-many (1 client, hundreds of shifts)
  - Shift → VisitNotes: 1-to-many (1 shift, potentially multiple notes)
  - Client → Invoices: 1-to-many (1 client, 12+ invoices/year)

### 4.3 Reliability
- **Data Integrity**:
  - Foreign key relationships must be maintained
  - Cascade deletes: If User deleted → Caregiver/Client should be soft-deleted
  - Prevent orphaned records: VisitNote requires valid Shift, InvoiceLineItem requires valid Invoice

- **Concurrency**:
  - Domain entities are immutable value objects (no state changes after creation)
  - EF Core will handle optimistic concurrency (future phase)

### 4.4 Usability
- **Developer Experience**:
  - Domain entities should be self-documenting with XML comments
  - Computed properties provide convenience (Age, BillableHours) without storing redundant data
  - Navigation properties enable intuitive code: `shift.Client.FullName`

- **Business Logic Clarity**:
  - Gross margin calculations are transparent (visible in entity properties)
  - Certification expiration logic is clear (IsExpired, IsExpiringSoon)
  - Employment type differentiation is explicit (W2Employee vs Contractor1099)

### 4.5 Compliance

**HIPAA Requirements**:
1. **Minimum Necessary Standard**: Only collect PHI required for business operations
   - Client medical conditions, allergies: Required for safe care delivery
   - Visit notes with vital signs: Required for care documentation

2. **Access Controls**: Role-based access enforced in Application layer

3. **Audit Trails**: BaseEntity.CreatedBy/UpdatedBy tracks data modifications

**Maryland Healthcare Regulations**:
1. **Caregiver Certification Tracking**: System enforces valid certifications (FR-007, FR-008)
2. **Background Checks**: Not captured in domain entities (handled separately in Infrastructure layer)
3. **Medicaid Billing**: Client.MedicaidNumber supports Medicaid reimbursement claims

**Labor Law Compliance**:
1. **W-2 vs 1099 Classification**: Employment type clearly differentiated (FR-005)
2. **Overtime Tracking**: Shift.OvertimePayRate supports overtime pay calculations
3. **Break Time**: Shift.BreakMinutes ensures unpaid breaks are not billed

---

## 5. Success Criteria

### 5.1 Quantitative Metrics

**Development Metrics**:
- ✅ All 12 core entities created with 100% XML documentation coverage
- ✅ All enumerations defined (7 enums)
- ✅ 100% of computed properties tested (Age, BillableHours, GrossMargin, etc.)
- ✅ Zero circular dependencies in entity relationships
- ✅ < 500 lines of code per entity (maintainability)

**Business Metrics** (measured after implementation in Application/Infrastructure layers):
- ✅ System tracks margins per shift with 100% accuracy
- ✅ 95% of caregivers have valid certifications at all times (alerts for expiring certs)
- ✅ Invoice generation time reduced from 30 min/invoice (manual) to < 2 min/invoice (automated)
- ✅ Zero HIPAA compliance violations in first 6 months

### 5.2 Qualitative Goals

**Code Quality**:
- Clean Architecture principles followed (no dependencies outside Domain layer)
- Domain entities are testable in isolation
- Entities clearly represent business concepts from CarePath_Health.pdf playbook

**Business Alignment**:
- Dual-service model (In-Home Care + Staffing) is clearly differentiated in entities
- Margin tracking (40-45% in-home, 25-30% staffing) is built into entities
- Employment types (W-2 vs 1099) are captured for accurate cost calculations

**Developer Experience**:
- Entities are intuitive to work with (clear naming, logical relationships)
- Computed properties reduce repetitive calculations in business logic
- Navigation properties enable efficient data access patterns

---

## 6. Scope & Boundaries

### 6.1 In Scope

**Phase 1: Domain Entities (This Spec)**
- ✅ Define all 12 core entities (User, Caregiver, Client, Shift, VisitNote, Invoice, etc.)
- ✅ Define all enumerations (UserRole, EmploymentType, CertificationType, etc.)
- ✅ Define repository interfaces (IRepository<T>, IUnitOfWork)
- ✅ Define computed properties (Age, BillableHours, GrossMargin, etc.)
- ✅ XML documentation for all public members
- ✅ Navigation properties for entity relationships

### 6.2 Out of Scope (Future Phases)

**Phase 2: EF Core Configurations** (Next spec)
- ❌ Entity Framework Core configurations (Fluent API)
- ❌ Database migrations
- ❌ Repository implementations
- ❌ SQL Server schema design

**Phase 3: Application Layer** (Future)
- ❌ DTOs (Data Transfer Objects)
- ❌ AutoMapper profiles
- ❌ FluentValidation validators
- ❌ Application services (CaregiverService, ShiftService, etc.)

**Phase 4: Business Logic** (Future)
- ❌ Scheduling algorithms (match caregivers to shifts)
- ❌ Margin optimization logic
- ❌ Invoice auto-generation workflows
- ❌ Certification renewal reminders

**Phase 5: API Layer** (Future)
- ❌ REST API controllers
- ❌ SignalR hubs for real-time updates
- ❌ JWT authentication implementation

**Never In Scope** (Handled Separately)
- ❌ Background check integration (third-party service)
- ❌ GPS tracking implementation (handled in .NET MAUI app)
- ❌ Payment processing (Stripe/Square integration)
- ❌ Email/SMS notifications (Twilio/SendGrid)

### 6.3 Future Considerations

**Potential Enhancements** (Not required for MVP):
1. **Rating System**: Client and family members rate caregivers after each shift
2. **Recurring Shifts**: Template shifts that repeat weekly (e.g., "Every Monday 9am-5pm")
3. **Shift Swapping**: Caregivers can swap assigned shifts with approval
4. **Multi-Language Support**: Spanish translations for client-facing features
5. **Advanced Analytics**: Predictive margins, caregiver utilization forecasting
6. **Integration with EMR Systems**: Import client medical history from electronic medical records

---

## 7. Dependencies & Assumptions

### 7.1 Technical Dependencies

**Required**:
- ✅ .NET 9 SDK
- ✅ C# 13 language features (record types, init-only properties)
- ✅ System.Linq (for collection operations in computed properties)

**Future Phases Will Require**:
- Entity Framework Core 9 (for persistence)
- SQL Server (for production database)
- xUnit + Moq (for unit testing)

### 7.2 Business Dependencies

**Required**:
- ✅ CarePath_Health.pdf playbook approved (contains business model)
- ✅ Margin targets confirmed: 40-45% in-home, 25-30% staffing
- ✅ Employment types defined: W-2 for in-home, 1099 for facility staffing

**Approvals Needed**:
- ⏳ Legal review of HIPAA compliance approach (before Production)
- ⏳ Stakeholder approval of data model (before moving to Phase 2)

### 7.3 Assumptions

**Business Assumptions**:
1. **Assume Maryland is the only state** for MVP (State property defaults to "Maryland")
2. **Assume caregivers can work both service lines** (in-home and facility staffing)
3. **Assume clients are billed monthly** (not per-shift), so invoices aggregate multiple shifts
4. **Assume GPS check-in/out is required for in-home care** but optional for facility staffing
5. **Assume overtime is paid at 1.5× base rate** (per Maryland law for W-2 employees)

**Technical Assumptions**:
1. **Assume UTC timestamps** for all DateTime properties (converted to local time in UI)
2. **Assume Guid primary keys** (not auto-incrementing integers) for globally unique IDs
3. **Assume soft deletes** (IsDeleted flag) instead of hard deletes for audit trails
4. **Assume navigation properties are eagerly loaded** in queries (EF Core .Include())

**Compliance Assumptions**:
1. **Assume HIPAA applies to all client data** (conservative approach)
2. **Assume 6-year data retention** for medical records (Maryland requirement)
3. **Assume caregivers consent to background checks** during onboarding (not modeled here)

---

## 8. Risks & Mitigation

| Risk | Likelihood | Impact | Mitigation Strategy |
|------|------------|--------|---------------------|
| **Domain model doesn't match business reality** | Medium | High | Reference CarePath_Health.pdf playbook extensively; validate with stakeholders before coding |
| **Entity relationships cause circular dependencies** | Low | Medium | Follow Clean Architecture; use navigation properties carefully; avoid bidirectional required relationships |
| **Computed properties are inefficient at scale** | Low | Medium | Use Entity Framework query projections; cache expensive calculations; profile performance |
| **HIPAA compliance violations in data model** | Low | Critical | Legal review before Production; encrypt PHI at rest; implement role-based access controls |
| **Margin calculations are incorrect** | Medium | High | Unit test all margin calculations; validate against manual spreadsheets; cross-check with accounting |
| **Caregiver scheduling conflicts** (double-booking) | Medium | High | Implement validation in Application layer; add database constraints; alert coordinators |
| **Certification expiration not tracked** | Medium | High | Implement automated alerts 30 days before expiration; prevent assignment to shifts requiring expired certs |
| **Invoice totals don't match shift data** | Low | High | Validate invoice generation logic with accounting team; implement reconciliation reports |

---

## 9. Stakeholder Sign-Off

| Stakeholder | Role | Status | Date | Comments |
|-------------|------|--------|------|----------|
| Tobi Kareem | Product Owner / Developer | **Draft** | 2026-02-16 | Initial spec created |
| [Legal Counsel] | Compliance Officer | Pending | - | HIPAA review needed before Production |
| [Business Stakeholder] | Domain Expert | Pending | - | Validate against actual Maryland healthcare operations |

---

## 10. Related Documents

- **[Architecture.md](../../Documentation/Architecture.md)** - System architecture (Clean Architecture layers)
- **[CarePath_Health.pdf](../../Documentation/CarePath_Health.pdf)** - Business model, playbook, margin targets
- **[Design Spec](../02-design/cp-01-create-domain-entities.md)** - Technical design (to be created after this spec is approved)
- **[Tasks Spec](../03-tasks/cp-01-create-domain-entities.md)** - Implementation tasks (to be created after design is approved)

---

## 11. Appendix: Business Model Reference

**From CarePath_Health.pdf:**

### In-Home Care Service Line (40-45% Margin)
- **Employment**: W-2 employees
- **Bill Rate**: $30-45/hour (client pays)
- **Pay Rate**: $16-20/hour + ~25% employer taxes (payroll taxes, workers' comp, unemployment insurance)
- **Example**: Bill $35/hr, Pay $18/hr + $4.50 taxes = $12.50 margin = **35.7% gross margin**
- **Services**: Personal care, companionship, meal prep, medication reminders, light housekeeping
- **Target Market**: Seniors aging in place, post-hospital discharge, chronic illness management

### Healthcare Staffing Service Line (25-30% Margin)
- **Employment**: 1099 contractors
- **Bill Rate**: CNA $30-40/hr, LPN $50-65/hr, RN $70-90/hr (facility pays)
- **Pay Rate**: 70-75% of bill rate (no employer taxes for 1099)
- **Example (RN)**: Bill $80/hr, Pay $60/hr (75%) = $20 margin = **25% gross margin**
- **Services**: Nursing staff placed at hospitals, nursing homes, assisted living facilities
- **Target Market**: Healthcare facilities with temporary or permanent staffing needs

### Target Metrics (Year 1)
- **Month 1-2**: Onboard 15 caregivers, 10 clients
- **Month 3-4**: Ramp to 75 caregivers, 50 clients
- **Monthly Revenue (Month 4)**: $315,000 (75 caregivers × 30 hrs/wk × $35/hr × 4 weeks)
- **Monthly Gross Profit**: $125,000 at 40% margin
- **Break-Even**: Month 4-5 with $50,000 monthly operating expenses

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-02-16 | Tobi Kareem | Initial comprehensive spec based on CarePath_Health.pdf, Architecture.md, and existing domain entities |
