# Tasks Breakdown: [Feature Name]

**Date**: YYYY-MM-DD
**Author**: [Your Name]
**Project**: CarePath Health
**Status**: Draft | In Review | Approved | In Progress | Completed
**Related Specs**:
- Requirements: [Link to 01-requirements spec]
- Design: [Link to 02-design spec]

---

## Executive Summary

> **One-sentence summary of the implementation work.**

Example: "Implement GPS-based check-in/out across all layers: Domain entities, Application services, Infrastructure GPS integration, API endpoints, and MAUI mobile UI."

---

## Task Breakdown

### Task Format

Each task should follow this structure:
- **ID**: Unique identifier (e.g., TASK-001)
- **Title**: Clear, action-oriented description
- **Layer**: Which project/layer (Domain, Application, Infrastructure, API, MauiApp, Web, Shared)
- **Dependencies**: What tasks must be completed before this one
- **Estimate**: Time estimate (hours or story points)
- **Success Criteria**: What "done" looks like
- **Files to Modify/Create**: Specific file paths

---

## Phase 1: Domain Layer

### TASK-001: Create GpsCoordinates Value Object
- **Layer**: CarePath.Domain
- **Dependencies**: None
- **Estimate**: 1 hour
- **Priority**: High
- **Success Criteria**:
  - `GpsCoordinates` record created with Latitude and Longitude properties
  - Implements `DistanceFrom()` method using Haversine formula
  - Implements `IsWithinRadius()` method for geofence validation
  - Unit tests pass (>90% coverage)
- **Files**:
  - CREATE: `src/CarePath.Domain/ValueObjects/GpsCoordinates.cs`
  - CREATE: `tests/CarePath.Domain.Tests/ValueObjects/GpsCoordinatesTests.cs`
- **Implementation Notes**:
  ```csharp
  public record GpsCoordinates(double Latitude, double Longitude)
  {
      public double DistanceFrom(GpsCoordinates other)
      {
          // Haversine formula: https://en.wikipedia.org/wiki/Haversine_formula
          var R = 6371000; // Earth radius in meters
          // ... implementation
      }
  }
  ```

---

### TASK-002: Create CheckInStatus Enum
- **Layer**: CarePath.Domain
- **Dependencies**: None
- **Estimate**: 0.5 hours
- **Priority**: High
- **Success Criteria**:
  - Enum created with values: Pending, Verified, RequiresApproval, Rejected
  - Enum documented with XML comments
- **Files**:
  - CREATE: `src/CarePath.Domain/Enums/CheckInStatus.cs`

---

### TASK-003: Update Shift Entity
- **Layer**: CarePath.Domain
- **Dependencies**: TASK-001 (GpsCoordinates)
- **Estimate**: 1 hour
- **Priority**: High
- **Success Criteria**:
  - `Shift` entity has new properties:
    - `ActualCheckInTime` (DateTime?)
    - `ActualCheckOutTime` (DateTime?)
    - `CheckInLocation` (GpsCoordinates?)
    - `CheckOutLocation` (GpsCoordinates?)
  - Properties are nullable (backward compatibility)
  - Unit tests updated
- **Files**:
  - MODIFY: `src/CarePath.Domain/Entities/Scheduling/Shift.cs`
  - MODIFY: `tests/CarePath.Domain.Tests/Entities/ShiftTests.cs`

---

### TASK-004: Create CheckInRecord Entity
- **Layer**: CarePath.Domain
- **Dependencies**: TASK-001, TASK-002
- **Estimate**: 1.5 hours
- **Priority**: Medium
- **Success Criteria**:
  - Entity created inheriting from `BaseEntity`
  - Properties: ShiftId, CaregiverId, CheckInTime, Location (GpsCoordinates), IsWithinGeofence, Status
  - Navigation properties to Shift and Caregiver
  - Unit tests pass
- **Files**:
  - CREATE: `src/CarePath.Domain/Entities/Scheduling/CheckInRecord.cs`
  - CREATE: `tests/CarePath.Domain.Tests/Entities/CheckInRecordTests.cs`

---

