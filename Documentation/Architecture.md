# CarePath Health - Architecture & .NET Project Structure

## System Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                          CLIENT LAYER                                │
├─────────────────────────────────────────────────────────────────────┤
│                                                                       │
│  ┌──────────────────────┐              ┌──────────────────────┐    │
│  │  .NET MAUI Blazor    │              │  Blazor WebAssembly  │    │
│  │  Hybrid Mobile App   │              │     Web Admin        │    │
│  │  (iOS/Android)       │              │     Dashboard        │    │
│  │                      │              │                      │    │
│  │  • Caregiver Portal  │              │  • Client Mgmt       │    │
│  │  • GPS Check-in/out  │              │  • Scheduling        │    │
│  │  • Visit Notes       │              │  • Analytics         │    │
│  │  • Time Tracking     │              │  • Billing           │    │
│  │  • Push Notifications│              │  • Reports           │    │
│  └──────────────────────┘              └──────────────────────┘    │
│           │                                       │                  │
│           └───────────────────┬───────────────────┘                 │
│                               │                                      │
└───────────────────────────────┼──────────────────────────────────────┘
                                │
                                │ HTTPS/REST API + SignalR
                                │
┌───────────────────────────────┼──────────────────────────────────────┐
│                          API LAYER                                    │
├───────────────────────────────┼──────────────────────────────────────┤
│                               ▼                                       │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │         ASP.NET Core 9 Web API                              │    │
│  │                                                              │    │
│  │  ┌─────────────┐  ┌──────────────┐  ┌──────────────┐      │    │
│  │  │ Controllers │  │  Minimal APIs │  │  SignalR     │      │    │
│  │  │             │  │               │  │  Hubs        │      │    │
│  │  └─────────────┘  └──────────────┘  └──────────────┘      │    │
│  │                                                              │    │
│  │  ┌─────────────────────────────────────────────────────┐   │    │
│  │  │           Middleware Pipeline                        │   │    │
│  │  │  • Authentication (JWT)                              │   │    │
│  │  │  • Authorization                                     │   │    │
│  │  │  • Exception Handling                                │   │    │
│  │  │  • Logging (Serilog)                                 │   │    │
│  │  └─────────────────────────────────────────────────────┘   │    │
│  └─────────────────────────────────────────────────────────────┘    │
│                               │                                       │
└───────────────────────────────┼──────────────────────────────────────┘
                                │
                                │
┌───────────────────────────────┼──────────────────────────────────────┐
│                       BUSINESS LAYER                                  │
├───────────────────────────────┼──────────────────────────────────────┤
│                               ▼                                       │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │         Application Services Layer                           │    │
│  │                                                              │    │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐     │    │
│  │  │ HomeCare     │  │  Staffing    │  │ Scheduling   │     │    │
│  │  │ Services     │  │  Services    │  │ Services     │     │    │
│  │  └──────────────┘  └──────────────┘  └──────────────┘     │    │
│  │                                                              │    │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐     │    │
│  │  │ Billing      │  │  Analytics   │  │ Notification │     │    │
│  │  │ Services     │  │  Services    │  │ Services     │     │    │
│  │  └──────────────┘  └──────────────┘  └──────────────┘     │    │
│  │                                                              │    │
│  │  • Business Logic                                           │    │
│  │  • Validation (FluentValidation)                            │    │
│  │  • AutoMapper (Entity ↔DTO)                                │    │
│  │  • CQRS Patterns (optional)                                 │    │
│  └─────────────────────────────────────────────────────────────┘    │
│                               │                                       │
└───────────────────────────────┼──────────────────────────────────────┘
                                │
                                │
