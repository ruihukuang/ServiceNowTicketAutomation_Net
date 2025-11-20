using System;
using Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Persistent;

namespace Application.Activities.Queries;

public class AssignedTeamResponsibleByDate
{
    public class Query : IRequest<string>
    {
        public string? Year { get; set; }
        public string? Month { get; set; }
        public string? ServiceOwner { get; set; }
        public string? Is_AissignedGroup_ResponsibleTeam { get; set; }  // Keep original spelling
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
                var message = "{\"responsible_aissigned_team\": 0, \"non_responsible_aissigned_team\": 0, \"message\": \"No records found matching the criteria\"}";
                Console.WriteLine($"RESULT: {message}");
                return message;
            }

            // Count records where Is_AissignedGroup_ResponsibleTeam equals "yes" and "no"
            var aissignedTeamResponsibleRecords = await query.Where(a => a.Is_AissignedGroup_ResponsibleTeam == "yes").CountAsync(cancellationToken);
            var aissignedTeamNotResponsibleRecords = await query.Where(a => a.Is_AissignedGroup_ResponsibleTeam == "no").CountAsync(cancellationToken);
            
            Console.WriteLine($"Records with responsible aissigned team (Is_AissignedGroup_ResponsibleTeam = 'yes'): {aissignedTeamResponsibleRecords}");
            Console.WriteLine($"Records with non responsible aissigned team (Is_AissignedGroup_ResponsibleTeam = 'no'): {aissignedTeamNotResponsibleRecords}");

            // Dictionary/JSON format - using consistent spelling with property name
            var result = $"{{\"responsible_aissigned_team\": {aissignedTeamResponsibleRecords}, \"non_responsible_aissigned_team\": {aissignedTeamNotResponsibleRecords}}}";
            
            Console.WriteLine($"Aissigned team responsibility distribution: {result}");
            Console.WriteLine($"=== END RESULTS ===");
            
            return result;
        }
    }
}