// Ruta: SimpleExtractor/Program.cs
using System.Diagnostics;
using Chroma;

namespace SimpleExtractor
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Iniciando extractor avanzado de furnis...");

            string inputDirectory = "swfs";
            string outputDirectory = "output";

            Directory.CreateDirectory(inputDirectory);
            Directory.CreateDirectory(outputDirectory);

            var swfFiles = Directory.GetFiles(inputDirectory, "*.swf");
            if (swfFiles.Length == 0)
            {
                Console.WriteLine($"No se encontraron archivos .swf en la carpeta '{inputDirectory}'.");
                return;
            }

            Console.WriteLine($"Se encontraron {swfFiles.Length} archivos .swf para procesar.");
            var stopwatch = Stopwatch.StartNew();

            foreach (var swfFile in swfFiles)
            {
                string furniName = Path.GetFileNameWithoutExtension(swfFile);
                string furniOutputDirectory = Path.Combine(outputDirectory, furniName);

                try
                {
                    Console.WriteLine($"\n-> Procesando: {furniName}");

                    // --- FASE 1: Extracción y Detección de Colores ---
                    Console.WriteLine("   Fase 1: Extrayendo assets y detectando colores...");
                    FurniExtractor.Parse(swfFile, furniOutputDirectory);
                    
                    var colorDetector = new ChromaFurniture(swfFile, isSmallFurni: false, renderState: 0, renderDirection: 0);
                    var availableColorIds = colorDetector.GetAvailableColorIds();
                    if (!availableColorIds.Any()) availableColorIds.Add(-1); // Añadir un "no color" por defecto

                    // --- BUCLE PRINCIPAL POR CADA COLOR ---
                    foreach (int colorId in availableColorIds)
                    {
                        if (colorId > -1) Console.WriteLine($"   --- Procesando Color ID: {colorId} ---");

                        // --- FASE 2: Renderizado de imágenes estáticas ---
                        Console.WriteLine("   Fase 2: Renderizando imágenes estáticas...");
                        string renderedDir = Path.Combine(furniOutputDirectory, "rendered");
                        Directory.CreateDirectory(renderedDir);
                        int[] directionsToRender = { 0, 2, 4, 6 };
                        foreach (int direction in directionsToRender)
                        {
                            var furniture = new ChromaFurniture(swfFile, isSmallFurni: false, renderState: 0, renderDirection: direction, colourId: colorId);
                            furniture.Run();
                            byte[]? imageData = furniture.CreateImage();
                            if (imageData != null)
                            {
                                string filename = $"{furniName}_dir_{direction}";
                                if (colorId > -1) filename += $"_color_{colorId}";
                                File.WriteAllBytes(Path.Combine(renderedDir, filename + ".png"), imageData);
                            }
                        }

                        // --- FASE 3: Generación de GIF animado ---
                        Console.WriteLine("   Fase 3: Buscando y generando animaciones...");
                        string animationDir = Path.Combine(furniOutputDirectory, "animations");
                        Directory.CreateDirectory(animationDir);
                        
                        var animFurniture = new ChromaFurniture(swfFile, isSmallFurni: false, renderState: 0, renderDirection: 2, colourId: colorId);
                        animFurniture.Run();
                        
                        string gifFilename = $"{furniName}_animation";
                        if (colorId > -1) gifFilename += $"_color_{colorId}";
                        animFurniture.GenerateAnimationGif(Path.Combine(animationDir, gifFilename + ".gif"));
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"   Error al procesar el archivo {furniName}: {ex.Message}\n{ex.StackTrace}");
                    Console.ResetColor();
                }
            }

            stopwatch.Stop();
            Console.WriteLine($"\n¡Proceso completado en {stopwatch.Elapsed.TotalSeconds:F2} segundos!");
            Console.WriteLine($"Los archivos extraídos se encuentran en la carpeta '{outputDirectory}'.");
        }
    }
}