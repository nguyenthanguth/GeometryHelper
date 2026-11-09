using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// Writes the image of PNGOUT, JPGOUT and BMPOUT (<see cref="CadImageJob"/>): the view drawn by
    /// <see cref="SkiaCanvasRenderer"/> at the view's size in pixels, on the canvas's background, with no cursor, grips or
    /// highlight, then a PNG of 8-bit red, green and blue, a JPEG at a quality of 90, or a bitmap of 24 bits a pixel. The same
    /// view gives the same bytes each time. The canvas writes what it shows, its grid included; this writes a view with no
    /// canvas, as a test or a host with no window does. A view of 1 920 by 1 080 pixels of a drawing of 100 000 entities is
    /// a PNG of 1.2 MB, written in 0.25 s.
    /// </summary>
    public static class CadRasterWriter
    {
        /// <summary>The quality JPGOUT writes at, out of 100.</summary>
        internal const int JpegQuality = 90;

        /// <summary>Draws the job's view of its space and writes it to its file, the objects selected alone where it has some.</summary>
        /// <param name="document">The document.</param>
        /// <param name="job">The image: its view, its file and the objects selected.</param>
        /// <param name="options">The colours and font it is drawn with, the canvas's; those of a new <see cref="CanvasOptions"/> where null.</param>
        /// <exception cref="ArgumentNullException">Thrown when the document or the job is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the job names no file.</exception>
        /// <exception cref="IOException">Thrown when the file cannot be written.</exception>
        public static void Write(CadDocument document, CadImageJob job, CanvasOptions options = null)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (job == null)
            {
                throw new ArgumentNullException(nameof(job));
            }

            if (string.IsNullOrEmpty(job.FileName))
            {
                throw new ArgumentException("The image names no file.", nameof(job));
            }

            using (SKBitmap picture = Draw(document, job.Space, job.View, options, job.Handles))
            {
                File.WriteAllBytes(job.FileName, Encode(picture, job.Format));
            }
        }

        /// <summary>
        /// A view drawn into a new picture of its size, as the canvas keeps its scene: blue, green, red and alpha, every pixel
        /// opaque. With handles, only their entities are drawn.
        /// </summary>
        internal static SKBitmap Draw(CadDocument document, CadSpace space, CadView view, CanvasOptions options, IReadOnlyCollection<string> handles)
        {
            int width = Math.Max(1, (int)Math.Round(view.Width));
            int height = Math.Max(1, (int)Math.Round(view.Height));
            var picture = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));

            using (SKSurface surface = SKSurface.Create(picture.Info, picture.GetPixels(), picture.RowBytes))
            using (var renderer = new SkiaCanvasRenderer())
            {
                if (handles == null)
                {
                    renderer.RenderScene(surface.Canvas, document, space, view, options);
                }
                else
                {
                    renderer.RenderOnly(surface.Canvas, document, space, view, options, handles);
                }

                surface.Canvas.Flush();
            }

            return picture;
        }

        /// <summary>The bytes of the file of a picture whose every pixel is opaque.</summary>
        internal static byte[] Encode(SKBitmap picture, CadImageFormat format)
        {
            if (format == CadImageFormat.Bmp)
            {
                return Bitmap(picture);
            }

            // Read as opaque, the encoders write no alpha: a PNG of colour type 2, as AutoCAD's.
            var opaque = new SKPixmap(picture.Info.WithAlphaType(SKAlphaType.Opaque), picture.GetPixels(), picture.RowBytes);

            using (opaque)
            using (SKImage image = SKImage.FromPixels(opaque))
            using (SKData data = image.Encode(format == CadImageFormat.Png ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg, format == CadImageFormat.Png ? 100 : JpegQuality))
            {
                return data.ToArray();
            }
        }

        /// <summary>
        /// A bitmap of 24 bits a pixel as BMPOUT writes it: the file header and the 40-byte information header, no resolution
        /// given, then the rows from the bottom up, each blue, green and red per pixel and padded to 4 bytes.
        /// </summary>
        private static byte[] Bitmap(SKBitmap picture)
        {
            int width = picture.Width;
            int height = picture.Height;
            int row = (width * 3 + 3) & ~3;
            int image = row * height;
            var bytes = new byte[54 + image];
            var pixels = new byte[picture.RowBytes * height];
            Marshal.Copy(picture.GetPixels(), pixels, 0, pixels.Length);
            bytes[0] = (byte)'B';
            bytes[1] = (byte)'M';
            Put(bytes, 2, bytes.Length);
            Put(bytes, 10, 54);
            Put(bytes, 14, 40);
            Put(bytes, 18, width);
            Put(bytes, 22, height);
            bytes[26] = 1;
            bytes[28] = 24;
            Put(bytes, 34, image);

            for (int y = 0; y < height; y++)
            {
                int from = (height - 1 - y) * picture.RowBytes;
                int to = 54 + y * row;

                for (int x = 0; x < width; x++)
                {
                    bytes[to + 3 * x] = pixels[from + 4 * x];
                    bytes[to + 3 * x + 1] = pixels[from + 4 * x + 1];
                    bytes[to + 3 * x + 2] = pixels[from + 4 * x + 2];
                }
            }

            return bytes;
        }

        /// <summary>A 32-bit number written little-endian.</summary>
        private static void Put(byte[] bytes, int at, int value)
        {
            bytes[at] = (byte)value;
            bytes[at + 1] = (byte)(value >> 8);
            bytes[at + 2] = (byte)(value >> 16);
            bytes[at + 3] = (byte)(value >> 24);
        }
    }
}
