using System;
using System.Collections.Generic;
using System.IO;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// The PDFs the underlays of the open drawings show, each read once by its file and the time it was written, so that a PDF
    /// changed on disk is read again, and the pages of one interpreted once each. Of the last few files read the cache keeps
    /// the most recently used; a file that cannot be read is remembered with the reason, so that the drawing says it once.
    /// </summary>
    internal static class CadPdfCache
    {
        /// <summary>How many PDFs are kept at once.</summary>
        private const int Kept = 6;

        private static readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>(
            StringComparer.OrdinalIgnoreCase
        );
        private static readonly object Lock = new object();
        private static long _tick;

        private sealed class Entry
        {
            public PdfDocument Document;
            public string Error;
            public long Used;
        }

        /// <summary>Gets the time a file was last written, in ticks; nought for none.</summary>
        internal static long Stamp(string path)
        {
            try
            {
                return path != null && File.Exists(path) ? File.GetLastWriteTimeUtc(path).Ticks : 0L;
            }
            catch (Exception exception)
                when (exception is IOException
                    || exception is UnauthorizedAccessException
                    || exception is ArgumentException
                    || exception is NotSupportedException
                )
            {
                return 0L;
            }
        }

        /// <summary>Reads a PDF, or gives the one read before from the same file at the same time.</summary>
        /// <param name="path">The full path of the file.</param>
        /// <param name="error">Why it cannot be read, in plain words, where it cannot.</param>
        /// <returns>The document, or null.</returns>
        internal static PdfDocument Get(string path, out string error)
        {
            error = null;

            if (path == null)
            {
                error = "No file.";
                return null;
            }

            string key = path + "|" + Stamp(path).ToString(System.Globalization.CultureInfo.InvariantCulture);

            lock (Lock)
            {
                if (Entries.TryGetValue(key, out Entry found))
                {
                    found.Used = ++_tick;
                    error = found.Error;
                    return found.Document;
                }
            }

            var entry = new Entry();

            try
            {
                entry.Document = PdfDocument.OpenFile(path);
            }
            catch (PdfException exception)
            {
                entry.Error = exception.Message;
            }
            catch (Exception exception)
            {
                // A file of another form than any the reader expects, or one that is no file: the underlay is drawn as its frame.
                GeometryHelper.GeometryHelperLog.Warn(
                    "CadPdfCache: the PDF " + path + " could not be read.",
                    exception
                );
                entry.Error = "The file cannot be read.";
            }

            lock (Lock)
            {
                entry.Used = ++_tick;
                Entries[key] = entry;

                while (Entries.Count > Kept)
                {
                    string oldest = null;
                    long least = long.MaxValue;

                    foreach (KeyValuePair<string, Entry> pair in Entries)
                    {
                        if (pair.Value.Used < least)
                        {
                            least = pair.Value.Used;
                            oldest = pair.Key;
                        }
                    }

                    Entries.Remove(oldest);
                }
            }

            error = entry.Error;
            return entry.Document;
        }

        /// <summary>Forgets every PDF read, so that the next drawing reads them again: for a test, and for RELOAD.</summary>
        internal static void Clear()
        {
            lock (Lock)
            {
                Entries.Clear();
            }
        }
    }
}