┌───────────────────────────────┼──────────────────────────────────────┐
│                        DOMAIN LAYER                                   │
├───────────────────────────────┼──────────────────────────────────────┤
│                               ▼                                       │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │              Domain Models & Interfaces                      │    │
│  │                                                              │    │
│  │  Entities:                          Aggregates:              │    │
│  │  • User (base)                      • Client + Care Plans    │    │
│  │  • Caregiver                        • Shift + Visit Notes    │    │
│  │  • Client                           • Invoice + Line Items   │    │
│  │  • Shift                                                     │    │
│  │  • Visit Note                       Value Objects:           │    │
│  │  • Invoice                          • Address                │    │
│  │  • Certification                    • GPS Coordinates        │    │
│  │  • CarePlan                         • Money                  │    │
│  │                                     • TimeRange              │    │
│  │  Enums:                                                      │    │
│  │  • ShiftStatus, EmploymentType, CertificationType,          │    │
│  │    ServiceLineType, PaymentStatus                           │    │
│  │                                                              │    │
│  │  Repository Interfaces (IRepository<T>)                      │    │
│  │  Domain Services Interfaces                                  │    │
│  └─────────────────────────────────────────────────────────────┘    │
└───────────────────────────────────────────────────────────────────────┘
                                │
                                │
┌───────────────────────────────┼──────────────────────────────────────┐
│                    INFRASTRUCTURE LAYER                               │
├───────────────────────────────┼──────────────────────────────────────┤
│                               ▼                                       │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │         Data Access & External Services                      │    │
│  │                                                              │    │
│  │  ┌──────────────────────────────────────────┐               │    │
│  │  │   Entity Framework Core 9                 │               │    │
│  │  │                                           │               │    │
│  │  │  • DbContext (CarePathDbContext)         │               │    │
│  │  │  • Repository Implementations             │               │    │
│  │  │  • Unit of Work Pattern                   │               │    │
│  │  │  • Migrations                             │               │    │
│  │  │  • Entity Configurations (Fluent API)     │               │    │
│  │  └──────────────────────────────────────────┘               │    │
│  │                                                              │    │
│  │  ┌──────────────────────────────────────────┐               │    │
│  │  │   External Service Integrations           │               │    │
│  │  │                                           │               │    │
│  │  │  • Email Service (SMTP/SendGrid)         │               │    │
│  │  │  • SMS Service (Twilio)                  │               │    │
│  │  │  • Push Notifications (Firebase)         │               │    │
│  │  │  • Payment Gateway (Stripe)              │               │    │
│  │  │  • File Storage (Azure Blob/AWS S3)      │               │    │
│  │  │  • Geocoding/Maps API                    │               │    │
│  │  └──────────────────────────────────────────┘               │    │
│  │                                                              │    │
│  │  ┌──────────────────────────────────────────┐               │    │
│  │  │   Identity & Security                     │               │    │
│  │  │                                           │               │    │
│  │  │  • ASP.NET Core Identity                 │               │    │
│  │  │  • JWT Token Generation                  │               │    │
│  │  │  • Role-based Authorization              │               │    │
│  │  │  • Password Hashing                      │               │    │
│  │  └──────────────────────────────────────────┘               │    │
│  └─────────────────────────────────────────────────────────────┘    │
│                               │                                       │
└───────────────────────────────┼──────────────────────────────────────┘
                                │
                                ▼
                    ┌───────────────────────┐
                    │   SQL Server Database │
                    │                       │
                    │  • Users & Identity   │
                    │  • Caregivers         │
                    │  • Clients            │
                    │  • Shifts             │
                    │  • Visit Notes        │
                    │  • Invoices           │
                    │  • Analytics          │
                    └───────────────────────┘
```

---

## How This Maps to .NET Project Structure

### 1. **Solution Organization**

```
CarePath.sln
│
├── Domain/                         # Core domain models and interfaces
├── Application/                    # Business logic and services
├── Infrastructure/                 # Data access and external integrations
├── WebApi/                         # ASP.NET Core 9 Web API
├── CarePath.MauiApp/               # .NET MAUI Blazor Hybrid (Mobile)
├── CarePath.Web/                   # Blazor WebAssembly (Admin)
├── CarePath.Contracts/             # Client-safe DTOs and response models
├── CarePath.Client/                # Typed API client and client helpers
├── CarePath.Client.UI/             # Shared Razor components and UI primitives
│
└── tests/
    ├── Domain.Tests/
    ├── Application.Tests/
    ├── Infrastructure.Tests/
    └── WebApi.Tests/
