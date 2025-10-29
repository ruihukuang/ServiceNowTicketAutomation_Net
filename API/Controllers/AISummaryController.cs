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
    public class AISummaryController(AppDbContext context, IHttpClientFactory httpClientFactory) : BaseApiController
    {
        [HttpGet]
        public IActionResult Get()
        {
            return Ok("AI Summary test controller is working!");
        }

        [HttpPost("AI_summary")]
        public async Task<IActionResult> SendDataToApi()
        {
            // Log the incoming request details for Postman
            Console.WriteLine("=== POSTMAN REQUEST RECEIVED ===");
            Console.WriteLine($"Endpoint: POST /api/AISummary/AI_summary");
            Console.WriteLine($"Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            Console.WriteLine($"Content-Type: application/json");
            Console.WriteLine("=================================");

            // First, check if there are any activities that need processing
            // (have LongDescription but no Summary_Issue_AI)
            Console.WriteLine("Checking for activities that need AI summarization...");
            var activitiesNeedingProcessing = await context.Activities
                .Where(a => !string.IsNullOrWhiteSpace(a.LongDescription) && 
                           string.IsNullOrWhiteSpace(a.Summary_Issue_AI))
                .Select(a => new { a.Id, a.LongDescription, a.Summary_Issue_AI })
                .ToListAsync();

            Console.WriteLine($"=== PROCESSING CHECK ===");
            Console.WriteLine($"Activities needing AI summarization: {activitiesNeedingProcessing.Count}");
            
            // If no activities need processing, return early
            if (activitiesNeedingProcessing.Count == 0)
            {
                Console.WriteLine("No activities need AI summarization - all Summary_Issue_AI fields are already populated");
                return Ok(new { 
                    Message = "No AI summary processing needed - all Summary_Issue_AI fields are already populated", 
                    ProcessedCount = 0,
                    Timestamp = DateTime.UtcNow
                });
            }

            // Show which activities need processing
            Console.WriteLine("Activities that need processing:");
            foreach (var activity in activitiesNeedingProcessing)
            {
                Console.WriteLine($"ID: {activity.Id}, Current Summary: '{activity.Summary_Issue_AI}'");
            }
            Console.WriteLine("======================================");

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
                Message = "AI summary processing completed", 
                ProcessedCount = summaries.Count,
                Timestamp = DateTime.UtcNow
            });
        }

        private async Task ProcessActivityAsync(string Id, string longDescription, Dictionary<string, string> summaries)
        {
            try
            {
                using var httpClient = httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(300);
                
                var requestBody = new
                {
                    model = "myllaama3",
                    prompt = $"Provide a concise summary with 150 characters or less for this technical issue : {longDescription}",
                    stream = false
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

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
                        Console.WriteLine($"=== DEBUG JSON RESPONSE ===");
                        Console.WriteLine($"Full JSON response: {responseContent}");
                        Console.WriteLine($"=====================================");
                        
                        // Use case-insensitive deserialization
                        var options = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        };
                        
                        var ollamaResponse = JsonSerializer.Deserialize<OllamaResponse>(responseContent, options);
                        
                        // Use the lowercase properties directly
                        var rawSummary = ollamaResponse?.response ?? "No response generated";

                        Console.WriteLine($"=== DEBUG RAW SUMMARY EXTRACTION ===");
                        Console.WriteLine($"Raw summary extracted: '{rawSummary}'");
                        Console.WriteLine($"Raw summary length: {rawSummary?.Length}");
                        Console.WriteLine($"Is raw summary null: {rawSummary == null}");
                        Console.WriteLine($"Is raw summary empty: {string.IsNullOrEmpty(rawSummary)}");
                        
                        // Debug the entire OllamaResponse object
                        Console.WriteLine($"OllamaResponse model: '{ollamaResponse?.model}'");
                        Console.WriteLine($"OllamaResponse created_at: '{ollamaResponse?.created_at}'");
                        Console.WriteLine($"OllamaResponse done: {ollamaResponse?.done}");
                        Console.WriteLine($"OllamaResponse done_reason: '{ollamaResponse?.done_reason}'");
                        Console.WriteLine($"=====================================");
                        
                        // If rawSummary is empty, try to extract manually from JSON
                        if (string.IsNullOrEmpty(rawSummary))
                        {
                            Console.WriteLine("=== ATTEMPTING MANUAL JSON EXTRACTION ===");
                            try
                            {
                                using JsonDocument document = JsonDocument.Parse(responseContent);
                                if (document.RootElement.TryGetProperty("response", out JsonElement responseElement))
                                {
                                    rawSummary = responseElement.GetString() ?? "Manual extraction failed";
                                    Console.WriteLine($"Manually extracted response: '{rawSummary}'");
                                }
                                else
                                {
                                    rawSummary = "Response property not found in JSON";
                                    Console.WriteLine("Response property not found in JSON");
                                }
                            }
                            catch (Exception jsonEx)
                            {
                                rawSummary = $"Manual JSON parsing failed: {jsonEx.Message}";
                                Console.WriteLine($"Manual JSON parsing error: {jsonEx.Message}");
                            }
                            Console.WriteLine($"=====================================");
                        }

                        // Use the raw summary directly without word limit restrictions
                        var finalSummary = rawSummary.Trim();
                        
                        Console.WriteLine($"=== DEBUG FINAL SUMMARY ===");
                        Console.WriteLine($"Final summary: '{finalSummary}'");
                        Console.WriteLine($"Final summary length: {finalSummary.Length}");
                        Console.WriteLine($"Word count: {finalSummary.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length}");
                        Console.WriteLine($"=====================================");

                        // Store in dictionary for batch update
                        lock (summaries)
                        {
                            summaries[Id] = finalSummary;
                        }
                        Console.WriteLine($"Stored summary for activity {Id} in batch");
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
                
                // Debug: Show all summaries before update
                Console.WriteLine("=== SUMMARIES TO UPDATE ===");
                foreach (var (id, summary) in summaries)
                {
                    Console.WriteLine($"ID: {id}, Summary: '{summary}', Length: {summary?.Length}");
                }
                Console.WriteLine("============================");
                
                if (summaries.Count == 0)
                {
                    Console.WriteLine("No summaries to update - skipping batch update");
                    return;
                }

                // Get all activities that need updating
                var activityIds = summaries.Keys.ToList();
                Console.WriteLine($"Activity IDs to update: {string.Join(", ", activityIds)}");

                var activitiesToUpdate = await context.Activities
                    .Where(a => activityIds.Contains(a.Id))
                    .ToListAsync();

                Console.WriteLine($"Found {activitiesToUpdate.Count} activities in database to update");

                int updatedCount = 0;
                foreach (var activity in activitiesToUpdate)
                {
                    if (summaries.TryGetValue(activity.Id, out var summary))
                    {
                        Console.WriteLine($"=== UPDATING ACTIVITY {activity.Id} ===");
                        Console.WriteLine($"Current Summary_Issue_AI: '{activity.Summary_Issue_AI}'");
                        Console.WriteLine($"New summary: '{summary}'");
                        Console.WriteLine($"New summary length: {summary.Length}");
                        
                        // Ensure we have a valid summary before updating
                        if (!string.IsNullOrWhiteSpace(summary))
                        {
                            activity.Summary_Issue_AI = summary;
                            Console.WriteLine($"Setting Summary_Issue_AI to: '{activity.Summary_Issue_AI}'");
                            updatedCount++;
                        }
                        else
                        {
                            Console.WriteLine($"WARNING: Empty summary for activity {activity.Id} - skipping update");
                            // Set a fallback value
                            activity.Summary_Issue_AI = "AI summary not generated";
                            updatedCount++;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"No summary found for activity {activity.Id}");
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