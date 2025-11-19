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
    public class DuplicateUpdateController(AppDbContext context, IHttpClientFactory httpClientFactory) : BaseApiController
    {
        [HttpGet]
        public IActionResult Get()
        {
            return Ok("AI duplicate update test controller is working!");
        }

        [HttpPost("AI_Duplicate_update")]
        public async Task<IActionResult> SendDataToApi()
        {
            // Log the incoming request details for Postman
            Console.WriteLine("=== POSTMAN REQUEST RECEIVED ===");
            Console.WriteLine($"Endpoint: POST /api/Duplicate/AI_Duplicate");
            Console.WriteLine($"Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            Console.WriteLine($"Content-Type: application/json");
            Console.WriteLine("=================================");

            // First, check if there are any activities that need processing
            // Only process records that don't have valid duplicate groups yet
            Console.WriteLine("Checking for activities that need AI duplicate analysis...");
            var activitiesNeedingProcessing = await context.Activities
                .Where(a => !string.IsNullOrWhiteSpace(a.LongDescription) &&
                           !string.IsNullOrWhiteSpace(a.Issue_AI) &&
                           !string.IsNullOrWhiteSpace(a.System_AI) &&
                           a.OpenDate.HasValue &&
                           a.UpdatedDate.HasValue &&
                           a.IncidentNumber != null )
                .Select(a => new ActivityData 
                { 
                    Id = a.Id, 
                    LongDescription = a.LongDescription, 
                    Issue_AI = a.Issue_AI, 
                    System_AI = a.System_AI, 
                    IncidentNumber = a.IncidentNumber,
                    OpenDate = a.OpenDate.Value, // Use .Value for nullable DateTime
                    UpdatedDate = a.UpdatedDate.Value // Use .Value for nullable DateTime
                })
                .ToListAsync();

            Console.WriteLine($"=== PROCESSING CHECK ===");
            Console.WriteLine($"Activities needing AI duplicate analysis: {activitiesNeedingProcessing.Count}");
            
            // If no activities need processing, return early
            if (activitiesNeedingProcessing.Count == 0)
            {
                Console.WriteLine("No activities need AI duplicate analysis - all duplicate_AI fields are already populated with valid results");
                return Ok(new { 
                    Message = "No AI duplicate processing needed - all duplicate_AI fields are already populated with valid results", 
                    ProcessedCount = 0,
                    Timestamp = DateTime.UtcNow
                });
            }

            // Show which activities need processing
            Console.WriteLine("Activities that need processing:");
            foreach (var activity in activitiesNeedingProcessing)
            {
                Console.WriteLine($"ID: {activity.Id}, IncidentNumber: {activity.IncidentNumber}, OpenDate: {activity.OpenDate}, UpdatedDate: {activity.UpdatedDate}");
            }
            Console.WriteLine("======================================");

            // Then, test if Ollama is accessible
            if (!await IsOllamaAccessible())
            {
                Console.WriteLine("Ollama service is not accessible - returning 503");
                return StatusCode(503, "Ollama service is not accessible. Please ensure it's running on localhost:11434");
            }

            // Get all activities from database for similarity comparison (with date information)
            var allActivities = await context.Activities
                .Where(a => !string.IsNullOrWhiteSpace(a.LongDescription) &&
                           !string.IsNullOrWhiteSpace(a.Issue_AI) &&
                           !string.IsNullOrWhiteSpace(a.System_AI) &&
                           a.IncidentNumber != null &&
                           a.OpenDate.HasValue &&
                           a.UpdatedDate.HasValue)
                .Select(a => new ActivityData 
                { 
                    Id = a.Id, 
                    LongDescription = a.LongDescription, 
                    Issue_AI = a.Issue_AI, 
                    System_AI = a.System_AI, 
                    IncidentNumber = a.IncidentNumber,
                    OpenDate = a.OpenDate.Value, // Use .Value for nullable DateTime
                    UpdatedDate = a.UpdatedDate.Value // Use .Value for nullable DateTime
                })
                .ToListAsync();

            Console.WriteLine($"=== SIMILARITY COMPARISON DATA ===");
            Console.WriteLine($"Total activities available for similarity comparison: {allActivities.Count}");
            Console.WriteLine("======================================");

            // Create dictionaries to store all duplicate analysis results
            var duplicateResults = new Dictionary<string, string>();
            var duplicateGroups = new Dictionary<string, HashSet<string>>(); // Tracks complete duplicate groups
            var processedRecords = new HashSet<string>(); // Tracks records that are already part of a duplicate group
            
            Console.WriteLine($"=== STARTING AI PROCESSING ===");
            Console.WriteLine($"Processing {activitiesNeedingProcessing.Count} activities for duplicate analysis");

            // Process only activities that need duplicate analysis
            foreach (var currentActivity in activitiesNeedingProcessing)
            {
                // Skip if this record is already part of a processed duplicate group
                if (processedRecords.Contains(currentActivity.Id))
                {
                    Console.WriteLine($"Skipping activity {currentActivity.Id} - already part of a duplicate group");
                    continue;
                }

                // Find similar activities (excluding current activity and within date range)
                var similarActivities = await FindSimilarActivitiesAsync(currentActivity, allActivities);
                
                await ProcessActivityAsync(
                    currentActivity.Id, 
                    currentActivity.LongDescription, 
                    currentActivity.Issue_AI, 
                    currentActivity.System_AI,
                    currentActivity.OpenDate,
                    currentActivity.UpdatedDate,
                    currentActivity.IncidentNumber,
                    similarActivities,
                    duplicateResults,
                    duplicateGroups,
                    processedRecords,
                    allActivities
                );
            }

            Console.WriteLine($"All processing tasks completed");

            // Process duplicate groups to ensure all records in a group reference each other
            await ProcessDuplicateGroups(duplicateResults, duplicateGroups);

            // Batch update all activities at the end
            Console.WriteLine($"=== BATCH DATABASE UPDATE ===");
            Console.WriteLine($"Number of duplicate results to update: {duplicateResults.Count}");
            await BatchUpdateActivityDuplicateResults(duplicateResults);

            return Ok(new { 
                Message = "AI duplicates processing completed", 
                ProcessedCount = duplicateResults.Count,
                ActivitiesWithDuplicateAI = activitiesNeedingProcessing.Count,
                Timestamp = DateTime.UtcNow
            });
        }

        private async Task ProcessDuplicateGroups(Dictionary<string, string> duplicateResults, Dictionary<string, HashSet<string>> duplicateGroups)
        {
            Console.WriteLine($"=== PROCESSING DUPLICATE GROUPS ===");
            Console.WriteLine($"Total duplicate groups found: {duplicateGroups.Count}");
            
            foreach (var (groupId, duplicateSet) in duplicateGroups)
            {
                Console.WriteLine($"Processing duplicate group with {duplicateSet.Count} records");
                
                // Convert the set to a sorted list for consistent output
                var incidentNumbers = duplicateSet
                    .Select(id => GetIncidentNumberById(id))
                    .Where(incidentNumber => !string.IsNullOrEmpty(incidentNumber))
                    .OrderBy(incidentNumber => incidentNumber)
                    .ToList();

                if (incidentNumbers.Count > 1)
                {
                    var formattedResult = $"[{string.Join(", ", incidentNumbers)}]";
                    
                    // Update all records in the duplicate group with the same result
                    foreach (var activityId in duplicateSet)
                    {
                        duplicateResults[activityId] = formattedResult;
                        Console.WriteLine($"Updated activity {activityId} with duplicate group: {formattedResult}");
                    }
                }
            }
            
            Console.WriteLine($"Processed {duplicateGroups.Count} duplicate groups");
            Console.WriteLine($"======================================");
        }

        private string GetIncidentNumberById(string activityId)
        {
            // This would typically query the database, but for now we'll use a simple approach
            // In a real implementation, you might want to pre-load this information
            var activity = context.Activities.Local.FirstOrDefault(a => a.Id == activityId) 
                         ?? context.Activities.FirstOrDefault(a => a.Id == activityId);
            return activity?.IncidentNumber ?? "UNKNOWN";
        }

        private async Task<List<SimilarActivityInfo>> FindSimilarActivitiesAsync(ActivityData currentActivity, List<ActivityData> allActivities)
        {
            var similarActivities = new List<SimilarActivityInfo>();
            var currentId = currentActivity.Id;
            var currentOpenDate = currentActivity.OpenDate;
            var currentUpdatedDate = currentActivity.UpdatedDate;

            // Calculate date range: 
            // - 10 days before current activity's OpenDate
            // - 10 days after current activity's UpdatedDate
            var startDate = currentOpenDate.AddDays(-10);
            var endDate = currentUpdatedDate.AddDays(10);

            Console.WriteLine($"=== DATE RANGE FOR ACTIVITY {currentId} ===");
            Console.WriteLine($"Current OpenDate: {currentOpenDate:yyyy-MM-dd}");
            Console.WriteLine($"Current UpdatedDate: {currentUpdatedDate:yyyy-MM-dd}");
            Console.WriteLine($"Search range: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
            Console.WriteLine($"Date range spans: {(endDate - startDate).TotalDays} days");
            Console.WriteLine($"Total activities to check: {allActivities.Count}");

            var comparisonTasks = new List<Task>();

            foreach (var otherActivity in allActivities)
            {
                var otherId = otherActivity.Id;
                var otherOpenDate = otherActivity.OpenDate;
                
                // Skip comparing with itself
                if (otherId == currentId)
                    continue;

                // Check if the other activity's OpenDate is within the range:
                // - 10 days before current OpenDate OR
                // - 10 days after current UpdatedDate
                if (otherOpenDate >= startDate && otherOpenDate <= endDate)
                {
                    comparisonTasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            Console.WriteLine($"=== CALCULATING SEMANTIC SIMILARITY FOR ACTIVITY {otherId} ===");
                            
                            // Calculate semantic similarity scores for each field using Ollama
                            Console.WriteLine($"Calculating LongDescription similarity...");
                            var longDescriptionSimilarity = await CalculateSemanticSimilarityAsync(
                                currentActivity.LongDescription ?? "", 
                                otherActivity.LongDescription ?? ""
                            );
                            Console.WriteLine($"  LongDescription Semantic Similarity: {longDescriptionSimilarity:P2}");

                            Console.WriteLine($"Calculating Issue_AI similarity...");
                            var issueAISimilarity = await CalculateSemanticSimilarityAsync(
                                currentActivity.Issue_AI ?? "", 
                                otherActivity.Issue_AI ?? ""
                            );
                            Console.WriteLine($"  Issue_AI Semantic Similarity: {issueAISimilarity:P2}");

                            Console.WriteLine($"Calculating System_AI similarity...");
                            var systemAISimilarity = await CalculateSemanticSimilarityAsync(
                                currentActivity.System_AI ?? "", 
                                otherActivity.System_AI ?? ""
                            );
                            Console.WriteLine($"  System_AI Semantic Similarity: {systemAISimilarity:P2}");

                            Console.WriteLine($"=== SEMANTIC SIMILARITY RESULTS FOR ACTIVITY {otherId} ===");
                            Console.WriteLine($"  LongDescription: {longDescriptionSimilarity:P2} ({GetSimilarityDescription(longDescriptionSimilarity)})");
                            Console.WriteLine($"  Issue_AI: {issueAISimilarity:P2} ({GetSimilarityDescription(issueAISimilarity)})");
                            Console.WriteLine($"  System_AI: {systemAISimilarity:P2} ({GetSimilarityDescription(systemAISimilarity)})");

                            // Check if any field has semantic similarity > 70%
                            if (longDescriptionSimilarity > 0.7 || issueAISimilarity > 0.7 || systemAISimilarity > 0.7)
                            {
                                var daysFromOpen = Math.Round((otherOpenDate - currentOpenDate).TotalDays, 1);
                                var daysFromUpdate = Math.Round((otherOpenDate - currentUpdatedDate).TotalDays, 1);
                                
                                var similarActivity = new SimilarActivityInfo
                                {
                                    ActivityId = otherActivity.Id,
                                    IncidentNumber = otherActivity.IncidentNumber ?? "N/A",
                                    LongDescriptionSimilarity = Math.Round(longDescriptionSimilarity * 100, 2),
                                    IssueAISimilarity = Math.Round(issueAISimilarity * 100, 2),
                                    SystemAISimilarity = Math.Round(systemAISimilarity * 100, 2),
                                    OpenDate = otherOpenDate,
                                    DaysDifference = daysFromOpen,
                                    DaysFromUpdatedDate = daysFromUpdate
                                };

                                lock (similarActivities)
                                {
                                    similarActivities.Add(similarActivity);
                                }

                                var timingContext = otherOpenDate < currentOpenDate ? 
                                    $"{Math.Abs(daysFromOpen)} days before current incident" :
                                    $"{daysFromOpen} days after current incident";
                                
                                Console.WriteLine($"*** FOUND SIMILAR ACTIVITY: {otherActivity.IncidentNumber} ***");
                                Console.WriteLine($"  Other OpenDate: {otherOpenDate:yyyy-MM-dd} ({timingContext})");
                                Console.WriteLine($"  LongDescription: {longDescriptionSimilarity:P2} ({GetSimilarityDescription(longDescriptionSimilarity)})");
                                Console.WriteLine($"  Issue_AI: {issueAISimilarity:P2} ({GetSimilarityDescription(issueAISimilarity)})");
                                Console.WriteLine($"  System_AI: {systemAISimilarity:P2} ({GetSimilarityDescription(systemAISimilarity)})");
                                Console.WriteLine($"  OVERALL ASSESSMENT: Meets similarity threshold (>70%)");
                            }
                            else
                            {
                                Console.WriteLine($"  OVERALL ASSESSMENT: Does not meet similarity threshold (<70%)");
                            }
                            Console.WriteLine($"=== END SEMANTIC ANALYSIS FOR ACTIVITY {otherId} ===");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error comparing activity {otherId}: {ex.Message}");
                        }
                    }));
                }
            }

            // Wait for all comparison tasks to complete
            await Task.WhenAll(comparisonTasks);

            Console.WriteLine($"Total similar activities found within date range: {similarActivities.Count}");
            Console.WriteLine("======================================");

            return similarActivities;
        }

        private string GetSimilarityDescription(double similarity)
        {
            return similarity switch
            {
                >= 0.9 => "Very High Similarity",
                >= 0.8 => "High Similarity",
                >= 0.7 => "Good Similarity",
                >= 0.6 => "Moderate Similarity",
                >= 0.4 => "Low Similarity",
                >= 0.2 => "Very Low Similarity",
                _ => "No Similarity"
            };
        }

        private async Task<double> CalculateSemanticSimilarityAsync(string source, string target)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
                return 0.0;

            if (source == target)
                return 1.0;

            using var httpClient = httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(3600);

            var requestBody = new
            {
                model = "myllaama3",
                prompt = $@"Calculate the semantic similarity between these two technical incident descriptions on a scale of 0.0 to 1.0, where:
                - 0.0 means completely different topics
                - 0.5 means somewhat related but different issues
                - 0.8 means very similar issues with minor differences
                - 1.0 means identical issues

                TEXT 1: {source}

                TEXT 2: {target}

                Analyze the semantic meaning, technical context, and core issues described in both texts.

                Return ONLY a number between 0.0 and 1.0 representing the semantic similarity score. Do not include any explanations or additional text.",
                stream = false
            };

            try
            {
                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                Console.WriteLine($"    Sending request to Ollama for similarity calculation...");
                var response = await httpClient.PostAsync("http://localhost:11434/api/generate", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var ollamaResponse = JsonSerializer.Deserialize<OllamaResponse>(responseContent, 
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    
                    var rawResult = ollamaResponse?.response ?? "0.0";
                    Console.WriteLine($"    Raw Ollama response: '{rawResult}'");
                    
                    // Extract the numeric similarity score
                    if (double.TryParse(rawResult.Trim(), out double similarity))
                    {
                        Console.WriteLine($"    Parsed similarity score: {similarity:F4}");
                        return Math.Clamp(similarity, 0.0, 1.0);
                    }
                    
                    // Fallback: try to extract number from text response
                    var match = Regex.Match(rawResult, @"[0-9]*\.?[0-9]+");
                    if (match.Success && double.TryParse(match.Value, out similarity))
                    {
                        Console.WriteLine($"    Extracted similarity score: {similarity:F4}");
                        return Math.Clamp(similarity, 0.0, 1.0);
                    }

                    Console.WriteLine($"    Could not parse similarity score from response");
                }
                else
                {
                    Console.WriteLine($"    Ollama API error: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    Error calculating semantic similarity: {ex.Message}");
            }

            return 0.0; // Default fallback
        }

        private async Task ProcessActivityAsync(
            string id, 
            string longDescription, 
            string issueAI, 
            string systemAI,
            DateTime openDate,
            DateTime updatedDate,
            string currentIncidentNumber,
            List<SimilarActivityInfo> similarActivities,
            Dictionary<string, string> duplicateResults,
            Dictionary<string, HashSet<string>> duplicateGroups,
            HashSet<string> processedRecords,
            List<ActivityData> allActivities)
        {
            try
            {
                Console.WriteLine($"=== PROCESSING ACTIVITY {id} ===");
                Console.WriteLine($"Current Incident Number: {currentIncidentNumber}");
                Console.WriteLine($"Open Date: {openDate:yyyy-MM-dd}");
                Console.WriteLine($"Updated Date: {updatedDate:yyyy-MM-dd}");
                Console.WriteLine($"Potential duplicates found within extended date range: {similarActivities.Count}");

                // Filter similar activities to only include those with all three similarity scores >= 70%
                var trueDuplicates = similarActivities
                    .Where(similar => 
                        similar.LongDescriptionSimilarity >= 70.0 &&
                        similar.IssueAISimilarity >= 70.0 &&
                        similar.SystemAISimilarity >= 70.0)
                    .ToList();

                Console.WriteLine($"True duplicates meeting 70% threshold in all categories: {trueDuplicates.Count}");

                if (trueDuplicates.Count > 0)
                {
                    // Create duplicate group including current record
                    var duplicateSet = new HashSet<string> { id };
                    var incidentNumbers = new List<string> { currentIncidentNumber };

                    // Add all true duplicates to the group
                    foreach (var duplicate in trueDuplicates)
                    {
                        var activityId = allActivities
                            .Where(a => a.IncidentNumber == duplicate.IncidentNumber)
                            .Select(a => a.Id)
                            .FirstOrDefault();
                        
                        if (!string.IsNullOrEmpty(activityId) && activityId != id)
                        {
                            duplicateSet.Add(activityId);
                            incidentNumbers.Add(duplicate.IncidentNumber);
                        }
                    }

                    // Sort incident numbers for consistent output
                    incidentNumbers.Sort();

                    // Store the duplicate group
                    lock (duplicateGroups)
                    {
                        duplicateGroups[id] = duplicateSet;
                    }

                    // Mark all records in this group as processed
                    lock (processedRecords)
                    {
                        foreach (var activityId in duplicateSet)
                        {
                            processedRecords.Add(activityId);
                        }
                    }

                    Console.WriteLine($"Created duplicate group with {duplicateSet.Count} records: [{string.Join(", ", incidentNumbers)}]");
                }
                else
                {
                    // No duplicates found
                    lock (duplicateResults)
                    {
                        duplicateResults[id] = "NO_DUPLICATE";
                    }
                    Console.WriteLine($"Activity {id} has no duplicates (no matches with >=70% similarity in all categories)");
                }

                Console.WriteLine($"=== COMPLETED PROCESSING ACTIVITY {id} ===");
            }
            catch (Exception ex)
            {
                var errorMessage = $"PROCESSING_ERROR: {ex.Message}";
                Console.WriteLine($"Exception for activity {id}: {errorMessage}");
                lock (duplicateResults)
                {
                    duplicateResults[id] = errorMessage;
                }
            }
        }

        private async Task BatchUpdateActivityDuplicateResults(Dictionary<string, string> duplicateResults)
        {
            try
            {
                Console.WriteLine($"=== STARTING BATCH UPDATE ===");
                Console.WriteLine($"Total duplicate results to process: {duplicateResults.Count}");
                
                // Debug: Show all results before update
                Console.WriteLine("=== DUPLICATE RESULTS TO UPDATE ===");
                foreach (var (id, result) in duplicateResults)
                {
                    Console.WriteLine($"ID: {id}, Result: '{result}'");
                }
                Console.WriteLine("============================");
                
                if (duplicateResults.Count == 0)
                {
                    Console.WriteLine("No duplicate results to update - skipping batch update");
                    return;
                }

                // Get all activities that need updating
                var activityIds = duplicateResults.Keys.ToList();
                var activitiesToUpdate = await context.Activities
                    .Where(a => activityIds.Contains(a.Id))
                    .ToListAsync();

                Console.WriteLine($"Found {activitiesToUpdate.Count} activities in database to update");

                int updatedCount = 0;
                foreach (var activity in activitiesToUpdate)
                {
                    if (duplicateResults.TryGetValue(activity.Id, out var result))
                    {
                        Console.WriteLine($"=== UPDATING ACTIVITY {activity.Id} ===");
                        Console.WriteLine($"Current Duplicate_AI: '{activity.Duplicate_AI}'");
                        Console.WriteLine($"New duplicate result: '{result}'");
                        
                        activity.Duplicate_AI = result;
                        updatedCount++;
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

        // Data transfer object for activity data
        public class ActivityData
        {
            public string Id { get; set; } = string.Empty;
            public string LongDescription { get; set; } = string.Empty;
            public string Issue_AI { get; set; } = string.Empty;
            public string System_AI { get; set; } = string.Empty;
            public string IncidentNumber { get; set; } = string.Empty;
            public DateTime OpenDate { get; set; }
            public DateTime UpdatedDate { get; set; }
        }

        // Helper class for similar activity information
        public class SimilarActivityInfo
        {
            public string ActivityId { get; set; } = string.Empty;
            public string IncidentNumber { get; set; } = string.Empty;
            public double LongDescriptionSimilarity { get; set; }
            public double IssueAISimilarity { get; set; }
            public double SystemAISimilarity { get; set; }
            public DateTime OpenDate { get; set; }
            public double DaysDifference { get; set; }
            public double DaysFromUpdatedDate { get; set; }
        }

        // Ollama response class
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