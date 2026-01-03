using System;
using System.IO;
using System.Text.Json;

namespace StravaUploader;

public class ConfigurationManager
{
    private const string ConfigFileName = "strava_config.json";
    private readonly string _configPath;
    private Config? _config;

    public ConfigurationManager()
    {
        _configPath = Path.Combine(Directory.GetCurrentDirectory(), ConfigFileName);
    }

    public string GetAccessToken()
    {
        LoadConfig();

        if (_config == null || string.IsNullOrWhiteSpace(_config.AccessToken))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("No access token found. Please enter your Strava access token:");
            Console.ResetColor();
            Console.Write("> ");
            
            string? token = Console.ReadLine();
            
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException("Access token is required to upload files to Strava.");
            }

            _config = new Config { AccessToken = token };
            SaveConfig();
            
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Access token saved to: {_configPath}");
            Console.ResetColor();
            Console.WriteLine();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Access token loaded successfully.");
            Console.WriteLine($"Config location: {_configPath}");
            Console.ResetColor();
            Console.WriteLine();
        }

        return _config.AccessToken;
    }

    public string? GetActivityTypeOverride()
    {
        LoadConfig();
        return _config?.ActivityTypeOverride;
    }

    private void LoadConfig()
    {
        if (!File.Exists(_configPath))
        {
            return;
        }

        try
        {
            string json = File.ReadAllText(_configPath);
            _config = JsonSerializer.Deserialize<Config>(json);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Warning: Could not load config file: {ex.Message}");
            Console.ResetColor();
        }
    }

    private void SaveConfig()
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(_config, options);
            File.WriteAllText(_configPath, json);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Warning: Could not save config file: {ex.Message}");
            Console.ResetColor();
        }
    }

    private class Config
    {
        public string AccessToken { get; set; } = string.Empty;
        public string? ActivityTypeOverride { get; set; }
    }
}