```

---

### 2. **CarePath.Domain** (Domain Layer)

**Purpose**: Core business entities, value objects, enums, and interfaces. No dependencies on other projects.

```
CarePath.Domain/
├── Entities/
│   ├── Common/
│   │   └── BaseEntity.cs              # Base class with Id, CreatedAt, etc.
│   ├── Identity/
│   │   ├── User.cs                    # Base user entity
│   │   ├── Caregiver.cs               # Inherits from User
│   │   ├── Administrator.cs           # Inherits from User
│   │   └── Client.cs                  # Client/Patient entity
│   ├── HomeCare/
│   │   ├── CarePlan.cs
│   │   ├── VisitNote.cs
│   │   └── CareActivity.cs
│   ├── Staffing/
│   │   ├── StaffingContract.cs
│   │   └── Facility.cs
│   ├── Scheduling/
│   │   ├── Shift.cs
│   │   ├── ShiftAssignment.cs
│   │   └── Availability.cs
│   └── Billing/
│       ├── Invoice.cs
│       ├── InvoiceLineItem.cs
│       ├── Payment.cs
│       └── TimeEntry.cs
│
├── ValueObjects/
│   ├── Address.cs
│   ├── GpsCoordinates.cs
│   ├── Money.cs
│   ├── TimeRange.cs
│   └── PhoneNumber.cs
│
├── Enums/
│   ├── ShiftStatus.cs
│   ├── EmploymentType.cs              # W2, 1099
│   ├── ServiceLineType.cs             # HomeCare, Staffing
│   ├── CertificationType.cs           # CNA, RN, LPN
│   ├── PaymentStatus.cs
│   └── UserRole.cs
│
├── Interfaces/
│   ├── Repositories/
│   │   ├── IRepository.cs             # Generic repository
│   │   ├── ICaregiverRepository.cs
│   │   ├── IClientRepository.cs
│   │   ├── IShiftRepository.cs
│   │   └── IInvoiceRepository.cs
│   └── Services/
│       ├── IMarginCalculator.cs
│       └── IGpsValidator.cs
│
└── Exceptions/
    ├── DomainException.cs
    ├── ValidationException.cs
    └── NotFoundException.cs
```

---

### 3. **CarePath.Application** (Business Logic Layer)

**Purpose**: Application services, DTOs, validators, AutoMapper profiles. Depends on Domain layer.

```
CarePath.Application/
├── DTOs/
│   ├── Authentication/
│   │   ├── LoginDto.cs
│   │   ├── RegisterDto.cs
│   │   └── TokenResponseDto.cs
│   ├── Caregivers/
│   │   ├── CaregiverDto.cs
│   │   ├── CreateCaregiverDto.cs
│   │   └── UpdateCaregiverDto.cs
│   ├── Clients/
│   │   ├── ClientDto.cs
│   │   └── CarePlanDto.cs
│   ├── Shifts/
│   │   ├── ShiftDto.cs
│   │   ├── CreateShiftDto.cs
│   │   ├── CheckInDto.cs
│   │   └── CheckOutDto.cs
│   └── Billing/
│       ├── InvoiceDto.cs
│       └── PaymentDto.cs
│
├── Services/
│   ├── Authentication/
│   │   ├── IAuthService.cs
│   │   └── AuthService.cs
│   ├── HomeCare/
│   │   ├── ICaregiverService.cs
│   │   ├── CaregiverService.cs
│   │   ├── IClientService.cs
│   │   └── ClientService.cs
│   ├── Staffing/
│   │   ├── IStaffingService.cs
│   │   └── StaffingService.cs
│   ├── Scheduling/
│   │   ├── IShiftService.cs
│   │   ├── ShiftService.cs
│   │   ├── ISchedulingOptimizer.cs
│   │   └── SchedulingOptimizer.cs
│   ├── Billing/
│   │   ├── IInvoiceService.cs
│   │   ├── InvoiceService.cs
│   │   ├── IMarginAnalyzer.cs
│   │   └── MarginAnalyzer.cs
│   └── Analytics/
│       ├── IAnalyticsService.cs
│       └── AnalyticsService.cs
│
├── Validators/
│   ├── CaregiverValidator.cs          # FluentValidation
│   ├── ClientValidator.cs
│   └── ShiftValidator.cs
│
├── Mappings/
│   └── AutoMapperProfile.cs           # Entity ↔ DTO mappings
│
└── Common/
    ├── Interfaces/
    │   ├── ICurrentUserService.cs
    │   └── IDateTime.cs
    └── Behaviors/
        └── ValidationBehavior.cs       # MediatR pipeline behavior
