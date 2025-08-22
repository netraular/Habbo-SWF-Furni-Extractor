// Ruta: SimpleExtractor/Chroma/ImageUtil.cs
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Chroma
{
    public static class ImageUtil
    {
        // CAMBIO: El método ahora devuelve una tupla con la imagen y el rectángulo de recorte.
        public static (Image<Rgba32> Image, Rectangle TrimRectangle) TrimImage(Image<Rgba32> image, params Rgba32[] trimColors)
        {
            int top = 0, bottom = image.Height - 1, left = 0, right = image.Width - 1;
            bool stop;
            bool foundPixel = false; // Añadir flag para manejar imágenes completamente transparentes

            // Find top
            stop = false;
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    if (!trimColors.Contains(image[x, y]))
                    {
                        top = y;
                        stop = true;
                        foundPixel = true;
                        break;
                    }
                }
                if (stop) break;
            }

            // Si no se encontraron píxeles, devolver una imagen de 1x1 y un rect por defecto
            if (!foundPixel)
            {
                return (new Image<Rgba32>(1, 1), new Rectangle(0, 0, 1, 1));
            }

            // Find bottom
            stop = false;
            for (int y = image.Height - 1; y >= 0; y--)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    if (!trimColors.Contains(image[x, y]))
                    {
                        bottom = y;
                        stop = true;
                        break;
                    }
                }
                if (stop) break;
            }

            // Find left
            stop = false;
            for (int x = 0; x < image.Width; x++)
            {
                for (int y = 0; y < image.Height; y++)
                {
                    if (!trimColors.Contains(image[x, y]))
                    {
                        left = x;
                        stop = true;
                        break;
                    }
                }
                if (stop) break;
            }

            // Find right
            stop = false;
            for (int x = image.Width - 1; x >= 0; x--)
            {
                for (int y = 0; y < image.Height; y++)
                {
                    if (!trimColors.Contains(image[x, y]))
                    {
                        right = x;
                        stop = true;
                        break;
                    }
                }
                if (stop) break;
            }

            int width = right - left + 1;
            int height = bottom - top + 1;

            var trimRect = new Rectangle(left, top, width, height);
            var clone = image.Clone(ctx => ctx.Crop(trimRect));
            
            // Devolver tanto la imagen clonada como el rectángulo usado
            return (clone, trimRect);
        }
    }
}