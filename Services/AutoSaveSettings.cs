using System.IO;
using System.Text.Json;

namespace JsonTool.Services;
public class AutoSaveSettings
{
    private static readonly string SettingsFileName = "autosave.settings.json";
    public bool IsEnabled { get; set; } = true;
    public int DebounceDelayMs { get; set; } = 3000;
    public string AutoSaveDirectory { get; set; } = string.Empty;
    public string AutoSaveSuffix { get; set; } = ".autosave.json";
    public int MaxBackupCount { get; set; } = 5;
    public bool CreateBackups { get; set; } = false;
    public bool ShowNotifications { get; set; } = true;
    public bool AutoRecoverWithoutPrompt { get; set; } = false;
    public int AutoSaveLifetimeHours { get; set; } = 24;
    public string GetAutoSaveDirectory()
    {
        if (!string.IsNullOrEmpty(AutoSaveDirectory))
        {
            return AutoSaveDirectory;
        }
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "JsonTool", "AutoSave");
    }
    public string GetAutoSaveFilePath(string? originalFilePath)
    {
        var directory = GetAutoSaveDirectory();
        
        if (string.IsNullOrEmpty(originalFilePath))
        {
            return Path.Combine(directory, $"untitled{AutoSaveSuffix}");
        }

        var fileName = Path.GetFileNameWithoutExtension(originalFilePath);
        return Path.Combine(directory, $"{fileName}{AutoSaveSuffix}");
    }
    public static string GetSettingsFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "JsonTool", SettingsFileName);
    }
    public static async Task<AutoSaveSettings> LoadAsync()
    {
        var filePath = GetSettingsFilePath();

        try
        {
            if (File.Exists(filePath))
            {
                var json = await File.ReadAllTextAsync(filePath);
                var settings = JsonSerializer.Deserialize<AutoSaveSettings>(json);
                return settings ?? new AutoSaveSettings();
            }
        }
        catch
        {
        }

        return new AutoSaveSettings();
    }
    public async Task SaveAsync()
    {
        var filePath = GetSettingsFilePath();
        var directory = Path.GetDirectoryName(filePath);

        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(this, options);
        await File.WriteAllTextAsync(filePath, json);
    }
    public void Reset()
    {
        IsEnabled = true;
        DebounceDelayMs = 3000;
        AutoSaveDirectory = string.Empty;
        AutoSaveSuffix = ".autosave.json";
        MaxBackupCount = 5;
        CreateBackups = false;
        ShowNotifications = true;
        AutoRecoverWithoutPrompt = false;
        AutoSaveLifetimeHours = 24;
    }
}