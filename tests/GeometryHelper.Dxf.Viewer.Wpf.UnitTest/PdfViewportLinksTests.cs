using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// EXPORTPDF's hyperlinks on objects seen through a layout's viewports: a link over the box of the object as it stands on
    /// the paper, cut to the frame of the viewport, as an object plotted of the layout itself has one.
    /// </summary>
    public class PdfViewportLinksTests
    {
        /// <summary>A link of the page: its address and its rectangle in points.</summary>
        private sealed class PdfLink
        {
            public string Uri;
            public double[] Rect;
        }

        /// <summary>
        /// A metric drawing whose Layout1, a sheet of 420 by 297, has one viewport 200 by 150 about (150, 100) showing model
        /// space at 1:20 about (2000, 1500): a model point (x, y) stands at (150 + (x - 2000) / 20, 100 + (y - 1500) / 20).
        /// </summary>
        private static DxfDatabase Layout(out DxfLayout layout, out DxfViewport frame)
        {
            DxfDatabase db = CadTemplate.CreateMetric();
            layout = db.Layouts.First(l => l.LayoutName == "Layout1");
            layout.Initialize();
            frame = Viewport(150, 100, 200, 150);
            layout.BlockTableRecord.AppendEntity(frame);
            frame.On = true;
            return db;
        }

        /// <summary>A viewport of a sheet at 1:20 about the model point (2000, 1500).</summary>
        private static DxfViewport Viewport(double x, double y, double width, double height) =>
            new DxfViewport
            {
                CenterPoint = new GeoPoint3(x, y, 0),
                Width = width,
                Height = height,
                ViewHeight = height * 20.0,
                ViewCenter = new GeoPoint2(2000, 1500),
            };

        /// <summary>A line of a block with a hyperlink, added to the block first, as a hyperlink needs its drawing.</summary>
        private static DxfLine Link(
            DxfBlockTableRecord owner,
            double x0,
            double y0,
            double x1,
            double y1,
            string address
        )
        {
            var line = new DxfLine(new GeoPoint3(x0, y0, 0), new GeoPoint3(x1, y1, 0));
            owner.AppendEntity(line);
            line.Hyperlinks.Add(new DxfHyperLink { Name = address, Description = address });
            return line;
        }

        /// <summary>Plots the first layout to a PDF with the hyperlinks on or off and gives the links the page carries.</summary>
        private static List<PdfLink> Export(DxfDatabase database, bool hyperlinks = true)
        {
            CadDocument document = CadDocument.From(database, "sheet.dxf");
            CadSpace space = document.Spaces.First(s => !s.IsModel);
            CadPlotSettings settings = CadPlotSettings.Read(document, space);
            settings.UseDevice(CadPlotSettings.PdfDevice);
            settings.Area = CadPlotArea.Window;
            settings.Window = new DisplayBox(0, 0, 420, 297);
            settings.FitToPaper = true;
            settings.PdfHyperlinks = hyperlinks;
            string text;

            using (var memory = new MemoryStream())
            {
                CadPdfWriter.Write(memory, document, space, settings, DisplayBox.Empty);
                text = Encoding.GetEncoding(28591).GetString(memory.ToArray());
            }
            var links = new List<PdfLink>();

            foreach (Match match in Regex.Matches(text, @"/Rect \[([^\]]*)\][^>]*?/URI \(([^)]*)\)"))
            {
                links.Add(
                    new PdfLink
                    {
                        Uri = match.Groups[2].Value,
                        Rect = match
                            .Groups[1]
                            .Value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(v => double.Parse(v, CultureInfo.InvariantCulture))
                            .ToArray(),
                    }
                );
            }

            return links;
        }

        private static void AssertRect(double[] expected, double[] actual)
        {
            Assert.Equal(4, actual.Length);

            for (int i = 0; i < 4; i++)
            {
                Assert.Equal(expected[i], actual[i], 2);
            }
        }

        [Fact]
        public void AModelObjectInAViewport_GetsALinkOverItsBoxOnThePaper_AsOneOfTheLayoutItselfWould()
        {
            DxfDatabase db = Layout(out DxfLayout layout, out _);

            // Model (1800, 1200)-(2200, 1800) stands at (140, 85)-(160, 115) on the sheet, as the line drawn there.
            Link(db.ModelSpace, 1800, 1200, 2200, 1800, "https://example.com/model.pdf");
            Link(layout.BlockTableRecord, 140, 85, 160, 115, "https://example.com/direct.pdf");

            List<PdfLink> links = Export(db);

            PdfLink direct = Assert.Single(links, l => l.Uri == "https://example.com/direct.pdf");
            PdfLink seen = Assert.Single(links, l => l.Uri == "https://example.com/model.pdf");
            AssertRect(direct.Rect, seen.Rect);
        }

        [Fact]
        public void AnObjectPastTheFrame_HasItsLinkCutToTheFrame_AndOneWhollyOutsideHasNone()
        {
            DxfDatabase db = Layout(out DxfLayout layout, out _);

            // Model (3000, 1000)-(6000, 2000) is (200, 75)-(350, 125) on the sheet, and the frame ends at x 250.
            Link(db.ModelSpace, 3000, 1000, 6000, 2000, "https://example.com/past.pdf");
            Link(db.ModelSpace, 5000, 1000, 5400, 1400, "https://example.com/outside.pdf");
            Link(layout.BlockTableRecord, 200, 75, 250, 125, "https://example.com/cut.pdf");

            List<PdfLink> links = Export(db);

            AssertRect(
                Assert.Single(links, l => l.Uri == "https://example.com/cut.pdf").Rect,
                Assert.Single(links, l => l.Uri == "https://example.com/past.pdf").Rect
            );
            Assert.DoesNotContain(links, l => l.Uri == "https://example.com/outside.pdf");
        }

        [Fact]
        public void AnObjectOnALayerFrozenInTheViewport_GetsNoLink()
        {
            DxfDatabase db = Layout(out _, out DxfViewport frame);
            var hidden = new DxfLayerTableRecord { Name = "HIDDEN" };
            db.LayerTable.Add(hidden);
            Link(db.ModelSpace, 1800, 1200, 2200, 1800, "https://example.com/hidden.pdf").Layer = "HIDDEN";
            Link(db.ModelSpace, 1000, 1000, 1400, 1400, "https://example.com/shown.pdf");
            frame.FreezeLayersInViewport(new[] { hidden });

            List<PdfLink> links = Export(db);

            Assert.Contains(links, l => l.Uri == "https://example.com/shown.pdf");
            Assert.DoesNotContain(links, l => l.Uri == "https://example.com/hidden.pdf");
        }

        [Fact]
        public void AnObjectSeenInTwoViewports_GetsALinkInEach()
        {
            DxfDatabase db = Layout(out DxfLayout layout, out _);

            // A second viewport 60 by 60 about (350, 100) shows the same place at the same scale.
            DxfViewport second = Viewport(350, 100, 60, 60);
            layout.BlockTableRecord.AppendEntity(second);
            second.On = true;
            Link(db.ModelSpace, 1900, 1400, 2100, 1600, "https://example.com/twice.pdf");

            List<PdfLink> twice = Export(db).Where(l => l.Uri == "https://example.com/twice.pdf").ToList();

            Assert.Equal(2, twice.Count);
            Assert.NotEqual(twice[0].Rect[0], twice[1].Rect[0]);
        }

        /// <summary>
        /// A rectangle of the PDF, in points, of a box of the sheet in millimetres: the 420 by 297 window is fitted into the A3
        /// sheet's printable area of 410 by 287, so that a millimetre is 287 / 297 of one, 5 mm in from the corner, and a PDF
        /// has its Y up.
        /// </summary>
        private static double[] Points(double x0, double y0, double x1, double y1)
        {
            double Point(double mm) => (5.0 + mm * 287.0 / 297.0) * 72.0 / 25.4;
            return new[] { Point(x0), Point(y0), Point(x1), Point(y1) };
        }

        /// <summary>
        /// Turns the view of a viewport by an angle and keeps (2000, 1500) of model space at its centre: the view centre is in the
        /// view's own coordinates, which the twist turns.
        /// </summary>
        private static void Twist(DxfViewport frame, double angle)
        {
            frame.TwistAngle = angle;
            frame.ViewCenter = CadViewports.ToView(frame, new GeoPoint2(2000, 1500));
        }

        /// <summary>A closed polyline of the layout from x, y pairs, to clip a viewport to.</summary>
        private static DxfPolyline Outline(DxfLayout layout, params double[] xy)
        {
            var outline = new DxfPolyline();

            for (int i = 0; i < xy.Length / 2; i++)
            {
                outline.AddVertexAt(i, new GeoPoint2(xy[2 * i], xy[2 * i + 1]), 0.0, 0.0, 0.0);
            }

            outline.Closed = true;
            layout.BlockTableRecord.AppendEntity(outline);
            return outline;
        }

        [Fact]
        public void AViewportClippedToARectangle_HasItsLinkCutToTheClip_AndAnObjectOutsideItHasNone()
        {
            DxfDatabase db = Layout(out DxfLayout layout, out DxfViewport frame);

            // The clip is 55 by 50 inside the frame of 200 by 150; model (1800, 1200)-(2200, 1800) stands at (140, 85)-(160, 115),
            // so the clip ends it at x 155 and y 110; model (2400, 1200)-(2800, 1600) is at (170, 85)-(190, 105), in the frame
            // and outside the clip.
            frame.ClipTo(Outline(layout, 100, 60, 155, 60, 155, 110, 100, 110));
            Link(db.ModelSpace, 1800, 1200, 2200, 1800, "https://example.com/cut.pdf");
            Link(db.ModelSpace, 2400, 1200, 2800, 1600, "https://example.com/outside.pdf");

            List<PdfLink> links = Export(db);

            AssertRect(Points(140, 85, 155, 110), Assert.Single(links).Rect);
            Assert.Equal("https://example.com/cut.pdf", links[0].Uri);
        }

        [Fact]
        public void AnObjectInTheNotchOfAnLShapedClip_HasNoLink_AndOneAcrossTheNotchHasItsBoxCutToTheClipsBox()
        {
            DxfDatabase db = Layout(out DxfLayout layout, out DxfViewport frame);

            // An L of 150 by 90 whose upper right corner, from x 200 and y 100, is left out. Model (3200, 1700)-(3600, 2100) is at
            // (210, 110)-(230, 130), wholly in the notch; model (2800, 1300)-(3400, 1900) is at (190, 90)-(220, 120), across it.
            frame.ClipTo(Outline(layout, 100, 60, 250, 60, 250, 100, 200, 100, 200, 150, 100, 150));
            Link(db.ModelSpace, 3200, 1700, 3600, 2100, "https://example.com/notch.pdf");
            Link(db.ModelSpace, 2800, 1300, 3400, 1900, "https://example.com/across.pdf");

            List<PdfLink> links = Export(db);

            Assert.DoesNotContain(links, l => l.Uri == "https://example.com/notch.pdf");
            AssertRect(
                Points(190, 90, 220, 120),
                Assert.Single(links, l => l.Uri == "https://example.com/across.pdf").Rect
            );
        }

        [Fact]
        public void AnObjectInAViewportTwistedAQuarterTurn_HasItsLinkOverTheTurnedBox()
        {
            DxfDatabase db = Layout(out _, out DxfViewport frame);
            Twist(frame, Math.PI / 2.0);

            // About the view's centre (2000, 1500) a quarter turn takes the offset (x, y) to (-y, x): model (1800, 1200) is
            // 300 to the right of and 200 below the centre of the frame at (150, 100), and model (2200, 1800) 300 to the left
            // of and 200 above it, at 1:20.
            Link(db.ModelSpace, 1800, 1200, 2200, 1800, "https://example.com/turned.pdf");

            AssertRect(Points(135, 90, 165, 110), Assert.Single(Export(db)).Rect);
        }

        [Fact]
        public void AnObjectInAViewportTwistedThirtyDegrees_HasItsLinkOverTheBoxOfWhatIsDrawn_NotOverTheTurnedBoxOfItsBox()
        {
            DxfDatabase db = Layout(out _, out DxfViewport frame);
            Twist(frame, Math.PI / 6.0);

            // The offsets (-200, -300) and (200, 300) of the line's ends from the view's centre turn to (-23.2, -359.8) and
            // (23.2, 359.8), that is (-1.16, -17.99) and (1.16, 17.99) at 1:20 about the centre (150, 100) of the frame; the
            // turned corners of the line's box would reach 16.16 to either side instead of 1.16.
            Link(db.ModelSpace, 1800, 1200, 2200, 1800, "https://example.com/turned.pdf");

            double x = (200.0 * Math.Cos(Math.PI / 6.0) + 300.0 * -Math.Sin(Math.PI / 6.0)) / 20.0;
            double y = (200.0 * Math.Sin(Math.PI / 6.0) + 300.0 * Math.Cos(Math.PI / 6.0)) / 20.0;
            AssertRect(
                Points(150 - Math.Abs(x), 100 - Math.Abs(y), 150 + Math.Abs(x), 100 + Math.Abs(y)),
                Assert.Single(Export(db)).Rect
            );
        }

        [Fact]
        public void ACircleInAViewportTwistedThirtyDegrees_HasItsLinkOverItsOwnBox()
        {
            DxfDatabase db = Layout(out _, out DxfViewport frame);
            Twist(frame, Math.PI / 6.0);

            // A circle of radius 300 about (2400, 1800): a turn leaves it a circle, 15 on the paper, and its centre at the offset
            // (400, 300) from the view's centre goes to (196.4, 459.8) / 20 from the frame's.
            var circle = new DxfCircle(new GeoPoint3(2400, 1800, 0), new GeoVector3(0, 0, 1), 300);
            db.ModelSpace.AppendEntity(circle);
            circle.Hyperlinks.Add(new DxfHyperLink { Name = "https://example.com/round.pdf" });

            double cx = 150 + (400.0 * Math.Cos(Math.PI / 6.0) - 300.0 * Math.Sin(Math.PI / 6.0)) / 20.0;
            double cy = 100 + (400.0 * Math.Sin(Math.PI / 6.0) + 300.0 * Math.Cos(Math.PI / 6.0)) / 20.0;
            AssertRect(Points(cx - 15, cy - 15, cx + 15, cy + 15), Assert.Single(Export(db)).Rect);
        }

        [Fact]
        public void WithHyperlinksOff_AViewportsObjectGetsNoLink()
        {
            DxfDatabase db = Layout(out _, out _);
            Link(db.ModelSpace, 1800, 1200, 2200, 1800, "https://example.com/model.pdf");

            Assert.Empty(Export(db, hyperlinks: false));
        }
    }
}
