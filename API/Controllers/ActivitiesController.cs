using System;
using Persistent;
using Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;


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


}