// Path: SimpleExtractor/Program.cs
using System.Diagnostics;
using Chroma;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SixLabors.ImageSharp;

namespace SimpleExtractor
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Contains("--help"))
            {
                ShowHelp();
                return;
            }
            
            // --- Argument Parsing ---
            Logger.IsVerbose = args.Contains("--verbose");
            bool isSequential = args.Contains("--sequential");
            string inputDirectory = "swfs";
            string outputDirectory = "output";

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i].ToLower();
                if ((arg == "--input" || arg == "-i") && i + 1 < args.Length)
                {
                    inputDirectory = args[i + 1];
                }
                else if ((arg == "--output" || arg == "-o") && i + 1 < args.Length)
                {
                    outputDirectory = args[i + 1];
                }
            }

            Logger.Info("Starting advanced furni extractor...");
            Logger.Log($" - Verbose Mode: {(Logger.IsVerbose ? "Enabled" : "Disabled")}");
            Logger.Log($" - Parallel Mode: {(!isSequential ? "Enabled" : "Disabled")}");
            Logger.Log($" - Input Directory: {inputDirectory}");
            Logger.Log($" - Output Directory: {outputDirectory}");

            Directory.CreateDirectory(inputDirectory);
            Directory.CreateDirectory(outputDirectory);

            var swfFiles = Directory.GetFiles(inputDirectory, "*.swf");
            if (swfFiles.Length == 0)
            {
                Logger.Info($"No .swf files found in the '{inputDirectory}' folder.");
                return;
            }

            Logger.Info($"\nFound {swfFiles.Length} .swf files to process.");
            var stopwatch = Stopwatch.StartNew();

            if (isSequential)
            {
                Logger.Info("Running in sequential mode...");
                foreach (var swfFile in swfFiles)
                {
                    ProcessSingleSwf(swfFile, outputDirectory);
                }
            }
            else
            {
                Logger.Info("Running in parallel mode...");
                Parallel.ForEach(swfFiles, swfFile =>
                {
                    ProcessSingleSwf(swfFile, outputDirectory);
                });
            }

            stopwatch.Stop();
            Logger.Info($"\nProcess completed in {stopwatch.Elapsed.TotalSeconds:F2} seconds!");
            Logger.Info($"Extracted files are located in the '{outputDirectory}' folder.");
        }
        
        private static void ProcessSingleSwf(string swfFile, string baseOutputDirectory)
        {
            string furniName = Path.GetFileNameWithoutExtension(swfFile);
            string furniOutputDirectory = Path.Combine(baseOutputDirectory, furniName);
            ChromaFurniture? masterFurniture = null;

            // Dictionary to store rendering offsets
            var renderOffsets = new Dictionary<string, Point>();

            try
            {
                Logger.Log($"   [{furniName}] Phase 1: Extracting assets and preparing data...");
                if (!FurniExtractor.Parse(swfFile, furniOutputDirectory))
                {
                    throw new Exception("Initial extraction of assets and XML failed.");
                }
                
                masterFurniture = new ChromaFurniture(swfFile, baseOutputDirectory, isSmallFurni: false, renderState: 0, renderDirection: 0);
                masterFurniture.Run();

                var availableColorIds = masterFurniture.GetAvailableColorIds();
                if (!availableColorIds.Any()) availableColorIds.Add(-1);

                foreach (int colorId in availableColorIds)
                {
                    if (colorId > -1) Logger.Log($"   [{furniName}] --- Processing Color ID: {colorId} ---");

                    foreach (bool renderWithShadows in new[] { true, false })
                    {
                        Logger.Log($"      [{furniName}] --- Processing Shadow: {(renderWithShadows ? "Yes" : "No")} ---");

                        string renderedDir = Path.Combine(furniOutputDirectory, "rendered");
                        Directory.CreateDirectory(renderedDir);
                        int[] directionsToRender = { 0, 2, 4, 6 };

                        foreach (int direction in directionsToRender)
                        {
                            masterFurniture.RenderDirection = direction;
                            masterFurniture.ColourId = colorId;
                            masterFurniture.RenderShadows = renderWithShadows;
                            masterFurniture.IsIcon = false;
                            
                            RenderResult? renderResult = masterFurniture.CreateImage();
                            if (renderResult?.ImageData != null)
                            {
                                string filenameBase = $"{furniName}_dir_{direction}";
                                if (colorId > -1) filenameBase += $"_{colorId}";
                                if (!renderWithShadows) filenameBase += "_no_sd";
                                string filename = filenameBase + ".png";

                                File.WriteAllBytes(Path.Combine(renderedDir, filename), renderResult.ImageData);
                                
                                // Save the offset to our dictionary
                                renderOffsets[filenameBase] = renderResult.Offset;
                            }
                        }
                        
                        // Animation logic remains, its offset is not saved for now.
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

                    Logger.Log($"   [{furniName}] Phase 4: Rendering icon for color ID: {colorId}...");
                    
                    masterFurniture.IsIcon = true;
                    masterFurniture.RenderDirection = 0;
                    masterFurniture.ColourId = colorId;
                    masterFurniture.RenderShadows = false;

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
                
                // At the end, save all offsets to a JSON file
                string renderDataPath = Path.Combine(furniOutputDirectory, "renderdata.json");
                string json = JsonConvert.SerializeObject(renderOffsets, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(renderDataPath, json);
                Logger.Log($"      [{furniName}] Render data saved to renderdata.json");

                Logger.Info($"   -> {furniName} completed successfully!");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Logger.Info($"   -> Error processing file {furniName}: {ex.Message}\n{ex.StackTrace}");
                Console.ResetColor();
            }
            finally
            {
                masterFurniture?.Dispose();
            }
        }
        
        private static void ShowHelp()
        {
            Console.WriteLine("\nSimpleExtractor - Advanced Furni Extractor");
            Console.WriteLine("-------------------------------------------");
            Console.WriteLine("Usage: SimpleExtractor.exe [options]");
            Console.WriteLine("\nOptions:");
            Console.WriteLine("  -i, --input <path>     Specifies the directory to read .swf files from.");
            Console.WriteLine("                         (Default: ./swfs)");
            Console.WriteLine("\n  -o, --output <path>    Specifies the root directory for all output files.");
            Console.WriteLine("                         (Default: ./output)");
            Console.WriteLine("\n  --verbose              Displays all detailed logs of the extraction process.");
            Console.WriteLine("                         By default, only the start and end of each file is shown.");
            Console.WriteLine("\n  --sequential           Disables parallel processing of SWF files.");
            Console.WriteLine("                         Useful for debugging or if mixed logs are an issue.");
            Console.WriteLine("\n  --help                 Displays this help message.");
            Console.WriteLine("\nExample usage:");
            Console.WriteLine("  SimpleExtractor.exe --input C:\\MySwfs --output D:\\Extracted --verbose");
        }
    }
}