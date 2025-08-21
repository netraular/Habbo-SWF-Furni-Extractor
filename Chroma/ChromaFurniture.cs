// Ruta: SimpleExtractor/Chroma/ChromaFurniture.cs
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Chroma.Extensions;
using Color = SixLabors.ImageSharp.Color;
using System;
using Newtonsoft.Json;
using System.Numerics;
using SixLabors.ImageSharp.Formats.Gif;

namespace Chroma
{
    public class ChromaFurniture
    {
        // --- PROPIEDADES ---
        private string fileName;
        public bool IsSmallFurni;
        public int RenderState;
        public int RenderDirection;
        public int ColourId;
        public string Sprite;
        public List<ChromaAsset> Assets;
        public Image<Rgba32> DrawingCanvas = null!;
        public int CANVAS_WIDTH = 500;
        public int CANVAS_HEIGHT = 500;
        private bool CropImage = true;
        public bool IsIcon; // Esta propiedad ya existía, ahora la inicializaremos.
        public SortedDictionary<int, ChromaAnimation> Animations;
        public int HighestAnimationLayer;
        public int MaxStates { get; private set; }
        public string OutputDirectory => Path.Combine("output", Sprite);
        public string OutputAssetsDirectory => Path.Combine(OutputDirectory, "assets");
        public string XmlDirectory => Path.Combine(OutputDirectory, "xml");
        
        // --- CONSTRUCTOR ---
        // <-- CAMBIO: Añadido el parámetro 'isIcon' -->
        public ChromaFurniture(string inputFileName, bool isSmallFurni, int renderState, int renderDirection, int colourId = -1, bool isIcon = false)
        {
            this.fileName = inputFileName;
            this.IsSmallFurni = isSmallFurni;
            this.Assets = new List<ChromaAsset>();
            this.RenderState = renderState;
            this.RenderDirection = renderDirection;
            this.ColourId = colourId;
            this.Sprite = Path.GetFileNameWithoutExtension(inputFileName);
            this.Animations = new SortedDictionary<int, ChromaAnimation>();
            this.IsIcon = isIcon; // <-- CAMBIO: Se asigna el valor del parámetro.
        }

        // El resto del archivo permanece sin cambios...
        // --- NUEVO MÉTODO DE DETECCIÓN DE COLOR ---
        public List<int> GetAvailableColorIds()
        {
            var colorIds = new List<int>();
            var xmlData = FileUtil.SolveXmlFile(XmlDirectory, "visualization");
            if (xmlData == null) return colorIds;

            string size = IsSmallFurni ? "32" : "64";
            XmlNodeList? colorNodes = xmlData.SelectNodes($"//visualizationData/visualization[@size='{size}']/colors/color");
            
            if (colorNodes != null)
            {
                foreach (XmlNode colorNode in colorNodes)
                {
                    if (colorNode.Attributes?["id"] != null && int.TryParse(colorNode.Attributes["id"]!.InnerText, out int id))
                    {
                        colorIds.Add(id);
                    }
                }
            }
            return colorIds;
        }

        // --- MÉTODOS DE INICIALIZACIÓN ---
        public void Run()
        {
            DrawingCanvas = new Image<Rgba32>(CANVAS_WIDTH, CANVAS_HEIGHT, Color.Transparent);
            GenerateAnimations();
            GenerateAssets();
        }
        
        private void GenerateAssets()
        {
            var xmlData = FileUtil.SolveXmlFile(XmlDirectory, "assets");
            if (xmlData == null) return;
            XmlNodeList? assets = xmlData.SelectNodes("//assets/asset");
            if (assets == null) return;
            foreach (XmlNode assetNode in assets)
            {
                var x = int.Parse(assetNode.Attributes?["x"]?.InnerText ?? "0");
                var y = int.Parse(assetNode.Attributes?["y"]?.InnerText ?? "0");
                string? imageName = assetNode.Attributes?["name"]?.InnerText;
                if (string.IsNullOrEmpty(imageName)) continue;
                // Esta lógica ahora funcionará correctamente gracias al constructor
                if (!IsIcon && imageName.Contains("_icon_")) continue;
                if (IsIcon && !imageName.Contains("_icon_")) continue;
                string? source = assetNode.Attributes?["source"]?.InnerText;
                var chromaAsset = new ChromaAsset(this, x, y, source, imageName);
                if (chromaAsset.Parse())
                {
                    chromaAsset.flipH = assetNode.Attributes?["flipH"]?.InnerText == "1";
                    if (imageName.Contains("_sd_")) { chromaAsset.Shadow = true; chromaAsset.Z = int.MinValue; }
                    Assets.Add(chromaAsset);
                }
            }
            HighestAnimationLayer = Assets.Count > 0 ? Assets.Max(x => x.Layer) + 1 : 0;
        }

