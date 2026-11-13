using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The contexts of a dimension and of a multileader at a second scale, and the default context deleted, as AutoCAD 2021
    /// writes them (probes of the plugin GHAN4 in accoreconsole: a DIMLINEAR, DIMALIGNED, DIMRADIUS, DIMDIAMETER, DIMANGULAR
    /// and DIMORDINATE of the template's style Annotative at 1:50 given 1:100 by -OBJECTSCALE; a multileader of its style
    /// Annotative; a TEXT and an MTEXT whose default scale was deleted). The drawing is ezdxf's of R2018 with the scales 1:1
    /// (F0), 1:50 (F1) and 1:100 (F2).
    /// </summary>
    public class DxfAnnotativeSecondScaleTests
    {
        private const string ScaleList = "  0\nDICTIONARY\n  5\n14\n330\nA\n100\nAcDbDictionary\n281\n1\n";

        private static string Scale(string handle, string name, string paper, string drawing, string unit) =>
            "  0\nSCALE\n  5\n"
            + handle
            + "\n102\n{ACAD_REACTORS\n330\n14\n102\n}\n330\n14\n100\nAcDbScale\n 70\n0\n300\n"
            + name
            + "\n140\n"
            + paper
            + "\n141\n"
            + drawing
            + "\n290\n"
            + unit
            + "\n";

        private static DxfDatabase Scaled()
        {
            string text = DxfFileEzdxfDrawings.R2018;
            Assert.Contains(ScaleList, text, StringComparison.Ordinal);
            string scales =
                Scale("F0", "1:1", "1", "1", "1")
                + Scale("F1", "1:50", "1", "50", "0")
                + Scale("F2", "1:100", "1", "100", "0");
            int end = text.LastIndexOf("  0\nENDSEC\n", StringComparison.Ordinal);
            text = (text.Substring(0, end) + scales + text.Substring(end)).Replace(
                ScaleList,
                ScaleList + "  3\nA0\n350\nF0\n  3\nA1\n350\nF1\n  3\nA2\n350\nF2\n"
            );
            return DxfDatabase.Parse(text);
        }

        private static DxfAnnotationScale Of(DxfDatabase db, string name) =>
            (DxfAnnotationScale)db.ObjectContextManager.GetContextCollection("ACDB_ANNOTATIONSCALES").GetContext(name);

        private static string Record(string text, string handle) => Records(text).Single(r => HandleOf(r) == handle);

        private static string[] Names(DxfDBObject o) => o.GetContexts().Select(c => c.Name).ToArray();

        private static string[] Keys(DxfDBObject o)
        {
            var manager = (DxfDBDictionary)o.ExtensionDictionary.GetAt("AcDbContextDataManager");
            var scales = (DxfDBDictionary)manager.GetAt("ACDB_ANNOTATIONSCALES");
            return scales.Select(e => e.Key).ToArray();
        }

        /// <summary>The groups of a context, code and value of each, as the drawing's text holds them.</summary>
        private static List<KeyValuePair<int, string>> Context(DxfDatabase db, DxfDBObject o, string key)
        {
            var manager = (DxfDBDictionary)o.ExtensionDictionary.GetAt("AcDbContextDataManager");
            var scales = (DxfDBDictionary)manager.GetAt("ACDB_ANNOTATIONSCALES");
            string handle = scales.GetAt(key).Handle;
            string record = Records(db.ToText()).Single(r => HandleOf(r) == handle);
            string[] lines = record.Split('\n');
            var groups = new List<KeyValuePair<int, string>>();

            for (int i = 2; i + 1 < lines.Length; i += 2)
            {
                groups.Add(
                    new KeyValuePair<int, string>(
                        int.Parse(lines[i].Trim(), CultureInfo.InvariantCulture),
                        lines[i + 1].Trim()
                    )
                );
            }

            return groups;
        }

        private static string Join(IEnumerable<KeyValuePair<int, string>> groups) =>
            string.Join(
                " ",
                groups.Select(g =>
                    g.Key
                    + "="
                    + (
                        g.Key != 340
                        && g.Key != 100
                        && g.Key != 2
                        && double.TryParse(g.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)
                            ? Math.Round(d, 4).ToString("0.####", CultureInfo.InvariantCulture)
                            : g.Value
                    )
                )
            );

        private static string After(List<KeyValuePair<int, string>> groups) =>
            Join(groups.SkipWhile(g => g.Key != 340));

        private static DxfBlockTableRecord Picture(DxfDatabase db)
        {
            var picture = new DxfBlockTableRecord { Name = "*D" };
            db.BlockTable.Add(picture);
            picture.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)));
            return picture;
        }

        private static DxfRotatedDimension Linear(DxfDatabase db)
        {
            var dimension = new DxfRotatedDimension(
                0.0,
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 0, 0),
                new GeoPoint3(50, 30, 0),
                string.Empty,
                null
            );
            dimension.DimBlock = Picture(db);
            db.ModelSpace.AppendEntity(dimension);
            dimension.TextPosition = new GeoPoint3(50, 30, 0);
            dimension.UsingDefaultTextPosition = true;
            return dimension;
        }

        [Fact]
        public void ALinearDimensionMadeAnnotativeAt1To50_HoldsOneDefaultContext_OfTheAlignedKind_AsAutoCADWritesIt()
        {
            DxfDatabase db = Scaled();
            DxfRotatedDimension dimension = Linear(db);
            string block = dimension.DimBlock.Name;

            dimension.MakeDimensionAnnotative(Of(db, "1:50"));

            Assert.Equal(DxfAnnotativeStates.True, dimension.Annotative);
            Assert.NotNull(dimension.GetXDataForApplication("AcadAnnotative"));
            Assert.Equal(new[] { "1:50" }, Names(dimension));
            Assert.Equal(new[] { "*A1" }, Keys(dimension));
            Assert.Contains("ACDB_ALDIMOBJECTCONTEXTDATA_CLASS", db.ToText(), StringComparison.Ordinal);

            // The groups of the context of a DIMLINEAR at 1:50: its block, the middle of its words, then the points of its kind
            // (the dimension line's point, group 10 of the record).
            Assert.Equal(
                "340=F1 100=AcDbDimensionObjectContextData 2="
                    + block
                    + " 293=0 10=50 20=30 294=1 140=0 298=0 291=0 70=0 292=0 71=0 280=0 295=0 296=0 297=0"
                    + " 100=AcDbAlignedDimensionObjectContextData 11=50 21=30 31=0",
                After(Context(db, dimension, "*A1"))
            );
        }

        [Fact]
        public void ADimensionGivenASecondScale_NamesAPictureBlockOfThatScale_AndKeepsItsRecord()
        {
            DxfDatabase db = Scaled();
            DxfRotatedDimension dimension = Linear(db);
            string first = dimension.DimBlock.Name;
            dimension.MakeDimensionAnnotative(Of(db, "1:50"));
            string record = Record(db.ToText(), dimension.Handle);

            DxfBlockTableRecord second = Picture(db);
            dimension.AddDimensionContext(Of(db, "1:100"), second, new GeoPoint3(177.4699863574352, 30, 0));

            Assert.Equal(new[] { "*A1", "*A2" }, Keys(dimension));
            Assert.Equal(new[] { "1:50", "1:100" }, Names(dimension));
            Assert.Equal(first, dimension.DimBlock.Name);
            Assert.StartsWith(
                record.Substring(0, record.IndexOf("1001", StringComparison.Ordinal)),
                Record(db.ToText(), dimension.Handle),
                StringComparison.Ordinal
            );
            List<KeyValuePair<int, string>> groups = Context(db, dimension, "*A2");
            Assert.Equal("0", groups.First(g => g.Key == 290).Value);
            Assert.Equal(
                "340=F2 100=AcDbDimensionObjectContextData 2="
                    + second.Name
                    + " 293=0 10=177.47 20=30 294=1 140=0 298=0 291=0 70=0 292=0 71=0 280=0 295=0 296=0 297=0"
                    + " 100=AcDbAlignedDimensionObjectContextData 11=50 21=30 31=0",
                After(groups).Replace(" 11=50 21=30 31=0", " 11=50 21=30 31=0")
            );
        }

        [Fact]
        public void TheDefaultContextOfADimensionTakenOut_MakesTheNextTheDefault_ItsBlockAndWordsTheRecords()
        {
            DxfDatabase db = Scaled();
            DxfRotatedDimension dimension = Linear(db);
            string first = dimension.DimBlock.Name;
            dimension.MakeDimensionAnnotative(Of(db, "1:50"));
            DxfBlockTableRecord second = Picture(db);
            dimension.AddDimensionContext(Of(db, "1:100"), second, new GeoPoint3(177.4699863574352, 30, 0));

            dimension.RemoveContext(Of(db, "1:50"));

            // As AutoCAD 2021 rewrote the record: the block of the next context and the middle of its words; the old picture goes.
            Assert.Equal(new[] { "*A2" }, Keys(dimension));
            Assert.Equal(new[] { "1:100" }, Names(dimension));
            Assert.Equal("1", Context(db, dimension, "*A2").First(g => g.Key == 290).Value);
            Assert.Equal(second.Name, dimension.DimBlock.Name);
            Assert.Equal(177.4699863574352, dimension.TextPosition.X, 9);
            Assert.False(db.BlockTable.Has(first));

            // The next entry is *A3, as AutoCAD 2021 counts them, though *A2 is the highest now.
            DxfBlockTableRecord third = Picture(db);
            dimension.AddDimensionContext(Of(db, "1:50"), third, new GeoPoint3(50, 30, 0));
            Assert.Equal(new[] { "*A2", "*A3" }, Keys(dimension));
        }

        [Fact]
        public void TheLastEntryTakenOut_IsNotGivenAgain_ASecondScaleAddedAfterIsTheNextNumber()
        {
            DxfDatabase db = Scaled();
            var text = new DxfDBText
            {
                Position = new GeoPoint3(10, 10, 0),
                TextString = "ABC",
                Height = 2.5,
            };
            db.ModelSpace.AppendEntity(text);
            text.Annotative = DxfAnnotativeStates.True;
            text.AddContext(Of(db, "1:50"));
            Assert.Equal(new[] { "*A1", "*A2" }, Keys(text));

            // AutoCAD 2021: after *A2 was deleted, the scale added is *A3.
            text.RemoveContext(Of(db, "1:50"));
            text.AddContext(Of(db, "1:100"));
            Assert.Equal(new[] { "*A1", "*A3" }, Keys(text));
        }

        [Fact]
        public void TheDefaultContextOfATextTakenOut_MakesTheNextTheDefault_AndTheRecordsHeightIsThatOfItsScale()
        {
            DxfDatabase db = Scaled();
            db.Cannoscale = "1:50";
            var text = new DxfDBText
            {
                Position = new GeoPoint3(10, 10, 0),
                TextString = "ABC",
                Height = 125.0,
            };
            db.ModelSpace.AppendEntity(text);
            text.Annotative = DxfAnnotativeStates.True;
            text.AddContext(Of(db, "1:100"));

            text.RemoveContext(Of(db, "1:50"));

            // AutoCAD 2021 (a TEXT of 125 at 1:50, its default 1:50 deleted beside 1:100): the record is 250 high.
            Assert.Equal(250.0, text.Height, 9);
            Assert.Equal(new[] { "*A2" }, Keys(text));
            Assert.Equal(new[] { "1:100" }, Names(text));
            Assert.Equal("1", Context(db, text, "*A2").First(g => g.Key == 290).Value);
        }

        [Fact]
        public void TheDefaultContextOfAnMTextTakenOut_GivesTheRecordTheHeightAndTheWidthOfTheNextContext()
        {
            DxfDatabase db = Scaled();
            db.Cannoscale = "1:50";
            var words = new DxfMText
            {
                Location = new GeoPoint3(0, 300, 0),
                TextHeight = 125.0,
                Contents = "Hello world",
                Width = 5000.0,
            };
            db.ModelSpace.AppendEntity(words);
            words.Annotative = DxfAnnotativeStates.True;
            words.AddContext(Of(db, "1:100"));

            words.RemoveContext(Of(db, "1:50"));

            // AutoCAD 2021: the record is 250 high and its width (41) 10000, the width of the context of 1:100.
            Assert.Equal(250.0, words.TextHeight, 9);
            Assert.Equal(10000.0, words.Width, 9);
        }

        [Fact]
        public void AMultileaderGivenASecondScale_IsLaidOutAgainAtIt_AsAutoCADLaidOutAnAnnotativeOne()
        {
            // The context data of AutoCAD 2021's multileader of the style Annotative at 1:50 (text "Note" 0.2 high, an arrowhead
            // of 9, a gap of 4.5, a dogleg of 0.36, the landing at (100, 100)), the groups the test follows by code.
            var at50 = new List<KeyValuePair<int, string>>();

            void G(int code, string value) => at50.Add(new KeyValuePair<int, string>(code, value));

            G(40, "50");
            G(10, "95.5");
            G(20, "95.42633015006821");
            G(30, "0");
            G(41, "0.2");
            G(140, "9");
            G(145, "4.5");
            G(290, "1");
            G(304, "Note");
            G(11, "0");
            G(21, "0");
            G(31, "1");
            G(340, "20");
            G(12, "100");
            G(22, "95.52796725784446");
            G(32, "0");
            G(13, "1");
            G(23, "0");
            G(33, "0");
            G(42, "0");
            G(43, "0");
            G(44, "0");
            G(45, "1");
            G(142, "0");
            G(143, "0");
            G(302, "LEADER{");
            G(290, "1");
            G(291, "1");
            G(10, "100");
            G(20, "100");
            G(30, "0");
            G(11, "1");
            G(21, "0");
            G(31, "0");
            G(90, "0");
            G(40, "0.36");
            G(304, "LEADER_LINE{");
            G(10, "40");
            G(20, "40");
            G(30, "0");
            G(91, "0");
            G(305, "}");
            G(271, "0");
            G(303, "}");
            G(272, "9");
            G(273, "9");
            DxfDatabase db = Scaled();

            List<KeyValuePair<int, string>> at100 = DxfDBObject.MLeaderLaidOut(at50, Of(db, "1:100"), 2.0);

            // AutoCAD 2021 at 1:100: the scale 100, sizes doubled, the landing and the line's end where they were, the base of the
            // content out at the landing by the new dogleg (0.72), the text 9.72 further right and 0.2033 above it.
            string text = Join(at100);
            Assert.StartsWith(
                "40=100 10=100.72 20=100 30=0 41=0.4 140=18 145=9 290=1 304=Note",
                text,
                StringComparison.Ordinal
            );
            Assert.Contains("12=109.72 22=100.2033 32=0", text, StringComparison.Ordinal);
            Assert.Contains(
                "302=LEADER{ 290=1 291=1 10=100 20=100 30=0 11=1 21=0 31=0 90=0 40=0.72",
                text,
                StringComparison.Ordinal
            );
            Assert.Contains("304=LEADER_LINE{ 10=40 20=40 30=0", text, StringComparison.Ordinal);
            Assert.Equal(at50.Count, at100.Count);
        }
    }
}
