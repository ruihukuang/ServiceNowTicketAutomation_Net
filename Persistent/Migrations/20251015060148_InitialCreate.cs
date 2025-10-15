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
                    Team_Fixed_Issue = table.Column<string>(type: "TEXT", nullable: false),
                    Team_Fixed_Issue_AI_Format = table.Column<string>(type: "TEXT", nullable: false),
                    Team_Included_in_Ticket = table.Column<string>(type: "TEXT", nullable: false),
                    Team_Included_in_Ticket_AI_Format = table.Column<string>(type: "TEXT", nullable: false),
                    NumberTeam_Included_in_Ticket = table.Column<int>(type: "INTEGER", nullable: false),
                    NumberTeam_Fixed_Issue = table.Column<int>(type: "INTEGER", nullable: false),
                    Is_AissignedGroup_ResponsibleTeam = table.Column<string>(type: "TEXT", nullable: false),
                    Is_AssignedGroup_Fixed_Issue = table.Column<string>(type: "TEXT", nullable: false),
                    Summary_Issue = table.Column<string>(type: "TEXT", nullable: false),
                    Summary_Issue_AI = table.Column<string>(type: "TEXT", nullable: false),
                    System = table.Column<string>(type: "TEXT", nullable: false),
                    System_AI = table.Column<string>(type: "TEXT", nullable: false),
                    Issue = table.Column<string>(type: "TEXT", nullable: false),
                    Issue_AI = table.Column<string>(type: "TEXT", nullable: false),
                    Root_Cause = table.Column<string>(type: "TEXT", nullable: false),
                    Duplicate = table.Column<string>(type: "TEXT", nullable: false),
                    Duplicate_AI = table.Column<string>(type: "TEXT", nullable: false)
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