### TASK-005: Create IGpsValidator Interface
- **Layer**: CarePath.Domain
- **Dependencies**: TASK-001
- **Estimate**: 0.5 hours
- **Priority**: Medium
- **Success Criteria**:
  - Interface defined with methods:
    - `Task<bool> ValidateLocationAsync(GpsCoordinates actual, GpsCoordinates expected, double toleranceMeters)`
    - `Task<GpsValidationResult> ValidateCheckInAsync(Guid shiftId, GpsCoordinates location)`
  - XML documentation added
- **Files**:
  - CREATE: `src/CarePath.Domain/Interfaces/Services/IGpsValidator.cs`
  - CREATE: `src/CarePath.Domain/Models/GpsValidationResult.cs`

---

## Phase 2: Application Layer

### TASK-006: Create CheckInDto
- **Layer**: CarePath.Application
- **Dependencies**: None
- **Estimate**: 0.5 hours
- **Priority**: High
- **Success Criteria**:
  - DTO record created with properties: ShiftId, Latitude, Longitude, Timestamp
  - Uses `record` type for immutability
- **Files**:
  - CREATE: `src/CarePath.Application/DTOs/Shifts/CheckInDto.cs`

---

### TASK-007: Create CheckInResultDto
- **Layer**: CarePath.Application
- **Dependencies**: TASK-002
- **Estimate**: 0.5 hours
- **Priority**: High
- **Success Criteria**:
  - DTO record created with properties: Success, Message, Status, UpdatedShift
- **Files**:
  - CREATE: `src/CarePath.Application/DTOs/Shifts/CheckInResultDto.cs`

---

### TASK-008: Create CheckInValidator
- **Layer**: CarePath.Application
- **Dependencies**: TASK-006
- **Estimate**: 1 hour
- **Priority**: High
- **Success Criteria**:
  - FluentValidation validator created
  - Validates: ShiftId (NotEmpty), Latitude (-90 to 90), Longitude (-180 to 180), Timestamp (not future)
  - Unit tests pass (all validation rules tested)
- **Files**:
  - CREATE: `src/CarePath.Application/Validators/CheckInValidator.cs`
  - CREATE: `tests/CarePath.Application.Tests/Validators/CheckInValidatorTests.cs`

---

### TASK-009: Update IShiftService Interface
- **Layer**: CarePath.Application
- **Dependencies**: TASK-006, TASK-007
- **Estimate**: 0.5 hours
- **Priority**: High
- **Success Criteria**:
  - Interface updated with new methods:
    - `Task<CheckInResultDto> CheckInAsync(CheckInDto dto, Guid caregiverId)`
    - `Task<CheckOutResultDto> CheckOutAsync(CheckOutDto dto, Guid caregiverId)`
    - `Task<ShiftDto> GetActiveShiftAsync(Guid caregiverId)`
- **Files**:
  - MODIFY: `src/CarePath.Application/Services/Scheduling/IShiftService.cs`

---

### TASK-010: Implement ShiftService.CheckInAsync
- **Layer**: CarePath.Application
- **Dependencies**: TASK-009, TASK-005
- **Estimate**: 3 hours
- **Priority**: Critical
- **Success Criteria**:
  - Method implemented with business logic:
    1. Validate shift exists and belongs to caregiver
    2. Validate shift is in correct status (Scheduled)
    3. Call `IGpsValidator` to validate location
    4. Update Shift entity with check-in details
    5. Create CheckInRecord entity
    6. Save to repository (Unit of Work)
    7. Send SignalR notification (via INotificationService)
    8. Return CheckInResultDto
  - Unit tests pass (>80% coverage, mock all dependencies)
  - Edge cases handled: shift not found, wrong caregiver, already checked in, GPS out of range
- **Files**:
  - MODIFY: `src/CarePath.Application/Services/Scheduling/ShiftService.cs`
  - CREATE: `tests/CarePath.Application.Tests/Services/ShiftServiceCheckInTests.cs`

---

### TASK-011: Update AutoMapper Profile
- **Layer**: CarePath.Application
- **Dependencies**: TASK-006, TASK-004
- **Estimate**: 0.5 hours
- **Priority**: Medium
- **Success Criteria**:
  - Mapping added: `CheckInDto` → `CheckInRecord`
  - Mapping added: `Shift` → `ShiftDto` (updated with new properties)
  - AutoMapper configuration tests pass
