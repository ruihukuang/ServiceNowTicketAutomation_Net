using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistent.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Activities",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    IncidentNumber = table.Column<string>(type: "TEXT", nullable: false),
                    AssignedGroup = table.Column<string>(type: "TEXT", nullable: false),
                    LongDescription = table.Column<string>(type: "TEXT", nullable: false),
                    Team_Fixed_Issue = table.Column<string>(type: "TEXT", nullable: true),
                    Team_Fixed_Issue_AI_Format = table.Column<string>(type: "TEXT", nullable: true),
                    Team_Included_in_Ticket = table.Column<string>(type: "TEXT", nullable: true),
                    Team_Included_in_Ticket_AI_Format = table.Column<string>(type: "TEXT", nullable: true),
                    NumberTeam_Included_in_Ticket = table.Column<int>(type: "INTEGER", nullable: false),
                    NumberTeam_Fixed_Issue = table.Column<int>(type: "INTEGER", nullable: false),
                    Is_AissignedGroup_ResponsibleTeam = table.Column<string>(type: "TEXT", nullable: true),
                    Did_AssignedGroup_Fix_Issue = table.Column<string>(type: "TEXT", nullable: true),
                    Summary_Issue = table.Column<string>(type: "TEXT", nullable: true),
                    Summary_Issue_AI = table.Column<string>(type: "TEXT", nullable: true),
                    System = table.Column<string>(type: "TEXT", nullable: true),
                    System_AI = table.Column<string>(type: "TEXT", nullable: true),
                    Issue = table.Column<string>(type: "TEXT", nullable: true),
                    Issue_AI = table.Column<string>(type: "TEXT", nullable: true),
                    Root_Cause = table.Column<string>(type: "TEXT", nullable: true),
                    Duplicate = table.Column<string>(type: "TEXT", nullable: true),
                    Duplicate_AI = table.Column<string>(type: "TEXT", nullable: true),
                    OpenDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    OpenDate_Year = table.Column<string>(type: "TEXT", nullable: true),
                    OpenDate_Month = table.Column<string>(type: "TEXT", nullable: true),
                    OpenDate_Day = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedDate_Year = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedDate_Month = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedDate_Day = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Activities", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Activities");
        }
    }
}
