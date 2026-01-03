using com.strava.v3.api.Upload;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace StravaUploader;

public class StravaFileUploader
{
    private const string ResourcesDirectory = "Resources";
    private const int MaxRetries = 3;
    private const int RetryDelaySeconds = 5;
    private const int StatusCheckIntervalSeconds = 2;
    private const int MaxStatusCheckAttempts = 30;

    private readonly ConfigurationManager _configManager;
    private readonly UploadTracker _uploadTracker;
    private readonly RateLimitMonitor _rateLimitMonitor;
    private com.strava.v3.api.Clients.StravaClient? _client;

    public StravaFileUploader(ConfigurationManager configManager, UploadTracker uploadTracker)
    {
        _configManager = configManager;
        _uploadTracker = uploadTracker;
        _rateLimitMonitor = new RateLimitMonitor();
    }

    public async Task UploadAllFilesAsync()
    {
        string accessToken = _configManager.GetAccessToken();
        InitializeClient(accessToken);

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("Validating Strava access token...");
        Console.ResetColor();
        
        if (!await ValidateTokenAsync())
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Token validation failed. Please check your access token and try again.");
            Console.WriteLine("Make sure your token has 'activity:write' scope.");
            Console.ResetColor();
            return;
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("[OK] Token validated successfully");
        Console.ResetColor();
        Console.WriteLine();

        var files = GetFitFilesToUpload();

        if (files.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"No .fit files found in {ResourcesDirectory} directory.");
            Console.ResetColor();
            return;
        }

        Console.WriteLine($"Found {files.Count} file(s) to process.");
        Console.WriteLine();

        int successCount = 0;
        int failureCount = 0;
        int skippedCount = 0;

