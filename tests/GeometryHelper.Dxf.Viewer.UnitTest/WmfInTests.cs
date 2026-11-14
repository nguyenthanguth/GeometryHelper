using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// WMFIN, as AutoCAD 2021's console runs it (probed through standard input): the metafile read of its own, made block WMF0
    /// of 2D polylines and TEXT, fitted into half the view with its aspect kept, placed as INSERT places a block of inches.
    /// Every file is built in code (<see cref="WmfBytes"/>); no view is set, so the half view is the console's, 8.82739 by 5.
    /// </summary>
    public class WmfInTests : IDisposable
    {
        private const double BoxWidth = 8.82739;
        private const double BoxHeight = 5.0;

        private readonly string _folder = Path.Combine(
            Path.GetTempPath(),
            "gk",
            "wmfin-" + Guid.NewGuid().ToString("N")
        );

        public WmfInTests()
        {
            Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_folder, true);
            }
            catch (IOException)
            {
                // A file still held is left for the system to clear.
            }
        }

        private static CadDocument Millimetres()
        {
            CadDocument document = Blank();
            document.Database.Insunits = DxfUnits.Millimeters;
            return document;
        }

        /// <summary>Runs WMFIN on a file, answering the insertion point (0, 0) and Enter at scale, Y scale and rotation.</summary>
        private CadCommandSession Imported(CadDocument document, WmfBytes file, bool placeable = true)
        {
            CadCommandSession session = Session(document);
            Run(session, "FILEDIA", "0");
            Run(session, "WMFIN", file.Save(_folder, "a.wmf", placeable));
            Run(session, P(0, 0), Enter, Enter, Enter);
            Assert.False(session.IsRunning);
            return session;
        }

        private static List<DxfPolyline2d> Polylines(DxfBlockTableRecord block) =>
            block.OfType<DxfPolyline2d>().ToList();

        private static List<GeoPoint3> Points(DxfPolyline2d polyline) => polyline.Select(v => v.Position).ToList();

        private static void Close(double expected, double actual) =>
            Assert.InRange(actual, expected - 1E-9, expected + 1E-9);

        [Fact]
        public void WmfinAndItsVariables_AreRegistered()
        {
            CadCommandRegistry registry = CadCommandRegistry.CreateDefault();

            Assert.NotNull(registry.Find("WMFIN"));
            Assert.NotNull(registry.FindVariable("WMFBKGND"));
            Assert.NotNull(registry.FindVariable("WMFFOREGND"));
        }

        [Fact]
        public void WithFiledia0_ItAsksTheNameWithoutExtension_AndGivesBlockWmf0AnInsertOfScale25Point4()
        {
            CadDocument document = Millimetres();
            string path = new WmfBytes()
                .WindowExt(1000, 1000)
                .MoveTo(100, 100)
                .LineTo(900, 900)
                .Save(_folder, "logo.wmf");
            CadCommandSession session = Session(document);
            Run(session, "FILEDIA", "0");

            Run(session, "WMFIN");
            Assert.Equal("Enter name of WMF file to import (no extension):", session.PromptText);
            Run(session, Path.Combine(_folder, "logo"));
            Assert.Contains("Units: Unitless   Conversion:   25.4000", session.History);
            Assert.Equal(
                "Specify insertion point or [Basepoint/Scale/X/Y/Z/Rotate/Explode/REpeat]:",
                session.PromptText
            );
            Run(session, P(10, 20));
            Assert.Equal("Enter X scale factor, specify opposite corner, or [Corner/XYZ] <1>:", session.PromptText);
            Run(session, Enter);
            Assert.Equal("Enter Y scale factor <use X scale factor>:", session.PromptText);
            Run(session, Enter);
            Assert.Equal("Specify rotation angle <0>:", session.PromptText);
            Run(session, Enter);

            Assert.False(session.IsRunning);
            DxfBlockReference insert = Assert.Single(All<DxfBlockReference>(document.ModelSpace));
            Assert.Equal("WMF0", insert.Name);
            Near(new GeoPoint3(10, 20, 0), insert.Position);
            Close(25.4, insert.ScaleFactors.X);
            Close(25.4, insert.ScaleFactors.Y);
            Assert.Equal(DxfUnits.Inches, document.Database.BlockTable["WMF0"].Units);
            Assert.True(File.Exists(path));
        }

        [Fact]
        public void TheFactorFollowsTheDrawingsUnit_AsItDoesForAnyBlockOfInches()
        {
            var file = new WmfBytes().WindowExt(1000, 1000).MoveTo(0, 0).LineTo(1000, 1000);
            string text = null;

            foreach (
                (DxfUnits units, double factor, string said) in new[]
                {
                    (DxfUnits.Undefined, 1.0, "    1.0000"),
                    (DxfUnits.Inches, 1.0, "    1.0000"),
                    (DxfUnits.Meters, 0.0254, "    0.0254"),
                    (DxfUnits.Centimeters, 2.54, "    2.5400"),
                }
            )
            {
                CadDocument document = Blank();
                document.Database.Insunits = units;
                CadCommandSession session = Imported(document, file);
                DxfBlockReference insert = Assert.Single(All<DxfBlockReference>(document.ModelSpace));
                Close(factor, insert.ScaleFactors.X);
                text = session.History.Single(h => h.StartsWith("Units: ", StringComparison.Ordinal));
                Assert.Equal("Units: Unitless   Conversion:" + said, text);
            }
        }

        [Fact]
        public void ANameThatIsNoFile_IsCannotFindTheSpecifiedMetafile()
        {
            CadDocument document = Millimetres();
            CadCommandSession session = Session(document);
            Run(session, "FILEDIA", "0");

            Run(session, "WMFIN", Path.Combine(_folder, "missing.wmf"));

            Assert.False(session.IsRunning);
            Assert.Equal("Cannot find the specified metafile", session.History.Last());
            Assert.Empty(All<DxfBlockReference>(document.ModelSpace));
            Assert.False(document.Database.BlockTable.Has("WMF0"));
        }

        [Fact]
        public void EnterAtTheName_Leaves()
        {
            CadDocument document = Millimetres();
            CadCommandSession session = Session(document);
            Run(session, "FILEDIA", "0");

            Run(session, "WMFIN", Enter);

            Assert.False(session.IsRunning);
            Assert.False(document.Database.BlockTable.Has("WMF0"));
        }

        [Fact]
        public void AFileThatIsNoMetafile_IsRefusedAndNothingIsMade()
        {
            CadDocument document = Millimetres();
            string junk = Path.Combine(_folder, "junk.wmf");
            File.WriteAllBytes(
                junk,
                new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 }
            );
            CadCommandSession session = Session(document);
            Run(session, "FILEDIA", "0");

            Run(session, "WMFIN", junk);

            Assert.False(session.IsRunning);
            Assert.StartsWith("The file is no Windows Metafile", session.History.Last());
            Assert.False(document.Database.BlockTable.Has("WMF0"));
        }

        [Fact]
        public void ALine_IsA2DPolylineOfTheWidthOfThePenAndTheNearestIndexOfItsColour()
        {
            CadDocument document = Millimetres();
            var file = new WmfBytes()
                .WindowOrg(0, 0)
                .WindowExt(1000, 1000)
                .Pen(0, 10, 255, 0, 0)
                .Select(0)
                .MoveTo(100, 100)
                .LineTo(900, 900);

            Imported(document, file);

            DxfPolyline2d line = Assert.Single(Polylines(document.Database.BlockTable["WMF0"]));
            Assert.False(line.Closed);
            List<GeoPoint3> points = Points(line);
            Assert.Equal(2, points.Count);
            Near(new GeoPoint3(0.5, -0.5, 0), points[0]);
            Near(new GeoPoint3(4.5, -4.5, 0), points[1]);
            Close(0.05, line.ConstantWidth);
            Assert.Equal(1, line.Color.ColorIndex);
        }

        [Fact]
        public void EachLineTo_IsATwoVertexPolylineOfItsOwn()
        {
            CadDocument document = Millimetres();
            var file = new WmfBytes()
                .WindowExt(1000, 1000)
                .Pen(0, 0, 0, 255, 0)
                .Select(0)
                .MoveTo(100, 100)
                .LineTo(100, 900)
                .LineTo(900, 900)
                .LineTo(900, 100);

            Imported(document, file);

            List<DxfPolyline2d> lines = Polylines(document.Database.BlockTable["WMF0"]);
            Assert.Equal(3, lines.Count);
            Assert.All(lines, l => Assert.Equal(2, l.Count()));
            Assert.All(lines, l => Assert.Equal(3, l.Color.ColorIndex));
            Close(0.0, lines[0].ConstantWidth);
            Near(new GeoPoint3(0.5, -4.5, 0), Points(lines[0])[1]);
            Near(new GeoPoint3(0.5, -4.5, 0), Points(lines[1])[0]);
        }

        [Fact]
        public void ThePictureIsFittedIntoHalfTheViewWithItsAspectKept()
        {
            CadDocument document = Millimetres();
            var wide = new WmfBytes()
                .WindowExt(2000, 1000)
                .Pen(0, 10, 255, 0, 0)
                .Select(0)
                .MoveTo(0, 0)
                .LineTo(2000, 1000);

            Imported(document, wide);

            DxfPolyline2d line = Assert.Single(Polylines(document.Database.BlockTable["WMF0"]));
            Close(BoxWidth, Points(line)[1].X);
            Close(-4.4137, Math.Round(Points(line)[1].Y, 4));
            Close(10.0 * BoxWidth / 2000.0, line.ConstantWidth);
        }

        [Fact]
        public void ATallPicture_IsFittedByItsHeight()
        {
            CadDocument document = Millimetres();
            var tall = new WmfBytes()
                .WindowExt(1000, 2000)
                .Pen(0, 0, 255, 0, 0)
                .Select(0)
                .MoveTo(0, 0)
                .LineTo(1000, 2000);

            Imported(document, tall);

            DxfPolyline2d line = Assert.Single(Polylines(document.Database.BlockTable["WMF0"]));
            Near(new GeoPoint3(2.5, -5.0, 0), Points(line)[1]);
        }

        [Fact]
        public void TheAspectIsTheWindowExtents_NotThePlaceableBox()
        {
            CadDocument document = Millimetres();
            var file = new WmfBytes()
                .WindowExt(1000, 1000)
                .Pen(0, 10, 255, 0, 0)
                .Select(0)
                .MoveTo(0, 0)
                .LineTo(1000, 1000);

            Imported(document, file, true);
            CadDocument other = Millimetres();
            string path = file.Save(_folder, "wide.wmf", true, 2000, 1000);
            CadCommandSession session = Session(other);
            Run(session, "FILEDIA", "0");
            Run(session, "WMFIN", path, P(0, 0), Enter, Enter, Enter);

            DxfPolyline2d line = Assert.Single(Polylines(other.Database.BlockTable["WMF0"]));
            Near(new GeoPoint3(5.0, -5.0, 0), Points(line)[1]);
        }

        [Fact]
        public void WithNoWindowExtents_TheUnitIsOne_AndAUnitFillsTheBox()
        {
            CadDocument document = Millimetres();
            var file = new WmfBytes().Pen(0, 10, 255, 0, 0).Select(0).MoveTo(0, 0).LineTo(500, 500);

            Imported(document, file, false);

            DxfPolyline2d line = Assert.Single(Polylines(document.Database.BlockTable["WMF0"]));
            Near(new GeoPoint3(2500.0, -2500.0, 0), Points(line)[1]);
            Close(50.0, line.ConstantWidth);
        }

        [Fact]
        public void TheWindowOriginIsNotTaken_AsAutoCadsMetafileReaderDoesNotTakeIt()
        {
            CadDocument document = Millimetres();
            var file = new WmfBytes()
                .WindowOrg(500, 500)
                .WindowExt(1000, 1000)
                .Pen(0, 10, 255, 0, 0)
                .Select(0)
                .MoveTo(600, 600)
                .LineTo(900, 900);

            Imported(document, file);

            DxfPolyline2d line = Assert.Single(Polylines(document.Database.BlockTable["WMF0"]));
            Near(new GeoPoint3(3.0, -3.0, 0), Points(line)[0]);
            Near(new GeoPoint3(4.5, -4.5, 0), Points(line)[1]);
        }

        [Fact]
        public void ARectangleAndAPolygon_AreClosedPolylinesInTheColourOfTheBrush_AndOfTheWidthOfThePen()
        {
            CadDocument document = Millimetres();
            var file = new WmfBytes()
                .WindowExt(1000, 1000)
                .Pen(0, 10, 255, 0, 0)
                .Brush(0, 0, 0, 255)
                .Pen(5, 0, 0, 0, 0)
                .Select(0)
                .Select(1)
                .Rectangle(200, 300, 600, 400)
                .Select(2)
                .Polygon(500, 100, 600, 200, 400, 200);

            Imported(document, file);

            List<DxfPolyline2d> shapes = Polylines(document.Database.BlockTable["WMF0"]);
            Assert.Equal(2, shapes.Count);
            Assert.True(shapes[0].Closed);
            Assert.Equal(5, shapes[0].Color.ColorIndex);
            Close(0.05, shapes[0].ConstantWidth);
            List<GeoPoint3> corners = Points(shapes[0]);
            Assert.Equal(4, corners.Count);
            Near(new GeoPoint3(1.0, -1.5, 0), corners[0]);
            Near(new GeoPoint3(3.0, -1.5, 0), corners[1]);
            Near(new GeoPoint3(3.0, -2.0, 0), corners[2]);
            Near(new GeoPoint3(1.0, -2.0, 0), corners[3]);
            Assert.True(shapes[1].Closed);
            Assert.Equal(5, shapes[1].Color.ColorIndex);
            Close(0.0, shapes[1].ConstantWidth);
            Assert.Equal(3, shapes[1].Count());
        }

        [Fact]
        public void AHollowBrush_GivesTheColourOfThePen()
        {
            CadDocument document = Millimetres();
            var file = new WmfBytes()
                .WindowExt(1000, 1000)
                .Pen(0, 10, 0, 255, 0)
                .Brush(1, 0, 0, 0)
                .Select(0)
                .Select(1)
                .Rectangle(100, 100, 300, 300);

            Imported(document, file);

            DxfPolyline2d shape = Assert.Single(Polylines(document.Database.BlockTable["WMF0"]));
            Assert.True(shape.Closed);
            Assert.Equal(3, shape.Color.ColorIndex);
        }

        [Fact]
        public void AnOpenPolylineRecord_IsOneOpenPolyline_AndAPolyPolygonOneClosedPolylineEach()
        {
            CadDocument document = Millimetres();
            var file = new WmfBytes()
                .WindowExt(1000, 1000)
                .Pen(0, 0, 0, 0, 255)
                .Select(0)
                .Polyline(0, 0, 300, 100, 500, 400, 600, 300)
                .PolyPolygon(new[] { 3, 4 }, 0, 0, 100, 0, 100, 100, 200, 200, 300, 200, 300, 300, 200, 300);

            Imported(document, file);

            List<DxfPolyline2d> shapes = Polylines(document.Database.BlockTable["WMF0"]);
            Assert.Equal(3, shapes.Count);
            Assert.False(shapes[0].Closed);
            Assert.Equal(4, shapes[0].Count());
            Assert.True(shapes[1].Closed);
            Assert.Equal(3, shapes[1].Count());
            Assert.True(shapes[2].Closed);
            Assert.Equal(4, shapes[2].Count());
            Assert.Equal(5, shapes[0].Color.ColorIndex);

            // The polygons take the brush, which a metafile that selects none leaves white: the foreground, 7.
            Assert.Equal(7, shapes[1].Color.ColorIndex);
            Assert.Equal(7, shapes[2].Color.ColorIndex);
        }

        [Fact]
        public void AnEllipse_IsAnOpenPolylineOf101PointsInThePenColour_FromTheRightmostPointCounterClockwise()
        {
            CadDocument document = Millimetres();
            var file = new WmfBytes()
                .WindowExt(1000, 1000)
                .Pen(0, 10, 255, 0, 0)
                .Brush(0, 0, 0, 255)
                .Select(0)
                .Select(1)
                .Ellipse(100, 200, 300, 600);

            Imported(document, file);

            DxfPolyline2d ring = Assert.Single(Polylines(document.Database.BlockTable["WMF0"]));
            List<GeoPoint3> points = Points(ring);
            Assert.False(ring.Closed);
            Assert.Equal(101, points.Count);
            Assert.Equal(1, ring.Color.ColorIndex);
            Near(new GeoPoint3(1.5, -2.0, 0), points[0]);
            Near(new GeoPoint3(1.5, -2.0, 0), points[100]);

            // A quarter of the way round, counter-clockwise on the screen: the top of the box, y up.
            Near(new GeoPoint3(1.0, -1.0, 0), points[25]);
            Near(new GeoPoint3(0.5, -2.0, 0), points[50]);
            Near(new GeoPoint3(1.0, -3.0, 0), points[75]);
        }

        [Fact]
        public void AnArc_RunsCounterClockwiseFromItsStartToItsEnd_APieAndAChordCloseIt()
        {
            CadDocument document = Millimetres();
            var file = new WmfBytes()
                .WindowExt(1000, 1000)
                .Pen(0, 10, 255, 0, 0)
                .Brush(0, 0, 0, 255)
                .Select(0)
                .Select(1)
                .Arc(200, 200, 600, 600, 600, 400, 400, 200)
                .Pie(200, 200, 600, 600, 600, 400, 400, 200)
                .Chord(200, 200, 600, 600, 600, 400, 400, 200);

            Imported(document, file);

            List<DxfPolyline2d> shapes = Polylines(document.Database.BlockTable["WMF0"]);
            Assert.Equal(3, shapes.Count);
            Assert.False(shapes[0].Closed);
            Assert.Equal(1, shapes[0].Color.ColorIndex);
            List<GeoPoint3> arc = Points(shapes[0]);

            // From the rightmost point (600, 400) to the top of the circle (400, 200): a quarter turn, counter-clockwise on the screen.
            Near(new GeoPoint3(3.0, -2.0, 0), arc[0]);
            Near(new GeoPoint3(2.0, -1.0, 0), arc[arc.Count - 1]);
            Assert.InRange(arc.Count, 20, 60);

            Assert.True(shapes[1].Closed);
            Assert.Equal(5, shapes[1].Color.ColorIndex);
            Near(new GeoPoint3(2.0, -2.0, 0), Points(shapes[1])[0]);
            Assert.True(shapes[2].Closed);
            Assert.Equal(5, shapes[2].Color.ColorIndex);
            Near(arc[0], Points(shapes[2])[0]);
        }

        [Fact]
        public void Text_IsATextOfTheStyleWmfAndTheFaceAndANumber_ItsHeightTheCapsOfTheEm()
        {
            CadDocument document = Millimetres();
            var file = new WmfBytes()
                .WindowExt(1000, 1000)
                .Font(-80, 400, false, 0, "Arial")
                .Select(0)
                .TextOut(200, 600, "Hello");

            Imported(document, file);

            DxfDBText text = Assert.Single(document.Database.BlockTable["WMF0"].OfType<DxfDBText>());
            Assert.Equal("Hello", text.TextString);
            Assert.Equal("WMF-Arial0", text.TextStyleName);
            Close(0.288, text.Height);
            Close(0.0, text.Rotation);
            Assert.Equal(7, text.Color.ColorIndex);

            // The reference point is the top left of the cell: the baseline lies 0.9 of the em below it.
            Near(new GeoPoint3(1.0, -3.0 - 0.36, 0), text.Position);
            DxfTextStyleTableRecord style = document.Database.TextStyleTable["WMF-Arial0"];
            Assert.Equal("Arial", style.Font.TypeFace);
            Assert.False(style.Font.Bold);
            Assert.False(style.Font.Italic);
            Assert.EndsWith(".ttf", style.FileName);
        }

        [Fact]
        public void TheTextColourAndTheTurnOfTheFont_AreTheTextsOwn()
        {
            CadDocument document = Millimetres();
            var file = new WmfBytes()
                .WindowExt(1000, 1000)
                .Font(-80, 400, false, 900, "Arial")
                .Select(0)
                .SetTextColor(255, 0, 0)
                .TextOut(500, 500, "Up");

            Imported(document, file);

            DxfDBText text = Assert.Single(document.Database.BlockTable["WMF0"].OfType<DxfDBText>());
            Assert.Equal(1, text.Color.ColorIndex);
            Close(Math.PI / 2.0, text.Rotation);

            // Turned a quarter, the baseline lies to the right of the reference point, by the same 0.36.
            Close(2.5 + 0.36, text.Position.X);
            Close(-2.5, text.Position.Y);
        }

        [Fact]
        public void AFaceTakesANumberForEachLookItIsUsedIn_AndTheSameLookKeepsItsStyle()
        {
            CadDocument document = Millimetres();
            var file = new WmfBytes()
                .WindowExt(1000, 1000)
                .Font(-80, 700, true, 0, "Arial")
                .Font(-80, 400, false, 0, "Arial")
                .Font(-60, 400, false, 0, "Arial")
                .Font(-80, 400, false, 0, "Courier New")
                .Select(0)
                .TextOut(100, 100, "a")
                .Select(1)
                .TextOut(100, 200, "b")
                .Select(2)
                .TextOut(100, 300, "c")
                .Select(3)
                .TextOut(100, 400, "d");

            Imported(document, file);

            List<DxfDBText> texts = document.Database.BlockTable["WMF0"].OfType<DxfDBText>().ToList();
            Assert.Equal(
                new[] { "WMF-Arial0", "WMF-Arial1", "WMF-Arial1", "WMF-Courier New0" },
                texts.Select(t => t.TextStyleName).ToArray()
            );
            DxfTextStyleTableRecord bold = document.Database.TextStyleTable["WMF-Arial0"];
            Assert.True(bold.Font.Bold);
            Assert.True(bold.Font.Italic);
            Assert.Equal("Courier New", document.Database.TextStyleTable["WMF-Courier New0"].Font.TypeFace);
            Assert.Equal(0.0, bold.TextSize);
        }

        [Fact]
        public void ATextAlignedToTheCentreOrTheBaseline_IsJustifiedAtItsPoint()
        {
            CadDocument document = Millimetres();
            var file = new WmfBytes()
                .WindowExt(1000, 1000)
                .Font(-80, 400, false, 0, "Arial")
                .Select(0)
                .SetTextAlign(6 | 24)
                .TextOut(500, 500, "Mid");

            Imported(document, file);

            DxfDBText text = Assert.Single(document.Database.BlockTable["WMF0"].OfType<DxfDBText>());
            Assert.Equal(DxfAttachmentPoint.BaseCenter, text.Justify);
            Near(new GeoPoint3(2.5, -2.5, 0), text.AlignmentPoint);
        }

        [Fact]
        public void TheColourOfAPen_IsTheNearestIndex_BlackAndWhiteBeingTheForeground()
        {
            CadDocument document = Millimetres();
            var file = new WmfBytes().WindowExt(1000, 1000);
            var colours = new (byte, byte, byte, int)[]
            {
                (255, 0, 0, 1),
                (255, 255, 0, 2),
                (0, 255, 0, 3),
                (0, 0, 255, 5),
                (0, 255, 255, 4),
                (255, 0, 255, 6),
                (0, 0, 0, 7),
                (255, 255, 255, 7),
            };

            for (int i = 0; i < colours.Length; i++)
            {
                file.Pen(0, 0, colours[i].Item1, colours[i].Item2, colours[i].Item3)
                    .Select(i)
                    .MoveTo(0, i * 10)
                    .LineTo(100, i * 10);
            }

            Imported(document, file);

            Assert.Equal(
                colours.Select(c => c.Item4).ToArray(),
                Polylines(document.Database.BlockTable["WMF0"]).Select(p => (int)p.Color.ColorIndex).ToArray()
            );
        }

        [Fact]
        public void AnObjectDeleted_FreesItsPlace_AndANullPenDrawsNoLine()
        {
            CadDocument document = Millimetres();
            var file = new WmfBytes()
                .WindowExt(1000, 1000)
                .Pen(0, 0, 255, 0, 0)
                .Pen(5, 0, 0, 255, 0)
                .Select(1)
                .MoveTo(0, 0)
                .LineTo(100, 100)
                .Select(0)
                .Delete(1)
                .Pen(0, 0, 0, 0, 255)
                .Select(1)
                .MoveTo(0, 0)
                .LineTo(200, 200);

            Imported(document, file);

            List<DxfPolyline2d> lines = Polylines(document.Database.BlockTable["WMF0"]);
            Assert.Single(lines);
            Assert.Equal(5, lines[0].Color.ColorIndex);
        }

        [Fact]
        public void ASecondImport_IsBlockWmf1()
        {
            CadDocument document = Millimetres();
            var file = new WmfBytes().WindowExt(1000, 1000).MoveTo(0, 0).LineTo(100, 100);
            Imported(document, file);

            CadCommandSession session = Session(document);
            Run(session, "FILEDIA", "0");
            Run(session, "WMFIN", Path.Combine(_folder, "a.wmf"), P(50, 0), Enter, Enter, Enter);

            Assert.True(document.Database.BlockTable.Has("WMF1"));
            Assert.Equal(2, All<DxfBlockReference>(document.ModelSpace).Count);
        }

        [Fact]
        public void TheScaleAndTheRotationTyped_GoToTheInsertAfterTheFactorOfTheUnit()
        {
            CadDocument document = Millimetres();
            var file = new WmfBytes().WindowExt(1000, 1000).MoveTo(0, 0).LineTo(100, 100);
            CadCommandSession session = Session(document);
            Run(session, "FILEDIA", "0");

            Run(session, "WMFIN", file.Save(_folder, "a.wmf"), P(0, 0), "2", Enter, "90");

            DxfBlockReference insert = Assert.Single(All<DxfBlockReference>(document.ModelSpace));
            Close(50.8, insert.ScaleFactors.X);
            Close(50.8, insert.ScaleFactors.Y);
            Close(Math.PI / 2.0, insert.Rotation);
        }

        [Fact]
        public void TheExplodeOption_AsksYesOrNo_AndPlacesThePiecesInPlaceOfTheInsert()
        {
            CadDocument document = Millimetres();
            var file = new WmfBytes()
                .WindowExt(1000, 1000)
                .Pen(0, 10, 255, 0, 0)
                .Select(0)
                .MoveTo(0, 0)
                .LineTo(100, 100)
                .MoveTo(0, 0)
                .LineTo(100, 0);
            CadCommandSession session = Session(document);
            Run(session, "FILEDIA", "0");

            Run(session, "WMFIN", file.Save(_folder, "a.wmf"), "E");
            Assert.Equal("Explode block [Yes/No] <Yes>:", session.PromptText);
            Run(session, Enter);
            Assert.Equal(
                "Specify insertion point or [Basepoint/Scale/X/Y/Z/Rotate/Explode/REpeat]:",
                session.PromptText
            );
            Run(session, P(0, 0), Enter, Enter, Enter);

            Assert.False(session.IsRunning);
            Assert.Empty(All<DxfBlockReference>(document.ModelSpace));
            List<DxfPolyline2d> pieces = All<DxfPolyline2d>(document.ModelSpace);
            Assert.Equal(2, pieces.Count);
            Close(25.4 * 0.5, Points(pieces[0])[1].X);
        }

        [Fact]
        public void TheRepeatOption_AsksYesOrNo_AndAsksTheInsertionPointAgain()
        {
            CadDocument document = Millimetres();
            var file = new WmfBytes().WindowExt(1000, 1000).MoveTo(0, 0).LineTo(100, 100);
            CadCommandSession session = Session(document);
            Run(session, "FILEDIA", "0");

            Run(session, "WMFIN", file.Save(_folder, "a.wmf"), "RE");
            Assert.Equal("Repeat insertion of block [Yes/No] <Yes>:", session.PromptText);
            Run(session, Enter, P(0, 0), Enter, Enter, Enter);
            Assert.Equal(
                "Specify insertion point or [Basepoint/Scale/X/Y/Z/Rotate/Explode/REpeat]:",
                session.PromptText
            );
            Run(session, P(100, 0), Enter, Enter, Enter, Esc);

            Assert.False(session.IsRunning);
            List<DxfBlockReference> inserts = All<DxfBlockReference>(document.ModelSpace);
            Assert.Equal(2, inserts.Count);
            Assert.All(inserts, i => Assert.Equal("WMF0", i.Name));
            Assert.Single(document.Database.BlockTable, b => b.Name == "WMF0");
        }

        [Fact]
        public void TheDrawingWithTheBlockAndTheInsert_ReopensAsItIs()
        {
            CadDocument document = Millimetres();
            var file = new WmfBytes().WindowExt(1000, 1000).MoveTo(0, 0).LineTo(100, 100);
            Imported(document, file);

            Assert.True(document.Database.BlockTable.Has("WMF0"));
            Assert.NotEmpty(All<DxfBlockReference>(document.ModelSpace));
            AssertReopens(document);
        }

        [Fact]
        public void WithAViewOfTheCanvas_TheBoxIsHalfTheViewInItsOwnUnits()
        {
            CadDocument document = Millimetres();
            CadCommandSession session = Session(document);
            session.View = new CadView(942, 534);
            Run(session, "FILEDIA", "0");
            session.View.ZoomToHeight(100, 50, 100);
            var file = new WmfBytes()
                .WindowExt(1000, 1000)
                .Pen(0, 0, 255, 0, 0)
                .Select(0)
                .MoveTo(0, 0)
                .LineTo(1000, 1000);

            Run(session, "WMFIN", file.Save(_folder, "a.wmf"), P(0, 0), Enter, Enter, Enter);

            DxfPolyline2d line = Assert.Single(Polylines(document.Database.BlockTable["WMF0"]));
            DisplayBox seen = session.View.VisibleBox;
            double half = Math.Min((seen.MaxX - seen.MinX) / 2.0, (seen.MaxY - seen.MinY) / 2.0);
            Close(half, Points(line)[1].X);
            Close(-half, Points(line)[1].Y);
        }
    }
}
