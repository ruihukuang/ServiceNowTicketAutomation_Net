using System;
using Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Persistent;

namespace Application.Activities.Queries;

public class GetActivityListNeedProcess
{
    public class Query : IRequest<List<Activity>> { }
    
    public class Handler(AppDbContext context) : IRequestHandler<Query, List<Activity>>
    {
        public async Task<List<Activity>> Handle(Query request, CancellationToken cancellationToken)
        {
            return await context.Activities
                .Where(a => 
                    a.Guided_SLAdays == null || 
                    a.Met_SLA == null || 
                    a.ExtraDays_AfterSLAdays == null || 
                    a.Is_AissignedGroup_ResponsibleTeam == null || 
                    a.Did_AssignedGroup_Fix_Issue == null || 
                    a.OpenDate_Year == null || 
                    a.OpenDate_Month == null || 
                    a.OpenDate_Day == null || 
                    a.UpdatedDate_Year == null || 
                    a.UpdatedDate_Month == null || 
                    a.UpdatedDate_Day == null || 
                    a.NumberTeam_Included_in_Ticket == 0 || 
                    a.NumberTeam_Fixed_Issue == 0)
                .ToListAsync(cancellationToken);
        }
    }
}