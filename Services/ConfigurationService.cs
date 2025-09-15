using Newtonsoft.Json;

namespace ChatSupporter.Services;

public class ConfigurationService
{
    private readonly string _configPath;
    private AppSettings? _settings;

    public ConfigurationService(string configPath = "appsettings.json")
    {
        _configPath = configPath;
        LoadConfiguration();
    }

    public AppSettings Settings => _settings ?? throw new InvalidOperationException("Configuration not loaded");

    private void LoadConfiguration()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                _settings = JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
            }
            else
            {
                _settings = new AppSettings();
                SaveConfiguration();
            }
        }
        catch (Exception)
        {
            _settings = new AppSettings();
        }
    }

    public void SaveConfiguration()
    {
        try
        {
            if (_settings != null)
            {
                var json = JsonConvert.SerializeObject(_settings, Formatting.Indented);
                File.WriteAllText(_configPath, json);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to save configuration: {ex.Message}");
        }
    }

    public void UpdateSetting<T>(string section, string key, T value)
    {
        if (_settings == null) return;

        var property = _settings.GetType().GetProperty(section);
        if (property?.GetValue(_settings) is not object sectionObject) return;

        var keyProperty = sectionObject.GetType().GetProperty(key);
        keyProperty?.SetValue(sectionObject, value);

        SaveConfiguration();
    }
}

public class AppSettings
{
    public GoogleAppsScriptSettings GoogleAppsScript { get; set; } = new();
    public ChatSettings Chat { get; set; } = new();
    public ClaimSettings Claim { get; set; } = new();
    public AISettings AI { get; set; } = new();
    public UISettings UI { get; set; } = new();
    public CustomerDefaultsSettings CustomerDefaults { get; set; } = new();
}

public class GoogleAppsScriptSettings
{
    public string ChatApiUrl { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public int MaxRetries { get; set; } = 3;
    public int TimeoutSeconds { get; set; } = 30;
}

public class ChatSettings
{
    public int MaxMessageLength { get; set; } = 1000;
    public int MaxHistoryCount { get; set; } = 100;
    public bool AutoSaveEnabled { get; set; } = true;
    public int RefreshIntervalSeconds { get; set; } = 5;
}

public class ClaimSettings
{
    public string[] Categories { get; set; } = Array.Empty<string>();
    public string[] Priorities { get; set; } = Array.Empty<string>();
}

public class AISettings
{
    public bool LearningEnabled { get; set; } = true;
    public double MinConfidenceScore { get; set; } = 0.7;
    public int MaxSimilarQuestions { get; set; } = 5;
}

public class UISettings
{
    public int WindowWidth { get; set; } = 800;
    public int WindowHeight { get; set; } = 600;
    public int FontSize { get; set; } = 10;
    public string Theme { get; set; } = "Light";
}

public class CustomerDefaultsSettings
{
    public string LastEmail { get; set; } = string.Empty;
    public string[] RecentEmails { get; set; } = Array.Empty<string>();
}