```

---

### 4. **CarePath.Infrastructure** (Infrastructure Layer)

**Purpose**: EF Core, external service implementations, identity. Depends on Application and Domain.

```
CarePath.Infrastructure/
├── Persistence/
│   ├── CarePathDbContext.cs
│   ├── Configurations/                # Entity configurations
│   │   ├── CaregiverConfiguration.cs
│   │   ├── ClientConfiguration.cs
│   │   ├── ShiftConfiguration.cs
│   │   └── InvoiceConfiguration.cs
│   ├── Migrations/                    # EF Core migrations
│   ├── Repositories/
│   │   ├── Repository.cs              # Generic implementation
│   │   ├── CaregiverRepository.cs
│   │   ├── ClientRepository.cs
│   │   ├── ShiftRepository.cs
│   │   └── InvoiceRepository.cs
│   └── Seeds/
│       └── DataSeeder.cs              # Seed data
│
├── Identity/
│   ├── ApplicationUser.cs             # Extends IdentityUser
│   ├── JwtTokenGenerator.cs
│   └── CurrentUserService.cs
│
├── Services/
│   ├── Email/
│   │   ├── IEmailService.cs
│   │   └── EmailService.cs            # SendGrid/SMTP
│   ├── Sms/
│   │   ├── ISmsService.cs
│   │   └── SmsService.cs              # Twilio
│   ├── Notifications/
│   │   ├── IPushNotificationService.cs
│   │   └── FirebasePushService.cs
│   ├── Storage/
│   │   ├── IFileStorageService.cs
│   │   └── AzureBlobStorageService.cs
│   └── Geolocation/
│       ├── IGpsService.cs
│       └── GpsService.cs
│
└── DependencyInjection.cs             # Service registration
```

---

### 5. **WebApi** (API Layer)

**Purpose**: REST API endpoints, SignalR hubs, middleware. Entry point for backend.

```
WebApi/
├── Controllers/
│   ├── AuthController.cs
│   ├── CaregiversController.cs
│   ├── ClientsController.cs
│   ├── ShiftsController.cs
│   ├── InvoicesController.cs
│   └── AnalyticsController.cs
│
├── Endpoints/                         # Minimal APIs (alternative)
│   ├── AuthEndpoints.cs
│   ├── CaregiverEndpoints.cs
│   └── ShiftEndpoints.cs
│
├── Hubs/
│   ├── ShiftHub.cs                    # Real-time shift updates
│   └── NotificationHub.cs             # Push notifications
│
├── Middleware/
│   ├── ExceptionHandlingMiddleware.cs
│   ├── JwtMiddleware.cs
│   └── RequestLoggingMiddleware.cs
│
├── Filters/
│   └── ValidateModelAttribute.cs
│
├── appsettings.json
├── appsettings.Development.json
├── Program.cs                         # Application startup
└── Dockerfile                         # Container deployment
```

---

### 6. **CarePath.MauiApp** (.NET MAUI Blazor Hybrid)

**Purpose**: Mobile app for caregivers (iOS/Android). Shares Blazor components.

```
CarePath.MauiApp/
├── Platforms/
│   ├── Android/
│   ├── iOS/
│   ├── Windows/
│   └── MacCatalyst/
│
├── Resources/
│   ├── Images/
│   ├── Fonts/
│   └── Splash/
│
├── Pages/                             # Razor components
│   ├── Authentication/
│   │   ├── Login.razor
│   │   └── Register.razor
│   ├── Dashboard/
│   │   └── Dashboard.razor
│   ├── Shifts/
│   │   ├── ShiftList.razor
│   │   ├── ShiftDetails.razor
│   │   └── CheckInOut.razor
│   └── VisitNotes/
│       └── CreateVisitNote.razor
│
├── Services/
│   ├── ApiClient.cs                   # HTTP client wrapper
│   ├── AuthService.cs
│   ├── ShiftService.cs
│   ├── GpsService.cs                  # Platform-specific GPS
│   ├── SecureStorageService.cs        # Token storage
│   └── OfflineDataService.cs          # SQLite for offline
│
├── ViewModels/
│   ├── LoginViewModel.cs
│   ├── DashboardViewModel.cs
│   └── ShiftViewModel.cs
│
├── MauiProgram.cs                     # App configuration
└── wwwroot/
    ├── css/
    └── js/
