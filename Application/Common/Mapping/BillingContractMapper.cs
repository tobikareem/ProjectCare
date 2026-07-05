using CarePath.Contracts.Billing;
using CarePath.Domain.Entities.Billing;
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
            RatePerHour = lineItem.RatePerHour,
            Total = lineItem.Total,
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
