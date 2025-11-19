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
    public class GetActivityDuplicate 
    {
        public class Query : IRequest<List<Activity>>
        {
            // No parameters needed since we're getting all duplicates
        }
        
        public class Handler(AppDbContext context) : IRequestHandler<Query, List<Activity>>
        {
            public async Task<List<Activity>> Handle(Query request, CancellationToken cancellationToken)
            {
                // Find all activities where Duplicate is not null and not "NO_DUPLICATE"
                var activitiesWithDuplicates = await context.Activities
                    .Where(a => a.Duplicate != null && 
                               a.Duplicate != "NO_DUPLICATE" &&
                               !string.IsNullOrWhiteSpace(a.Duplicate))
                    .ToListAsync(cancellationToken);

                if (!activitiesWithDuplicates.Any()) 
                    throw new Exception("No activities with duplicate values found");

                // Group by Duplicate value and order by the Duplicate value
                var groupedActivities = activitiesWithDuplicates
                    .OrderBy(a => a.Duplicate) // Order by duplicate group
                    .ThenBy(a => a.IncidentNumber) // Then order by incident number within each group
                    .ToList();

                return groupedActivities;
            }
        }
    }
}