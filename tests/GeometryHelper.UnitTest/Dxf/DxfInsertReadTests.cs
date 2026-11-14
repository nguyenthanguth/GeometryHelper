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
    /// What the display reads of an INSERT in one pass of its groups (<c>DxfBlockReference.TryReadForDisplay</c>) against the
    /// block, the point and the transformation its properties give, and whether it is an array (<c>IsArray</c>) read where its
    /// groups stand against the groups read one by one: the same to the bit over generated records, in whichever order the
    /// groups come, written whichever way, braced, doubled, misread or left out; and what each gives after the INSERT is edited.
    /// </summary>
    /// <remarks>
    /// The generated records run to <c>GH_FUZZ_COUNT</c> a test (1 500 where it is not set), with <c>GH_FUZZ_SEED</c> moving
    /// the seed.
    /// </remarks>
    public class DxfInsertReadTests
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static int Count() =>
            int.TryParse(Environment.GetEnvironmentVariable("GH_FUZZ_COUNT"), NumberStyles.Integer, Inv, out int n)
            && n > 0
                ? n
                : 1500;

        private static int Seed() =>
            int.TryParse(Environment.GetEnvironmentVariable("GH_FUZZ_SEED"), NumberStyles.Integer, Inv, out int n)
                ? n
                : 0;

        /// <summary>Three blocks: B with its origin at (5, 7, 0), b2 at the world's, and C at (-3.5, 2, 1000).</summary>
        private static string Blocks() =>
            G(0, "SECTION", 2, "BLOCKS")
            + Block("B", 5.0, 7.0, 0.0)
            + Block("b2", 0.0, 0.0, 0.0)
            + Block("C", -3.5, 2.0, 1000.0)
            + G(0, "ENDSEC");

        private static string Block(string name, double x, double y, double z) =>
            G(
                0,
                "BLOCK",
                8,
                "0",
                100,
                "AcDbEntity",
                100,
                "AcDbBlockBegin",
                2,
                name,
                70,
                0,
                10,
                x,
                20,
                y,
                30,
                z,
                3,
                name
            )
            + G(1, string.Empty)
            + G(0, "LINE", 8, "0", 10, 0.0, 20, 0.0, 30, 0.0, 11, 1.0, 21, 1.0, 31, 0.0)
            + G(0, "ENDBLK", 8, "0");

        private static DxfDatabase Drawing(string entities) => DxfDatabase.Parse(File("AC1015", 4, Blocks(), entities));

        #region An INSERT, read in one pass

        [Fact]
        public void APlainInsert_IsReadInOnePass_AsItsPropertiesReadIt()
        {
            DxfDatabase db = Drawing(
                G(0, "INSERT", 5, "A1", 8, "0", 100, "AcDbEntity", 100, "AcDbBlockReference", 2, "B")
                    + G(10, 100.0, 20, 50.0, 30, 3.0, 41, 2.0, 42, 3.0, 43, 4.0, 50, 90.0)
            );
            DxfBlockReference insert = db.ModelSpace.OfType<DxfBlockReference>().Single();

            Assert.True(insert.TryReadForDisplay(out DxfBlockReference.DisplayRead read));
            Assert.Same(insert.BlockTableRecord, read.Block);
            Assert.Equal("B", read.Block.Name);
            Assert.Equal(new GeoPoint3(100.0, 50.0, 3.0), read.Position);

            // The block 2 by 3 turned a quarter: its x axis along the world's y, scaled by 2; its origin (5, 7) put at (100, 50).
            Assert.Equal(0.0, read.A, 12);
            Assert.Equal(2.0, read.B, 12);
            Assert.Equal(-3.0, read.C, 12);
            Assert.Equal(0.0, read.D, 12);
            Assert.Equal(121.0, read.Tx, 12);
            Assert.Equal(40.0, read.Ty, 12);
            Assert.Null(Difference(insert));
        }

        [Fact]
        public void AnInsertWithAnExtensionDictionary_IsReadByItsProperties_ForItsContextsMayMoveIt()
        {
            DxfDatabase db = Drawing(
                G(0, "INSERT", 5, "A1", 102, "{ACAD_XDICTIONARY", 360, "B1", 102, "}", 8, "0")
                    + G(100, "AcDbEntity", 100, "AcDbBlockReference", 2, "B", 10, 1.0, 20, 2.0, 30, 0.0)
            );
            DxfBlockReference insert = db.ModelSpace.OfType<DxfBlockReference>().Single();

            Assert.False(insert.TryReadForDisplay(out _));
        }

        [Fact]
        public void AnInsertOfABlockTheDrawingHasNot_IsReadWithNoBlock()
        {
            DxfDatabase db = Drawing(
                G(0, "INSERT", 5, "A1", 8, "0", 100, "AcDbEntity", 100, "AcDbBlockReference", 2, "NONE")
                    + G(10, 1.0, 20, 2.0, 30, 0.0)
            );
            DxfBlockReference insert = db.ModelSpace.OfType<DxfBlockReference>().Single();

            Assert.True(insert.TryReadForDisplay(out DxfBlockReference.DisplayRead read));
            Assert.Null(read.Block);
            Assert.Null(insert.BlockTableRecord);
            Assert.Equal(new GeoPoint3(1.0, 2.0, 0.0), read.Position);
        }

        [Fact]
        public void AnInsertEditedAfterItWasRead_IsReadWithItsNewValues()
        {
            DxfDatabase db = Drawing(
                G(0, "INSERT", 5, "A1", 8, "0", 100, "AcDbEntity", 100, "AcDbBlockReference", 2, "B")
                    + G(10, 100.0, 20, 50.0, 30, 0.0, 50, 30.0)
            );
            DxfBlockReference insert = db.ModelSpace.OfType<DxfBlockReference>().Single();
            Assert.True(insert.TryReadForDisplay(out DxfBlockReference.DisplayRead before));
            Assert.Equal(new GeoPoint3(100.0, 50.0, 0.0), before.Position);
            Assert.Equal("B", before.Block.Name);

            insert.Position = new GeoPoint3(-20.0, 5.5, 0.0);
            insert.Rotation = 0.5;
            insert.ScaleFactors = new GeoVector3(2.0, -1.5, 1.0);
            Assert.True(insert.TryReadForDisplay(out DxfBlockReference.DisplayRead after));
            Assert.Equal(new GeoPoint3(-20.0, 5.5, 0.0), after.Position);
            Assert.Equal(Math.Cos(0.5) * 2.0, after.A, 12);
            Assert.Null(Difference(insert));

            insert.Normal = new GeoVector3(0.0, 0.6, 0.8);
            Assert.True(insert.TryReadForDisplay(out _));
            Assert.Null(Difference(insert));

            insert.BlockTransform = GeoTransform3.Translation(new GeoVector3(7.0, 8.0, 9.0));
            Assert.True(insert.TryReadForDisplay(out _));
            Assert.Null(Difference(insert));

            // Given its block by code, it is read by its properties, which keep the block it was given.
            insert.BlockTableRecord = db.BlockTable["C"];
            Assert.False(insert.TryReadForDisplay(out _));
            Assert.Equal("C", insert.BlockTableRecord.Name);
        }

        [Fact]
        public void AnArrayOfInserts_IsToldByItsMarkerOrItsCounts()
        {
            DxfDatabase db = Drawing(
                G(0, "INSERT", 5, "A1", 8, "0", 100, "AcDbEntity", 100, "AcDbBlockReference", 2, "B")
                    + G(10, 0.0, 20, 0.0, 30, 0.0, 100, "AcDbMInsertBlock", 70, 3, 71, 2, 44, 10.0, 45, 10.0)
                    + G(0, "INSERT", 5, "A2", 8, "0", 100, "AcDbEntity", 100, "AcDbBlockReference", 2, "B")
                    + G(10, 0.0, 20, 0.0, 30, 0.0, 70, 1, 71, 1)
                    + G(0, "INSERT", 5, "A3", 8, "0", 100, "AcDbEntity", 100, "AcDbBlockReference", 2, "B")
                    + G(10, 0.0, 20, 0.0, 30, 0.0, 71, 4)
                    + G(0, "INSERT", 5, "A4", 8, "0", 100, "AcDbEntity", 100, "AcDbMInsertBlock", 2, "B")
            );
            DxfEntity[] entities = db.ModelSpace.ToArray();

            Assert.IsType<DxfMInsertBlock>(entities[0]);
            Assert.IsNotType<DxfMInsertBlock>(entities[1]);
            Assert.IsType<DxfMInsertBlock>(entities[2]);
            Assert.IsType<DxfMInsertBlock>(entities[3]);
        }

        /// <summary>Where the one pass and the properties part, null where they do not.</summary>
        private static string Difference(DxfBlockReference insert)
        {
            if (!insert.TryReadForDisplay(out DxfBlockReference.DisplayRead read))
            {
                return "not read in one pass";
            }

            var wrong = new List<string>();

            void Check(string name, double got, double wanted)
            {
                if (BitConverter.DoubleToInt64Bits(got) != BitConverter.DoubleToInt64Bits(wanted))
                {
                    wrong.Add(name + " " + got.ToString("R", Inv) + " against " + wanted.ToString("R", Inv));
                }
            }

            DxfBlockTableRecord block = insert.BlockTableRecord;

            if (!ReferenceEquals(read.Block, block))
            {
                wrong.Add("Block " + read.Block?.Name + " against " + block?.Name);
            }

            GeoPoint3 at = insert.Position;
            Check("Position.X", read.Position.X, at.X);
            Check("Position.Y", read.Position.Y, at.Y);
            Check("Position.Z", read.Position.Z, at.Z);

            // The transformation is read where there is a block to place.
            if (block != null)
            {
                GeoTransform3 transform = insert.BlockTransform;
                Check("A", read.A, transform[0, 0]);
                Check("B", read.B, transform[1, 0]);
                Check("C", read.C, transform[0, 1]);
                Check("D", read.D, transform[1, 1]);
                Check("Tx", read.Tx, transform[0, 3]);
                Check("Ty", read.Ty, transform[1, 3]);
            }

            return wrong.Count == 0 ? null : string.Join("; ", wrong);
        }

        #endregion

        #region Generated records

        [Fact]
        public void GeneratedInserts_AreReadInOnePass_AsTheirPropertiesReadThem()
        {
            int total = Count();
            var wrong = new List<string>();
            int taken = 0;
            int seed = 7301 + Seed();

            for (int batch = 0; batch * 250 < total; batch++)
            {
                var rnd = new Random(seed + batch);
                var entities = new StringBuilder();
                var dictionary = new List<bool>();
                int made = Math.Min(250, total - batch * 250);

                for (int i = 0; i < made; i++)
                {
                    entities.Append(InsertRecord(rnd, i + 1, out bool braced));
                    dictionary.Add(braced);
                }

                DxfDatabase db = Drawing(entities.ToString());
                DxfBlockReference[] inserts = db.ModelSpace.OfType<DxfBlockReference>().ToArray();
                Assert.Equal(made, inserts.Length);

                for (int i = 0; i < inserts.Length; i++)
                {
                    string difference;

                    if (dictionary[i])
                    {
                        difference = inserts[i].TryReadForDisplay(out _) ? "read in one pass with a dictionary" : null;
                    }
                    else
                    {
                        difference = Difference(inserts[i]);
                        taken++;
                    }

                    if (difference != null)
                    {
                        wrong.Add("batch " + batch + " insert " + (i + 1) + ": " + difference);
                    }
                }
            }

            Assert.Empty(wrong);
            Assert.True(taken > total / 2, taken + " of " + total + " read in one pass");
        }

        [Fact]
        public void GeneratedInserts_AreArrays_AsTheirGroupsOneByOneSayThey_Are()
        {
            int total = Count();
            var wrong = new List<string>();
            int arrays = 0;
            int seed = 7401 + Seed();

            for (int batch = 0; batch * 250 < total; batch++)
            {
                var rnd = new Random(seed + batch);
                var entities = new StringBuilder();
                int made = Math.Min(250, total - batch * 250);

                for (int i = 0; i < made; i++)
                {
                    entities.Append(InsertRecord(rnd, i + 1, out _));
                }

                DxfDatabase db = Drawing(entities.ToString());

                foreach (DxfEntity entity in db.ModelSpace)
                {
                    DxfItem item = entity.Item;
                    bool wanted = ArrayByItsGroups(item);
                    bool got = entity is DxfMInsertBlock;
                    arrays += got ? 1 : 0;

                    if (got != wanted)
                    {
                        wrong.Add("batch " + batch + " " + entity.Handle + ": " + got + " against " + wanted);
                    }
                }
            }

            Assert.Empty(wrong);
            Assert.InRange(arrays, total / 20, total);
        }

        /// <summary>An INSERT is an array where its own part opens with the marker of one, or its first 70 or 71 is above 1.</summary>
        private static bool ArrayByItsGroups(DxfItem item)
        {
            IReadOnlyList<DxfTypedValue> own = item.File.OwnGroupsOf(item, 0);

            if (own.Count > 0 && own[0].TypeCode == 100 && own[0].Value.Trim() == "AcDbMInsertBlock")
            {
                return true;
            }

            foreach (int code in new[] { 70, 71 })
            {
                string value = item.File.ValueOf(item, code);

                if (value != null && int.TryParse(value.Trim(), NumberStyles.Integer, Inv, out int count) && count > 1)
                {
                    return true;
                }
            }

            return false;
        }

        private static string Number(Random rnd, double v)
        {
            if (rnd.Next(50) == 0)
            {
                string[] bad = { "abc", "1e999", "NaN", "", " ", "1.2.3", "1,5", "--1", "Infinity", "." };
                return bad[rnd.Next(bad.Length)];
            }

            string s;

            switch (rnd.Next(8))
            {
                case 0:
                    s = v.ToString("F2", Inv);
                    break;
                case 1:
                    s = v.ToString("E5", Inv);
                    break;
                case 2:
                    s = v.ToString("G17", Inv);
                    break;
                case 3:
                    s = (v >= 0.0 ? "+" : string.Empty) + v.ToString("R", Inv);
                    break;
                default:
                    s = v.ToString("R", Inv);
                    break;
            }

            switch (rnd.Next(10))
            {
                case 0:
                    return " " + s;
                case 1:
                    return s + " ";
                default:
                    return s;
            }
        }

        private static double Coordinate(Random rnd)
        {
            switch (rnd.Next(6))
            {
                case 0:
                    return 0.0;
                case 1:
                    return Math.Round(rnd.NextDouble() * 200.0 - 100.0, 3);
                case 2:
                    return rnd.NextDouble() * 1e4 - 5e3;
                case 3:
                    return rnd.Next(-50, 50);
                case 4:
                    return -0.0;
                default:
                    return (rnd.NextDouble() - 0.5) * Math.Pow(10, rnd.Next(-6, 8));
            }
        }

        private static double Factor(Random rnd)
        {
            switch (rnd.Next(6))
            {
                case 0:
                    return 1.0;
                case 1:
                    return -1.0;
                case 2:
                    return 0.0;
                case 3:
                    return Math.Round(rnd.NextDouble() * 4.0 - 2.0, 3);
                case 4:
                    return rnd.Next(1, 5);
                default:
                    return (rnd.NextDouble() - 0.5) * Math.Pow(10, rnd.Next(-3, 4));
            }
        }

        private static double Direction(Random rnd)
        {
            switch (rnd.Next(5))
            {
                case 0:
                    return 0.0;
                case 1:
                    return 1.0;
                case 2:
                    return -1.0;
                case 3:
                    return Math.Round(rnd.NextDouble() * 2.0 - 1.0, 4);
                default:
                    return (rnd.NextDouble() - 0.5) * Math.Pow(10, rnd.Next(-8, 3));
            }
        }

        private static string Group(Random rnd, int code, string value) =>
            (rnd.Next(10) == 0 ? code.ToString(Inv) : code.ToString(Inv).PadLeft(3)) + "\n" + value + "\n";

        /// <summary>One INSERT as a record, its groups of the own part in any order, and whether it holds an extension dictionary.</summary>
        private static string InsertRecord(Random rnd, int handle, out bool dictionary)
        {
            dictionary = false;
            var head = new StringBuilder();
            head.Append(Group(rnd, 0, "INSERT"))
                .Append(Group(rnd, 5, rnd.Next(12) == 0 ? "" : handle.ToString("X", Inv)));

            if (rnd.Next(5) == 0)
            {
                head.Append(Group(rnd, 102, "{ACAD_REACTORS"))
                    .Append(Group(rnd, 330, "1F"))
                    .Append(Group(rnd, 102, "}"));
            }

            if (rnd.Next(9) == 0)
            {
                dictionary = true;
                head.Append(Group(rnd, 102, "{ACAD_XDICTIONARY"))
                    .Append(Group(rnd, 360, "B1"))
                    .Append(Group(rnd, 102, "}"));
            }

            head.Append(Group(rnd, 8, "0"));

            // What comes before the subclass marker of the own part is not its own: a marker, or the groups of a R12 INSERT.
            bool r12 = rnd.Next(8) == 0;
            var own = new List<string>();

            if (!r12)
            {
                head.Append(Group(rnd, 100, rnd.Next(10) == 0 ? " AcDbEntity " : "AcDbEntity"));

                if (rnd.Next(8) == 0)
                {
                    head.Append(Group(rnd, 100, "AcDbEntity"));
                }

                // Groups that stand before the marker of the own part are forgotten where it comes.
                if (rnd.Next(10) == 0)
                {
                    head.Append(Group(rnd, 2, "b2")).Append(Group(rnd, 10, "999")).Append(Group(rnd, 50, "45"));
                    head.Append(Group(rnd, 70, "3")).Append(Group(rnd, 71, "4"));
                }

                head.Append(Group(rnd, 100, rnd.Next(12) == 0 ? "AcDbMInsertBlock" : "AcDbBlockReference"));
            }

            string[] names = { "B", "b", "B2", "b2", "C", "c", "NONE", " B ", "B\\U+0031", "", "caf\\U+00E9" };
            string name = names[rnd.Next(names.Length)];

            if (rnd.Next(14) != 0)
            {
                own.Add(Group(rnd, 2, name));
            }

            if (rnd.Next(4) != 0)
            {
                own.Add(Group(rnd, 10, Number(rnd, Coordinate(rnd))));
            }

            if (rnd.Next(4) != 0)
            {
                own.Add(Group(rnd, 20, Number(rnd, Coordinate(rnd))));
            }

            if (rnd.Next(3) != 0)
            {
                own.Add(Group(rnd, 30, Number(rnd, Coordinate(rnd))));
            }

            if (rnd.Next(3) == 0)
            {
                own.Add(Group(rnd, 41, Number(rnd, Factor(rnd))));
            }

            if (rnd.Next(3) == 0)
            {
                own.Add(Group(rnd, 42, Number(rnd, Factor(rnd))));
            }

            if (rnd.Next(4) == 0)
            {
                own.Add(Group(rnd, 43, Number(rnd, Factor(rnd))));
            }

            if (rnd.Next(2) == 0)
            {
                own.Add(Group(rnd, 50, Number(rnd, rnd.Next(3) == 0 ? 0.0 : (rnd.NextDouble() - 0.2) * 720.0)));
            }

            if (rnd.Next(4) == 0)
            {
                // An extrusion: a whole one, one of a part, one of no length, one not of unit length.
                int parts = rnd.Next(4);

                if (parts != 1)
                {
                    own.Add(Group(rnd, 210, Number(rnd, Direction(rnd))));
                }

                if (parts != 2)
                {
                    own.Add(Group(rnd, 220, Number(rnd, Direction(rnd))));
                }

                if (parts != 3)
                {
                    own.Add(Group(rnd, 230, Number(rnd, Direction(rnd))));
                }
            }

            if (rnd.Next(6) == 0)
            {
                own.Add(Group(rnd, 66, "1"));
            }

            if (rnd.Next(8) == 0)
            {
                own.Add(Group(rnd, 70, Whole(rnd)));
            }

            if (rnd.Next(8) == 0)
            {
                own.Add(Group(rnd, 71, Whole(rnd)));
            }

            if (rnd.Next(10) == 0)
            {
                own.Add(Group(rnd, 44, "5"));
                own.Add(Group(rnd, 45, "6"));
            }

            // A group doubled: the first of its code is read.
            if (own.Count > 0 && rnd.Next(6) == 0)
            {
                string[] doubled =
                {
                    Group(rnd, 10, "77"),
                    Group(rnd, 41, "9"),
                    Group(rnd, 50, "10"),
                    Group(rnd, 2, "C"),
                };
                own.Add(doubled[rnd.Next(doubled.Length)]);
            }

            if (rnd.Next(10) == 0)
            {
                own.Add(Group(rnd, 999, "a comment"));
            }

            // Any order, but the groups of braces and data, which have their places.
            for (int i = own.Count - 1; i > 0; i--)
            {
                int j = rnd.Next(i + 1);
                string swap = own[i];
                own[i] = own[j];
                own[j] = swap;
            }

            var record = new StringBuilder(head.ToString());

            foreach (string group in own)
            {
                record.Append(group);
            }

            if (rnd.Next(9) == 0)
            {
                record
                    .Append(Group(rnd, 102, "{APP"))
                    .Append(Group(rnd, 10, "55"))
                    .Append(Group(rnd, 2, "C"))
                    .Append(Group(rnd, 102, "}"));
            }

            if (rnd.Next(8) == 0)
            {
                // After the extended data begins, nothing is the INSERT's own.
                record
                    .Append(Group(rnd, 1001, "APP"))
                    .Append(Group(rnd, 1010, "1"))
                    .Append(Group(rnd, 10, "88"))
                    .Append(Group(rnd, 41, "7"));
                record.Append(Group(rnd, 100, "AcDbMInsertBlock")).Append(Group(rnd, 70, "5"));
            }

            return record.ToString();
        }

        private static string Whole(Random rnd)
        {
            string[] values = { "1", "2", "3", "0", "-2", "x", "99999999999", " 4 ", "1.5", "+5", "" };
            return values[rnd.Next(values.Length)];
        }

        #endregion
    }
}