        // --- MÉTODOS DE RENDERIZADO ESTÁTICO ---
        private List<ChromaAsset> CreateBuildQueue()
        {
            if (RenderState > MaxStates) RenderState = 0;

            var candidates = Assets.Where(x => x.IsSmall == IsSmallFurni && x.Direction == RenderDirection).ToList();
            var renderFrames = new List<ChromaAsset>();

            for (int layer = 0; layer < this.HighestAnimationLayer; layer++)
            {
                int frameId = 0;
                if (Animations.ContainsKey(layer) && Animations[layer].States.ContainsKey(RenderState) && Animations[layer].States[RenderState].Frames.Any())
                {
                    frameId = int.Parse(Animations[layer].States[RenderState].Frames[0]);
                }
                
                // <-- ***** CORRECCIÓN CLAVE ***** -->
                // Antes: Se usaba FirstOrDefault, lo que solo seleccionaba un asset por capa.
                // Ahora: Se usa Where y AddRange para añadir TODOS los assets de la capa,
                // permitiendo renderizar la base y la máscara de color del icono juntas.
                var assetsForLayer = candidates.Where(a => a.Layer == layer && a.Frame == frameId && !a.Shadow);
                renderFrames.AddRange(assetsForLayer);
            }

            // Añadimos las sombras al final. Su Z-index (int.MinValue) las pondrá debajo.
            renderFrames.AddRange(candidates.Where(a => a.Shadow));
            
            return renderFrames.OrderBy(x => x.Z).ToList();
        }

        public byte[]? CreateImage()
        {
            using var image = RenderSingleFrame();
            return image?.ToByteArray();
        }
        
        private Image<Rgba32>? RenderSingleFrame()
        {
            var buildQueue = CreateBuildQueue();
            if (buildQueue == null || buildQueue.Count == 0) return null;

            var origin = new Point(CANVAS_WIDTH / 2, CANVAS_HEIGHT / 2);
            var canvas = DrawingCanvas.Clone();

            foreach (var asset in buildQueue)
            {
                var assetPath = asset.GetImagePath();
                if (string.IsNullOrEmpty(assetPath) || !File.Exists(assetPath)) continue;
                using (var image = Image.Load<Rgba32>(assetPath))
                {
                    int finalRelativeX = asset.RelativeX;
                    if (asset.flipH) { finalRelativeX = image.Width - asset.RelativeX; }
                    
                    var location = new Point(origin.X - finalRelativeX, origin.Y - asset.RelativeY);
                    
                    if (asset.Alpha != -1) TintImage(image, "FFFFFF", (byte)asset.Alpha);
                    if (asset.ColourCode != null) TintImage(image, asset.ColourCode, 255);
                    if (asset.Shadow) image.Mutate(ctx => ctx.Opacity(0.4f));

                    var blendMode = (asset.Ink == "ADD" || asset.Ink == "33") ? PixelColorBlendingMode.Add : PixelColorBlendingMode.Normal;
                    var options = new DrawingOptions { GraphicsOptions = { ColorBlendingMode = blendMode } };
                    canvas.Mutate(ctx => ctx.DrawImage(image, location, options.GraphicsOptions));
                }
            }
            
            return CropImage ? ImageUtil.TrimImage(canvas, Color.Transparent) : canvas;
        }

