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
        public int Alpha = -1;
        public bool IsIconAsset; // <-- NUEVO: Indica si este asset es parte de un icono (basado en su nombre)

        public ChromaAsset(ChromaFurniture chromaFurniture, int x, int y, string? sourceImage, string imageName)
        {
            this.chromaFurniture = chromaFurniture;
            this.RelativeX = x;
            this.RelativeY = y;
            this.sourceImage = sourceImage;
            this.imageName = imageName;
        }

        // El método Parse ahora recibe el XmlDocument directamente para no depender del estado inicial de ChromaFurniture.
        public bool Parse(XmlDocument xmlData)
        {
            try
            {
                string dataName = imageName.Replace(chromaFurniture.Sprite + "_", "");
                string[] data = dataName.Split('_');
                IsSmall = (data[0] == "32");
                Layer = (data[1].ToUpper().FirstOrDefault() - 64) - 1;
                
                // <-- CAMBIO CRÍTICO: Determinamos si es un asset de icono por su nombre de imagen. -->
                // Esto es independiente del estado 'IsIcon' de ChromaFurniture en el momento de la carga.
                IsIconAsset = imageName.Contains("_icon_");

                // <-- CAMBIO CRÍTICO: La Dirección y el Frame se establecen a 0 si es un asset de icono. -->
                // Esto asegura que los assets de icono siempre se carguen con la dirección y frame correctas.
                Direction = IsIconAsset ? 0 : int.Parse(data[2]);
                Frame = IsIconAsset ? 0 : int.Parse(data[3]);
                
                // <-- CORRECCIÓN AQUÍ: El tamaño para buscar propiedades de capa debe depender de si ESTE asset
                // es un icono, no del estado global del renderizador (chromaFurniture.IsSmallFurni).
                string sizeForLayerProperties = IsIconAsset ? "1" : (IsSmall ? "32" : "64");

                var layerNode = xmlData.SelectSingleNode($"//visualizationData/visualization[@size='{sizeForLayerProperties}']/layers/layer[@id='{this.Layer}']");
                
                if (layerNode == null)
                {
                    // La búsqueda de fallback también debe usar la dirección del propio asset.
                    layerNode = xmlData.SelectSingleNode($"//visualizationData/visualization[@size='{sizeForLayerProperties}']/directions/direction[@id='{this.Direction}']/layer[@id='{this.Layer}']");
                }
                
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

                Z = (Z * 1000) + Layer;
                
                // <-- ELIMINADO: La lógica de ColourCode se movió a ChromaFurniture.RenderSingleFrame/RenderAnimationFrame -->
            }
            catch (Exception e)
            {
                SimpleExtractor.Logger.Log($"      [AVISO] [{chromaFurniture.Sprite}] Error parseando asset '{imageName}': {e.Message}");
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