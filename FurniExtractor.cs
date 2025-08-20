// Ruta: SimpleExtractor/FurniExtractor.cs
using Flazzy;
using Flazzy.Tags;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Text;
using System.Xml;
using Chroma;

namespace SimpleExtractor
{
    public class FurniExtractor
    {
        public static bool Parse(string inputFile, string outputFurniDirectory)
        {
            string furniName = Path.GetFileNameWithoutExtension(inputFile);
            string assetsDirectory = Path.Combine(outputFurniDirectory, "assets");
            string xmlDirectory = Path.Combine(outputFurniDirectory, "xml");

            Directory.CreateDirectory(assetsDirectory);
            Directory.CreateDirectory(xmlDirectory);

            var flash = new ShockwaveFlash(inputFile);
            flash.Disassemble();

            var symbolClass = flash.Tags.FirstOrDefault(t => t.Kind == TagKind.SymbolClass) as SymbolClassTag;
            if (symbolClass == null) return false;

            var symbolsMap = new Dictionary<ushort, string>();
            for (int i = 0; i < symbolClass.Ids.Count; i++)
            {
                symbolsMap.TryAdd(symbolClass.Ids[i], symbolClass.Names[i]);
            }

            // 1. Extraer datos binarios (XML y JSON)
            Console.WriteLine("   Fase 1a: Buscando datos binarios (XML/JSON)...");
            var dataTags = flash.Tags.Where(t => t.Kind == TagKind.DefineBinaryData).Cast<DefineBinaryDataTag>();
            bool jsonFound = false;

            foreach (var data in dataTags)
            {
                if (!symbolsMap.ContainsKey(data.Id)) continue;
                var name = symbolsMap[data.Id];
                var text = Encoding.Default.GetString(data.Data).Trim();
                string? filename = null;
                string targetDirectory = xmlDirectory;

                // =================================================================
                // CAMBIO CLAVE Y FINAL: Detección de JSON por contenido
                // =================================================================
                // Si el texto empieza con { y termina con }, es un archivo JSON.
                if (text.StartsWith("{") && text.EndsWith("}"))
                {
                    filename = "furni.json";
                    targetDirectory = outputFurniDirectory;
                    jsonFound = true;
                }
                // =================================================================
                else if (name.Contains("visualization")) { filename = "visualization.xml"; }
                else if (name.Contains("assets")) { filename = "assets.xml"; }
                else if (name.Contains("logic")) { filename = "logic.xml"; }
                else if (name.Contains("manifest")) { filename = "manifest.xml"; }
                
                if (filename != null)
                {
                    File.WriteAllText(Path.Combine(targetDirectory, filename), text);
                }
            }

            if(jsonFound) {
                Console.WriteLine("      -> Archivo furni.json extraído correctamente.");
            } else {
                Console.WriteLine("      [AVISO] No se encontró un archivo furni.json en este SWF.");
            }

            // 2. Extraer imágenes
            var imageTags = flash.Tags.Where(t => t.Kind == TagKind.DefineBitsLossless2).Cast<DefineBitsLossless2Tag>();
            var symbolsImages = imageTags.ToDictionary(img => img.Id);
            foreach (var entry in symbolsMap)
            {
                if (!symbolsImages.ContainsKey(entry.Key) || !entry.Value.StartsWith(furniName + "_")) continue;
                string imagePath = Path.Combine(assetsDirectory, entry.Value.Substring(furniName.Length + 1) + ".png");
                WriteImage(symbolsImages[entry.Key], imagePath);
            }

            // 3. Post-procesar assets.xml
            var assetsXml = FileUtil.SolveXmlFile(xmlDirectory, "assets");
            if (assetsXml != null)
            {
                XmlNodeList? assetNodes = assetsXml.SelectNodes("//assets/asset");
                if (assetNodes != null)
                {
                    foreach (XmlNode assetNode in assetNodes)
                    {
                        string name = assetNode.Attributes?["name"]?.InnerText!;
                        string? source = assetNode.Attributes?["source"]?.InnerText;
                        bool flipH = assetNode.Attributes?["flipH"]?.InnerText == "1";
                        if (string.IsNullOrEmpty(name)) continue;

                        if (!string.IsNullOrEmpty(source))
                        {
                            string sourcePath = Path.Combine(assetsDirectory, source + ".png");
                            string targetPath = Path.Combine(assetsDirectory, name + ".png");
                            if (File.Exists(sourcePath) && !File.Exists(targetPath))
                            {
                                if (flipH) { using (var image = Image.Load(sourcePath)) { image.Mutate(x => x.Flip(FlipMode.Horizontal)); image.SaveAsPng(targetPath); } }
                                else { File.Copy(sourcePath, targetPath); }
                            }
                        }
                        
                        if (name.Contains("_icon_a"))
                        {
                            string iconSourcePath = Path.Combine(assetsDirectory, name + ".png");
                            if (File.Exists(iconSourcePath))
                            {
                                File.Copy(iconSourcePath, Path.Combine(outputFurniDirectory, furniName + "_icon.png"), true);
                            }
                        }
                    }
                }
            }
            return true;
        }

        private static void WriteImage(DefineBitsLossless2Tag image, string path)
        {
            if (File.Exists(path)) return;
            try
            {
                System.Drawing.Color[,] table = image.GetARGBMap();
                int width = table.GetLength(0);
                int height = table.GetLength(1);
                using (var payload = new Image<Rgba32>(width, height))
                {
                    for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) payload[x, y] = new Rgba32(table[x, y].R, table[x, y].G, table[x, y].B, table[x, y].A);
                    payload.SaveAsPng(path);
                }
            }
            catch {}
        }
    }
}