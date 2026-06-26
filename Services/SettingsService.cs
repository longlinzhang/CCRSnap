using System.IO;
using System.Text.Json;
using CCRSnap.Models;

namespace CCRSnap.Services;

public interface ISettingsService
{
    AppSettings Settings { get; }
    void Save();
    void Load();
    string SettingsPath { get; }
}

public class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public AppSettings Settings { get; private set; } = new();
    public string SettingsPath { get; }

    public SettingsService()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string dir = Path.Combine(appData, "CCRSnap");
        Directory.CreateDirectory(dir);
        SettingsPath = Path.Combine(dir, "settings.json");
        Load();
    }

    public void Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                string json = File.ReadAllText(SettingsPath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (loaded != null)
                    Settings = loaded;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"Failed to load settings: {ex.Message}");
        }

        // Set default save path if empty
        if (string.IsNullOrWhiteSpace(Settings.SavePath))
            Settings.SavePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Snap");
    }

    public void Save()
    {
        try
        {
            string dir = Path.GetDirectoryName(SettingsPath)!;
            Directory.CreateDirectory(dir);
            string json = JsonSerializer.Serialize(Settings, JsonOptions);
            File.WriteAllText(SettingsPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"Failed to save settings: {ex.Message}");
        }
    }
}
