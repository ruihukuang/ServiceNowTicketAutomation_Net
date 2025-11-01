using System;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Persistent;
using Domain;

namespace Application.Activities.Queries
{
    public class GetActivityIncidentNumber
    {
        public class Query : IRequest<string>
        {
            public required string IncidentNumber { get; set; }
        }
        
        public class Handler(AppDbContext context) : IRequestHandler<Query, string>
        {
            public async Task<string> Handle(Query request, CancellationToken cancellationToken)
            {
                // Use FirstOrDefaultAsync to find by IncidentNumber and return only the ID
                var activityId = await context.Activities
                    .Where(a => a.IncidentNumber == request.IncidentNumber)
                    .Select(a => a.Id)
                    .FirstOrDefaultAsync(cancellationToken);

                if (string.IsNullOrEmpty(activityId)) 
                    throw new Exception($"Activity with IncidentNumber '{request.IncidentNumber}' not found");

                return activityId;
            }
        }
    }
}