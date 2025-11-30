using System;
using System.Text.Json;
using Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Persistent;

namespace Application.Activities.Queries;

public class DuplicatesByDate
{
    public class Query : IRequest<string>
    {
        public string? Year { get; set; }
        public string? Month { get; set; }
        public string? ServiceOwner { get; set; }
        public string? Duplicate { get; set; }
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

            // Apply Duplicate filter if provided
            if (!string.IsNullOrEmpty(request.Duplicate))
            {
                query = query.Where(a => a.Duplicate == request.Duplicate);
                Console.WriteLine($"Applied Duplicate filter: {request.Duplicate}");
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
                var message = "{\"duplicates\": 0}";
                Console.WriteLine($"RESULT: {message}");
                return message;
            }


            // Count distinct Duplicate values
           var distinctDuplicatesCount = await query
               .Where(a => a.Duplicate != null && a.Duplicate != "NO_DUPLICATE")  // ✅ Using logical AND (&&)
               .Select(a => a.Duplicate)
               .Distinct()
               .CountAsync(cancellationToken);
            
            Console.WriteLine($"Duplicate Records: {distinctDuplicatesCount}");

            // Use proper JSON serialization
            var resultData = new { duplicates = distinctDuplicatesCount };
            var jsonOptions = new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            var result = JsonSerializer.Serialize(resultData, jsonOptions);
            
            Console.WriteLine($"Duplicate distribution: {result}");
            Console.WriteLine($"=== END RESULTS ===");
            
            return result;
        }
    }
}