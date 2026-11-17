using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// A picture file read by the presenter's own decoders (TGA, PCX, TIFF, a DIB or an RLE bitmap: <see cref="CadRasterReader"/>)
    /// as a bitmap of WPF, for the dialogs that show a preview where Windows' own codecs cannot read the file.
    /// </summary>
    internal static class CadRasterBitmap
    {
        /// <summary>
        /// Reads a picture file as 32-bit colour, at most <paramref name="mostWidth"/> pixels across (the whole picture where
        /// that is not above 0); null where the file is none of the types the presenter reads or cannot be read.
        /// </summary>
        /// <param name="path">The file.</param>
        /// <param name="mostWidth">The most pixels across, or 0 for no limit.</param>
        internal static BitmapSource Read(string path, int mostWidth)
        {
            try
            {
                if (!CadRasterReader.TryRead(File.ReadAllBytes(path), path, true, out CadRaster raster))
                {
                    return null;
                }

                int width = raster.KeptWidth;
                int height = raster.KeptHeight;
                var bgra = new byte[width * height * 4];

                for (int i = 0; i < bgra.Length; i += 4)
                {
                    bgra[i] = raster.Rgba[i + 2];
                    bgra[i + 1] = raster.Rgba[i + 1];
                    bgra[i + 2] = raster.Rgba[i];
                    bgra[i + 3] = raster.Rgba[i + 3];
                }

                BitmapSource bitmap = BitmapSource.Create(
                    width,
                    height,
                    96,
                    96,
                    PixelFormats.Bgra32,
                    null,
                    bgra,
                    width * 4
                );

                if (mostWidth > 0 && width > mostWidth)
                {
                    double scale = (double)mostWidth / width;
                    bitmap = new FormatConvertedBitmap(
                        new TransformedBitmap(bitmap, new ScaleTransform(scale, scale)),
                        PixelFormats.Bgra32,
                        null,
                        0
                    );
                }

                bitmap.Freeze();
                return bitmap;
            }
            catch (Exception exception)
                when (exception is IOException
                    || exception is UnauthorizedAccessException
                    || exception is ArgumentException
                    || exception is NotSupportedException
                    || exception is InvalidOperationException
                    || exception is OutOfMemoryException
                )
            {
                GeometryHelperLog.Debug("CadRasterBitmap: " + path + " was not read (" + exception.Message + ").");
                return null;
            }
        }
    }
}