        for (int i = 0; i < files.Count; i++)
        {
            var file = files[i];
            string fileName = Path.GetFileName(file);

            Console.WriteLine($"[{i + 1}/{files.Count}] Processing: {fileName}");

            if (_uploadTracker.IsFileUploaded(fileName))
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"  Skipped - already uploaded previously");
                Console.ResetColor();
                skippedCount++;
                Console.WriteLine();
                continue;
            }

            bool success = await UploadFileWithRetryAsync(file);

            if (success)
            {
                successCount++;
                _uploadTracker.MarkAsUploaded(fileName);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("  [SUCCESS] Successfully uploaded");
                Console.ResetColor();
            }
            else
            {
                failureCount++;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("  [FAILED] Failed to upload");
                Console.ResetColor();
            }

            Console.WriteLine();
            
            if ((i + 1) % 10 == 0 && _rateLimitMonitor.IsNearLimit())
            {
                _rateLimitMonitor.DisplayLimitWarning();
            }
        }

        PrintSummary(successCount, failureCount, skippedCount);
    }

    private async Task<bool> ValidateTokenAsync()
    {
        if (_client is null)
        {
            return false;
        }

        try
        {
            using var httpClient = new System.Net.Http.HttpClient();
            string token = _configManager.GetAccessToken();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
            
            var response = await httpClient.GetAsync("https://www.strava.com/api/v3/athlete");
            
            _rateLimitMonitor.UpdateLimitsFromHeaders(response.Headers);
            
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("  Rate Limit Exceeded!");
                Console.WriteLine("  Strava API limits: 600 requests per 15 minutes, 30,000 per day");
                Console.WriteLine($"  Current status: {_rateLimitMonitor.GetLimitStatus()}");
                Console.WriteLine("  Please wait before trying again.");
                Console.ResetColor();
                return false;
            }
            
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("  Unauthorized - Invalid or expired access token");
                Console.ResetColor();
                return false;
            }
            
            if (!response.IsSuccessStatusCode)
            {
                string errorBody = await response.Content.ReadAsStringAsync();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  API Error ({response.StatusCode}): {errorBody}");
                Console.ResetColor();
                return false;
            }
            
            string jsonResponse = await response.Content.ReadAsStringAsync();
            
            var athlete = await _client.Athletes.GetAthleteAsync();
            if (athlete != null)
            {
                Console.WriteLine($"  Authenticated as: {athlete.FirstName} {athlete.LastName}");
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"  {_rateLimitMonitor.GetLimitStatus()}");
                Console.ResetColor();
                return true;
            }
            
            return false;
        }
        catch (System.Net.Http.HttpRequestException ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  Network error: {ex.Message}");
            Console.ResetColor();
            return false;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  Validation error: {ex.Message}");
            Console.ResetColor();
            return false;
        }
    }

    private void InitializeClient(string accessToken)
    {
        var auth = new com.strava.v3.api.Authentication.StaticAuthentication(accessToken);
        _client = new com.strava.v3.api.Clients.StravaClient(auth);
    }

    private List<string> GetFitFilesToUpload()
    {
        string resourcesPath = Path.Combine(Directory.GetCurrentDirectory(), ResourcesDirectory);

        if (!Directory.Exists(resourcesPath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: {ResourcesDirectory} directory not found at: {resourcesPath}");
            Console.ResetColor();
            return new List<string>();
        }

        var files = Directory.GetFiles(resourcesPath, "*.fit", SearchOption.TopDirectoryOnly)
            .OrderBy(f => f)
            .ToList();

        return files;
    }

    private async Task<bool> UploadFileWithRetryAsync(string filePath)
    {
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                if (attempt > 1)
                {
                    Console.WriteLine($"  Retry attempt {attempt}/{MaxRetries}...");
                    await Task.Delay(RetryDelaySeconds * 1000);
                }

                bool success = await UploadSingleFileAsync(filePath);
                return success;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  Attempt {attempt} failed: {ex.Message}");
                Console.ResetColor();

                if (attempt == MaxRetries)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  All retry attempts exhausted.");
                    Console.ResetColor();
                    return false;
                }
            }
        }

        return false;
    }

    private async Task<bool> UploadSingleFileAsync(string filePath)
    {
        if (_client is null)
        {
            throw new InvalidOperationException("Strava client not initialized.");
        }

        string fileName = Path.GetFileName(filePath);
        Console.WriteLine($"  Uploading to Strava...");

        string? activityTypeOverride = _configManager.GetActivityTypeOverride();
        
        com.strava.v3.api.Activities.ActivityType activityType;
        if (!string.IsNullOrEmpty(activityTypeOverride) && 
            Enum.TryParse<com.strava.v3.api.Activities.ActivityType>(activityTypeOverride, true, out activityType))
        {
            Console.WriteLine($"  Using configured activity type: {activityType}");
        }
        else
        {
            activityType = com.strava.v3.api.Activities.ActivityType.Unknown;
            Console.WriteLine($"  Activity type will be auto-detected from .FIT file");
        }

        var uploadStatus = await _client.Uploads.UploadActivityAsync(
            filePath,
            com.strava.v3.api.Upload.DataFormat.Fit,
            activityType
        );

        if (uploadStatus == null)
        {
            throw new InvalidOperationException("Upload failed - no response received from Strava.");
        }

        Console.WriteLine($"  Upload ID: {uploadStatus.Id} - Status: {uploadStatus.Status ?? "Unknown"}");
        Console.WriteLine($"  Current Status: {uploadStatus.CurrentStatus}");

        if (uploadStatus.CurrentStatus == CurrentUploadStatus.Error)
        {
            string errorMsg = !string.IsNullOrEmpty(uploadStatus.Error) 
                ? uploadStatus.Error 
                : "Upload error (no details provided)";
            
            if (errorMsg.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  Duplicate detected: {errorMsg}");
                Console.WriteLine($"  Treating as success");
                Console.ResetColor();
                return true;
            }
            
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  Upload Failed: {errorMsg}");
            Console.ResetColor();
            throw new InvalidOperationException($"Upload error: {errorMsg}");
        }

        if (uploadStatus.Id > 0)
        {
            bool processingSuccess = await WaitForUploadProcessingAsync(uploadStatus);
            return processingSuccess;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  Warning: Upload ID is 0 (no ID assigned)");
            Console.WriteLine($"  Status string: \"{uploadStatus.Status}\"");
            Console.ResetColor();
            
            bool isKnownStatus = uploadStatus.Status == "Your activity is still being processed." ||
                                uploadStatus.Status == "The created activity has been deleted." ||
                                uploadStatus.Status == "There was an error processing your activity." ||
                                uploadStatus.Status == "Your activity is ready.";
            
            if (uploadStatus.Status == "Your activity is ready." && uploadStatus.ActivityId != null)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("  [OK] Upload complete!");
                Console.WriteLine($"  Activity ID: {uploadStatus.ActivityId}");
                Console.ResetColor();
                return true;
            }
            else if (!isKnownStatus || string.IsNullOrEmpty(uploadStatus.Status))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  Cannot process: Upload ID is 0 and status is ambiguous");
                Console.WriteLine($"  The upload may not have been accepted by Strava");
                Console.ResetColor();
                throw new InvalidOperationException($"Upload rejected - ID=0 with unknown status: {uploadStatus.Status}");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  Cannot monitor: Upload ID is 0");
                Console.ResetColor();
                throw new InvalidOperationException($"Upload ID not assigned - Status: {uploadStatus.Status}");
            }
        }
    }

    private async Task<bool> WaitForUploadProcessingAsync(com.strava.v3.api.Upload.UploadStatus initialStatus)
    {
        if (_client is null)
        {
            return false;
        }

        string uploadId = initialStatus.Id.ToString();
        Console.WriteLine($"  Monitoring upload status (ID: {uploadId})...");

        int consecutiveEmptyResponses = 0;
        const int maxConsecutiveEmptyResponses = 5;

        for (int check = 1; check <= MaxStatusCheckAttempts; check++)
        {
            await Task.Delay(StatusCheckIntervalSeconds * 1000);

            try
            {
                var status = await _client.Uploads.CheckUploadStatusAsync(uploadId);

                if (status == null)
                {
                    Console.WriteLine($"  Status check {check}: No response");
                    consecutiveEmptyResponses++;
                    
                    if (consecutiveEmptyResponses >= maxConsecutiveEmptyResponses)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"  No status updates after {consecutiveEmptyResponses} checks");
                        Console.WriteLine($"  Upload was accepted (ID: {uploadId}) - treating as success");
                        Console.WriteLine($"  Check your Strava account to confirm the activity appears");
                        Console.ResetColor();
                        return true;
                    }
                    continue;
                }

                consecutiveEmptyResponses = 0;

                Console.Write($"  Status check {check}: ");
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($"CurrentStatus={status.CurrentStatus}, ");
                Console.ResetColor();
                Console.WriteLine($"Status=\"{status.Status}\"");

                switch (status.CurrentStatus)
                {
                    case CurrentUploadStatus.Ready:
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("  [OK] Upload complete!");
                        if (status.ActivityId != null)
                        {
                            Console.WriteLine($"  Activity ID: {status.ActivityId}");
                        }
                        Console.ResetColor();
                        return true;

                    case CurrentUploadStatus.Error:
                        string errorMsg = !string.IsNullOrEmpty(status.Error) 
                            ? status.Error 
                            : "Unknown error occurred";
                        
                        if (errorMsg.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine($"  Duplicate detected: {errorMsg}");
                            Console.ResetColor();
                            return true;
                        }
                        
                        throw new InvalidOperationException($"Processing error: {errorMsg}");

                    case CurrentUploadStatus.Processing:
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine($"  Still processing...");
                        Console.ResetColor();
                        continue;

                    default:
                        if (status.Status != null)
                        {
                            if (status.Status.Contains("ready", StringComparison.OrdinalIgnoreCase))
                            {
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine("  [OK] Upload complete!");
                                if (status.ActivityId != null)
                                {
                                    Console.WriteLine($"  Activity ID: {status.ActivityId}");
                                }
                                Console.ResetColor();
                                return true;
                            }
                            
                            if (status.Status.Contains("error", StringComparison.OrdinalIgnoreCase))
                            {
                                throw new InvalidOperationException($"Upload failed with status: {status.Status}");
                            }
                        }
                        break;
                }
            }
            catch (Exception ex) when (ex.Message.Contains("json string is null or empty"))
            {
                consecutiveEmptyResponses++;
                
                if (consecutiveEmptyResponses >= maxConsecutiveEmptyResponses)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"  Strava API returning empty responses ({consecutiveEmptyResponses} consecutive)");
                    Console.WriteLine($"  Upload was accepted (ID: {uploadId}) - treating as success");
                    Console.WriteLine($"  This is a known issue with the Strava API during processing");
                    Console.ResetColor();
                    return true;
                }
                
                if (check % 5 == 0)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"  Check {check}: Still processing (empty API response)...");
                    Console.ResetColor();
                }
                continue;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  Status check error: {ex.Message}");
                Console.ResetColor();

                if (check == MaxStatusCheckAttempts)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"  Upload timeout but file was accepted (ID: {uploadId})");
                    Console.WriteLine($"  Treating as success - verify on Strava");
                    Console.ResetColor();
                    return true;
                }
            }
        }

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"  Status check timeout after {MaxStatusCheckAttempts * StatusCheckIntervalSeconds} seconds");
        Console.WriteLine($"  Upload was accepted (ID: {uploadId}) - treating as success");
        Console.WriteLine($"  Check your Strava account to confirm");
        Console.ResetColor();
        return true;
    }

    private void PrintSummary(int successCount, int failureCount, int skippedCount)
    {
        Console.WriteLine("=== Upload Summary ===");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Successful: {successCount}");
        Console.ResetColor();
        
        if (skippedCount > 0)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"Skipped: {skippedCount}");
            Console.ResetColor();
        }
        
        if (failureCount > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Failed: {failureCount}");
            Console.ResetColor();
        }

        Console.WriteLine($"Total previously uploaded: {_uploadTracker.GetUploadedCount()}");
    }
}
