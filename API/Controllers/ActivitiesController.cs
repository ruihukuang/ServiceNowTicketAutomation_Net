using System;
using Persistent;
using Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Linq;

namespace API.Controllers;

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
        var records = await context.Activities
            .Where(r => r.Team_Fixed_Issue != null)
            .ToListAsync();

        // Transform and Load: Update NumberTeam_Fixed_Issue with the number of teams
        foreach (var record in records)
        {
            // Calculate the number of teams based on the number of colons
            int numberOfTeams = record.Team_Fixed_Issue.Split(',').Length;
            record.NumberTeam_Fixed_Issue = numberOfTeams;
        }

        // Save changes to the database
        await context.SaveChangesAsync();

        return Ok("Data processing completed.");
    }

}
