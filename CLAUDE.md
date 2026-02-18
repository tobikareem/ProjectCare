# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

CarePath Health — a .NET 9 healthcare management platform for in-home care (W-2 employees) and healthcare staffing (1099 contractors). Clean Architecture with planned layers: Domain, Application, Infrastructure, WebApi, MAUI Mobile, and Blazor Web Admin.

## Build & Run Commands

```bash
# Build entire solution
dotnet build CarePath.sln

# Run the Web API (http://localhost:5240, https://localhost:7028)
dotnet run --project WebApi

# Run tests (when test projects exist)
dotnet test CarePath.sln

# Run a single test project
dotnet test Domain.Tests/Domain.Tests.csproj

# Run a single test by name
dotnet test --filter "FullyQualifiedName~TestClassName.TestMethodName"

# EF Core migrations (Infrastructure as --project, WebApi as --startup-project)
dotnet ef migrations add <Name> --project Infrastructure --startup-project WebApi
dotnet ef database update --startup-project WebApi
```

## Architecture

**Clean Architecture layers (inner → outer):**
- **Domain** — Entities, value objects, enumerations, domain events. Zero dependencies on other projects.
- **Application** — Services, DTOs, validators, interfaces (depends on Domain only).
- **Infrastructure** — EF Core DbContext, repositories, external services (depends on Application & Domain).
- **WebApi** — ASP.NET Core controllers, middleware, SignalR hubs (depends on all inner layers).

## Key Conventions

- **When adding new pages or components**, always add a navigation link in the project's site structure and header
- **Guid primary keys** (not auto-increment integers)
- **UTC timestamps** for all DateTime properties
- **Soft deletes** via `IsDeleted` flag — never hard delete
- **Audit fields** on all entities: `CreatedBy`, `UpdatedBy`, `CreatedAt`, `UpdatedAt` (inherited from `BaseEntity`)
- **Nullable reference types** enabled across all projects
- **Implicit usings** enabled

## Spec-Driven Development Workflow

All features follow a three-phase spec process in `_specs/`:
1. **Requirements** (`01-requirements/`) — Problem statement, user stories (Gherkin), acceptance criteria
2. **Design** (`02-design/`) — Architecture decisions, entity design, API endpoints, testing strategy
3. **Tasks** (`03-tasks/`) — Atomic tasks (1-4 hours), dependencies, files to create/modify

Create new specs with: `bash _specs/scripts/new-spec.sh CP-XX "Feature Name"`

Read existing specs before implementing features — they contain detailed entity definitions, relationships, and acceptance criteria.

## Domain Model Summary

Core entities: User, Caregiver, Client, CarePlan, Shift, VisitNote, VisitPhoto, Invoice, InvoiceLineItem, Payment, CaregiverCertification. Key enums: UserRole, EmploymentType, CertificationType, ServiceType, ShiftStatus, InvoiceStatus, PaymentMethod. Value objects: Address, GpsCoordinates, Money, TimeRange, PhoneNumber.

## Documentation Lookup

Use the Context7 MCP tool to check up-to-date docs when implementing new .NET libraries, frameworks, or adding features using them.

## Planned Tech Stack

EF Core 9 + SQL Server, ASP.NET Core Identity + JWT, SignalR, xUnit + Moq + FluentAssertions, FluentValidation, AutoMapper, Serilog, .NET MAUI Blazor Hybrid (mobile), Blazor WebAssembly (admin dashboard).

## Compliance

HIPAA compliance required for all PHI data. 6-year data retention for medical records (Maryland requirement). Encryption at rest. Role-based authorization.