- **Files**:
  - MODIFY: `src/CarePath.Application/Mappings/ShiftMappingProfile.cs`
  - MODIFY: `tests/CarePath.Application.Tests/Mappings/ShiftMappingProfileTests.cs`

---

## Phase 3: Infrastructure Layer

### TASK-012: Update CarePathDbContext
- **Layer**: CarePath.Infrastructure
- **Dependencies**: TASK-004
- **Estimate**: 0.5 hours
- **Priority**: High
- **Success Criteria**:
  - `DbSet<CheckInRecord>` added to `CarePathDbContext`
- **Files**:
  - MODIFY: `src/CarePath.Infrastructure/Persistence/CarePathDbContext.cs`

---

### TASK-013: Create CheckInRecordConfiguration
- **Layer**: CarePath.Infrastructure
- **Dependencies**: TASK-012
- **Estimate**: 1.5 hours
- **Priority**: High
- **Success Criteria**:
  - Entity configuration created using Fluent API
  - Table name: `CheckInRecords`
  - GpsCoordinates owned entity configured (columns: Latitude, Longitude with precision)
  - Foreign keys: ShiftId, CaregiverId (with cascade delete)
  - Indexes: ShiftId, CheckInTime, CaregiverId
- **Files**:
  - CREATE: `src/CarePath.Infrastructure/Persistence/Configurations/CheckInRecordConfiguration.cs`

---

### TASK-014: Update ShiftConfiguration
- **Layer**: CarePath.Infrastructure
- **Dependencies**: TASK-003
- **Estimate**: 1 hour
- **Priority**: High
- **Success Criteria**:
  - Shift entity configuration updated with new columns
  - GPS coordinates configured as owned entities (or separate columns)
- **Files**:
  - MODIFY: `src/CarePath.Infrastructure/Persistence/Configurations/ShiftConfiguration.cs`

---

### TASK-015: Create EF Core Migration
- **Layer**: CarePath.Infrastructure
- **Dependencies**: TASK-013, TASK-014
- **Estimate**: 1 hour
- **Priority**: Critical
- **Success Criteria**:
  - Migration created: `AddGpsCheckInTracking`
  - Migration script reviewed (up and down methods)
  - Migration tested on local database
  - Schema changes verified:
    - New table: `CheckInRecords`
    - Updated table: `Shifts` (new columns)
    - Indexes created
- **Files**:
  - CREATE: `src/CarePath.Infrastructure/Persistence/Migrations/YYYYMMDDHHMMSS_AddGpsCheckInTracking.cs`
- **Commands**:
  ```bash
  dotnet ef migrations add AddGpsCheckInTracking --project src/CarePath.Infrastructure --startup-project src/CarePath.Api
  dotnet ef database update --project src/CarePath.Api
  ```

---

### TASK-016: Update IShiftRepository Interface
- **Layer**: CarePath.Infrastructure
- **Dependencies**: TASK-004
- **Estimate**: 0.5 hours
- **Priority**: Medium
- **Success Criteria**:
  - Interface updated with new method:
    - `Task<Shift?> GetActiveShiftByCaregiverAsync(Guid caregiverId)`
    - `Task<List<Shift>> GetShiftsRequiringApprovalAsync()`
- **Files**:
  - MODIFY: `src/CarePath.Domain/Interfaces/Repositories/IShiftRepository.cs`

---

### TASK-017: Implement ShiftRepository Methods
- **Layer**: CarePath.Infrastructure
- **Dependencies**: TASK-016
- **Estimate**: 1.5 hours
- **Priority**: Medium
- **Success Criteria**:
  - Methods implemented using EF Core with proper includes
  - Query optimization (no N+1 queries)
  - Integration tests pass
- **Files**:
  - MODIFY: `src/CarePath.Infrastructure/Persistence/Repositories/ShiftRepository.cs`
  - CREATE: `tests/CarePath.Infrastructure.Tests/Repositories/ShiftRepositoryTests.cs`

---

