using System;
using Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Persistent;

namespace Application.Activities.Queries;

public class PriorityByDate
{
    public class Query : IRequest<string>
    {
        public string? Year { get; set; }
        public string? Month { get; set; }
        public string? ServiceOwner { get; set; }
        public string? Met_SLA { get; set; }
    }
    
    public class Handler(AppDbContext context) : IRequestHandler<Query, string>
    {
        public async Task<string> Handle(Query request, CancellationToken cancellationToken)
        {
            var query = context.Activities.AsQueryable();

            // Apply filters...
            if (!string.IsNullOrEmpty(request.Year))
                query = query.Where(a => a.OpenDate_Year == request.Year);

            if (!string.IsNullOrEmpty(request.Month))
                query = query.Where(a => a.OpenDate_Month == request.Month);

            if (!string.IsNullOrEmpty(request.ServiceOwner))
                query = query.Where(a => a.ServiceOwner == request.ServiceOwner);

            query = query.Where(a =>
                !string.IsNullOrEmpty(a.OpenDate_Year) &&
                !string.IsNullOrEmpty(a.OpenDate_Month) &&
                !string.IsNullOrEmpty(a.ServiceOwner));

            var totalRecords = await query.CountAsync(cancellationToken);
            
            if (totalRecords == 0)
                return "[]"; // Empty array

            // Count records for each priority
            var p1Records = await query.Where(a => a.Priority == "P1").CountAsync(cancellationToken);
            var p2Records = await query.Where(a => a.Priority == "P2").CountAsync(cancellationToken);
            var p3Records = await query.Where(a => a.Priority == "P3").CountAsync(cancellationToken);
            var p4Records = await query.Where(a => a.Priority == "P4").CountAsync(cancellationToken);

            // Array format - returns [P1_count, P2_count, P3_count, P4_count]
            var result = $"[{p1Records}, {p2Records}, {p3Records}, {p4Records}]";
            
            return result;
        }
    }
}