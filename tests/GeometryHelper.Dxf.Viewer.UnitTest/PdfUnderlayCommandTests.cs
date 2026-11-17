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
    /// PDF underlays as AutoCAD 2021 does them: -PDFATTACH asking its page, point, scale (with the Unit option) and rotation,
    /// the page drawn at its place with the colours of its fills and strokes, PDFCLIP, PDFADJUST, PDFLAYERS, PDFFRAME, PDFOSNAP and
    /// FRAME, an underlay unloaded, detached, reloaded and not found, and the files built by the tests.
    /// </summary>
    public class PdfUnderlayCommandTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "gk", "pdf-" + Guid.NewGuid().ToString("N"));

        public PdfUnderlayCommandTests()
        {
            Directory.CreateDirectory(_folder);
            CadPdfCache.Clear();
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

        /// <summary>A PDF of two pages: the first 200 by 100 points, a red rectangle (20, 20) to (120, 70) and a blue line; the second 100 by 100.</summary>
        private string TwoPages(string name = "sample.pdf")
        {
            var file = new PdfTestFile();
            int pages = file.Reserve();
            int one = file.AddStream(string.Empty, "1 0 0 rg 20 20 100 50 re f 0 0 1 RG 2 w 10 10 m 190 90 l S");
            int two = file.AddStream(string.Empty, "0 1 0 rg 0 0 50 50 re f");
            int a = file.Add(
                "<< /Type /Page /Parent " + pages + " 0 R /MediaBox [0 0 200 100] /Contents " + one + " 0 R >>"
            );
            int b = file.Add(
                "<< /Type /Page /Parent " + pages + " 0 R /MediaBox [0 0 100 100] /Contents " + two + " 0 R >>"
            );
            file.Set(pages, "<< /Type /Pages /Count 2 /Kids [" + a + " 0 R " + b + " 0 R] >>");
            int root = file.Add("<< /Type /Catalog /Pages " + pages + " 0 R >>");
            string path = Path.Combine(_folder, name);
            File.WriteAllBytes(path, file.Build(root));
            return path;
        }

        /// <summary>A PDF of one page with two layers: a red rectangle on Walls and a blue line on Notes, and a green square on none.</summary>
        private string Layered()
        {
            var file = new PdfTestFile();
            int walls = file.Add("<< /Type /OCG /Name (Walls) >>");
            int notes = file.Add("<< /Type /OCG /Name (Notes) >>");
            int pages = file.Reserve();
            int content = file.AddStream(
                string.Empty,
                "/OC /L1 BDC 1 0 0 rg 20 20 100 50 re f EMC /OC /L2 BDC 0 0 1 RG 10 10 m 190 90 l S EMC 0 1 0 rg 150 10 20 20 re f"
            );
            int page = file.Add(
                "<< /Type /Page /Parent "
                    + pages
                    + " 0 R /MediaBox [0 0 200 100] /Contents "
                    + content
                    + " 0 R /Resources << /Properties << /L1 "
                    + walls
                    + " 0 R /L2 "
                    + notes
                    + " 0 R >> >> >>"
            );
            file.Set(pages, "<< /Type /Pages /Count 1 /Kids [" + page + " 0 R] >>");
            int root = file.Add(
                "<< /Type /Catalog /Pages "
                    + pages
                    + " 0 R /OCProperties << /OCGs ["
                    + walls
                    + " 0 R "
                    + notes
                    + " 0 R] /D << /Order ["
                    + walls
                    + " 0 R "
                    + notes
                    + " 0 R] >> >> >>"
            );
            string path = Path.Combine(_folder, "layered.pdf");
            File.WriteAllBytes(path, file.Build(root));
            return path;
        }

        private CadDocument Host()
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            string path = Path.Combine(_folder, "host.dxf");
            database.Save(path);
            return CadDocument.Open(path);
        }

        private static List<T> Items<T>(CadDocument document)
            where T : DisplayItem2 => document.GetDisplay(document.ModelSpace).Items.OfType<T>().ToList();

        private static DxfPdfUnderlay Underlay(CadDocument document) =>
            Assert.Single(All<DxfPdfUnderlay>(document.ModelSpace));

        private static void Near(double expected, double actual, double tolerance = 1E-6) =>
            Assert.True(Math.Abs(expected - actual) <= tolerance, expected + " was " + actual);

        private static CadCommandSession Attached(
            CadDocument document,
            string pdf,
            string scale = "25.4",
            string at = "10,20"
        )
        {
            CadCommandSession session = Session(document);
            Run(session, "-PDFATTACH", pdf, Enter, at, scale, Enter);
            return session;
        }

        [Fact]
        public void APdfAttachedByTheTypedCommand_IsPlacedAtThePointScaleAndRotationAsked()
        {
            CadDocument document = Host();
            string pdf = TwoPages();
            CadCommandSession session = Session(document);

            Run(session, "-PDFATTACH", pdf, "1", "10,20", "25.4", "90");

            // The size the page has at scale 1, in the drawing's unit, as -IMAGE says it.
            Assert.Contains("Base image size: Width: 2.777778, Height: 1.388889, Millimeters", session.History);
            DxfPdfUnderlay underlay = Underlay(document);
            Near(10, underlay.Position.X);
            Near(20, underlay.Position.Y);
            Assert.Equal(new GeoVector3(25.4, 25.4, 25.4), underlay.ScaleFactors);
            Near(Math.PI / 2, underlay.Rotation, 1E-9);
            Assert.Equal(".\\sample.pdf", underlay.Path);
            Assert.Equal(pdf, document.Xrefs.Locate(underlay.Path));
            Assert.Equal("1", underlay.NameOfSheet);
            Assert.Equal("sample - Page_1", underlay.Name);
        }

        [Fact]
        public void ThePagesAreListedByAQuestionMark_AndAPageThatIsNotThereIsAskedAgain()
        {
            CadDocument document = Host();
            string pdf = TwoPages();
            CadCommandSession session = Session(document);

            Run(session, "-PDFATTACH", pdf, "?", "*", "5", "2", "0,0", Enter, Enter);

            Assert.Contains("Page 1 - 2.777778 x 1.388889 inches", session.History);
            Assert.Contains("Page 2 - 1.388889 x 1.388889 inches", session.History);
            Assert.Contains("Requires an integer value between 1 and 2.", session.History);
            Assert.Equal("2", Underlay(document).NameOfSheet);
            Assert.Equal("sample - Page_2", Underlay(document).Name);
        }

        [Fact]
        public void TheUnitOption_GivesTheScaleThatMakesThePagesInchesThatUnit()
        {
            CadDocument document = Host();
            string pdf = TwoPages();
            CadCommandSession session = Session(document);

            // The inches of the page read as inches in a drawing of millimetres: 25.4; read as millimetres: 1.
            Run(session, "-PDFATTACH", pdf, "1", "0,0", "Unit", "Inch", Enter, Enter);
            Near(25.4, Underlay(document).ScaleFactors.X);
            Run(session, "-PDFATTACH", pdf, "1", "0,0", "Unit", "MM", Enter, Enter);
            List<DxfPdfUnderlay> all = All<DxfPdfUnderlay>(document.ModelSpace);
            Assert.Equal(2, all.Count);
            Assert.Equal(1.0, all.Min(u => u.ScaleFactors.X), 9);
        }

        [Fact]
        public void AFileThatIsNoPdf_OrIsNotThere_IsRefusedInAutoCadsWords()
        {
            CadDocument document = Host();
            string text = Path.Combine(_folder, "notes.pdf");
            File.WriteAllText(text, "this is not a PDF");
            CadCommandSession session = Session(document);

            Run(session, "-PDFATTACH", text);
            Run(session, "-PDFATTACH", Path.Combine(_folder, "missing.pdf"));
            Run(session, "-PDFATTACH", Path.Combine(_folder, "notes.txt"));

            Assert.Contains("You have selected an invalid or unsupported PDF file.", session.History);
            Assert.Contains("PDF not found.", session.History);
            Assert.Empty(All<DxfPdfUnderlay>(document.ModelSpace));
        }

        [Fact]
        public void AnUnderlay_IsDrawnByTheFillsAndStrokesOfItsPage_AtItsPlaceAndScale()
        {
            CadDocument document = Host();
            Attached(document, TwoPages());

            // A point of the page is 1/72 of an inch: 25.4 mm an inch makes it 0.35278 mm; the corner is (10, 20).
            double mm = 25.4 / 72.0;
            DisplayFill2 fill = Assert.Single(Items<DisplayFill2>(document));
            Assert.Equal(0xFF0000, fill.Style.Rgb);
            double[] ring = Assert.Single(fill.Rings);
            Near(10 + 20 * mm, ring[0]);
            Near(20 + 20 * mm, ring[1]);
            Near(10 + 120 * mm, ring[2]);
            Near(20 + 70 * mm, ring[5]);

            DisplayPolyline2 line = Items<DisplayPolyline2>(document).Single(p => p.Style.Rgb == 0x0000FF);
            Near(10 + 10 * mm, line.Xy[0]);
            Near(20 + 90 * mm, line.Xy[3]);
            Near(Math.Round(2 * 25.4 / 72.0, 2), line.Style.LineWeight);

            // The frame, the page's rectangle, in the underlay's own colour.
            DisplayPolyline2 frame = Items<DisplayPolyline2>(document).Single(p => p.Closed && p.Count == 4);
            Near(10 + 200 * mm, frame.Bounds.MaxX);
            Near(20 + 100 * mm, frame.Bounds.MaxY);
        }

        [Fact]
        public void ThePageOfAnotherNumber_IsTheOneDrawn()
        {
            CadDocument document = Host();
            CadCommandSession session = Session(document);
            Run(session, "-PDFATTACH", TwoPages(), "2", "0,0", "25.4", "0");

            DisplayFill2 fill = Assert.Single(Items<DisplayFill2>(document));
            Assert.Equal(0x00FF00, fill.Style.Rgb);
            Near(50 * 25.4 / 72.0, fill.Bounds.MaxX);
        }

        [Fact]
        public void APdfNotFound_IsDrawnAsItsFrameWithItsNameInTheCorner_AndOneUnloadedAsItsFrameAlone()
        {
            CadDocument document = Host();
            string pdf = TwoPages();
            Attached(document, pdf);
            CadCommandSession session = Session(document);

            // Unloaded: no page, only the frame.
            Run(session, "-PDFUNDERLAY", "Unload", "*");
            Assert.Empty(Items<DisplayFill2>(document));
            Assert.Single(Items<DisplayPolyline2>(document), p => p.Closed && p.Count == 4);
            Run(session, "-PDFUNDERLAY", "Reload", "*");
            Assert.Single(Items<DisplayFill2>(document));

            // The file gone: the frame and the file's name.
            File.Delete(pdf);
            CadPdfCache.Clear();
            document.Regen();
            Assert.Empty(Items<DisplayFill2>(document));
            Assert.Contains(Items<DisplayText2>(document), t => t.Text == ".\\sample.pdf");
        }

        [Fact]
        public void PdfClip_KeepsARectangleInThePagesInches_AndDrawsThePageThroughIt()
        {
            CadDocument document = Host();
            Attached(document, TwoPages(), "25.4", "0,0");
            DxfPdfUnderlay underlay = Underlay(document);
            CadCommandSession session = Session(document);

            // A window of the drawing, (10, 10) to (30, 30) mm: in the page's inches (10, 10)/25.4 to (30, 30)/25.4.
            Run(session, "PDFCLIP", new[] { underlay.Handle }, "New", "Rectangular", "10,10", "30,30");
            underlay = Underlay(document);
            Assert.True(underlay.IsClipped);
            IReadOnlyList<GeoPoint2> boundary = underlay.GetClipBoundary();
            Assert.Equal(2, boundary.Count);
            Near(10 / 25.4, boundary[0].X);
            Near(30 / 25.4, boundary[0].Y);
            Near(30 / 25.4, boundary[1].X);
            Near(10 / 25.4, boundary[1].Y);

            // The fill is cut by it: the item carries the clip region of the window.
            DisplayFill2 fill = Assert.Single(Items<DisplayFill2>(document));
            Assert.NotNull(fill.BlockClip);
            Near(10, fill.BlockClip.Bounds.MinX);
            Near(30, fill.BlockClip.Bounds.MaxY);

            // The frame follows the boundary.
            DisplayPolyline2 frame = Items<DisplayPolyline2>(document).Single(p => p.Closed && p.Count == 4);
            Near(10, frame.Bounds.MinX);
            Near(30, frame.Bounds.MaxX);

            Run(session, "PDFCLIP", new[] { underlay.Handle }, "Off");
            Assert.False(Underlay(document).IsClipped);
            Run(session, "PDFCLIP", new[] { underlay.Handle }, "Delete");
            Assert.False(Underlay(document).HasClipBoundary);
            Assert.Null(Assert.Single(Items<DisplayFill2>(document)).BlockClip);
        }

        [Fact]
        public void PdfClip_InvertedAndPolygonal_AreKeptAsAPolygonWithTheFlagOfAutoCad()
        {
            CadDocument document = Host();
            Attached(document, TwoPages(), "25.4", "0,0");
            DxfPdfUnderlay underlay = Underlay(document);
            CadCommandSession session = Session(document);

            Run(
                session,
                "PDFCLIP",
                new[] { underlay.Handle },
                "New",
                "Invert clip",
                "Polygonal",
                "10,10",
                "40,10",
                "40,30",
                "Close"
            );
            underlay = Underlay(document);
            Assert.True(underlay.IsClipInverted);
            Assert.Equal(3, underlay.GetClipBoundary().Count);

            Run(
                session,
                "PDFCLIP",
                new[] { underlay.Handle },
                "New",
                "Yes",
                "Polygonal",
                "0,0",
                "30,30",
                "30,0",
                "0,30",
                "Close"
            );
            Assert.Contains("Self-intersecting clip boundaries are not acceptable.", session.History);
            Assert.Equal(3, Underlay(document).GetClipBoundary().Count);
        }

        [Fact]
        public void PdfAdjust_SetsTheFadeTheContrastAndTheMonochrome_ThatTheColoursFollow()
        {
            CadDocument document = Host();
            Attached(document, TwoPages());
            DxfPdfUnderlay underlay = Underlay(document);
            CadCommandSession session = Session(document);

            Run(session, "-PDFADJUST", new[] { underlay.Handle }, Enter, Enter, "40");
            Run(session, "-PDFADJUST", new[] { underlay.Handle }, Enter, "Contrast", "70");
            Run(session, "PDFADJUST", new[] { underlay.Handle }, Enter, "Monochrome", "Yes");
            underlay = Underlay(document);
            Assert.Equal(40, underlay.Fade);
            Assert.Equal(70, underlay.Contrast);
            Assert.True(underlay.Monochrome);

            // The look the items carry: what the canvas turns the colours by, on its background.
            DisplayUnderlayLook look = Assert.Single(Items<DisplayFill2>(document)).Style.Underlay;
            Assert.Equal(40, look.Fade);
            Assert.Equal(70, look.Contrast);
            Assert.True(look.Monochrome);

            Run(session, "-PDFADJUST", new[] { underlay.Handle }, Enter, "Fade", "101");
            Assert.Contains("Requires an integer value between 0 and 100.", session.History);
            Assert.Equal(40, Underlay(document).Fade);
        }

        [Fact]
        public void TheLook_FadesAndGreysTheColoursOnTheirBackground_AndTurnsTheBlackOfAPageOnADarkOne()
        {
            var plain = new DisplayUnderlayLook(0, 100, false, false);
            Assert.Equal(0xFF0000, plain.Apply(0xFF0000, 0xFFFFFF));

            // A fade of half: red towards a white background is pink.
            Assert.Equal(0xFF8080, new DisplayUnderlayLook(50, 100, false, false).Apply(0xFF0000, 0xFFFFFF));
            // Monochrome: the grey of the colour.
            int grey = new DisplayUnderlayLook(0, 100, true, false).Apply(0xFF0000, 0xFFFFFF);
            Assert.Equal((grey >> 16) & 0xFF, grey & 0xFF);
            // Adjusted for a dark background: black becomes white, white black, red stays red.
            var adjust = new DisplayUnderlayLook(0, 100, false, true);
            Assert.Equal(0xFFFFFF, adjust.Apply(0x000000, 0x000000));
            Assert.Equal(0x000000, adjust.Apply(0xFFFFFF, 0x000000));
            Assert.Equal(0xFF0000, adjust.Apply(0xFF0000, 0x000000));
            // On a light background nothing is turned.
            Assert.Equal(0x000000, adjust.Apply(0x000000, 0xFFFFFF));
        }

        [Fact]
        public void PdfLayers_SwitchesALayerOfThePdf_AndItsContentLeavesTheDrawing()
        {
            CadDocument document = Host();
            Attached(document, Layered());
            Assert.Equal(2, Items<DisplayFill2>(document).Count);
            Assert.Equal(1, Items<DisplayPolyline2>(document).Count(p => p.Style.Rgb == 0x0000FF));
            DxfPdfUnderlay underlay = Underlay(document);
            CadCommandSession session = Session(document);

            Run(session, "PDFLAYERS", new[] { underlay.Handle }, "Notes", Enter);
            Assert.Contains("  " + "Walls".PadRight(30) + "On", session.History);
            Assert.Contains("  " + "Notes".PadRight(30) + "On", session.History);
            Assert.Contains("  " + "Notes".PadRight(30) + "Off", session.History);
            Assert.Equal(0, Items<DisplayPolyline2>(document).Count(p => p.Style.Rgb == 0x0000FF));
            Assert.Equal(2, Items<DisplayFill2>(document).Count);

            // The layer kept off by the underlay, read back through a DXF.
            Assert.Equal(
                new Dictionary<string, bool> { ["Notes"] = false },
                CadPdfLayers.Overrides(Underlay(document))
            );
            string path = Path.Combine(_folder, "saved.dxf");
            document.Database.Save(path);
            CadDocument reopened = CadDocument.Open(path);
            Assert.Equal(0, Items<DisplayPolyline2>(reopened).Count(p => p.Style.Rgb == 0x0000FF));

            Run(session, "PDFLAYERS", new[] { underlay.Handle }, "Walls", "All on", Enter);
            Assert.Equal(1, Items<DisplayPolyline2>(document).Count(p => p.Style.Rgb == 0x0000FF));
            Assert.Equal(2, Items<DisplayFill2>(document).Count);
        }

        [Fact]
        public void PdfFrame_ShowsTheFrameOrNot_AndPdfOsnap_GivesOrTakesTheSnapsOfItsStrokes()
        {
            CadDocument document = Host();
            Attached(document, TwoPages());
            CadCommandSession session = Session(document);
            bool Frame() => Items<DisplayPolyline2>(document).Any(p => p.Closed && p.Count == 4);

            Assert.True(Frame());
            Run(session, "PDFFRAME", "0");
            Assert.Equal(0, document.PdfFrame);
            Assert.False(Frame());
            Run(session, "PDFFRAME", "2");
            Assert.True(Frame());
            Assert.True(Items<DisplayPolyline2>(document).Single(p => p.Closed && p.Count == 4).IsNotPlotted);
            Run(session, "PDFFRAME", "1");
            Assert.False(Items<DisplayPolyline2>(document).Single(p => p.Closed && p.Count == 4).IsNotPlotted);

            DisplayPolyline2 stroke = Items<DisplayPolyline2>(document).Single(p => p.Style.Rgb == 0x0000FF);
            Assert.False(stroke.IsFrameInside);
            Run(session, "PDFOSNAP", "0");
            Assert.False(document.PdfOsnap);
            Assert.True(Items<DisplayPolyline2>(document).Single(p => p.Style.Rgb == 0x0000FF).IsFrameInside);
            Run(session, "PDFOSNAP", "1");
            Assert.False(Items<DisplayPolyline2>(document).Single(p => p.Style.Rgb == 0x0000FF).IsFrameInside);

            // The fill of a page gives no snaps of its own whatever PDFOSNAP is.
            Assert.True(Items<DisplayFill2>(document).Single().IsFrameInside);

            // FRAME sets the frames of images and PDFs together.
            Run(session, "FRAME", "2");
            Assert.Equal(2, document.PdfFrame);
        }

        [Fact]
        public void ThePaletteRows_AreListedAndTheTypedCommandDetachesRenamesThePathAndLists()
        {
            CadDocument document = Host();
            string pdf = TwoPages();
            Attached(document, pdf);
            CadCommandSession session = Session(document);

            CadExternalReference row = Assert.Single(document.Xrefs.GetReferences(), r => r.IsPdf);
            Assert.Equal("sample - Page_1", row.Name);
            Assert.Equal("PDF", row.TypeName);
            Assert.Equal(CadReferenceStatus.Loaded, row.Status);

            Run(session, "-PDFUNDERLAY", "?", "*");
            Assert.Contains("Total PDF underlay definitions: 1", session.History);

            Run(session, "-PDFUNDERLAY", "Path", "*", "D:\\nowhere\\other.pdf");
            row = Assert.Single(document.Xrefs.GetReferences(), r => r.IsPdf);
            Assert.Equal(CadReferenceStatus.NotFound, row.Status);

            Run(session, "-PDFUNDERLAY", "Detach", "*");
            Assert.Empty(All<DxfPdfUnderlay>(document.ModelSpace));
            Assert.DoesNotContain(document.Xrefs.GetReferences(), r => r.IsPdf);

            // An undo brings the underlay and its definition back.
            Assert.True(document.Undo());
            Assert.Single(All<DxfPdfUnderlay>(document.ModelSpace));
        }

        [Fact]
        public void AnUnderlayInABlock_IsDrawnWhereTheBlockIsPlaced()
        {
            // The underlay put in a block and the block inserted at (1000, 0): the page is drawn there.
            DxfDatabase database = CadTemplate.CreateMetric();
            DxfDBDictionary underlays = DxfPdfDefinition.CreatePdfDictionary(database);
            string pdf = TwoPages();
            var definition = new DxfPdfDefinition(pdf, "1");
            underlays.SetAt("sample - Page_1", definition);
            var block = new DxfBlockTableRecord { Name = "Plan" };
            database.BlockTable.Add(block);
            var inside = new DxfPdfUnderlay(definition);
            block.AppendEntity(inside);
            inside.ScaleFactors = new GeoVector3(25.4, 25.4, 25.4);
            database.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(1000, 0, 0), block));
            string path = Path.Combine(_folder, "block.dxf");
            database.Save(path);
            CadDocument document = CadDocument.Open(path);

            DisplayFill2 fill = Assert.Single(Items<DisplayFill2>(document));
            Near(1000 + 20 * 25.4 / 72.0, fill.Bounds.MinX);
        }
    }
}
