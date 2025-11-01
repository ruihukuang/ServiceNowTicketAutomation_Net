using System;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Persistent;
using Domain;
using System.Collections.Generic;

namespace Application.Activities.Queries
{
    public class GetActivityIncidentNumberDetails
    {
        public class Query : IRequest<List<Activity>>
        {
            public required string IncidentNumber { get; set; }
        }
        
        public class Handler : IRequestHandler<Query, List<Activity>>
        {
            private readonly AppDbContext _context;

            public Handler(AppDbContext context)
            {
                _context = context;
            }

            public async Task<List<Activity>> Handle(Query request, CancellationToken cancellationToken)
            {
                if (string.IsNullOrWhiteSpace(request.IncidentNumber))
                    throw new ArgumentException("Incident number cannot be null or empty");

                var trimmedIncidentNumber = request.IncidentNumber.Trim();

                var activities = await _context.Activities
                    .Where(a => a.IncidentNumber == trimmedIncidentNumber)
                    .ToListAsync(cancellationToken);

                if (activities == null || activities.Count == 0) 
                    throw new Exception($"No activities found with IncidentNumber '{trimmedIncidentNumber}'");

                return activities;
            }
        }
    }
}