### TASK-018: Implement GpsValidator Service
- **Layer**: CarePath.Infrastructure
- **Dependencies**: TASK-005, TASK-017
- **Estimate**: 2 hours
- **Priority**: High
- **Success Criteria**:
  - Service implements `IGpsValidator` interface
  - Validates GPS location against expected location (geofence)
  - Uses configurable tolerance (default 500 meters)
  - Fetches expected location from Client address in database
  - Unit tests pass (mock repository)
- **Files**:
  - CREATE: `src/CarePath.Infrastructure/Services/Geolocation/GpsValidator.cs`
  - CREATE: `tests/CarePath.Infrastructure.Tests/Services/GpsValidatorTests.cs`

---

### TASK-019: Create GpsService for .NET MAUI
- **Layer**: CarePath.Infrastructure
- **Dependencies**: TASK-001
- **Estimate**: 2 hours
- **Priority**: High
- **Success Criteria**:
  - Service uses `Microsoft.Maui.Essentials.Geolocation` API
  - Implements `GetCurrentLocationAsync()` with timeout (10 seconds)
  - Handles permission requests (LocationWhenInUse)
  - Handles errors gracefully (GPS disabled, timeout, permission denied)
  - Returns `GpsCoordinates?` (null if unavailable)
  - Unit tests pass (mock Geolocation API)
- **Files**:
  - CREATE: `src/CarePath.Infrastructure/Services/Geolocation/GpsService.cs`
  - CREATE: `src/CarePath.Domain/Interfaces/Services/IGpsService.cs`
  - CREATE: `tests/CarePath.Infrastructure.Tests/Services/GpsServiceTests.cs`

---

### TASK-020: Register Services in DI Container
- **Layer**: CarePath.Infrastructure
- **Dependencies**: TASK-018, TASK-019
- **Estimate**: 0.5 hours
- **Priority**: High
- **Success Criteria**:
  - Services registered in `DependencyInjection.cs`
  - Lifetimes: Scoped for `GpsValidator`, Transient for `GpsService`
- **Files**:
  - MODIFY: `src/CarePath.Infrastructure/DependencyInjection.cs`

---

## Phase 4: API Layer

### TASK-021: Create ShiftsController.CheckIn Endpoint
- **Layer**: CarePath.Api
- **Dependencies**: TASK-009, TASK-010
- **Estimate**: 2 hours
- **Priority**: Critical
- **Success Criteria**:
  - `[HttpPost("check-in")]` endpoint created in `ShiftsController`
  - Endpoint decorated with `[Authorize]` attribute
  - Extracts `caregiverId` from JWT claims
  - Calls `IShiftService.CheckInAsync()`
  - Returns `200 OK` with `CheckInResultDto` on success
  - Returns `400 Bad Request` on validation failure
  - Returns `404 Not Found` if shift not found
  - Returns `403 Forbidden` if shift doesn't belong to caregiver
  - Integration tests pass
- **Files**:
  - MODIFY: `src/CarePath.Api/Controllers/ShiftsController.cs`
  - CREATE: `tests/CarePath.Api.Tests/Controllers/ShiftsControllerCheckInTests.cs`

---

### TASK-022: Create ShiftsController.GetActiveShift Endpoint
- **Layer**: CarePath.Api
- **Dependencies**: TASK-009
- **Estimate**: 1 hour
- **Priority**: High
- **Success Criteria**:
  - `[HttpGet("active")]` endpoint created
  - Returns active shift for authenticated caregiver
  - Returns `404` if no active shift
  - Integration tests pass
- **Files**:
  - MODIFY: `src/CarePath.Api/Controllers/ShiftsController.cs`
  - CREATE: `tests/CarePath.Api.Tests/Controllers/ShiftsControllerGetActiveTests.cs`

---

### TASK-023: Create ShiftHub SignalR Hub
- **Layer**: CarePath.Api
- **Dependencies**: None
- **Estimate**: 2 hours
- **Priority**: Medium
- **Success Criteria**:
  - Hub created with `[Authorize]` attribute
  - Methods:
    - `NotifyShiftCheckIn(Guid shiftId, CheckInResultDto result)`
  - Groups configured: "Administrators", "Client_{clientId}"
  - Hub registered in `Program.cs`
  - SignalR endpoint mapped: `/hubs/shifts`
