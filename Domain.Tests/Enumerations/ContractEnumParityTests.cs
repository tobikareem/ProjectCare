using FluentAssertions;
using ContractCertificationType = CarePath.Contracts.Enumerations.CertificationType;
using ContractEmploymentType = CarePath.Contracts.Enumerations.EmploymentType;
using ContractInvoiceStatus = CarePath.Contracts.Enumerations.InvoiceStatus;
using ContractPaymentMethod = CarePath.Contracts.Enumerations.PaymentMethod;
using ContractPaymentStatus = CarePath.Contracts.Enumerations.PaymentStatus;
using ContractServiceType = CarePath.Contracts.Enumerations.ServiceType;
using ContractShiftStatus = CarePath.Contracts.Enumerations.ShiftStatus;
using ContractUserRole = CarePath.Contracts.Enumerations.UserRole;
using DomainCertificationType = CarePath.Domain.Enumerations.CertificationType;
using DomainEmploymentType = CarePath.Domain.Enumerations.EmploymentType;
using DomainInvoiceStatus = CarePath.Domain.Enumerations.InvoiceStatus;
using DomainPaymentMethod = CarePath.Domain.Enumerations.PaymentMethod;
using DomainPaymentStatus = CarePath.Domain.Enumerations.PaymentStatus;
using DomainServiceType = CarePath.Domain.Enumerations.ServiceType;
using DomainShiftStatus = CarePath.Domain.Enumerations.ShiftStatus;
using DomainUserRole = CarePath.Domain.Enumerations.UserRole;

namespace CarePath.Domain.Tests.Enumerations;

public sealed class ContractEnumParityTests
{
    public static TheoryData<Type, Type> EnumPairs => new()
    {
        { typeof(DomainUserRole), typeof(ContractUserRole) },
        { typeof(DomainEmploymentType), typeof(ContractEmploymentType) },
        { typeof(DomainCertificationType), typeof(ContractCertificationType) },
        { typeof(DomainServiceType), typeof(ContractServiceType) },
        { typeof(DomainShiftStatus), typeof(ContractShiftStatus) },
        { typeof(DomainInvoiceStatus), typeof(ContractInvoiceStatus) },
        { typeof(DomainPaymentMethod), typeof(ContractPaymentMethod) },
        { typeof(DomainPaymentStatus), typeof(ContractPaymentStatus) },
    };

    [Theory]
    [MemberData(nameof(EnumPairs))]
    public void ContractEnum_WhenComparedWithDomain_HasIdenticalMemberNamesAndValues(Type domainType, Type contractType)
    {
        // Arrange
        var domainMembers = GetEnumMembers(domainType);
        var contractMembers = GetEnumMembers(contractType);

        // Act / Assert
        contractMembers.Should().Equal(domainMembers);
        domainMembers.Should().Equal(contractMembers);
    }

    private static IReadOnlyList<(string Name, int Value)> GetEnumMembers(Type enumType)
    {
        enumType.IsEnum.Should().BeTrue();

        return Enum.GetNames(enumType)
            .Select(name => (Name: name, Value: Convert.ToInt32(Enum.Parse(enumType, name))))
            .OrderBy(member => member.Value)
            .ThenBy(member => member.Name, StringComparer.Ordinal)
            .ToArray();
    }
}