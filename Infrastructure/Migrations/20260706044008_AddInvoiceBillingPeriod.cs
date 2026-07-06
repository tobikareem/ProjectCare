using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarePath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceBillingPeriod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEndUtc",
                table: "Invoices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStartUtc",
                table: "Invoices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ServiceType",
                table: "Invoices",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(@"
;WITH InvoiceBackfill AS (
    SELECT
        i.[Id],
        MIN(li.[ServiceDate]) AS [PeriodStartUtc],
        DATEADD(day, 1, MAX(li.[ServiceDate])) AS [PeriodEndUtc],
        MIN(s.[ServiceType]) AS [MinServiceType],
        MAX(s.[ServiceType]) AS [MaxServiceType],
        SUM(CASE WHEN li.[ShiftId] IS NULL OR s.[Id] IS NULL THEN 1 ELSE 0 END) AS [MissingShiftCount]
    FROM [Invoices] i
    INNER JOIN [InvoiceLineItems] li ON li.[InvoiceId] = i.[Id] AND li.[IsDeleted] = 0
    LEFT JOIN [Shifts] s ON s.[Id] = li.[ShiftId] AND s.[IsDeleted] = 0
    WHERE i.[IsDeleted] = 0
    GROUP BY i.[Id]
)
IF EXISTS (
    SELECT 1
    FROM [Invoices] i
    LEFT JOIN InvoiceBackfill b ON b.[Id] = i.[Id]
    WHERE i.[IsDeleted] = 0
      AND (b.[Id] IS NULL OR b.[MissingShiftCount] > 0 OR b.[MinServiceType] <> b.[MaxServiceType])
)
BEGIN
    THROW 51000, 'Cannot backfill invoice billing periods because existing invoice rows are ambiguous.', 1;
END;

;WITH InvoiceBackfill AS (
    SELECT
        i.[Id],
        MIN(li.[ServiceDate]) AS [PeriodStartUtc],
        DATEADD(day, 1, MAX(li.[ServiceDate])) AS [PeriodEndUtc],
        MIN(s.[ServiceType]) AS [ServiceType]
    FROM [Invoices] i
    INNER JOIN [InvoiceLineItems] li ON li.[InvoiceId] = i.[Id] AND li.[IsDeleted] = 0
    INNER JOIN [Shifts] s ON s.[Id] = li.[ShiftId] AND s.[IsDeleted] = 0
    WHERE i.[IsDeleted] = 0
    GROUP BY i.[Id]
)
UPDATE i
SET
    [PeriodStartUtc] = b.[PeriodStartUtc],
    [PeriodEndUtc] = b.[PeriodEndUtc],
    [ServiceType] = b.[ServiceType]
FROM [Invoices] i
INNER JOIN InvoiceBackfill b ON b.[Id] = i.[Id];

;WITH DeletedInvoiceBackfill AS (
    SELECT
        i.[Id],
        COALESCE(MIN(li.[ServiceDate]), i.[InvoiceDate]) AS [PeriodStartUtc],
        COALESCE(DATEADD(day, 1, MAX(li.[ServiceDate])), DATEADD(day, 1, i.[InvoiceDate])) AS [PeriodEndUtc],
        COALESCE(MIN(s.[ServiceType]), 1) AS [ServiceType]
    FROM [Invoices] i
    LEFT JOIN [InvoiceLineItems] li ON li.[InvoiceId] = i.[Id] AND li.[IsDeleted] = 0
    LEFT JOIN [Shifts] s ON s.[Id] = li.[ShiftId] AND s.[IsDeleted] = 0
    WHERE i.[IsDeleted] = 1
    GROUP BY i.[Id], i.[InvoiceDate]
)
UPDATE i
SET
    [PeriodStartUtc] = b.[PeriodStartUtc],
    [PeriodEndUtc] = b.[PeriodEndUtc],
    [ServiceType] = b.[ServiceType]
FROM [Invoices] i
INNER JOIN DeletedInvoiceBackfill b ON b.[Id] = i.[Id];

IF EXISTS (
    SELECT 1
    FROM [Invoices]
    WHERE [IsDeleted] = 0
    GROUP BY [ClientId], [ServiceType], [PeriodStartUtc], [PeriodEndUtc]
    HAVING COUNT(*) > 1
)
BEGIN
    THROW 51001, 'Cannot add invoice billing-period uniqueness because existing invoice rows conflict.', 1;
END;");

            migrationBuilder.AlterColumn<DateTime>(
                name: "PeriodEndUtc",
                table: "Invoices",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "PeriodStartUtc",
                table: "Invoices",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ServiceType",
                table: "Invoices",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Client_Service_Period",
                table: "Invoices",
                columns: new[] { "ClientId", "ServiceType", "PeriodStartUtc", "PeriodEndUtc" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invoices_Client_Service_Period",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "PeriodEndUtc",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "PeriodStartUtc",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ServiceType",
                table: "Invoices");
        }
    }
}
