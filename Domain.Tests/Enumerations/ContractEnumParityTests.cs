using FluentAssertions;
using ContractCertificationType = CarePath.Contracts.Enumerations.CertificationType;
using ContractDischargeDocumentSourceType = CarePath.Contracts.Enumerations.DischargeDocumentSourceType;
using ContractDischargeDocumentStatus = CarePath.Contracts.Enumerations.DischargeDocumentStatus;
using ContractEmploymentType = CarePath.Contracts.Enumerations.EmploymentType;
using ContractEscalationLevel = CarePath.Contracts.Enumerations.EscalationLevel;
using ContractEscalationTriggerType = CarePath.Contracts.Enumerations.EscalationTriggerType;
using ContractInvoiceStatus = CarePath.Contracts.Enumerations.InvoiceStatus;
using ContractPaymentMethod = CarePath.Contracts.Enumerations.PaymentMethod;
using ContractPaymentStatus = CarePath.Contracts.Enumerations.PaymentStatus;
using ContractReminderChannel = CarePath.Contracts.Enumerations.ReminderChannel;
using ContractReminderStatus = CarePath.Contracts.Enumerations.ReminderStatus;
using ContractReminderType = CarePath.Contracts.Enumerations.ReminderType;
using ContractServiceType = CarePath.Contracts.Enumerations.ServiceType;
using ContractShiftStatus = CarePath.Contracts.Enumerations.ShiftStatus;
using ContractTransitionInstructionCategory = CarePath.Contracts.Enumerations.TransitionInstructionCategory;
using ContractTransitionInstructionStatus = CarePath.Contracts.Enumerations.TransitionInstructionStatus;
using ContractTransitionPlanStatus = CarePath.Contracts.Enumerations.TransitionPlanStatus;
using ContractTransitionRiskLevel = CarePath.Contracts.Enumerations.TransitionRiskLevel;
using ContractUserRole = CarePath.Contracts.Enumerations.UserRole;
using DomainCertificationType = CarePath.Domain.Enumerations.CertificationType;
using DomainDischargeDocumentSourceType = CarePath.Domain.Enumerations.DischargeDocumentSourceType;
using DomainDischargeDocumentStatus = CarePath.Domain.Enumerations.DischargeDocumentStatus;
using DomainEmploymentType = CarePath.Domain.Enumerations.EmploymentType;
using DomainEscalationLevel = CarePath.Domain.Enumerations.EscalationLevel;
using DomainEscalationTriggerType = CarePath.Domain.Enumerations.EscalationTriggerType;
using DomainInvoiceStatus = CarePath.Domain.Enumerations.InvoiceStatus;
using DomainPaymentMethod = CarePath.Domain.Enumerations.PaymentMethod;
using DomainPaymentStatus = CarePath.Domain.Enumerations.PaymentStatus;
using DomainReminderChannel = CarePath.Domain.Enumerations.ReminderChannel;
using DomainReminderStatus = CarePath.Domain.Enumerations.ReminderStatus;
using DomainReminderType = CarePath.Domain.Enumerations.ReminderType;
using DomainServiceType = CarePath.Domain.Enumerations.ServiceType;
using DomainShiftStatus = CarePath.Domain.Enumerations.ShiftStatus;
using DomainTransitionInstructionCategory = CarePath.Domain.Enumerations.TransitionInstructionCategory;
using DomainTransitionInstructionStatus = CarePath.Domain.Enumerations.TransitionInstructionStatus;
using DomainTransitionPlanStatus = CarePath.Domain.Enumerations.TransitionPlanStatus;
using DomainTransitionRiskLevel = CarePath.Domain.Enumerations.TransitionRiskLevel;
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
        { typeof(DomainDischargeDocumentSourceType), typeof(ContractDischargeDocumentSourceType) },
        { typeof(DomainDischargeDocumentStatus), typeof(ContractDischargeDocumentStatus) },
        { typeof(DomainTransitionPlanStatus), typeof(ContractTransitionPlanStatus) },
        { typeof(DomainTransitionRiskLevel), typeof(ContractTransitionRiskLevel) },
        { typeof(DomainTransitionInstructionCategory), typeof(ContractTransitionInstructionCategory) },
        { typeof(DomainTransitionInstructionStatus), typeof(ContractTransitionInstructionStatus) },
        { typeof(DomainReminderType), typeof(ContractReminderType) },
        { typeof(DomainReminderChannel), typeof(ContractReminderChannel) },
        { typeof(DomainReminderStatus), typeof(ContractReminderStatus) },
        { typeof(DomainEscalationTriggerType), typeof(ContractEscalationTriggerType) },
        { typeof(DomainEscalationLevel), typeof(ContractEscalationLevel) },
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
