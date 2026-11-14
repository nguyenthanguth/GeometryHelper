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

        [Fact]
        public void WithHyperlinksOff_AViewportsObjectGetsNoLink()
        {
            DxfDatabase db = Layout(out _, out _);
            Link(db.ModelSpace, 1800, 1200, 2200, 1800, "https://example.com/model.pdf");

            Assert.Empty(Export(db, hyperlinks: false));
        }
    }
}
