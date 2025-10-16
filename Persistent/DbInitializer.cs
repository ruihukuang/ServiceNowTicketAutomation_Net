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
                LongDescription="The user faced a "username/password" error on JupyterHub. Tried too clear browser cache, and  another Jupyterhub link but faced the same issue. restarted the PC, and tried a password reset. Logged in successfully but couldn't start the server. Referred to ML operation Chapter for further assistance.",
                Team_Fixed_Issue="ML operation",
                Team_Fixed_Issue_AI_Format = "",
                Team_Included_in_Ticket = "ML operation , SageMaker",
                Team_Included_in_Ticket_AI_Format = "",
                NumberTeam_Included_in_Ticket = "",
                NumberTeam_Fixed_Issue = "",
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

    