```

---

### 7. **CarePath.Web** (Blazor WebAssembly)

**Purpose**: Admin web dashboard. Shares components with MAUI app.

```
CarePath.Web/
├── Pages/
│   ├── Index.razor
│   ├── Authentication/
│   │   └── Login.razor
│   ├── Caregivers/
│   │   ├── CaregiverList.razor
│   │   ├── CaregiverDetails.razor
│   │   └── CreateCaregiver.razor
│   ├── Clients/
│   │   ├── ClientList.razor
│   │   └── ClientDetails.razor
│   ├── Scheduling/
│   │   ├── Calendar.razor             # Drag-drop scheduling
│   │   └── ShiftManagement.razor
│   ├── Billing/
│   │   ├── InvoiceList.razor
│   │   └── CreateInvoice.razor
│   └── Analytics/
│       ├── Dashboard.razor
│       └── Reports.razor
│
├── Shared/
│   ├── MainLayout.razor
│   ├── NavMenu.razor
│   └── Components/
│       ├── ShiftCard.razor            # Reusable component
│       └── MarginChart.razor
│
├── Services/
│   ├── ApiClient.cs
│   ├── AuthService.cs
│   ├── CaregiverService.cs
│   └── SignalRService.cs              # Real-time updates
│
├── wwwroot/
│   ├── index.html
│   ├── css/
│   └── js/
│
└── Program.cs
```

---

### 8. **CarePath.Contracts / CarePath.Client / CarePath.Client.UI** (Shared Client Architecture)

**Purpose**: Client-safe code shared by Blazor WebAssembly and MAUI Blazor Hybrid without binding UI directly to Domain entities.

```
CarePath.Contracts/
├── DTOs/                              # Client-safe request/response models
├── Pagination/                        # PagedResult, query parameters
├── Results/                           # Error/result envelopes
└── Validation/                        # Shared validation metadata

CarePath.Client/
├── Api/                               # Typed API clients
├── Auth/                              # Token abstractions and auth helpers
├── Errors/                            # API error mapping
└── DependencyInjection.cs

