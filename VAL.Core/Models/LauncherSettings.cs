
namespace VAL.Core.Models;

public class LauncherSettings
{
    public string GamePath { get; set; } = "";

    public List<ModInfo> Mods { get; set; } = new();
}
