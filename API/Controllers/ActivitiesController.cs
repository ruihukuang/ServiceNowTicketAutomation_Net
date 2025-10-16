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
        // Extract: Get all records where Team_Fixed_Issue is not null
        var records_teamfix = await context.Activities
            .Where(r => r.Team_Fixed_Issue != null)
            .ToListAsync();

            // Transform and Load: Update NumberTeam_Fixed_Issue with the number of teams
            foreach (var record in records_teamfix)
            {
                // Calculate the number of teams based on the number of commas
                int numberOfTeams = record.Team_Fixed_Issue.Contains(',')
                    ? record.Team_Fixed_Issue.Split(',').Length
                    : 1;

                record.NumberTeam_Fixed_Issue = numberOfTeams;
            }
        
                // Extract: Get all records where Team_Fixed_Issue is not null
        var records_teamticket = await context.Activities
            .Where(r => r.Team_Included_in_Ticket != null)
            .ToListAsync();

            // Transform and Load: Update NumberTeam_Fixed_Issue with the number of teams
            foreach (var record in records_teamticket)
            {
                // Calculate the number of teams based on the number of commas
                int numberOfTeams = record.Team_Included_in_Ticket.Contains(',') 
                    ? record.Team_Included_in_Ticket.Split(',').Length 
                    : 1;

                record.NumberTeam_Included_in_Ticket = numberOfTeams;
            }


        // Save changes to the database
        await context.SaveChangesAsync();

        return Ok("Data processing completed.");
        }
    }
}