CarePath.Client.UI/
├── Components/                        # Shared Razor UI primitives
├── Forms/                             # Reusable form components
├── Tables/                            # Grid/list components
└── StatusBadges/                      # Enum/status display components
```

Do not put Domain entities, EF models, WebApi controllers, platform services, provider SDK clients, secrets, or full application pages in shared client projects.

---

## Key Architectural Patterns

### 1. **Clean Architecture / Onion Architecture**
- **Domain** at the center (no dependencies)
- **Application** depends only on Domain
- **Infrastructure** depends on Application & Domain
- **API/UI** depends on all inner layers

### 2. **Repository Pattern**
```csharp
// Domain
public interface IRepository<T> where T : BaseEntity
{
    Task<T> GetByIdAsync(Guid id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<T> AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(Guid id);
}

// Infrastructure
public class Repository<T> : IRepository<T> where T : BaseEntity
{
    private readonly CarePathDbContext _context;
    // Implementation...
}
```

### 3. **Unit of Work Pattern**
```csharp
public interface IUnitOfWork : IDisposable
{
    ICaregiverRepository Caregivers { get; }
    IClientRepository Clients { get; }
    IShiftRepository Shifts { get; }
    Task<int> SaveChangesAsync();
}
```

### 4. **CQRS (Optional, for complex operations)**
```csharp
// Command
public class CreateShiftCommand : IRequest<ShiftDto>
{
    public Guid CaregiverId { get; set; }
    public DateTime StartTime { get; set; }
    // ...
}

// Handler
public class CreateShiftHandler : IRequestHandler<CreateShiftCommand, ShiftDto>
{
    // Implementation...
}
```

---

## Technology Stack Mapping

| Layer | Technologies |
|-------|-------------|
| **Presentation** | .NET MAUI 9, Blazor WebAssembly, MudBlazor/FluentUI |
| **API** | ASP.NET Core 9, SignalR, JWT Authentication |
| **Business Logic** | C# 13, FluentValidation, AutoMapper, MediatR |
| **Data Access** | Entity Framework Core 9, SQL Server |
| **Identity** | ASP.NET Core Identity, JWT |
| **Testing** | xUnit, Moq, FluentAssertions |
| **Logging** | Serilog |
| **Offline Storage** | SQLite (MAUI app) |

---

## Business Model Integration

### 1. **In-Home Care Services** (40-45% margin)
- **Domain Entities**: Client, Caregiver, CarePlan, VisitNote
- **Service Layer**: HomeCareService (calculates margins based on W-2 rates)
- **Bill Rate**: Configurable per client (\$30-45/hour)
- **Pay Rate**: W-2 employee (\$16-20/hour + taxes)

### 2. **Healthcare Staffing** (25-30% margin)
- **Domain Entities**: StaffingContract, Facility, Shift
- **Service Layer**: StaffingService (handles 1099 vs W-2 classification)
- **Bill Rate**: Per role (RN \$70-90/hour, LPN \$50-65, CNA \$30-40)
- **Pay Rate**: 1099 contractors (higher pay, lower employer costs)

### 3. **Margin Tracking**
```csharp
public class MarginAnalyzer : IMarginAnalyzer
{
    public decimal CalculateGrossMargin(Invoice invoice)
    {
        var revenue = invoice.TotalAmount;
        var laborCost = invoice.LineItems.Sum(x => x.LaborCost);
        return (revenue - laborCost) / revenue;
    }
    
    public MarginReport GenerateServiceLineReport(
        ServiceLineType serviceType, 
        DateRange period)
    {
        // Track 40-45% for HomeCare, 25-30% for Staffing
    }
}
```

---

## Development Workflow

### 1. **Database First Development**
```bash
# Create migration
dotnet ef migrations add InitialCreate --project Infrastructure --startup-project WebApi

# Update database
dotnet ef database update --startup-project WebApi
```

### 2. **API Development**
```bash
# Run API
cd WebApi
dotnet run

# API available at: https://localhost:7001
```

### 3. **Mobile App Development**
```bash
# Run on Android emulator
cd CarePath.MauiApp
dotnet build -t:Run -f net9.0-android

# Run on iOS simulator
dotnet build -t:Run -f net9.0-ios
```

### 4. **Web App Development**
```bash
# Run Blazor WASM
cd src/CarePath.Web
dotnet run

# Web available at: https://localhost:7002
```

---

## Deployment Considerations

### 1. **Backend (API)**
- **Azure App Service** or **AWS Elastic Beanstalk**
- **SQL Server** on Azure SQL Database or AWS RDS
- **Redis** for caching (optional)
- **Application Insights** for monitoring

### 2. **Mobile App**
- **iOS**: App Store (requires Apple Developer account)
- **Android**: Google Play Store
- **CI/CD**: GitHub Actions or Azure DevOps

### 3. **Web App**
- **Azure Static Web Apps** or **Netlify**
- CDN for global distribution

---

## Next Steps for Implementation

1. **Set up solution structure** (all projects)
2. **Design database schema** (create EF Core entities)
3. **Implement authentication** (JWT + Identity)
4. **Build core API endpoints** (CRUD operations)
5. **Create mobile app shell** (navigation, basic UI)
6. **Implement scheduling logic** (optimize caregiver assignments)
7. **Add GPS check-in/out** (geolocation validation)
8. **Build analytics dashboard** (margin tracking, KPIs)
9. **Add real-time features** (SignalR for notifications)
10. **Testing & refinement**

---

## Contact & Support

For questions about this architecture:
- **Business Model**: See `CarePath_Health_Playbook.docx`
- **Technical Questions**: Review this document
- **MVP Timeline**: 6 months to first release

---

**Document Version**: 1.0  
**Last Updated**: February 2026  
**Framework**: .NET 9  
**Target Platform**: Maryland Healthcare Market