- **Files**:
  - CREATE: `src/CarePath.Api/Hubs/ShiftHub.cs`
  - MODIFY: `src/CarePath.Api/Program.cs`

---

### TASK-024: Update ShiftService to Call SignalR Hub
- **Layer**: CarePath.Application
- **Dependencies**: TASK-023, TASK-010
- **Estimate**: 1 hour
- **Priority**: Medium
- **Success Criteria**:
  - `ShiftService.CheckInAsync()` updated to call `IHubContext<ShiftHub>`
  - Notification sent to "Administrators" group
  - Notification sent to specific client group (if applicable)
  - Unit tests updated (mock IHubContext)
- **Files**:
  - MODIFY: `src/CarePath.Application/Services/Scheduling/ShiftService.cs`
  - MODIFY: `tests/CarePath.Application.Tests/Services/ShiftServiceCheckInTests.cs`

---

### TASK-025: Add Swagger Documentation
- **Layer**: CarePath.Api
- **Dependencies**: TASK-021, TASK-022
- **Estimate**: 0.5 hours
- **Priority**: Low
- **Success Criteria**:
  - Endpoints documented with XML comments
  - Swagger UI shows check-in endpoint with request/response examples
  - Response types documented: 200, 400, 404, 403
- **Files**:
  - MODIFY: `src/CarePath.Api/Controllers/ShiftsController.cs`

---

## Phase 5: Mobile App (.NET MAUI)

### TASK-026: Create CheckInPage UI
- **Layer**: CarePath.MauiApp
- **Dependencies**: TASK-006, TASK-007
- **Estimate**: 3 hours
- **Priority**: Critical
- **Success Criteria**:
  - Razor page created: `CheckInOut.razor`
  - UI shows:
    - Active shift details (client name, address, scheduled time)
    - "Check In" button (disabled during loading)
    - Loading indicator during check-in
    - Success/error messages using `IDialogService`
  - Responsive design for iOS and Android
- **Files**:
  - CREATE: `src/CarePath.MauiApp/Pages/Shifts/CheckInOut.razor`
  - CREATE: `src/CarePath.MauiApp/Pages/Shifts/CheckInOut.razor.cs` (code-behind)

---

### TASK-027: Create ShiftService (Mobile)
- **Layer**: CarePath.MauiApp
- **Dependencies**: TASK-021
- **Estimate**: 2 hours
- **Priority**: High
- **Success Criteria**:
  - Service wraps API calls using `HttpClient`
  - Methods:
    - `Task<ShiftDto> GetActiveShiftAsync()`
    - `Task<CheckInResultDto> CheckInAsync(CheckInDto dto)`
  - Includes JWT token in Authorization header
  - Handles errors gracefully (network errors, timeouts, 4xx/5xx responses)
  - Unit tests pass (mock HttpClient)
- **Files**:
  - CREATE: `src/CarePath.MauiApp/Services/ShiftService.cs`
  - CREATE: `tests/CarePath.MauiApp.Tests/Services/ShiftServiceTests.cs`

---

### TASK-028: Implement GPS Location Service (Mobile)
- **Layer**: CarePath.MauiApp
- **Dependencies**: TASK-019
- **Estimate**: 2 hours
- **Priority**: Critical
- **Success Criteria**:
  - Service requests LocationWhenInUse permission on app start
  - Calls `Geolocation.GetLocationAsync()` with Best accuracy
  - Timeout: 10 seconds
  - Returns `GpsCoordinates?` (null if failed)
  - Shows user-friendly error if GPS disabled or permission denied
- **Files**:
  - CREATE: `src/CarePath.MauiApp/Services/GpsService.cs`
  - MODIFY: `src/CarePath.MauiApp/MauiProgram.cs` (register service)

---

### TASK-029: Implement Check-In Logic in Page
- **Layer**: CarePath.MauiApp
- **Dependencies**: TASK-026, TASK-027, TASK-028
- **Estimate**: 2 hours
- **Priority**: Critical
- **Success Criteria**:
  - `CheckInAsync()` method implemented in code-behind:
    1. Get GPS location using `IGpsService`
    2. Show error if GPS unavailable
    3. Create `CheckInDto` with shift ID and GPS coordinates
    4. Call `IShiftService.CheckInAsync()`
    5. Show success message and navigate to shift details
    6. Show error message if check-in failed
  - Loading state managed (disable button, show spinner)
  - UI tests pass (using Appium or similar)
