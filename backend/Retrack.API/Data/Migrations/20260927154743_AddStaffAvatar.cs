using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace Retrack.API.Data.Migrations;

public partial class AddStaffAvatar : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<string>(name: "avatar_url", table: "users",
            type: "character varying(2048)", maxLength: 2048, nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "avatar_url", table: "users");
}
