using System;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// User coordinate systems (C7): the conversions, the UCS command and its options, points typed in a UCS, the grid, ortho
    /// and polar along it, the commands that measure and make objects in it, and the systems saved as AutoCAD saves them.
    /// </summary>
    public class UcsTests
    {
        private static readonly double Cos30 = Math.Cos(Math.PI / 6.0);

        /// <summary>Saves a document into a folder of its own under %TEMP%\gk\drawing\ucs-tests and reads the file back.</summary>
        private static DxfDatabase Saved(CadDocument document)
        {
            string folder = Path.Combine(
                Path.GetTempPath(),
                "gk",
                "drawing",
                "ucs-tests",
                Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(folder);

            try
            {
                string path = Path.Combine(folder, "saved.dxf");
                document.SaveAs(path);
                return DxfDatabase.Parse(File.ReadAllText(path));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Fact]
        public void AUcsTurned30DegreesAt100And50_TakesPointsToTheWorldAndBack()
        {
            CadUcs ucs = CadUcs.InPlane(P(100, 50), Math.PI / 6.0);

            Near(P(100 + 10 * Cos30, 55), ucs.ToWorld(P(10, 0)));
            Near(P(5, 10 * Cos30), ucs.ToUcs(P(100, 60)));
            Near(P(-37.5, 12.25), ucs.ToUcs(ucs.ToWorld(P(-37.5, 12.25))));
            Near(new GeoPoint3(5, 10 * Cos30, 7), ucs.ToUcs(new GeoPoint3(100, 60, 7)));
            Assert.Equal(Math.PI / 3.0, ucs.AngleToWorld(Math.PI / 6.0), 12);
            Assert.Equal(0.0, ucs.AngleToUcs(Math.PI / 6.0), 12);
            Assert.True(ucs.IsPlanar);
            Assert.False(ucs.IsWorld);
            Assert.True(CadUcs.World.IsWorld);

            // A system whose Y axis stands up out of the plan is not one the view from above can work in.
            Assert.False(new CadUcs(GeoPoint3.Origin, GeoVector3.XAxis, GeoVector3.ZAxis).IsPlanar);
            Assert.Throws<ArgumentException>(() =>
                new CadUcs(GeoPoint3.Origin, GeoVector3.XAxis, new GeoVector3(2, 0, 0))
            );
        }

        [Fact]
        public void ALineTypedAt100Degrees0_InAUcsTurnedAQuarter_IsUprightInTheWorld()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "UCS", "100,50", Enter, "UCS", "Z", "90");
            Run(session, "LINE", "0,0", "@100<0", Enter, "LINE", "100,0", "-20,0", Enter);

            var lines = All<DxfLine>(document.ModelSpace);
            Assert.Equal(2, lines.Count);
            Near(new GeoPoint3(100, 50, 0), lines[0].StartPoint);
            Near(new GeoPoint3(100, 150, 0), lines[0].EndPoint);
            Near(new GeoPoint3(100, 150, 0), lines[1].StartPoint);
            Near(new GeoPoint3(100, 30, 0), lines[1].EndPoint);
        }

        [Fact]
        public void APointTypedAfterAStar_IsInWorldCoordinates_AndIdSaysThePointInTheUcs()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "UCS", "100,50", Enter, "UCS", "Z", "90");
            Run(session, "LINE", "*0,0", "@*5,0", Enter);

            DxfLine line = All<DxfLine>(document.ModelSpace).Single();
            Near(new GeoPoint3(0, 0, 0), line.StartPoint);
            Near(new GeoPoint3(5, 0, 0), line.EndPoint);

            // The world point (100, 60) is 10 along the X axis of the UCS, which points up the world's Y.
            Run(session, "ID", P(100, 60));
            Assert.Equal("X = 10.0000     Y = 0.0000     Z = 0.0000", session.History.Last());
            Run(session, "DIST", P(100, 50), P(100, 60));
            Assert.Contains(session.History, l =>
                l.StartsWith("Delta X = 10.0000,  Delta Y = 0.0000", StringComparison.Ordinal)
            );
        }

        [Fact]
        public void APointTakenAsADisplacement_IsMeasuredAlongTheAxesOfTheUcs()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "UCS", "*100,0", Enter, "UCS", "Z", "90", "LINE", "*0,0", "*10,0", Enter);
            string handle = All<DxfLine>(document.ModelSpace).Single().Handle;

            // 10,0 typed and Enter: 10 along the X axis of the UCS, which points up the world's Y.
            Run(session, "MOVE", new[] { handle }, Enter, "10,0", Enter);
            DxfLine line = Entity<DxfLine>(document, handle);
            Near(new GeoPoint3(0, 10, 0), line.StartPoint);
            Near(new GeoPoint3(10, 10, 0), line.EndPoint);
        }

        [Fact]
        public void OrthoGridSnapAndPolar_FollowTheAxesOfTheUcs()
        {
            CadDocument blank = Blank();
            PickIndex index = blank.GetPickIndex(blank.ModelSpace);
            CadUcs ucs = CadUcs.InPlane(P(0, 0), Math.PI / 6.0);
            var snap = new ObjectSnap(Tight)
            {
                IsOn = false,
                Ortho = true,
                Ucs = ucs,
            };

            CadSnapResult held = snap.Snap(index, ucs.ToWorld(P(100, 10)), 1.0, ucs.ToWorld(P(0, 0)));
            Assert.Equal(CadSnapKind.Ortho, held.Kind);
            Near(ucs.ToWorld(P(100, 0)), held.Point);

            snap.Ortho = false;
            snap.GridSnap = true;
            CadSnapResult gridded = snap.Snap(index, ucs.ToWorld(P(12, 18)), 1.0);
            Assert.Equal(CadSnapKind.Grid, gridded.Kind);
            Near(ucs.ToWorld(P(10, 20)), gridded.Point);

            snap.GridSnap = false;
            snap.Polar = true;
            snap.PolarAngle = Math.PI / 4.0;
            double off = 44.0 * Math.PI / 180.0;
            CadSnapResult tracked = snap.Snap(
                index,
                ucs.ToWorld(P(100 * Math.Cos(off), 100 * Math.Sin(off))),
                5.0,
                ucs.ToWorld(P(0, 0))
            );
            double along = 100 * Math.Cos(Math.PI / 180.0);
            Assert.Equal(CadSnapKind.Polar, tracked.Kind);
            Near(ucs.ToWorld(P(along * Math.Cos(Math.PI / 4.0), along * Math.Sin(Math.PI / 4.0))), tracked.Point);
        }

        [Fact]
        public void UcsPreviousRestoreAndDelete_GoBackThroughTheSystems()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            CadUcsManager systems = document.Ucs;

            Run(session, "UCS", "Z", "30", "UCS", "NA", "S", "Tilted30");
            Assert.Equal("Tilted30", systems.Current.Name);
            Assert.Equal(Math.PI / 6.0, systems.Current.Angle, 12);

            Run(session, "UCS", Enter);
            Assert.True(systems.Current.IsWorld);
            Run(session, "UCS", "P");
            Assert.Equal("Tilted30", systems.Current.Name);

            Run(session, "UCS", "W", "UCS", "NA", "R", "tilted30");
            Assert.Equal("Tilted30", systems.Current.Name);
            Run(session, "UCS", "NA", "R", "Nothing");
            Assert.Equal("Cannot find UCS Nothing.", session.History.Last());

            Run(session, "UCS", "NA", "D", "T*");
            Assert.Equal("1 UCS deleted.", session.History.Last());
            Assert.Empty(systems.Named);
            Assert.Null(systems.Current.Name);
            Assert.Equal(Math.PI / 6.0, systems.Current.Angle, 12);

            for (int i = 0; i < 12; i++)
            {
                Run(session, "UCS", "P");
            }

            Assert.Equal("No previous UCS saved.", session.History.Last());
            Assert.True(systems.Current.IsWorld);
        }

        [Fact]
        public void UcsObject_AlignsToALineFromTheEndNearerThePick_AndToACircleThroughThePick()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);

            // The line A2 from (0, 0) to (0, 50), picked near its top: the origin at the top, the X axis down the line.
            Run(session, "UCS", "OB", P(0, 45));
            Near(new GeoPoint3(0, 50, 0), document.Ucs.Current.Origin);
            Assert.Equal(-Math.PI / 2.0, document.Ucs.Current.Angle, 12);

            // The circle A3 of radius 10 about (200, 0), picked at its top.
            Run(session, "UCS", "OB", P(200, 10));
            Near(new GeoPoint3(200, 0, 0), document.Ucs.Current.Origin);
            Assert.Equal(Math.PI / 2.0, document.Ucs.Current.Angle, 12);

            // The arc A4 of radius 20 about (300, 0) from 0 to 90 degrees, picked near its end at 90.
            Run(session, "UCS", "OB", P(301, 20));
            Assert.Equal(Math.PI / 2.0, document.Ucs.Current.Angle, 12);
        }

        [Fact]
        public void ALinearDimensionInAUcsTurned30Degrees_IsARotatedDimensionOf30Degrees()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "UCS", "Z", "30");
            CadUcs ucs = document.Ucs.Current;
            Run(session, "DIMLINEAR", ucs.ToWorld(P(0, 0)), ucs.ToWorld(P(100, 20)), ucs.ToWorld(P(50, 60)));

            DxfRotatedDimension dimension = All<DxfRotatedDimension>(document.ModelSpace).Single();
            Assert.Equal(Math.PI / 6.0, dimension.Rotation, 9);
            Assert.Contains(session.History, l => l.StartsWith("Dimension text = 100", StringComparison.Ordinal));
        }

        [Fact]
        public void TextAndARectangle_TakeTheTurnOfTheUcs()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "UCS", "Z", "30");
            Run(session, "TEXT", "0,0", "2.5", "0", "ABC", Enter);
            Run(session, "RECTANG", "0,0", "100,50");

            Assert.Equal(Math.PI / 6.0, All<DxfDBText>(document.ModelSpace).Single().Rotation, 9);
            DxfPolyline rectangle = All<DxfPolyline>(document.ModelSpace).Single();
            Near(document.Ucs.Current.ToWorld(P(100, 0)), rectangle.GetPoint2dAt(1));
            Near(document.Ucs.Current.ToWorld(P(100, 50)), rectangle.GetPoint2dAt(2));
        }

        [Fact]
        public void UndoOfACommandDrawnInAUcs_LeavesTheUcsAsItWas()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "UCS", "Z", "30");
            Assert.False(document.CanUndo);
            Assert.True(document.IsModified);

            Run(session, "LINE", "0,0", "@100<0", Enter);
            Near(new GeoPoint3(100 * Cos30, 50, 0), All<DxfLine>(document.ModelSpace).Single().EndPoint);
            Run(session, "U");

            Assert.Empty(All<DxfLine>(document.ModelSpace));
            Assert.Equal(Math.PI / 6.0, document.Ucs.Current.Angle, 12);
            Assert.Equal(Math.PI / 6.0, session.Ucs.Angle, 12);
        }

        [Fact]
        public void ANamedUcs_IsSavedAsAutoCadWritesIt_AndReadBack()
        {
            string folder = Path.Combine(
                Path.GetTempPath(),
                "gk",
                "drawing",
                "ucs-tests",
                Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(folder);

            try
            {
                string path = Path.Combine(folder, "slab.dxf");
                CadDocument document = Blank();
                CadCommandSession session = Session(document);
                Run(session, "UCS", "100,50", Enter, "UCS", "Z", "30", "UCS", "NA", "S", "Slab");
                document.SaveAs(path);
                Assert.False(document.IsModified);

                string text = string.Join("\n", File.ReadAllLines(path).Select(l => l.Trim()));
                Assert.Contains(
                    "AcDbSymbolTableRecord\n100\nAcDbUCSTableRecord\n2\nSlab\n70\n0\n10\n100\n20\n50\n30\n0\n11\n0.866025403784",
                    text,
                    StringComparison.Ordinal
                );
                Assert.Contains("\n79\n0\n146\n0\n0\n", text, StringComparison.Ordinal);
                Assert.Contains("$UCSNAME\n2\nSlab\n", text, StringComparison.Ordinal);

                DxfDatabase saved = DxfDatabase.Open(path);
                Assert.Equal("Slab", saved.Ucsname);
                Near(new GeoPoint3(100, 50, 0), saved.Ucsorg);
                Near(new GeoPoint3(Cos30, 0.5, 0), GeoPoint3.Origin + saved.UcsTable["Slab"].XAxis);
                Near(new GeoPoint3(-0.5, Cos30, 0), GeoPoint3.Origin + saved.Ucsydir);

                CadDocument again = CadDocument.Open(path);
                Assert.Equal("Slab", again.Ucs.Current.Name);
                Assert.Equal(Math.PI / 6.0, again.Ucs.Current.Angle, 12);
                Assert.Single(again.Ucs.Named);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Fact]
        public void ModelSpaceAndPaperSpace_EachHaveTheirOwnUcs()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            document.CurrentSpace = document.Spaces[1];
            Run(session, "UCS", "Z", "45");
            Assert.Equal(Math.PI / 4.0, document.Ucs.Paper.Angle, 12);
            Assert.True(document.Ucs.Model.IsWorld);

            document.CurrentSpace = document.ModelSpace;
            Assert.True(session.Ucs.IsWorld);
            Run(session, "UCS", "Z", "-30");
            DxfDatabase saved = Saved(document);

            Near(new GeoPoint3(Math.Cos(Math.PI / 4.0), Math.Sin(Math.PI / 4.0), 0), GeoPoint3.Origin + saved.Pucsxdir);
            Near(new GeoPoint3(Cos30, -0.5, 0), GeoPoint3.Origin + saved.Ucsxdir);
        }

        [Fact]
        public void AUcsOfTheFileOutOfThePlan_IsListedAndKept_NotMadeCurrent()
        {
            var database = new DxfDatabase("AC1027");
            database.UcsTable.Add(
                new DxfUcsTableRecord
                {
                    Name = "Front",
                    XAxis = GeoVector3.XAxis,
                    YAxis = GeoVector3.ZAxis,
                }
            );
            database.Ucs = new GeoCoordinateSystem3(GeoPoint3.Origin, GeoVector3.XAxis, GeoVector3.ZAxis);
            database.Ucsname = "Front";
            string before = database.ToText();
            CadDocument document = CadDocument.From(database, "front.dxf");
            CadCommandSession session = Session(document);

            Assert.False(document.Ucs.Current.IsPlanar);
            Assert.True(session.Ucs.IsWorld);

            Run(session, "UCS", "NA", "R", "Front");
            Assert.Contains("not parallel to the view", session.History.Last(), StringComparison.Ordinal);
            Run(session, "UCS", "?", Enter);
            Assert.Contains(session.History, l => l.StartsWith("\"Front\"  (not parallel", StringComparison.Ordinal));
            Assert.False(document.IsModified);

            DxfDatabase saved = Saved(document);
            Assert.Equal(before, document.Database.ToText());
            Assert.Equal("Front", saved.Ucsname);
        }
    }
}