- **Files**:
  - MODIFY: `src/CarePath.MauiApp/Pages/Shifts/CheckInOut.razor.cs`

---

### TASK-030: Add GPS Permissions (Android)
- **Layer**: CarePath.MauiApp (Android Platform)
- **Dependencies**: TASK-028
- **Estimate**: 0.5 hours
- **Priority**: Critical
- **Success Criteria**:
  - `AndroidManifest.xml` updated with permissions:
    - `ACCESS_FINE_LOCATION`
    - `ACCESS_COARSE_LOCATION`
  - Permissions requested at runtime using .NET MAUI Permissions API
- **Files**:
  - MODIFY: `src/CarePath.MauiApp/Platforms/Android/AndroidManifest.xml`

---

### TASK-031: Add GPS Permissions (iOS)
- **Layer**: CarePath.MauiApp (iOS Platform)
- **Dependencies**: TASK-028
- **Estimate**: 0.5 hours
- **Priority**: Critical
- **Success Criteria**:
  - `Info.plist` updated with usage descriptions:
    - `NSLocationWhenInUseUsageDescription`
  - Description text: "We need your location to verify check-in/out at client addresses."
- **Files**:
  - MODIFY: `src/CarePath.MauiApp/Platforms/iOS/Info.plist`

---

### TASK-032: Add Navigation to Check-In Page
- **Layer**: CarePath.MauiApp
- **Dependencies**: TASK-026
- **Estimate**: 0.5 hours
- **Priority**: Medium
- **Success Criteria**:
  - "Check In" button added to shift details page
  - Button navigates to `/shifts/check-in` route
  - Route registered in `MauiProgram.cs`
- **Files**:
  - MODIFY: `src/CarePath.MauiApp/Pages/Shifts/ShiftDetails.razor`
  - MODIFY: `src/CarePath.MauiApp/MauiProgram.cs`

---

## Phase 6: Web Admin Dashboard (Blazor WebAssembly)

### TASK-033: Create ShiftMonitor Page
- **Layer**: CarePath.Web
- **Dependencies**: TASK-022, TASK-023
- **Estimate**: 3 hours
- **Priority**: Medium
- **Success Criteria**:
  - Page displays list of today's shifts in MudTable
  - Columns: Caregiver, Client, Scheduled Time, Actual Check-In, Status, GPS
  - Real-time updates using SignalR connection to `/hubs/shifts`
  - Updates shift list when `ShiftCheckedIn` event received
  - GPS icon button to show location on map (modal or external link)
- **Files**:
  - CREATE: `src/CarePath.Web/Pages/Scheduling/ShiftMonitor.razor`

---

### TASK-034: Create SignalR Service (Web)
- **Layer**: CarePath.Web
- **Dependencies**: TASK-023
- **Estimate**: 1.5 hours
- **Priority**: Medium
- **Success Criteria**:
  - Service establishes SignalR connection on app start
  - Connection includes JWT token for authentication
  - Reconnects automatically on disconnect
  - Implements `IAsyncDisposable` for cleanup
- **Files**:
  - CREATE: `src/CarePath.Web/Services/SignalRService.cs`

---

### TASK-035: Add Navigation to Shift Monitor
- **Layer**: CarePath.Web
- **Dependencies**: TASK-033
- **Estimate**: 0.5 hours
- **Priority**: Low
- **Success Criteria**:
  - Menu item added to NavMenu for "Shift Monitor"
  - Icon: Location pin or clock
  - Route: `/shifts/monitor`
  - Only visible to Administrators
- **Files**:
  - MODIFY: `src/CarePath.Web/Shared/NavMenu.razor`

---

## Phase 7: Testing & Quality Assurance

### TASK-036: Write Unit Tests for Domain Layer
- **Layer**: Tests
- **Dependencies**: TASK-001 through TASK-005
- **Estimate**: 3 hours
- **Priority**: High
- **Success Criteria**:
  - Test coverage >90% for domain entities and value objects
  - Tests cover:
    - GpsCoordinates distance calculations
    - Shift entity validation
    - CheckInRecord creation
  - All tests pass
