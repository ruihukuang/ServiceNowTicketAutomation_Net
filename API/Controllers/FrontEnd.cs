using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using Application.Activities.Queries;
using MediatR;
using System.Collections.Generic; 
using Persistent;
using Domain;
using Application.Activities.Commands;

namespace API.Controllers
{
    public class FrontEndController : BaseApiController
    {
        [HttpGet]
        public async Task<ActionResult<List<Activity>>> GetActivities()
        {
            return await Mediator.Send(new GetActivityList.Query());
        }

        [HttpGet("NeedProcessList")]
        public async Task<ActionResult<List<Activity>>> GetActiviGetActivityListNeedProcess()
        {
            return await Mediator.Send(new GetActivityListNeedProcess.Query());
        }

        [HttpGet("ReviewList")]
        public async Task<ActionResult<List<Activity>>> GetActivityListReview()
        {
            return await Mediator.Send(new GetActivityListReview.Query());
        }
        
        // Get activities by specific year and/or month
        [HttpGet("byDate")]
        public async Task<ActionResult<List<Activity>>> GetActivitiesByDate([FromQuery] string? year, [FromQuery] string? month)
        {
            return await Mediator.Send(new GetActivityListByDate.Query 
            { 
                Year = year, 
                Month = month 
            });
        }


        // Get activities by specific year and/or month and Service Owner for Met SLA
        [HttpGet("byDateServiceOwnerMetSLA")]
        public async Task<ActionResult<string>> MetSLAByDate([FromQuery] string? year, [FromQuery] string? month, [FromQuery] string? ServiceOwner)
        {
            return await Mediator.Send(new MetSLAByDate.Query
            {
                Year = year,
                Month = month,
                ServiceOwner = ServiceOwner
            });
        }
        
        
        // Get activities by specific year and/or month and Service Owner for Extra Days after SLA
        [HttpGet("byDateServiceOwnerExtraDaysAfterSLA")]
        public async Task<ActionResult<string>> ExtraDaysAfterSLAByDate([FromQuery] string? year, [FromQuery] string? month ,[FromQuery] string? ServiceOwner  )
        {
            return await Mediator.Send(new ExtraDaysAfterSLAByDate.Query
            {
                Year = year,
                Month = month,
                ServiceOwner =ServiceOwner 
            });
        }

        // Get activities by specific year and/or month and Service Owner for priority
        [HttpGet("byDateServiceOwnerPriority")]
        public async Task<ActionResult<string>> PriorityByDate([FromQuery] string? year, [FromQuery] string? month ,[FromQuery] string? ServiceOwner  )
        {
            return await Mediator.Send(new PriorityByDate.Query
            {
                Year = year,
                Month = month,
                ServiceOwner =ServiceOwner 
            });
        }

        // Get activities by specific year and/or month and Service Owner for priority
        [HttpGet("byDateServiceOwnerExtraNonFuntionalTeam")]
        public async Task<ActionResult<string>> ExtraNonFuntionalTeamByDate([FromQuery] string? year, [FromQuery] string? month ,[FromQuery] string? ServiceOwner  )
        {
            return await Mediator.Send(new ExtraNonFuntionalTeamByDate.Query
            {
                Year = year,
                Month = month,
                ServiceOwner =ServiceOwner 
            });
        }

        // Get activities by specific year and/or month and Service Owner for checking responsibility of assigned team
        [HttpGet("byDateServiceOwnerAssignedTeamResponsible")]
        public async Task<ActionResult<string>> AssignedTeamResponsibleByDate([FromQuery] string? year, [FromQuery] string? month, [FromQuery] string? ServiceOwner)
        {
            return await Mediator.Send(new AssignedTeamResponsibleByDate.Query
            {
                Year = year,
                Month = month,
                ServiceOwner = ServiceOwner
            });
        }


        // Get activities by specific year and/or month and Service Owner for assigned team fixing issues
        [HttpGet("byDateServiceOwnerAssignedTeamFixingIssues")]
        public async Task<ActionResult<string>> AssignedTeamFixingIsssueByDate([FromQuery] string? year, [FromQuery] string? month, [FromQuery] string? ServiceOwner)
        {
            return await Mediator.Send(new AssignedTeamFixingIsssueByDate.Query
            {
                Year = year,
                Month = month,
                ServiceOwner = ServiceOwner
            });
        }

                // Get activities by specific year and/or month and Service Owner for assigned team fixing issues
        [HttpGet("byDateServiceOwnerSystem")]
        public async Task<ActionResult<string>> SystemByDate([FromQuery] string? year, [FromQuery] string? month, [FromQuery] string? ServiceOwner)
        {
            return await Mediator.Send(new SystemByDate.Query
            {
                Year = year,
                Month = month,
                ServiceOwner = ServiceOwner
            });
        }
        
        [HttpGet("id/{id}")]
        public async Task<ActionResult<Activity>> GetActivitiesDetails(string id)
        {
            return await Mediator.Send(new GetActivityDetail.Query { Id = id });
        }

        [HttpGet("incident/{incidentNumber}")]
        public async Task<ActionResult<string>> GetActivityIncidentNumber(string incidentNumber)
        {
            return await Mediator.Send(new GetActivityIncidentNumber.Query { IncidentNumber = incidentNumber });
        }

        [HttpGet("incident/details/{incidentNumber}")]
        public async Task<ActionResult<List<Activity>>> GetActivityIncidentNumberDetails(string incidentNumber)
        {
            return await Mediator.Send(new GetActivityIncidentNumberDetails.Query { IncidentNumber = incidentNumber });
        }

        [HttpGet("duplicatelist")]
        public async Task<ActionResult<List<Activity>>> GetActivityDuplicate()
        {
            return await Mediator.Send(new GetActivityDuplicate.Query());
        }


        [HttpGet("duplicatelistAI")]
        public async Task<ActionResult<List<Activity>>> GetActivityDuplicateAI()
        {
            return await Mediator.Send(new GetActivityDuplicateAI.Query());
        }
        
        [HttpPost]
        public async Task<ActionResult<string>> CreateActivity(Activity activity)
        {
            return await Mediator.Send(new CreateActivity.Command { Activity = activity });
        }

        [HttpPut]
        public async Task<ActionResult<string>> EditActivity(Activity activity)
        {
            return await Mediator.Send(new EditActivity.Command { Activity = activity });
        }
        
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteActivity(string id)
        {
            await Mediator.Send(new DeleteActivity.Command { Id = id });

            return Ok();
        }
    }
}