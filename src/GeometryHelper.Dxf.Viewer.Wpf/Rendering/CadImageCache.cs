using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SkiaSharp;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The pictures of the images drawn, decoded once each by their file and its time on disk, so that a picture changed is
    /// read again: off the window's thread for the canvas, which draws the frame alone until the picture is ready and is told
    /// by <see cref="Decoded"/>; at once for a plot, a test or a screenshot. A picture of more than 16 million pixels is kept
    /// at that size, as the screen shows no more.
    /// </summary>
    internal static class CadImageCache
    {
        private const double MostPixels = 16E6;

        // The pictures by file and time: looked up only, never listed.
        private static readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>(
            StringComparer.OrdinalIgnoreCase
        );
        private static readonly object Lock = new object();
        private static int _version;

        /// <summary>Raised on a worker thread when a picture decoded off the window's thread is ready.</summary>
        internal static event EventHandler Decoded;

        /// <summary>Gets a number that grows each time a picture is ready, so that a picture kept of the drawing is drawn again.</summary>
        internal static int Version => _version;

        /// <summary>
        /// The picture of a file: decoded at once where <paramref name="wait"/> is true; else as decoded, null while it is being
        /// decoded on a worker thread. Null for a file that cannot be decoded.
        /// </summary>
        internal static SKImage Get(string path, long stamp, bool wait)
        {
            string key = path + "|" + stamp.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Entry entry;
            bool start = false;

            lock (Lock)
            {
                if (!Entries.TryGetValue(key, out entry))
                {
                    entry = new Entry();
                    Entries[key] = entry;
                    start = true;
                }
            }

            if (entry.Ready)
            {
                return entry.Image;
            }

            if (wait)
            {
                lock (entry)
                {
                    if (!entry.Ready)
                    {
                        Decode(path, entry);
                    }
                }

                return entry.Image;
            }

            if (start)
            {
                Task.Run(() =>
                {
                    lock (entry)
                    {
                        if (!entry.Ready)
                        {
                            Decode(path, entry);
                        }
                    }

                    System.Threading.Interlocked.Increment(ref _version);
                    Decoded?.Invoke(null, EventArgs.Empty);
                });
            }

            return null;
        }

        private static void Decode(string path, Entry entry)
        {
            try
            {
                using (SKBitmap bitmap = SKBitmap.Decode(path))
                {
                    if (bitmap != null)
                    {
                        double pixels = (double)bitmap.Width * bitmap.Height;

                        if (pixels > MostPixels)
                        {
                            double shrink = Math.Sqrt(MostPixels / pixels);
                            var info = new SKImageInfo(
                                Math.Max(1, (int)(bitmap.Width * shrink)),
                                Math.Max(1, (int)(bitmap.Height * shrink)),
                                SKColorType.Rgba8888,
                                SKAlphaType.Premul
                            );

                            using (
                                SKBitmap smaller = bitmap.Resize(
                                    info,
                                    new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None)
                                )
                            )
                            {
                                entry.Image = smaller == null ? null : SKImage.FromBitmap(smaller);
                            }
                        }
                        else
                        {
                            entry.Image = SKImage.FromBitmap(bitmap);
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                GeometryHelperLog.Warn("CadImageCache: the picture " + path + " cannot be read.", exception);
                entry.Image = null;
            }

            entry.Ready = true;
        }

        private sealed class Entry
        {
            public volatile bool Ready;

            public SKImage Image { get; set; }
        }
    }
}