- **Files**:
  - Multiple test files in `tests/CarePath.Domain.Tests/`

---

### TASK-037: Write Integration Tests for API
- **Layer**: Tests
- **Dependencies**: TASK-021, TASK-022
- **Estimate**: 4 hours
- **Priority**: High
- **Success Criteria**:
  - Integration tests using `WebApplicationFactory<Program>`
  - Tests cover:
    - Successful check-in
    - Check-in with invalid GPS
    - Check-in with wrong caregiver
    - Check-in with missing shift
  - Tests use in-memory database or test container
  - All tests pass
- **Files**:
  - Multiple test files in `tests/CarePath.Api.Tests/`

---

### TASK-038: Write E2E Tests for Mobile App
- **Layer**: Tests
- **Dependencies**: TASK-029
- **Estimate**: 4 hours
- **Priority**: Medium
- **Success Criteria**:
  - E2E tests using Appium or similar framework
  - Tests cover:
    - Full check-in flow
    - GPS permission handling
    - Error scenarios
  - Tests run on Android emulator
  - All tests pass
- **Files**:
  - Multiple test files in `tests/CarePath.MauiApp.UITests/`

---

### TASK-039: Manual QA Testing
- **Layer**: Manual Testing
- **Dependencies**: All implementation tasks
- **Estimate**: 6 hours
- **Priority**: Critical
- **Success Criteria**:
  - Test plan executed on real devices (iOS, Android)
  - Test scenarios:
    - Happy path check-in
    - GPS disabled scenarios
    - Poor network scenarios
    - Offline check-in queuing (if implemented)
    - Real-time updates on admin dashboard
  - Bugs logged and fixed
  - Test report created
- **Files**:
  - CREATE: `tests/manual-testing/gps-check-in-test-plan.md`

---

## Phase 8: Deployment

### TASK-040: Deploy Database Migration
- **Layer**: Infrastructure
- **Dependencies**: TASK-015
- **Estimate**: 1 hour
- **Priority**: Critical
- **Success Criteria**:
  - Migration script reviewed
  - Backup of production database created
  - Migration applied to production SQL Server
  - Database schema verified
  - Rollback script tested
- **Commands**:
  ```bash
  # Staging
  dotnet ef database update --project src/CarePath.Infrastructure --startup-project src/CarePath.Api --connection "StagingConnectionString"

  # Production
  dotnet ef database update --project src/CarePath.Infrastructure --startup-project src/CarePath.Api --connection "ProductionConnectionString"
  ```

---

### TASK-041: Deploy API to Azure App Service
- **Layer**: API Deployment
- **Dependencies**: TASK-040
- **Estimate**: 2 hours
- **Priority**: Critical
- **Success Criteria**:
  - API deployed to Azure App Service (Standard tier)
  - Configuration updated in Azure Portal (connection strings, app settings)
  - Health check endpoint verified: `/health`
  - Smoke tests pass (Postman collection run against production)
  - Application Insights monitoring enabled
- **Commands**:
  ```bash
  dotnet publish -c Release -o ./publish
  az webapp deployment source config-zip --resource-group CarePath-RG --name carepath-api --src ./publish.zip
  ```

---

### TASK-042: Deploy Mobile App to Test Flight (iOS)
- **Layer**: Mobile Deployment
- **Dependencies**: TASK-029, TASK-031
- **Estimate**: 3 hours
- **Priority**: High
- **Success Criteria**:
  - App version incremented to 1.1.0
  - Build created with Release configuration
  - IPA uploaded to App Store Connect
  - Test Flight build available to internal testers
  - Beta testers notified
- **Commands**:
  ```bash
  dotnet publish -f net9.0-ios -c Release
  # Upload to App Store Connect via Transporter or Xcode
  ```

---

### TASK-043: Deploy Mobile App to Google Play (Android)
- **Layer**: Mobile Deployment
- **Dependencies**: TASK-029, TASK-030
- **Estimate**: 3 hours
- **Priority**: High
- **Success Criteria**:
  - App version incremented to 1.1.0
  - APK/AAB created with Release configuration
  - Uploaded to Google Play Console (internal testing track)
  - Internal testers notified
