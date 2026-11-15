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

            // Turned a quarter, the baseline lies to the right of the reference point, by 74 units of 0.005 (the 72 of
            // the ascent and the 2 that AutoCAD 2021 adds to a quarter turn at an em of 80).
            Close(2.5 + 0.37, text.Position.X);
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
        public void ATextAlignedToTheCentreOnTheBaseline_StaysLeftJustifiedAndStartsHalfItsWidthBack()
        {
            CadDocument document = Millimetres();
            var file = new WmfBytes()
                .WindowExt(1000, 1000)
                .Font(-80, 400, false, 0, "Arial")
                .Select(0)
                .SetTextAlign(6 | 24)
                .TextOut(500, 500, "Mid");

            Imported(document, file);

            // AutoCAD 2021 gives no justification: the text is moved back by the width of "Mid" in Arial (0.833 + 0.222 + 0.556 of
            // the em of 80 units, times 1.264, the ascent over the capitals' height), half of it for the centre.
            DxfDBText text = Assert.Single(document.Database.BlockTable["WMF0"].OfType<DxfDBText>());
            Assert.Equal(DxfAttachmentPoint.BaseLeft, text.Justify);
            double width = (1706.0 + 455.0 + 1139.0) / 2048.0 * 80.0 * (0.9053 / 0.7163);
            Assert.InRange(text.Position.X, 2.5 - (width / 2.0 + 0.5) * 0.005, 2.5 - (width / 2.0 - 0.5) * 0.005);
            Close(-2.5, text.Position.Y);
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

        // The colour between the primaries: the values below are the indexes AutoCAD 2021's console gave to pens of these colours.

        [Theory]
        [InlineData(0, 160, 0, 92)]
        [InlineData(0, 90, 0, 96)]
        [InlineData(0, 0, 200, 172)]
        [InlineData(30, 0, 0, 18)]
        [InlineData(80, 0, 0, 16)]
        [InlineData(120, 0, 0, 14)]
        [InlineData(180, 0, 0, 12)]
        [InlineData(170, 85, 85, 13)]
        [InlineData(30, 30, 0, 48)]
        [InlineData(40, 40, 0, 58)]
        [InlineData(60, 60, 0, 46)]
        [InlineData(100, 100, 0, 44)]
        [InlineData(130, 130, 0, 54)]
        [InlineData(180, 180, 0, 52)]
        [InlineData(210, 210, 0, 40)]
        [InlineData(1, 1, 1, 7)]
        [InlineData(10, 10, 10, 7)]
        [InlineData(100, 100, 100, 250)]
        [InlineData(128, 128, 128, 251)]
        [InlineData(160, 160, 160, 252)]
        [InlineData(190, 190, 190, 253)]
        [InlineData(220, 220, 220, 254)]
        [InlineData(240, 240, 240, 7)]
        public void APenBetweenThePrimaries_TakesTheIndexAutoCadGivesIt(int red, int green, int blue, int expected)
        {
            CadDocument document = Millimetres();
            var file = new WmfBytes()
                .WindowExt(1000, 1000)
                .Pen(0, 0, (byte)red, (byte)green, (byte)blue)
                .Select(0)
                .MoveTo(0, 0)
                .LineTo(100, 100);

            Imported(document, file);

            DxfPolyline2d line = Assert.Single(Polylines(document.Database.BlockTable["WMF0"]));
            Assert.Equal(expected, line.Color.ColorIndex);
        }

        [Fact]
        public void ABrushBetweenThePrimaries_TakesTheSameIndexAsAPenOfThatColour()
        {
            CadDocument document = Millimetres();
            var file = new WmfBytes()
                .WindowExt(1000, 1000)
                .Pen(5, 0, 0, 0, 0)
                .Brush(0, 0, 160, 0)
                .Select(0)
                .Select(1)
                .Rectangle(100, 100, 500, 500);

            Imported(document, file);

            DxfPolyline2d box = Assert.Single(Polylines(document.Database.BlockTable["WMF0"]));
            Assert.Equal(92, box.Color.ColorIndex);
        }

        [Fact]
        public void ManyPensInOneFile_EachKeepItsOwnColour()
        {
            CadDocument document = Millimetres();
            var file = new WmfBytes().WindowExt(1000, 1000);
            const int count = 80;

            for (int i = 0; i < count; i++)
            {
                file.Pen(0, 0, (byte)(i * 3), 0, 0);
            }

            for (int i = 0; i < count; i++)
            {
                file.Select(i).MoveTo(0, i * 10).LineTo(100, i * 10);
            }

            Imported(document, file);

            List<DxfPolyline2d> lines = Polylines(document.Database.BlockTable["WMF0"]);
            Assert.Equal(count, lines.Count);
            Assert.Equal(7, lines[0].Color.ColorIndex);
            Assert.Equal(14, lines[40].Color.ColorIndex);
            Assert.Equal(1, lines[count - 1].Color.ColorIndex);
        }

        // SaveDC and RestoreDC.

        [Fact]
        public void RestoreDC_GivesBackThePenAndTheBrushOfTheSave()
        {
            CadDocument document = Millimetres();
            var file = new WmfBytes()
                .WindowExt(1000, 1000)
                .Pen(0, 10, 255, 0, 0)
                .Pen(0, 10, 0, 255, 0)
                .Brush(0, 0, 0, 255)
                .Brush(0, 255, 255, 0)
                .Select(0)
                .MoveTo(0, 100)
                .LineTo(500, 100)
                .SaveDC()
                .Select(1)
                .MoveTo(0, 200)
                .LineTo(500, 200)
                .RestoreDC(-1)
                .MoveTo(0, 300)
                .LineTo(500, 300)
                .SaveDC()
                .Select(2)
                .Rectangle(0, 400, 100, 450)
                .RestoreDC(-1)
                .Rectangle(200, 400, 300, 450);

            Imported(document, file);

            Assert.Equal(
                new[] { 1, 3, 1, 5, 7 },
                Polylines(document.Database.BlockTable["WMF0"]).Select(p => (int)p.Color.ColorIndex).ToArray()
            );
        }

        [Fact]
        public void RestoreDCWithANumberAbove0_GoesBackToThatSave_AndOneWithNoSaveDoesNothing()
        {
            CadDocument document = Millimetres();
            var file = new WmfBytes()
                .WindowExt(1000, 1000)
                .Pen(0, 10, 255, 0, 0)
                .Pen(0, 10, 0, 255, 0)
                .Pen(0, 10, 0, 0, 255)
                .Select(0)
                .SaveDC()
                .Select(1)
                .SaveDC()
                .Select(2)
                .RestoreDC(1)
                .MoveTo(0, 100)
                .LineTo(500, 100)
                .RestoreDC(-1)
                .MoveTo(0, 200)
                .LineTo(500, 200);

            Imported(document, file);

            // RestoreDC 1 goes back to the first save (red) and drops both; the second, with no save left, changes nothing.
            Assert.Equal(
                new[] { 1, 1 },
                Polylines(document.Database.BlockTable["WMF0"]).Select(p => (int)p.Color.ColorIndex).ToArray()
            );
        }

        // Clipping: IntersectClipRect clips the lines.

        private static List<GeoPoint3> ClippedLine(CadDocument document, int index = 0) =>
            Points(Polylines(document.Database.BlockTable["WMF0"])[index]);

        [Fact]
        public void IntersectClipRect_CutsALineThatCrossesItToTheClip()
        {
            CadDocument document = Millimetres();
            var file = new WmfBytes()
                .WindowExt(1000, 1000)
                .Pen(0, 10, 255, 0, 0)
                .Select(0)
                .IntersectClipRect(200, 0, 600, 1000)
                .MoveTo(0, 100)
                .LineTo(1000, 100)
                .MoveTo(0, 300)
                .LineTo(1000, 500);

            Imported(document, file);

            Near(new GeoPoint3(1.0, -0.5, 0), ClippedLine(document)[0]);
            Near(new GeoPoint3(3.0, -0.5, 0), ClippedLine(document)[1]);
            Near(new GeoPoint3(1.0, -1.7, 0), ClippedLine(document, 1)[0]);
            Near(new GeoPoint3(3.0, -2.1, 0), ClippedLine(document, 1)[1]);
        }

        [Fact]
        public void TheClipIsTheIntersectionOfTheRectanglesGiven_AndRestoreDCGivesTheClipBack()
        {
            CadDocument document = Millimetres();
            var file = new WmfBytes()
                .WindowExt(1000, 1000)
                .Pen(0, 10, 255, 0, 0)
                .Select(0)
                .SaveDC()
                .IntersectClipRect(200, 0, 600, 1000)
                .IntersectClipRect(300, 0, 400, 1000)
                .MoveTo(0, 500)
                .LineTo(1000, 500)
                .RestoreDC(-1)
                .MoveTo(0, 700)
                .LineTo(1000, 700);

            Imported(document, file);

            Near(new GeoPoint3(1.5, -2.5, 0), ClippedLine(document)[0]);
            Near(new GeoPoint3(2.0, -2.5, 0), ClippedLine(document)[1]);
            Near(new GeoPoint3(0.0, -3.5, 0), ClippedLine(document, 1)[0]);
            Near(new GeoPoint3(5.0, -3.5, 0), ClippedLine(document, 1)[1]);
        }

        [Fact]
        public void ALineThatMissesTheClip_IsLeftWholeUnlessItRunsAlongAnAxis_WhenItIsDropped()
        {
            CadDocument document = Millimetres();
            var file = new WmfBytes()
                .WindowExt(1000, 1000)
                .Pen(0, 10, 255, 0, 0)
                .Select(0)
                .IntersectClipRect(200, 200, 600, 600)
                .MoveTo(0, 0)
                .LineTo(150, 1000)
                .MoveTo(0, 100)
                .LineTo(150, 100)
                .MoveTo(650, 100)
                .LineTo(900, 100);

            Imported(document, file);

            // AutoCAD 2021 keeps the first, cut by nothing, and drops the other two.
            DxfPolyline2d kept = Assert.Single(Polylines(document.Database.BlockTable["WMF0"]));
            Near(new GeoPoint3(0.0, 0.0, 0), Points(kept)[0]);
            Near(new GeoPoint3(0.75, -5.0, 0), Points(kept)[1]);
        }

        [Fact]
        public void ALineAcrossOneAxisOfTheClipAndOutsideTheOther_IsCutAcrossTheFirstOnly()
        {
            CadDocument document = Millimetres();
            var file = new WmfBytes()
                .WindowExt(1000, 1000)
                .Pen(0, 10, 255, 0, 0)
                .Select(0)
                .IntersectClipRect(200, 200, 600, 600)
                .MoveTo(0, 100)
                .LineTo(1000, 100)
                .MoveTo(100, 0)
                .LineTo(100, 1000);

            Imported(document, file);

            Near(new GeoPoint3(1.0, -0.5, 0), ClippedLine(document)[0]);
            Near(new GeoPoint3(3.0, -0.5, 0), ClippedLine(document)[1]);
            Near(new GeoPoint3(0.5, -1.0, 0), ClippedLine(document, 1)[0]);
            Near(new GeoPoint3(0.5, -3.0, 0), ClippedLine(document, 1)[1]);
        }

        [Fact]
        public void TheClipTouchesLinesAlone_AndExcludeClipRectIsIgnored()
        {
            CadDocument document = Millimetres();
            var file = new WmfBytes()
                .WindowExt(1000, 1000)
                .Pen(0, 10, 255, 0, 0)
                .Brush(0, 0, 0, 255)
                .Select(0)
                .Select(1)
                .IntersectClipRect(200, 0, 600, 1000)
                .Rectangle(100, 200, 700, 300)
                .Polyline(0, 400, 400, 450, 800, 400)
                .ExcludeClipRect(200, 0, 600, 1000)
                .MoveTo(300, 600)
                .LineTo(500, 600);

            Imported(document, file);

            List<DxfPolyline2d> shapes = Polylines(document.Database.BlockTable["WMF0"]);
            Assert.Equal(3, shapes.Count);
            Near(new GeoPoint3(0.5, -1.0, 0), Points(shapes[0])[0]);
            Near(new GeoPoint3(3.5, -1.0, 0), Points(shapes[0])[1]);
            Near(new GeoPoint3(4.0, -2.0, 0), Points(shapes[1])[2]);
            Near(new GeoPoint3(1.5, -3.0, 0), Points(shapes[2])[0]);
            Near(new GeoPoint3(2.5, -3.0, 0), Points(shapes[2])[1]);
        }

        // A window of a width or a height below nought.

        [Theory]
        [InlineData(-1000, 1000, -0.5, -0.5, 0.05)]
        [InlineData(-2000, 1000, -0.5, -0.5, 0.05)]
        [InlineData(-1000, 2000, -0.25, -0.25, 0.025)]
        [InlineData(1000, -1000, 0.882739, 0.882739, 0.0882739)]
        [InlineData(1000, -2000, 0.882739, 0.882739, 0.0882739)]
        [InlineData(2000, -1000, 0.4413695, 0.4413695, 0.04413695)]
        [InlineData(-1000, -1000, 0.882739, -0.882739, 0.0)]
        [InlineData(-1000, -2000, 0.882739, -0.882739, 0.0)]
        [InlineData(-2000, -1000, 0.5, -0.5, 0.0)]
        public void AWindowOfANegativeExtent_TurnsThePictureOverAsAutoCadDoes(
            int width,
            int height,
            double x,
            double y,
            double penWidth
        )
        {
            CadDocument document = Millimetres();
            var file = new WmfBytes()
                .WindowExt(width, height)
                .Pen(0, 10, 255, 0, 0)
                .Select(0)
                .MoveTo(100, 100)
                .LineTo(900, 300);

            Imported(document, file);

            DxfPolyline2d line = Assert.Single(Polylines(document.Database.BlockTable["WMF0"]));
            Near(new GeoPoint3(x, y, 0), Points(line)[0]);
            Near(new GeoPoint3(9.0 * x, 3.0 * y, 0), Points(line)[1]);
            Close(penWidth, line.ConstantWidth);
        }

        // The look of a text: its ascent, its height and the style of its font.

        private static DxfDBText TextOf(CadDocument document) =>
            Assert.Single(document.Database.BlockTable["WMF0"].OfType<DxfDBText>());

        [Theory]
        [InlineData("Arial", 362.0)]
        [InlineData("Courier New", 333.0)]
        [InlineData("Tahoma", 400.0)]
        public void ATextsBaselineLiesTheAscentOfItsFaceBelowThePoint_AndItsHeightIs0Point8OfIt(
            string face,
            double ascent
        )
        {
            CadDocument document = Millimetres();
            var file = new WmfBytes()
                .WindowExt(4000, 4000)
                .Font(-400, 400, false, 0, face)
                .Select(0)
                .TextOut(500, 1000, "Hi");

            Imported(document, file);

            // An em of 400 units at 0.00125 to the unit; AutoCAD 2021 measured the ascent of Arial 362, Courier New 333, Tahoma 400.
            DxfDBText text = TextOf(document);
            Close(0.8 * ascent * 0.00125, text.Height);
            Near(new GeoPoint3(0.625, -1.25 - ascent * 0.00125, 0), text.Position);
        }

        [Fact]
        public void ABoldTextOfArialStillRestsOnArialsAscent()
        {
            CadDocument document = Millimetres();
            var file = new WmfBytes()
                .WindowExt(4000, 4000)
                .Font(-400, 700, false, 0, "Arial")
                .Select(0)
                .TextOut(500, 1000, "Hi");

            Imported(document, file);

            DxfDBText text = TextOf(document);
            Close(0.8 * 362.0 * 0.00125, text.Height);
        }

        [Fact]
        public void ATextTurnedAQuarter_ReachesTwoPartsInAThousandAndSeventyFiveOfTheEmFurther()
        {
            CadDocument document = Millimetres();
            var file = new WmfBytes()
                .WindowExt(4000, 4000)
                .Font(-400, 400, false, 900, "Arial")
                .Select(0)
                .TextOut(500, 1000, "Hi");

            Imported(document, file);

            // 362 and 7 (0.0175 of 400) as AutoCAD 2021 measured Arial at 90 degrees: 369.
            DxfDBText text = TextOf(document);
            Close(0.8 * 369.0 * 0.00125, text.Height);
            Close(0.625 + 369.0 * 0.00125, text.Position.X);
            Close(-1.25, text.Position.Y);
        }

        [Fact]
        public void ATextTurnedToAnyOtherAngle_ReachesFurtherByTheMeasureOfItsFace()
        {
            CadDocument document = Millimetres();
            var file = new WmfBytes()
                .WindowExt(4000, 4000)
                .Font(-400, 400, false, 450, "Arial")
                .Select(0)
                .TextOut(500, 1000, "Hi");

            Imported(document, file);

            // AutoCAD 2021 gives Arial 423 of the 400 at 45 degrees (and at 15, 30, 60 and 75): 362 and 0.1525 of the em.
            DxfDBText text = TextOf(document);
            Close(0.8 * 423.0 * 0.00125, text.Height);
            double reach = 423.0 * 0.00125 * Math.Sqrt(0.5);
            Close(0.625 + reach, text.Position.X);
            Close(-1.25 - reach, text.Position.Y);
        }

        [Fact]
        public void ATextAlignedToTheBottom_RestsOnTheBaselineTheDescentTimesTheRatioAboveThePoint()
        {
            CadDocument document = Millimetres();
            var file = new WmfBytes()
                .WindowExt(4000, 4000)
                .Font(-400, 400, false, 0, "Arial")
                .Select(0)
                .SetTextAlign(8)
                .TextOut(500, 1000, "Hi");

            Imported(document, file);

            // AutoCAD 2021 put the baseline of Arial of 400 107.2 units above the bottom: 0.2119 of the em times 1.264.
            DxfDBText text = TextOf(document);
            Assert.InRange(text.Position.Y, -1.25 + 106.5 * 0.00125, -1.25 + 108.0 * 0.00125);
        }

        [Theory]
        [InlineData("Arial", 26, 1152.4)]
        [InlineData("Arial", 30, 576.2)]
        [InlineData("Courier New", 26, 1749.0)]
        [InlineData("Courier New", 30, 874.5)]
        [InlineData("Tahoma", 26, 1211.3)]
        public void ATextAlignedToTheRightOrTheCentre_StartsItsWidthOrHalfBack_NotJustified(
            string face,
            int align,
            double back
        )
        {
            CadDocument document = Millimetres();
            var file = new WmfBytes()
                .WindowExt(4000, 4000)
                .Font(-400, 400, false, 0, face)
                .Select(0)
                .SetTextAlign(align)
                .TextOut(2000, 1000, "Hello");

            Imported(document, file);

            // The width AutoCAD 2021 took off Arial "Hello" at 400 was 1152.4.
            DxfDBText text = TextOf(document);
            Assert.Equal(DxfAttachmentPoint.BaseLeft, text.Justify);
            Assert.InRange(text.Position.X, 2.5 - (back + 1.5) * 0.00125, 2.5 - (back - 1.5) * 0.00125);
            Close(-1.25, text.Position.Y);
        }

        [Fact]
        public void TheLooksOfAFace_AreToldApartByBoldFrom700_ItalicTheCharacterSetAndThePitch_NotByUnderlineOrByTheCaseOfTheName()
        {
            CadDocument document = Millimetres();
            var file = new WmfBytes()
                .WindowExt(1000, 1000)
                .Font(-80, 400, false, 0, "Arial")
                .Font(-80, 600, false, 0, "arial")
                .Font(-80, 700, false, 0, "Arial")
                .Font(-80, 400, false, 0, "Arial", 204)
                .Font(-80, 400, false, 0, "Arial", 0, 34)
                .Font(-80, 400, true, 0, "Arial")
                .Font(-80, 400, false, 0, "Arial", 204)
                .Select(0)
                .TextOut(100, 100, "a")
                .Select(1)
                .TextOut(100, 200, "b")
                .Select(2)
                .TextOut(100, 300, "c")
                .Select(3)
                .TextOut(100, 400, "d")
                .Select(4)
                .TextOut(100, 500, "e")
                .Select(5)
                .TextOut(100, 600, "f")
                .Select(6)
                .TextOut(100, 700, "g");

            Imported(document, file);

            Assert.Equal(
                new[]
                {
                    "WMF-Arial0",
                    "WMF-Arial0",
                    "WMF-Arial1",
                    "WMF-Arial2",
                    "WMF-Arial3",
                    "WMF-Arial4",
                    "WMF-Arial2",
                },
                document.Database.BlockTable["WMF0"].OfType<DxfDBText>().Select(t => t.TextStyleName).ToArray()
            );
            DxfFontDescriptor russian = document.Database.TextStyleTable["WMF-Arial2"].Font;
            Assert.Equal(204, russian.CharacterSet);
            Assert.Equal(34, document.Database.TextStyleTable["WMF-Arial3"].Font.PitchAndFamily);
            Assert.True(document.Database.TextStyleTable["WMF-Arial1"].Font.Bold);
        }

        [Fact]
        public void TheBytesOfATextAreWindows1252_WhateverTheCharacterSetOfItsFont()
        {
            CadDocument document = Millimetres();
            var file = new WmfBytes()
                .WindowExt(1000, 1000)
                .Font(-80, 400, false, 0, "Arial", 204)
                .Select(0)
                .TextOutBytes(100, 100, 0x48, 0xE9, 0x80, 0x99, 0xCF);

            Imported(document, file);

            Assert.Equal("Hé€™Ï", TextOf(document).TextString);
        }
    }
}
