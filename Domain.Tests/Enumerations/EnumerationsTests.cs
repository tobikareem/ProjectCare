using CarePath.Domain.Enumerations;
using FluentAssertions;

namespace CarePath.Domain.Tests.Enumerations;

public class EnumerationsTests
{
    // ── UserRole ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Coordinator)]
    [InlineData(UserRole.Caregiver)]
    [InlineData(UserRole.Client)]
    [InlineData(UserRole.FacilityManager)]
    public void UserRole_MemberIsDefined(UserRole role)
    {
        Enum.IsDefined(role).Should().BeTrue();
    }

    [Fact]
    public void UserRole_HasFiveMembers()
    {
        Enum.GetValues<UserRole>().Should().HaveCount(5);
    }

    // ── EmploymentType ────────────────────────────────────────────────────────

    [Theory]
    [InlineData(EmploymentType.W2Employee)]
    [InlineData(EmploymentType.Contractor1099)]
    public void EmploymentType_MemberIsDefined(EmploymentType type)
    {
        Enum.IsDefined(type).Should().BeTrue();
    }

    [Fact]
    public void EmploymentType_HasTwoMembers()
    {
        Enum.GetValues<EmploymentType>().Should().HaveCount(2);
    }

    // ── ServiceType ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData(ServiceType.InHomeCare)]
    [InlineData(ServiceType.FacilityStaffing)]
    public void ServiceType_MemberIsDefined(ServiceType type)
    {
        Enum.IsDefined(type).Should().BeTrue();
    }

    // ── ShiftStatus ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData(ShiftStatus.Scheduled)]
    [InlineData(ShiftStatus.InProgress)]
    [InlineData(ShiftStatus.Completed)]
    [InlineData(ShiftStatus.Cancelled)]
    [InlineData(ShiftStatus.NoShow)]
    public void ShiftStatus_MemberIsDefined(ShiftStatus status)
    {
        Enum.IsDefined(status).Should().BeTrue();
    }

    [Fact]
    public void ShiftStatus_HasFiveMembers()
    {
        Enum.GetValues<ShiftStatus>().Should().HaveCount(5);
    }

    // ── InvoiceStatus ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(InvoiceStatus.Draft)]
    [InlineData(InvoiceStatus.Sent)]
    [InlineData(InvoiceStatus.Paid)]
    [InlineData(InvoiceStatus.PartiallyPaid)]
    [InlineData(InvoiceStatus.Overdue)]
    [InlineData(InvoiceStatus.Cancelled)]
    public void InvoiceStatus_MemberIsDefined(InvoiceStatus status)
    {
        Enum.IsDefined(status).Should().BeTrue();
    }

    // ── PaymentMethod ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(PaymentMethod.Cash)]
    [InlineData(PaymentMethod.Check)]
    [InlineData(PaymentMethod.CreditCard)]
    [InlineData(PaymentMethod.BankTransfer)]
    [InlineData(PaymentMethod.Insurance)]
    [InlineData(PaymentMethod.Medicaid)]
    public void PaymentMethod_MemberIsDefined(PaymentMethod method)
    {
        Enum.IsDefined(method).Should().BeTrue();
    }

    // ── PaymentStatus ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(PaymentStatus.Pending)]
    [InlineData(PaymentStatus.Settled)]
    [InlineData(PaymentStatus.Failed)]
    [InlineData(PaymentStatus.Refunded)]
    public void PaymentStatus_MemberIsDefined(PaymentStatus status)
    {
        Enum.IsDefined(status).Should().BeTrue();
    }

    // ── CertificationType ─────────────────────────────────────────────────────

    [Theory]
    // Board credentials (Maryland Board of Nursing)
    [InlineData(CertificationType.CNA)]
    [InlineData(CertificationType.LPN)]
    [InlineData(CertificationType.RN)]
    [InlineData(CertificationType.HHA)]
    [InlineData(CertificationType.GNA)]
    [InlineData(CertificationType.CRMA)]
    // Training completions (no state-issued credential number)
    [InlineData(CertificationType.CPR)]
    [InlineData(CertificationType.FirstAid)]
    [InlineData(CertificationType.Dementia)]
    [InlineData(CertificationType.Alzheimers)]
    public void CertificationType_MemberIsDefined(CertificationType type)
    {
        Enum.IsDefined(type).Should().BeTrue();
    }

    [Fact]
    public void CertificationType_HasTenMembers()
    {
        Enum.GetValues<CertificationType>().Should().HaveCount(10);
    }
}