- **Commands**:
  ```bash
  dotnet publish -f net9.0-android -c Release
  # Upload to Google Play Console
  ```

---

### TASK-044: Deploy Web Dashboard to Azure Static Web Apps
- **Layer**: Web Deployment
- **Dependencies**: TASK-033
- **Estimate**: 1 hour
- **Priority**: Medium
- **Success Criteria**:
  - Blazor WebAssembly app published
  - Deployed to Azure Static Web Apps
  - CDN configured for global distribution
  - Smoke tests pass
- **Commands**:
  ```bash
  dotnet publish -c Release -o ./publish
  # Deploy using Azure Static Web Apps CLI or GitHub Actions
  ```

---

## Phase 9: Monitoring & Rollout

### TASK-045: Configure Application Insights
- **Layer**: Monitoring
- **Dependencies**: TASK-041
- **Estimate**: 1 hour
- **Priority**: High
- **Success Criteria**:
  - Custom metrics configured:
    - Check-in success rate
    - GPS validation failure rate
    - API response times
  - Alerts configured:
    - Alert if check-in failure rate > 5%
    - Alert if API response time > 2 seconds
  - Dashboard created in Azure Portal
- **Files**:
  - MODIFY: `src/CarePath.Api/Program.cs` (add telemetry)

---

### TASK-046: Create Release Notes
- **Layer**: Documentation
- **Dependencies**: All tasks
- **Estimate**: 1 hour
- **Priority**: Medium
- **Success Criteria**:
  - Release notes document created
  - Features listed: GPS-based check-in/out
  - Known issues documented (if any)
  - User guide updated with new feature
- **Files**:
  - CREATE: `docs/release-notes/v1.1.0.md`

---

### TASK-047: Gradual Rollout Plan
- **Layer**: Deployment Strategy
- **Dependencies**: TASK-042, TASK-043
- **Estimate**: Planning only (no dev time)
- **Priority**: Critical
- **Success Criteria**:
  - Rollout plan documented:
    - Week 1: 10% of caregivers (internal testers)
    - Week 2: 25% of caregivers (early adopters)
    - Week 3: 50% of caregivers
    - Week 4: 100% rollout
  - Rollback plan documented
  - Success metrics defined (95% check-in success rate)
- **Files**:
  - CREATE: `docs/deployment/gps-check-in-rollout-plan.md`

---

## Summary

### Total Estimates
- **Phase 1 (Domain)**: 4.5 hours
- **Phase 2 (Application)**: 6.5 hours
- **Phase 3 (Infrastructure)**: 9 hours
- **Phase 4 (API)**: 6.5 hours
- **Phase 5 (Mobile)**: 11 hours
- **Phase 6 (Web)**: 5 hours
- **Phase 7 (Testing)**: 17 hours
- **Phase 8 (Deployment)**: 10 hours
- **Phase 9 (Monitoring)**: 2 hours

**Total**: ~71.5 hours (~9 days for 1 developer, ~4-5 days for 2 developers)

### Critical Path
TASK-001 → TASK-003 → TASK-004 → TASK-010 → TASK-015 → TASK-021 → TASK-029 → TASK-040 → TASK-042

### Risk Areas
- **GPS accuracy**: May need field testing and tuning of geofence tolerance
- **Battery drain**: GPS polling may drain battery; optimize polling frequency
- **Network reliability**: Mobile app needs robust offline queuing
- **Testing**: E2E testing on real devices is time-consuming but critical

---

## Progress Tracking

| Task ID | Status | Assignee | Started | Completed | Notes |
|---------|--------|----------|---------|-----------|-------|
| TASK-001 | Not Started | - | - | - | - |
| TASK-002 | Not Started | - | - | - | - |
| ... | ... | ... | ... | ... | ... |

---

## Related Documents

- [Requirements Spec](../01-requirements/[feature-name].md)
- [Design Spec](../02-design/[feature-name].md)
- [Architecture.md](../../Documentation/Architecture.md)

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | YYYY-MM-DD | [Name] | Initial task breakdown |
