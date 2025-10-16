using System;
using Persistent;
using Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;

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

            var tasks = new List<Task>();

            if (runBothTasks)
            {
                // Run both tasks concurrently
                tasks.Add(RunTask1());
                tasks.Add(RunTask2());
            }
            else if (runTask1)
            {
                // Run task related to NumberTeam_Fixed_Issue
                tasks.Add(RunTask1());
            }
            else if (runTask2)
            {
                // Run task related to NumberTeam_Included_in_Ticket
                tasks.Add(RunTask2());
            }

            // Run the new task to check and update Did_AssignedGroup_Fix_Issue concurrently
            tasks.Add(CheckAndUpdateAssignedGroup());

            // Run the task to extract date components concurrently
            tasks.Add(ExtractDateComponents());

            // Wait for all tasks to complete
            await Task.WhenAll(tasks);

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

        private async Task CheckAndUpdateAssignedGroup()
        {
            // Extract: Get all records where Did_AssignedGroup_Fix_Issue is null
            var records_assignedGroup = await context.Activities
                .Where(r => r.Did_AssignedGroup_Fix_Issue == null)
                .ToListAsync();

            // Check if AssignedGroup is included in Team_Fixed_Issue and update if necessary
            foreach (var record in records_assignedGroup)
            {
                if (record.Team_Fixed_Issue != null && record.AssignedGroup != null)
                {
                    // Remove spaces and convert to uppercase
                    var assignedGroupUpper = record.AssignedGroup.Replace(" ", "").ToUpper();
                    var teamListUpper = record.Team_Fixed_Issue.Split(',')
                        .Select(t => t.Replace(" ", "").ToUpper())
                        .ToList();

                    // Check if AssignedGroup is a part of Team_Fixed_Issue
                    if (teamListUpper.Contains(assignedGroupUpper))
                    {
                        // Set Did_AssignedGroup_Fix_Issue to "Y" if AssignedGroup is included in Team_Fixed_Issue
                        record.Did_AssignedGroup_Fix_Issue = "Y";
                    }
                    else
                    {
                        // Set Did_AssignedGroup_Fix_Issue to "N" if not included
                        record.Did_AssignedGroup_Fix_Issue = "N";
                    }
                }
            }
        }

        private async Task ExtractDateComponents()
        {
            // Extract: Get all records where date components are null
            var records_with_null_dates = await context.Activities
                .Where(r => r.OpenDate_Year == null || r.OpenDate_Month == null || r.OpenDate_Day == null ||
                            r.UpdatedDate_Year == null || r.UpdatedDate_Month == null || r.UpdatedDate_Day == null)
                .ToListAsync();

            // Extract year, month, and day from OpenDate and UpdatedDate
            foreach (var record in records_with_null_dates)
            {
                record.OpenDate_Year = record.OpenDate.Year.ToString();
                record.OpenDate_Month = record.OpenDate.Month.ToString();
                record.OpenDate_Day = record.OpenDate.Day.ToString();

                record.UpdatedDate_Year = record.UpdatedDate.Year.ToString();
                record.UpdatedDate_Month = record.UpdatedDate.Month.ToString();
                record.UpdatedDate_Day = record.UpdatedDate.Day.ToString();
            }
        }
    }
}
