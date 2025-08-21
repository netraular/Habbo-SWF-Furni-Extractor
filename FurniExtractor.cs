// Ruta: SimpleExtractor/FurniExtractor.cs
using Flazzy;
using Flazzy.Tags;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Text;
using System.Xml;
using SimpleExtractor.Json;
using Newtonsoft.Json;
using System.Linq;
using System.Globalization; // Necesario para parsear floats correctamente

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

            // --- FASE 1: Extraer datos binarios (XML) ---
            Console.WriteLine("   Fase 1: Extrayendo datos binarios (XML)...");
            var dataTags = flash.Tags.Where(t => t.Kind == TagKind.DefineBinaryData).Cast<DefineBinaryDataTag>();
            foreach (var data in dataTags)
            {
                if (!symbolsMap.ContainsKey(data.Id)) continue;
                var name = symbolsMap[data.Id];
                var text = Encoding.Default.GetString(data.Data).Trim();
                string? filename = null;

                if (name.Contains("visualization")) { filename = "visualization.xml"; }
                else if (name.Contains("assets")) { filename = "assets.xml"; }
                else if (name.Contains("logic")) { filename = "logic.xml"; }
                else if (name.Contains("manifest")) { filename = "manifest.xml"; }

                if (filename != null)
                {
                    File.WriteAllText(Path.Combine(xmlDirectory, filename), text);
                }
            }

            // --- FASE 2: Extraer imágenes ---
            Console.WriteLine("   Fase 2: Extrayendo imágenes...");
            var imageTags = flash.Tags.Where(t => t.Kind == TagKind.DefineBitsLossless2).Cast<DefineBitsLossless2Tag>();
            var symbolsImages = imageTags.ToDictionary(img => img.Id);
            foreach (var entry in symbolsMap)
            {
                if (!symbolsImages.ContainsKey(entry.Key) || !entry.Value.StartsWith(furniName + "_")) continue;
                string imagePath = Path.Combine(assetsDirectory, entry.Value.Substring(furniName.Length + 1) + ".png");
                WriteImage(symbolsImages[entry.Key], imagePath);
            }

            // --- FASE 3: Post-procesar assets ---
            Console.WriteLine("   Fase 3: Post-procesando assets...");
            var assetsXmlPath = Path.Combine(xmlDirectory, "assets.xml");
            if (File.Exists(assetsXmlPath))
            {
                XmlDocument assetsXml = new XmlDocument();
                assetsXml.Load(assetsXmlPath);
                XmlNodeList? assetNodes = assetsXml.SelectNodes("//assets/asset");
                if (assetNodes != null)
                {
                    foreach (XmlNode assetNode in assetNodes)
                    {
                        string? name = assetNode.Attributes?["name"]?.InnerText;
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

            // --- FASE 4: Generar furni.json a partir de los XML ---
            Console.WriteLine("   Fase 4: Generando furni.json...");
            GenerateFurniJson(furniName, outputFurniDirectory, xmlDirectory);

            return true;
        }

        private static void GenerateFurniJson(string furniName, string outputDir, string xmlDir)
        {
            var furniData = new JsonFurniData
            {
                Type = furniName,
                Assets = new Dictionary<string, JsonAsset>(),
                Logic = new JsonLogicData(),
                Visualization = new JsonVisualizationData()
            };

            // Parse assets.xml
            var assetsDoc = Chroma.FileUtil.SolveXmlFile(xmlDir, "assets");
            if (assetsDoc != null)
            {
                var assetNodes = assetsDoc.SelectNodes("//assets/asset");
                if (assetNodes != null)
                {
                    foreach (XmlNode node in assetNodes)
                    {
                        string? name = node.Attributes?["name"]?.InnerText;
                        if (string.IsNullOrEmpty(name)) continue;

                        var asset = new JsonAsset
                        {
                            Name = name,
                            X = int.TryParse(node.Attributes?["x"]?.InnerText, out int x) ? x : 0,
                            Y = int.TryParse(node.Attributes?["y"]?.InnerText, out int y) ? y : 0,
                            Source = node.Attributes?["source"]?.InnerText,
                            FlipH = node.Attributes?["flipH"]?.InnerText == "1" ? true : null
                        };
                        furniData.Assets[name] = asset;
                    }
                }
            }

            // Parse logic.xml
            var logicDoc = Chroma.FileUtil.SolveXmlFile(xmlDir, "logic");
            if (logicDoc != null)
            {
                var objectDataNode = logicDoc.SelectSingleNode("//objectData");
                furniData.Logic.Type = objectDataNode?.Attributes?["type"]?.InnerText;

                var modelNode = objectDataNode?.SelectSingleNode("model");
                if (modelNode != null)
                {
                    var dimensionsNode = modelNode.SelectSingleNode("dimensions");
                    if (dimensionsNode?.Attributes != null)
                    {
                        furniData.Logic.Dimensions = new Dictionary<string, object>();
                        foreach (XmlAttribute attr in dimensionsNode.Attributes)
                        {
                            string value = attr.Value;
                            if (int.TryParse(value, out int intValue))
                            {
                                furniData.Logic.Dimensions[attr.Name] = intValue;
                            }
                            else if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out double doubleValue))
                            {
                                furniData.Logic.Dimensions[attr.Name] = doubleValue;
                            }
                            else
                            {
                                furniData.Logic.Dimensions[attr.Name] = value;
                            }
                        }
                    }

                    var dirNodes = modelNode.SelectNodes("directions/direction");
                    if (dirNodes != null)
                    {
                         furniData.Logic.Directions = dirNodes.Cast<XmlNode>()
                            .Select(n => int.TryParse(n.Attributes?["id"]?.InnerText, out int id) ? id : -1)
                            .Where(id => id != -1)
                            .ToList();
                    }
                }
            }

            // Parse visualization.xml
            var vizDoc = Chroma.FileUtil.SolveXmlFile(xmlDir, "visualization");
            if (vizDoc != null)
            {
                furniData.Visualization.Type = vizDoc.SelectSingleNode("//visualizationData")?.Attributes?["type"]?.InnerText;
                var vizNodes = vizDoc.SelectNodes("//visualizationData/visualization");
                if (vizNodes != null)
                {
                    foreach (XmlNode vizNode in vizNodes)
                    {
                        if (!int.TryParse(vizNode.Attributes?["size"]?.InnerText, out int size)) continue;

                        var viz = new JsonVisualization
                        {
                            Size = size,
                            LayerCount = int.TryParse(vizNode.Attributes?["layerCount"]?.InnerText, out int lc) ? lc : 0,
                            Angle = int.TryParse(vizNode.Attributes?["angle"]?.InnerText, out int ang) ? ang : 0,
                            Layers = new Dictionary<string, JsonLayer>(),
                            Directions = new Dictionary<string, JsonDirection>(),
                            Colors = new Dictionary<string, List<JsonColor>>(),
                            Animations = new Dictionary<string, JsonAnimation>()
                        };

                        // Layers (nivel superior)
                        var layerNodes = vizNode.SelectNodes("layers/layer");
                        if (layerNodes != null) foreach (XmlNode layerNode in layerNodes)
                        {
                            if (int.TryParse(layerNode.Attributes?["id"]?.InnerText, out int id))
                            {
                                viz.Layers[id.ToString()] = ParseJsonLayer(layerNode);
                            }
                        }

                        // Directions (puede contener capas anidadas)
                        var dirNodes = vizNode.SelectNodes("directions/direction");
                        if (dirNodes != null) foreach (XmlNode dirNode in dirNodes)
                        {
                            if (int.TryParse(dirNode.Attributes?["id"]?.InnerText, out int dirId))
                            {
                                var jsonDir = new JsonDirection { Id = dirId, Layers = new Dictionary<string, JsonLayer>() };
                                var dirLayerNodes = dirNode.SelectNodes("layer");
                                if (dirLayerNodes != null)
                                {
                                    foreach (XmlNode dirLayerNode in dirLayerNodes)
                                    {
                                        if (int.TryParse(dirLayerNode.Attributes?["id"]?.InnerText, out int layerId))
                                        {
                                            jsonDir.Layers[layerId.ToString()] = ParseJsonLayer(dirLayerNode);
                                        }
                                    }
                                }
                                viz.Directions[dirId.ToString()] = jsonDir;
                            }
                        }
                        
                        // Colors
                        var colorNodes = vizNode.SelectNodes("colors/color");
                        if (colorNodes != null) foreach (XmlNode colorNode in colorNodes)
                        {
                            string? colorId = colorNode.Attributes?["id"]?.InnerText;
                            if (colorId != null)
                            {
                                viz.Colors[colorId] = new List<JsonColor>();
                                var colorLayerNodes = colorNode.SelectNodes("colorLayer");
                                if (colorLayerNodes != null) foreach (XmlNode cl in colorLayerNodes)
                                {
                                    if(int.TryParse(cl.Attributes?["id"]?.InnerText, out int id))
                                    {
                                        viz.Colors[colorId].Add(new JsonColor { LayerId = id, Color = cl.Attributes?["color"]?.InnerText });
                                    }
                                }
                            }
                        }

                        // Animations
                        var animNodes = vizNode.SelectNodes("animations/animation");
                        if (animNodes != null) foreach (XmlNode animNode in animNodes)
                        {
                            if (int.TryParse(animNode.Attributes?["id"]?.InnerText, out int animId))
                            {
                                var anim = new JsonAnimation { Id = animId, Layers = new Dictionary<string, JsonAnimationLayer>() };
                                var animLayerNodes = animNode.SelectNodes("animationLayer");
                                if (animLayerNodes != null) foreach (XmlNode animLayerNode in animLayerNodes)
                                {
                                    if (int.TryParse(animLayerNode.Attributes?["id"]?.InnerText, out int layerId))
                                    {
                                        var frames = (animLayerNode.SelectNodes("frameSequence/frame")?.Cast<XmlNode>() ?? Enumerable.Empty<XmlNode>())
                                                .Select(n => int.TryParse(n.Attributes?["id"]?.InnerText, out int frameId) ? frameId : -1)
                                                .Where(fId => fId != -1)
                                                .ToList();

                                        var animLayer = new JsonAnimationLayer
                                        {
                                            Id = layerId,
                                            LoopCount = int.TryParse(animLayerNode.Attributes?["loopCount"]?.InnerText, out int loop) ? loop : null,
                                            FrameRepeat = int.TryParse(animLayerNode.Attributes?["frameRepeat"]?.InnerText, out int repeat) ? repeat : null,
                                            Frames = frames
                                        };
                                        anim.Layers[layerId.ToString()] = animLayer;
                                    }
                                }
                                viz.Animations[animId.ToString()] = anim;
                            }
                        }

                        if (viz.Size == 64) furniData.Visualization.Size64 = viz;
                        else if (viz.Size == 32) furniData.Visualization.Size32 = viz;
                        else if (viz.Size == 1) furniData.Visualization.Size1 = viz;
                    }
                }
            }
            
            var jsonSettings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Newtonsoft.Json.Formatting.Indented };
            string json = JsonConvert.SerializeObject(furniData, jsonSettings);
            File.WriteAllText(Path.Combine(outputDir, "furni.json"), json);
            Console.WriteLine("      -> Archivo furni.json generado correctamente.");
        }

        // <-- NUEVO MÉTODO HELPER PARA EVITAR REPETIR CÓDIGO -->
        private static JsonLayer ParseJsonLayer(XmlNode layerNode)
        {
            return new JsonLayer
            {
                Id = int.Parse(layerNode.Attributes!["id"]!.InnerText),
                Z = int.TryParse(layerNode.Attributes?["z"]?.InnerText, out int z) ? z : null,
                Ink = layerNode.Attributes?["ink"]?.InnerText,
                Alpha = int.TryParse(layerNode.Attributes?["alpha"]?.InnerText, out int alpha) ? alpha : null,
                IgnoreMouse = layerNode.Attributes?["ignoreMouse"]?.InnerText == "1" ? true : null
            };
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
                    for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    {
                        var p = table[x, y];
                        payload[x, y] = new Rgba32(p.R, p.G, p.B, p.A);
                    }
                    payload.SaveAsPng(path);
                }
            }
            catch {}
        }
    }
}