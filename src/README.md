# Strava Uploader

A C# console application that automatically uploads .fit files from the Resources directory to Strava.

## Features

- **Automatic Upload**: Simply place .fit files in the Resources directory and run the program
- **Upload Tracking**: Tracks successfully uploaded files to prevent duplicates
- **Retry Logic**: Automatically retries failed uploads up to 3 times
- **Status Monitoring**: Monitors upload processing status and confirms successful completion
- **Progress Reporting**: Shows detailed progress for each file with color-coded output
- **Batch Processing**: Handles thousands of files efficiently

## Setup

### Prerequisites

- .NET 10.0 SDK
- Strava API access token with `activity:write` scope

### Getting a Strava Access Token

1. Go to https://www.strava.com/settings/api
2. Create an application if you haven't already
3. Use the OAuth flow to get an access token with `activity:write` scope
4. Keep your access token secure

## Usage

1. **Place Files**: Copy your .fit files into the `Resources` directory (next to the executable)

2. **Run the Application**:
   ```bash
   dotnet run --project src
   ```
   Or run the compiled executable:
   ```bash
   .\bin\Debug\net10.0\StravaUploader.exe
   ```

3. **Enter Token**: On first run, you'll be prompted to enter your Strava access token
   - The token will be saved to `strava_config.json` for future runs
   - Keep this file secure and don't commit it to version control

4. **Monitor Progress**: The application will:
   - Show each file being processed
   - Display upload progress and status checks
   - Skip files that were previously uploaded
   - Retry failed uploads automatically

5. **Review Summary**: At the end, you'll see:
   - Number of successful uploads
   - Number of skipped files (previously uploaded)
   - Number of failed uploads
   - Total count of all uploaded files

## File Management

### Configuration Files

- **strava_config.json**: Stores your Strava access token (created on first run)
- **uploaded_files.json**: Tracks successfully uploaded files to prevent duplicates

### Resources Directory

The application looks for .fit files in the `Resources` directory relative to the executable. Make sure your files are in this location:
```
StravaUploader/
??? bin/Debug/net10.0/
?   ??? StravaUploader.exe
?   ??? Resources/
?   ?   ??? Ride_2020-10-09_18-24-05.fit
?   ?   ??? Ride_2020-10-12_16-37-06.fit
?   ?   ??? ...
?   ??? strava_config.json (created on first run)
?   ??? uploaded_files.json (created after first upload)
```

## Configuration

### Activity Type Detection

By default, the application **automatically detects the activity type from the .FIT file**. Your .FIT files contain metadata about the activity type (Run, Ride, Swim, Hike, etc.), and Strava will read this automatically.

**To override the activity type** (force all uploads to be a specific type):

1. Open `strava_config.json` (in the same directory as the executable)
2. Add the `ActivityTypeOverride` property:

```json
{
  "AccessToken": "your_token_here",
  "ActivityTypeOverride": "Ride"
}
```

**Supported activity types**: Ride, Run, Swim, Hike, Walk, AlpineSki, BackcountrySki, Canoeing, Crossfit, EBikeRide, Elliptical, Golf, Handcycle, IceSkate, InlineSkate, Kayaking, Kitesurf, NordicSki, RockClimbing, RollerSki, Rowing, Sail, Skateboard, Snowboard, Snowshoe, Soccer, StairStepper, StandUpPaddling, Surfing, Velomobile, VirtualRide, VirtualRun, WeightTraining, Wheelchair, Windsurf, Workout

### Constants (in StravaFileUploader.cs)

You can modify these constants if needed:

```csharp
private const int MaxRetries = 3;                    // Number of retry attempts for failed uploads
private const int RetryDelaySeconds = 5;             // Delay between retry attempts
private const int StatusCheckIntervalSeconds = 2;    // How often to check upload status
private const int MaxStatusCheckAttempts = 30;       // Maximum status checks (60 seconds total)
```

## Troubleshooting

### "No .fit files found"
- Ensure files are in the `Resources` directory
- Check that files have the `.fit` extension
- Verify the directory exists next to the executable

### "Upload failed"
- Check your access token is valid and has `activity:write` scope
- Verify your internet connection
- Check Strava API rate limits (600 requests per 15 minutes)
- Review the error message for specific details

### "Processing timed out"
- Strava may be experiencing delays
- The file will be retried on the next run
- Consider increasing `MaxStatusCheckAttempts`

### Rate Limiting
Strava has the following API limits:
- Short term: 600 requests per 15 minutes
- Long term: 30,000 requests per day

The application processes files sequentially to respect these limits.

## Safety Features

- ? Duplicate prevention via upload tracking
- ? Automatic retry on failure
- ? Status verification for each upload
- ? Detailed error logging
- ? Graceful handling of API errors
- ? Secure token storage

## Building from Source

```bash
cd src
dotnet build
dotnet run
```

## Notes

- **Activity types are automatically detected** from the .FIT file metadata
- Different files can have different activity types (Run, Ride, Swim, etc.) - all handled automatically
- You can optionally override to force all uploads to a specific type via `strava_config.json`
- The file name is used as the external ID to help with tracking
- Upload tracking is file-name based (case-insensitive)
- The application processes files in alphabetical order

## Dependencies

- com.strava.v3.api v5.0.5 (NuGet package)
- .NET 10.0

## License

This project uses the Strava API. Make sure to comply with Strava's API Agreement and Terms of Service.
