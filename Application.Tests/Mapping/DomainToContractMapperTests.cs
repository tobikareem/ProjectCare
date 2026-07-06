using System.Reflection;
using CarePath.Application.Common.Mapping;
using CarePath.Contracts.Billing;
using CarePath.Contracts.Clients;
using CarePath.Contracts.Identity;
using CarePath.Contracts.Scheduling;
using CarePath.Domain.Entities.Billing;
using CarePath.Domain.Entities.Clinical;
using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Entities.Scheduling;
using CarePath.Domain.Enumerations;
using FluentAssertions;
using ContractCertificationType = CarePath.Contracts.Enumerations.CertificationType;
using ContractEmploymentType = CarePath.Contracts.Enumerations.EmploymentType;
using ContractInvoiceStatus = CarePath.Contracts.Enumerations.InvoiceStatus;
using ContractPaymentMethod = CarePath.Contracts.Enumerations.PaymentMethod;
using ContractPaymentStatus = CarePath.Contracts.Enumerations.PaymentStatus;
using ContractServiceType = CarePath.Contracts.Enumerations.ServiceType;
using ContractShiftStatus = CarePath.Contracts.Enumerations.ShiftStatus;
using ContractUserRole = CarePath.Contracts.Enumerations.UserRole;

namespace CarePath.Application.Tests.Mapping;

