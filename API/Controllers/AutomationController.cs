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
            // Check if fields are null before processing
            var hasGuidedSLADays = await HasNullGuidedSLADays();
            var hasMetSLA = await HasNullMetSLA();
            var hasExtraDaysAfterSLA = await HasNullExtraDaysAfterSLA();
            var hasIsAssignedGroupResponsibleTeam = await HasNullIsAssignedGroupResponsibleTeam();

            Console.WriteLine($"Field status - Guided_SLAdays: {(hasGuidedSLADays ? "has nulls" : "all set")}");
            Console.WriteLine($"Field status - Met_SLA: {(hasMetSLA ? "has nulls" : "all set")}");
            Console.WriteLine($"Field status - ExtraDays_AfterSLAdays: {(hasExtraDaysAfterSLA ? "has nulls" : "all set")}");
            Console.WriteLine($"Field status - Is_AissignedGroup_ResponsibleTeam: {(hasIsAssignedGroupResponsibleTeam ? "has nulls" : "all set")}");

            // Process fields in the correct order only if they have null values
            if (hasGuidedSLADays)
            {
                await UpdateGuidedSLADays();
            }

            if (hasMetSLA)
            {
                await UpdateMetSLA(); // Must complete before UpdateExtraDaysAfterSLA
            }

            if (hasExtraDaysAfterSLA)
            {
                await UpdateExtraDaysAfterSLA(); // Depends on Met_SLA being populated
            }

            if (hasIsAssignedGroupResponsibleTeam)
            {
                await UpdateIsAssignedGroupResponsibleTeam();
            }

            // Save changes to the database
            await context.SaveChangesAsync();

            return Ok("Further data processing completed with null checks.");
        }

        private async Task<bool> HasNullGuidedSLADays()
        {
            return await context.Activities
                .AnyAsync(r => r.Guided_SLAdays == null && 
                              r.Priority != null && 
                              r.Priority.ToUpper() == "P4");
        }

        private async Task<bool> HasNullMetSLA()
        {
            return await context.Activities
                .AnyAsync(r => r.Met_SLA == null && 
                              r.OpenDate.HasValue && 
                              r.UpdatedDate.HasValue && 
                              (r.Guided_SLAdays != null || r.Priority != null));
        }

        private async Task<bool> HasNullExtraDaysAfterSLA()
        {
            // CHANGED: Now also check for records that should have 0 instead of null
            return await context.Activities
                .AnyAsync(r => (r.ExtraDays_AfterSLAdays == null && r.Met_SLA == "no") ||
                              (r.ExtraDays_AfterSLAdays != null && r.Met_SLA != "no") ||
                              (r.ExtraDays_AfterSLAdays == null && (r.Met_SLA == "yes" || r.Met_SLA == null)));
        }

        private async Task<bool> HasNullIsAssignedGroupResponsibleTeam()
        {
            return await context.Activities
                .AnyAsync(r => r.Is_AissignedGroup_ResponsibleTeam == null && 
                              r.AssignedGroup != null);
        }

        private async Task UpdateGuidedSLADays()
        {
            Console.WriteLine("Starting UpdateGuidedSLADays...");
            
            // Extract: Get all records where Priority is P4 and Guided_SLAdays is null
            var records_p4_priority = await context.Activities
                .Where(r => r.Priority != null && 
                           r.Priority.ToUpper() == "P4" && 
                           r.Guided_SLAdays == null)
                .ToListAsync();

            Console.WriteLine($"Found {records_p4_priority.Count} P4 records with null Guided_SLAdays");

            // Transform and Load: Set Guided_SLAdays to 5 for P4 priority records
            foreach (var record in records_p4_priority)
            {
                record.Guided_SLAdays = 5;
                Console.WriteLine($"Set Guided_SLAdays to 5 for activity {record.Id} with Priority P4");
            }
        }

        private async Task UpdateMetSLA()
        {
            Console.WriteLine("Starting UpdateMetSLA...");
            
            // Extract: Get all records where Met_SLA is null and we can calculate it
            var records_to_check_sla = await context.Activities
                .Where(r => r.Met_SLA == null && 
                           r.OpenDate.HasValue && 
                           r.UpdatedDate.HasValue && 
                           (r.Guided_SLAdays != null || r.Priority != null))
                .ToListAsync();

            Console.WriteLine($"Found {records_to_check_sla.Count} records with null Met_SLA that can be calculated");

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
            Console.WriteLine("Starting UpdateExtraDaysAfterSLA...");
            
            // First, save changes to ensure Met_SLA is persisted
            await context.SaveChangesAsync();
            Console.WriteLine("Saved changes to ensure Met_SLA values are persisted");

            // Extract: Get records that need ExtraDays_AfterSLAdays calculation (null when Met_SLA is "no")
            var records_exceeded_sla = await context.Activities
                .Where(r => r.Met_SLA == "no" && 
                           r.ExtraDays_AfterSLAdays == null &&
                           r.OpenDate.HasValue && 
                           r.UpdatedDate.HasValue && 
                           r.Guided_SLAdays.HasValue &&
                           r.Guided_SLAdays > 0)
                .ToListAsync();

            Console.WriteLine($"Found {records_exceeded_sla.Count} records that exceeded SLA with null ExtraDays_AfterSLAdays");

            // Transform and Load: Calculate extra days after SLA
            foreach (var record in records_exceeded_sla)
            {
                var daysToResolve = (record.UpdatedDate.Value - record.OpenDate.Value).TotalDays;
                record.ExtraDays_AfterSLAdays = Convert.ToInt64(daysToResolve - record.Guided_SLAdays);
                Console.WriteLine($"Set ExtraDays_AfterSLAdays to {record.ExtraDays_AfterSLAdays} for activity {record.Id}");
            }

            // CHANGED: Set ExtraDays_AfterSLAdays to 0 for records that met SLA or don't have "no"
            var records_met_sla_or_other = await context.Activities
                .Where(r => (r.Met_SLA == "yes" || r.Met_SLA == null || r.Met_SLA != "no") && 
                           r.ExtraDays_AfterSLAdays != 0) // Only update if not already 0
                .ToListAsync();

            Console.WriteLine($"Found {records_met_sla_or_other.Count} records that met SLA or other status to set ExtraDays_AfterSLAdays to 0");

            foreach (var record in records_met_sla_or_other)
            {
                // BEFORE: record.ExtraDays_AfterSLAdays = null;
                // AFTER: Set to 0 instead of null
                record.ExtraDays_AfterSLAdays = 0;
                Console.WriteLine($"Set ExtraDays_AfterSLAdays to 0 for activity {record.Id} (Met_SLA: '{record.Met_SLA}')");
            }

            // CHANGED: Also set ExtraDays_AfterSLAdays to 0 for records without any SLA calculation but have null
            var records_no_sla_calculation = await context.Activities
                .Where(r => r.ExtraDays_AfterSLAdays == null && 
                           (r.Met_SLA == null || r.Met_SLA != "no"))
                .ToListAsync();

            Console.WriteLine($"Found {records_no_sla_calculation.Count} records without SLA calculation to set ExtraDays_AfterSLAdays to 0");

            foreach (var record in records_no_sla_calculation)
            {
                // BEFORE: Would remain null
                // AFTER: Set to 0
                record.ExtraDays_AfterSLAdays = 0;
                Console.WriteLine($"Set ExtraDays_AfterSLAdays to 0 for activity {record.Id} (no SLA exceeded)");
            }
        }

        private async Task UpdateIsAssignedGroupResponsibleTeam()
        {
            Console.WriteLine("Starting UpdateIsAssignedGroupResponsibleTeam...");
            
            // Extract: Get all records where AssignedGroup is not null and Is_AissignedGroup_ResponsibleTeam is null
            var records_assigned_group = await context.Activities
                .Where(r => r.AssignedGroup != null && 
                           r.Is_AissignedGroup_ResponsibleTeam == null)
                .ToListAsync();

            Console.WriteLine($"Found {records_assigned_group.Count} records with AssignedGroup but null Is_AissignedGroup_ResponsibleTeam");

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