        // --- LÓGICA DE ANIMACIÓN ---
        public void GenerateAnimationGif(string outputGifPath)
        {
            var bestAnimationState = Animations.Values.SelectMany(anim => anim.States).OrderByDescending(state => state.Value.Frames.Count).FirstOrDefault();
            if (bestAnimationState.Value == null || bestAnimationState.Value.Frames.Count <= 1)
            {
                Console.WriteLine("      Este mueble no tiene secuencias de animación con más de un frame.");
                return;
            }

            int animationId = bestAnimationState.Key;
            var animationSequence = bestAnimationState.Value.Frames;
            int frameRepeat = bestAnimationState.Value.FramesPerSecond > 0 ? bestAnimationState.Value.FramesPerSecond : 4;
            int frameDelay = (int)Math.Round(frameRepeat * 4.16);

            Console.WriteLine($"      Generando GIF desde animación ID={animationId} con {animationSequence.Count} frames (velocidad: {frameRepeat})...");
            
            var fullSizeFrames = new List<Image<Rgba32>>();
            for (int i = 0; i < animationSequence.Count; i++)
            {
                var frameImage = RenderAnimationFrame(i, animationId, trim: false);
                if (frameImage != null) fullSizeFrames.Add(frameImage);
            }

            if (fullSizeFrames.Count < 2)
            {
                Console.WriteLine("      No se pudieron generar suficientes frames para la animación.");
                fullSizeFrames.ForEach(f => f.Dispose());
                return;
            }

            var masterBoundingBox = FindBoundingBox(fullSizeFrames[0], Color.Transparent);
            for (int i = 1; i < fullSizeFrames.Count; i++)
            {
                masterBoundingBox = Rectangle.Union(masterBoundingBox, FindBoundingBox(fullSizeFrames[i], Color.Transparent));
            }

            using (var finalGif = new Image<Rgba32>(masterBoundingBox.Width, masterBoundingBox.Height))
            {
                var gifMetadata = finalGif.Metadata.GetGifMetadata();
                gifMetadata.RepeatCount = 0;

                foreach (var fullFrame in fullSizeFrames)
                {
                    using (var croppedFrame = fullFrame.Clone(ctx => ctx.Crop(masterBoundingBox)))
                    {
                        croppedFrame.Frames.RootFrame.Metadata.GetGifMetadata().FrameDelay = frameDelay;
                        finalGif.Frames.AddFrame(croppedFrame.Frames.RootFrame);
                    }
                }
                
                finalGif.Frames.RemoveFrame(0);
                finalGif.SaveAsGif(outputGifPath);
            }

            fullSizeFrames.ForEach(f => f.Dispose());
            Console.WriteLine($"      -> GIF de animación guardado en: {Path.GetFileName(outputGifPath)}");
        }

        private List<ChromaAsset> CreateBuildQueueForAnimationFrame(int timelineIndex, int animationId)
        {
            var candidates = Assets.Where(x => x.IsSmall == IsSmallFurni && x.Direction == RenderDirection).ToList();
            var renderFrames = new List<ChromaAsset>();
            for (int layer = 0; layer < HighestAnimationLayer; layer++)
            {
                int assetFrameId = 0;
                if (Animations.ContainsKey(layer) && Animations[layer].States.ContainsKey(animationId))
                {
                    var sequence = Animations[layer].States[animationId].Frames;
                    if (sequence.Any()) assetFrameId = int.Parse(sequence[timelineIndex % sequence.Count]);
                }
                var asset = candidates.FirstOrDefault(a => a.Layer == layer && a.Frame == assetFrameId);
                if (asset != null)
                {
                    if (!asset.Shadow)
                    {
                        renderFrames.Add(asset);
                    }
                }
            }
            return renderFrames.OrderBy(x => x.Z).ToList();
        }

