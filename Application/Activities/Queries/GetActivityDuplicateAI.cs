using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Persistent;
using Domain;

namespace Application.Activities.Queries
{
    public class GetActivityDuplicateAI
    {
        public class Query : IRequest<List<Activity>>
        {
            // No parameters needed since we're getting all duplicates AI
        }
        
        public class Handler(AppDbContext context) : IRequestHandler<Query, List<Activity>>
        {
            public async Task<List<Activity>> Handle(Query request, CancellationToken cancellationToken)
            {
                // Find all activities where Duplicate_AI is not null and not "NO_DUPLICATE"
                var activitiesWithDuplicatesAI = await context.Activities
                    .Where(a => a.Duplicate_AI != null && 
                               a.Duplicate_AI != "NO_DUPLICATE" &&
                               !string.IsNullOrWhiteSpace(a.Duplicate_AI))
                    .ToListAsync(cancellationToken);

                if (!activitiesWithDuplicatesAI.Any()) 
                    throw new Exception("No activities with duplicate AI values found");

                // Group by Duplicate AI value and order by the Duplicate AI value
                var groupedActivities = activitiesWithDuplicatesAI
                    .OrderBy(a => a.Duplicate_AI) // Order by duplicate group
                    .ThenBy(a => a.IncidentNumber) // Then order by incident number within each group
                    .ToList();

                return groupedActivities;
            }
        }
    }
}