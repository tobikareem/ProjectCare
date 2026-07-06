using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarePath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTransitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TransitionPlanId",
                table: "VisitNotes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DischargeDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    RawContent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SourceReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    UploadedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DischargeDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DischargeDocuments_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TransitionPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DischargeDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HospitalName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DischargeDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TransitionWindowEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RiskLevel = table.Column<int>(type: "int", nullable: false),
                    VerifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActivatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransitionPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransitionPlans_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransitionPlans_DischargeDocuments_DischargeDocumentId",
                        column: x => x.DischargeDocumentId,
                        principalTable: "DischargeDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TransitionCheckIns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransitionPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CheckInDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Channel = table.Column<int>(type: "int", nullable: false),
                    ResponsesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContainsWarningSymptom = table.Column<bool>(type: "bit", nullable: false),
                    ReviewedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransitionCheckIns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransitionCheckIns_TransitionPlans_TransitionPlanId",
                        column: x => x.TransitionPlanId,
                        principalTable: "TransitionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TransitionEscalations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransitionPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TriggerType = table.Column<int>(type: "int", nullable: false),
                    TriggerDetails = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    EscalationLevel = table.Column<int>(type: "int", nullable: false),
                    EscalatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcknowledgedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AcknowledgedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolutionNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransitionEscalations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransitionEscalations_TransitionPlans_TransitionPlanId",
                        column: x => x.TransitionPlanId,
                        principalTable: "TransitionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TransitionInstructions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransitionPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    InstructionText = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    SourceText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConfidenceScore = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    ClinicalNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    NeedsPharmacistReview = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransitionInstructions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransitionInstructions_TransitionPlans_TransitionPlanId",
                        column: x => x.TransitionPlanId,
                        principalTable: "TransitionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TransitionReminders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransitionPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransitionInstructionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReminderType = table.Column<int>(type: "int", nullable: false),
                    Channel = table.Column<int>(type: "int", nullable: false),
                    ScheduledAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcknowledgedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransitionReminders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransitionReminders_TransitionInstructions_TransitionInstructionId",
                        column: x => x.TransitionInstructionId,
                        principalTable: "TransitionInstructions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransitionReminders_TransitionPlans_TransitionPlanId",
                        column: x => x.TransitionPlanId,
                        principalTable: "TransitionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VisitNotes_TransitionPlanId",
                table: "VisitNotes",
                column: "TransitionPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_DischargeDocuments_ClientId",
                table: "DischargeDocuments",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_DischargeDocuments_IsDeleted",
                table: "DischargeDocuments",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_DischargeDocuments_Status",
                table: "DischargeDocuments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TransitionCheckIns_IsDeleted",
                table: "TransitionCheckIns",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_TransitionCheckIns_TransitionPlanId",
                table: "TransitionCheckIns",
                column: "TransitionPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_TransitionEscalations_IsDeleted",
                table: "TransitionEscalations",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_TransitionEscalations_TransitionPlanId",
                table: "TransitionEscalations",
                column: "TransitionPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_TransitionInstructions_IsDeleted",
                table: "TransitionInstructions",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_TransitionInstructions_Status",
                table: "TransitionInstructions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TransitionInstructions_TransitionPlanId",
                table: "TransitionInstructions",
                column: "TransitionPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_TransitionPlans_ClientId",
                table: "TransitionPlans",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_TransitionPlans_DischargeDocumentId",
                table: "TransitionPlans",
                column: "DischargeDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_TransitionPlans_IsDeleted",
                table: "TransitionPlans",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_TransitionPlans_Status",
                table: "TransitionPlans",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TransitionReminders_IsDeleted",
                table: "TransitionReminders",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_TransitionReminders_ScheduledAt",
                table: "TransitionReminders",
                column: "ScheduledAt");

            migrationBuilder.CreateIndex(
                name: "IX_TransitionReminders_Status",
                table: "TransitionReminders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TransitionReminders_TransitionInstructionId",
                table: "TransitionReminders",
                column: "TransitionInstructionId");

            migrationBuilder.CreateIndex(
                name: "IX_TransitionReminders_TransitionPlanId",
                table: "TransitionReminders",
                column: "TransitionPlanId");

            migrationBuilder.AddForeignKey(
                name: "FK_VisitNotes_TransitionPlans_TransitionPlanId",
                table: "VisitNotes",
                column: "TransitionPlanId",
                principalTable: "TransitionPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "THROW 51002, 'AddTransitions is forward-only because rollback would destroy clinical PHI and VisitNote transition links.', 1;");
        }
    }
}
