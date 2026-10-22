using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CarePath.Infrastructure.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class RemoveIdentityFromTenantContext : Migration
    {
        /// <inheritdoc />
        /// <remarks>
        /// Adds Users.PlatformUserId with a filtered unique index; leaves the legacy AspNet* tables untouched.
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Identity moved to the control plane (CP-04). The scaffolded table-drop operations for
            // the AspNet* tables were removed by hand (D-CP04-08): the legacy tables stay in place,
            // unmanaged, until the CP-05 forward-only drop migration after HomeLab verification.
            migrationBuilder.AddColumn<Guid>(
                name: "PlatformUserId",
                table: "Users",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_PlatformUserId",
                table: "Users",
                column: "PlatformUserId",
                unique: true,
                filter: "\"PlatformUserId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Forward-only for Identity: the legacy tables were never dropped, so Down only removes
            // the column this migration added.
            migrationBuilder.DropIndex(
                name: "IX_Users_PlatformUserId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PlatformUserId",
                table: "Users");
        }
    }
}
