# Quick Start Guide

## First Time Setup

1. **Get your Strava API token**:
   - Visit: https://www.strava.com/settings/api
   - Create an application
   - Get an access token with `activity:write` scope

2. **Build the application**:
   ```bash
   cd src
   dotnet build
   ```

3. **Copy your .fit files**:
   - Place all your .fit files in the `src/Resources/` directory
   - If the Resources folder doesn't exist, create it in the `src/` directory

4. **Run the application** (from the src directory):
   ```bash
   cd src
   dotnet run
   ```

5. **Enter your token when prompted**
   - Paste your Strava access token
   - It will be saved for future runs

6. **Watch the magic happen!**
   - Files will be uploaded one by one
   - Progress and status will be displayed
   - Successfully uploaded files are tracked to prevent duplicates

## Subsequent Runs

Just run the application again - it will:
- Use the saved token
- Skip previously uploaded files
- Upload only new files in the Resources directory

```bash
dotnet run --project src
```

## Tips

- **For thousands of files**: The application handles this automatically with retry logic
- **If interrupted**: Just run again - it remembers what was uploaded
- **Activity types**: The application automatically detects the activity type from each .FIT file (Run, Ride, Swim, etc.)
- **Check Strava**: Activities will appear in your Strava account with their correct type
- **Errors**: Review the console output for detailed error messages

## Directory Structure After First Run

```
StravaUploader/
??? src/
?   ??? Resources/               ? Your .fit files go here
?   ??? strava_config.json       ? Auto-created (your token)
?   ??? uploaded_files.json      ? Auto-created on startup (tracks uploads)
?   ??? Program.cs
?   ??? ConfigurationManager.cs
?   ??? ...other source files
```

**Note:** `uploaded_files.json` is created immediately when you first run the application (in the `src/` directory), even before any uploads occur. Run the application from the `src/` directory using `dotnet run` and all config/tracking files will be created there alongside your source code.

## Important Notes

?? **API Limits**: Strava limits you to 600 requests per 15 minutes
?? **Security**: Keep your `strava_config.json` file secure - it contains your access token
? **Safe to re-run**: The app won't upload the same file twice
? **Automatic retry**: Failed uploads are retried up to 3 times

## Troubleshooting

**"No .fit files found"**
? Make sure files are in: `src/Resources/`
? Make sure you're running the app from the `src/` directory using `dotnet run`

**"Upload failed"**
? Check your token has `activity:write` permission
? Verify your internet connection

**"Rate Limit Exceeded"**
? Strava limits: 600 requests per 15 minutes, 30,000 requests per day
? The app will show your current usage during startup
? Wait 15 minutes before retrying if you hit the short-term limit
? The app checks every 10 uploads and warns you when approaching limits

**"Duplicate activity detected"**
? This is normal! The file was already uploaded to Strava previously
? The app treats duplicates as successful uploads and continues
? Strava automatically detects duplicate activities to prevent re-uploads

**Activity type issues**
? By default, activity type is **auto-detected** from the .FIT file
? The .FIT file contains the activity type information (Run, Ride, Swim, etc.)
? To override, edit `strava_config.json` and add: `"ActivityTypeOverride": "Run"` (or Ride, Swim, Hike, etc.)

## Need Help?

Check the full README.md for detailed documentation and troubleshooting steps.
