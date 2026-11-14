using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.CenterMarkTests;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// What AutoCAD 2021's console kept of ISOLATEOBJECTS, HIDEOBJECTS and UNISOLATEOBJECTS in the drawing (standard input): the
    /// list of the objects hidden (AEC_ISOLATED_OBJECTS), the record of the last isolation (ACAD_OBJECTISOLATION), and with
    /// OBJECTISOLATIONMODE 1 the objects written hidden, each with its mark in extended data.
    /// </summary>
    public class ObjectIsolationTests
    {
        private const string Mark = "AEC_ISOLATEDOBJECTS_XDATA";

        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        /// <summary>A drawing with three lines, their handles in order.</summary>
        private static CadDocument ThreeLines(out string[] lines)
        {
            CadDocument document = Blank();
            Start(
                document,
                "LINE",
                P(0, 100),
                P(100, 100),
                Enter,
                "LINE",
                P(0, 120),
                P(100, 120),
                Enter,
                "LINE",
                P(0, 140),
                P(100, 140),
                Enter
            );
            lines = All<DxfLine>(document.ModelSpace).Select(l => l.Handle).ToArray();
            return document;
        }

        /// <summary>The list of the hidden objects the drawing holds: the 340 of the record in AEC_ISOLATED_OBJECTS; null where it has none.</summary>
        private static string[] Listed(CadDocument document)
        {
            DxfDBDictionary root = document.Database.NamedObjectsDictionary;

            if (!root.Contains("AEC_ISOLATED_OBJECTS"))
            {
                return null;
            }

            var held = (DxfDBDictionary)root["AEC_ISOLATED_OBJECTS"];
            string handle = held["AEC_ISOLATED_OBJECTS_XRECORD"].Handle;
            Rec record = Objects(document).Single(r => r.Handle == handle);
            Assert.Equal("XRECORD", record.Type);
            return record.Groups.Where(g => g.Key == 340).Select(g => g.Value).ToArray();
        }

        /// <summary>The groups of the record of the list, its subclass marker and its 280 and 70 where it has them, as codes and values.</summary>
        private static string[] ListRecord(CadDocument document)
        {
            string handle = ((DxfDBDictionary)document.Database.NamedObjectsDictionary["AEC_ISOLATED_OBJECTS"])[
                "AEC_ISOLATED_OBJECTS_XRECORD"
            ].Handle;
            return Objects(document)
                .Single(r => r.Handle == handle)
                .Groups.Where(g => g.Key == 100 || g.Key == 280 || g.Key == 70)
                .Select(g => g.Key + "=" + g.Value)
                .ToArray();
        }

        private static Rec RecordOf(CadDocument document, string key)
        {
            string handle = document.Database.NamedObjectsDictionary[key].Handle;
            return Objects(document).Single(r => r.Handle == handle);
        }

        private static string[] Marks(DxfEntity entity) =>
            (entity.GetXDataForApplication(Mark) ?? new DxfTypedValue[0])
                .Select(v => v.TypeCode.ToString() + "=" + v.Value)
                .ToArray();

        [Fact]
        public void TheVariable_TakesNoughtAndOne_AndStartsAtNought()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "OBJECTISOLATIONMODE");

            Assert.Equal("Enter new value for OBJECTISOLATIONMODE <0>:", session.PromptText);
            Run(session, "1", "OBJECTISOLATIONMODE");
            Assert.Equal("Enter new value for OBJECTISOLATIONMODE <1>:", session.PromptText);
            Run(session, "2");
            Assert.Contains("Requires 0 or 1 only.", session.History);
        }

        [Fact]
        public void TheObjectsHiddenInModeNought_AreListedInTheDrawing_AndSavedShown()
        {
            CadDocument document = ThreeLines(out string[] lines);
            Assert.Null(Listed(document));

            Start(document, "ISOLATEOBJECTS", new[] { lines[0] }, Enter);

            // AutoCAD 2021: AEC_ISOLATED_OBJECTS, a dictionary holding the record that lists, by handle, what was hidden.
            Assert.Equal(new[] { lines[1], lines[2] }, Listed(document));
            Assert.False(Entity<DxfLine>(document, lines[1]).Visible);
            Assert.Empty(Marks(Entity<DxfLine>(document, lines[1])));
            Assert.Equal(new[] { "100=AcDbXrecord", "280=1" }, ListRecord(document));

            string path = Path.Combine(TestRun.Folder("isolation"), "shown.dxf");
            document.SaveAs(path);
            Assert.All(DxfDatabase.Open(path).ModelSpace.OfType<DxfLine>(), l => Assert.True(l.Visible));
        }

        [Fact]
        public void UnisolateEmptiesTheList_AndWritesTheRecordOfTheLastIsolation()
        {
            CadDocument document = ThreeLines(out string[] lines);
            Assert.False(document.Database.NamedObjectsDictionary.Contains("ACAD_OBJECTISOLATION"));

            // AutoCAD 2021: an isolation leaves 0 in the record, a hiding 1, then -1.
            Start(document, "ISOLATEOBJECTS", new[] { lines[0] }, Enter);
            Assert.False(document.Database.NamedObjectsDictionary.Contains("ACAD_OBJECTISOLATION"));
            Start(document, "UNISOLATEOBJECTS");

            Assert.Empty(Listed(document));
            Rec record = RecordOf(document, "ACAD_OBJECTISOLATION");
            Assert.Equal("XRECORD", record.Type);
            Assert.Equal(
                new[] { "280=1", "70=0", "70=-1" },
                record.Groups.Where(g => g.Key == 280 || g.Key == 70).Select(g => g.Key + "=" + g.Value)
            );
            Assert.Equal(new[] { "100=AcDbXrecord", "280=1", "70=-1" }, ListRecord(document));

            CadCommandSession hiding = Start(document, "HIDEOBJECTS", new[] { lines[0] }, Enter);
            Assert.Equal(new[] { lines[0] }, Listed(document));
            Run(hiding, "UNISOLATEOBJECTS");
            Assert.Equal(
                new[] { "280=1", "70=1", "70=-1" },
                RecordOf(document, "ACAD_OBJECTISOLATION")
                    .Groups.Where(g => g.Key == 280 || g.Key == 70)
                    .Select(g => g.Key + "=" + g.Value)
            );
            AssertReopens(document);
        }

        [Fact]
        public void InModeOne_AnObjectIsWrittenHidden_WithItsMark_AndUnisolateTakesBothOff()
        {
            CadDocument document = ThreeLines(out string[] lines);
            CadCommandSession session = Start(document, "OBJECTISOLATIONMODE", "1");
            Run(session, "HIDEOBJECTS", new[] { lines[1] }, Enter);

            // AutoCAD 2021: group 60 of 1 and AEC_ISOLATEDOBJECTS_XDATA with 1070 100 and 1070 1.
            DxfLine hidden = Entity<DxfLine>(document, lines[1]);
            Assert.False(hidden.Visible);
            Assert.Equal(new[] { "1001=AEC_ISOLATEDOBJECTS_XDATA", "1070=100", "1070=1" }, Marks(hidden));
            Assert.Equal(new[] { lines[1] }, Listed(document));

            DxfDatabase saved = DxfDatabase.Parse(document.Database.ToText());
            DxfLine[] savedLines = saved.ModelSpace.OfType<DxfLine>().ToArray();
            Assert.True(savedLines[0].Visible);
            Assert.False(savedLines[1].Visible);
            Assert.True(savedLines[2].Visible);

            Run(session, "UNISOLATEOBJECTS");
            Assert.Contains("1 object(s) unisolated.", session.History);
            Assert.True(Entity<DxfLine>(document, lines[1]).Visible);
            Assert.Empty(Marks(Entity<DxfLine>(document, lines[1])));
            Assert.Empty(Listed(document));
        }

        [Fact]
        public void WhatModeOneSavedHidden_IsShownAgainByUnisolateOfTheReopenedDrawing()
        {
            CadDocument document = ThreeLines(out string[] lines);
            CadCommandSession mode = Start(document, "OBJECTISOLATIONMODE", "1");
            Run(mode, "ISOLATEOBJECTS", new[] { lines[0] }, Enter);

            CadDocument again = CadDocument.From(DxfDatabase.Parse(document.Database.ToText()), "again.dxf");
            Assert.False(Entity<DxfLine>(again, lines[1]).Visible);
            Assert.False(Entity<DxfLine>(again, lines[2]).Visible);

            CadCommandSession session = Start(again, "UNISOLATEOBJECTS");
            Assert.Contains("2 object(s) unisolated.", session.History);
            Assert.All(All<DxfLine>(again.ModelSpace), l => Assert.True(l.Visible));
        }

        [Fact]
        public void AHidingTakenBackByUndo_TakesItsListBack()
        {
            CadDocument document = ThreeLines(out string[] lines);
            Start(document, "HIDEOBJECTS", new[] { lines[0] }, Enter);
            Assert.Equal(new[] { lines[0] }, Listed(document));

            Assert.True(document.Undo());
            Assert.True(Entity<DxfLine>(document, lines[0]).Visible);
            Assert.Null(Listed(document));
            CadCommandSession session = Start(document, "UNISOLATEOBJECTS");
            Assert.Contains("No objects to unisolate.", session.History);
        }
    }
}
