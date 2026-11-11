using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// TrueType fonts written by the tests (check F2), no real font in the repository: the tables AutoCAD 2021's measuring reads
    /// (head, hhea, maxp, OS/2, hmtx, cmap of format 4, loca, the headers of glyf, name) and a kern table it does not, an em
    /// of 1000 units, so that a text 7 high is 0.01 of a unit to the unit where A reaches 700 up.
    /// </summary>
    internal static class TrueTypeFile
    {
        /// <summary>A letter of the font: its number (Unicode, or U+F0xx in a font of symbols), advance and the box of its glyf header; no box for one of no outline.</summary>
        internal sealed class Letter
        {
            internal Letter(int number, int advance, params int[] box)
            {
                Number = number;
                Advance = advance;
                Box = box.Length == 4 ? box : null;
            }

            internal int Number { get; }

            internal int Advance { get; }

            internal int[] Box { get; }
        }

        /// <summary>
        /// The letters of the tests' font: A inked from -10 to 610 and 700 up, H 720 up (as Times New Roman's capitals pass A's
        /// top), g 200 below, the degree sign from 500 to 700 up, a space of no outline, and Ø; .notdef first.
        /// </summary>
        internal static List<Letter> Letters(bool diameterSign = false)
        {
            var letters = new List<Letter>
            {
                new Letter(0, 500, 50, 0, 450, 700),
                new Letter(' ', 250),
                new Letter('A', 600, -10, 0, 610, 700),
                new Letter('V', 600, 0, 0, 600, 700),
                new Letter('H', 700, 50, 0, 650, 720),
                new Letter('g', 500, 20, -200, 480, 500),
                new Letter(0xD8, 800, 0, -30, 800, 730),
                new Letter(0xB0, 500, 100, 500, 400, 700),
            };

            if (diameterSign)
            {
                letters.Add(new Letter(0x2205, 900, 100, 0, 800, 700));
            }

            return letters;
        }

        /// <summary>The bytes of a .ttf of the letters given, its family named as given, bold and italic as said, its letters mapped by the subtable of symbols where said.</summary>
        internal static byte[] Font(
            IList<Letter> letters,
            string family = "GH Test",
            int style = 0,
            bool symbols = false
        ) => Face(0, letters, family, style, symbols);

        /// <summary>The bytes of a .ttc of two faces of the letters given, the first of the family "GH Test", the second "GH Other".</summary>
        internal static byte[] Collection(IList<Letter> letters)
        {
            int second = 20 + Face(0, letters, "GH Test", 0, false).Length;
            var bytes = new List<byte>();
            bytes.AddRange(Encoding.ASCII.GetBytes("ttcf"));
            bytes.AddRange(U32(0x00010000));
            bytes.AddRange(U32(2));
            bytes.AddRange(U32(20));
            bytes.AddRange(U32((uint)second));
            bytes.AddRange(Face(20, letters, "GH Test", 0, false));
            bytes.AddRange(Face(second, letters, "GH Other", 0, false));
            return bytes.ToArray();
        }

        /// <summary>The bytes of an OpenType font of CFF outlines: its offset table alone, as much as tells it from one of TrueType outlines.</summary>
        internal static byte[] Cff() => Encoding.ASCII.GetBytes("OTTO").Concat(new byte[8]).ToArray();

        /// <summary>A face, its offset table and tables, the offsets in its tables counted from the start of the file it stands in at the place given.</summary>
        private static byte[] Face(int at, IList<Letter> letters, string family, int style, bool symbols)
        {
            int n = letters.Count;
            var tables = new SortedDictionary<string, byte[]>(StringComparer.Ordinal)
            {
                {
                    "head",
                    Join(
                        U32(0x00010000),
                        U32(0),
                        U32(0),
                        U32(0x5F0F3CF5),
                        U16(0),
                        U16(1000),
                        new byte[16],
                        U16(0),
                        U16(0),
                        U16(0),
                        U16(0),
                        U16(style),
                        U16(8),
                        U16(2),
                        U16(1),
                        U16(0)
                    )
                },
                { "hhea", Join(U32(0x00010000), U16(800), U16(-250), U16(0), U16(1000), new byte[20], U16(0), U16(n)) },
                { "maxp", Join(U32(0x00005000), U16(n)) },
                { "OS/2", Join(new byte[74], U16(900), U16(210)) },
                { "hmtx", Join(letters.Select(l => Join(U16(l.Advance), U16(0))).ToArray()) },
                {
                    "glyf",
                    Join(
                        letters
                            .Where(l => l.Box != null)
                            .Select(l => Join(U16(0), U16(l.Box[0]), U16(l.Box[1]), U16(l.Box[2]), U16(l.Box[3])))
                            .ToArray()
                    )
                },
                { "cmap", Cmap(letters, symbols) },
                { "name", Name(family) },
                {
                    "kern",
                    Join(
                        U16(0),
                        U16(1),
                        U16(0),
                        U16(20),
                        U16(1),
                        U16(1),
                        U16(6),
                        U16(0),
                        U16(0),
                        U16(letters.IndexOf(letters.First(l => l.Number == ('A' | (symbols ? 0xF000 : 0))))),
                        U16(letters.IndexOf(letters.First(l => l.Number == ('V' | (symbols ? 0xF000 : 0))))),
                        U16(-100)
                    )
                },
            };
            var loca = new List<byte>();
            int glyph = 0;

            foreach (Letter letter in letters)
            {
                loca.AddRange(U32((uint)glyph));
                glyph += letter.Box == null ? 0 : 10;
            }

            loca.AddRange(U32((uint)glyph));
            tables.Add("loca", loca.ToArray());
            var bytes = new List<byte>(Join(U32(0x00010000), U16(tables.Count), U16(0), U16(0), U16(0)));
            int data = at + 12 + (16 * tables.Count);
            var body = new List<byte>();

            foreach (KeyValuePair<string, byte[]> table in tables)
            {
                bytes.AddRange(Encoding.ASCII.GetBytes(table.Key));
                bytes.AddRange(U32(0));
                bytes.AddRange(U32((uint)(data + body.Count)));
                bytes.AddRange(U32((uint)table.Value.Length));
                body.AddRange(table.Value);

                while (body.Count % 4 != 0)
                {
                    body.Add(0);
                }
            }

            bytes.AddRange(body);
            return bytes.ToArray();
        }

        /// <summary>A cmap of one subtable of format 4, of Windows' Unicode (3, 1) or its symbols (3, 0): a segment for each letter, mapped by its delta, then the last of 0xFFFF.</summary>
        private static byte[] Cmap(IList<Letter> letters, bool symbols)
        {
            List<KeyValuePair<int, int>> mapped = letters
                .Select((l, i) => new KeyValuePair<int, int>(l.Number, i))
                .Where(p => p.Key > 0)
                .OrderBy(p => p.Key)
                .ToList();
            mapped.Add(new KeyValuePair<int, int>(0xFFFF, 0));
            int segments = mapped.Count;
            byte[] format4 = Join(
                U16(4),
                U16(16 + (8 * segments)),
                U16(0),
                U16(2 * segments),
                U16(0),
                U16(0),
                U16(0),
                Join(mapped.Select(p => U16(p.Key)).ToArray()),
                U16(0),
                Join(mapped.Select(p => U16(p.Key)).ToArray()),
                Join(mapped.Select(p => U16(p.Key == 0xFFFF ? 1 : p.Value - p.Key)).ToArray()),
                Join(mapped.Select(p => U16(0)).ToArray())
            );
            return Join(U16(0), U16(1), U16(3), U16(symbols ? 0 : 1), U32(12), format4);
        }

        /// <summary>A name table of one name, the family (1) of Windows' platform in US English, its string UTF-16 big-endian.</summary>
        private static byte[] Name(string family)
        {
            byte[] text = Encoding.BigEndianUnicode.GetBytes(family);
            return Join(U16(0), U16(1), U16(18), U16(3), U16(1), U16(0x409), U16(1), U16(text.Length), U16(0), text);
        }

        private static byte[] U16(int value) => new[] { (byte)(value >> 8), (byte)value };

        private static byte[] U32(uint value) =>
            new[] { (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value };

        private static byte[] Join(params byte[][] parts) => parts.SelectMany(p => p).ToArray();
    }
}
