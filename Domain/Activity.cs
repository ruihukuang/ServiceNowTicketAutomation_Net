using System;

namespace Domain
{
    public class Activity
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public required string IncidentNumber { get; set; }

        public required string AssignedGroup { get; set; }

        public required string LongDescription { get; set; }

        public string? Team_Fixed_Issue { get; set; }

        public string? Team_Fixed_Issue_AI_Format { get; set; }

        public string? Team_Included_in_Ticket { get; set; }

        public string? Team_Included_in_Ticket_AI_Format { get; set; }

        public int NumberTeam_Included_in_Ticket { get; set; }
        
        public int NumberTeam_Fixed_Issue { get; set; }

        public string? Is_AissignedGroup_ResponsibleTeam { get; set; }

        public string? Is_AssignedGroup_Fixed_Issue { get; set; }

        public string? Summary_Issue { get; set; }

        public string? Summary_Issue_AI { get; set; }

        public string? System { get; set; }

        public string? System_AI { get; set; }

        public string? Issue { get; set; }

        public string? Issue_AI { get; set; }

        public string? Root_Cause { get; set; }

        public string? Duplicate { get; set; }

        public string? Duplicate_AI { get; set; }

        // New DateTime properties
        public DateTime OpenDate { get; set; }
        public DateTime UpdatedDate { get; set; }

        // New string properties for date components
        public string? OpenDate_Year { get; set; }
        public string? OpenDate_Month { get; set; }
        public string? OpenDate_Day { get; set; }

        public string? UpdatedDate_Year { get; set; }
        public string? UpdatedDate_Month { get; set; }
        public string? UpdatedDate_Day { get; set; }
    }
}
