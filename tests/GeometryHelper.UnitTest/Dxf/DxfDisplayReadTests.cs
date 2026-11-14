using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Export;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfLines;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// What the display reads of a TEXT in one pass of its groups (<c>DxfDBText.ReadForDisplay</c>) against the properties
    /// read one by one, and a LWPOLYLINE's <c>HasWidth</c> read where its groups stand against the vertices read from them:
    /// the same to the bit over generated records, in whichever order the groups come, written whichever way, braced,
    /// doubled, misread or left out; and what each gives after the record is edited.
    /// </summary>
    /// <remarks>
    /// The generated records run to <c>GH_FUZZ_COUNT</c> a type (1 500 where it is not set), with <c>GH_FUZZ_SEED</c> moving
    /// the seed.
    /// </remarks>
    public class DxfDisplayReadTests
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

        #region A text, read in one pass

        [Fact]
        public void APlainText_IsReadInOnePass_AsEachPropertyReadsIt()
        {
            DxfDatabase db = DxfDatabase.Parse(
                R2000(
                    G(0, "TEXT", 5, "A1", 8, "0", 100, "AcDbEntity", 100, "AcDbText")
                        + G(
                            10,
                            12.5,
                            20,
                            -3.0,
                            30,
                            0.0,
                            40,
                            5.0,
                            1,
                            "PLATE",
                            50,
                            30.0,
                            41,
                            0.8,
                            51,
                            15.0,
                            7,
                            "Standard"
                        )
                        + G(71, 2, 72, 1, 11, 20.0, 21, 4.0, 31, 0.0, 100, "AcDbText", 73, 2)
                )
            );
            DxfDBText text = db.ModelSpace.OfType<DxfDBText>().Single();

            Assert.True(Taken(text));
            DxfDBText.DisplayRead read = text.ReadForDisplay();
            Assert.Equal("PLATE", read.TextString);
            Assert.Equal(5.0, read.Height);
            Assert.Equal(new GeoPoint3(20.0, 4.0, 0.0), read.At);
            Assert.False(read.IsDefaultAlignment);
            Assert.Equal(DxfTextHorizontalMode.TextCenter, read.HorizontalMode);
            Assert.Equal(DxfTextVerticalMode.TextVerticalMid, read.VerticalMode);
            Assert.Equal(30.0 * (Math.PI / 180.0), read.Rotation);
            Assert.True(read.IsMirroredInX);
            Assert.False(read.IsMirroredInY);
            Assert.Equal(0.8, read.WidthFactor);
            Assert.Equal(15.0 * (Math.PI / 180.0), read.Oblique);
            Assert.Equal("Standard", read.TextStyleName);
        }

        [Fact]
        public void ATextWithAnExtensionDictionary_IsReadByItsProperties_ForItsContextsMayMoveIt()
        {
            DxfDatabase db = DxfDatabase.Parse(
                R2000(
                    G(0, "TEXT", 5, "A1", 102, "{ACAD_XDICTIONARY", 360, "B1", 102, "}", 8, "0")
                        + G(100, "AcDbEntity", 100, "AcDbText", 10, 1.0, 20, 2.0, 30, 0.0, 40, 3.0, 1, "NOTE")
                        + G(100, "AcDbText")
                )
            );
            DxfDBText text = db.ModelSpace.OfType<DxfDBText>().Single();

            Assert.False(Taken(text));
            AssertSame(text);
        }

        [Fact]
        public void ATextEditedAfterItWasRead_IsReadWithItsNewValues()
        {
            DxfDatabase db = DxfDatabase.Parse(
                R2000(
                    G(0, "TEXT", 5, "A1", 8, "0", 100, "AcDbEntity", 100, "AcDbText")
                        + G(10, 12.5, 20, -3.0, 30, 0.0, 40, 5.0, 1, "PLATE", 50, 30.0, 100, "AcDbText")
                )
            );
            DxfDBText text = db.ModelSpace.OfType<DxfDBText>().Single();
            DxfDBText.DisplayRead before = text.ReadForDisplay();

            Assert.Equal("PLATE", before.TextString);
            Assert.Equal(new GeoPoint3(12.5, -3.0, 0.0), before.At);

            text.TextString = "BEAM";
            text.Height = 7.5;
            text.Position = new GeoPoint3(1.0, 2.0, 0.0);
            text.Rotation = 0.5;
            text.Oblique = 0.25;
            text.WidthFactor = 0.6;
            text.IsMirroredInY = true;
            DxfDBText.DisplayRead after = text.ReadForDisplay();

            Assert.Equal("BEAM", after.TextString);
            Assert.Equal(7.5, after.Height);
            Assert.Equal(new GeoPoint3(1.0, 2.0, 0.0), after.At);
            Assert.Equal(0.5, after.Rotation, 12);
            Assert.Equal(0.25, after.Oblique, 12);
            Assert.Equal(0.6, after.WidthFactor);
            Assert.True(after.IsMirroredInY);
            AssertSame(text);

            // Justified, then given a normal: the point it is placed by is the second one, in the world.
            text.Justify = DxfAttachmentPoint.MiddleCenter;
            text.AlignmentPoint = new GeoPoint3(4.0, 5.0, 0.0);
            text.Normal = new GeoVector3(0.0, 0.6, 0.8);
            AssertSame(text);
            Assert.False(text.ReadForDisplay().IsDefaultAlignment);
        }

        [Fact]
        public void GeneratedTexts_AreReadInOnePass_AsEachPropertyReadsThem()
        {
            int total = Count();
            var wrong = new List<string>();
            int taken = 0;
            int dictionaries = 0;
            int seed = 4101 + Seed();

            for (int batch = 0; batch * 250 < total; batch++)
            {
                var rnd = new Random(seed + batch);
                var entities = new StringBuilder();
                bool r12 = batch % 5 == 4;
                int made = Math.Min(250, total - batch * 250);

                for (int i = 0; i < made; i++)
                {
                    entities.Append(TextRecord(rnd, r12, i + 1, out bool dictionary));
                    dictionaries += dictionary ? 1 : 0;
                }

                DxfDatabase db = DxfDatabase.Parse(r12 ? R12(entities.ToString()) : R2000(entities.ToString()));

                foreach (DxfDBText text in db.ModelSpace.OfType<DxfDBText>())
                {
                    taken += Taken(text) ? 1 : 0;
                    string difference = Difference(text);

                    if (difference != null)
                    {
                        wrong.Add("batch " + batch + " handle " + text.Handle + ": " + difference);
                    }
                }
            }

            Assert.Empty(wrong);

            // Where no extension dictionary stands in the record the one pass takes it.
            Assert.Equal(total - dictionaries, taken);
        }

        private static bool Taken(DxfDBText text) => text.Item.File.TryTextGroups(text.Item, out _);

        private static void AssertSame(DxfDBText text) => Assert.Null(Difference(text));

        /// <summary>Where the one pass and the properties part, null where they do not.</summary>
        private static string Difference(DxfDBText text)
        {
            DxfDBText.DisplayRead read = text.ReadForDisplay();
            bool simple = text.IsDefaultAlignment;
            GeoPoint3 at = simple ? text.Position : text.AlignmentPoint;
            var wrong = new List<string>();

            void Check(string name, object got, object wanted)
            {
                if (!Equals(got, wanted))
                {
                    wrong.Add(name + " " + got + " against " + wanted);
                }
            }

            string Bits(double d) => BitConverter.DoubleToInt64Bits(d).ToString("x", Inv);

            Check("TextString", read.TextString, text.TextString);
            Check("Height", Bits(read.Height), Bits(text.Height));
            Check("IsDefaultAlignment", read.IsDefaultAlignment, simple);
            Check("At.X", Bits(read.At.X), Bits(at.X));
            Check("At.Y", Bits(read.At.Y), Bits(at.Y));
            Check("At.Z", Bits(read.At.Z), Bits(at.Z));
            Check("HorizontalMode", read.HorizontalMode, text.HorizontalMode);
            Check("VerticalMode", read.VerticalMode, text.VerticalMode);
            Check("Rotation", Bits(read.Rotation), Bits(text.Rotation));
            Check("IsMirroredInX", read.IsMirroredInX, text.IsMirroredInX);
            Check("IsMirroredInY", read.IsMirroredInY, text.IsMirroredInY);
            Check("TextStyleName", read.TextStyleName, text.TextStyleName);
            Check("WidthFactor", Bits(read.WidthFactor), Bits(text.WidthFactor));
            Check("Oblique", Bits(read.Oblique), Bits(text.Oblique));
            return wrong.Count == 0 ? null : string.Join("; ", wrong);
        }

        #endregion

        #region Hidden

        [Fact]
        public void AnEntityWhoseGroup60IsOne_IsHidden_AndOneEditedAfterIsAskedAnew()
        {
            DxfDatabase db = DxfDatabase.Parse(
                R2000(
                    G(0, "LINE", 5, "A1", 8, "0", 60, 1, 100, "AcDbEntity", 100, "AcDbLine", 10, 0.0, 20, 0.0, 30, 0.0)
                        + G(11, 5.0, 21, 0.0, 31, 0.0)
                        + G(0, "LINE", 5, "A2", 8, "0", 100, "AcDbEntity", 100, "AcDbLine", 10, 0.0, 20, 0.0, 30, 0.0)
                        + G(11, 5.0, 21, 0.0, 31, 0.0)
                )
            );
            DxfLine hidden = ByHandle<DxfLine>(db, "A1");
            DxfLine shown = ByHandle<DxfLine>(db, "A2");

            Assert.False(hidden.Visible);
            Assert.True(shown.Visible);

            hidden.Visible = true;
            shown.Visible = false;
            Assert.True(hidden.Visible);
            Assert.False(shown.Visible);
        }

        [Fact]
        public void GeneratedEntities_AskedIfTheyAreHidden_AnswerAsTheirGroupsSay()
        {
            int total = Count();
            var wrong = new List<string>();
            int seed = 9101 + Seed();

            for (int batch = 0; batch * 250 < total; batch++)
            {
                var rnd = new Random(seed + batch);
                var entities = new StringBuilder();
                int made = Math.Min(250, total - batch * 250);

                for (int i = 0; i < made; i++)
                {
                    entities.Append(HiddenRecord(rnd, i + 1));
                }

                DxfDatabase db = DxfDatabase.Parse(R2000(entities.ToString()));

                foreach (DxfEntity entity in db.ModelSpace)
                {
                    // As the property read it, from the first 60 of the part all entities have.
                    bool wanted = !(
                        int.TryParse(
                            entity.Item.File.CommonValueOf(entity.Item, 60)?.Trim(),
                            NumberStyles.Integer,
                            Inv,
                            out int hidden
                        )
                        && hidden == 1
                    );

                    if (entity.Visible != wanted)
                    {
                        wrong.Add(
                            "batch " + batch + " handle " + entity.Handle + ": " + entity.Visible + " against " + wanted
                        );
                    }
                }
            }

            Assert.Empty(wrong);
        }

        private static string HiddenRecord(Random rnd, int handle)
        {
            string[] values = { "1", "0", "1.0", " 1 ", "x", "", "99999999999", "+1", "01", "2", "-1", "\t1" };
            string Value() => values[rnd.Next(values.Length)];
            var text = new StringBuilder();
            text.Append(Group(rnd, 0, rnd.Next(2) == 0 ? "LINE" : "CIRCLE"))
                .Append(Group(rnd, 5, handle.ToString("X", Inv)));

            if (rnd.Next(5) == 0)
            {
                // A 60 among the groups of an application is the application's.
                text.Append(Group(rnd, 102, "{ACAD_REACTORS"))
                    .Append(Group(rnd, 60, "1"))
                    .Append(Group(rnd, 102, "}"));
            }

            if (rnd.Next(4) == 0)
            {
                text.Append(Group(rnd, 60, Value()));
            }

            text.Append(Group(rnd, 8, "0"));

            if (rnd.Next(3) == 0)
            {
                text.Append(Group(rnd, 60, Value()));
            }

            text.Append(Group(rnd, 100, "AcDbEntity"));

            if (rnd.Next(3) == 0)
            {
                text.Append(Group(rnd, 60, Value()));
            }

            if (rnd.Next(10) == 0)
            {
                text.Append(Group(rnd, 100, " AcDbEntity "));
            }

            if (rnd.Next(10) == 0)
            {
                text.Append(Group(rnd, 999, "a comment")).Append(Group(rnd, 60, Value()));
            }

            text.Append(Group(rnd, 100, "AcDbCircle"));
            text.Append(Group(rnd, 10, "1"))
                .Append(Group(rnd, 20, "2"))
                .Append(Group(rnd, 30, "0"))
                .Append(Group(rnd, 40, "3"));

            // After the own subclass, a 60 is its own; so it is in the extended data.
            if (rnd.Next(3) == 0)
            {
                text.Append(Group(rnd, 60, Value()));
            }

            if (rnd.Next(5) == 0)
            {
                text.Append(Group(rnd, 1001, "APP")).Append(Group(rnd, 60, Value()));
            }

            return text.ToString();
        }

        #endregion

        #region The look

        [Fact]
        public void GeneratedRecords_ReadTheirLook_AsTheFirstGroupOfEachCodeSays()
        {
            int total = Count();
            var wrong = new List<string>();
            int seed = 5501 + Seed();

            for (int batch = 0; batch * 250 < total; batch++)
            {
                var rnd = new Random(seed + batch);
                var entities = new StringBuilder();
                int made = Math.Min(250, total - batch * 250);

                for (int i = 0; i < made; i++)
                {
                    entities.Append(LookRecord(rnd, i + 1));
                }

                DxfDatabase db = DxfDatabase.Parse(R2000(entities.ToString()));

                foreach (DxfEntity entity in db.ModelSpace)
                {
                    DxfItem item = entity.Item;
                    var first = new Dictionary<int, string>();

                    foreach (DxfTypedValue group in item.Groups)
                    {
                        if (!first.ContainsKey(group.TypeCode))
                        {
                            first.Add(group.TypeCode, group.Value);
                        }
                    }

                    string Value(int code) => first.TryGetValue(code, out string v) ? v : null;
                    int? Whole(int code) =>
                        first.TryGetValue(code, out string v)
                        && int.TryParse(v.Trim(), NumberStyles.Integer, Inv, out int n)
                            ? n
                            : (int?)null;
                    double raised =
                        first.TryGetValue(39, out string text)
                        && Numbers.TryRead(text, out double read)
                        && Guard.IsFinite(read)
                            ? read
                            : 0.0;
                    string wanted = string.Join(
                        "|",
                        Value(8),
                        Whole(62),
                        Whole(420),
                        Value(6),
                        Whole(370),
                        BitConverter.DoubleToInt64Bits(raised),
                        string.IsNullOrEmpty(Value(5)?.Trim()) ? null : Value(5).Trim()
                    );
                    DxfFile.Look look = item.File.LookOf(item, out string handle);
                    string got = string.Join(
                        "|",
                        look.Layer,
                        look.Colour,
                        look.TrueColour,
                        look.Linetype,
                        look.Lineweight,
                        BitConverter.DoubleToInt64Bits(look.Thickness),
                        handle
                    );

                    if (got != wanted)
                    {
                        wrong.Add("batch " + batch + " " + entity.Handle + ": " + got + " against " + wanted);
                    }
                }
            }

            Assert.Empty(wrong);
        }

        private static string LookRecord(Random rnd, int handle)
        {
            string[] values =
            {
                "1",
                "0",
                "7",
                " 3 ",
                "x",
                "",
                "99999999999",
                "-2",
                "Steel",
                "\\U+00E9t",
                "HIDDEN",
                "16711680",
                "2.5",
                "1e999",
            };
            string Value() => values[rnd.Next(values.Length)];
            int[] codes = { 5, 8, 6, 62, 370, 420, 39 };
            var text = new StringBuilder();
            text.Append(Group(rnd, 0, "CIRCLE"))
                .Append(Group(rnd, 5, rnd.Next(12) == 0 ? "" : handle.ToString("X", Inv)));

            void Some(int count)
            {
                for (int i = 0; i < count; i++)
                {
                    int code = codes[rnd.Next(codes.Length)];
                    text.Append(Group(rnd, code, code == 5 ? "9" + handle.ToString("X", Inv) : Value()));
                }
            }

            if (rnd.Next(4) == 0)
            {
                text.Append(Group(rnd, 102, "{ACAD_REACTORS"))
                    .Append(Group(rnd, 330, "1F"))
                    .Append(Group(rnd, 102, "}"));
            }

            Some(rnd.Next(5));
            text.Append(Group(rnd, 100, "AcDbEntity"));
            Some(rnd.Next(4));
            text.Append(Group(rnd, 100, "AcDbCircle"));
            text.Append(Group(rnd, 10, "1"))
                .Append(Group(rnd, 20, "2"))
                .Append(Group(rnd, 30, "0"))
                .Append(Group(rnd, 40, "3"));
            Some(rnd.Next(3));

            if (rnd.Next(5) == 0)
            {
                text.Append(Group(rnd, 999, "a comment"));
                Some(1);
            }

            if (rnd.Next(5) == 0)
            {
                text.Append(Group(rnd, 1001, "APP")).Append(Group(rnd, 1000, Value()));
            }

            return text.ToString();
        }

        #endregion

        #region A polyline's width

        [Fact]
        public void APolylineWithAVertexWidth_HasWidth_WhereOnesOfNoWidthHaveNone()
        {
            DxfDatabase plain = DxfDatabase.Parse(R2000(Polyline("P1", null, null, 0.0, 0.0)));
            DxfDatabase constant = DxfDatabase.Parse(R2000(Polyline("P1", 0.5, null, 0.0, 0.0)));
            DxfDatabase vertex = DxfDatabase.Parse(R2000(Polyline("P1", null, new[] { 2.0, 1.0 }, 0.0, 0.0)));

            Assert.False(plain.ModelSpace.OfType<DxfPolyline>().Single().HasWidth);
            Assert.True(constant.ModelSpace.OfType<DxfPolyline>().Single().HasWidth);
            Assert.True(vertex.ModelSpace.OfType<DxfPolyline>().Single().HasWidth);
        }

        [Fact]
        public void APolylineEditedAfterItsWidthWasAsked_IsAskedAnew()
        {
            DxfDatabase db = DxfDatabase.Parse(R2000(Polyline("P1", null, null, 0.0, 0.0)));
            DxfPolyline polyline = db.ModelSpace.OfType<DxfPolyline>().Single();

            Assert.False(polyline.HasWidth);
            polyline.ConstantWidth = 1.5;
            Assert.True(polyline.HasWidth);
            polyline.ConstantWidth = 0.0;
            Assert.False(polyline.HasWidth);
            polyline.SetStartWidthAt(1, 0.75);
            Assert.True(polyline.HasWidth);
        }

        [Fact]
        public void GeneratedPolylines_AskedTheirWidthWhereTheirGroupsStand_AnswerAsTheirVerticesDo()
        {
            int total = Count();
            var wrong = new List<string>();
            int seed = 7301 + Seed();

            for (int batch = 0; batch * 250 < total; batch++)
            {
                var rnd = new Random(seed + batch);
                var entities = new StringBuilder();
                int made = Math.Min(250, total - batch * 250);

                for (int i = 0; i < made; i++)
                {
                    entities.Append(PolylineRecord(rnd, i + 1));
                }

                string file = R2000(entities.ToString());
                DxfDatabase asked = DxfDatabase.Parse(file);
                DxfDatabase read = DxfDatabase.Parse(file);
                DxfPolyline[] first = asked.ModelSpace.OfType<DxfPolyline>().ToArray();
                DxfPolyline[] second = read.ModelSpace.OfType<DxfPolyline>().ToArray();

                Assert.Equal(first.Length, second.Length);

                for (int i = 0; i < first.Length; i++)
                {
                    // The first asks where the groups stand; the second has its vertices read, as an edit leaves them.
                    bool got = first[i].HasWidth;
                    int count = second[i].NumberOfVertices;
                    bool wanted = count == 0 ? second[i].ConstantWidth != 0.0 : false;

                    for (int v = 0; v < count && !wanted; v++)
                    {
                        wanted = second[i].GetStartWidthAt(v) != 0.0 || second[i].GetEndWidthAt(v) != 0.0;
                    }

                    if (got != wanted || second[i].HasWidth != wanted)
                    {
                        wrong.Add("batch " + batch + " handle " + first[i].Handle + ": " + got + " against " + wanted);
                    }
                }
            }

            Assert.Empty(wrong);
        }

        #endregion

        #region Generated records

        private static string Number(Random rnd, double v)
        {
            if (rnd.Next(60) == 0)
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
                case 2:
                    return "\t" + s + "  ";
                default:
                    return s;
            }
        }

        private static string Whole(Random rnd, int[] likely)
        {
            if (rnd.Next(30) == 0)
            {
                string[] odd = { "x", "99999999999", "-1", "1.5", "", " 2 ", "+4" };
                return odd[rnd.Next(odd.Length)];
            }

            return likely[rnd.Next(likely.Length)].ToString(Inv);
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

        private static string Group(Random rnd, int code, string value) =>
            (rnd.Next(10) == 0 ? code.ToString(Inv) : code.ToString(Inv).PadLeft(3)) + "\n" + value + "\n";

        private static string TextRecord(Random rnd, bool r12, int handle, out bool dictionary)
        {
            dictionary = false;
            var groups = new List<string>();
            string[] words =
            {
                "PLATE",
                "A^Ib",
                "%%c20",
                "caf\\U+00E9",
                "",
                " lead ",
                "x^ y",
                "T\\U+0041",
                "%%uAB%%u",
                "100 x 6 PL",
            };
            string[] styles = { "Standard", "ARIAL", "My\\U+0020Style", "", "  Spaced  ", "STANDARD" };
            int[] horizontals = { 0, 0, 0, 1, 2, 3, 4, 5 };
            int[] verticals = { 0, 0, 0, 1, 2, 3 };
            var wanted = new List<string>();

            void Add(int code, string value)
            {
                wanted.Add(Group(rnd, code, value));

                // The first of a code is the one read: a later one of another value is no matter.
                if (rnd.Next(25) == 0)
                {
                    wanted.Add(Group(rnd, code, "77"));
                }
            }

            if (rnd.Next(10) != 0)
            {
                Add(10, Number(rnd, Coordinate(rnd)));
            }

            if (rnd.Next(10) != 0)
            {
                Add(20, Number(rnd, Coordinate(rnd)));
            }

            if (rnd.Next(3) == 0)
            {
                Add(30, Number(rnd, Coordinate(rnd)));
            }

            if (rnd.Next(8) != 0)
            {
                Add(40, Number(rnd, rnd.Next(5) == 0 ? -1.0 : Math.Round(rnd.NextDouble() * 20.0, 2)));
            }

            if (rnd.Next(8) != 0)
            {
                Add(1, words[rnd.Next(words.Length)]);
            }

            if (rnd.Next(3) == 0)
            {
                Add(50, Number(rnd, Math.Round(rnd.NextDouble() * 360.0, 3)));
            }

            if (rnd.Next(5) == 0)
            {
                Add(41, Number(rnd, rnd.Next(4) == 0 ? 0.0 : Math.Round(rnd.NextDouble() * 2.0, 3)));
            }

            if (rnd.Next(5) == 0)
            {
                Add(51, Number(rnd, Math.Round(rnd.NextDouble() * 30.0, 2)));
            }

            if (rnd.Next(3) == 0)
            {
                Add(7, styles[rnd.Next(styles.Length)]);
            }

            if (rnd.Next(6) == 0)
            {
                Add(71, Whole(rnd, new[] { 0, 2, 4, 6 }));
            }

            if (rnd.Next(3) == 0)
            {
                Add(72, Whole(rnd, horizontals));
                Add(11, Number(rnd, Coordinate(rnd)));
                Add(21, Number(rnd, Coordinate(rnd)));

                if (rnd.Next(2) == 0)
                {
                    Add(31, Number(rnd, Coordinate(rnd)));
                }
            }
            else if (rnd.Next(12) == 0)
            {
                Add(11, Number(rnd, Coordinate(rnd)));
            }

            if (rnd.Next(7) == 0)
            {
                string[] normals =
                {
                    "0",
                    "0",
                    "1",
                    "-1",
                    "0.6",
                    "0.8",
                    "3",
                    "0.5",
                    "0.5",
                    "0.70710678118654757",
                    "0.70710678118654757",
                };
                Add(210, Number(rnd, double.Parse(normals[rnd.Next(normals.Length)], Inv)));
                Add(220, Number(rnd, double.Parse(normals[rnd.Next(normals.Length)], Inv)));
                Add(230, Number(rnd, double.Parse(normals[rnd.Next(normals.Length)], Inv)));
            }

            for (int i = wanted.Count - 1; i > 0; i--)
            {
                // The order the groups come in is shuffled in a few, kept as AutoCAD writes it in most.
                if (rnd.Next(8) == 0)
                {
                    int j = rnd.Next(i + 1);
                    string t = wanted[i];
                    wanted[i] = wanted[j];
                    wanted[j] = t;
                }
            }

            var text = new StringBuilder();
            text.Append(Group(rnd, 0, "TEXT"));
            text.Append(Group(rnd, 5, handle.ToString("X", Inv)));

            if (!r12 && rnd.Next(5) == 0)
            {
                text.Append(Group(rnd, 102, "{ACAD_REACTORS"))
                    .Append(Group(rnd, 330, "1F"))
                    .Append(Group(rnd, 102, "}"));
            }

            if (!r12 && rnd.Next(10) == 0)
            {
                dictionary = true;
                text.Append(Group(rnd, 102, rnd.Next(2) == 0 ? "{ACAD_XDICTIONARY" : " {ACAD_XDICTIONARY "))
                    .Append(Group(rnd, 360, "AB" + handle.ToString("X", Inv)))
                    .Append(Group(rnd, 102, "}"));
            }

            text.Append(Group(rnd, 8, "0"));

            if (!r12)
            {
                text.Append(Group(rnd, 100, "AcDbEntity"));
            }

            if (!r12 && rnd.Next(4) == 0)
            {
                text.Append(Group(rnd, 100, "AcDbEntity"));
            }

            if (!r12)
            {
                text.Append(Group(rnd, 100, "AcDbText"));
            }

            // A group of an application among the own ones, braced: what it holds is not the text's.
            if (!r12 && rnd.Next(8) == 0)
            {
                text.Append(Group(rnd, 102, "{APP_DECOY"))
                    .Append(Group(rnd, 10, "9999"))
                    .Append(Group(rnd, 40, "9999"))
                    .Append(Group(rnd, 72, "5"))
                    .Append(Group(rnd, 102, "}"));
            }

            int split = rnd.Next(wanted.Count + 1);

            for (int i = 0; i < wanted.Count; i++)
            {
                text.Append(wanted[i]);

                if (rnd.Next(30) == 0)
                {
                    text.Append(Group(rnd, 999, "a comment"));
                }

                if (!r12 && i + 1 == split && rnd.Next(2) == 0)
                {
                    text.Append(Group(rnd, 100, "AcDbText"));
                }
            }

            if (!r12 && rnd.Next(2) == 0)
            {
                text.Append(Group(rnd, 100, "AcDbText"));
                text.Append(Group(rnd, 73, Whole(rnd, verticals)));
            }
            else if (rnd.Next(2) == 0)
            {
                text.Append(Group(rnd, 73, Whole(rnd, verticals)));
            }

            if (rnd.Next(10) == 0)
            {
                text.Append(Group(rnd, 1001, "APP")).Append(Group(rnd, 1000, "x")).Append(Group(rnd, 1040, "3.5"));
            }

            return text.ToString();
        }

        private static string Polyline(string handle, double? constant, double[] widths, double x, double y)
        {
            // A square of four vertices, the second given widths where they are.
            var text = new StringBuilder(
                G(0, "LWPOLYLINE", 5, handle, 8, "0", 100, "AcDbEntity", 100, "AcDbPolyline", 90, 4, 70, 1)
            );

            if (constant.HasValue)
            {
                text.Append(G(43, constant.Value));
            }

            for (int v = 0; v < 4; v++)
            {
                text.Append(G(10, x + (v == 1 || v == 2 ? 10.0 : 0.0), 20, y + (v >= 2 ? 10.0 : 0.0)));

                if (v == 1 && widths != null)
                {
                    text.Append(G(40, widths[0], 41, widths[1]));
                }
            }

            return text.ToString();
        }

        private static string PolylineRecord(Random rnd, int handle)
        {
            var groups = new StringBuilder();
            groups.Append(Group(rnd, 0, "LWPOLYLINE")).Append(Group(rnd, 5, handle.ToString("X", Inv)));

            if (rnd.Next(8) == 0)
            {
                groups
                    .Append(Group(rnd, 102, "{ACAD_REACTORS"))
                    .Append(Group(rnd, 330, "1F"))
                    .Append(Group(rnd, 102, "}"));
            }

            groups
                .Append(Group(rnd, 8, "0"))
                .Append(Group(rnd, 100, "AcDbEntity"))
                .Append(Group(rnd, 100, "AcDbPolyline"));
            int vertices = rnd.Next(7) == 0 ? 0 : rnd.Next(1, 6);
            groups.Append(Group(rnd, 90, vertices.ToString(Inv))).Append(Group(rnd, 70, rnd.Next(2).ToString(Inv)));

            // The constant width, where the file puts it, or after the vertices, or twice, or a word.
            string constant =
                rnd.Next(3) == 0 ? Number(rnd, rnd.Next(3) == 0 ? 0.0 : Math.Round(rnd.NextDouble() * 3.0, 2)) : null;
            bool constantLast = constant != null && rnd.Next(5) == 0;

            if (constant != null && !constantLast)
            {
                groups.Append(Group(rnd, 43, constant));

                if (rnd.Next(20) == 0)
                {
                    groups.Append(Group(rnd, 43, "9"));
                }
            }

            if (rnd.Next(15) == 0)
            {
                // A width before the first vertex belongs to none.
                groups.Append(Group(rnd, 40, "4")).Append(Group(rnd, 41, "4"));
            }

            for (int v = 0; v < vertices; v++)
            {
                groups
                    .Append(Group(rnd, 10, Number(rnd, Coordinate(rnd))))
                    .Append(Group(rnd, 20, Number(rnd, Coordinate(rnd))));

                if (rnd.Next(4) == 0)
                {
                    groups.Append(Group(rnd, 91, "0"));
                }

                if (rnd.Next(5) == 0)
                {
                    groups.Append(
                        Group(rnd, 40, Number(rnd, rnd.Next(3) == 0 ? 0.0 : Math.Round(rnd.NextDouble() * 2.0, 2)))
                    );
                }

                if (rnd.Next(5) == 0)
                {
                    groups.Append(
                        Group(rnd, 41, Number(rnd, rnd.Next(3) == 0 ? 0.0 : Math.Round(rnd.NextDouble() * 2.0, 2)))
                    );
                }

                if (rnd.Next(12) == 0)
                {
                    // The last of a code in a vertex is the one read.
                    groups.Append(Group(rnd, 40, Number(rnd, rnd.Next(2) == 0 ? 0.0 : 0.5)));
                }

                if (rnd.Next(4) == 0)
                {
                    groups.Append(Group(rnd, 42, Number(rnd, Math.Round(rnd.NextDouble() - 0.5, 3))));
                }

                if (rnd.Next(40) == 0)
                {
                    groups.Append(Group(rnd, 999, "a comment"));
                }
            }

            if (constantLast)
            {
                groups.Append(Group(rnd, 43, constant));
            }

            if (rnd.Next(10) == 0)
            {
                groups.Append(Group(rnd, 102, "{APP_DECOY")).Append(Group(rnd, 40, "8")).Append(Group(rnd, 102, "}"));
            }

            if (rnd.Next(10) == 0)
            {
                groups.Append(Group(rnd, 1001, "APP")).Append(Group(rnd, 1040, "3.5"));
            }

            return groups.ToString();
        }

        #endregion
    }
}
