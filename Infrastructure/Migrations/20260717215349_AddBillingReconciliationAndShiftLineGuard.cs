using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarePath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBillingReconciliationAndShiftLineGuard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // D-S6-18 fail-closed preflight: if any shift is already linked by more than one
            // invoice line (including soft-deleted lines), the migration must FAIL rather than
            // mutate or delete billing data. Duplicates require a reviewed manual correction.
            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT ShiftId
                    FROM InvoiceLineItems
                    WHERE ShiftId IS NOT NULL
                    GROUP BY ShiftId
                    HAVING COUNT(*) > 1)
                BEGIN
                    THROW 51003, 'AddBillingReconciliationAndShiftLineGuard preflight failed: duplicate InvoiceLineItems.ShiftId links exist and must be manually reviewed before the unique index can be created. No data was modified.', 1;
                END;
                """);

            migrationBuilder.DropIndex(
                name: "IX_InvoiceLineItems_ShiftId",
                table: "InvoiceLineItems");

            migrationBuilder.CreateTable(
                name: "BillingReconciliationResolutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShiftId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<int>(type: "int", nullable: false),
                    ResolvedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SupersedesResolutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingReconciliationResolutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BillingReconciliationResolutions_BillingReconciliationResolutions_SupersedesResolutionId",
                        column: x => x.SupersedesResolutionId,
                        principalTable: "BillingReconciliationResolutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BillingReconciliationResolutions_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "Shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_InvoiceLineItems_ShiftId_NotNull",
                table: "InvoiceLineItems",
                column: "ShiftId",
                unique: true,
                filter: "[ShiftId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BillingReconciliationResolutions_IsDeleted",
                table: "BillingReconciliationResolutions",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_BillingReconciliationResolutions_Shift_ResolvedAt",
                table: "BillingReconciliationResolutions",
                columns: new[] { "ShiftId", "ResolvedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_BillingReconciliationResolutions_SupersedesResolutionId",
                table: "BillingReconciliationResolutions",
                column: "SupersedesResolutionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Fail closed once real decisions exist: rolling back would destroy append-only
            // reconciliation history and remove the double-billing guard. An empty table may
            // roll back cleanly (fresh environments).
            migrationBuilder.Sql(
                """
                IF EXISTS (SELECT 1 FROM BillingReconciliationResolutions)
                BEGIN
                    THROW 51004, 'AddBillingReconciliationAndShiftLineGuard is forward-only once reconciliation resolutions exist; rollback would destroy append-only billing decisions.', 1;
                END;
                """);

            migrationBuilder.DropTable(
                name: "BillingReconciliationResolutions");

            migrationBuilder.DropIndex(
                name: "UX_InvoiceLineItems_ShiftId_NotNull",
                table: "InvoiceLineItems");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLineItems_ShiftId",
                table: "InvoiceLineItems",
                column: "ShiftId");
        }
    }
}
