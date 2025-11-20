using System;
using Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Persistent;

namespace Application.Activities.Queries;

public class ExtraNonFuntionalTeamByDate
{
    public class Query : IRequest<string>
    {
        public string? Year { get; set; }
        public string? Month { get; set; }
        public string? ServiceOwner { get; set; }
        public int? NumberTeam_Included_in_Ticket { get; set; }
        public int? NumberTeam_Fixed_Issue { get; set; }
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

            // Apply NumberTeam_Included_in_Ticket filter if provided
            if (request.NumberTeam_Included_in_Ticket.HasValue)
            {
                query = query.Where(a => a.NumberTeam_Included_in_Ticket == request.NumberTeam_Included_in_Ticket.Value);
                Console.WriteLine($"Applied NumberTeam_Included_in_Ticket filter: {request.NumberTeam_Included_in_Ticket}");
            }
            
            // Apply NumberTeam_Fixed_Issue filter if provided
            if (request.NumberTeam_Fixed_Issue.HasValue)
            {
                query = query.Where(a => a.NumberTeam_Fixed_Issue == request.NumberTeam_Fixed_Issue.Value);
                Console.WriteLine($"Applied NumberTeam_Fixed_Issue filter: {request.NumberTeam_Fixed_Issue}");
            }

            // Ensure valid year/month/ServiceOwner values and that both team counts are not 0
            query = query.Where(a =>
                !string.IsNullOrEmpty(a.OpenDate_Year) &&
                !string.IsNullOrEmpty(a.OpenDate_Month) &&
                !string.IsNullOrEmpty(a.ServiceOwner) &&
                a.NumberTeam_Included_in_Ticket != 0 &&
                a.NumberTeam_Fixed_Issue != 0);

            // Get total record count after all filters
            var totalRecords = await query.CountAsync(cancellationToken);
            Console.WriteLine($"=== FILTER RESULTS ===");
            Console.WriteLine($"Total records after all filters: {totalRecords}");

            // Return early if no records found
            if (totalRecords == 0)
            {
                var message = "{\"extra_teams\": 0, \"no_extra_teams\": 0, \"message\": \"No records found matching the criteria\"}";
                Console.WriteLine($"RESULT: {message}");
                return message;
            }

            // Calculate the number of records with NumberTeam_Included_in_Ticket - NumberTeam_Fixed_Issue > 0 and = 0
            var recordExtraNonFunctionalTeamIncluded = await query
                .CountAsync(a => a.NumberTeam_Included_in_Ticket - a.NumberTeam_Fixed_Issue > 0, cancellationToken);
                
            var recordNoExtraNonFunctionalTeamIncluded = await query
                .CountAsync(a => a.NumberTeam_Included_in_Ticket - a.NumberTeam_Fixed_Issue == 0, cancellationToken);
            
            // Dictionary/JSON format
            var result = $"{{\"extra_teams\": {recordExtraNonFunctionalTeamIncluded}, \"no_extra_teams\": {recordNoExtraNonFunctionalTeamIncluded}}}";
            
            Console.WriteLine($"Records with extra non-functional teams: {recordExtraNonFunctionalTeamIncluded}");
            Console.WriteLine($"Records with no extra non-functional teams: {recordNoExtraNonFunctionalTeamIncluded}");
            Console.WriteLine($"Result JSON: {result}");
            Console.WriteLine($"=== END RESULTS ===");
            
            return result;
        }
    }
}