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
        
        // NEW: Get activities by specific year and/or month
        [HttpGet("byDate")]
        public async Task<ActionResult<List<Activity>>> GetActivitiesByDate([FromQuery] string? year, [FromQuery] string? month)
        {
            return await Mediator.Send(new GetActivityListByDate.Query 
            { 
                Year = year, 
                Month = month 
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