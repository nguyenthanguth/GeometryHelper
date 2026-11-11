using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// What the Recent Documents lists say of a DXF without opening it: its version, read from <c>$ACADVER</c> near the top of
    /// the file, and its preview picture, read from the THUMBNAILIMAGE section at its end. Only the first 256 KB and the last
    /// 4 MB are read, so that a list of large files shows at once; a binary DXF, or a file that cannot be read, gives no
    /// version and no picture.
    /// </summary>
    internal sealed class CadFileHeader
    {
        private const int HeadBytes = 256 * 1024;
        private const int TailBytes = 4 * 1024 * 1024;

        private static readonly Dictionary<string, string> Releases = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase
        )
        {
            ["AC1009"] = "AutoCAD R12",
            ["AC1012"] = "AutoCAD R13",
            ["AC1014"] = "AutoCAD R14",
            ["AC1015"] = "AutoCAD 2000",
            ["AC1018"] = "AutoCAD 2004",
            ["AC1021"] = "AutoCAD 2007",
            ["AC1024"] = "AutoCAD 2010",
            ["AC1027"] = "AutoCAD 2013",
            ["AC1032"] = "AutoCAD 2018",
        };

        private CadFileHeader(string version, byte[] thumbnail)
        {
            Version = version;
            Thumbnail = thumbnail;
        }

        /// <summary>Gets the version code of the file, as AC1032; null where it was not found.</summary>
        public string Version { get; }

        /// <summary>Gets the version in words: "AutoCAD 2018 DXF (AC1032)"; "DXF" where it was not found.</summary>
        public string VersionText =>
            Version == null ? "DXF"
            : Releases.TryGetValue(Version, out string release) ? release + " DXF (" + Version.ToUpperInvariant() + ")"
            : "DXF (" + Version.ToUpperInvariant() + ")";

        /// <summary>Gets the preview as the bytes of a whole PNG or BMP file; null where the file has none.</summary>
        public byte[] Thumbnail { get; }

        /// <summary>Reads the version and the preview of a DXF file; never throws, a file that cannot be read giving neither.</summary>
        /// <param name="path">The path of the file.</param>
        /// <returns>What was read.</returns>
        public static CadFileHeader Read(string path)
        {
            try
            {
                using (
                    var stream = new FileStream(
                        path,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete
                    )
                )
                {
                    byte[] head = ReadAt(stream, 0, (int)Math.Min(stream.Length, HeadBytes));

                    if (
                        Latin1(head, 0, Math.Min(head.Length, 22))
                            .StartsWith("AutoCAD Binary DXF", StringComparison.Ordinal)
                    )
                    {
                        return new CadFileHeader(null, null);
                    }

                    string version = Value(Lines(Latin1(head, 0, head.Length)), "$ACADVER");
                    long start = Math.Max(0L, stream.Length - TailBytes);
                    byte[] tail =
                        start == 0 && head.Length == stream.Length
                            ? head
                            : ReadAt(stream, start, (int)(stream.Length - start));
                    return new CadFileHeader(version, Picture(Lines(Latin1(tail, 0, tail.Length))));
                }
            }
            catch (Exception exception)
                when (exception is IOException
                    || exception is UnauthorizedAccessException
                    || exception is ArgumentException
                    || exception is NotSupportedException
                )
            {
                GeometryHelperLog.Debug("CadFileHeader: " + path + " could not be read: " + exception.Message);
                return new CadFileHeader(null, null);
            }
        }

        /// <summary>Makes a BMP file of the bytes of a THUMBNAILIMAGE section, a bitmap of Windows without its file header; a PNG is given as it is.</summary>
        internal static byte[] AsFile(byte[] section)
        {
            if (section == null || section.Length < 8)
            {
                return null;
            }

            if (section[0] == 0x89 && section[1] == (byte)'P' && section[2] == (byte)'N' && section[3] == (byte)'G')
            {
                return section;
            }

            int headerSize = BitConverter.ToInt32(section, 0);

            if (headerSize < 12 || headerSize > section.Length)
            {
                return null;
            }

            // The colours of the palette follow the header: those it says it uses, or all of them for 8 bits or fewer.
            int bits = headerSize >= 16 ? BitConverter.ToInt16(section, 14) : 24;
            int used = headerSize >= 36 ? BitConverter.ToInt32(section, 32) : 0;
            int colours =
                used > 0 ? used
                : bits > 0 && bits <= 8 ? 1 << bits
                : 0;
            int offset = 14 + headerSize + 4 * colours;
            var file = new byte[14 + section.Length];
            file[0] = (byte)'B';
            file[1] = (byte)'M';
            BitConverter.GetBytes(file.Length).CopyTo(file, 2);
            BitConverter.GetBytes(offset).CopyTo(file, 10);
            section.CopyTo(file, 14);
            return file;
        }

        private static byte[] ReadAt(Stream stream, long start, int count)
        {
            var bytes = new byte[count];
            stream.Seek(start, SeekOrigin.Begin);
            int read = 0;

            while (read < count)
            {
                int got = stream.Read(bytes, read, count - read);

                if (got <= 0)
                {
                    break;
                }

                read += got;
            }

            if (read < count)
            {
                Array.Resize(ref bytes, read);
            }

            return bytes;
        }

        private static string Latin1(byte[] bytes, int start, int count)
        {
            var text = new StringBuilder(count);

            for (int i = start; i < start + count; i++)
            {
                text.Append((char)bytes[i]);
            }

            return text.ToString();
        }

        private static string[] Lines(string text) => text.Replace("\r", string.Empty).Split('\n');

        /// <summary>The value of a header variable: the line after the group code that follows its name.</summary>
        private static string Value(string[] lines, string name)
        {
            for (int i = 0; i + 2 < lines.Length; i++)
            {
                if (string.Equals(lines[i].Trim(), name, StringComparison.OrdinalIgnoreCase))
                {
                    string value = lines[i + 2].Trim();
                    return value.Length == 0 ? null : value;
                }
            }

            return null;
        }

        /// <summary>The preview of the last THUMBNAILIMAGE section the lines hold, its groups 310 joined, as a whole file; null for none.</summary>
        private static byte[] Picture(string[] lines)
        {
            int start = -1;

            for (int i = lines.Length - 1; i > 0; i--)
            {
                if (lines[i].Trim() == "THUMBNAILIMAGE" && lines[i - 1].Trim() == "2")
                {
                    start = i + 1;
                    break;
                }
            }

            if (start < 0)
            {
                return null;
            }

            var bytes = new List<byte>();

            for (int i = start; i + 1 < lines.Length; i += 2)
            {
                string code = lines[i].Trim();

                if (code == "0")
                {
                    break;
                }

                if (code != "310")
                {
                    continue;
                }

                string hex = lines[i + 1].Trim();

                for (int k = 0; k + 1 < hex.Length; k += 2)
                {
                    if (
                        !byte.TryParse(
                            hex.Substring(k, 2),
                            NumberStyles.HexNumber,
                            CultureInfo.InvariantCulture,
                            out byte value
                        )
                    )
                    {
                        return null;
                    }

                    bytes.Add(value);
                }
            }

            return AsFile(bytes.ToArray());
        }
    }
}
