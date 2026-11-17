using System;
using System.IO;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The raster files the presenter reads with its own decoders, which the platform's do not: TGA, PCX, TIFF (plain, packed,
    /// LZW, Deflate and fax coded, in strips or tiles) and the Windows bitmaps with no file header (DIB) or run-length coded
    /// (RLE), besides any BMP. PNG and JPEG are left to the platform, and a GIF is only measured here: its size is read from
    /// its header and its pixels by the platform.
    /// </summary>
    internal static class CadRasterReader
    {
        /// <summary>The most pixels a decoder holds in memory unpacked, for the formats whose rows cannot be read one by one.</summary>
        internal const double MostRawPixels = 3E8;

        /// <summary>Whether a file name ends in one of the extensions of the raster types: .bmp, .rle, .dib, .gif, .jpg, .jpeg, .pcx, .png, .tga, .tif or .tiff.</summary>
        internal static bool IsPictureName(string path)
        {
            switch (Extension(path))
            {
                case ".bmp":
                case ".rle":
                case ".dib":
                case ".gif":
                case ".jpg":
                case ".jpeg":
                case ".pcx":
                case ".png":
                case ".tga":
                case ".tif":
                case ".tiff":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Whether the platform's decoder is left to read the pixels of a file of this name: PNG, JPEG, GIF and BMP.</summary>
        internal static bool IsDecodedByThePlatform(string path)
        {
            switch (Extension(path))
            {
                case ".png":
                case ".jpg":
                case ".jpeg":
                case ".gif":
                case ".bmp":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Reads a picture from the bytes of its file, by what they begin with and, for a TGA, which begins with nothing, by the
        /// file's name.
        /// </summary>
        /// <param name="data">The bytes of the file.</param>
        /// <param name="path">The file's name, or its extension.</param>
        /// <param name="pixels">Whether to decode the pixels, or the size and resolution alone.</param>
        /// <param name="raster">The picture; null where the file is none of these types or cannot be read.</param>
        /// <returns>Whether it was read.</returns>
        internal static bool TryRead(byte[] data, string path, bool pixels, out CadRaster raster)
        {
            raster = null;

            if (data == null || data.Length < 12)
            {
                return false;
            }

            string extension = Extension(path);

            if (extension == ".tga" && CadTgaFile.TryRead(data, pixels, out raster))
            {
                return true;
            }

            if (CadTiffFile.LooksLikeTiff(data))
            {
                return CadTiffFile.TryRead(data, pixels, out raster);
            }

            if (CadPcxFile.LooksLikePcx(data))
            {
                return CadPcxFile.TryRead(data, pixels, out raster);
            }

            if (data[0] == 'B' && data[1] == 'M')
            {
                return CadBmpFile.TryRead(data, pixels, out raster);
            }

            if (data.Length >= 10 && data[0] == 'G' && data[1] == 'I' && data[2] == 'F' && data[3] == '8')
            {
                int width = data[6] | (data[7] << 8);
                int height = data[8] | (data[9] << 8);

                if (pixels || width == 0 || height == 0)
                {
                    return false;
                }

                raster = new CadRaster { Width = width, Height = height };
                return true;
            }

            if (
                (extension == ".dib" || extension == ".rle" || CadBmpFile.LooksLikeDib(data))
                && CadBmpFile.TryRead(data, pixels, out raster)
            )
            {
                return true;
            }

            return extension != ".tga" && CadTgaFile.TryRead(data, pixels, out raster);
        }

        private static string Extension(string path)
        {
            try
            {
                return Path.GetExtension(path ?? string.Empty).ToLowerInvariant();
            }
            catch (ArgumentException)
            {
                return string.Empty;
            }
        }
    }
}
