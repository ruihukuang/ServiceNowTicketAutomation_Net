using System;
using Domain;

namespace Persistent;


public class DbInitializer
{
    
    public static async Task SeedData(AppDbContext context)
    {
        if (context.Activities.Any()) return;

        var activities = new List<Activity>
        {
            new() {
                IncidentNumber="IN5001",
                AssignedGroup= "SageMaker",
                LongDescription="Logged in with wrong password and user name in Jupyterhub",
                Team_Fixed_Issue="",
                Team_Fixed_Issue_AI_Format = "",
                Team_Included_in_Ticket = "",
                Team_Included_in_Ticket_AI_Format = "",
                NumberTeam_Included_in_Ticket = 2,
                NumberTeam_Fixed_Issue = 1,
                Is_AissignedGroup_ResponsibleTeam = "",
                Is_AssignedGroup_Fixed_Issue = "",
                Summary_Issue = "",
                Summary_Issue_AI = "",
                System = "",
                System_AI = "",
                Issue = "",
                Issue_AI = "",
                Root_Cause = "",
                Duplicate = "",
                Duplicate_AI = ""
            }

        };

        context.Activities.AddRange(activities);

        await context.SaveChangesAsync();

    }

}

    