public sealed class DomainToContractMapperTests
{
    [Fact]
    public void ToSummaryDto_WhenMappingUser_FlattensFullNameAndCastsRoleByNumericValue()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            FirstName = "Test",
            LastName = "Coordinator A",
            Email = "coordinator-a@carepath.local",
            PhoneNumber = "555-0100",
            Role = UserRole.Clinician,
            IsActive = true,
        };

        // Act
        var dto = user.ToSummaryDto();

        // Assert
        dto.Id.Should().Be(user.Id);
        dto.FullName.Should().Be("Test Coordinator A");
        dto.Role.Should().Be(ContractUserRole.Clinician);
        ((int)dto.Role).Should().Be((int)user.Role);
    }

    [Fact]
    public void ToSummaryDto_WhenMappingClientSummary_UsesAgeAndExcludesDateOfBirth()
    {
        // Arrange
        var client = CreateClient(DateTime.UtcNow.AddYears(-72).AddDays(-1));

        // Act
        var dto = client.ToSummaryDto();

        // Assert
        dto.Id.Should().Be(client.Id);
        dto.UserId.Should().Be(client.UserId);
        dto.FullName.Should().Be("Test Client A");
        dto.Age.Should().Be(client.Age);
        dto.ServiceType.Should().Be(ContractServiceType.InHomeCare);
        typeof(ClientSummaryDto).GetProperty("DateOfBirth").Should().BeNull();
    }

    [Fact]
    public void ToDetailDto_WhenMappingClientDetail_ExcludesInsuranceAndRawGpsFields()
    {
        // Arrange
        var client = CreateClient(new DateTime(1955, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        client.InsuranceProvider = "Synthetic Insurance";
        client.InsurancePolicyNumber = "SYNTH-POLICY";
        client.MedicaidNumber = "SYNTH-MEDICAID";
        client.Latitude = 39.0;
        client.Longitude = -76.0;

        // Act
        var dto = client.ToDetailDto();

        // Assert
        dto.FullName.Should().Be("Test Client A");
        dto.DateOfBirth.Should().Be(client.DateOfBirth);
        dto.Age.Should().Be(client.Age);
        typeof(ClientDetailDto).GetProperty(nameof(client.InsuranceProvider)).Should().BeNull();
        typeof(ClientDetailDto).GetProperty(nameof(client.InsurancePolicyNumber)).Should().BeNull();
        typeof(ClientDetailDto).GetProperty(nameof(client.MedicaidNumber)).Should().BeNull();
        typeof(ClientDetailDto).GetProperty(nameof(client.Latitude)).Should().BeNull();
        typeof(ClientDetailDto).GetProperty(nameof(client.Longitude)).Should().BeNull();
    }

    [Fact]
    public void ToDetailDto_WhenMappingCaregiver_FlattensCertificationsAndComputedExpiryFlags()
    {
        // Arrange
        var caregiver = CreateCaregiver();
        caregiver.RecordCompletedShift();
        caregiver.RecordNoShow();
        var certification = new CaregiverCertification
        {
            Id = Guid.NewGuid(),
            CaregiverId = caregiver.Id,
            Type = CertificationType.CNA,
            CertificationNumber = "SYNTH-CERT",
            IssueDate = DateTime.UtcNow.AddYears(-1),
            ExpirationDate = DateTime.UtcNow.AddDays(10),
            IssuingAuthority = "Synthetic Training Authority",
        };
        caregiver.Certifications.Add(certification);

        // Act
        var dto = caregiver.ToDetailDto();

        // Assert
        dto.FullName.Should().Be("Test Caregiver A");
        dto.EmploymentType.Should().Be(ContractEmploymentType.W2Employee);
        dto.TotalShiftsCompleted.Should().Be(1);
        dto.NoShowCount.Should().Be(1);
        dto.Certifications.Should().ContainSingle();
        dto.Certifications[0].Type.Should().Be(ContractCertificationType.CNA);
        dto.Certifications[0].IsExpired.Should().BeFalse();
        dto.Certifications[0].IsExpiringSoon.Should().BeTrue();
    }

    [Fact]
    public void ToDetailDto_WhenMappingShiftWithNullActuals_FlattensZeroBillableHoursAndNoRates()
    {
        // Arrange
        var shift = CreateShift();
        shift.ActualStartTime = null;
        shift.ActualEndTime = null;
        shift.BillRate = 80m;
        shift.PayRate = 30m;

        // Act
        var dto = shift.ToDetailDto();

        // Assert
        dto.ClientFullName.Should().Be("Test Client A");
        dto.CaregiverFullName.Should().Be("Test Caregiver A");
        dto.BillableHours.Should().Be(0m);
        dto.Status.Should().Be(ContractShiftStatus.Scheduled);
        typeof(ShiftDetailDto).GetProperty("BillRate").Should().BeNull();
        typeof(ShiftDetailDto).GetProperty("PayRate").Should().BeNull();
    }

    [Fact]
    public void ToDetailDto_WhenMappingCompletedShift_FlattensBillableHoursWithoutRatesOrGps()
    {
        // Arrange
        var shift = CreateShift();
        shift.ActualStartTime = new DateTime(2026, 7, 1, 8, 0, 0, DateTimeKind.Utc);
        shift.ActualEndTime = new DateTime(2026, 7, 1, 12, 30, 0, DateTimeKind.Utc);
        shift.BreakMinutes = 30;
        shift.CheckInLatitude = 39.0;
        shift.CheckInLongitude = -76.0;
        shift.CheckOutLatitude = 39.1;
        shift.CheckOutLongitude = -76.1;

        // Act
        var dto = shift.ToDetailDto();

        // Assert
        dto.BillableHours.Should().Be(4m);
        dto.CheckInTime.Should().Be(shift.CheckInTime);
        dto.CheckOutTime.Should().Be(shift.CheckOutTime);
        typeof(ShiftDetailDto).GetProperty("CheckInLatitude").Should().BeNull();
        typeof(ShiftDetailDto).GetProperty("CheckInLongitude").Should().BeNull();
        typeof(ShiftDetailDto).GetProperty("CheckOutLatitude").Should().BeNull();
        typeof(ShiftDetailDto).GetProperty("CheckOutLongitude").Should().BeNull();
    }

    [Fact]
    public void ToDetailDto_WhenMappingVisitNote_MapsStructuredAndClinicalFields()
    {
        // Arrange
        var visitNote = new VisitNote
        {
            Id = Guid.NewGuid(),
            ShiftId = Guid.NewGuid(),
            CaregiverId = Guid.NewGuid(),
            VisitDateTime = new DateTime(2026, 7, 1, 9, 0, 0, DateTimeKind.Utc),
            PersonalCare = true,
            Medication = true,
            Activities = "Synthetic activity note.",
            ClientCondition = "Synthetic condition note.",
            Concerns = "Synthetic concern note.",
            Medications = "Synthetic medication note.",
            BloodPressureSystolic = 120,
            BloodPressureDiastolic = 80,
            Temperature = 98.6m,
            HeartRate = 72,
            TransitionPlanId = Guid.NewGuid(),
            CaregiverSignatureUrl = "https://storage.local/synthetic-caregiver-signature",
            ClientOrFamilySignatureUrl = "https://storage.local/synthetic-client-signature",
        };
        visitNote.Photos.Add(new VisitPhoto
        {
            Id = Guid.NewGuid(),
            VisitNoteId = visitNote.Id,
            TakenAt = visitNote.VisitDateTime,
            Caption = "Synthetic photo caption.",
            PhotoUrl = "opaque-photo-object-id"
        });

        // Act
        var dto = visitNote.ToDetailDto();

        // Assert
        dto.Id.Should().Be(visitNote.Id);
        dto.PersonalCare.Should().BeTrue();
        dto.Medication.Should().BeTrue();
        dto.Activities.Should().Be("Synthetic activity note.");
        dto.TransitionPlanId.Should().Be(visitNote.TransitionPlanId);
        dto.Photos.Should().ContainSingle();
        dto.Photos[0].Caption.Should().BeNull();
        dto.Photos[0].Url.Should().BeNull();
    }

    [Fact]
    public void ToDetailDto_WhenMappingInvoice_FlattensComputedTotalsAndNestedCollections()
    {
        // Arrange
        var invoice = CreateInvoice();

        // Act
        var dto = invoice.ToDetailDto();

        // Assert
        dto.ClientFullName.Should().Be("Test Client A");
        dto.Status.Should().Be(ContractInvoiceStatus.Sent);
        dto.Subtotal.Should().Be(300m);
        dto.TaxAmount.Should().Be(15m);
        dto.Total.Should().Be(315m);
        dto.AmountPaid.Should().Be(100m);
        dto.Balance.Should().Be(215m);
        dto.LineItems.Should().ContainSingle(line => line.Total == 300m);
        dto.Payments.Should().ContainSingle(payment =>
            payment.Method == ContractPaymentMethod.CreditCard &&
            payment.Status == ContractPaymentStatus.Settled);
    }

    [Fact]
    public void ToDto_WhenMappingInvoiceLineWithZeroHours_FlattensZeroTotal()
    {
        // Arrange
        var lineItem = new InvoiceLineItem
        {
            Id = Guid.NewGuid(),
            Description = "Synthetic zero-hour line.",
            ServiceDate = new DateTime(2026, 7, 2, 0, 0, 0, DateTimeKind.Utc),
            BillableHours = 0m,
            RatePerHour = 75m,
            CostPerHour = 35m,
        };

        // Act
        var dto = lineItem.ToDto();

        // Assert
        dto.Total.Should().Be(0m);
        typeof(InvoiceLineItemDto).GetProperty("CostPerHour").Should().BeNull();
        typeof(InvoiceLineItemDto).GetProperty("GrossMarginPercentage").Should().BeNull();
    }

    [Fact]
    public void ContractDtos_WhenInspected_DoNotExposeForbiddenPhiOrFinancialFields()
    {
        // Arrange
        var forbiddenExactNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "InsuranceProvider",
            "InsurancePolicyNumber",
            "MedicaidNumber",
            "Latitude",
            "Longitude",
            "CheckInLatitude",
            "CheckInLongitude",
            "CheckOutLatitude",
            "CheckOutLongitude",
            "BillRate",
            "PayRate",
            "HourlyBillRate",
            "HourlyPayRate",
            "RatePerHour",
        };
        var dtoTypes = GetContractDtoTypes();
        var approvedMarginRateMembers = new HashSet<string>(StringComparer.Ordinal)
        {
            $"{typeof(ShiftMarginDto).FullName}.BillRate",
            $"{typeof(ShiftMarginDto).FullName}.PayRate",
        };

        // Act
        var forbiddenMembers = dtoTypes
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => forbiddenExactNames.Contains(property.Name))
                .Select(property => $"{type.FullName}.{property.Name}"))
            .Where(member => !approvedMarginRateMembers.Contains(member))
            .ToArray();

        // Assert
        typeof(ClientSummaryDto).GetProperty("DateOfBirth").Should().BeNull();
        forbiddenMembers.Should().BeEmpty();
    }

    [Fact]
    public void SummaryDtos_WhenInspected_DoNotExposePhiHeavyFields()
    {
        // Arrange
        var forbiddenSummaryNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "DateOfBirth",
            "MedicalConditions",
            "Allergies",
            "SpecialInstructions",
            "Description",
            "Goals",
            "Interventions",
            "Notes",
            "Activities",
            "ClientCondition",
            "Concerns",
            "Medications",
            "LocationNotes",
            "CaregiverSignatureUrl",
            "ClientOrFamilySignatureUrl",
            "InsuranceProvider",
            "InsurancePolicyNumber",
            "MedicaidNumber",
            "Latitude",
            "Longitude",
            "BillRate",
            "PayRate",
            "HourlyBillRate",
            "HourlyPayRate",
            "RatePerHour",
        };
        var summaryDtoTypes = GetContractDtoTypes()
            .Where(type => type.Name.EndsWith("SummaryDto", StringComparison.Ordinal))
            .ToArray();

        // Act
        var forbiddenMembers = summaryDtoTypes
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => forbiddenSummaryNames.Contains(property.Name))
                .Select(property => $"{type.FullName}.{property.Name}"))
            .ToArray();

        // Assert
        forbiddenMembers.Should().BeEmpty();
    }

    [Fact]
    public void ContractsAssembly_WhenInspected_DoesNotReferenceOrExposeDomainTypes()
    {
        // Arrange
        var contractsAssembly = typeof(UserSummaryDto).Assembly;

        // Act
        var referencesDomainAssembly = contractsAssembly.GetReferencedAssemblies()
            .Any(assembly => assembly.Name == "CarePath.Domain");
        var publicDomainTypes = contractsAssembly.GetExportedTypes()
            .SelectMany(type => type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .SelectMany(GetMemberSignatureTypes))
            .Where(type => type.FullName?.StartsWith("CarePath.Domain.", StringComparison.Ordinal) == true)
            .Select(type => type.FullName)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        // Assert
        referencesDomainAssembly.Should().BeFalse();
        publicDomainTypes.Should().BeEmpty();
    }
    [Fact]
    public void ApplicationMappers_WhenPublicSignaturesAreInspected_DoNotExposeDomainTypes()
    {
        // Arrange
        var mapperTypes = typeof(IdentityContractMapper).Assembly.GetTypes()
            .Where(type => type.Namespace == "CarePath.Application.Common.Mapping")
            .ToArray();

        // Act
        var publicDomainReferences = mapperTypes
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .SelectMany(GetSignatureTypes)
            .Where(type => type.FullName?.StartsWith("CarePath.Domain.", StringComparison.Ordinal) == true)
            .Select(type => type.FullName)
            .ToArray();

        // Assert
        publicDomainReferences.Should().BeEmpty();
    }


    private static IEnumerable<Type> GetMemberSignatureTypes(MemberInfo member)
    {
        return member switch
        {
            PropertyInfo property => [property.PropertyType],
            FieldInfo field => [field.FieldType],
            MethodInfo method => GetSignatureTypes(method),
            _ => [],
        };
    }

    private static IEnumerable<Type> GetSignatureTypes(MethodInfo method)
    {
        yield return method.ReturnType;

        foreach (var parameter in method.GetParameters())
        {
            yield return parameter.ParameterType;
        }
    }

    private static IReadOnlyList<Type> GetContractDtoTypes()
    {
        var namespaces = new HashSet<string>(StringComparer.Ordinal)
        {
            "CarePath.Contracts.Identity",
            "CarePath.Contracts.Clients",
            "CarePath.Contracts.Scheduling",
            "CarePath.Contracts.Billing",
        };

        return typeof(UserSummaryDto).Assembly.GetTypes()
            .Where(type =>
                type.IsClass &&
                type.Name.EndsWith("Dto", StringComparison.Ordinal) &&
                type.Namespace is not null &&
                namespaces.Contains(type.Namespace))
            .ToArray();
    }

    private static Client CreateClient(DateTime dateOfBirth)
    {
        var userId = Guid.NewGuid();

        return new Client
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            User = new User
            {
                Id = userId,
                FirstName = "Test",
                LastName = "Client A",
                Email = "client-a@carepath.local",
                PhoneNumber = "555-0101",
                Role = UserRole.Client,
                IsActive = true,
            },
            DateOfBirth = dateOfBirth,
            EmergencyContactName = "Test Contact A",
            EmergencyContactPhone = "555-0102",
            EmergencyContactRelationship = "Test Contact",
            RequiresDementiaCare = true,
            RequiresMobilityAssistance = true,
            RequiresMedicationManagement = true,
            RequiresCompanionship = true,
            SpecialInstructions = "Synthetic instruction.",
            MedicalConditions = "Synthetic condition.",
            Allergies = "Synthetic allergy.",
            ServiceType = ServiceType.InHomeCare,
            HourlyBillRate = 75m,
            EstimatedWeeklyHours = 20,
        };
    }

    private static Caregiver CreateCaregiver()
    {
        var userId = Guid.NewGuid();

        return new Caregiver
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            User = new User
            {
                Id = userId,
                FirstName = "Test",
                LastName = "Caregiver A",
                Email = "caregiver-a@carepath.local",
                PhoneNumber = "555-0103",
                Role = UserRole.Caregiver,
                IsActive = true,
            },
            EmploymentType = EmploymentType.W2Employee,
            HourlyPayRate = 30m,
            HireDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            HasDementiaCare = true,
            HasAlzheimersCare = true,
            HasMobilityAssistance = true,
            HasMedicationManagement = true,
            AvailableWeekdays = true,
            AvailableWeekends = true,
            AvailableNights = false,
            MaxWeeklyHours = 32,
            AverageRating = 4.7m,
        };
    }

    private static Shift CreateShift()
    {
        var client = CreateClient(new DateTime(1955, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var caregiver = CreateCaregiver();

        return new Shift
        {
            Id = Guid.NewGuid(),
            ClientId = client.Id,
            Client = client,
            CaregiverId = caregiver.Id,
            Caregiver = caregiver,
            ScheduledStartTime = new DateTime(2026, 7, 1, 8, 0, 0, DateTimeKind.Utc),
            ScheduledEndTime = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc),
            CheckInTime = new DateTime(2026, 7, 1, 8, 0, 0, DateTimeKind.Utc),
            CheckOutTime = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc),
            Status = ShiftStatus.Scheduled,
            ServiceType = ServiceType.InHomeCare,
            Notes = "Synthetic shift note.",
        };
    }

    private static Invoice CreateInvoice()
    {
        var client = CreateClient(new DateTime(1955, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = "INV-SYNTH-0001",
            ClientId = client.Id,
            Client = client,
            InvoiceDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            DueDate = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc),
            Status = InvoiceStatus.Sent,
            TaxAmount = 15m,
            Notes = "Synthetic billing note.",
        };

        invoice.LineItems.Add(new InvoiceLineItem
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoice.Id,
            Description = "Synthetic care service.",
            ServiceDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            BillableHours = 4m,
            RatePerHour = 75m,
            CostPerHour = 30m,
        });
        invoice.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoice.Id,
            PaymentDate = new DateTime(2026, 7, 5, 0, 0, 0, DateTimeKind.Utc),
            Amount = 100m,
            Method = PaymentMethod.CreditCard,
            Status = PaymentStatus.Settled,
            ReferenceNumber = "SYNTH-REF",
        });
        invoice.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoice.Id,
            PaymentDate = new DateTime(2026, 7, 6, 0, 0, 0, DateTimeKind.Utc),
            Amount = 50m,
            Method = PaymentMethod.Check,
            Status = PaymentStatus.Pending,
        });

        return invoice;
    }
}
