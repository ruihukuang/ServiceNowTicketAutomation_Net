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
    public class RootCauseDateController(AppDbContext context, IHttpClientFactory httpClientFactory) : BaseApiController
    {
        [HttpGet]
        public IActionResult Get()
        {
            return Ok("AI root cause processing API with date filters is working!");
        }

        [HttpPost("AI_RootCause_date")]
        public async Task<IActionResult> SendDataToApi([FromQuery] string? year = null, [FromQuery] string? month = null)
        {
            // Log the incoming request details for Postman
            Console.WriteLine("=== POSTMAN REQUEST RECEIVED ===");
            Console.WriteLine($"Endpoint: POST /api/RootCause/AI_RootCause");
            Console.WriteLine($"Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            Console.WriteLine($"Year filter: {year ?? "Not specified"}");
            Console.WriteLine($"Month filter: {month ?? "Not specified"}");
            Console.WriteLine("=================================");

            // Build query based on filters
            var query = context.Activities
                .Where(a => !string.IsNullOrWhiteSpace(a.LongDescription)&& 
                           !string.IsNullOrWhiteSpace(a.Issue_AI));

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

            // First, check if there are any activities that need processing
            // (have LongDescription and Root_Cause_AI but no Root_Cause_AI)
            Console.WriteLine("Checking for activities that need AI root cause analysis...");
            var activitiesNeedingProcessing = await query
                .Select(a => new { a.Id, a.LongDescription, a.Issue_AI, a.Root_Cause_AI,a.OpenDate_Year, a.OpenDate_Month })
                .ToListAsync();

            Console.WriteLine($"=== PROCESSING CHECK ===");
            Console.WriteLine($"Activities needing AI root cause analysis: {activitiesNeedingProcessing.Count}");
            
            // If no activities need processing, return early
            if (activitiesNeedingProcessing.Count == 0)
            {
                Console.WriteLine("No activities need AI root cause analysis - all Root_Cause_AI fields are already populated");
                return Ok(new { 
                    Message = "No AI root cause processing needed - all Root_Cause_AI fields are already populated", 
                    ProcessedCount = 0,
                    Timestamp = DateTime.UtcNow
                });
            }

            // Show which activities need processing
            Console.WriteLine("Activities that need processing:");
            foreach (var activity in activitiesNeedingProcessing)
            {
                Console.WriteLine($"ID: {activity.Id}, Current Root_Cause_AI: '{activity.Root_Cause_AI}'");
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
            Console.WriteLine($"Processing {activitiesNeedingProcessing.Count} activities that need root cause analysis");

            // Verify activities exist in database and show Root_Cause_AI data
            Console.WriteLine($"=== DATABASE VERIFICATION ===");
            Console.WriteLine($"Activities retrieved from DB: {activitiesNeedingProcessing.Count}");
            foreach (var activity in activitiesNeedingProcessing)
            {
                var exists = await context.Activities.AnyAsync(a => a.Id == activity.Id);
                Console.WriteLine($"Activity {activity.Id} exists in database: {exists}");
                Console.WriteLine($"Activity {activity.Id} Issue_AI: '{activity.Issue_AI}'");
            }
            Console.WriteLine("======================================");

            // Show the activities in console
            Console.WriteLine($"=== DATABASE ACTIVITIES RETRIEVED ===");
            Console.WriteLine($"Total activities found with Root_Cause_AI data: {activitiesNeedingProcessing.Count}");
            Console.WriteLine("======================================");

            // Process only activities that need root cause analysis
            foreach (var activity in activitiesNeedingProcessing)
            {
                var cleanedLongDescription = activity.LongDescription.Replace("'", "").Trim();
                
                if (!string.IsNullOrWhiteSpace(cleanedLongDescription) && !string.IsNullOrWhiteSpace(activity.Issue_AI))
                {
                    tasks.Add(ProcessActivityAsync(activity.Id, cleanedLongDescription, activity.Issue_AI, summaries));
                }
                else
                {
                    Console.WriteLine($"Skipping activity {activity.Id} - missing LongDescription or Root_Cause_AI data");
                }
            }

            Console.WriteLine($"Started {tasks.Count} processing tasks");

            // Wait for all tasks to complete
            await Task.WhenAll(tasks);

            Console.WriteLine($"All {tasks.Count} tasks completed successfully");

            // Batch update all activities at the end
            Console.WriteLine($"=== BATCH DATABASE UPDATE ===");
            Console.WriteLine($"Number of root causes to update: {summaries.Count}");
            await BatchUpdateActivitySummaries(summaries);

            return Ok(new { 
                Message = "AI root causes processing for selected period completed", 
                ProcessedCount = summaries.Count,
                ActivitiesWithRootCauseAI = activitiesNeedingProcessing.Count,
                Timestamp = DateTime.UtcNow
            });
        }

        private async Task ProcessActivityAsync(string Id, string longDescription, string Issue_AI, Dictionary<string, string> summaries)
        {
            try
            {
                using var httpClient = httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(3600);
                
                var requestBody = new
                {
                    model = "myllaama3",
                    prompt = $@"Analyze a technical issue statement and identify the most likely root causes based on the category/categories.

                    Root causes should not be the same as a category or categories.

                    Statement: {longDescription}

                    Category: {Issue_AI}

                    Potential Root Causes by Category:

                    1. Root causes for a category Authentication & Authorization:
                    - Invalid credentials (wrong password, expired tokens)
                    - Account lockouts or suspensions
                    - Insufficient permissions or role assignments
                    - Session/timeout configuration issues
                    - Certificate or SSL/TLS problems
                    - Multi-factor authentication failures

                    2. Root causes for a category Network:
                    - Internet connectivity outages (ISP issues)
                    - DNS resolution failures
                    - Firewall or security group blocks
                    - Router/switch hardware failures
                    - Bandwidth limitations or throttling
                    - Network configuration errors (IP, subnet, gateway)

                    3. Root causes for a category Functionality & Logic:
                    - Software bugs or coding errors
                    - Race conditions in concurrent operations
                    - Null pointer or exception handling issues
                    - Business rule validation failures
                    - Data integrity or consistency problems
                    - Algorithm or calculation errors

                    4. Root causes for a category Integration:
                    - Third-party service downtime or maintenance
                    - API version mismatches or deprecation
                    - Authentication failures with external systems
                    - Rate limiting or quota exceeded
                    - Data format incompatibilities (JSON/XML schema)
                    - Network latency between microservices

                    5. Root causes for a category Data Migration:
                    - Data type incompatibilities between systems
                    - Character encoding issues (UTF-8 vs ASCII)
                    - Referential integrity violations
                    - Data truncation from field size differences
                    - Missing required fields or validation rules
                    - Custom field mapping errors

                    6. Root causes for a category Client-Side:
                    - Browser compatibility or version issues
                    - JavaScript errors or library conflicts
                    - Missing dependencies or CDN failures
                    - CORS policy violations
                    - Local storage or cache problems
                    - Device-specific limitations (memory, CPU)

                    7. Root causes for a category Infrastructure & Resources:
                    - Hardware failures (CPU, memory, disk, network)
                    - Resource exhaustion (memory leaks, disk space)
                    - Configuration errors (ports, paths, permissions)
                    - Scaling limitations (insufficient capacity)
                    - Operating system or kernel issues
                    - Virtualization/containerization problems

                   Return up root causes according to each category in {Issue_AI}, separated by commas (e.g., 'Resource exhaustion, Configuration errors, Hardware failure', 'Software bugs, Data validation issues', etc.)",
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

                        // Store the raw summary directly (no punctuation removal)
                        var finalSummary = rawSummary.Trim().ToLower();
                        
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
                        Console.WriteLine($"Stored root cause for activity {Id} in batch");
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
                        Console.WriteLine($"Current Root_Cause_AI: '{activity.Root_Cause_AI}'");
                        Console.WriteLine($"New summary: '{summary}'");
                        Console.WriteLine($"New summary length: {summary.Length}");
                        
                        // Ensure we have a valid summary before updating
                        if (!string.IsNullOrWhiteSpace(summary))
                        {
                            activity.Root_Cause_AI = summary;
                            Console.WriteLine($"Setting Root_Cause_AI to: '{activity.Root_Cause_AI}'");
                            updatedCount++;
                        }
                        else
                        {
                            Console.WriteLine($"WARNING: Empty root cause for activity {activity.Id} - skipping update");
                            // Set a fallback value
                            activity.Root_Cause_AI = "AI root cause not generated";
                            updatedCount++;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"No root cause found for activity {activity.Id}");
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