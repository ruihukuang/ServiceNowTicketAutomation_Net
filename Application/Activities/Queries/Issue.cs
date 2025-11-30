using System;
using System.Text.Json;
using Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Persistent;

namespace Application.Activities.Queries;

public class IssueByDate
{
    public class Query : IRequest<string>
    {
        public string? Year { get; set; }
        public string? Month { get; set; }
        public string? ServiceOwner { get; set; }
        public string? Issue { get; set; }
    }
    
    public class Handler : IRequestHandler<Query, string>
    {
        private readonly AppDbContext _context;

        public Handler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<string> Handle(Query request, CancellationToken cancellationToken)
        {
            var query = _context.Activities.AsQueryable();

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

            // Apply Issue filter if provided
            if (!string.IsNullOrEmpty(request.Issue))
            {
                query = query.Where(a => a.Issue == request.Issue);
                Console.WriteLine($"Applied Issue filter: {request.Issue}");
            }

            // Ensure valid year/month/ServiceOwner values
            query = query.Where(a =>
                !string.IsNullOrEmpty(a.OpenDate_Year) &&
                !string.IsNullOrEmpty(a.OpenDate_Month) &&
                !string.IsNullOrEmpty(a.ServiceOwner) &&
                !string.IsNullOrEmpty(a.Issue));

            // Get total record count after all filters
            var totalRecords = await query.CountAsync(cancellationToken);
            Console.WriteLine($"=== FILTER RESULTS ===");
            Console.WriteLine($"Total records after all filters: {totalRecords}");

            // Return early if no records found
            if (totalRecords == 0)
            {
                var message = "{\"All Issues\": 0}";
                Console.WriteLine($"RESULT: {message}");
                return message;
            }

            // Define issue categories in a maintainable way
            var issueCategories = new[]
            {
                new { Key = "Authentication & Authorization", SearchTerm = "authentication & authorization" },
                new { Key = "Network", SearchTerm = "network" },
                new { Key = "Functionality & Logic", SearchTerm = "functionality & logic" },
                new { Key = "Integration", SearchTerm = "integration" },
                new { Key = "Data Migration", SearchTerm = "data migration" },
                new { Key = "Client-Side", SearchTerm = "client-side" },
                new { Key = "Infrastructure & Resources", SearchTerm = "infrastructure & resources" }
            };

            // Create a dictionary to store results
            var results = new Dictionary<string, int>();

            // Count records for each issue category
            foreach (var category in issueCategories)
            {
                var count = await query
                    .Where(a => a.Issue != null && a.Issue.ToLower().Contains(category.SearchTerm))
                    .CountAsync(cancellationToken);
                
                results[category.Key] = count;
                Console.WriteLine($"Records with {category.Key}: {count}");
            }

            // FIX: Use JsonSerializer with options that don't escape HTML characters
            var jsonOptions = new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping // This prevents & from being escaped
            };
            
            var jsonResult = JsonSerializer.Serialize(results, jsonOptions);

            Console.WriteLine($"Issues distribution: {jsonResult}");
            Console.WriteLine($"=== END RESULTS ===");
            
            return jsonResult;
        }
    }
}