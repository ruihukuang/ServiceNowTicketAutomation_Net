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
    public class IssueDateController(AppDbContext context, IHttpClientFactory httpClientFactory) : BaseApiController
    {
        [HttpGet]
        public IActionResult Get()
        {
            return Ok("AI issue processing API with date filters is working!");
        }

        [HttpPost("AI_Issue_date")]
        public async Task<IActionResult> SendDataToApi([FromQuery] string? year = null, [FromQuery] string? month = null)
        {
            // Log the incoming request details for Postman
            Console.WriteLine("=== POSTMAN REQUEST RECEIVED ===");
            Console.WriteLine($"Endpoint: POST /api/Issue/AI_Issue");
            Console.WriteLine($"Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            Console.WriteLine($"Year filter: {year ?? "Not specified"}");
            Console.WriteLine($"Month filter: {month ?? "Not specified"}");
            Console.WriteLine("=================================");

            // Build query based on filters
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

            // Get activities that need processing (removed Issue_AI null check)
            Console.WriteLine("Checking for activities with LongDescription...");
            var activitiesToProcess = await query
                .Select(a => new { a.Id, a.LongDescription, a.Issue_AI, a.OpenDate_Year, a.OpenDate_Month })
                .ToListAsync();

            Console.WriteLine($"=== PROCESSING CHECK ===");
            Console.WriteLine($"Activities found with LongDescription: {activitiesToProcess.Count}");

            // Show filter details
            if (!string.IsNullOrEmpty(year) || !string.IsNullOrEmpty(month))
            {
                Console.WriteLine($"Filtered by - Year: {year ?? "Any"}, Month: {month ?? "Any"}");
            }
            
            // If no activities found, return early
            if (activitiesToProcess.Count == 0)
            {
                Console.WriteLine("No activities found with LongDescription matching the filters");
                return Ok(new { 
                    Message = "No activities found with LongDescription matching the specified filters", 
                    ProcessedCount = 0,
                    YearFilter = year,
                    MonthFilter = month,
                    Timestamp = DateTime.UtcNow
                });
            }

            // Show which activities will be processed
            Console.WriteLine("Activities that will be processed:");
            foreach (var activity in activitiesToProcess)
            {
                Console.WriteLine($"ID: {activity.Id}, Date: {activity.OpenDate_Year}-{activity.OpenDate_Month}, Current Issue_AI: '{activity.Issue_AI}'");
            }
            Console.WriteLine("======================================");

            // Then, test if Ollama is accessible
            if (!await IsOllamaAccessible())
            {
                Console.WriteLine("Ollama service is not accessible - returning 503");
                return StatusCode(503, "Ollama service is not accessible. Please ensure it's running on localhost:11434");
            }

            // Create a dictionary to store all issues
            var summaries = new Dictionary<string, string>();
            var tasks = new List<Task>();

            Console.WriteLine($"=== STARTING AI PROCESSING ===");
            Console.WriteLine($"Processing {activitiesToProcess.Count} activities with LongDescription");

            // Show the activities in console
            Console.WriteLine($"=== DATABASE ACTIVITIES RETRIEVED ===");
            Console.WriteLine($"Total activities found: {activitiesToProcess.Count}");
            Console.WriteLine("Activities details:");
            
            foreach (var activity in activitiesToProcess)
            {
                var shortDescription = activity.LongDescription.Length > 100 
                    ? activity.LongDescription.Substring(0, 100) + "..." 
                    : activity.LongDescription;
                    
                Console.WriteLine($"  ID: {activity.Id}");
                Console.WriteLine($"  Description: {shortDescription}");
                Console.WriteLine($"  Full Length: {activity.LongDescription.Length} characters");
                Console.WriteLine($"  Cleaned: {activity.LongDescription.Replace("'", "").Trim().Length} characters (after cleaning)");
                Console.WriteLine($"  Current Issue_AI: '{activity.Issue_AI}'");
                Console.WriteLine("  ---");
            }
            Console.WriteLine("======================================");

            // Process all activities with LongDescription (regardless of existing Issue_AI value)
            foreach (var activity in activitiesToProcess)
            {
                var cleanedLongDescription = activity.LongDescription.Replace("'", "").Trim();
                
                if (!string.IsNullOrWhiteSpace(cleanedLongDescription))
                {
                    tasks.Add(ProcessActivityAsync(activity.Id, cleanedLongDescription, summaries));
                    // Each task populates the dictionary with NEW AI-generated content
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
                Message = "AI issue processing for selected period completed", 
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
                    prompt = $@"
                    ANALYZE THIS STATEMENT AND CATEGORIZE IT:

                    Statement: {longDescription}

                    CATEGORIES TO CHOOSE FROM:
                    - Authentication & Authorization
                    - Network
                    - Functionality & Logic  
                    - Integration
                    - Data Migration
                    - Client-Side
                    - Infrastructure & Resources

                    CATEGORY DEFINITIONS AND EXAMPLES:

                    1. Authentication & Authorization
                    Errors related to user identity verification, login, or access permissions
                    Examples: 'Invalid username or password', 'Access Denied', 'Your session has expired', 
                    '401 Unauthorized', '403 Forbidden', 'Invalid Credentials', 'Authentication failures',
                    'authentication issues', 'Initial username/password errors', 'authentication loops'

                    2. Network
                    Errors related to connectivity, communication, or network issues between components
                    Examples: 'Connection Timed Out', 'Network Error', 'DNS_PROBE_FINISHED_NO_INTERNET', 
                    'Cannot reach the server', 'connection failed', 'timeout', 'network unavailable'

                    3. Functionality & Logic
                    Errors where features, calculations, or business logic fail to execute correctly
                    Examples: 'Failed to apply discount code', 'Unable to process your request', 
                    'Cannot divide by zero', 'The selected item is out of stock', 'calculation error'

                    4. Integration
                    Errors when communicating with external services, APIs, or third-party systems
                    Examples: 'Payment Gateway Unavailable', 'Service Unavailable', 
                    'Could not retrieve data from external service', 'API not responding', 'third-party error'

                    5. Data Migration
                    Errors during data transfer, import, or export involving format or validation issues
                    Examples: 'Migration Failed', 'Invalid date format', 'Duplicate key error', 
                    'Data truncation error', 'Referential integrity violation', 'import/export error'

                    6. Client-Side
                    Errors occurring entirely in the user's browser, device, or local application
                    Examples: 'JavaScript Error', 'This field is required', 'Please enter a valid email address', 
                    'Video could not be loaded', 'browser-related errors', 'client error', 'UI issue'

                    7. Infrastructure & Resources
                    Errors related to hardware, servers, infrastructure, or resource constraints
                    Examples: 'Server out of memory', 'Insufficient CPU resources', 'Disk space exhausted', 
                    'Resource quota exceeded', 'Could not start server', 'Port already in use', 
                    'Service failed to initialize'

                    INSTRUCTIONS:
                    - Analyze the statement and identify which categories it matches
                    - Look for keywords, phrases, or concepts that align with the category examples
                    - Return up to THREE most relevant category names in order of relevance
                    - Separate category names with commas only
                    - Do NOT include numbers, explanations, or additional text
                    - Only return category names from the list above

                    OUTPUT FORMAT: Category1, Category2, Category3 ",
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
                        Console.WriteLine($"Stored issues for activity {Id} in batch");
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
                        Console.WriteLine($"Current Issue_AI: '{activity.Issue_AI}'");
     
                        
                        // Ensure we have a valid summary before updating
                        if (!string.IsNullOrWhiteSpace(summary))
                        {
                            activity.Issue_AI = summary;
                            Console.WriteLine($"Setting Issue_AI to: '{activity.Issue_AI}'");
                            updatedCount++;
                        }
                        else
                        {
                            Console.WriteLine($"WARNING: Empty issues for activity {activity.Id} - skipping update");
                            // Set a fallback value
                            activity.Issue_AI = "AI issues not generated";
                            updatedCount++;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"No issues found for activity {activity.Id}");
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