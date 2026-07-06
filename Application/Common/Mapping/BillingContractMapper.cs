using CarePath.Contracts.Billing;
using CarePath.Domain.Entities.Billing;
using CarePath.Domain.Entities.Scheduling;
using DomainServiceType = CarePath.Domain.Enumerations.ServiceType;
using ContractInvoiceStatus = CarePath.Contracts.Enumerations.InvoiceStatus;
using ContractPaymentMethod = CarePath.Contracts.Enumerations.PaymentMethod;
using ContractPaymentStatus = CarePath.Contracts.Enumerations.PaymentStatus;

namespace CarePath.Application.Common.Mapping;

internal static class BillingContractMapper
{
    internal static InvoiceSummaryDto ToSummaryDto(this Invoice invoice)
    {
        return new InvoiceSummaryDto
        {
            Id = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            ClientId = invoice.ClientId,
            ClientFullName = invoice.Client?.User?.FullName ?? string.Empty,
            InvoiceDate = invoice.InvoiceDate,
            DueDate = invoice.DueDate,
            Status = (ContractInvoiceStatus)(int)invoice.Status,
            Total = invoice.Total,
            AmountPaid = invoice.AmountPaid,
            Balance = invoice.Balance,
        };
    }

    internal static InvoiceDetailDto ToDetailDto(this Invoice invoice)
    {
        return new InvoiceDetailDto
        {
            Id = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            ClientId = invoice.ClientId,
            ClientFullName = invoice.Client?.User?.FullName ?? string.Empty,
            InvoiceDate = invoice.InvoiceDate,
            DueDate = invoice.DueDate,
            PaidDate = invoice.PaidDate,
            Status = (ContractInvoiceStatus)(int)invoice.Status,
            Subtotal = invoice.Subtotal,
            TaxAmount = invoice.TaxAmount,
            Total = invoice.Total,
            AmountPaid = invoice.AmountPaid,
            Balance = invoice.Balance,
            Notes = invoice.Notes,
            LineItems = invoice.LineItems
                .Select(lineItem => lineItem.ToDto())
                .ToArray(),
            Payments = invoice.Payments
                .Select(payment => payment.ToDto())
                .ToArray(),
        };
    }

    internal static InvoiceLineItemDto ToDto(this InvoiceLineItem lineItem)
    {
        return new InvoiceLineItemDto
        {
            Id = lineItem.Id,
            ShiftId = lineItem.ShiftId,
            Description = lineItem.Description,
            ServiceDate = lineItem.ServiceDate,
            BillableHours = lineItem.BillableHours,
            Total = lineItem.Total,
        };
    }


    internal static ShiftMarginDto ToMarginDto(this Shift shift)
    {
        return new ShiftMarginDto
        {
            ShiftId = shift.Id,
            ServiceType = (CarePath.Contracts.Enumerations.ServiceType)(int)shift.ServiceType,
            ScheduledStartTime = shift.ScheduledStartTime,
            BillableHours = shift.BillableHours,
            BillRate = shift.BillRate,
            PayRate = shift.PayRate,
            HourlyGrossMargin = shift.BillRate - shift.PayRate,
            GrossMargin = shift.GrossMargin,
            GrossMarginPercentage = shift.GrossMarginPercentage,
        };
    }

    internal static MarginSummaryDto ToMarginSummaryDto(this IReadOnlyList<Shift> shifts, DateTime periodStartUtc, DateTime periodEndUtc)
    {
        var inHome = shifts.Where(shift => shift.ServiceType == DomainServiceType.InHomeCare).ToArray();
        var facility = shifts.Where(shift => shift.ServiceType == DomainServiceType.FacilityStaffing).ToArray();

        return new MarginSummaryDto
        {
            PeriodStartUtc = periodStartUtc,
            PeriodEndUtc = periodEndUtc,
            InHomeCare = ToServiceLineMarginDto(DomainServiceType.InHomeCare, inHome),
            FacilityStaffing = ToServiceLineMarginDto(DomainServiceType.FacilityStaffing, facility),
            Overall = ToServiceLineMarginDto(DomainServiceType.InHomeCare, shifts),
        };
    }

    private static ServiceLineMarginDto ToServiceLineMarginDto(DomainServiceType serviceType, IReadOnlyList<Shift> shifts)
    {
        var totalHours = shifts.Sum(shift => shift.BillableHours);
        var totalRevenue = shifts.Sum(shift => shift.BillRate * shift.BillableHours);
        var totalLaborCost = shifts.Sum(shift => shift.PayRate * shift.BillableHours);
        var totalGrossMargin = totalRevenue - totalLaborCost;

        return new ServiceLineMarginDto
        {
            ServiceType = (CarePath.Contracts.Enumerations.ServiceType)(int)serviceType,
            ShiftCount = shifts.Count,
            TotalBillableHours = totalHours,
            TotalRevenue = totalRevenue,
            TotalLaborCost = totalLaborCost,
            TotalGrossMargin = totalGrossMargin,
            AverageHourlyGrossMargin = totalHours > 0m ? totalGrossMargin / totalHours : 0m,
            GrossMarginPercentage = totalRevenue > 0m ? totalGrossMargin / totalRevenue * 100m : 0m,
        };
    }
    internal static PaymentDto ToDto(this Payment payment)
    {
        return new PaymentDto
        {
            Id = payment.Id,
            InvoiceId = payment.InvoiceId,
            PaymentDate = payment.PaymentDate,
            Amount = payment.Amount,
            Method = (ContractPaymentMethod)(int)payment.Method,
            Status = (ContractPaymentStatus)(int)payment.Status,
            ReferenceNumber = payment.ReferenceNumber,
            FailureReason = payment.FailureReason,
        };
    }
}
