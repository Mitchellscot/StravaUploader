using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace StravaUploader;

public class UploadTracker
{
    private const string TrackerFileName = "uploaded_files.json";
    private readonly string _trackerPath;
    private HashSet<string> _uploadedFiles;

    public UploadTracker()
    {
        _trackerPath = Path.Combine(Directory.GetCurrentDirectory(), TrackerFileName);
        _uploadedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        LoadTrackedFiles();
        
        if (!File.Exists(_trackerPath))
        {
            SaveTrackedFiles();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"Created upload tracker at: {_trackerPath}");
            Console.ResetColor();
            Console.WriteLine();
        }
    }

    public bool IsFileUploaded(string fileName)
    {
        return _uploadedFiles.Contains(fileName);
    }

    public void MarkAsUploaded(string fileName)
    {
        _uploadedFiles.Add(fileName);
        SaveTrackedFiles();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"  Marked as uploaded in tracker: {fileName}");
        Console.ResetColor();
    }

    public int GetUploadedCount()
    {
        return _uploadedFiles.Count;
    }

    private void LoadTrackedFiles()
    {
        if (!File.Exists(_trackerPath))
        {
            return;
        }

        try
        {
            string json = File.ReadAllText(_trackerPath);
            var files = JsonSerializer.Deserialize<List<string>>(json);
            
            if (files != null)
            {
                _uploadedFiles = new HashSet<string>(files, StringComparer.OrdinalIgnoreCase);
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"Loaded {_uploadedFiles.Count} previously uploaded files from tracker.");
                Console.WriteLine($"Tracker location: {_trackerPath}");
                Console.ResetColor();
                Console.WriteLine();
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Warning: Could not load upload tracker: {ex.Message}");
            Console.ResetColor();
        }
    }

    private void SaveTrackedFiles()
    {
        try
        {
            string? directory = Path.GetDirectoryName(_trackerPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(_uploadedFiles.ToList(), options);
            File.WriteAllText(_trackerPath, json);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"ERROR: Could not save upload tracker to {_trackerPath}");
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            Console.ResetColor();
        }
    }
}
