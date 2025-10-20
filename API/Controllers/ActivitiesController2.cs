using System;
using Persistent;
using Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;

namespace API.Controllers
{
    public class ActivitiesController2 : BaseApiController
    {
        private readonly AppDbContext context;

        public ActivitiesController(AppDbContext context)
        {
            this.context = context;
        }

        [HttpPost]
        public async Task<IActionResult> SendDataToApi()
        {
            using var httpClient = new HttpClient();
            var tasks = new List<Task>();

            // Dictionary to store responses
            var responses = new Dictionary<string, string>();

            // Retrieve LongDescriptions from the database
            var longDescriptions = await context.Activities
                .Where(a => /* Add your criteria here, e.g., a.SomeColumn == someValue */)
                .Select(a => a.LongDescription)
                .ToListAsync();

            foreach (var longDescription in longDescriptions)
            {
                // Remove single quotes from longDescription
                var cleanedLongDescription = longDescription.Replace("'", "");

                // Check if cleanedLongDescription is not empty
                if (!string.IsNullOrWhiteSpace(cleanedLongDescription))
                {
                    var jsonObject = new
                    {
                        model = "myllama3",
                        prompt = cleanedLongDescription,
                        stream = false
                    };

                    var json = JsonSerializer.Serialize(jsonObject);

                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    // Send request asynchronously and process response
                    tasks.Add(Task.Run(async () =>
                    {
                        var response = await httpClient.PostAsync("https://api.example.com/endpoint", content);
                        var responseContent = await response.Content.ReadAsStringAsync();

                        lock (responses)
                        {
                            responses[cleanedLongDescription] = responseContent;
                        }
                    }));
                }
            }

            // Wait for all tasks to complete
            await Task.WhenAll(tasks);

            // Update database with responses
            foreach (var longDescription in longDescriptions)
            {
                var cleanedLongDescription = longDescription.Replace("'", "");

                if (!string.IsNullOrWhiteSpace(cleanedLongDescription) && responses.TryGetValue(cleanedLongDescription, out var responseContent))
                {
                    var activity = await context.Activities.FirstOrDefaultAsync(a => a.LongDescription == cleanedLongDescription);
                    if (activity != null)
                    {
                        activity.Summary_Issue_AI = responseContent;
                        await context.SaveChangesAsync();
                    }
                }
            }

            return Ok("Data processing completed.");
        }
    }
}
