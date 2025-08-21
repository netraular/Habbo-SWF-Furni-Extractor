// Ruta: SimpleExtractor/Program.cs
using System.Diagnostics;
using Chroma;
using System.Threading.Tasks;

namespace SimpleExtractor
{
    class Program
    {
        static void Main(string[] args)
        {
            bool isSequential = false;

            // --- FASE 0: PARSEO DE ARGUMENTOS ---
            if (args.Contains("--help"))
            {
                ShowHelp();
                return;
            }
            // Configura el Logger global
            Logger.IsVerbose = args.Contains("--verbose");
            isSequential = args.Contains("--sequential");


            Logger.Info("Iniciando extractor avanzado de furnis...");
            Logger.Log($" - Modo Verboso: {(Logger.IsVerbose ? "Activado" : "Desactivado")}");
            Logger.Log($" - Modo Paralelo: {(!isSequential ? "Activado" : "Desactivado")}");


            string inputDirectory = "swfs";
            string outputDirectory = "output";

            Directory.CreateDirectory(inputDirectory);
            Directory.CreateDirectory(outputDirectory);

            var swfFiles = Directory.GetFiles(inputDirectory, "*.swf");
            if (swfFiles.Length == 0)
            {
                Logger.Info($"No se encontraron archivos .swf en la carpeta '{inputDirectory}'.");
                return;
            }

            Logger.Info($"\nSe encontraron {swfFiles.Length} archivos .swf para procesar.");
            var stopwatch = Stopwatch.StartNew();

            if (isSequential)
            {
                Logger.Info("Ejecutando en modo secuencial...");
                foreach (var swfFile in swfFiles)
                {
                    ProcessSingleSwf(swfFile);
                }
            }
            else
            {
                Logger.Info("Ejecutando en modo paralelo...");
                Parallel.ForEach(swfFiles, swfFile =>
                {
                    ProcessSingleSwf(swfFile);
                });
            }

            stopwatch.Stop();
            Logger.Info($"\n¡Proceso completado en {stopwatch.Elapsed.TotalSeconds:F2} segundos!");
            Logger.Info($"Los archivos extraídos se encuentran en la carpeta '{outputDirectory}'.");
        }

        private static void ProcessSingleSwf(string swfFile)
        {
            string furniName = Path.GetFileNameWithoutExtension(swfFile);
            string furniOutputDirectory = Path.Combine("output", furniName);
            
            // <-- LÍNEA ELIMINADA: Logger.Info($"-> Procesando: {furniName}..."); -->

            try
            {
                Logger.Log($"   [{furniName}] Fase 1: Extrayendo assets y detectando colores...");
                FurniExtractor.Parse(swfFile, furniOutputDirectory);
                
                var colorDetector = new ChromaFurniture(swfFile, isSmallFurni: false, renderState: 0, renderDirection: 0);
                colorDetector.Run();
                var availableColorIds = colorDetector.GetAvailableColorIds();
                if (!availableColorIds.Any()) availableColorIds.Add(-1);

                foreach (int colorId in availableColorIds)
                {
                    if (colorId > -1) Logger.Log($"   [{furniName}] --- Procesando Color ID: {colorId} ---");

                    foreach (bool renderWithShadows in new[] { true, false })
                    {
                        Logger.Log($"      [{furniName}] --- Procesando Sombra: {(renderWithShadows ? "Sí" : "No")} ---");

                        Logger.Log($"      [{furniName}] Fase 2: Renderizando imágenes estáticas...");
                        string renderedDir = Path.Combine(furniOutputDirectory, "rendered");
                        Directory.CreateDirectory(renderedDir);
                        int[] directionsToRender = { 0, 2, 4, 6 };

                        foreach (int direction in directionsToRender)
                        {
                            var furniture = new ChromaFurniture(swfFile, isSmallFurni: false, renderState: 0, renderDirection: direction, colourId: colorId, renderShadows: renderWithShadows);
                            furniture.Run();
                            byte[]? imageData = furniture.CreateImage();
                            if (imageData != null)
                            {
                                string filename = $"{furniName}_dir_{direction}";
                                if (colorId > -1) filename += $"_color_{colorId}";
                                if (!renderWithShadows) filename += "_no_sd";

                                File.WriteAllBytes(Path.Combine(renderedDir, filename + ".png"), imageData);
                            }
                        }

                        Logger.Log($"      [{furniName}] Fase 3: Buscando y generando animaciones...");
                        string animationDir = Path.Combine(furniOutputDirectory, "animations");
                        Directory.CreateDirectory(animationDir);
                        
                        var animFurniture = new ChromaFurniture(swfFile, isSmallFurni: false, renderState: 0, renderDirection: 2, colourId: colorId, renderShadows: renderWithShadows);
                        animFurniture.Run();
                        
                        string gifFilenameBase = $"{furniName}_animation";
                        if (colorId > -1) gifFilenameBase += $"_color_{colorId}";
                        if (!renderWithShadows) gifFilenameBase += "_no_sd";

                        string gifFullPath = Path.Combine(animationDir, gifFilenameBase + ".gif");
                        animFurniture.GenerateAnimationGif(gifFullPath, furniName); 
                        animFurniture.GenerateAnimationFrames(gifFullPath, furniName);
                    }

                    Logger.Log($"   [{furniName}] Fase 4: Renderizando icono...");
                    var iconFurniture = new ChromaFurniture(
                        swfFile, isSmallFurni: false, renderState: 0,
                        renderDirection: 0, colourId: colorId, isIcon: true, renderShadows: false
                    );
                    iconFurniture.Run();
                    byte[]? iconData = iconFurniture.CreateImage();
                    
                    if (iconData != null)
                    {
                        string iconFilename = $"{furniName}_icon";
                        if (colorId > -1) iconFilename += $"_color_{colorId}";
                        File.WriteAllBytes(Path.Combine(furniOutputDirectory, iconFilename + ".png"), iconData);
                    }
                }
                
                Logger.Info($"   -> ¡{furniName} completado con éxito!");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Logger.Info($"   -> Error al procesar el archivo {furniName}: {ex.Message}\n{ex.StackTrace}");
                Console.ResetColor();
            }
        }
        
        private static void ShowHelp()
        {
            Console.WriteLine("\nSimpleExtractor - Extractor Avanzado de Furnis");
            Console.WriteLine("-------------------------------------------------");
            Console.WriteLine("Uso: SimpleExtractor.exe [opciones]");
            Console.WriteLine("\nOpciones:");
            Console.WriteLine("  --verbose       Muestra todos los logs detallados del proceso de extracción.");
            Console.WriteLine("                  Por defecto, solo se muestra el inicio y fin de cada archivo.");
            Console.WriteLine("\n  --sequential    Desactiva el procesamiento en paralelo de los archivos SWF.");
            Console.WriteLine("                  Útil para depurar o si los logs mezclados son un problema.");
            Console.WriteLine("\n  --help          Muestra este mensaje de ayuda.");
            Console.WriteLine("\nEjemplo de uso:");
            Console.WriteLine("  SimpleExtractor.exe --verbose --sequential");
        }
    }
}