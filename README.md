# SimpleExtractor - Advanced Habbo Furniture Extractor

SimpleExtractor is a powerful command-line tool for .NET designed to extract, process, and render Habbo Hotel furniture assets from their original SWF files. It goes beyond simple asset dumping by generating a structured `furni.json`, rendering static images in multiple directions, creating animated GIFs, and handling all color variations and shadow options automatically.

This tool is built for performance, utilizing parallel processing to handle large batches of SWF files quickly and efficiently.


*(Example: Stills rendered for a single furniture item with color and shadow variations)*

## Features

- **SWF Decompilation**: Extracts all necessary XML data (`logic`, `visualization`, `assets`) and raw image assets using Flazzy.
- **Structured JSON Output**: Parses the extracted XML files into a clean, modern `furni.json` file, perfect for use in modern web applications and game clients.
- **Static Rendering**: Renders furniture in standard directions (0, 2, 4, 6) for a complete view.
- **Animation Rendering**: Automatically detects the most complex animation sequence and generates high-quality animated GIFs and individual PNG frames.
- **Full Color & Shadow Handling**: Detects all available furniture colors and renders variations for each. Also provides options to render with or without shadows.
- **Icon Generation**: Creates a specific icon image for each furniture item and its color variations.
- **High Performance**: Utilizes parallel processing by default to handle large batches of SWF files in a fraction of the time. A sequential mode is also available for debugging.
- **Cross-Platform**: Built on .NET 6, it can run on Windows, macOS, and Linux.


*(Example: Automatically generated animation)*

## Getting Started

Follow these instructions to get the project up and running on your local machine.

### Prerequisites

- [.NET 6.0 SDK](https://dotnet.microsoft.com/download/dotnet/6.0) or higher.
- `Flazzy.dll` library. This project is configured to look for it in a `LIB/` folder.

### Setup

1.  **Clone the repository:**
    ```sh
    git clone <repository-url>
    cd SimpleExtractor
    ```

2.  **Add Dependencies:**
    - Create a folder named `LIB` in the root of the project directory.
    - Place your `Flazzy.dll` file inside the `LIB` folder.

3.  **Add Furniture Files:**
    - Create a folder named `swfs` in the root of the project directory.
    - Place all your `.swf` furniture files inside the `swfs` folder.

4.  **Build the project:**
    ```sh
    dotnet build -c Release
    ```

## Usage

Run the application from your terminal in the project's root directory. The extracted files will be placed in the `output` folder.

```sh
# Run with default parallel processing
dotnet run --project SimpleExtractor.csproj

# Run with detailed logging
dotnet run --project SimpleExtractor.csproj --verbose

# Run sequentially (one file at a time) for easier debugging
dotnet run --project SimpleExtractor.csproj --sequential
```

### Command-Line Options

- `--verbose`: Displays all detailed logs during the extraction and rendering process. By default, only summary information is shown.
- `--sequential`: Disables parallel processing and processes SWF files one by one. Useful for debugging or if mixed-up console logs are an issue.
- `--help`: Shows the help message with all available options.

## Output Structure

For each `.swf` file processed, a corresponding folder is created in the `output` directory. This folder contains all the extracted and generated assets in an organized structure.


*(Example: The generated folder structure for a single item)*

```
📂 [furni_name]
  ├─ 📂 assets
  │  └─ [furni_name]_32_a_0_0.png
  ├─ 📂 animations
  │  ├─ 📂 frames
  │  └─ [furni_name]_animation.gif
  ├─ 📂 rendered
  │  └─ [furni_name]_dir_0.png
  ├─ 📂 xml
  │  └─ assets.xml
  ├─ 📜 furni.json
  └─ 🖼️ [furni_name]_icon.png
```

- **`assets/`**: Contains all the raw, individual PNG layers extracted from the SWF.
- **`xml/`**: Contains the original data XML files (`assets.xml`, `logic.xml`, `visualization.xml`).
- **`rendered/`**: Contains the final composed static PNG images for different directions, colors, and shadow options.
- **`animations/`**: Contains the generated animated GIFs and a `frames/` subfolder with each frame saved as a separate PNG.
- **`furni.json`**: A single, consolidated JSON file containing all logic, visualization, and asset data in a structured format.
- **`*_icon.png`**: The generated icon for the furniture, including color variations.

## Acknowledgements

This project is heavily inspired by and based on the rendering logic from:
- **[Quackster/Chroma](https://github.com/Quackster/Chroma)**

Another similar and excellent project in this space is:
- **[scottstamp/FurniExtractor](https://github.com/scottstamp/FurniExtractor)**

Special thanks to the creators of these tools for paving the way.

## License

This project is licensed under the MIT License.