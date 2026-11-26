using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PrivateLibrary.Data;

#nullable disable

namespace PrivateLibrary.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261126023800_AddOnlineConferenceReminders")]
public partial class AddOnlineConferenceReminders : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ConferenceUrl",
            table: "Events",
            type: "varchar(2048)",
            maxLength: 2048,
            nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AddColumn<string>(
            name: "ActionUrl",
            table: "Notifications",
            type: "varchar(2048)",
            maxLength: 2048,
            nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ConferenceUrl",
            table: "Events");

        migrationBuilder.DropColumn(
            name: "ActionUrl",
            table: "Notifications");
    }
}
