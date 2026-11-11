using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Dwg
{
    /// <summary>
    /// The parts of a DWG file taken out of its container, as the decoders of objects need them whatever the version: the
    /// version and the code page, the data of the header variables and of the classes, and the object map with the bytes
    /// its offsets count into. Reading the container is the only step that knows where a version keeps its sections. Of
    /// R2000 (AC1015) the sections lie in the file as they are, found through the section locator records of the file
    /// header; of R2004 (AC1018) and of R2010 to R2018 (AC1024, AC1027, AC1032) they are compressed pages, read in
    /// DwgContainer.R2004.cs; R2007 (AC1021) has a container of its own, read in DwgContainer.R2007.cs.
    /// </summary>
    internal sealed partial class DwgContainer
    {
        private static readonly byte[] HeaderStart =
        {
            0xCF,
            0x7B,
            0x1F,
            0x23,
            0xFD,
            0xDE,
            0x38,
            0xA9,
            0x5F,
            0x7C,
            0x68,
            0xB8,
            0x4E,
            0x6D,
            0x33,
            0x5F,
        };
        private static readonly byte[] HeaderEnd =
        {
            0x30,
            0x84,
            0xE0,
            0xDC,
            0x02,
            0x21,
            0xC7,
            0x56,
            0xA0,
            0x83,
            0x97,
            0x47,
            0xB1,
            0x92,
            0xCC,
            0xA0,
        };
        private static readonly byte[] ClassesStart =
        {
            0x8D,
            0xA1,
            0xC4,
            0xB8,
            0xC4,
            0xA9,
            0xF8,
            0xC5,
            0xC0,
            0xDC,
            0xF4,
            0x5F,
            0xE7,
            0xCF,
            0xB6,
            0x8A,
        };
        private static readonly byte[] ClassesEnd =
        {
            0x72,
            0x5E,
            0x3B,
            0x47,
            0x3B,
            0x56,
            0x07,
            0x3A,
            0x3F,
            0x23,
            0x0B,
            0xA0,
            0x18,
            0x30,
            0x49,
            0x75,
        };
        private static readonly byte[] FileHeaderEnd =
        {
            0x95,
            0xA0,
            0x4E,
            0x28,
            0x99,
            0x82,
            0x1A,
            0xE5,
            0x5E,
            0x41,
            0xE0,
            0x5F,
            0x9D,
            0x3A,
            0x4D,
            0x00,
        };

        /// <summary>
        /// The names AutoCAD writes into <c>$DWGCODEPAGE</c> for the code page numbers of a DWG file header, by number.
        /// </summary>
        private static readonly string[] CodePageNames =
        {
            "ANSI_1252",
            "ASCII",
            "ISO8859-1",
            "ISO8859-2",
            "ISO8859-3",
            "ISO8859-4",
            "ISO8859-5",
            "ISO8859-6",
            "ISO8859-7",
            "ISO8859-8",
            "ISO8859-9",
            "DOS437",
            "DOS850",
            "DOS852",
            "DOS855",
            "DOS857",
            "DOS860",
            "DOS861",
            "DOS863",
            "DOS864",
            "DOS865",
            "DOS869",
            "DOS932",
            "MACINTOSH",
            "BIG5",
            "KSC5601",
            "JOHAB",
            "DOS866",
            "ANSI_1250",
            "ANSI_1251",
            "ANSI_1252",
            "GB2312",
            "ANSI_1253",
            "ANSI_1254",
            "ANSI_1255",
            "ANSI_1256",
            "ANSI_1257",
            "ANSI_874",
            "ANSI_932",
            "ANSI_936",
            "ANSI_949",
            "ANSI_950",
            "ANSI_1361",
            "ANSI_1200",
            "ANSI_1258",
        };

        private DwgContainer() { }

        /// <summary>The version string at the start of the file, such as <c>AC1015</c>.</summary>
        public string Version { get; private set; }

        /// <summary>The maintenance release the file header names.</summary>
        public int MaintenanceVersion { get; private set; }

        /// <summary>The name of the drawing's code page as <c>$DWGCODEPAGE</c> carries it, such as <c>ANSI_1252</c>.</summary>
        public string CodePage { get; private set; }

        /// <summary>The header variables: the data between the size and the check sum of their section.</summary>
        public byte[] Header { get; private set; }

        /// <summary>The classes: the data between the size and the check sum of their section.</summary>
        public byte[] Classes { get; private set; }

        /// <summary>The handles of the objects with the offset of each into <see cref="Objects"/>, by rising handle.</summary>
        public List<KeyValuePair<ulong, long>> ObjectMap { get; private set; }

        /// <summary>The bytes the offsets of <see cref="ObjectMap"/> count into.</summary>
        public byte[] Objects { get; private set; }

        /// <summary>The value of <c>$MEASUREMENT</c> (0 English, 1 metric), or -1 where the file has not the section that carries it.</summary>
        public int Measurement { get; private set; }

        /// <summary>
        /// The version string of a file, its first six bytes, where they are those of a DWG (<c>AC</c> and four digits or
        /// such); null where the bytes are not the start of a DWG.
        /// </summary>
        public static string VersionOf(byte[] file)
        {
            if (file == null || file.Length < 6 || file[0] != (byte)'A' || file[1] != (byte)'C')
            {
                return null;
            }

            var version = new char[6];

            for (int i = 0; i < 6; i++)
            {
                byte b = file[i];

                if (
                    !(
                        (b >= (byte)'0' && b <= (byte)'9')
                        || (b >= (byte)'A' && b <= (byte)'Z')
                        || b == (byte)'.'
                        || (b >= (byte)'a' && b <= (byte)'z')
                    )
                )
                {
                    return null;
                }

                version[i] = (char)b;
            }

            return new string(version);
        }

        /// <summary>The release of AutoCAD that first wrote a version string, for the messages; the string itself where it is not known.</summary>
        public static string ReleaseOf(string version)
        {
            switch (version)
            {
                case "AC1009":
                    return "AutoCAD R11 and R12";
                case "AC1012":
                    return "AutoCAD R13";
                case "AC1014":
                    return "AutoCAD R14";
                case "AC1015":
                    return "AutoCAD 2000";
                case "AC1018":
                    return "AutoCAD 2004";
                case "AC1021":
                    return "AutoCAD 2007";
                case "AC1024":
                    return "AutoCAD 2010";
                case "AC1027":
                    return "AutoCAD 2013";
                case "AC1032":
                    return "AutoCAD 2018";
                default:
                    return "a release not known";
            }
        }

        /// <summary>
        /// Takes a file apart. Throws <see cref="FormatException"/> where the bytes are not a DWG or are broken (a section
        /// outside the file, a missing sentinel, a wrong check sum), and <see cref="NotSupportedException"/>, naming the
        /// version, for a version that is not read yet.
        /// </summary>
        public static DwgContainer Read(byte[] file)
        {
            string version = VersionOf(file);

            if (version == null)
            {
                throw new FormatException(
                    "The data is not a DWG file: it does not start with a version such as AC1015."
                );
            }

            switch (version)
            {
                case "AC1015":
                    return ReadR15(file, version);
                case "AC1018":
                case "AC1024":
                case "AC1027":
                case "AC1032":
                    return ReadR18(file, version);
                case "AC1021":
                    return ReadR21(file, version);
                default:
                    throw new NotSupportedException(
                        "The DWG version "
                            + version
                            + " ("
                            + ReleaseOf(version)
                            + ") is not read yet; DWG files of "
                            + "AC1015 (AutoCAD 2000), AC1018 (AutoCAD 2004), AC1021 (AutoCAD 2007), AC1024 (AutoCAD 2010), AC1027 (AutoCAD 2013) "
                            + "and AC1032 (AutoCAD 2018) are."
                    );
            }
        }

        /// <summary>The number of a version, such as 1018 for AC1018, for comparing versions; 0 where it is not of that form.</summary>
        public static int NumberOf(string version)
        {
            return
                version != null
                && version.Length == 6
                && int.TryParse(version.Substring(2), NumberStyles.None, CultureInfo.InvariantCulture, out int number)
                ? number
                : 0;
        }

        private static DwgContainer ReadR15(byte[] file, string version)
        {
            var reader = new DwgBitReader(file);
            reader.Position = 0x0B * 8;
            int maintenance = reader.RawChar();
            reader.Position = 0x13 * 8;
            int codePage = reader.RawShort();
            int count = reader.RawLong();

            if (count < 3 || count > 16)
            {
                throw new FormatException(
                    "The DWG file header names "
                        + count.ToString(CultureInfo.InvariantCulture)
                        + " sections; a file of AC1015 has 3 to 6."
                );
            }

            var seekers = new long[count];
            var sizes = new long[count];

            for (int i = 0; i < count; i++)
            {
                int number = reader.RawChar();
                long seeker = (uint)reader.RawLong();
                long size = (uint)reader.RawLong();

                if (number < count)
                {
                    seekers[number] = seeker;
                    sizes[number] = size;
                }

                if (seeker + size > file.LongLength)
                {
                    throw new FormatException(
                        "The DWG file is cut short: its section "
                            + number.ToString(CultureInfo.InvariantCulture)
                            + " would end past the end of the file."
                    );
                }
            }

            long crcAt = reader.Position / 8;
            int crc = reader.RawShort() & 0xFFFF;
            int[] masks = { 0, 0, 0, 0xA598, 0x8101, 0x3CC4, 0x8461 };
            int expected = DwgCrc.Of(file, 0, crcAt, 0) ^ (count < masks.Length ? masks[count] : 0);

            if (crc != expected)
            {
                throw new FormatException("The DWG file header has a wrong check sum.");
            }

            if (!Matches(file, reader.Position / 8, FileHeaderEnd))
            {
                throw new FormatException("The DWG file header does not end with its sentinel.");
            }

            var container = new DwgContainer
            {
                Version = version,
                MaintenanceVersion = maintenance,
                CodePage = codePage >= 0 && codePage < CodePageNames.Length ? CodePageNames[codePage] : "ANSI_1252",
                Header = Section(file, seekers[0], sizes[0], HeaderStart, HeaderEnd, "header variables"),
                Classes = Section(file, seekers[1], sizes[1], ClassesStart, ClassesEnd, "classes"),
                ObjectMap = ReadObjectMap(file, seekers[2], sizes[2], file.LongLength),
                Objects = file,
                Measurement = -1,
            };

            if (count > 4 && sizes[4] >= 2)
            {
                container.Measurement = file[seekers[4]] | (file[seekers[4] + 1] << 8);
            }

            return container;
        }

        /// <summary>
        /// The data of a section that stands between two sentinels: the start sentinel, the size as a raw long, the data,
        /// the check sum of the size and the data as a raw short, the end sentinel.
        /// </summary>
        private static byte[] Section(byte[] file, long seeker, long size, byte[] start, byte[] end, string name)
        {
            if (size < 38 || !Matches(file, seeker, start))
            {
                throw new FormatException("The DWG section of the " + name + " does not start with its sentinel.");
            }

            long dataSize = (uint)(
                file[seeker + 16] | (file[seeker + 17] << 8) | (file[seeker + 18] << 16) | (file[seeker + 19] << 24)
            );

            if (dataSize + 38 > size)
            {
                throw new FormatException("The DWG section of the " + name + " names more data than it holds.");
            }

            long crcAt = seeker + 20 + dataSize;
            int crc = file[crcAt] | (file[crcAt + 1] << 8);

            if (crc != DwgCrc.Of(file, seeker + 16, 4 + dataSize, DwgCrc.Seed))
            {
                throw new FormatException("The DWG section of the " + name + " has a wrong check sum.");
            }

            if (!Matches(file, crcAt + 2, end))
            {
                throw new FormatException("The DWG section of the " + name + " does not end with its sentinel.");
            }

            var data = new byte[dataSize];
            Array.Copy(file, seeker + 20, data, 0, dataSize);
            return data;
        }

        /// <summary>
        /// The object map of R13 to R2000: pages of at most 2032 bytes, each its size as a big-endian short (the size
        /// counts itself), then pairs of modular chars, the step from the last handle and the step from the last offset,
        /// then the check sum of the page as a big-endian short. Both steps start again from 0 in every page; a page of
        /// size 2 ends the map. Every offset is to lie inside the <paramref name="objects"/> bytes the objects are in. From
        /// R2004 on the map is a section of its own, the same in form.
        /// </summary>
        private static List<KeyValuePair<ulong, long>> ReadObjectMap(byte[] file, long seeker, long size, long objects)
        {
            var map = new List<KeyValuePair<ulong, long>>();
            long at = seeker;
            long sectionEnd = seeker + size;

            while (at + 2 <= sectionEnd)
            {
                int pageSize = (file[at] << 8) | file[at + 1];

                if (pageSize < 2 || at + pageSize + 2 > sectionEnd)
                {
                    throw new FormatException("The DWG object map has a page that runs past its end.");
                }

                int crc = (file[at + pageSize] << 8) | file[at + pageSize + 1];

                if (crc != DwgCrc.Of(file, at, pageSize, DwgCrc.Seed))
                {
                    throw new FormatException("The DWG object map has a wrong check sum.");
                }

                if (pageSize == 2)
                {
                    return map;
                }

                var reader = new DwgBitReader(file, (at + 2) * 8, (at + pageSize) * 8);
                ulong handle = 0;
                long offset = 0;

                while (reader.Remaining >= 16)
                {
                    handle += (ulong)reader.ModularChar(false);
                    offset += reader.ModularChar();

                    if (offset <= 0 || offset >= objects)
                    {
                        throw new FormatException(
                            "The DWG object map places the object "
                                + handle.ToString("X", CultureInfo.InvariantCulture)
                                + " outside the file."
                        );
                    }

                    map.Add(new KeyValuePair<ulong, long>(handle, offset));
                }

                at += pageSize + 2;
            }

            throw new FormatException("The DWG object map does not end with its empty page.");
        }

        private static bool Matches(byte[] file, long at, byte[] sentinel)
        {
            if (at < 0 || at + sentinel.Length > file.LongLength)
            {
                return false;
            }

            for (int i = 0; i < sentinel.Length; i++)
            {
                if (file[at + i] != sentinel[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
