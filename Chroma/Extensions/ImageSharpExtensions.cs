// Ruta: SimpleExtractor/Chroma/Extensions/ImageSharpExtensions.cs
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using System.IO;

namespace Chroma.Extensions
{
    public static class ImageSharpExtensions
    {
        public static byte[] ToByteArray<TPixel>(this Image<TPixel> image) where TPixel : unmanaged, IPixel<TPixel>
        {
            using (var memoryStream = new MemoryStream())
            {
                image.Save(memoryStream, PngFormat.Instance);
                return memoryStream.ToArray();
            }
        }
    }
}