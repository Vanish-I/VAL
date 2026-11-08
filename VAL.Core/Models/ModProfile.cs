namespace VAL.Core.Models;

public class ModProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Name { get; set; } = "Новый профиль";

    public Dictionary<string, bool> EnabledMods { get; set; } = new();
}