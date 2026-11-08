namespace VAL.Core.Models;

public class LauncherSettings
{
    public string GamePath { get; set; } = "";

    public List<ModInfo> Mods { get; set; } = new();

    public List<ModProfile> Profiles { get; set; } = new();

    public string ActiveProfileId { get; set; } = "";

    public Dictionary<string, bool> CurrentConfiguration { get; set; } = new();
}