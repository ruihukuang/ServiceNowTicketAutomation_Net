using System;
using Domain;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Globalization;

namespace Persistent
{
    public class DbInitializer
    {
        public static async Task SeedData(AppDbContext context)
        {
            if (context.Activities.Any()) return;

            var activities = new List<Activity>
            {
                new Activity  {
                    IncidentNumber = "IN5001",
                    AssignedGroup = "SageMaker",
                    LongDescription = "The user faced a username/password error on JupyterHub. Tried too clear browser cache, and another Jupyterhub link but faced the same issue. restarted the PC, and tried a password reset. Logged in successfully but couldn't start the server. Referred to ML operation Chapter for further assistance.",
                    Team_Fixed_Issue = "ML operation",
                    Team_Included_in_Ticket = "ML operation , SageMaker",
                    ServiceOwner="Mark",
                    Priority="P4",
                    Guided_SLAdays=null,
                    Met_SLA=null,
                    ExtraDays_AfterSLAdays=null,
                    NumberTeam_Included_in_Ticket = 0,
                    NumberTeam_Fixed_Issue = 0,
                    Is_AissignedGroup_ResponsibleTeam = null,
                    Did_AssignedGroup_Fix_Issue = null,
                    Summary_Issue = null,
                    Summary_Issue_AI = null,
                    System = null,
                    System_AI = null,
                    Issue = null,
                    Issue_AI = null,
                    Root_Cause = null,
                    Root_Cause_AI = null,
                    Duplicate = null,
                    Duplicate_AI = null,
                    OpenDate = DateTime.Parse("1/05/2025 10:24:34 AM", new CultureInfo("en-AU")),
                    UpdatedDate = DateTime.Parse("13/05/2025 3:00:33 PM", new CultureInfo("en-AU")),
                    OpenDate_Year= null,
                    OpenDate_Month = null,
                    OpenDate_Day = null,
                    UpdatedDate_Year =null,
                    UpdatedDate_Month=null,
                    UpdatedDate_Day=null,

                },
                 new Activity  {
                    IncidentNumber = "IN5002",
                    AssignedGroup = "ML operation",
                    LongDescription = "A user reported persistent authentication failures when accessing JupyterHub. Initial username/password errors persisted through standard troubleshooting measures including browser cache clearance, alternative access methods, and system restart. Following a password reset, authentication succeeded but server startup failed",
                    Team_Fixed_Issue = "ML operation",
                    Team_Included_in_Ticket = "ML operation ",
                    ServiceOwner="Mark",
                    Priority="P4",
                    Guided_SLAdays=null,
                    Met_SLA=null,
                    ExtraDays_AfterSLAdays=null,
                    NumberTeam_Included_in_Ticket = 0,
                    NumberTeam_Fixed_Issue = 0,
                    Is_AissignedGroup_ResponsibleTeam = null,
                    Did_AssignedGroup_Fix_Issue = null,
                    Summary_Issue = null,
                    Summary_Issue_AI = null,
                    System = null,
                    System_AI = null,
                    Issue = null,
                    Issue_AI = null,
                    Root_Cause = null,
                    Root_Cause_AI = null,
                    Duplicate = null,
                    Duplicate_AI = null,
                    OpenDate = DateTime.Parse("16/05/2025 9:24:34 AM", new CultureInfo("en-AU")),
                    UpdatedDate = DateTime.Parse("31/05/2025 5:00:33 PM", new CultureInfo("en-AU")),
                    OpenDate_Year= null,
                    OpenDate_Month = null,
                    OpenDate_Day = null,
                    UpdatedDate_Year =null,
                    UpdatedDate_Month=null,
                    UpdatedDate_Day=null,

                }          
            };

            context.Activities.AddRange(activities);

            await context.SaveChangesAsync();
        }
    }
}
