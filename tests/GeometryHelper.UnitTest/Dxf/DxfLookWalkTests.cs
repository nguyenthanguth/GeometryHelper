using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfLines;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// What one walk over the head record of an entity gives: its look (layer, colour, true colour, linetype, lineweight and
    /// thickness, each the first of its code), its handle, and whether it is hidden, read from the bytes against what the
    /// generated records were written with; the greatest handle of a file, found as its records are walked, against the greatest
    /// the text holds; the origin of a block, kept until the file next changes; and what each gives after an edit.
    /// </summary>
    /// <remarks>
    /// The generated records run to <c>GH_FUZZ_COUNT</c> a test (400 where it is not set), with <c>GH_FUZZ_SEED</c> moving the
    /// seed.
    /// </remarks>
    public class DxfLookWalkTests
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static int Count() =>
            int.TryParse(Environment.GetEnvironmentVariable("GH_FUZZ_COUNT"), NumberStyles.Integer, Inv, out int n)
            && n > 0
                ? n
                : 400;

        private static int Seed() =>
            int.TryParse(Environment.GetEnvironmentVariable("GH_FUZZ_SEED"), NumberStyles.Integer, Inv, out int n)
                ? n
                : 0;

        /// <summary>One group as the file writes it, the code padded to three places or not.</summary>
        private static void Group(StringBuilder text, int code, string value, bool pad) =>
            text.Append(pad ? code.ToString(Inv).PadLeft(3) : code.ToString(Inv))
                .Append('\n')
                .Append(value)
                .Append('\n');

        /// <summary>The look an entity is written with: null for a group the record leaves out.</summary>
        private sealed class Written
        {
            public string Layer;
            public string Colour;
            public string TrueColour;
            public string Linetype;
            public string Lineweight;
            public string Thickness;
        }

        /// <summary>What the generated entity is written with, and what it should read as.</summary>
        private sealed class Made
        {
            public string Handle;
            public Written Look;
            public bool Hidden;
        }

        private static readonly string[] Layers = { "0", "Steel", " Steel", "Notes ", "Bolts", "A|B", "Grid", "Dim 1" };

        private static Written RandomLook(Random random)
        {
            string Pick(string[] values, double chance) =>
                random.NextDouble() < chance ? null : values[random.Next(values.Length)];

            return new Written
            {
                Layer = Pick(Layers, 0.1),
                Colour = Pick(new[] { "1", "7", " 3", "256", "0", "250 ", "-5" }, 0.5),
                TrueColour = Pick(new[] { "16711680", "255", "8421504" }, 0.8),
                Linetype = Pick(new[] { "DASHED", "ByLayer", "CENTER", "ByBlock" }, 0.7),
                Lineweight = Pick(new[] { "-1", "25", "-3", "50", "211" }, 0.7),
                Thickness = Pick(new[] { "2.5", "0", "10", "-1.5" }, 0.85),
            };
        }

        /// <summary>
        /// A LINE written with a look and a handle, the groups in an order of the random's choosing: the look's before the first
        /// marker, between the markers or after the geometry (thickness is there where AutoCAD puts it); a second of some
        /// groups that the first must win over; the group 60 before the markers, after them, inside a braced group or after
        /// the extended data.
        /// </summary>
        private static Made Line(StringBuilder text, Random random, int number)
        {
            var made = new Made { Handle = (0x100 + number * 3).ToString("X", Inv), Look = RandomLook(random) };
            bool pad = random.Next(2) == 0;
            text.Append(pad ? "  0\nLINE\n" : "0\nLINE\n");
            string handle = random.Next(6) == 0 ? " " + made.Handle + " " : made.Handle;
            Group(text, 5, handle, pad);

            // Where the group 60 goes: 0 none, 1 in the common part, 2 after the first marker that is not AcDbEntity, 3 inside a
            // braced group, 4 after the extended data, 5 after the marker AcDbEntity.
            int sixty = random.Next(6);
            string value =
                random.Next(3) == 0 ? "0"
                : random.Next(2) == 0 ? "1"
                : " 1 ";
            bool wantsHidden = value.Trim() == "1";

            if (sixty == 3)
            {
                Group(text, 102, random.Next(2) == 0 ? "{ACAD_REACTORS" : "  {ACAD_XDICTIONARY", pad);
                Group(text, 330, "1F", pad);
                Group(text, 60, value, pad);
                Group(text, 102, "}", pad);
            }

            if (sixty == 1)
            {
                Group(text, 60, value, pad);
            }

            Written look = made.Look;

            void Emit(int code, string v)
            {
                if (v != null)
                {
                    Group(text, code, v, pad);
                }
            }

            Emit(8, look.Layer);
            Emit(6, look.Linetype);
            Emit(62, look.Colour);

            if (random.Next(4) == 0)
            {
                Emit(420, look.TrueColour);
                Emit(370, look.Lineweight);
            }
            else
            {
                Emit(370, look.Lineweight);
                Emit(420, look.TrueColour);
            }

            Group(text, 100, "AcDbEntity", pad);

            if (sixty == 5)
            {
                Group(text, 60, value, pad);
            }

            Group(text, 100, "AcDbLine", pad);

            if (sixty == 2)
            {
                Group(text, 60, value, pad);
            }

            Emit(39, look.Thickness);
            Group(text, 10, "0", pad);
            Group(text, 20, "0", pad);
            Group(text, 30, "0", pad);
            Group(text, 11, "5", pad);
            Group(text, 21, "5", pad);
            Group(text, 31, "0", pad);

            // A second group of each code after the geometry: the first of its code is the look's, and where the record had
            // none before, this one is.
            if (random.Next(4) == 0)
            {
                Group(text, 8, "Later", pad);
                Group(text, 62, "9", pad);
                Group(text, 370, "99", pad);
                look.Layer = look.Layer ?? "Later";
                look.Colour = look.Colour ?? "9";
                look.Lineweight = look.Lineweight ?? "99";

                if (look.Thickness == null)
                {
                    Group(text, 39, "7.5", pad);
                    look.Thickness = "7.5";
                }
            }

            if (sixty == 4)
            {
                Group(text, 1001, "ACAD", pad);
                Group(text, 1000, "x", pad);
                Group(text, 60, value, pad);
            }

            // Hidden: a group 60 of 1 in the part every entity has, which the marker AcDbEntity does not end.
            made.Hidden = wantsHidden && (sixty == 1 || sixty == 5);
            return made;
        }

        private static DxfDatabase Parse(StringBuilder entities, string version = "AC1015") =>
            DxfDatabase.Parse(File(version, 4, string.Empty, entities.ToString()));

        private static int? Whole(string value) =>
            value != null && int.TryParse(value.Trim(), NumberStyles.Integer, Inv, out int n) ? n : (int?)null;

        [Fact]
        public void ManyLooks_AreReadAsTheirRecordsWriteThem_EachTheFirstOfItsCode()
        {
            int seed = Seed();
            var random = new Random(seed);
            var text = new StringBuilder();
            var made = new List<Made>();

            for (int i = 0; i < Count(); i++)
            {
                made.Add(Line(text, random, i));
            }

            DxfDatabase db = Parse(text);
            List<DxfEntity> entities = db.ModelSpace.ToList();
            Assert.Equal(made.Count, entities.Count);

            for (int i = 0; i < made.Count; i++)
            {
                DxfItem item = entities[i].Item;
                Written look = made[i].Look;
                string where = "entity " + i + " of seed " + seed;
                Assert.True(look != null, where);
                Assert.Equal(made[i].Handle, item.Handle);
                Assert.Equal(look.Layer, item.Layer);
                Assert.Equal(Whole(look.Colour), item.Colour);
                Assert.Equal(Whole(look.TrueColour), item.TrueColour);
                Assert.Equal(look.Linetype, item.Linetype);
                Assert.Equal(Whole(look.Lineweight), item.Lineweight);
                Assert.Equal(look.Thickness == null ? 0.0 : double.Parse(look.Thickness, Inv), item.Thickness);
                Assert.Equal(!made[i].Hidden, entities[i].Visible);
            }
        }

        [Fact]
        public void MoreLooksThanTheFileRemembersByTheirBytes_AreReadAsTheyAre()
        {
            // 300 layers of one colour each, twice round: the file's memory of looks is smaller, and each hit and each miss reads true.
            var text = new StringBuilder();

            for (int round = 0; round < 2; round++)
            {
                for (int i = 0; i < 300; i++)
                {
                    text.Append("0\nLINE\n5\n").Append((0x100 + round * 300 + i).ToString("X", Inv)).Append('\n');
                    text.Append("8\nL").Append(i.ToString(Inv)).Append("\n62\n").Append(1 + i % 200).Append('\n');
                    text.Append("10\n0\n20\n0\n30\n0\n11\n1\n21\n1\n31\n0\n");
                }
            }

            List<DxfEntity> entities = Parse(text).ModelSpace.ToList();

            for (int k = 0; k < entities.Count; k++)
            {
                int i = k % 300;
                Assert.Equal("L" + i.ToString(Inv), entities[k].Item.Layer);
                Assert.Equal(1 + i % 200, entities[k].Item.Colour);
            }
        }

        [Fact]
        public void AnEmptyValue_IsNotAValueLeftOut()
        {
            var text = new StringBuilder();
            text.Append("0\nLINE\n5\nA1\n8\n\n6\n\n10\n0\n20\n0\n30\n0\n11\n1\n21\n1\n31\n0\n");
            text.Append("0\nLINE\n5\nA2\n10\n0\n20\n0\n30\n0\n11\n1\n21\n1\n31\n0\n");
            text.Append("0\nLINE\n5\nA3\n8\n\n6\n\n10\n0\n20\n0\n30\n0\n11\n1\n21\n1\n31\n0\n");
            List<DxfEntity> entities = Parse(text).ModelSpace.ToList();

            Assert.Equal(string.Empty, entities[0].Item.Layer);
            Assert.Equal(string.Empty, entities[0].Item.Linetype);
            Assert.Null(entities[1].Item.Layer);
            Assert.Null(entities[1].Item.Linetype);
            Assert.Equal(string.Empty, entities[2].Item.Layer);
        }

        [Fact]
        public void AnEntityEditedAfterItsLookWasRead_IsReadWithTheNewLook()
        {
            var text = new StringBuilder();
            text.Append("0\nLINE\n5\nA1\n8\nSteel\n62\n1\n10\n0\n20\n0\n30\n0\n11\n1\n21\n1\n31\n0\n");
            text.Append("0\nLINE\n5\nA2\n8\nSteel\n62\n1\n10\n0\n20\n0\n30\n0\n11\n1\n21\n1\n31\n0\n");
            DxfDatabase db = Parse(text);
            List<DxfEntity> entities = db.ModelSpace.ToList();
            Assert.Equal("Steel", entities[0].Item.Layer);
            Assert.Equal(1, entities[0].Item.Colour);
            Assert.True(entities[0].Visible);

            entities[0].Layer = "Notes";
            entities[0].Visible = false;
            entities[0].Item.Colour = 5;

            Assert.Equal("Notes", entities[0].Item.Layer);
            Assert.Equal(5, entities[0].Item.Colour);
            Assert.False(entities[0].Visible);
            Assert.Equal("Steel", entities[1].Item.Layer);
            Assert.Equal(1, entities[1].Item.Colour);
            Assert.True(entities[1].Visible);

            entities[0].Visible = true;
            Assert.True(entities[0].Visible);
            Assert.Equal("A1", entities[0].Item.Handle);
        }

        [Fact]
        public void AGroup102WhoseFirstLetterIsBeyondAscii_IsBracedWhenTrimmedItIsABrace()
        {
            // A no-break space before the brace is white space to Trim, as the bytes C2 A0 of a file of R2007 are read: the
            // group 60 inside is the application's, and the entity is shown.
            string text =
                "0\nLINE\n5\nA1\n102\n {ACAD_REACTORS\n 60\n1\n102\n}\n8\n0\n10\n0\n20\n0\n30\n0\n11\n1\n21\n1\n31\n0\n"
                + "0\nLINE\n5\nA2\n102\n \t{ACAD_REACTORS\n 60\n1\n102\n}\n8\n0\n10\n0\n20\n0\n30\n0\n11\n1\n21\n1\n31\n0\n"
                + "0\nLINE\n5\nA3\n102\né{ACAD_REACTORS\n 60\n1\n102\n}\n8\n0\n10\n0\n20\n0\n30\n0\n11\n1\n21\n1\n31\n0\n"
                + "0\nLINE\n5\nA4\n102\n\n 60\n1\n102\n}\n8\n0\n10\n0\n20\n0\n30\n0\n11\n1\n21\n1\n31\n0\n";
            DxfDatabase db = DxfDatabase.Parse(File("AC1032", 4, string.Empty, text));
            List<DxfEntity> entities = db.ModelSpace.ToList();

            Assert.True(entities[0].Visible);
            Assert.True(entities[1].Visible);
            Assert.False(entities[2].Visible);
            Assert.False(entities[3].Visible);
        }

        [Fact]
        public void EveryAsciiFirstByteOfAGroup102_IsBracedAsTrimReadsIt()
        {
            // For each byte from 1 to 127 before a brace: braced where the byte is white space to Trim, which a hidden 60 inside
            // is then not seen through.
            var text = new StringBuilder();
            var expected = new List<bool>();

            for (int b = 1; b < 128; b++)
            {
                if (b == '\n' || b == '\r')
                {
                    continue;
                }

                string lead = ((char)b).ToString();
                bool braced = (lead + "{X").Trim().StartsWith("{", StringComparison.Ordinal);
                text.Append("0\nLINE\n5\n")
                    .Append((0x100 + b).ToString("X", Inv))
                    .Append("\n102\n")
                    .Append(lead)
                    .Append("{X\n 60\n1\n102\n}\n8\n0\n");
                text.Append("10\n0\n20\n0\n30\n0\n11\n1\n21\n1\n31\n0\n");
                expected.Add(braced);
            }

            List<DxfEntity> entities = Parse(text).ModelSpace.ToList();
            Assert.Equal(expected.Count, entities.Count);

            for (int i = 0; i < expected.Count; i++)
            {
                // Braced: the 60 is the application's and the entity is shown; else it is hidden.
                Assert.Equal(expected[i], entities[i].Visible);
            }
        }

        #region The greatest handle of a file

        [Fact]
        public void TheNextHandle_IsAboveEveryHandleEveryPointerAndTheSeed_OutsideTheHeader()
        {
            // The records: handles up to 2A, a pointer to 300 in a group 340, one to 3F0 in a 1005, a 390 of the header at
            // FFFF that is not counted, the seed 1F4; the greatest is 3F0, so the next is 3F1.
            string header = G(
                0,
                "SECTION",
                2,
                "HEADER",
                9,
                "$ACADVER",
                1,
                "AC1015",
                9,
                "$HANDSEED",
                5,
                "1F4",
                9,
                "$CPSNID",
                390,
                "FFFF",
                0,
                "ENDSEC"
            );
            string entities =
                G(0, "LINE", 5, "2A", 8, "0", 340, "300", 10, 0.0, 20, 0.0, 30, 0.0, 11, 1.0, 21, 1.0, 31, 0.0)
                + G(0, "LINE", 5, "1F", 1005, "3F0", 10, 0.0, 20, 0.0, 30, 0.0, 11, 1.0, 21, 1.0, 31, 0.0);
            DxfDatabase db = DxfDatabase.Parse(
                header + G(0, "SECTION", 2, "ENTITIES") + entities + G(0, "ENDSEC", 0, "EOF")
            );
            var added = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 1, 0));
            db.ModelSpace.AppendEntity(added);

            Assert.Equal("3F1", added.Handle);
        }

        [Fact]
        public void TheNextHandle_OfADimstyle_IsFromItsGroup105_NotItsArrowBlockName()
        {
            string tables = G(
                0,
                "SECTION",
                2,
                "TABLES",
                0,
                "TABLE",
                2,
                "DIMSTYLE",
                0,
                "DIMSTYLE",
                5,
                "DEAD",
                105,
                "9C",
                2,
                "Standard",
                0,
                "ENDTAB",
                0,
                "ENDSEC"
            );
            DxfDatabase db = DxfDatabase.Parse(
                File("AC1015", 4, tables, G(0, "LINE", 5, "20", 10, 0.0, 20, 0.0, 30, 0.0, 11, 1.0, 21, 1.0, 31, 0.0))
            );
            var added = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 1, 0));
            db.ModelSpace.AppendEntity(added);

            Assert.Equal("9D", added.Handle);
        }

        [Fact]
        public void TheNextHandle_OfGeneratedFiles_IsAboveTheGreatestTheTextHolds()
        {
            int seed = Seed();
            var random = new Random(seed);

            for (int file = 0; file < Count() / 4; file++)
            {
                ulong most = 0;
                ulong seedValue = random.Next(3) == 0 ? 0 : (ulong)random.Next(1, 0x5000);
                var header = new StringBuilder();
                Group(header, 0, "SECTION", false);
                Group(header, 2, "HEADER", false);
                Group(header, 9, "$ACADVER", false);
                Group(header, 1, "AC1015", false);

                if (seedValue > 0)
                {
                    Group(header, 9, "$HANDSEED", false);
                    Group(header, 5, seedValue.ToString("X", Inv), false);
                }

                Group(header, 9, "$X", false);
                Group(header, 390, "FFFFFF", false);
                Group(header, 0, "ENDSEC", false);

                var body = new StringBuilder();
                int count = random.Next(1, 40);
                ulong highest = 0;

                for (int i = 0; i < count; i++)
                {
                    bool pad = random.Next(2) == 0;
                    body.Append(pad ? "  0\nLINE\n" : "0\nLINE\n");

                    // The handle, sometimes after other groups, sometimes absent, sometimes not a number; the first 5 or 105 wins.
                    int kind = random.Next(8);

                    if (kind > 0)
                    {
                        ulong handle = (ulong)random.Next(1, 0x4000);

                        if (random.Next(4) == 0)
                        {
                            Group(body, 8, "L", pad);
                        }

                        string written = random.Next(5) == 0 ? handle.ToString("x", Inv) : handle.ToString("X", Inv);

                        if (kind == 7)
                        {
                            Group(body, 105, " " + written, pad);
                        }
                        else if (kind == 6)
                        {
                            Group(body, 5, "NOTHEX", pad);
                            handle = 0;
                        }
                        else
                        {
                            Group(body, 5, written, pad);
                        }

                        if (random.Next(5) == 0)
                        {
                            Group(body, 5, "FFFFF", pad);
                        }

                        highest = Math.Max(highest, handle);
                    }

                    int[] codes =
                    {
                        330,
                        340,
                        360,
                        390,
                        480,
                        481,
                        1005,
                        320,
                        369,
                        399,
                        319,
                        370,
                        400,
                        479,
                        482,
                        1004,
                        1006,
                    };

                    for (int p = random.Next(4); p > 0; p--)
                    {
                        int code = codes[random.Next(codes.Length)];
                        ulong pointed = (ulong)random.Next(1, 0x7000);
                        Group(body, code, pointed.ToString("X", Inv), pad);
                        bool counted =
                            (code >= 320 && code <= 369)
                            || (code >= 390 && code <= 399)
                            || code == 480
                            || code == 481
                            || code == 1005;

                        if (counted)
                        {
                            most = Math.Max(most, pointed);
                        }
                    }

                    Group(body, 10, "0", pad);
                    Group(body, 20, "0", pad);
                    Group(body, 30, "0", pad);
                    Group(body, 11, "1", pad);
                    Group(body, 21, "1", pad);
                    Group(body, 31, "0", pad);
                }

                // The greatest of the records' handles, of the pointers and of the seed less one; the next is above them.
                ulong expected = Math.Max(Math.Max(seedValue > 0 ? seedValue - 1 : 0, highest), most) + 1;
                string textOfFile = header + "0\nSECTION\n2\nENTITIES\n" + body + "0\nENDSEC\n0\nEOF\n";
                DxfDatabase db = DxfDatabase.Parse(textOfFile);
                bool handles = highest > 0 || seedValue > 0;
                var added = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 1, 0));
                db.ModelSpace.AppendEntity(added);

                if (handles)
                {
                    Assert.Equal(expected.ToString("X", Inv), added.Handle);
                }
            }
        }

        #endregion

        #region The origin of a block

        private const string Blocks =
            "0\nSECTION\n2\nBLOCKS\n"
            + "0\nBLOCK\n5\nB1\n8\n0\n2\nB\n70\n0\n10\n5\n20\n7\n30\n0\n3\nB\n1\n\n0\nLINE\n5\nB2\n8\n0\n10\n0\n20\n0\n30\n0\n11\n1\n21\n1\n31\n0\n0\nENDBLK\n5\nB3\n8\n0\n"
            + "0\nENDSEC\n";

        [Fact]
        public void TheOriginOfABlock_ReadAgainAfterTheBlockIsSet_IsTheNewOne()
        {
            DxfDatabase db = DxfDatabase.Parse(File("AC1015", 4, Blocks, string.Empty));
            DxfBlockTableRecord block = db.BlockTable["B"];

            Assert.Equal(new GeoPoint3(5, 7, 0), block.Origin);
            Assert.Equal(new GeoPoint3(5, 7, 0), block.Origin);

            block.Origin = new GeoPoint3(-1.5, 2, 3);
            Assert.Equal(new GeoPoint3(-1.5, 2, 3), block.Origin);
            Assert.Equal(new GeoPoint3(-1.5, 2, 3), db.BlockTable["B"].Origin);

            block.Origin = new GeoPoint3(0, 0, 0);
            Assert.Equal(new GeoPoint3(0, 0, 0), block.Origin);
        }

        [Fact]
        public void TheOriginOfABlock_ReadAgainAfterAnotherEditOfTheFile_IsTheRecordsOwn()
        {
            DxfDatabase db = DxfDatabase.Parse(
                File("AC1015", 4, Blocks, G(0, "INSERT", 5, "A1", 8, "0", 2, "B", 10, 100.0, 20, 50.0, 30, 0.0))
            );
            DxfBlockReference insert = db.ModelSpace.OfType<DxfBlockReference>().Single();
            Assert.True(insert.TryReadForDisplay(out DxfBlockReference.DisplayRead before));
            Assert.Equal(95.0, before.Tx, 12);
            Assert.Equal(43.0, before.Ty, 12);

            // The INSERT moved, which edits the file but not the block, then the block's origin set, the INSERT read each time.
            insert.Position = new GeoPoint3(101.0, 50.0, 0.0);
            Assert.True(insert.TryReadForDisplay(out DxfBlockReference.DisplayRead moved));
            Assert.Equal(96.0, moved.Tx, 12);
            Assert.Equal(43.0, moved.Ty, 12);

            db.BlockTable["B"].Origin = new GeoPoint3(10, 20, 0);
            Assert.True(insert.TryReadForDisplay(out DxfBlockReference.DisplayRead after));
            Assert.Equal(91.0, after.Tx, 12);
            Assert.Equal(30.0, after.Ty, 12);
        }

        [Fact]
        public void TheOriginOfABlockMadeInCode_IsItsOwn_BeforeAndAfterItIsAdded()
        {
            var block = new DxfBlockTableRecord { Name = "MADE", Origin = new GeoPoint3(1, 2, 3) };
            Assert.Equal(new GeoPoint3(1, 2, 3), block.Origin);

            DxfDatabase db = DxfDatabase.Parse(File("AC1015", 4, Blocks, string.Empty));
            db.BlockTable.Add(block);

            Assert.Equal(new GeoPoint3(1, 2, 3), db.BlockTable["MADE"].Origin);
            db.BlockTable["MADE"].Origin = new GeoPoint3(4, 5, 6);
            Assert.Equal(new GeoPoint3(4, 5, 6), block.Origin);
        }

        #endregion
    }
}
