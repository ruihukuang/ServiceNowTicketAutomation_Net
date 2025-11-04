using System;
using Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Persistent;

namespace Application.Activities.Queries;

public class GetActivityListByDate
{
    public class Query : IRequest<List<Activity>>
    {
        public string? Year { get; set; }
        public string? Month { get; set; }
    }
    
    public class Handler(AppDbContext context) : IRequestHandler<Query, List<Activity>>
    {
        public async Task<List<Activity>> Handle(Query request, CancellationToken cancellationToken)
        {
            var query = context.Activities.AsQueryable();

            // Filter by year if provided
            if (!string.IsNullOrEmpty(request.Year))
            {
                query = query.Where(a => a.OpenDate_Year == request.Year);
            }

            // Filter by month if provided
            if (!string.IsNullOrEmpty(request.Month))
            {
                query = query.Where(a => a.OpenDate_Month == request.Month);
            }

            // Ensure both year and month are specified (not null or empty)
            query = query.Where(a => 
                !string.IsNullOrEmpty(a.OpenDate_Year) && 
                !string.IsNullOrEmpty(a.OpenDate_Month));

            return await query.ToListAsync(cancellationToken);
        }
    }
}