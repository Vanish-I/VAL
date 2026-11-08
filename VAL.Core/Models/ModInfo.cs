using System.Collections.Generic;
namespace VAL.Core.Models;

public class ModInfo
{
    public string Name { get; set; } = "";
    public string PackageId { get; set; } = "";
    public string Version { get; set; } = "";

    // Поддерживаемые версии RimWorld
    public List<string> SupportedVersions { get; set; } = new();

    public bool? IsCompatible { get; set; }

    public string Path { get; set; } = "";
    public string Source { get; set; } = "";

    public string PublishedFileId { get; set; } = "";
    public string PreviewPath { get; set; } = "";

    // Состояние активации мода
    public bool IsEnabled { get; set; } = true;
}