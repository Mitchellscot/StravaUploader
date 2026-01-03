using System;
using System.IO;
using System.Threading.Tasks;
using StravaUploader;

Console.OutputEncoding = System.Text.Encoding.UTF8;

Console.WriteLine("=== Strava Uploader ===");
Console.WriteLine();

var configManager = new ConfigurationManager();
var uploadTracker = new UploadTracker();
var uploader = new StravaFileUploader(configManager, uploadTracker);

try
{
    await uploader.UploadAllFilesAsync();
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Fatal error: {ex.Message}");
    Console.ResetColor();
    return 1;
}

Console.WriteLine();
Console.WriteLine("Press any key to exit...");
Console.ReadKey();
return 0;
