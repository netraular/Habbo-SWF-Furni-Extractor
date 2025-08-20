// Ruta: SimpleExtractor/Chroma/FileUtil.cs
using System;
using System.IO;
using System.Xml;

namespace Chroma
{
    public class FileUtil
    {
        public static XmlDocument? SolveXmlFile(string directory, string fileNameContains)
        {
            if (!Directory.Exists(directory)) return null;

            foreach (var file in Directory.GetFiles(directory, "*.xml"))
            {
                if (Path.GetFileNameWithoutExtension(file).Contains(fileNameContains, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var text = File.ReadAllText(file);
                        
                        if (text.Contains("\n<?xml")) text = text.Replace("\n<?xml", "<?xml");
                        if (text.Contains("<graphics>"))
                        {
                            text = text.Replace("<graphics>", "").Replace("</graphics>", "");
                        }
                        
                        XmlDocument xmlDoc = new XmlDocument();
                        xmlDoc.LoadXml(text);
                        return xmlDoc;
                    }
                    catch { /* Ignorar errores de carga de XML */ }
                }
            }
            return null;
        }

        public static string? SolveFile(string directory, string fileNameContains)
        {
             if (!Directory.Exists(directory)) return null;
             
            foreach (var file in Directory.GetFiles(directory, "*.png"))
            {
                if (Path.GetFileNameWithoutExtension(file).Equals(fileNameContains, StringComparison.OrdinalIgnoreCase))
                {
                    return file;
                }
            }
            return null;
        }
    }
}