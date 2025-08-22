// Ruta: SimpleExtractor/Program.cs
using System.Diagnostics;
using Chroma;
using System.Threading.Tasks;
using Newtonsoft.Json; // Añadir esto
using SixLabors.ImageSharp; // Añadir esto para la clase Point

namespace SimpleExtractor
{
    class Program
    {
        // ... Main y ShowHelp sin cambios ...
        static void Main(string[] args)
        {
            bool isSequential = false;

            if (args.Contains("--help"))
            {
                ShowHelp();
                return;
            }
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
            ChromaFurniture? masterFurniture = null;

            // NUEVO: Diccionario para guardar los offsets de renderizado
            var renderOffsets = new Dictionary<string, Point>();

            try
            {
                Logger.Log($"   [{furniName}] Fase 1: Extrayendo assets y preparando datos...");
                if (!FurniExtractor.Parse(swfFile, furniOutputDirectory))
                {
                    throw new Exception("Fallo la extracción inicial de assets y XML.");
                }
                
                masterFurniture = new ChromaFurniture(swfFile, isSmallFurni: false, renderState: 0, renderDirection: 0);
                masterFurniture.Run();

                var availableColorIds = masterFurniture.GetAvailableColorIds();
                if (!availableColorIds.Any()) availableColorIds.Add(-1);

                foreach (int colorId in availableColorIds)
                {
                    if (colorId > -1) Logger.Log($"   [{furniName}] --- Procesando Color ID: {colorId} ---");

                    foreach (bool renderWithShadows in new[] { true, false })
                    {
                        Logger.Log($"      [{furniName}] --- Procesando Sombra: {(renderWithShadows ? "Sí" : "No")} ---");

                        string renderedDir = Path.Combine(furniOutputDirectory, "rendered");
                        Directory.CreateDirectory(renderedDir);
                        int[] directionsToRender = { 0, 2, 4, 6 };

                        foreach (int direction in directionsToRender)
                        {
                            masterFurniture.RenderDirection = direction;
                            masterFurniture.ColourId = colorId;
                            masterFurniture.RenderShadows = renderWithShadows;
                            masterFurniture.IsIcon = false;
                            
                            // CAMBIO: Usar el nuevo método CreateImage que devuelve RenderResult
                            RenderResult? renderResult = masterFurniture.CreateImage();
                            if (renderResult?.ImageData != null)
                            {
                                string filenameBase = $"{furniName}_dir_{direction}";
                                if (colorId > -1) filenameBase += $"_{colorId}";
                                if (!renderWithShadows) filenameBase += "_no_sd";
                                string filename = filenameBase + ".png";

                                File.WriteAllBytes(Path.Combine(renderedDir, filename), renderResult.ImageData);
                                
                                // Guardar el offset en nuestro diccionario
                                renderOffsets[filenameBase] = renderResult.Offset;
                            }
                        }
                        
                        // Lógica de animación sin cambios, no se guarda su offset por ahora.
                        string animationDir = Path.Combine(furniOutputDirectory, "animations");
                        Directory.CreateDirectory(animationDir);
                        masterFurniture.RenderDirection = 2;
                        masterFurniture.ColourId = colorId;
                        masterFurniture.RenderShadows = renderWithShadows;
                        masterFurniture.IsIcon = false;
                        string gifFilenameBase = $"{furniName}_animation";
                        if (colorId > -1) gifFilenameBase += $"_{colorId}";
                        if (!renderWithShadows) gifFilenameBase += "_no_sd";
                        string gifFullPath = Path.Combine(animationDir, gifFilenameBase + ".gif");
                        masterFurniture.GenerateAnimationGif(gifFullPath, furniName); 
                        masterFurniture.GenerateAnimationFrames(gifFullPath, furniName);
                    }

                    Logger.Log($"   [{furniName}] Fase 4: Renderizando icono para color ID: {colorId}...");
                    
                    masterFurniture.IsIcon = true;
                    masterFurniture.RenderDirection = 0;
                    masterFurniture.ColourId = colorId;
                    masterFurniture.RenderShadows = false;

                    // CAMBIO: Guardar el offset del icono también
                    RenderResult? iconResult = masterFurniture.CreateImage();
                    if (iconResult?.ImageData != null)
                    {
                        string iconFilenameBase = $"{furniName}_icon";
                        if (colorId > -1) iconFilenameBase += $"_{colorId}";
                        string iconFilename = iconFilenameBase + ".png";
                        File.WriteAllBytes(Path.Combine(furniOutputDirectory, iconFilename), iconResult.ImageData);
                        
                        renderOffsets[iconFilenameBase] = iconResult.Offset;
                    }
                }
                
                // NUEVO: Al final, guardar todos los offsets a un archivo JSON
                string renderDataPath = Path.Combine(furniOutputDirectory, "renderdata.json");
                string json = JsonConvert.SerializeObject(renderOffsets, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(renderDataPath, json);
                Logger.Log($"      [{furniName}] Datos de renderizado guardados en renderdata.json");

                Logger.Info($"   -> ¡{furniName} completado con éxito!");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Logger.Info($"   -> Error al procesar el archivo {furniName}: {ex.Message}\n{ex.StackTrace}");
                Console.ResetColor();
            }
            finally
            {
                masterFurniture?.Dispose();
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