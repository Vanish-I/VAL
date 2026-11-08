using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using VAL.Core.Models;

namespace VAL.Infrastructure.Mods;

public class ModScanner
{
    public List<ModInfo> Scan(string directory, string source)
    {
        var result = new List<ModInfo>();

        if (!Directory.Exists(directory))
            return result;

        foreach (var folder in Directory.GetDirectories(directory))
        {
            string aboutFolder = Path.Combine(folder, "About");
            string aboutPath = Path.Combine(aboutFolder, "About.xml");

            if (!File.Exists(aboutPath))
                continue;

            try
            {
                var xml = XDocument.Load(aboutPath);
                var metadata = xml.Root;

                if (metadata == null)
                    continue;

                // Ищем обложку мода
                string previewPath = FindPreview(aboutFolder);

                // Получаем поддерживаемые версии RimWorld
                var supportedVersions = metadata
                    .Element("supportedVersions")?
                    .Elements("li")
                    .Select(x => x.Value.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct()
                    .ToList() ?? new List<string>();

                // Получаем идентификатор Steam Workshop
                string publishedFileIdPath = Path.Combine(
    aboutFolder,
    "PublishedFileId.txt");

                string publishedFileId = "";

                if (File.Exists(publishedFileIdPath))
                {
                    publishedFileId = File.ReadAllText(
                        publishedFileIdPath).Trim();
                }
                else
                {
                    publishedFileId =
                        metadata.Element("publishedFileId")?.Value.Trim() ?? "";
                }

                // Если ID не найден, используем имя папки Workshop
                if (string.IsNullOrWhiteSpace(publishedFileId) &&
                    source == "Workshop")
                {
                    string folderId = Path.GetFileName(folder);

                    if (long.TryParse(folderId, out _))
                        publishedFileId = folderId;
                }

                result.Add(new ModInfo
                {
                    Name = metadata.Element("name")?.Value
                           ?? Path.GetFileName(folder),

                    PackageId = metadata.Element("packageId")?.Value ?? "",

                    Version = metadata.Element("modVersion")?.Value ?? "",

                    SupportedVersions = supportedVersions,

                    Path = folder,

                    Source = source,

                    PublishedFileId = publishedFileId,

                    PreviewPath = previewPath
                });
            }
            catch (Exception)
            {
                // Пропускаем моды с повреждённым About.xml
            }
        }

        return result;
    }

    private static string FindPreview(string aboutFolder)
    {
        string[] possibleNames =
        {
            "Preview.png",
            "Preview.jpg",
            "Preview.jpeg"
        };

        foreach (string fileName in possibleNames)
        {
            string path = Path.Combine(aboutFolder, fileName);

            if (File.Exists(path))
                return path;
        }

        return "";
    }
}