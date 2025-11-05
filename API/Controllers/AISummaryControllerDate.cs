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

namespace API.Controllers
{
    public class AISummaryDateController(AppDbContext context, IHttpClientFactory httpClientFactory) : BaseApiController
    {
        [HttpGet]
        public IActionResult Get()
        {
            return Ok("AI summary processing API with date filters is working!");
        }

        [HttpPost("AI_summary_date")]
        public async Task<IActionResult> SendDataToApi([FromQuery] string? year = null, [FromQuery] string? month = null)
        {
            // Log the incoming request details
            Console.WriteLine("=== POSTMAN REQUEST RECEIVED ===");
            Console.WriteLine($"Endpoint: POST /api/AISummary/AI_summary");
            Console.WriteLine($"Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            Console.WriteLine($"Year filter: {year ?? "Not specified"}");
            Console.WriteLine($"Month filter: {month ?? "Not specified"}");
            Console.WriteLine("=================================");

            // Build query for activities that need processing
            Console.WriteLine("Checking for activities that need AI summarization...");
            var query = context.Activities
                .Where(a => !string.IsNullOrWhiteSpace(a.LongDescription));

            // Apply year filter if provided
            if (!string.IsNullOrEmpty(year))
            {
                query = query.Where(a => a.OpenDate_Year == year);
                Console.WriteLine($"Applied year filter: {year}");
            }

            // Apply month filter if provided
            if (!string.IsNullOrEmpty(month))
            {
                query = query.Where(a => a.OpenDate_Month == month);
                Console.WriteLine($"Applied month filter: {month}");
            }

            // Ensure both year and month are specified in the data (not null or empty)
            query = query.Where(a => 
                !string.IsNullOrEmpty(a.OpenDate_Year) && 
                !string.IsNullOrEmpty(a.OpenDate_Month));

            var activitiesNeedingProcessing = await query
                .Select(a => new { a.Id, a.LongDescription, a.Summary_Issue_AI, a.OpenDate_Year, a.OpenDate_Month })
                .ToListAsync();

            Console.WriteLine($"=== PROCESSING CHECK ===");
            Console.WriteLine($"Activities needing AI summarization: {activitiesNeedingProcessing.Count}");
            
            // Show filter details
            if (!string.IsNullOrEmpty(year) || !string.IsNullOrEmpty(month))
            {
                Console.WriteLine($"Filtered by - Year: {year ?? "Any"}, Month: {month ?? "Any"}");
            }

            // Show which activities need processing with their dates
            Console.WriteLine("Activities that need processing:");
            foreach (var activity in activitiesNeedingProcessing)
            {
                Console.WriteLine($"ID: {activity.Id}, Date: {activity.OpenDate_Year}-{activity.OpenDate_Month}, Current Summary: '{activity.Summary_Issue_AI}'");
            }
            Console.WriteLine("======================================");

            // If no activities need processing, return early
            if (activitiesNeedingProcessing.Count == 0)
            {
                var message = "No activities need AI summarization";
                if (!string.IsNullOrEmpty(year) || !string.IsNullOrEmpty(month))
                {
                    message += $" for the specified filters (Year: {year ?? "Any"}, Month: {month ?? "Any"})";
                }
                message += " - all Summary_Issue_AI fields are already populated";
                
                return Ok(new { 
                    Message = message, 
                    ProcessedCount = 0,
                    YearFilter = year,
                    MonthFilter = month,
                    Timestamp = DateTime.UtcNow
                });
            }

            // Then, test if Ollama is accessible
            if (!await IsOllamaAccessible())
            {
                Console.WriteLine("Ollama service is not accessible - returning 503");
                return StatusCode(503, "Ollama service is not accessible. Please ensure it's running on localhost:11434");
            }

            // Create a dictionary to store all summaries
            var summaries = new Dictionary<string, string>();
            var tasks = new List<Task>();

            Console.WriteLine($"=== STARTING AI PROCESSING ===");
            Console.WriteLine($"Processing {activitiesNeedingProcessing.Count} activities that need summarization");
            if (!string.IsNullOrEmpty(year) || !string.IsNullOrEmpty(month))
            {
                Console.WriteLine($"Filter criteria - Year: {year ?? "Any"}, Month: {month ?? "Any"}");
            }

            // Process only activities that need summarization
            foreach (var activity in activitiesNeedingProcessing)
            {
                var cleanedLongDescription = activity.LongDescription.Replace("'", "").Trim();
                
                if (!string.IsNullOrWhiteSpace(cleanedLongDescription))
                {
                    tasks.Add(ProcessActivityAsync(activity.Id, cleanedLongDescription, summaries));
                }
            }

            Console.WriteLine($"Started {tasks.Count} processing tasks");

            // Wait for all tasks to complete
            await Task.WhenAll(tasks);

            Console.WriteLine($"All {tasks.Count} tasks completed successfully");

            // Batch update all activities at the end
            Console.WriteLine($"=== BATCH DATABASE UPDATE ===");
            Console.WriteLine($"Number of summaries to update: {summaries.Count}");
            await BatchUpdateActivitySummaries(summaries);

            return Ok(new { 
                Message = "AI summary processing for selected period completed", 
                ProcessedCount = summaries.Count,
                YearFilter = year,
                MonthFilter = month,
                Timestamp = DateTime.UtcNow
            });
        }

        private async Task ProcessActivityAsync(string Id, string longDescription, Dictionary<string, string> summaries)
        {
            try
            {
                using var httpClient = httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(3600);
                
                var requestBody = new
                {
                    model = "myllaama3",
                    prompt = $"Provide a concise summary with 150 characters or less for this technical issue : {longDescription}",
                    stream = false
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                Console.WriteLine($"Sending request to Ollama for activity {Id}");
                
                var response = await httpClient.PostAsync("http://localhost:11434/api/generate", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    
                    // Parse the JSON response to extract the actual text
                    try
                    {
                        // Use case-insensitive deserialization
                        var options = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        };
                        
                        var ollamaResponse = JsonSerializer.Deserialize<OllamaResponse>(responseContent, options);
                        
                        // Use the lowercase properties directly
                        var rawSummary = ollamaResponse?.response ?? "No response generated";

                        // If rawSummary is empty, try to extract manually from JSON
                        if (string.IsNullOrEmpty(rawSummary))
                        {
                            try
                            {
                                using JsonDocument document = JsonDocument.Parse(responseContent);
                                if (document.RootElement.TryGetProperty("response", out JsonElement responseElement))
                                {
                                    rawSummary = responseElement.GetString() ?? "Manual extraction failed";
                                }
                                else
                                {
                                    rawSummary = "Response property not found in JSON";
                                }
                            }
                            catch (Exception jsonEx)
                            {
                                rawSummary = $"Manual JSON parsing failed: {jsonEx.Message}";
                            }
                        }

                        // Use the raw summary directly without word limit restrictions
                        var finalSummary = rawSummary.Trim();
                        
                        Console.WriteLine($"Generated summary for activity {Id} (length: {finalSummary.Length})");

                        // Store in dictionary for batch update
                        lock (summaries)
                        {
                            summaries[Id] = finalSummary;
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        Console.WriteLine($"JSON parsing error for activity {Id}: {jsonEx.Message}");
                        lock (summaries)
                        {
                            summaries[Id] = "Error parsing AI response";
                        }
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    var errorMessage = $"Ollama API returned {response.StatusCode}: {errorContent}";
                    Console.WriteLine($"Error for activity {Id}: {errorMessage}");
                    lock (summaries)
                    {
                        summaries[Id] = errorMessage;
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error and store error message
                var errorMessage = $"Error processing activity: {ex.Message}";
                Console.WriteLine($"Exception for activity {Id}: {errorMessage}");
                lock (summaries)
                {
                    summaries[Id] = errorMessage;
                }
            }
        }

        private async Task BatchUpdateActivitySummaries(Dictionary<string, string> summaries)
        {
            try
            {
                Console.WriteLine($"=== STARTING BATCH UPDATE ===");
                Console.WriteLine($"Total summaries to process: {summaries.Count}");
                
                if (summaries.Count == 0)
                {
                    Console.WriteLine("No summaries to update - skipping batch update");
                    return;
                }

                // Get all activities that need updating
                var activityIds = summaries.Keys.ToList();
                var activitiesToUpdate = await context.Activities
                    .Where(a => activityIds.Contains(a.Id))
                    .ToListAsync();

                Console.WriteLine($"Found {activitiesToUpdate.Count} activities in database to update");

                int updatedCount = 0;
                foreach (var activity in activitiesToUpdate)
                {
                    if (summaries.TryGetValue(activity.Id, out var summary))
                    {
                        // Ensure we have a valid summary before updating
                        if (!string.IsNullOrWhiteSpace(summary))
                        {
                            activity.Summary_Issue_AI = summary;
                            updatedCount++;
                        }
                        else
                        {
                            // Set a fallback value
                            activity.Summary_Issue_AI = "AI summary not generated";
                            updatedCount++;
                        }
                    }
                }

                Console.WriteLine($"Attempting to save {updatedCount} changes to database...");
                var changesSaved = await context.SaveChangesAsync();
                Console.WriteLine($"SaveChangesAsync completed. Changes saved: {changesSaved}");
                Console.WriteLine($"Successfully batch updated {updatedCount} activities");
                Console.WriteLine($"=== BATCH UPDATE COMPLETED ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"=== BATCH UPDATE ERROR ===");
                Console.WriteLine($"Error in batch update: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                Console.WriteLine($"=== END BATCH UPDATE ERROR ===");
                throw;
            }
        }

        private async Task<bool> IsOllamaAccessible()
        {
            try
            {
                using var httpClient = httpClientFactory.CreateClient();
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
            public string model { get; set; } = string.Empty;
            public string created_at { get; set; } = string.Empty;
            public string response { get; set; } = string.Empty;
            public bool done { get; set; }
            public string done_reason { get; set; } = string.Empty;
        }
    }
}