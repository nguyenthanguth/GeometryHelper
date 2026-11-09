using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The pictures of the True Color tab of Select Color, drawn into bitmaps and kept: the field of hue and saturation, once
    /// for its size; the luminance bar, again only when the hue or the saturation changes; the bars of red, green and blue,
    /// again only when the other two change. A drag that moves nothing they show draws nothing.
    /// </summary>
    internal static class ColorFieldBitmaps
    {
        private static readonly object Gate = new object();
        private static BitmapSource _field;
        private static long _fieldKey = -1;
        private static BitmapSource _luminance;
        private static long _luminanceKey = -1;
        private static readonly BitmapSource[] Channels = new BitmapSource[3];
        private static readonly long[] ChannelKeys = { -1, -1, -1 };

        /// <summary>
        /// The field of hue and saturation, a pixel for each place of it: the hue from 0 degrees at the left to 360 at the right,
        /// the saturation from 100 at the top to 0 at the foot, at luminance 50, so that its foot is grey 128.
        /// </summary>
        internal static BitmapSource Field(int width, int height)
        {
            long key = ((long)width << 32) | (uint)height;

            lock (Gate)
            {
                if (_fieldKey != key)
                {
                    var pixels = new int[width * height];

                    for (int y = 0; y < height; y++)
                    {
                        double saturation = 100.0 * (1.0 - y / (double)(height - 1));

                        for (int x = 0; x < width; x++)
                        {
                            pixels[y * width + x] = SelectColorViewModel.HslToRgb(360.0 * x / (width - 1), saturation, 50.0);
                        }
                    }

                    _field = Make(width, height, pixels);
                    _fieldKey = key;
                }

                return _field;
            }
        }

        /// <summary>The luminance bar of a hue and a saturation, one pixel wide, white at its top, black at its foot; stretched to the bar's width.</summary>
        internal static BitmapSource Luminance(int hue, int saturation, int height)
        {
            long key = ((long)hue << 40) | ((long)saturation << 32) | (uint)height;

            lock (Gate)
            {
                if (_luminanceKey != key)
                {
                    var pixels = new int[height];

                    for (int y = 0; y < height; y++)
                    {
                        pixels[y] = SelectColorViewModel.HslToRgb(hue, saturation, 100.0 * (1.0 - y / (double)(height - 1)));
                    }

                    _luminance = Make(1, height, pixels);
                    _luminanceKey = key;
                }

                return _luminance;
            }
        }

        /// <summary>The bar of red (0), green (1) or blue (2), 256 pixels from none to full, the other two as given; stretched to the bar's size.</summary>
        internal static BitmapSource Channel(int channel, int first, int second)
        {
            long key = ((long)first << 8) | (uint)second;

            lock (Gate)
            {
                if (ChannelKeys[channel] != key)
                {
                    var pixels = new int[256];

                    for (int v = 0; v < 256; v++)
                    {
                        pixels[v] = channel == 0 ? (v << 16) | (first << 8) | second : channel == 1 ? (first << 16) | (v << 8) | second : (first << 16) | (second << 8) | v;
                    }

                    Channels[channel] = Make(256, 1, pixels);
                    ChannelKeys[channel] = key;
                }

                return Channels[channel];
            }
        }

        private static BitmapSource Make(int width, int height, int[] rgb)
        {
            var bitmap = new WriteableBitmap(width, height, 96.0, 96.0, PixelFormats.Bgr32, null);

            for (int i = 0; i < rgb.Length; i++)
            {
                rgb[i] |= unchecked((int)0xFF000000);
            }

            bitmap.WritePixels(new Int32Rect(0, 0, width, height), rgb, width * 4, 0);
            bitmap.Freeze();
            return bitmap;
        }
    }
}
