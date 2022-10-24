using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gameboard.Api.Data.Migrations.PostgreSQL.GameboardDb
{
    public partial class AddTeamsTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "InviteCode",
                table: "Players",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvitationHostId",
                table: "ArchivedChallenges",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InviteCode",
                table: "ArchivedChallenges",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TeamCreatedOn",
                table: "ArchivedChallenges",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.CreateTable(
                name: "Teams",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    InviteCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    InivitationHostId = table.Column<string>(type: "character varying(40)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Teams_Players_InivitationHostId",
                        column: x => x.InivitationHostId,
                        principalTable: "Players",
                        principalColumn: "Id");
                });

            migrationBuilder.Sql("INSERT INTO \"Teams\" (\"Id\", \"CreatedOn\") SELECT DISTINCT \"TeamId\", now() at time zone \'utc\' FROM \"Players\"");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_InivitationHostId",
                table: "Teams",
                column: "InivitationHostId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Players_Teams_TeamId",
                table: "Players",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Players_Teams_TeamId",
                table: "Players");

            migrationBuilder.DropTable(
                name: "Teams");

            migrationBuilder.DropColumn(
                name: "InvitationHostId",
                table: "ArchivedChallenges");

            migrationBuilder.DropColumn(
                name: "InviteCode",
                table: "ArchivedChallenges");

            migrationBuilder.DropColumn(
                name: "TeamCreatedOn",
                table: "ArchivedChallenges");

            migrationBuilder.AlterColumn<string>(
                name: "InviteCode",
                table: "Players",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
