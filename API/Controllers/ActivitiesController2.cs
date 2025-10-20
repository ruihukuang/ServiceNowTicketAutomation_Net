using System;
using Persistent;
using Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

//curl.exe http://localhost:11434/api/generate -H "Content-Type: application/json" -d "@request.json" 

namespace API.Controllers
{
    public class ActivitiesController2(AppDbContext context, IHttpClientFactory httpClientFactory) : BaseApiController
    {
        [HttpPost("AI_summary")]
        public async Task<IActionResult> SendDataToApi()
        {
            // First, test if Ollama is accessible
            if (!await IsOllamaAccessible())
            {
                return StatusCode(503, "Ollama service is not accessible. Please ensure it's running on localhost:11434");
            }

            var tasks = new List<Task>();

            // Retrieve activities with their IDs for updating
            var activities = await _context.Activities
                .Where(a => !string.IsNullOrWhiteSpace(a.LongDescription))
                .Select(a => new { a.Id, a.LongDescription })
                .ToListAsync();

            foreach (var activity in activities)
            {
                var cleanedLongDescription = activity.LongDescription.Replace("'", "").Trim();
                
                if (!string.IsNullOrWhiteSpace(cleanedLongDescription))
                {
                    tasks.Add(ProcessActivityAsync(activity.Id, cleanedLongDescription));
                }
            }

            // Wait for all tasks to complete
            await Task.WhenAll(tasks);

            return Ok($"AI summary processing completed for {tasks.Count} activities.");
        }

        private async Task ProcessActivityAsync(string Id, string longDescription)
        {
            try
            {
                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30);
                
                var requestBody = new
                {
                    model = "myllama3",
                    prompt = $"Provide a concise summary of this technical issue in 20 words or less: {longDescription}",
                    stream = false
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, "application/json");

                Console.WriteLine($"Sending request to Ollama for activity {Id}");
                Console.WriteLine($"Request JSON: {json}");
                
                var response = await httpClient.PostAsync("http://localhost:11434/api/generate", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Received response for activity {Id}: {responseContent}");
                    
                    // Parse the JSON response to extract the actual text
                    try
                    {
                        var ollamaResponse = JsonSerializer.Deserialize<OllamaResponse>(responseContent);
                        var rawSummary = ollamaResponse?.Response ?? "No response generated";

                        // Enforce 20-word limit and clean up the summary
                        var finalSummary = LimitWordCount(rawSummary, 20);
                        Console.WriteLine($"Final summary for activity {Id}: {finalSummary}");

                        // Update the database
                        await UpdateActivitySummary(Id, finalSummary);
                    }
                    catch (JsonException jsonEx)
                    {
                        Console.WriteLine($"JSON parsing error for activity {Id}: {jsonEx.Message}");
                        await UpdateActivitySummary(Id, "Error parsing AI response");
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    var errorMessage = $"Ollama API returned {response.StatusCode}: {errorContent}";
                    Console.WriteLine($"Error for activity {Id}: {errorMessage}");
                    await UpdateActivitySummary(Id, errorMessage);
                }
            }
            catch (Exception ex)
            {
                // Log error and store error message
                var errorMessage = $"Error processing activity: {ex.Message}";
                Console.WriteLine($"Exception for activity {Id}: {errorMessage}");
                await UpdateActivitySummary(Id, errorMessage);
            }
        }

        private string LimitWordCount(string text, int maxWords)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            // Split the text into words
            var words = text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            
            // Take only the first maxWords
            var limitedWords = words.Take(maxWords).ToArray();
            
            // Join them back together
            var result = string.Join(" ", limitedWords);
            
            // Ensure it ends with proper punctuation
            if (!result.EndsWith(".") && !result.EndsWith("!") && !result.EndsWith("?"))
            {
                result += ".";
            }
            
            return result;
        }

        private async Task UpdateActivitySummary(string Id, string summary)
        {
            try
            {
                // Only update the specific activity and verify the description matches our expectation
                var activity = await _context.Activities
                    .Where(a => a.Id == Id)
                    .FirstOrDefaultAsync();
                    
                if (activity != null)
                {
                    // Optional: Add verification that we're updating the right record
                    // This is overkill but adds an extra safety check
                    if (!string.IsNullOrEmpty(activity.LongDescription))
                    {
                        activity.Summary_Issue_AI = summary.Length > 500 ? summary.Substring(0, 500) : summary;
                        await _context.SaveChangesAsync();
                        Console.WriteLine($"Successfully updated Activity {Id}");
                    }
                    else
                    {
                        Console.WriteLine($"Activity {Id} has empty LongDescription - skipping update");
                    }
                }
                else
                {
                    Console.WriteLine($"Activity {Id} not found in database");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating activity {Id}: {ex.Message}");
            }
        }

        private async Task<bool> IsOllamaAccessible()
        {
            try
            {
                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(5);
                
                var response = await httpClient.GetAsync("http://localhost:11434/api/tags");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        // Add this class to parse Ollama response
        public class OllamaResponse
        {
            public string Model { get; set; } = string.Empty;
            public string Created_At { get; set; } = string.Empty;
            public string Response { get; set; } = string.Empty;
            public bool Done { get; set; }
        }
    }
}
