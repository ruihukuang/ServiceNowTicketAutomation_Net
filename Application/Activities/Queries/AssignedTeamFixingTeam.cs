using System;
using Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Persistent;

namespace Application.Activities.Queries;

public class AssignedTeamFixingIsssueByDate
{
    public class Query : IRequest<string>
    {
        public string? Year { get; set; }
        public string? Month { get; set; }
        public string? ServiceOwner { get; set; }
        public string? Did_AssignedGroup_Fix_Issue { get; set; }
    }
    
    public class Handler(AppDbContext context) : IRequestHandler<Query, string>
    {
        public async Task<string> Handle(Query request, CancellationToken cancellationToken)
        {
            var query = context.Activities.AsQueryable();

            // Apply year filter if provided
            if (!string.IsNullOrEmpty(request.Year))
            {
                query = query.Where(a => a.OpenDate_Year == request.Year);
                Console.WriteLine($"Applied year filter: {request.Year}");
            }

            // Apply month filter if provided
            if (!string.IsNullOrEmpty(request.Month))
            {
                query = query.Where(a => a.OpenDate_Month == request.Month);
                Console.WriteLine($"Applied month filter: {request.Month}");
            }

            // Apply ServiceOwner filter if provided
            if (!string.IsNullOrEmpty(request.ServiceOwner))
            {
                query = query.Where(a => a.ServiceOwner == request.ServiceOwner);
                Console.WriteLine($"Applied ServiceOwner filter: {request.ServiceOwner}");
            }

            // Apply Did_AssignedGroup_Fix_Issue filter if provided
            if (!string.IsNullOrEmpty(request.Did_AssignedGroup_Fix_Issue))
            {
                query = query.Where(a => a.Did_AssignedGroup_Fix_Issue == request.Did_AssignedGroup_Fix_Issue);
                Console.WriteLine($"Applied Did_AssignedGroup_Fix_Issue filter: {request.Did_AssignedGroup_Fix_Issue}");
            }

            // Ensure valid year/month/ServiceOwner values
            query = query.Where(a =>
                !string.IsNullOrEmpty(a.OpenDate_Year) &&
                !string.IsNullOrEmpty(a.OpenDate_Month) &&
                !string.IsNullOrEmpty(a.ServiceOwner));

            // Get total record count after all filters
            var totalRecords = await query.CountAsync(cancellationToken);
            Console.WriteLine($"=== FILTER RESULTS ===");
            Console.WriteLine($"Total records after all filters: {totalRecords}");

            // Return early if no records found
            if (totalRecords == 0)
            {
                var message = "{\"fixing_issues_assigned_team\": 0, \"non_fixing_issues_assigned_team\": 0, \"message\": \"No records found matching the criteria\"}";
                Console.WriteLine($"RESULT: {message}");
                return message;
            }

            // Count records where Did_AssignedGroup_Fix_Issue equals "Y" and "N"
            var assignedTeamFixingRecords = await query.Where(a => a.Did_AssignedGroup_Fix_Issue == "Y").CountAsync(cancellationToken);
            var assignedTeamNotFixingRecords = await query.Where(a => a.Did_AssignedGroup_Fix_Issue == "N").CountAsync(cancellationToken);
            
            Console.WriteLine($"Records with fixing issues assigned team (Did_AssignedGroup_Fix_Issue = 'Y'): {assignedTeamFixingRecords}");
            Console.WriteLine($"Records with non fixing issues assigned team (Did_AssignedGroup_Fix_Issue = 'N'): {assignedTeamNotFixingRecords}");

            // Dictionary/JSON format
            var result = $"{{\"fixing_issues_assigned_team\": {assignedTeamFixingRecords}, \"non_fixing_issues_assigned_team\": {assignedTeamNotFixingRecords}}}";
            
            Console.WriteLine($"Assigned team fixing issues distribution: {result}");
            Console.WriteLine($"=== END RESULTS ===");
            
            return result;
        }
    }
}