        private Image<Rgba32>? RenderAnimationFrame(int timelineIndex, int animationId, bool trim = true)
        {
            var buildQueue = CreateBuildQueueForAnimationFrame(timelineIndex, animationId);
            if (buildQueue == null || buildQueue.Count == 0) return null;
            var origin = new Point(CANVAS_WIDTH / 2, CANVAS_HEIGHT / 2);
            var canvas = DrawingCanvas.Clone();
            foreach (var asset in buildQueue)
            {
                var assetPath = asset.GetImagePath();
                if (string.IsNullOrEmpty(assetPath) || !File.Exists(assetPath)) continue;
                using (var image = Image.Load<Rgba32>(assetPath))
                {
                    int finalRelativeX = asset.RelativeX;
                    if (asset.flipH) { finalRelativeX = image.Width - asset.RelativeX; }
                    var location = new Point(origin.X - finalRelativeX, origin.Y - asset.RelativeY);
                    if (asset.Alpha != -1) TintImage(image, "FFFFFF", (byte)asset.Alpha);
                    if (asset.ColourCode != null) TintImage(image, asset.ColourCode, 255);
                    if (asset.Shadow) image.Mutate(ctx => ctx.Opacity(0.4f));
                    var blendMode = (asset.Ink == "ADD" || asset.Ink == "33") ? PixelColorBlendingMode.Add : PixelColorBlendingMode.Normal;
                    var options = new DrawingOptions { GraphicsOptions = { ColorBlendingMode = blendMode } };
                    canvas.Mutate(ctx => ctx.DrawImage(image, location, options.GraphicsOptions));
                }
            }
            return trim ? ImageUtil.TrimImage(canvas, Color.Transparent) : canvas;
        }

        private Rectangle FindBoundingBox(Image<Rgba32> image, Rgba32 trimColor)
        {
            int top = image.Height - 1, bottom = 0, left = image.Width - 1, right = 0;
            bool foundPixel = false;
            
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    if (image[x, y] != trimColor)
                    {
                        if (x < left) left = x;
                        if (x > right) right = x;
                        if (y < top) top = y;
                        if (y > bottom) bottom = y;
                        foundPixel = true;
                    }
                }
            }
            if (!foundPixel) return new Rectangle(0, 0, 1, 1);
            return new Rectangle(left, top, right - left + 1, bottom - top + 1);
        }

        // --- MÉTODOS DE SOPORTE ---
        private void TintImage(Image<Rgba32> image, string colourCode, byte alpha)
        {
            if (!Color.TryParseHex("#" + colourCode, out var tintColor)) return;
            image.Mutate(x => x.ProcessPixelRowsAsVector4(row =>
            {
                var newColor = tintColor.ToPixel<Rgba32>().ToVector4();
                for (int i = 0; i < row.Length; i++)
                {
                    if (row[i].W > 0)
                    {
                        row[i].X = newColor.X * row[i].X; row[i].Y = newColor.Y * row[i].Y; row[i].Z = newColor.Z * row[i].Z; row[i].W = (alpha / 255.0f) * row[i].W;
                    }
                }
            }));
        }

        private void GenerateAnimations()
        {
            var xmlData = FileUtil.SolveXmlFile(XmlDirectory, "visualization");
            if (xmlData == null) return;
            string size = IsSmallFurni ? "32" : "64";
            XmlNodeList? animations = xmlData.SelectNodes($"//visualizationData/visualization[@size='{size}']/animations/animation");
            if (animations == null || animations.Count == 0) { MaxStates = 1; return; }
            
            this.MaxStates = animations.Cast<XmlNode>().Max(n => int.Parse(n.Attributes!["id"]!.InnerText)) + 1;
            
            foreach (XmlNode animationNode in animations)
            {
                int animId = int.Parse(animationNode.Attributes?["id"]?.InnerText ?? "0");
                foreach (XmlNode layerNode in animationNode.SelectNodes("animationLayer")!)
                {
                    int layerId = int.Parse(layerNode.Attributes?["id"]?.InnerText ?? "0");
                    if (!this.Animations.ContainsKey(layerId)) this.Animations.Add(layerId, new ChromaAnimation());
                    if (!this.Animations[layerId].States.ContainsKey(animId))
                    {
                        var frame = new ChromaFrame();
                        if (layerNode.Attributes?["frameRepeat"] != null) frame.FramesPerSecond = int.Parse(layerNode.Attributes["frameRepeat"]!.InnerText);
                        var frameSequenceNode = layerNode.SelectSingleNode("sequence") ?? layerNode.SelectSingleNode("frameSequence");
                        if (frameSequenceNode != null)
                        {
                            foreach (XmlNode frameNode in frameSequenceNode.SelectNodes("frame")!)
                            {
                                frame.Frames.Add(frameNode.Attributes!["id"]!.InnerText);
                            }
                        }
                        this.Animations[layerId].States.Add(animId, frame);
                    }
                }
            }
        }
    }
}