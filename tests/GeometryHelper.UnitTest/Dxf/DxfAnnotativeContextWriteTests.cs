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
    /// An entity made annotative and shown at a second scale, as AutoCAD 2021 writes it (probes of the plugins GHAN1 and GHRUN:
    /// a TEXT, an MTEXT, an ATTDEF, a HATCH and an INSERT of an annotative block drawn at 1:1 and 1:50, their group values read
    /// from the DXF AutoCAD wrote). The drawing is ezdxf's of R2018 with the scales 1:1 (F0), 1:50 (F1) and 1:100 (F2).
    /// </summary>
    public class DxfAnnotativeContextWriteTests
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

        private static DxfDatabase Scaled(string version = "AC1032")
        {
            string text = DxfFileEzdxfDrawings.R2018.Replace("AC1032", version);
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

        private static string[] Names(DxfDBObject o) => o.GetContexts().Select(c => c.Name).ToArray();

        /// <summary>The names of the entries of ACDB_ANNOTATIONSCALES of an object, in order.</summary>
        private static string[] Keys(DxfDBObject o)
        {
            var manager = (DxfDBDictionary)o.ExtensionDictionary.GetAt("AcDbContextDataManager");
            var scales = (DxfDBDictionary)manager.GetAt("ACDB_ANNOTATIONSCALES");
            return scales.Select(e => e.Key).ToArray();
        }

        /// <summary>The groups of a context after its type, as the text of the drawing holds them: code and value of each.</summary>
        private static List<KeyValuePair<int, string>> Context(
            DxfDatabase db,
            DxfDBObject o,
            string key,
            out string type
        )
        {
            var manager = (DxfDBDictionary)o.ExtensionDictionary.GetAt("AcDbContextDataManager");
            var scales = (DxfDBDictionary)manager.GetAt("ACDB_ANNOTATIONSCALES");
            string handle = scales.GetAt(key).Handle;
            string record = Records(db.ToText()).Single(r => HandleOf(r) == handle);
            string[] lines = record.Split('\n');
            type = lines[1];
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

        /// <summary>The groups from the scale (340) on, with the code and the value read as numbers where they are.</summary>
        private static string After(List<KeyValuePair<int, string>> groups) =>
            string.Join(
                " ",
                groups
                    .SkipWhile(g => g.Key != 340)
                    .Select(g =>
                        g.Key
                        + "="
                        + (
                            g.Key != 340
                            && g.Key != 100
                            && double.TryParse(g.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)
                                ? Math.Round(d, 6).ToString("0.######", CultureInfo.InvariantCulture)
                                : g.Value
                        )
                    )
            );

        private static string Default(List<KeyValuePair<int, string>> groups) => groups.First(g => g.Key == 290).Value;

        private static DxfDBText Text(DxfDatabase db)
        {
            var text = new DxfDBText
            {
                Position = new GeoPoint3(10, 10, 0),
                TextString = "ABC",
                Height = 2.5,
            };
            db.ModelSpace.AppendEntity(text);
            return text;
        }

        [Fact]
        public void ATextMadeAnnotativeAtOneToOne_HoldsOneDefaultContext_AsAutoCADWritesIt()
        {
            DxfDatabase db = Scaled();
            db.Cannoscale = "1:1";
            DxfDBText text = Text(db);
            text.Annotative = DxfAnnotativeStates.True;

            Assert.Equal(DxfAnnotativeStates.True, text.Annotative);
            Assert.NotNull(text.GetXDataForApplication("AcadAnnotative"));
            Assert.Equal(new[] { "*A1" }, Keys(text));
            List<KeyValuePair<int, string>> groups = Context(db, text, "*A1", out string type);
            Assert.Equal("ACDB_TEXTOBJECTCONTEXTDATA_CLASS", type);
            Assert.Equal("1", Default(groups));
            Assert.Equal("340=F0 70=0 50=0 10=10 20=10 11=0 21=0", After(groups));
            Assert.Equal(new[] { "1:1" }, Names(text));
            Assert.True(text.HasContext(Of(db, "1:1")));
            Assert.False(text.HasContext(Of(db, "1:50")));

            DxfDatabase back = DxfDatabase.Parse(db.ToText());
            var again = ByHandle<DxfDBText>(back, text.Handle);
            Assert.Equal(new[] { "1:1" }, Names(again));
            Assert.Contains("ACDB_TEXTOBJECTCONTEXTDATA_CLASS", back.ToText(), StringComparison.Ordinal);
            Assert.Contains("AcDbTextObjectContextData", back.ToText(), StringComparison.Ordinal);
        }

        [Fact]
        public void ATextMadeAnnotativeAtOneToFifty_HoldsItsContextAtThatScale_ItsRecordKept()
        {
            DxfDatabase db = Scaled();
            db.Cannoscale = "1:50";
            DxfDBText text = Text(db);
            text.Height = 125.0;
            text.Annotative = DxfAnnotativeStates.True;

            Assert.Equal(new[] { "1:50" }, Names(text));
            Assert.Equal("340=F1 70=0 50=0 10=10 20=10 11=0 21=0", After(Context(db, text, "*A1", out _)));
            Assert.Equal(125.0, text.Height, 9);
        }

        [Fact]
        public void ATextShownAtASecondScale_HasASecondContext_NotTheDefault_AndLosesItAgain()
        {
            DxfDatabase db = Scaled();
            DxfDBText text = Text(db);
            text.Annotative = DxfAnnotativeStates.True;
            text.AddContext(Of(db, "1:50"));

            Assert.Equal(new[] { "*A1", "*A2" }, Keys(text));
            List<KeyValuePair<int, string>> second = Context(db, text, "*A2", out string type);
            Assert.Equal("ACDB_TEXTOBJECTCONTEXTDATA_CLASS", type);
            Assert.Equal("0", Default(second));
            Assert.Equal("340=F1 70=0 50=0 10=10 20=10 11=0 21=0", After(second));
            Assert.Equal(new[] { "1:1", "1:50" }, Names(text));
            Assert.True(text.HasContext(Of(db, "1:50")));

            text.AddContext(Of(db, "1:50"));
            Assert.Equal(new[] { "*A1", "*A2" }, Keys(text));

            text.RemoveContext(Of(db, "1:50"));
            Assert.Equal(new[] { "1:1" }, Names(text));
            text.AddContext(Of(db, "1:100"));
            Assert.Equal(new[] { "*A1", "*A2" }, Keys(text));
            Assert.Equal(new[] { "1:1", "1:100" }, Names(text));
        }

        [Fact]
        public void AnAttributeDefinitionMadeAnnotative_IsGivenTheContextOfAnAttribute_WithItsTrailingZero()
        {
            DxfDatabase db = Scaled();
            var definition = new DxfAttributeDefinition
            {
                Tag = "TAG1",
                TextString = "V",
                Position = new GeoPoint3(10, 50, 0),
                Height = 2.5,
            };
            db.ModelSpace.AppendEntity(definition);
            definition.Annotative = DxfAnnotativeStates.True;
            definition.AddContext(Of(db, "1:50"));

            List<KeyValuePair<int, string>> groups = Context(db, definition, "*A1", out string type);
            Assert.Equal("ACDB_MTEXTATTRIBUTEOBJECTCONTEXTDATA_CLASS", type);
            Assert.Equal("340=F0 70=0 50=0 10=10 20=50 11=0 21=0 290=0", After(groups));
            Assert.Equal("340=F1 70=0 50=0 10=10 20=50 11=0 21=0 290=0", After(Context(db, definition, "*A2", out _)));
        }

        [Fact]
        public void AnMTextMadeAnnotative_ScalesItsWidthAtASecondScale_AsAutoCADDoes()
        {
            DxfDatabase db = Scaled();
            var words = new DxfMText
            {
                Location = new GeoPoint3(10, 30, 0),
                TextHeight = 2.5,
                Contents = "MTX",
                Width = 40.0,
            };
            db.ModelSpace.AppendEntity(words);
            words.Annotative = DxfAnnotativeStates.True;
            words.AddContext(Of(db, "1:50"));

            List<KeyValuePair<int, string>> first = Context(db, words, "*A1", out string type);
            Assert.Equal("ACDB_MTEXTOBJECTCONTEXTDATA_CLASS", type);
            Assert.Equal("1", Default(first));
            Assert.StartsWith(
                "340=F0 70=1 10=1 20=0 30=0 11=10 21=30 31=0 40=40 41=0 42=",
                After(first),
                StringComparison.Ordinal
            );
            List<KeyValuePair<int, string>> second = Context(db, words, "*A2", out _);
            Assert.Equal("0", Default(second));
            Assert.StartsWith(
                "340=F1 70=1 10=1 20=0 30=0 11=10 21=30 31=0 40=2000 41=0 42=",
                After(second),
                StringComparison.Ordinal
            );
            Assert.EndsWith(" 71=0", After(second), StringComparison.Ordinal);
        }

        private static DxfHatch Hatch(DxfDatabase db)
        {
            var hatch = new DxfHatch();
            hatch.SetHatchPattern(DxfHatchPatternType.UserDefined, "_USER");
            hatch.PatternAngle = Math.PI / 6.0;
            hatch.PatternSpace = 2.0;
            hatch.AppendLoop(
                DxfHatchLoopTypes.External,
                new[] { new GeoPoint2(0, 0), new GeoPoint2(30, 0), new GeoPoint2(30, 20), new GeoPoint2(0, 20) },
                null
            );
            db.ModelSpace.AppendEntity(hatch);
            return hatch;
        }

        [Fact]
        public void AHatchMadeAnnotativeAtOneToFifty_HasItsPatternLinesScaledBy50_AndAContextOfScale50()
        {
            DxfDatabase db = Scaled();
            db.Cannoscale = "1:50";
            DxfHatch hatch = Hatch(db);
            double offset = hatch.GetPatternDefinitionAt(0).OffsetY;
            hatch.Annotative = DxfAnnotativeStates.True;

            Assert.Equal(new[] { "1:50" }, Names(hatch));
            Assert.Equal(offset * 50.0, hatch.GetPatternDefinitionAt(0).OffsetY, 6);
            List<KeyValuePair<int, string>> groups = Context(db, hatch, "*A1", out string type);
            Assert.Equal("ACDB_HATCHSCALECONTEXTDATA_CLASS", type);
            Assert.Equal("1", Default(groups));
            string after = After(groups);
            Assert.Contains("100=AcDbHatchObjectContextData 78=1 53=", after, StringComparison.Ordinal);
            Assert.Contains(" 40=50 10=0.866025 20=0.5 30=0 90=1 90=", after, StringComparison.Ordinal);
            Assert.EndsWith(" 290=1", after, StringComparison.Ordinal);
            Assert.Contains("ACDB_HATCHSCALECONTEXTDATA_CLASS", db.ToText(), StringComparison.Ordinal);
        }

        [Fact]
        public void AHatchShownAtASecondScale_HasItsLinesScaledByTheScalesAndGroup40OfOneOverIt()
        {
            DxfDatabase db = Scaled();
            DxfHatch hatch = Hatch(db);
            double offset = hatch.GetPatternDefinitionAt(0).OffsetY;
            hatch.Annotative = DxfAnnotativeStates.True;
            hatch.AddContext(Of(db, "1:100"));

            Assert.Equal(offset, hatch.GetPatternDefinitionAt(0).OffsetY, 9);
            List<KeyValuePair<int, string>> first = Context(db, hatch, "*A1", out _);
            List<KeyValuePair<int, string>> second = Context(db, hatch, "*A2", out _);
            Assert.Equal("0", Default(second));
            Assert.Contains(" 40=1 ", After(first) + " ", StringComparison.Ordinal);
            Assert.Contains(" 40=100 ", After(second) + " ", StringComparison.Ordinal);
            double Offset(List<KeyValuePair<int, string>> groups) =>
                double.Parse(groups.First(g => g.Key == 46).Value, CultureInfo.InvariantCulture);
            Assert.Equal(Offset(first) * 100.0, Offset(second), 6);
        }

        [Fact]
        public void AnInsertOfAnAnnotativeBlock_ShownAt1To50_HasItsFactorsScaledAndItsAttributesMovedOut()
        {
            DxfDatabase db = Scaled();
            var block = new DxfBlockTableRecord { Name = "ANBLK" };
            db.BlockTable.Add(block);
            block.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)));
            block.Annotative = DxfAnnotativeStates.True;
            Assert.Equal(DxfAnnotativeStates.True, block.Annotative);
            Assert.NotNull(block.GetXDataForApplication("AcadAnnotative"));

            var insert = new DxfBlockReference(new GeoPoint3(300, 0, 0), block);
            db.ModelSpace.AppendEntity(insert);
            Assert.Equal(new[] { "1:1" }, Names(insert));

            // AutoCAD 2021 gave the INSERT of no context the new scale as its one context, factors 50 for 1.
            insert.AddContext(Of(db, "1:50"));
            Assert.Equal(new[] { "1:50" }, Names(insert));
            Assert.Equal(new[] { "*A1" }, Keys(insert));
            List<KeyValuePair<int, string>> first = Context(db, insert, "*A1", out string type);
            Assert.Equal("ACDB_BLKREFOBJECTCONTEXTDATA_CLASS", type);
            Assert.Equal("1", Default(first));
            Assert.Equal("340=F1 50=0 10=300 20=0 30=0 41=50 42=50 43=50", After(first));
            Assert.Equal(new GeoVector3(50, 50, 50), insert.ScaleFactors);

            // A second scale: the factors of the default (50) times 1:50 over 1:100 (2).
            insert.AddContext(Of(db, "1:100"));
            Assert.Equal(new[] { "*A1", "*A2" }, Keys(insert));
            List<KeyValuePair<int, string>> second = Context(db, insert, "*A2", out _);
            Assert.Equal("0", Default(second));
            Assert.Equal("340=F2 50=0 10=300 20=0 30=0 41=100 42=100 43=100", After(second));
        }

        [Fact]
        public void AnObjectNotAnnotative_IsRefusedAContext_AndTheKindsNotMadeYetAreRefusedClearly()
        {
            DxfDatabase db = Scaled();
            DxfDBText text = Text(db);
            Assert.Throws<InvalidOperationException>(() => text.AddContext(Of(db, "1:50")));
            Assert.Throws<ArgumentNullException>(() => text.AddContext(null));
            Assert.Empty(Names(text));

            text.Annotative = DxfAnnotativeStates.True;
            Assert.Throws<ArgumentException>(() => text.AddContext(new DxfAnnotationScale { Name = "1:7" }));
            Assert.Throws<InvalidOperationException>(() => new DxfDBText().AddContext(Of(db, "1:50")));

            var line = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0));
            db.ModelSpace.AppendEntity(line);
            Assert.Throws<InvalidOperationException>(() => line.Annotative = DxfAnnotativeStates.True);
            Assert.Empty(line.GetContexts());

            DxfDatabase old = Scaled("AC1015");
            DxfDBText ancient = Text(old);
            Assert.Throws<InvalidOperationException>(() => ancient.Annotative = DxfAnnotativeStates.True);
        }

        [Fact]
        public void ATextMadeNotAnnotativeAgain_LosesItsContexts_AndThenHasNone()
        {
            DxfDatabase db = Scaled();
            DxfDBText text = Text(db);
            text.Annotative = DxfAnnotativeStates.True;
            text.AddContext(Of(db, "1:50"));
            text.Annotative = DxfAnnotativeStates.False;

            Assert.Equal(DxfAnnotativeStates.False, text.Annotative);
            Assert.Empty(Names(text));
            Assert.Throws<InvalidOperationException>(() => text.AddContext(Of(db, "1:50")));
        }

        [Fact]
        public void AMultileaderMadeAnnotative_SetsItsFlag_AndHoldsOneContextOfItsContextData()
        {
            DxfDatabase db = DxfDatabase.Parse(Scaled().ToText());
            var leader = new DxfMLeader();
            db.ModelSpace.AppendEntity(leader);
            leader.Annotative = DxfAnnotativeStates.True;

            Assert.True(leader.EnableAnnotationScale);
            Assert.Null(leader.GetXDataForApplication("AcadAnnotative"));
            Assert.Equal(new[] { "*A1" }, Keys(leader));
            Context(db, leader, "*A1", out string type);
            Assert.Equal("ACDB_MLEADEROBJECTCONTEXTDATA_CLASS", type);
            Assert.Equal(new[] { "1:1" }, Names(leader));
            Assert.Throws<NotSupportedException>(() => leader.AddContext(Of(db, "1:50")));
        }

        [Fact]
        public void AnInsertIsNotMadeAnnotativeItself_ButAsItsBlockIs_AndABlockIsNotMadeNotAnnotative()
        {
            DxfDatabase db = Scaled();
            var block = new DxfBlockTableRecord { Name = "B1" };
            db.BlockTable.Add(block);
            var insert = new DxfBlockReference(new GeoPoint3(0, 0, 0), block);
            db.ModelSpace.AppendEntity(insert);
            Assert.Throws<NotSupportedException>(() => insert.Annotative = DxfAnnotativeStates.True);
            Assert.Throws<InvalidOperationException>(() => insert.AddContext(Of(db, "1:50")));
            block.Annotative = DxfAnnotativeStates.True;
            Assert.Equal(DxfAnnotativeStates.True, insert.Annotative);
            Assert.Throws<NotSupportedException>(() => block.Annotative = DxfAnnotativeStates.False);
        }
    }
}
