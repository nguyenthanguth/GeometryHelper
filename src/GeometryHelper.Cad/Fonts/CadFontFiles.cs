using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// The TrueType and OpenType files (.ttf, .ttc, .otf) of some folders, found by the family name inside them, so that a
    /// font put in a Fonts folder is drawn by its family without being installed in Windows. Only the <c>name</c> table of
    /// a file is read, once while the file's name, size and time are unchanged; a folder's list of files is read again at
    /// each look, so that a font added or taken away is seen. A file that does not read is passed over, said once at Debug.
    /// </summary>
    internal static class CadFontFiles
    {
        private static readonly Dictionary<string, FaceNames[]> Read = new Dictionary<string, FaceNames[]>(
            StringComparer.OrdinalIgnoreCase
        );

        /// <summary>
        /// The file of a family: in the first folder that has one, the file whose face is the regular one where there is such
        /// a file, else the first by name. The family is matched whatever its case, by any name the face gives it.
        /// </summary>
        /// <param name="folders">The folders, in order.</param>
        /// <param name="family">The family, as a text style names it.</param>
        /// <returns>The full path, or null.</returns>
        internal static string Find(IEnumerable<string> folders, string family)
        {
            if (string.IsNullOrWhiteSpace(family))
            {
                return null;
            }

            string wanted = family.Trim();

            foreach (string folder in folders)
            {
                string regular = null;
                string any = null;

                foreach (FileInfo file in FilesOf(folder))
                {
                    foreach (FaceNames face in NamesOf(file))
                    {
                        if (!face.Families.Contains(wanted, StringComparer.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        any = any ?? file.FullName;
                        regular = regular ?? (face.IsRegular ? file.FullName : null);
                    }
                }

                if (any != null)
                {
                    return regular ?? any;
                }
            }

            return null;
        }

        /// <summary>The font files of a folder in the order of their names; none where it cannot be listed.</summary>
        private static IEnumerable<FileInfo> FilesOf(string folder)
        {
            try
            {
                return new DirectoryInfo(folder)
                    .GetFiles()
                    .Where(f =>
                    {
                        string extension = f.Extension.ToLowerInvariant();
                        return extension == ".ttf" || extension == ".ttc" || extension == ".otf";
                    })
                    .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(f => f.Name, StringComparer.Ordinal)
                    .ToList();
            }
            catch (Exception e)
                when (e is ArgumentException
                    || e is IOException
                    || e is UnauthorizedAccessException
                    || e is System.Security.SecurityException
                )
            {
                GeometryHelperLog.Debug("CadFontFiles: " + folder + " was not listed (" + e.Message + ").");
                return Array.Empty<FileInfo>();
            }
        }

        private static FaceNames[] NamesOf(FileInfo file)
        {
            string key = file.FullName + "|" + file.Length + "|" + file.LastWriteTimeUtc.Ticks;

            lock (Read)
            {
                if (!Read.TryGetValue(key, out FaceNames[] faces))
                {
                    faces = ReadNames(file);
                    Read[key] = faces;
                }

                return faces;
            }
        }

        /// <summary>The names of each face of a file; none where it is no font or cannot be read.</summary>
        private static FaceNames[] ReadNames(FileInfo file)
        {
            try
            {
                using (FileStream stream = file.OpenRead())
                {
                    byte[] head = Bytes(stream, 0, 16);
                    var offsets = new List<long>();

                    if (head != null && U32(head, 0) == 0x74746366)
                    {
                        long count = U32(head, 8);
                        byte[] list = count > 0 && count <= 256 ? Bytes(stream, 12, (int)(4 * count)) : null;

                        for (int i = 0; list != null && i < count; i++)
                        {
                            offsets.Add(U32(list, 4 * i));
                        }
                    }
                    else
                    {
                        offsets.Add(0);
                    }

                    var faces = new List<FaceNames>();

                    foreach (long offset in offsets)
                    {
                        FaceNames face = ReadFace(stream, offset);

                        if (face != null)
                        {
                            faces.Add(face);
                        }
                    }

                    return faces.ToArray();
                }
            }
            catch (Exception e)
                when (e is IOException || e is UnauthorizedAccessException || e is System.Security.SecurityException)
            {
                GeometryHelperLog.Debug("CadFontFiles: " + file.FullName + " was not read (" + e.Message + ").");
                return new FaceNames[0];
            }
        }

        private static FaceNames ReadFace(Stream stream, long offset)
        {
            byte[] directory = Bytes(stream, offset, 12);

            if (directory == null)
            {
                return null;
            }

            long version = U32(directory, 0);

            if (version != 0x00010000 && version != 0x74727565 && version != 0x4F54544F)
            {
                return null;
            }

            int tables = U16(directory, 4);
            byte[] records = Bytes(stream, offset + 12, tables * 16);

            if (records == null)
            {
                return null;
            }

            FaceNames names = null;
            bool plain = true;

            for (int i = 0; i < tables; i++)
            {
                string tag = Encoding.ASCII.GetString(records, i * 16, 4);
                long at = U32(records, i * 16 + 8);
                long length = U32(records, i * 16 + 12);

                if (tag == "name")
                {
                    byte[] table = length >= 6 && length <= 1 << 20 ? Bytes(stream, at, (int)length) : null;
                    names = table == null ? null : Parse(table);
                }
                else if (tag == "head" && length >= 46)
                {
                    // macStyle: bit 0 bold, bit 1 italic.
                    byte[] head = Bytes(stream, at + 44, 2);
                    plain = head == null || (U16(head, 0) & 3) == 0;
                }
            }

            return names == null ? null : new FaceNames(names.Families, names.IsRegular && plain);
        }

        /// <summary>The family names (1 and 16) and whether the style (2 and 17) is Regular, of the platform 3 (Windows), UTF-16 big-endian.</summary>
        private static FaceNames Parse(byte[] table)
        {
            int count = U16(table, 2);
            int strings = U16(table, 4);
            var families = new List<string>();
            bool regular = true;

            for (int i = 0; i < count && 6 + 12 * (i + 1) <= table.Length; i++)
            {
                int record = 6 + 12 * i;
                int id = U16(table, record + 6);
                int size = U16(table, record + 8);
                int at = strings + U16(table, record + 10);

                if (
                    U16(table, record) != 3
                    || (id != 1 && id != 2 && id != 16 && id != 17)
                    || size % 2 != 0
                    || at + size > table.Length
                )
                {
                    continue;
                }

                string text = Encoding.BigEndianUnicode.GetString(table, at, size).Trim();

                if (id == 1 || id == 16)
                {
                    families.Add(text);
                }
                else if (!string.Equals(text, "Regular", StringComparison.OrdinalIgnoreCase))
                {
                    regular = false;
                }
            }

            return families.Count == 0 ? null : new FaceNames(families.ToArray(), regular);
        }

        private static byte[] Bytes(Stream stream, long at, int count)
        {
            if (at < 0 || count < 0 || at + count > stream.Length)
            {
                return null;
            }

            var bytes = new byte[count];
            stream.Seek(at, SeekOrigin.Begin);
            int read = 0;

            while (read < count)
            {
                int got = stream.Read(bytes, read, count - read);

                if (got <= 0)
                {
                    return null;
                }

                read += got;
            }

            return bytes;
        }

        private static int U16(byte[] bytes, int at) => (bytes[at] << 8) | bytes[at + 1];

        private static long U32(byte[] bytes, int at) =>
            ((long)bytes[at] << 24) | ((long)bytes[at + 1] << 16) | ((long)bytes[at + 2] << 8) | bytes[at + 3];

        private sealed class FaceNames
        {
            public FaceNames(string[] families, bool isRegular)
            {
                Families = families;
                IsRegular = isRegular;
            }

            public string[] Families { get; }

            public bool IsRegular { get; }
        }
    }
}
