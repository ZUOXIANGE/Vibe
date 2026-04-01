using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShortLinker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAccessLogEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccessLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    ShortCode = table.Column<string>(type: "text", nullable: false),
                    AccessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IpAddress = table.Column<string>(type: "text", nullable: true),
                    UserAgent = table.Column<string>(type: "text", nullable: true),
                    Referer = table.Column<string>(type: "text", nullable: true),
                    Browser = table.Column<string>(type: "text", nullable: true),
                    OS = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccessLogs_AccessedAt",
                table: "AccessLogs",
                column: "AccessedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AccessLogs_TenantId",
                table: "AccessLogs",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccessLogs");
        }
    }
}
