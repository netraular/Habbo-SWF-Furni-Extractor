// Ruta: SimpleExtractor/Chroma/ChromaAsset.cs
using System;
using System.IO;
using System.Xml;
using System.Linq;

namespace Chroma
{
    public class ChromaAsset
    {
        public int RelativeX;
        public int RelativeY;
        public int DrawX;
        public int DrawY;
        public int Z = -1;
        public string? sourceImage;
        public string imageName;
        public bool flipH;
        public int Layer;
        public int Direction;
        public int Frame;
        public bool IsSmall;
        private ChromaFurniture chromaFurniture;
        public string? Ink;
        public bool IgnoreMouse;
        public bool Shadow;
        public string? ColourCode;
        public int Alpha = -1;

        public ChromaAsset(ChromaFurniture chromaFurniture, int x, int y, string? sourceImage, string imageName)
        {
            this.chromaFurniture = chromaFurniture;
            this.RelativeX = x;
            this.RelativeY = y;
            this.sourceImage = sourceImage;
            this.imageName = imageName;
        }

        public bool Parse()
        {
            try
            {
                string dataName = imageName.Replace(chromaFurniture.Sprite + "_", "");
                string[] data = dataName.Split('_');
                IsSmall = (data[0] == "32");
                Layer = (data[1].ToUpper().FirstOrDefault() - 64) - 1;
                Direction = chromaFurniture.IsIcon ? 0 : int.Parse(data[2]);
                Frame = chromaFurniture.IsIcon ? 0 : int.Parse(data[3]);
                
                var xmlData = FileUtil.SolveXmlFile(chromaFurniture.XmlDirectory, "visualization");
                if (xmlData == null) return false;
                
                string size = chromaFurniture.IsSmallFurni ? "32" : "64";

                // <-- CAMBIO IMPORTANTE: LÓGICA DE FALLBACK AÑADIDA -->
                // 1. Intentar encontrar la capa en la sección general de capas.
                var layerNode = xmlData.SelectSingleNode($"//visualizationData/visualization[@size='{size}']/layers/layer[@id='{this.Layer}']");

                // 2. Si no se encuentra, buscarla dentro de la dirección específica (fallback).
                if (layerNode == null)
                {
                    layerNode = xmlData.SelectSingleNode($"//visualizationData/visualization[@size='{size}']/directions/direction[@id='{chromaFurniture.RenderDirection}']/layer[@id='{this.Layer}']");
                }
                
                // Ahora, procesar el nodo de capa si se encontró en alguna de las rutas.
                if (layerNode != null)
                {
                    if (layerNode.Attributes?["z"]?.InnerText != null)
                    {
                        Z = int.Parse(layerNode.Attributes["z"]!.InnerText);
                    }
                    if (layerNode.Attributes?["ink"]?.InnerText != null) Ink = layerNode.Attributes["ink"]!.InnerText;
                    if (layerNode.Attributes?["alpha"]?.InnerText != null) Alpha = int.Parse(layerNode.Attributes["alpha"]!.InnerText);
                    if (layerNode.Attributes?["ignoreMouse"]?.InnerText != null) IgnoreMouse = layerNode.Attributes["ignoreMouse"]!.InnerText == "1";
                }

                // La Z se calcula para asegurar el orden correcto.
                Z = (Z * 1000) + Layer;
                
                if (chromaFurniture.ColourId > -1)
                {
                    var colorNode = xmlData.SelectSingleNode($"//visualizationData/visualization[@size='{size}']/colors/color[@id='{chromaFurniture.ColourId}']/colorLayer[@id='{Layer}']");
                    if (colorNode?.Attributes?["color"]?.InnerText != null)
                    {
                        ColourCode = colorNode.Attributes["color"]!.InnerText;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"      [AVISO] Error parseando asset '{imageName}': {e.Message}");
                return false;
            }
            return true;
        }

        public string? GetImagePath()
        {
            return FileUtil.SolveFile(chromaFurniture.OutputAssetsDirectory, imageName);
        }
    }
}