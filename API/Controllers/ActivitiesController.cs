using System;
using Persistent;
using Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Linq;

namespace API.Controllers
{
    public class ActivitiesController(AppDbContext context) : BaseApiController
    {
        [HttpGet]
        public async Task<ActionResult<List<Activity>>> GetActivities()
        {
            return await context.Activities.ToListAsync();
        }

        [HttpGet("{Id}")]
        public async Task<ActionResult<Activity>> GetActivitiesDetails(string Id)
        {
            var activity = await context.Activities.FindAsync(Id);

            if (activity == null) return NotFound();

            return activity;
        }

        [HttpPost("process")]
        public async Task<IActionResult> ProcessData()
        {
            // Determine which tasks to run based on the state of the records
            bool runTask1 = await context.Activities.AnyAsync(a => a.NumberTeam_Fixed_Issue == 0 && a.NumberTeam_Included_in_Ticket > 0);
            bool runTask2 = await context.Activities.AnyAsync(a => a.NumberTeam_Fixed_Issue > 0 && a.NumberTeam_Included_in_Ticket == 0);
            bool runBothTasks = await context.Activities.AnyAsync(a => a.NumberTeam_Fixed_Issue == 0 && a.NumberTeam_Included_in_Ticket == 0);

            if (runBothTasks)
            {
                // Run both tasks
                await RunTask1();
                await RunTask2();
            }
            else if (runTask1)
            {
                // Run task related to NumberTeam_Fixed_Issue
                await RunTask1();
            }
            else if (runTask2)
            {
                // Run task related to NumberTeam_Included_in_Ticket
                await RunTask2();
            }
            else
            {
                return Ok("No processing needed.");
            }

            // Save changes to the database
            await context.SaveChangesAsync();

            return Ok("Data processing completed.");
        }

        private async Task RunTask1()
        {
            // Extract: Get all records where Team_Fixed_Issue is not null and NumberTeam_Fixed_Issue is 0
            var records_teamfix = await context.Activities
                .Where(r => r.Team_Fixed_Issue != null && r.NumberTeam_Fixed_Issue == 0)
                .ToListAsync();

            // Transform and Load: Update NumberTeam_Fixed_Issue with the number of teams
            foreach (var record in records_teamfix)
            {
                int numberOfTeams = record.Team_Fixed_Issue.Contains(',')
                    ? record.Team_Fixed_Issue.Split(',').Length
                    : 1;

                record.NumberTeam_Fixed_Issue = numberOfTeams;
            }
        }

        private async Task RunTask2()
        {
            // Extract: Get all records where Team_Included_in_Ticket is not null and NumberTeam_Included_in_Ticket is 0
            var records_teamticket = await context.Activities
                .Where(r => r.Team_Included_in_Ticket != null && r.NumberTeam_Included_in_Ticket == 0)
                .ToListAsync();

            // Transform and Load: Update NumberTeam_Included_in_Ticket with the number of teams
            foreach (var record in records_teamticket)
            {
                int numberOfTeams = record.Team_Included_in_Ticket.Contains(',')
                    ? record.Team_Included_in_Ticket.Split(',').Length
                    : 1;

                record.NumberTeam_Included_in_Ticket = numberOfTeams;
            }
        }
    }
}
