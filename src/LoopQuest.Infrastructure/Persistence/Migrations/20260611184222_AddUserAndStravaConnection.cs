using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoopQuest.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserAndStravaConnection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StravaAthleteId = table.Column<long>(type: "bigint", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Connection_AccessToken = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Connection_RefreshToken = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Connection_ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Connection_Scope = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_users_StravaAthleteId",
                table: "users",
                column: "StravaAthleteId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
