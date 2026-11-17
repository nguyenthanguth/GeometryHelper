using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using SkiaSharp;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// PDF underlays in the window: a PDF written by SkiaSharp (the library the presenter's own plots go through) read for what it
    /// draws, the Attach PDF Underlay dialog's pages, thumbnails and unit, the PDF Layers dialog, the contextual PDF Underlay tab,
    /// and the palette's rows of PDF underlays.
    /// </summary>
    public sealed class PdfUnderlayWindowTests : IDisposable
    {
        private readonly string _folder = Path.Combine(
            Path.GetTempPath(),
            "gk",
            "pdfwin-" + Guid.NewGuid().ToString("N")
        );

        public PdfUnderlayWindowTests()
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

        /// <summary>A PDF of two pages written by SkiaSharp: the first 200 by 100 points with a red rectangle, a blue line, the word Hello and a 4 by 4 picture; the second 100 by 100 with a green square.</summary>
        private string SkiaPdf(string name = "skia.pdf")
        {
            string path = Path.Combine(_folder, name);

            using (FileStream stream = File.Create(path))
            using (SKDocument pdf = SKDocument.CreatePdf(stream))
            {
                using (SKCanvas canvas = pdf.BeginPage(200, 100))
                using (var red = new SKPaint { Color = new SKColor(255, 0, 0), IsAntialias = true })
                using (
                    var blue = new SKPaint
                    {
                        Color = new SKColor(0, 0, 255),
                        IsStroke = true,
                        StrokeWidth = 2f,
                        IsAntialias = true,
                    }
                )
                using (var black = new SKPaint { Color = SKColors.Black, IsAntialias = true })
                using (var font = new SKFont(SKTypeface.FromFamilyName("Arial"), 20f))
                using (var bitmap = new SKBitmap(4, 4))
                {
                    canvas.DrawRect(20, 20, 100, 50, red);
                    canvas.DrawLine(10, 10, 190, 90, blue);
                    canvas.DrawText("Hello", 130, 40, SKTextAlign.Left, font, black);
                    bitmap.Erase(new SKColor(0, 200, 0));
                    using (SKImage picture = SKImage.FromBitmap(bitmap))
                    {
                        canvas.DrawImage(picture, new SKRect(150, 50, 190, 90), new SKSamplingOptions());
                    }
                    pdf.EndPage();
                }

                using (SKCanvas canvas = pdf.BeginPage(100, 100))
                using (var green = new SKPaint { Color = new SKColor(0, 255, 0) })
                {
                    canvas.DrawRect(0, 0, 50, 50, green);
                    pdf.EndPage();
                }

                pdf.Close();
            }

            return path;
        }

        private MainViewModel WithPdf(out string handle, string path = null)
        {
            string pdf = path ?? SkiaPdf();
            var database = new DxfDatabase("AC1032");
            DxfDBDictionary underlays = DxfPdfDefinition.CreatePdfDictionary(database);
            var definition = new DxfPdfDefinition(pdf, "1");
            underlays.SetAt("skia - Page_1", definition);
            var underlay = new DxfPdfUnderlay(definition);
            database.ModelSpace.AppendEntity(underlay);
            underlay.ScaleFactors = new GeoVector3(25.4, 25.4, 25.4);
            var circle = new DxfCircle(new GeoPoint3(500, 500, 0), new GeoVector3(0, 0, 1), 5);
            database.ModelSpace.AppendEntity(circle);
            handle = underlay.Handle;
            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(database, Path.Combine(_folder, "host.dxf")));
            return model;
        }

        [Fact]
        public void APdfWrittenBySkiaSharp_IsReadAsItsFillsItsStrokeItsTextAndItsPicture()
        {
            PdfDocument document = PdfDocument.OpenFile(SkiaPdf());
            Assert.Equal(2, document.Pages.Count);
            Assert.Equal(200.0, document.Pages[0].Width, 6);
            Assert.Equal(100.0, document.Pages[0].Height, 6);

            PdfScene scene = document.Scene(1);

            // Skia writes the rectangle as a filled path in red, and the line as a stroked one in blue.
            PdfFill rectangle = Assert.Single(scene.Items.OfType<PdfFill>(), f => f.Rgb == 0xFF0000);
            double[] ring = rectangle.Rings[0];
            Assert.Equal(20.0, ring.Where((v, i) => i % 2 == 0).Min(), 3);
            Assert.Equal(120.0, ring.Where((v, i) => i % 2 == 0).Max(), 3);
            PdfStroke line = Assert.Single(scene.Items.OfType<PdfStroke>());
            Assert.Equal(0x0000FF, line.Rgb);
            Assert.Equal(2.0, line.Width, 3);

            // The word: the outlines of its embedded font, five letters as rings of black fills, or words in the font of the name.
            int letters =
                scene.Items.OfType<PdfFill>().Count(f => f.Rgb == 0x000000) + scene.Items.OfType<PdfWords>().Count();
            Assert.True(letters >= 1, "no text was read: " + string.Join(", ", scene.Dropped));

            // The picture of 4 by 4 pixels, a PNG or a JPEG, on the square (150, 50) to (190, 90).
            PdfPicture picture = Assert.Single(scene.Items.OfType<PdfPicture>());
            Assert.Equal(40.0, Math.Abs(picture.Matrix[0]), 3);
            Assert.Equal(150.0, picture.Matrix[4], 3);
        }

        [Fact]
        public void APageOfAPdf_IsDrawnAsAThumbnail_WithItsRedRectangleInIt()
        {
            BitmapSource thumbnail = CadStylePreview.PdfPage(SkiaPdf(), 1, 200, 100);
            Assert.NotNull(thumbnail);
            Assert.Equal(200, thumbnail.PixelWidth);
            var pixels = new byte[200 * 100 * 4];
            thumbnail.CopyPixels(pixels, 200 * 4, 0);
            bool red = false;
            bool white = false;

            for (int i = 0; i < pixels.Length; i += 4)
            {
                red |= pixels[i + 2] > 200 && pixels[i + 1] < 60 && pixels[i] < 60;
                white |= pixels[i] > 240 && pixels[i + 1] > 240 && pixels[i + 2] > 240;
            }

            Assert.True(red, "no red in the thumbnail");
            Assert.True(white, "the page is not on white");
            Assert.Null(CadStylePreview.PdfPage(SkiaPdf(), 5, 20, 10));
            Assert.Null(CadStylePreview.PdfPage(Path.Combine(_folder, "missing.pdf"), 1, 20, 10));
        }

        [Fact]
        public void TheAttachPdfDialog_ListsThePagesWithTheirSizes_AndAttachesTheOnesChosen()
        {
            string pdf = SkiaPdf();
            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(new DxfDatabase("AC1032"), Path.Combine(_folder, "empty.dxf")));
            var request = new CadAttachRequest(model.Document, CadAttachKind.Pdf);
            var dialog = new AttachReferenceViewModel(request) { FileName = pdf };

            Assert.True(dialog.IsPdf);
            Assert.False(dialog.IsXref);
            Assert.False(dialog.IsImage);
            Assert.Equal("Attach PDF Underlay", dialog.Title);
            Assert.Equal(2, dialog.PageCount);
            Assert.Equal(new[] { "Page 1", "Page 2" }, dialog.Pages.Select(p => p.Title));
            Assert.Equal("2.78 x 1.39 in", dialog.Pages[0].Size);
            Assert.NotNull(dialog.Pages[0].Thumbnail);
            Assert.Equal(new[] { 1 }, request.SelectedPages);
            Assert.True(dialog.CanOk);
            Assert.Contains("2 pages", dialog.UnitSecond);
            Assert.NotNull(dialog.Preview);

            dialog.SelectPages(new[] { 2, 1, 2 });
            Assert.Equal(new[] { 1, 2 }, request.SelectedPages);
            dialog.SelectPages(new int[0]);
            Assert.False(dialog.CanOk);
            dialog.SelectPages(new[] { 2 });

            // The Unit box: the inches of the page read as inches, in a drawing of the default unit.
            Assert.Contains("Inch", dialog.Units);
            dialog.SelectedUnit = "Inch";
            double factor = CadCommands.PdfUnitScale("Inch", model.Document.Database.Insunits);
            Assert.Equal(BlockNumbers.Text(factor), dialog.ScaleX);

            dialog.SpecifyPointOnScreen = false;
            dialog.X = "10";
            dialog.Y = "20";
            dialog.SpecifyScaleOnScreen = false;
            dialog.SpecifyRotationOnScreen = false;
            dialog.Close(CadDialogAction.Ok);
            Assert.Equal(CadDialogAction.Ok, request.Action);
            Assert.Equal(new GeoPoint3(10, 20, 0), request.Point);

            dialog.FileName = Path.Combine(_folder, "notes.txt");
            Assert.NotEmpty(dialog.Error);
            Assert.False(dialog.CanOk);
        }

        [Fact]
        public void PdfAttach_WithTheDialogAnswered_PlacesTheUnderlaysOfThePagesChosen()
        {
            string pdf = SkiaPdf();
            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(new DxfDatabase("AC1032"), Path.Combine(_folder, "target.dxf")));
            model.ShowAttachReference = dialog =>
            {
                dialog.FileName = pdf;
                dialog.SelectPages(new[] { 1, 2 });
                dialog.SpecifyPointOnScreen = false;
                dialog.X = "100";
                dialog.Y = "0";
                dialog.SpecifyScaleOnScreen = false;
                dialog.ScaleX = "25.4";
                dialog.SpecifyRotationOnScreen = false;
                dialog.Close(CadDialogAction.Ok);
                return true;
            };

            model.Execute("PDFATTACH");

            List<DxfPdfUnderlay> underlays = model.Document.ModelSpace.Block.OfType<DxfPdfUnderlay>().ToList();
            Assert.Equal(2, underlays.Count);
            Assert.Equal(new[] { "1", "2" }, underlays.Select(u => u.NameOfSheet).OrderBy(n => n));
            Assert.All(underlays, u => Assert.Equal(25.4, u.ScaleFactors.X, 9));
            Assert.All(underlays, u => Assert.Equal(100.0, u.Position.X, 9));
        }

        [Fact]
        public void ThePdfLayersDialog_ListsTheLayers_AndWritesTheOnesSwitchedOffAgainstThePdfsOwnState()
        {
            var layers = new List<PdfLayer>
            {
                new PdfLayer
                {
                    Id = 1,
                    Name = "Walls",
                    IsOn = true,
                },
                new PdfLayer
                {
                    Id = 2,
                    Name = "Notes",
                    IsOn = false,
                },
            };
            var request = new CadPdfLayersRequest(
                "plan - Page_1",
                new[] { "Walls", "Notes" },
                new Dictionary<string, bool>(),
                layers
            );
            var dialog = new PdfLayersViewModel(request);

            Assert.Equal("PDF Layers", dialog.Title);
            Assert.Equal(new[] { "Walls", "Notes" }, dialog.Rows.Select(r => r.Name));
            Assert.Equal(new[] { true, false }, dialog.Rows.Select(r => r.IsOn));
            Assert.Equal(new[] { "On", "Off" }, dialog.Rows.Select(r => r.State));

            dialog.Rows[0].IsOn = false;
            dialog.Rows[1].IsOn = true;
            dialog.Close(CadDialogAction.Ok);
            Assert.Equal(new Dictionary<string, bool> { ["Walls"] = false, ["Notes"] = true }, request.Overrides);

            // Back to the PDF's own state: nothing is overridden.
            dialog = new PdfLayersViewModel(request);
            dialog.Rows[0].IsOn = true;
            dialog.Rows[1].IsOn = false;
            dialog.Close(CadDialogAction.Ok);
            Assert.Empty(request.Overrides);

            dialog.SetAll(true);
            dialog.Close(CadDialogAction.Cancel);
            Assert.Equal(CadDialogAction.Cancel, request.Action);
        }

        [Fact]
        public void ThePdfUnderlayTab_IsShownWhileUnderlaysAloneAreSelected_AndItsValuesAreOneUndoStepEach()
        {
            MainViewModel model = WithPdf(out string handle);
            Assert.False(model.PdfTab.IsTabVisible);
            Assert.Null(model.PdfEditor);

            model.Document.Selection.Set(new[] { handle });

            Assert.True(model.PdfTab.IsTabVisible);
            Assert.Equal("PDF Underlay", model.PdfTab.TabHeader);
            Assert.Same(model.PdfTab, model.SelectedTab);
            PdfTabViewModel tab = model.PdfEditor;
            Assert.Equal("1 PDF underlay selected.", tab.Caption);
            Assert.Equal(0, tab.Fade);
            Assert.Equal(100, tab.Contrast);
            Assert.True(tab.AdjustColors);
            Assert.False(tab.Monochrome);
            Assert.True(tab.ShowUnderlay);

            tab.Fade = 40;
            Assert.Equal("PDFADJUST", model.Document.NextUndo.Name);
            tab.Contrast = 250;
            tab.Monochrome = true;
            tab.ShowUnderlay = false;
            var underlay = (DxfPdfUnderlay)model.Document.FindEntity(handle);
            Assert.Equal(40, underlay.Fade);
            Assert.Equal(100, underlay.Contrast);
            Assert.True(underlay.Monochrome);
            Assert.False(underlay.IsOn);

            Assert.True(model.Document.Undo());
            Assert.True(model.PdfEditor.ShowUnderlay);
            Assert.True(model.Document.Undo());
            Assert.False(model.PdfEditor.Monochrome);

            // Remove Clipping is for an underlay that has a boundary; the Frame box is PDFFRAME.
            Assert.False(tab.RemoveClipCommand.CanExecute(null));
            Assert.Equal(
                new[] { "Hide Frames", "Display and Plot Frames", "Display but Not Plot Frames" },
                tab.FrameNames
            );
            Assert.Equal("Display and Plot Frames", tab.Frame);
            tab.Frame = "Hide Frames";
            Assert.Equal(0, model.Document.PdfFrame);

            model.Document.Selection.Set(
                new string[] { handle, model.Document.ModelSpace.Block.OfType<DxfCircle>().Single().Handle }
            );
            Assert.False(model.PdfTab.IsTabVisible);
        }

        [Fact]
        public void ThePaletteListsAPdfUnderlay_ShowsItsPageAndDetachesItWithItsUnderlays()
        {
            MainViewModel model = WithPdf(out string handle);
            var palette = new ExternalReferencesViewModel(model);
            ReferenceRowViewModel row = Assert.Single(palette.Rows, r => r.IsPdf);

            Assert.Equal("skia - Page_1", row.Name);
            Assert.Equal("PDF", row.TypeName);
            Assert.Equal("Loaded", row.StatusText);
            palette.Selected = row;
            palette.IsPreview = true;
            Assert.NotNull(palette.Preview);

            palette.UnloadCommand.Execute(null);
            Assert.Equal("Unloaded", palette.Rows.Single(r => r.IsPdf).StatusText);
            palette.ReloadCommand.Execute(null);
            Assert.Equal("Loaded", palette.Rows.Single(r => r.IsPdf).StatusText);

            palette.Selected = palette.Rows.Single(r => r.IsPdf);
            palette.DetachCommand.Execute(null);
            Assert.DoesNotContain(palette.Rows, r => r.IsPdf);
            Assert.Null(model.Document.FindEntity(handle) as DxfPdfUnderlay);
            Assert.Equal("-PDFUNDERLAY", model.Document.NextUndo.Name);
        }
    }
}
