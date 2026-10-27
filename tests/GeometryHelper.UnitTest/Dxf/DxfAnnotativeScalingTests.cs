using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// Annotative scaling: the drawing's list of scales as AutoCAD's ObjectContextCollection (SCALE objects of ACAD_SCALELIST),
    /// read, added to, renamed and taken out; whether an object is annotative, read for every kind, set for styles and taken
    /// away from any entity with its contexts; the contexts of an object read and taken out; and its way up of the sheet read.
    /// No context is written: AutoCAD's record of one is not yet checked on a drawing AutoCAD saved.
    /// </summary>
    public class DxfAnnotativeScalingTests
    {
        private const string ScaleList = "  0\nDICTIONARY\n  5\n14\n330\nA\n100\nAcDbDictionary\n281\n1\n";
        private const string AnnotativeData = "1001\nAcadAnnotative\n1000\nAnnotativeData\n1002\n{\n1070\n1\n1070\n1\n1002\n}\n";

        private static readonly DxfTypedValue[] AnnotativeXData =
        {
            new DxfTypedValue(1001, "AcadAnnotative"), new DxfTypedValue(1000, "AnnotativeData"), new DxfTypedValue(1002, "{"),
            new DxfTypedValue(1070, "1"), new DxfTypedValue(1070, "1"), new DxfTypedValue(1002, "}"),
        };

        /// <summary>
        /// The drawing of ezdxf of R2018 with three scales in its list, as AutoCAD saves them: 1:1 the unit scale (F0), 1:50
        /// (F1) and 1:100 (F2), named A0, A1 and A2; of another version where one is given.
        /// </summary>
        private static string Listed(string version = "AC1032")
        {
            string text = DxfFileEzdxfDrawings.R2018.Replace("AC1032", version);
            Assert.Contains(ScaleList, text, StringComparison.Ordinal);
            string scales = Scale("F0", "1:1", "1", "1", "1") + Scale("F1", "1:50", "1", "50", "0") + Scale("F2", "1:100", "1", "100", "0");
            int end = text.LastIndexOf("  0\nENDSEC\n", StringComparison.Ordinal);
            return (text.Substring(0, end) + scales + text.Substring(end)).Replace(ScaleList, ScaleList + "  3\nA0\n350\nF0\n  3\nA1\n350\nF1\n  3\nA2\n350\nF2\n");
        }

        private static string Scale(string handle, string name, string paper, string drawing, string unit)
            => "  0\nSCALE\n  5\n" + handle + "\n102\n{ACAD_REACTORS\n330\n14\n102\n}\n330\n14\n100\nAcDbScale\n 70\n0\n300\n" + name + "\n140\n" + paper + "\n141\n" + drawing + "\n290\n" + unit + "\n";

        private static DxfObjectContextCollection Scales(DxfDatabase db) => db.ObjectContextManager.GetContextCollection("ACDB_ANNOTATIONSCALES");

        private static string Record(string text, string handle) => Records(text).Single(r => HandleOf(r) == handle);

        /// <summary>
        /// The drawing of <see cref="Listed"/> with its MTEXT 73 annotative at 1:50 (the default, E01) and 1:100 (E02): its data
        /// AcadAnnotative, its dictionaries AcDbContextDataManager and ACDB_ANNOTATIONSCALES, and two contexts holding the groups
        /// the library reads of one, the default flag (290 of AcDbObjectContextData) and the scale (340 of
        /// AcDbAnnotScaleObjectContextData), as libredwg names them. The part of its own a context of an MTEXT has after those is
        /// left out: the library reads none of it, and no drawing AutoCAD saved shows it here.
        /// </summary>
        private static string AnnotativeMText()
        {
            DxfDatabase db = DxfDatabase.Parse(Listed());
            DxfMText mtext = ByHandle<DxfMText>(db, "73");
            mtext.XData = AnnotativeXData;
            mtext.CreateExtensionDictionary();
            var manager = new DxfDBDictionary { TreatElementsAsHard = true };
            mtext.ExtensionDictionary.SetAt("AcDbContextDataManager", manager);
            var contexts = new DxfDBDictionary { TreatElementsAsHard = true };
            manager.SetAt("ACDB_ANNOTATIONSCALES", contexts);
            string text = db.ToText();
            string owner = contexts.Handle;
            string Context(string handle, string scale, string isDefault) => "  0\nACDB_MTEXTOBJECTCONTEXTDATA_CLASS\n  5\n" + handle + "\n102\n{ACAD_REACTORS\n330\n" + owner + "\n102\n}\n330\n" + owner
                + "\n100\nAcDbObjectContextData\n290\n" + isDefault + "\n100\nAcDbAnnotScaleObjectContextData\n340\n" + scale + "\n";
            int end = text.LastIndexOf("  0\nENDSEC\n", StringComparison.Ordinal);
            text = text.Substring(0, end) + Context("E01", "F1", "1") + Context("E02", "F2", "0") + text.Substring(end);
            string record = Record(text, owner);
            return text.Replace(record, record + "  3\n*A1\n360\nE01\n  3\n*A2\n360\nE02\n");
        }

        [Fact]
        public void TheListOfScales_IsReadAsAutoCADsAnnotationScales_InTheOrderOfACAD_SCALELIST()
        {
            string text = Listed();
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfObjectContextCollection scales = Scales(db);

            Assert.Equal("ACDB_ANNOTATIONSCALES", scales.Name);
            Assert.Equal(new[] { "1:1", "1:50", "1:100" }, scales.Select(s => s.Name));
            Assert.All(scales, s => Assert.Equal("ACDB_ANNOTATIONSCALES", s.CollectionName));
            var fifty = (DxfAnnotationScale)scales.GetContext("1:50");
            Assert.Equal(Bits(1.0), Bits(fifty.PaperUnits));
            Assert.Equal(Bits(50.0), Bits(fifty.DrawingUnits));
            Assert.Equal(Bits(0.02), Bits(fifty.Scale));
            Assert.Equal("1:100", scales.GetContext(" 1:100 ").Name);
            Assert.True(scales.HasContext("1:50"));
            Assert.False(scales.HasContext("1:20"));
            Assert.Null(scales.GetContext("1:20"));
            Assert.NotNull(db.ObjectContextManager.GetContextCollection("acdb_annotationscales"));
            Assert.Null(db.ObjectContextManager.GetContextCollection("ACDB_VIEWSCALES"));

            // CANNOSCALE is not set in a drawing of ezdxf: AutoCAD's 1:1 is current.
            Assert.Equal("1:1", scales.CurrentContext.Name);
            Assert.Equal("1:50 (1 to 50)", fifty.ToString());
            Assert.Equal(text, db.ToText());

            Assert.Throws<ArgumentNullException>(() => scales.GetContext(null));
            Assert.Throws<ArgumentNullException>(() => db.ObjectContextManager.GetContextCollection(null));
            Assert.Empty(Scales(DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018)));
            Assert.Null(Scales(DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018)).CurrentContext);
            Assert.Empty(Scales(DxfDatabase.Parse(DxfFileEzdxfDrawings.R12)));
        }

        [Fact]
        public void AScaleAdded_IsASCALEOfACAD_SCALELIST_AsAutoCADWritesOne_AndIsTakenOutWhereNothingUsesIt()
        {
            string text = Listed();
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfObjectContextCollection scales = Scales(db);
            var twenty = new DxfAnnotationScale { Name = "1:20", PaperUnits = 1, DrawingUnits = 20 };

            Assert.Null(twenty.Handle);
            Assert.Equal(Bits(0.05), Bits(twenty.Scale));
            scales.AddContext(twenty);

            string saved = db.ToText();
            OnlyTheseChanged(text, saved, "14", twenty.Handle);
            Assert.Equal("  0\nSCALE\n  5\n" + twenty.Handle + "\n102\n{ACAD_REACTORS\n330\n14\n102\n}\n330\n14\n100\nAcDbScale\n 70\n0\n300\n1:20\n140\n1\n141\n20\n290\n0\n", Record(saved, twenty.Handle));
            Assert.EndsWith("  3\nA2\n350\nF2\n  3\nA3\n350\n" + twenty.Handle + "\n", Record(saved, "14"), StringComparison.Ordinal);
            Assert.Equal(new[] { "1:1", "1:50", "1:100", "1:20" }, Scales(DxfDatabase.Parse(saved)).Select(s => s.Name));

            // A name the list has, letters of either case the same, is refused, as AutoCAD answers eDuplicateKey; and a scale
            // in a file already, or with no name.
            Assert.Throws<ArgumentException>(() => scales.AddContext(new DxfAnnotationScale { Name = "1:50" }));
            Assert.Throws<ArgumentException>(() => scales.AddContext(twenty));
            Assert.Throws<ArgumentException>(() => scales.AddContext(new DxfAnnotationScale()));
            Assert.Throws<ArgumentNullException>(() => scales.AddContext(null));
            Assert.Throws<ArgumentException>(() => new DxfAnnotationScale { Name = "1:\t5" });
            Assert.Throws<ArgumentOutOfRangeException>(() => new DxfAnnotationScale { DrawingUnits = 0 });
            Assert.Throws<ArgumentOutOfRangeException>(() => twenty.PaperUnits = double.NaN);
            Assert.Equal(saved, db.ToText());

            // Taken out with its entry, the drawing is as it was but for the handle given out.
            scales.RemoveContext("1:20");
            Assert.Equal(Records(text).Skip(1), Records(db.ToText()).Skip(1));
            Assert.Equal(new[] { "1:1", "1:50", "1:100" }, scales.Select(s => s.Name));
            Assert.Throws<KeyNotFoundException>(() => scales.RemoveContext("1:20"));
            Assert.Throws<ArgumentNullException>(() => scales.RemoveContext(null));
        }

        [Fact]
        public void AScaleAutoCADKeeps_IsNotTakenOut_TheUnitScaleTheCurrentScaleAndOneInUse()
        {
            DxfDatabase db = DxfDatabase.Parse(AnnotativeMText());
            DxfObjectContextCollection scales = Scales(db);
            scales.CurrentContext = scales.GetContext("1:50");
            string text = db.ToText();

            Assert.Contains("unit scale", Assert.Throws<InvalidOperationException>(() => scales.RemoveContext("1:1")).Message, StringComparison.Ordinal);
            Assert.Contains("current annotation scale", Assert.Throws<InvalidOperationException>(() => scales.RemoveContext("1:50")).Message, StringComparison.Ordinal);
            Assert.Contains("in use, 1 objects pointing at it", Assert.Throws<InvalidOperationException>(() => scales.RemoveContext("1:100")).Message, StringComparison.Ordinal);
            Assert.Equal(text, db.ToText());
        }

        [Fact]
        public void A1To1ScaleAddedToAListWithNoUnitScale_IsTheUnitScale_AsAutoCADKeepsOne()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfObjectContextCollection scales = Scales(db);
            var one = new DxfAnnotationScale { Name = "1:1", PaperUnits = 1, DrawingUnits = 1 };
            var ten = new DxfAnnotationScale { Name = "10:10", PaperUnits = 10, DrawingUnits = 10 };
            var fifty = new DxfAnnotationScale { Name = "1:50", PaperUnits = 1, DrawingUnits = 50 };

            scales.AddContext(fifty);
            scales.AddContext(one);
            scales.AddContext(ten);

            string saved = db.ToText();
            Assert.EndsWith("141\n50\n290\n0\n", Record(saved, fifty.Handle), StringComparison.Ordinal);
            Assert.EndsWith("300\n1:1\n140\n1\n141\n1\n290\n1\n", Record(saved, one.Handle), StringComparison.Ordinal);
            Assert.EndsWith("141\n10\n290\n0\n", Record(saved, ten.Handle), StringComparison.Ordinal);
            Assert.Contains("unit scale", Assert.Throws<InvalidOperationException>(() => scales.RemoveContext("1:1")).Message, StringComparison.Ordinal);
            scales.RemoveContext("10:10");

            // A list with a unit scale has its 1:1 named otherwise added as a scale like any other.
            DxfDatabase listed = DxfDatabase.Parse(Listed());
            var same = new DxfAnnotationScale { Name = "Full size", PaperUnits = 1, DrawingUnits = 1 };
            Scales(listed).AddContext(same);
            Assert.EndsWith("290\n0\n", Record(listed.ToText(), same.Handle), StringComparison.Ordinal);
        }

        [Fact]
        public void AScaleRenamed_IsNamedAnewByTheAnnotationScaleThatNamedIt_AndItsUnitsAreWrittenInPlace()
        {
            DxfDatabase db = DxfDatabase.Parse(Listed());
            DxfObjectContextCollection scales = Scales(db);
            scales.CurrentContext = scales.GetContext("1:50");
            string text = db.ToText();
            var fifty = (DxfAnnotationScale)scales.GetContext("1:50");

            fifty.Name = "Plan 1:50";
            fifty.DrawingUnits = 50.5;

            Assert.Equal("Plan 1:50", db.Cannoscale);
            Assert.Equal("Plan 1:50", scales.CurrentContext.Name);
            string saved = db.ToText();
            Assert.Contains("300\nPlan 1:50\n140\n1\n141\n50.5\n290\n0\n", Record(saved, "F1"), StringComparison.Ordinal);
            Assert.Equal(Bits(1.0 / 50.5), Bits(((DxfAnnotationScale)Scales(DxfDatabase.Parse(saved)).GetContext("plan 1:50")).Scale));

            // A name another scale has is refused, one of other letters of its own is not; values it has change nothing.
            Assert.Throws<ArgumentException>(() => fifty.Name = "1:100");
            fifty.Name = "PLAN 1:50";
            Assert.Equal("PLAN 1:50", db.Cannoscale);
            fifty.Name = "Plan 1:50";
            fifty.PaperUnits = 1;
            fifty.DrawingUnits = 50.5;
            Assert.Equal(saved, db.ToText());
            Assert.NotEqual(text, saved);
            Assert.Throws<ArgumentException>(() => scales.CurrentContext = new DxfAnnotationScale { Name = "1:5" });
            Assert.Throws<ArgumentException>(() => scales.CurrentContext = Scales(DxfDatabase.Parse(Listed())).GetContext("1:100"));
            Assert.Throws<ArgumentNullException>(() => scales.CurrentContext = null);
        }

        [Fact]
        public void TheCurrentScaleOfADrawingThatCannotWriteCANNOSCALE_IsNotRenamed_AndNothingIsWritten()
        {
            string listed = Listed();
            string variableClass = Records(listed).Single(r => r.StartsWith("  0\nCLASS\n  1\nDICTIONARYVAR\n", StringComparison.Ordinal));
            DxfDatabase db = DxfDatabase.Parse(listed.Replace(variableClass, string.Empty));
            string text = db.ToText();
            var one = (DxfAnnotationScale)Scales(db).CurrentContext;

            Assert.Equal("1:1", one.Name);
            Assert.Throws<InvalidOperationException>(() => one.Name = "Full size");

            Assert.Equal(text, db.ToText());
            Assert.Equal("1:1", one.Name);

            // A scale that is not current is renamed: CANNOSCALE is not written.
            Scales(db).GetContext("1:50").Name = "Plan";
            Assert.Equal("Plan", Scales(DxfDatabase.Parse(db.ToText())).GetContext("plan").Name);
        }

        [Fact]
        public void ADrawingWithNoListOfScales_IsGivenOneFromR2007_AndOneOlderIsRefused()
        {
            const string Entry = "  3\nACAD_SCALELIST\n350\n14\n";
            string unlisted = DxfFileEzdxfDrawings.R2018.Replace("AC1032", "AC1021").Replace(Entry, string.Empty);
            DxfDatabase db = DxfDatabase.Parse(unlisted);
            Assert.Empty(Scales(db));

            Scales(db).AddContext(new DxfAnnotationScale { Name = "1:50", PaperUnits = 1, DrawingUnits = 50 });

            DxfDBDictionary list = (DxfDBDictionary)db.NamedObjectsDictionary.GetAt("ACAD_SCALELIST");
            Assert.NotEqual("14", list.Handle);
            Assert.Equal("A0", list.Single().Key);
            Assert.Equal(new[] { "1:50" }, Scales(DxfDatabase.Parse(db.ToText())).Select(s => s.Name));

            DxfDatabase r2004 = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018.Replace("AC1032", "AC1018").Replace(Entry, string.Empty));
            string before = r2004.ToText();
            Assert.Throws<InvalidOperationException>(() => Scales(r2004).AddContext(new DxfAnnotationScale { Name = "1:50", PaperUnits = 1, DrawingUnits = 50 }));
            Assert.Equal(before, r2004.ToText());
            Assert.Throws<InvalidOperationException>(() => Scales(DxfDatabase.Parse(DxfFileEzdxfDrawings.R12)).AddContext(new DxfAnnotationScale { Name = "1:50" }));

            // A drawing of R2000 that has the list, as one of ezdxf, takes a scale in it.
            DxfDatabase r2000 = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            Scales(r2000).AddContext(new DxfAnnotationScale { Name = "1:50", PaperUnits = 1, DrawingUnits = 50 });
            Assert.Equal("1:50", Scales(DxfDatabase.Parse(r2000.ToText())).Single().Name);
        }

        [Fact]
        public void WhetherAnObjectIsAnnotative_IsReadForEveryKind_AndOneNeverAnnotativeSaysNotApplicable()
        {
            DxfDatabase db = DxfDatabase.Parse(Listed());
            string text = db.ToText();
            DxfLine line = ByHandle<DxfLine>(db, "3E");
            DxfDBText grid = ByHandle<DxfDBText>(db, "64");

            Assert.Equal(DxfAnnotativeStates.NotApplicable, line.Annotative);
            Assert.Equal(DxfPaperOrientationStates.NotApplicable, line.PaperOrientation);
            Assert.Throws<InvalidOperationException>(() => line.Annotative = DxfAnnotativeStates.True);
            Assert.Throws<InvalidOperationException>(() => line.Annotative = DxfAnnotativeStates.False);
            Assert.Equal(DxfAnnotativeStates.NotApplicable, db.LayerTable["0"].Annotative);
            Assert.Equal(DxfAnnotativeStates.NotApplicable, db.NamedObjectsDictionary.Annotative);
            Assert.Equal(DxfPaperOrientationStates.NotApplicable, ByHandle<DxfDimension>(db, "75").PaperOrientation);
            Assert.Equal(DxfAnnotativeStates.False, ByHandle<DxfDimension>(db, "75").Annotative);
            Assert.Equal(DxfAnnotativeStates.False, ByHandle<DxfHatch>(db, "74").Annotative);
            Assert.Equal(DxfAnnotativeStates.False, ByHandle<DxfMText>(db, "73").Annotative);
            Assert.Equal(DxfAnnotativeStates.False, grid.Annotative);
            Assert.Equal(DxfPaperOrientationStates.False, grid.PaperOrientation);
            Assert.False(grid.HasContext(Scales(db).GetContext("1:1")));

            // An entity is not made annotative: AutoCAD shows it at a scale by a context the library does not write yet.
            Assert.Contains("checked on a drawing AutoCAD saved", Assert.Throws<NotSupportedException>(() => grid.Annotative = DxfAnnotativeStates.True).Message, StringComparison.Ordinal);
            Assert.Throws<NotSupportedException>(() => ByHandle<DxfMText>(db, "73").Annotative = DxfAnnotativeStates.True);
            Assert.Throws<ArgumentException>(() => grid.Annotative = DxfAnnotativeStates.NotApplicable);
            Assert.Throws<ArgumentException>(() => grid.Annotative = (DxfAnnotativeStates)7);
            grid.Annotative = DxfAnnotativeStates.False;
            grid.RemoveContext(Scales(db).GetContext("1:1"));
            Assert.False(grid.HasContext(new DxfAnnotationScale { Name = "1:5" }));
            Assert.False(grid.HasContext(Scales(DxfDatabase.Parse(Listed())).GetContext("1:1")));
            Assert.Throws<ArgumentNullException>(() => grid.HasContext(null));
            Assert.Throws<ArgumentNullException>(() => grid.RemoveContext(null));
            Assert.Equal(text, db.ToText());
        }

        [Fact]
        public void TheContextsOfAnAnnotativeMText_AreReadAndTakenOut_TheDefaultAndTheLastKept()
        {
            string text = AnnotativeMText();
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfObjectContextCollection scales = Scales(db);
            DxfMText note = ByHandle<DxfMText>(db, "73");

            Assert.Equal(DxfAnnotativeStates.True, note.Annotative);
            Assert.Equal(DxfPaperOrientationStates.False, note.PaperOrientation);
            Assert.Equal(new[] { false, true, true }, scales.Select(s => note.HasContext(s)));
            Assert.Equal(text, db.ToText());

            // The default context is kept: the library makes no other the default in its place.
            Assert.Contains("is the default", Assert.Throws<InvalidOperationException>(() => note.RemoveContext(scales.GetContext("1:50"))).Message, StringComparison.Ordinal);
            note.RemoveContext(scales.GetContext("1:1"));
            Assert.Equal(text, db.ToText());

            // Another is taken out with its entry, every other record as it was; the last is kept.
            note.RemoveContext(scales.GetContext("1:100"));
            string saved = db.ToText();
            Assert.DoesNotContain(Records(saved), r => HandleOf(r) == "E02");
            Assert.Equal(Record(text, "E01"), Record(saved, "E01"));
            Assert.False(note.HasContext(scales.GetContext("1:100")));
            Assert.Contains("is the last", Assert.Throws<InvalidOperationException>(() => note.RemoveContext(scales.GetContext("1:50"))).Message, StringComparison.Ordinal);

            // Made not annotative, it loses its contexts and its data, its own groups kept.
            note.Annotative = DxfAnnotativeStates.False;
            Assert.Equal(DxfAnnotativeStates.False, note.Annotative);
            Assert.Empty(note.XData);
            Assert.DoesNotContain(Records(db.ToText()), r => HandleOf(r) == "E01");
            Assert.False(note.ExtensionDictionary.Contains("AcDbContextDataManager"));
            Assert.Equal(Record(Listed(), "73"), Record(db.ToText(), "73").Replace("102\n{ACAD_XDICTIONARY\n360\n" + note.ExtensionDictionary.Handle + "\n102\n}\n", string.Empty));
            note.Annotative = DxfAnnotativeStates.False;
        }

        [Fact]
        public void AnAnnotativeTextKeepsTheWayUpOfTheSheet_ByAcadAnnoPO_AndOneNotAnnotativeDoesNot()
        {
            DxfDatabase db = DxfDatabase.Parse(Listed());
            DxfDBText grid = ByHandle<DxfDBText>(db, "64");

            grid.XData = new[] { new DxfTypedValue(1001, "AcadAnnoPO"), new DxfTypedValue(1070, "1") };
            Assert.Equal(DxfPaperOrientationStates.False, grid.PaperOrientation);
            grid.XData = AnnotativeXData;
            Assert.Equal(DxfPaperOrientationStates.True, grid.PaperOrientation);
            grid.XData = new[] { new DxfTypedValue(1001, "AcadAnnoPO"), new DxfTypedValue(1070, "0") };
            Assert.Equal(DxfPaperOrientationStates.False, grid.PaperOrientation);

            // Made not annotative, it loses both.
            grid.Annotative = DxfAnnotativeStates.False;
            Assert.Empty(grid.XData);
            Assert.Null(grid.GetXDataForApplication("AcadAnnoPO"));
        }

        [Fact]
        public void AMultileader_IsAnnotativeByItsOwnFlagAsWell_AndLosesItWhenMadeNotAnnotative()
        {
            DxfDatabase db = DxfDatabase.Parse(Listed());
            DxfMLeader leader = ByHandle<DxfMLeader>(db, "8A");
            Assert.Equal(DxfAnnotativeStates.False, leader.Annotative);

            leader.EnableAnnotationScale = true;
            Assert.Equal(DxfAnnotativeStates.True, leader.Annotative);
            Assert.Equal(DxfPaperOrientationStates.NotApplicable, leader.PaperOrientation);

            leader.Annotative = DxfAnnotativeStates.False;
            Assert.False(leader.EnableAnnotationScale);
            Assert.Equal(DxfAnnotativeStates.False, leader.Annotative);

            // The MULTILEADER is as it was; the header's extents, which an edited entity grows, aside.
            Assert.Equal(Records(Listed()).Skip(1), Records(db.ToText()).Skip(1));
        }

        [Fact]
        public void StylesAreMadeAnnotativeByTheirDataAlone_AndAnINSERTIsAnnotativeAsItsBlockIs()
        {
            DxfDatabase db = DxfDatabase.Parse(Listed());
            DxfTextStyleTableRecord standard = db.TextStyleTable["Standard"];
            DxfDimStyleTableRecord dimensions = db.DimStyleTable["Standard"];
            string text = db.ToText();

            standard.Annotative = DxfAnnotativeStates.True;
            dimensions.Annotative = DxfAnnotativeStates.True;

            string saved = db.ToText();
            Assert.EndsWith(AnnotativeData, Record(saved, standard.Handle), StringComparison.Ordinal);
            Assert.EndsWith(AnnotativeData, Records(saved).Single(r => r.Contains("\n105\n" + dimensions.Handle + "\n")), StringComparison.Ordinal);
            Assert.Equal(DxfAnnotativeStates.True, standard.Annotative);
            Assert.Null(standard.ExtensionDictionary);
            Assert.Equal(DxfPaperOrientationStates.False, standard.PaperOrientation);
            standard.Annotative = DxfAnnotativeStates.False;
            dimensions.Annotative = DxfAnnotativeStates.False;
            OnlyTheseChanged(text, db.ToText(), "3", Records(db.ToText()).Where(r => r.StartsWith("  0\nAPPID\n", StringComparison.Ordinal)).Select(HandleOf).Last());

            // A block marked annotative by AutoCAD's data makes its INSERTs so; neither is set here.
            DxfBlockReference insert = ByHandle<DxfBlockReference>(db, "65");
            Assert.Equal(DxfAnnotativeStates.False, insert.Annotative);
            insert.BlockTableRecord.XData = AnnotativeXData.Concat(new[] { new DxfTypedValue(1001, "AcadAnnoPO"), new DxfTypedValue(1070, "1") }).ToArray();
            Assert.Equal(DxfAnnotativeStates.True, insert.BlockTableRecord.Annotative);
            Assert.Equal(DxfAnnotativeStates.True, insert.Annotative);
            Assert.Equal(DxfPaperOrientationStates.True, insert.PaperOrientation);
            Assert.Throws<NotSupportedException>(() => insert.Annotative = DxfAnnotativeStates.False);
            Assert.Throws<NotSupportedException>(() => insert.BlockTableRecord.Annotative = DxfAnnotativeStates.False);
        }

        [Fact]
        public void AStyleOfR2000_IsRefusedItsFlag_AndOneOfR2007TakesIt()
        {
            DxfDatabase r2007 = DxfDatabase.Parse(Listed("AC1021"));
            r2007.TextStyleTable["Standard"].Annotative = DxfAnnotativeStates.True;
            Assert.Equal(DxfAnnotativeStates.True, DxfDatabase.Parse(r2007.ToText()).TextStyleTable["Standard"].Annotative);

            DxfDatabase r2000 = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            string before = r2000.ToText();
            Assert.Throws<InvalidOperationException>(() => r2000.TextStyleTable["Standard"].Annotative = DxfAnnotativeStates.True);
            Assert.Equal(before, r2000.ToText());
            Assert.Throws<InvalidOperationException>(() => new DxfTextStyleTableRecord().Annotative = DxfAnnotativeStates.True);
        }

        [Fact]
        public void ADrawingWhoseAnnotativeMembersAreRead_ResavesByteForByte()
        {
            foreach (string text in new[] { DxfFileEzdxfDrawings.R12, DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018, Listed(), AnnotativeMText() })
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                List<DxfObjectContext> scales = Scales(db).ToList();
                var read = new List<string>();

                foreach (DxfDBObject item in Everything(db).Cast<DxfDBObject>().Concat(db.TextStyleTable.Cast<DxfDBObject>()).Concat(db.DimStyleTable.Cast<DxfDBObject>()).Concat(db.BlockTable.Cast<DxfDBObject>()).Concat(db.LayerTable.Cast<DxfDBObject>()))
                {
                    read.Add(item.Annotative + " " + item.PaperOrientation + " " + string.Concat(scales.Select(s => item.HasContext(s) ? "1" : "0")));
                }

                Assert.NotEmpty(read);
                Assert.Equal(text, db.ToText());
            }
        }
    }
}
