using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <inheritdoc />
public partial class ChangeNotificationColumns : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "StartDisplayDate",
            table: "Notifications");

        migrationBuilder.DropColumn(
            name: "EndDisplayDate",
            table: "Notifications");

        migrationBuilder.RenameColumn(
            name: "StartDateTime",
            table: "Notifications",
            newName: "PublishFrom");

        migrationBuilder.RenameColumn(
            name: "EndDateTime",
            table: "Notifications",
            newName: "PublishUntil");

        migrationBuilder.AddColumn<DateTime>(
            name: "EventStart",
            table: "Notifications",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "EventEnd",
            table: "Notifications",
            type: "datetime2",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "EventStart",
            table: "Notifications");

        migrationBuilder.DropColumn(
            name: "EventEnd",
            table: "Notifications");

        migrationBuilder.RenameColumn(
            name: "PublishFrom",
            table: "Notifications",
            newName: "StartDateTime");

        migrationBuilder.RenameColumn(
            name: "PublishUntil",
            table: "Notifications",
            newName: "EndDateTime");

        migrationBuilder.AddColumn<DateOnly>(
            name: "StartDisplayDate",
            table: "Notifications",
            type: "date",
            nullable: true);

        migrationBuilder.AddColumn<DateOnly>(
            name: "EndDisplayDate",
            table: "Notifications",
            type: "date",
            nullable: true);
    }
}
