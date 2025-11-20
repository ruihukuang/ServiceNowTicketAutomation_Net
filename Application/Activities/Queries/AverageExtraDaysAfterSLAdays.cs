using System;
using Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Persistent;

namespace Application.Activities.Queries;

public class ExtraDaysAfterSLAByDate
{
    public class Query : IRequest<string>
    {
        public string? Year { get; set; }
        public string? Month { get; set; }
        public string? ServiceOwner { get; set; }
        public long? ExtraDays_AfterSLAdays { get; set; }
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

            // Apply ExtraDays_AfterSLAdays filter if provided
            if (request.ExtraDays_AfterSLAdays.HasValue)
            {
                query = query.Where(a => a.ExtraDays_AfterSLAdays == request.ExtraDays_AfterSLAdays.Value);
                Console.WriteLine($"Applied ExtraDays_AfterSLAdays filter: {request.ExtraDays_AfterSLAdays} days");
            }

            // Ensure valid year/month/ServiceOwner values and that ExtraDays_AfterSLAdays is not null and not 0
            query = query.Where(a =>
                !string.IsNullOrEmpty(a.OpenDate_Year) &&
                !string.IsNullOrEmpty(a.OpenDate_Month) &&
                !string.IsNullOrEmpty(a.ServiceOwner) &&
                a.ExtraDays_AfterSLAdays.HasValue &&
                a.ExtraDays_AfterSLAdays.Value != 0);

            // Get total record count after all filters
            var totalRecords = await query.CountAsync(cancellationToken);
            Console.WriteLine($"=== FILTER RESULTS ===");
            Console.WriteLine($"Total records after all filters: {totalRecords}");

            // Return early if no records found
            if (totalRecords == 0)
            {
                var message = "0 days (No records found matching the criteria)";
                Console.WriteLine($"RESULT: {message}");
                return message;
            }

            // Calculate average of ExtraDays_AfterSLAdays (handle nullable long)
            var averageExtraDays = await query
                .AverageAsync(a => (double)a.ExtraDays_AfterSLAdays!.Value, cancellationToken);
            var result = averageExtraDays.ToString("F2") + " days";
            
            Console.WriteLine($"Average extra days after SLA: {result}");
            Console.WriteLine($"=== END RESULTS ===");
            
            return result;
        }
    }
}