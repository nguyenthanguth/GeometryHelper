using System;
using System.Collections.Generic;
using System.Text;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// Writes a compound file of OLE's structured storage (MS-CFB, version 3: sectors of 512 bytes, mini sectors of 64), for
    /// <see cref="DxfOle2Frame"/>: a root of a class, its streams side by side, as Windows' <c>OleSave</c> writes a static
    /// picture. A stream under 4 096 bytes goes into the mini stream, a larger one into sectors of its own. The sectors are
    /// laid out in one order, the FAT first, then the directory, the mini FAT, the mini stream and the streams; every time
    /// is nought, so that the same streams give the same bytes.
    /// </summary>
    internal static class DxfCompoundFileWriter
    {
        private const int Sector = 512;
        private const int Mini = 64;
        private const int Cutoff = 4096;
        private const uint Free = 0xFFFFFFFF;
        private const uint End = 0xFFFFFFFE;
        private const uint FatSector = 0xFFFFFFFD;
        private const uint DifatSector = 0xFFFFFFFC;
        private const uint NoStream = 0xFFFFFFFF;

        /// <summary>The compound file of a root of the class given and the streams given, in any order.</summary>
        internal static byte[] Write(Guid root, IReadOnlyList<KeyValuePair<string, byte[]>> streams)
        {
            // The streams in the order of the directory's tree: by length of name, then by letters upper-cased.
            var sorted = new List<KeyValuePair<string, byte[]>>(streams);
            sorted.Sort((a, b) => Compare(a.Key, b.Key));

            // The mini stream and its FAT.
            var mini = new List<byte>();
            var miniFat = new List<uint>();
            var starts = new uint[sorted.Count];
            var big = new List<int>();

            for (int i = 0; i < sorted.Count; i++)
            {
                byte[] data = sorted[i].Value;

                if (data.Length >= Cutoff)
                {
                    big.Add(i);
                    continue;
                }

                int count = (data.Length + Mini - 1) / Mini;
                starts[i] = count == 0 ? End : (uint)miniFat.Count;

                for (int k = 0; k < count; k++)
                {
                    miniFat.Add(k + 1 < count ? (uint)(miniFat.Count + 1) : End);
                }

                mini.AddRange(data);
                mini.AddRange(new byte[count * Mini - data.Length]);
            }

            int directorySectors = (sorted.Count + 1 + 3) / 4;
            int miniFatSectors = (miniFat.Count * 4 + Sector - 1) / Sector;
            int miniSectors = (mini.Count + Sector - 1) / Sector;
            int bigSectors = 0;

            foreach (int i in big)
            {
                bigSectors += (sorted[i].Value.Length + Sector - 1) / Sector;
            }

            // The FAT and DIFAT sectors needed to count every sector, themselves among them.
            int data0 = directorySectors + miniFatSectors + miniSectors + bigSectors;
            int fatSectors = 1;
            int difatSectors = 0;

            while (true)
            {
                int total = fatSectors + difatSectors + data0;
                int needFat = (total + 127) / 128;
                int needDifat = needFat <= 109 ? 0 : (needFat - 109 + 126) / 127;

                if (needFat == fatSectors && needDifat == difatSectors)
                {
                    break;
                }

                fatSectors = needFat;
                difatSectors = needDifat;
            }

            int totalSectors = fatSectors + difatSectors + data0;
            var fat = new uint[fatSectors * 128];

            for (int i = 0; i < fat.Length; i++)
            {
                fat[i] = Free;
            }

            int at = 0;

            for (int i = 0; i < fatSectors; i++)
            {
                fat[at++] = FatSector;
            }

            for (int i = 0; i < difatSectors; i++)
            {
                fat[at++] = DifatSector;
            }

            int directoryStart = at;
            at = Chain(fat, at, directorySectors);
            int miniFatStart = miniFatSectors == 0 ? -1 : at;
            at = Chain(fat, at, miniFatSectors);
            int miniStart = miniSectors == 0 ? -1 : at;
            at = Chain(fat, at, miniSectors);

            foreach (int i in big)
            {
                int count = (sorted[i].Value.Length + Sector - 1) / Sector;
                starts[i] = (uint)at;
                at = Chain(fat, at, count);
            }

            var file = new byte[(totalSectors + 1) * Sector];
            Header(file, fatSectors, difatSectors, directoryStart, miniFatStart, miniFatSectors);

            // The FAT, then the DIFAT beyond the header's 109.
            for (int i = 0; i < fat.Length; i++)
            {
                PutU32(file, Sector + 4 * i, fat[i]);
            }

            for (int i = 0; i < Math.Min(fatSectors, 109); i++)
            {
                PutU32(file, 76 + 4 * i, (uint)i);
            }

            for (int i = fatSectors; i < 109; i++)
            {
                PutU32(file, 76 + 4 * i, Free);
            }

            for (int d = 0; d < difatSectors; d++)
            {
                int offset = (fatSectors + d + 1) * Sector;

                for (int k = 0; k < 127; k++)
                {
                    int index = 109 + 127 * d + k;
                    PutU32(file, offset + 4 * k, index < fatSectors ? (uint)index : Free);
                }

                PutU32(file, offset + 508, d + 1 < difatSectors ? (uint)(fatSectors + d + 1) : End);
            }

            // The directory: the root, then the streams in their order, a tree of them balanced about the middle, all black.
            int directory = (directoryStart + 1) * Sector;
            Entry(
                file,
                directory,
                "Root Entry",
                5,
                NoStream,
                NoStream,
                sorted.Count == 0 ? NoStream : (uint)(Middle(0, sorted.Count) + 1),
                root,
                miniStart < 0 ? End : (uint)miniStart,
                (ulong)(miniSectors == 0 ? 0 : mini.Count)
            );

            for (int i = 0; i < sorted.Count; i++)
            {
                Tree(0, sorted.Count, i, out int left, out int right);
                Entry(
                    file,
                    directory + 128 * (i + 1),
                    sorted[i].Key,
                    2,
                    left < 0 ? NoStream : (uint)(left + 1),
                    right < 0 ? NoStream : (uint)(right + 1),
                    NoStream,
                    Guid.Empty,
                    starts[i],
                    (ulong)sorted[i].Value.Length
                );
            }

            for (int i = sorted.Count + 1; i < directorySectors * 4; i++)
            {
                Entry(file, directory + 128 * i, string.Empty, 0, NoStream, NoStream, NoStream, Guid.Empty, 0, 0);
            }

            if (miniFatStart >= 0)
            {
                int offset = (miniFatStart + 1) * Sector;

                for (int i = 0; i < miniFatSectors * 128; i++)
                {
                    PutU32(file, offset + 4 * i, i < miniFat.Count ? miniFat[i] : Free);
                }
            }

            if (miniStart >= 0)
            {
                mini.CopyTo(file, (miniStart + 1) * Sector);
            }

            foreach (int i in big)
            {
                Array.Copy(sorted[i].Value, 0, file, ((int)starts[i] + 1) * Sector, sorted[i].Value.Length);
            }

            return file;
        }

        /// <summary>Marks a run of sectors from <paramref name="start"/> as one chain; the sector after it.</summary>
        private static int Chain(uint[] fat, int start, int count)
        {
            for (int k = 0; k < count; k++)
            {
                fat[start + k] = k + 1 < count ? (uint)(start + k + 1) : End;
            }

            return start + count;
        }

        /// <summary>The middle of a run of the sorted streams, the root of its part of the tree.</summary>
        private static int Middle(int from, int to) => from + (to - from) / 2;

        /// <summary>The left and right of the stream <paramref name="index"/> in the tree of the run from <paramref name="from"/> to <paramref name="to"/>.</summary>
        private static void Tree(int from, int to, int index, out int left, out int right)
        {
            int middle = Middle(from, to);

            if (index < middle)
            {
                Tree(from, middle, index, out left, out right);
                return;
            }

            if (index > middle)
            {
                Tree(middle + 1, to, index, out left, out right);
                return;
            }

            left = from < middle ? Middle(from, middle) : -1;
            right = middle + 1 < to ? Middle(middle + 1, to) : -1;
        }

        /// <summary>The order of the directory's tree: a shorter name first, then the letters upper-cased, as MS-CFB 2.6.4 compares them.</summary>
        private static int Compare(string a, string b)
        {
            if (a.Length != b.Length)
            {
                return a.Length.CompareTo(b.Length);
            }

            return string.CompareOrdinal(a.ToUpperInvariant(), b.ToUpperInvariant());
        }

        private static void Header(
            byte[] file,
            int fatSectors,
            int difatSectors,
            int directoryStart,
            int miniFatStart,
            int miniFatSectors
        )
        {
            byte[] signature = { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };
            signature.CopyTo(file, 0);
            PutU16(file, 24, 0x003E);
            PutU16(file, 26, 3);
            PutU16(file, 28, 0xFFFE);
            PutU16(file, 30, 9);
            PutU16(file, 32, 6);
            PutU32(file, 44, (uint)fatSectors);
            PutU32(file, 48, (uint)directoryStart);
            PutU32(file, 56, Cutoff);
            PutU32(file, 60, miniFatStart < 0 ? End : (uint)miniFatStart);
            PutU32(file, 64, (uint)miniFatSectors);
            PutU32(file, 68, difatSectors == 0 ? End : (uint)fatSectors);
            PutU32(file, 72, (uint)difatSectors);
        }

        private static void Entry(
            byte[] file,
            int offset,
            string name,
            byte type,
            uint left,
            uint right,
            uint child,
            Guid classId,
            uint start,
            ulong size
        )
        {
            byte[] letters = Encoding.Unicode.GetBytes(name);
            Array.Copy(letters, 0, file, offset, Math.Min(letters.Length, 62));
            PutU16(file, offset + 64, type == 0 ? 0 : letters.Length + 2);
            file[offset + 66] = type;
            file[offset + 67] = (byte)(type == 0 ? 0 : 1);
            PutU32(file, offset + 68, left);
            PutU32(file, offset + 72, right);
            PutU32(file, offset + 76, child);
            classId.ToByteArray().CopyTo(file, offset + 80);
            PutU32(file, offset + 116, start);
            PutU32(file, offset + 120, (uint)size);
            PutU32(file, offset + 124, (uint)(size >> 32));
        }

        private static void PutU16(byte[] b, int at, int value)
        {
            b[at] = (byte)value;
            b[at + 1] = (byte)(value >> 8);
        }

        private static void PutU32(byte[] b, int at, uint value)
        {
            b[at] = (byte)value;
            b[at + 1] = (byte)(value >> 8);
            b[at + 2] = (byte)(value >> 16);
            b[at + 3] = (byte)(value >> 24);
        }
    }
}
