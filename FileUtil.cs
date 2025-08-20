using System.Xml;

namespace SimpleExtractor
{
    public class FileUtil
    {
        public static XmlDocument? SolveXmlFile(string directory, string fileNameContains)
        {
            foreach (var file in Directory.GetFiles(directory, "*"))
            {
                if (Path.GetFileNameWithoutExtension(file).Contains(fileNameContains))
                {
                    var text = File.ReadAllText(file);

                    // Pequeñas correcciones que hacía el original
                    if (text.Contains("\n<?xml")) text = text.Replace("\n<?xml", "<?xml");
                    if (text.Contains("<graphics>"))
                    {
                        text = text.Replace("<graphics>", "");
                        text = text.Replace("</graphics>", "");
                    }
                    
                    File.WriteAllText(file, text);

                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.Load(file);
                    return xmlDoc;
                }
            }
            return null;
        }

        public static string? SolveFile(string directory, string fileNameContains)
        {
            foreach (var file in Directory.GetFiles(directory, "*"))
            {
                if (Path.GetFileNameWithoutExtension(file).EndsWith(fileNameContains))
                {
                    return file;
                }
            }
            return null;
        }
    }
}