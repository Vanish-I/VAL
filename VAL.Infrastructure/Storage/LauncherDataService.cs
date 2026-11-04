using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using VAL.Core.Models;

namespace VAL.Infrastructure.Storage;

public class LauncherDataService
{
    private readonly string dataDirectory;
    private readonly string dataFile;

    private readonly JsonSerializerOptions jsonOptions = new()
    {
        WriteIndented = true
    };

    public LauncherDataService()
    {
        dataDirectory = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "VAL");

        dataFile = Path.Combine(
            dataDirectory,
            "launcher-data.json");

        Directory.CreateDirectory(dataDirectory);
    }

    public LauncherSettings Load()
    {
        if (!File.Exists(dataFile))
            return new LauncherSettings();

        try
        {
            string json = File.ReadAllText(dataFile);

            return JsonSerializer.Deserialize<LauncherSettings>(
                json, jsonOptions) ?? new LauncherSettings();
        }
        catch (JsonException)
        {
            return new LauncherSettings();
        }
    }

    public void Save(LauncherSettings settings)
    {
        Directory.CreateDirectory(dataDirectory);

        string json = JsonSerializer.Serialize(
            settings, jsonOptions);

        File.WriteAllText(dataFile, json);
    }
}
