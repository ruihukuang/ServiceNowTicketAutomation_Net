using System;
using Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Persistent;

namespace Application.Activities.Queries;

public class SystemByDate
{
    public class Query : IRequest<string>
    {
        public string? Year { get; set; }
        public string? Month { get; set; }
        public string? ServiceOwner { get; set; }
        public string? System { get; set; }
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

            // Apply System filter if provided
            if (!string.IsNullOrEmpty(request.System))
            {
                query = query.Where(a => a.System == request.System);
                Console.WriteLine($"Applied System filter: {request.System}");  // Fixed: request.System
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
                var message = "{\"Jupyterhub\": 0, \"Zeppelin\": 0, \"message\": \"No records found matching the criteria\"}";
                Console.WriteLine($"RESULT: {message}");
                return message;
            }

            // Count records for each system
            var jupyterhubRecords = await query.Where(a => a.System == "JupyterHub").CountAsync(cancellationToken);  // Fixed spacing and casing
            var zeppelinRecords = await query.Where(a => a.System == "Zeppelin").CountAsync(cancellationToken);      // Fixed spacing and casing
            
            Console.WriteLine($"Records with Jupyterhub (System = 'Jupyterhub'): {jupyterhubRecords}");
            Console.WriteLine($"Records with Zeppelin (System = 'Zeppelin'): {zeppelinRecords}");

            // Dictionary/JSON format
            var result = $"{{\"Jupyterhub\": {jupyterhubRecords}, \"Zeppelin\": {zeppelinRecords}}}";
            
            Console.WriteLine($"System distribution: {result}");
            Console.WriteLine($"=== END RESULTS ===");
            
            return result;
        }
    }
}