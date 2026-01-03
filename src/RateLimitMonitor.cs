using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace StravaUploader;

public class RateLimitMonitor
{
    private int _dailyLimit;
    private int _dailyUsage;
    private int _fifteenMinLimit;
    private int _fifteenMinUsage;
    private DateTime _lastCheck = DateTime.MinValue;

    public async Task<bool> CheckRateLimitsAsync(string accessToken)
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
            
            var response = await httpClient.GetAsync("https://www.strava.com/api/v3/athlete");
            
            UpdateLimitsFromHeaders(response.Headers);
            
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return true;
        }
    }

    public void UpdateLimitsFromHeaders(System.Net.Http.Headers.HttpResponseHeaders headers)
    {
        _lastCheck = DateTime.Now;
        
        if (headers.TryGetValues("X-RateLimit-Limit", out var limitValues))
        {
            var limits = string.Join(",", limitValues).Split(',');
            if (limits.Length >= 2)
            {
                int.TryParse(limits[0], out _fifteenMinLimit);
                int.TryParse(limits[1], out _dailyLimit);
            }
        }
        
        if (headers.TryGetValues("X-RateLimit-Usage", out var usageValues))
        {
            var usage = string.Join(",", usageValues).Split(',');
            if (usage.Length >= 2)
            {
                int.TryParse(usage[0], out _fifteenMinUsage);
                int.TryParse(usage[1], out _dailyUsage);
            }
        }
    }

    public bool IsNearLimit()
    {
        if (_fifteenMinLimit == 0 || _dailyLimit == 0)
            return false;
            
        double fifteenMinPercent = (double)_fifteenMinUsage / _fifteenMinLimit;
        double dailyPercent = (double)_dailyUsage / _dailyLimit;
        
        return fifteenMinPercent > 0.8 || dailyPercent > 0.8;
    }

    public void DisplayLimitWarning()
    {
        if (_fifteenMinLimit == 0 || _dailyLimit == 0)
            return;
            
        double fifteenMinPercent = (double)_fifteenMinUsage / _fifteenMinLimit * 100;
        double dailyPercent = (double)_dailyUsage / _dailyLimit * 100;
        
        if (fifteenMinPercent > 80 || dailyPercent > 80)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine();
            Console.WriteLine("? Rate Limit Warning:");
            Console.WriteLine($"  15-min: {_fifteenMinUsage}/{_fifteenMinLimit} ({fifteenMinPercent:F1}%)");
            Console.WriteLine($"  Daily: {_dailyUsage}/{_dailyLimit} ({dailyPercent:F1}%)");
            Console.WriteLine("  Consider slowing down to avoid being rate limited.");
            Console.ResetColor();
            Console.WriteLine();
        }
    }

    public string GetLimitStatus()
    {
        if (_fifteenMinLimit == 0 || _dailyLimit == 0)
            return "Rate limits: Unknown";
            
        return $"15-min: {_fifteenMinUsage}/{_fifteenMinLimit} | Daily: {_dailyUsage}/{_dailyLimit}";
    }
}
