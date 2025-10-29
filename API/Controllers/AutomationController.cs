using System;
using Persistent;
using Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

namespace API.Controllers
{
    public class AutomationController(AppDbContext context) : BaseApiController
    {
        [HttpGet]
        public IActionResult Get()
        {
            return Ok("Another automation controller is working!");
        }

        [HttpPost("process_further")]
        public async Task<IActionResult> ProcessData()
        {
            // Process fields in the correct order
            await UpdateGuidedSLADays();
            await UpdateMetSLA(); // Must complete before UpdateExtraDaysAfterSLA
            await UpdateExtraDaysAfterSLA(); // Depends on Met_SLA being populated
            await UpdateIsAssignedGroupResponsibleTeam();

            // Save changes to the database
            await context.SaveChangesAsync();

            return Ok("Another data processing completed.");
        }

        private async Task UpdateGuidedSLADays()
        {
            // Extract: Get all records where Priority is P4 and Guided_SLAdays is not set to 5
            var records_p4_priority = await context.Activities
                .Where(r => r.Priority != null && 
                           r.Priority.ToUpper() == "P4" && 
                           (r.Guided_SLAdays == null || r.Guided_SLAdays != 5))
                .ToListAsync();

            // Transform and Load: Set Guided_SLAdays to 5 for P4 priority records
            foreach (var record in records_p4_priority)
            {
                record.Guided_SLAdays = 5;
                Console.WriteLine($"Set Guided_SLAdays to 5 for activity {record.Id} with Priority P4");
            }
        }

        private async Task UpdateMetSLA()
        {
            // Extract: Get all records where we need to calculate Met_SLA
            var records_to_check_sla = await context.Activities
                .Where(r => r.OpenDate.HasValue && 
                           r.UpdatedDate.HasValue && 
                           (r.Guided_SLAdays != null || r.Priority != null))
                .ToListAsync();

            // Transform and Load: Calculate and update Met_SLA
            foreach (var record in records_to_check_sla)
            {
                // If Guided_SLAdays is null but priority is P4, set it to 5 first
                if (record.Guided_SLAdays == null && 
                    record.Priority != null && 
                    record.Priority.ToUpper() == "P4")
                {
                    record.Guided_SLAdays = 5;
                    Console.WriteLine($"Set Guided_SLAdays to 5 for activity {record.Id} during Met_SLA calculation");
                }

                // Only calculate Met_SLA if we have a valid Guided_SLAdays value
                if (record.Guided_SLAdays.HasValue && record.Guided_SLAdays > 0)
                {
                    // Calculate days between UpdatedDate and OpenDate
                    var daysToResolve = (record.UpdatedDate.Value - record.OpenDate.Value).TotalDays;

                    // Set Met_SLA to "no" if days are more than Guided_SLAdays, otherwise "yes"
                    record.Met_SLA = daysToResolve > record.Guided_SLAdays ? "no" : "yes";
                    Console.WriteLine($"Set Met_SLA to '{record.Met_SLA}' for activity {record.Id} (Resolution: {daysToResolve:F2} days vs SLA: {record.Guided_SLAdays} days)");
                }
                else
                {
                    Console.WriteLine($"Skipped Met_SLA calculation for activity {record.Id} - no valid Guided_SLAdays value");
                }
            }
        }

        private async Task UpdateExtraDaysAfterSLA()
        {
            // First, save changes to ensure Met_SLA is persisted
            await context.SaveChangesAsync();
            Console.WriteLine("Saved changes to ensure Met_SLA values are persisted");

            // Extract: Get all records where Met_SLA is "no" and ExtraDays_AfterSLAdays needs to be calculated
            var records_exceeded_sla = await context.Activities
                .Where(r => r.Met_SLA == "no" && 
                           r.OpenDate.HasValue && 
                           r.UpdatedDate.HasValue && 
                           r.Guided_SLAdays.HasValue &&
                           r.Guided_SLAdays > 0)
                .ToListAsync();

            Console.WriteLine($"Found {records_exceeded_sla.Count} records that exceeded SLA");

            // Transform and Load: Calculate extra days after SLA
            foreach (var record in records_exceeded_sla)
            {
                var daysToResolve = (record.UpdatedDate.Value - record.OpenDate.Value).TotalDays;
                record.ExtraDays_AfterSLAdays = Convert.ToInt64(daysToResolve - record.Guided_SLAdays);
                Console.WriteLine($"Set ExtraDays_AfterSLAdays to {record.ExtraDays_AfterSLAdays} for activity {record.Id}");
            }

            // Set ExtraDays_AfterSLAdays to null for records that met SLA
            var records_met_sla = await context.Activities
                .Where(r => r.Met_SLA == "yes" && 
                           r.ExtraDays_AfterSLAdays != null)
                .ToListAsync();

            Console.WriteLine($"Found {records_met_sla.Count} records that met SLA but have ExtraDays_AfterSLAdays set");

            foreach (var record in records_met_sla)
            {
                record.ExtraDays_AfterSLAdays = null;
                Console.WriteLine($"Set ExtraDays_AfterSLAdays to null for activity {record.Id} (SLA met)");
            }

            // Also set ExtraDays_AfterSLAdays to null for records without Met_SLA or where Met_SLA is not "no"
            var records_clear_extra_days = await context.Activities
                .Where(r => r.ExtraDays_AfterSLAdays != null && 
                           r.Met_SLA != "no")
                .ToListAsync();

            Console.WriteLine($"Found {records_clear_extra_days.Count} records to clear ExtraDays_AfterSLAdays");

            foreach (var record in records_clear_extra_days)
            {
                record.ExtraDays_AfterSLAdays = null;
                Console.WriteLine($"Cleared ExtraDays_AfterSLAdays for activity {record.Id} (Met_SLA: '{record.Met_SLA}')");
            }
        }

        private async Task UpdateIsAssignedGroupResponsibleTeam()
        {
            // Extract: Get all records where AssignedGroup is not null and Is_AissignedGroup_ResponsibleTeam needs to be set
            var records_assigned_group = await context.Activities
                .Where(r => r.AssignedGroup != null)
                .ToListAsync();

            // Transform and Load: Set Is_AissignedGroup_ResponsibleTeam based on AssignedGroup
            foreach (var record in records_assigned_group)
            {
                // Remove spaces and convert to uppercase for comparison
                var assignedGroupUpper = record.AssignedGroup.Replace(" ", "").ToUpper();
                
                // Check if assigned group is "ML operation" (case insensitive)
                if (assignedGroupUpper == "MLOPERATION")
                {
                    record.Is_AissignedGroup_ResponsibleTeam = "yes";
                    Console.WriteLine($"Set Is_AissignedGroup_ResponsibleTeam to 'yes' for activity {record.Id} (AssignedGroup: {record.AssignedGroup})");
                }
                else
                {
                    record.Is_AissignedGroup_ResponsibleTeam = "no";
                    Console.WriteLine($"Set Is_AissignedGroup_ResponsibleTeam to 'no' for activity {record.Id} (AssignedGroup: {record.AssignedGroup})");
                }
            }
        }
    }
}