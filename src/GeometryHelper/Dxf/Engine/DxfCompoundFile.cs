using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A compound file of Microsoft's structured storage (MS-CFB), read for <see cref="DxfOle2Frame"/>: the storage an OLE object
    /// is saved in, as the binary of an OLE2FRAME holds it after its head. The streams of its root storage are read by name, from
    /// its header, its FAT (and the DIFAT sectors past the 109 the header lists), its mini FAT and mini stream, and the tree of
    /// its directory, each checked against the bytes there are: a file damaged or cut short is refused with the reason, never
    /// read past its end, and a chain that runs in a loop is stopped.
    /// </summary>
    internal sealed class DxfCompoundFile
    {
        private const uint EndOfChain = 0xFFFFFFFE;
        private const uint NoStream = 0xFFFFFFFF;
        private const int EntrySize = 128;
        private const int MiniSectorSize = 64;

        private static readonly byte[] Signature = { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };

        private readonly byte[] _bytes;
        private readonly int _offset;
        private readonly int _length;
        private readonly int _sectorSize;
        private readonly int _sectorCount;
        private readonly bool _version4;
        private readonly uint _miniCutoff;
        private readonly uint[] _fat;
        private readonly uint[] _miniFat;
        private readonly byte[] _miniStream;
        private readonly byte[] _directory;

        // The streams of the root storage, each by its name and the index of its entry, in the order of their entries.
        private readonly List<KeyValuePair<string, int>> _streams;

        private DxfCompoundFile(byte[] bytes, int offset, int length, int sectorSize, bool version4, uint miniCutoff, uint[] fat, uint[] miniFat, byte[] miniStream, byte[] directory, List<KeyValuePair<string, int>> streams, Guid rootClassId)
        {
            _bytes = bytes;
            _offset = offset;
            _length = length;
            _sectorSize = sectorSize;
            _sectorCount = (int)(((long)length - sectorSize + sectorSize - 1) / sectorSize);
            _version4 = version4;
            _miniCutoff = miniCutoff;
            _fat = fat;
            _miniFat = miniFat;
            _miniStream = miniStream;
            _directory = directory;
            _streams = streams;
            RootClassId = rootClassId;
        }

        /// <summary>The class of the root storage (its CLSID): that of the object saved, as <c>ReadClassStg</c> reads it.</summary>
        internal Guid RootClassId { get; }

        /// <summary>
        /// Reads a compound file from some bytes: its header, FAT, DIFAT, mini FAT, mini stream and directory; its streams are read
        /// when asked for. Null, with the reason, where the bytes are not a compound file or one damaged.
        /// </summary>
        /// <param name="bytes">The bytes.</param>
        /// <param name="offset">Where the compound file begins in them.</param>
        /// <param name="length">How many bytes it has.</param>
        /// <param name="why">The reason it is refused; null where it is read.</param>
        internal static DxfCompoundFile Read(byte[] bytes, int offset, int length, out string why)
        {
            why = null;

            if (bytes == null || offset < 0 || length < 512 || (long)offset + length > bytes.Length)
            {
                why = "it is shorter than the 512 bytes of its header";
                return null;
            }

            for (int i = 0; i < Signature.Length; i++)
            {
                if (bytes[offset + i] != Signature[i])
                {
                    why = "it does not begin with the signature of a compound file";
                    return null;
                }
            }

            int major = U16(bytes, offset + 0x1A);
            int shift = U16(bytes, offset + 0x1E);

            if (U16(bytes, offset + 0x1C) != 0xFFFE || !((major == 3 && shift == 9) || (major == 4 && shift == 12)) || U16(bytes, offset + 0x20) != 6)
            {
                why = "its header gives a version, byte order or sector size MS-CFB has not";
                return null;
            }

            int sectorSize = 1 << shift;
            long sectors = ((long)length - sectorSize + sectorSize - 1) / sectorSize;

            if (sectors <= 0)
            {
                why = "it holds no sector after its header";
                return null;
            }

            uint fatCount = U32(bytes, offset + 0x2C);
            uint firstDirectory = U32(bytes, offset + 0x30);
            uint miniCutoff = U32(bytes, offset + 0x38);
            uint firstMiniFat = U32(bytes, offset + 0x3C);
            uint firstDifat = U32(bytes, offset + 0x44);
            int perSector = sectorSize / 4;

            // Every FAT sector is a sector of the file: a count beyond them is damage, not a size to allocate.
            if (fatCount == 0 || fatCount > sectors)
            {
                why = "it counts " + fatCount.ToString(CultureInfo.InvariantCulture) + " FAT sectors in " + sectors.ToString(CultureInfo.InvariantCulture) + " sectors";
                return null;
            }

            var fatSectors = new List<uint>((int)fatCount);

            for (int i = 0; i < 109 && fatSectors.Count < fatCount; i++)
            {
                fatSectors.Add(U32(bytes, offset + 0x4C + 4 * i));
            }

            // The DIFAT sectors, each listing the FAT sectors past the header's 109 and, last, the next DIFAT sector.
            uint difat = firstDifat;
            var seen = new HashSet<uint>();

            while (fatSectors.Count < fatCount)
            {
                if (difat >= sectors || !seen.Add(difat))
                {
                    why = "its DIFAT lists " + fatSectors.Count.ToString(CultureInfo.InvariantCulture) + " of its " + fatCount.ToString(CultureInfo.InvariantCulture) + " FAT sectors, or runs in a loop";
                    return null;
                }

                int at = offset + (int)((difat + 1) * sectorSize);

                if ((long)at + sectorSize > (long)offset + length)
                {
                    why = "a DIFAT sector runs past its end";
                    return null;
                }

                for (int i = 0; i < perSector - 1 && fatSectors.Count < fatCount; i++)
                {
                    fatSectors.Add(U32(bytes, at + 4 * i));
                }

                difat = U32(bytes, at + 4 * (perSector - 1));
            }

            var fat = new uint[(long)fatCount * perSector];

            for (int k = 0; k < fatSectors.Count; k++)
            {
                uint sector = fatSectors[k];
                long at = offset + ((long)sector + 1) * sectorSize;

                if (sector >= sectors || at + sectorSize > (long)offset + length)
                {
                    why = "its FAT sector " + k.ToString(CultureInfo.InvariantCulture) + " is not a sector of the file";
                    return null;
                }

                for (int i = 0; i < perSector; i++)
                {
                    fat[k * perSector + i] = U32(bytes, (int)at + 4 * i);
                }
            }

            var file = new DxfCompoundFile(bytes, offset, length, sectorSize, major == 4, miniCutoff, fat, null, null, null, null, Guid.Empty);
            byte[] directory = file.ReadChain(firstDirectory, -1, out why);

            if (directory == null || directory.Length < EntrySize)
            {
                why = "its directory " + (why ?? "holds no entry");
                return null;
            }

            int entries = directory.Length / EntrySize;

            if (directory[66] != 5)
            {
                why = "the first entry of its directory is not its root";
                return null;
            }

            var classId = new byte[16];
            Array.Copy(directory, 80, classId, 0, 16);

            // The mini stream, the root's own stream, and the mini FAT of its sectors of 64 bytes.
            byte[] miniStream = new byte[0];
            uint[] miniFat = new uint[0];
            long rootSize = file.SizeOf(directory, 0);

            if (rootSize > 0)
            {
                miniStream = file.ReadChain(U32(directory, 116), rootSize, out why);

                if (miniStream == null)
                {
                    why = "its mini stream " + why;
                    return null;
                }

                if (firstMiniFat != EndOfChain && firstMiniFat != NoStream)
                {
                    byte[] table = file.ReadChain(firstMiniFat, -1, out why);

                    if (table == null)
                    {
                        why = "its mini FAT " + why;
                        return null;
                    }

                    miniFat = new uint[table.Length / 4];

                    for (int i = 0; i < miniFat.Length; i++)
                    {
                        miniFat[i] = U32(table, 4 * i);
                    }
                }
            }

            // The streams of the root storage: its child and the siblings of the tree under it, each entry once.
            var streams = new List<KeyValuePair<string, int>>();
            var visited = new bool[entries];
            var stack = new Stack<uint>();
            stack.Push(U32(directory, 76));

            while (stack.Count > 0)
            {
                uint id = stack.Pop();

                if (id >= entries || visited[id])
                {
                    continue;
                }

                visited[id] = true;
                int at = (int)id * EntrySize;
                stack.Push(U32(directory, at + 72));
                stack.Push(U32(directory, at + 68));

                if (directory[at + 66] == 2)
                {
                    streams.Add(new KeyValuePair<string, int>(NameOf(directory, at), (int)id));
                }
            }

            streams.Sort((a, b) => a.Value.CompareTo(b.Value));
            return new DxfCompoundFile(bytes, offset, length, sectorSize, major == 4, miniCutoff, fat, miniFat, miniStream, directory, streams, new Guid(classId));
        }

        /// <summary>
        /// The bytes of a stream of the root storage, its name compared as MS-CFB compares them, letters of either case the same;
        /// null where the root has no stream of the name, or, with the reason, where its sectors run past the end of the file or
        /// in a loop.
        /// </summary>
        internal byte[] Stream(string name, out string why)
        {
            why = null;

            foreach (KeyValuePair<string, int> stream in _streams)
            {
                if (!string.Equals(stream.Key, name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                int at = stream.Value * EntrySize;
                long size = SizeOf(_directory, at);
                uint start = U32(_directory, at + 116);
                byte[] data = size < _miniCutoff ? ReadMiniChain(start, size, out why) : ReadChain(start, size, out why);

                if (data == null)
                {
                    why = "its stream " + Shown(name) + " " + why;
                }

                return data;
            }

            return null;
        }

        /// <summary>
        /// The bytes of a chain of sectors from its first: as many as the size where it is given, all its sectors where it is -1.
        /// Null, with the reason, where a sector is not one of the file, the chain is shorter than the size, or a sector comes
        /// again within the length it is read for, as in a loop, which only damage makes.
        /// </summary>
        private byte[] ReadChain(uint first, long size, out string why)
        {
            why = null;
            var sectors = new List<uint>();
            var taken = new bool[_sectorCount];
            uint sector = first;

            while (sector != EndOfChain)
            {
                if (sector >= _fat.Length || sector >= _sectorCount || taken[sector])
                {
                    why = sector < _sectorCount && sector < _fat.Length ? "comes back to its sector " + sector.ToString(CultureInfo.InvariantCulture) : "runs to a sector the file has not";
                    return null;
                }

                taken[sector] = true;
                sectors.Add(sector);

                if (size >= 0 && (long)sectors.Count * _sectorSize >= size)
                {
                    break;
                }

                sector = _fat[sector];
            }

            long total = size >= 0 ? size : (long)sectors.Count * _sectorSize;

            if ((long)sectors.Count * _sectorSize < total)
            {
                why = "has fewer sectors than its size, " + size.ToString(CultureInfo.InvariantCulture) + " bytes";
                return null;
            }

            var data = new byte[total];

            for (int i = 0; i < sectors.Count; i++)
            {
                long from = _offset + ((long)sectors[i] + 1) * _sectorSize;
                long count = Math.Min(_sectorSize, total - (long)i * _sectorSize);

                if (from + count > (long)_offset + _length)
                {
                    why = "runs past the end of the file";
                    return null;
                }

                Array.Copy(_bytes, from, data, (long)i * _sectorSize, count);
            }

            return data;
        }

        /// <summary>The bytes of a chain of sectors of the mini stream, as many as the size; null, with the reason, as <see cref="ReadChain"/> says.</summary>
        private byte[] ReadMiniChain(uint first, long size, out string why)
        {
            why = null;

            if (size > _miniStream.Length)
            {
                why = "is larger than the mini stream it is in";
                return null;
            }

            var data = new byte[size];
            var taken = new bool[_miniFat.Length];
            uint sector = first;
            long done = 0;

            while (done < size)
            {
                if (sector >= _miniFat.Length || (long)(sector + 1) * MiniSectorSize > _miniStream.Length || taken[sector])
                {
                    why = sector < _miniFat.Length && taken[sector] ? "comes back to its sector " + sector.ToString(CultureInfo.InvariantCulture) + " of the mini stream" : "runs to a sector the mini stream has not";
                    return null;
                }

                taken[sector] = true;

                long count = Math.Min(MiniSectorSize, size - done);
                Array.Copy(_miniStream, (long)sector * MiniSectorSize, data, done, count);
                done += count;
                sector = _miniFat[sector];
            }

            return data;
        }

        /// <summary>The size of the stream of an entry: of 64 bits from version 4, its low 32 in version 3, as MS-CFB says a reader take it.</summary>
        private long SizeOf(byte[] directory, int at)
        {
            uint low = U32(directory, at + 120);
            uint high = U32(directory, at + 124);
            return _version4 ? (high > 0 ? long.MaxValue : low) : low;
        }

        /// <summary>The name of an entry, of the UTF-16 letters its length (group 64, in bytes with the closing nought) counts.</summary>
        private static string NameOf(byte[] directory, int at)
        {
            int bytes = U16(directory, at + 64);
            int letters = Math.Max(0, Math.Min(31, bytes / 2 - 1));
            return Encoding.Unicode.GetString(directory, at, letters * 2);
        }

        /// <summary>A name as a message gives it, a letter below the space as its code: <c>\1Ole</c>.</summary>
        internal static string Shown(string name)
        {
            var b = new StringBuilder();

            foreach (char c in name)
            {
                b.Append(c < ' ' ? "\\" + ((int)c).ToString(CultureInfo.InvariantCulture) : c.ToString());
            }

            return b.ToString();
        }

        internal static int U16(byte[] bytes, int at) => bytes[at] | (bytes[at + 1] << 8);

        internal static uint U32(byte[] bytes, int at) => (uint)(bytes[at] | (bytes[at + 1] << 8) | (bytes[at + 2] << 16) | (bytes[at + 3] << 24));
    }
}
