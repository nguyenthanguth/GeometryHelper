using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Dxf.Viewer.Wpf;
using GeometryHelper.Geometry;
using SkiaSharp;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// Images of C18b: a PNG, JPEG or BMP attached by IMAGEATTACH and -IMAGE at the size AutoCAD 2021 gives it, drawn as its
    /// picture with its frame, clipped by IMAGECLIP, its frames switched by IMAGEFRAME, its transparency by TRANSPARENCY,
    /// unloaded, reloaded, listed and detached in AutoCAD's words.
    /// </summary>
    public class ImageCommandTests : IDisposable
    {
        private readonly string _folder = Path.Combine(
            Path.GetTempPath(),
            "gk",
            "image-" + Guid.NewGuid().ToString("N")
        );

        public ImageCommandTests()
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

        /// <summary>
        /// A PNG of 40 by 30 pixels, transparent but for a red disc 20 across at its centre, saved at 3 779 pixels a metre as
        /// Windows saves one of 96 dots an inch, or with no resolution.
        /// </summary>
        private string Png(string name, bool resolution = true)
        {
            string path = Path.Combine(_folder, name);

            using (var bitmap = new SKBitmap(new SKImageInfo(40, 30, SKColorType.Rgba8888, SKAlphaType.Unpremul)))
            {
                bitmap.Erase(SKColors.Transparent);

                using (var canvas = new SKCanvas(bitmap))
                using (var red = new SKPaint { Color = SKColors.Red, IsAntialias = false })
                {
                    canvas.DrawCircle(20, 15, 10, red);
                }

                using (SKData data = bitmap.Encode(SKEncodedImageFormat.Png, 100))
                {
                    byte[] bytes = data.ToArray();
                    File.WriteAllBytes(path, resolution ? WithResolution(bytes, 3779) : bytes);
                }
            }

            return path;
        }

        /// <summary>A PNG with a pHYs chunk of a number of pixels a metre put after its IHDR.</summary>
        private static byte[] WithResolution(byte[] png, int perMetre)
        {
            var chunk = new List<byte> { 0, 0, 0, 9, (byte)'p', (byte)'H', (byte)'Y', (byte)'s' };
            byte[] value = { (byte)(perMetre >> 24), (byte)(perMetre >> 16), (byte)(perMetre >> 8), (byte)perMetre };
            chunk.AddRange(value);
            chunk.AddRange(value);
            chunk.Add(1);
            uint crc = Crc(chunk.Skip(4).ToArray());
            chunk.AddRange(new[] { (byte)(crc >> 24), (byte)(crc >> 16), (byte)(crc >> 8), (byte)crc });
            int afterHeader = 8 + 25;
            return png.Take(afterHeader).Concat(chunk).Concat(png.Skip(afterHeader)).ToArray();
        }

        private static uint Crc(byte[] bytes)
        {
            uint crc = 0xFFFFFFFF;

            foreach (byte b in bytes)
            {
                crc ^= b;

                for (int k = 0; k < 8; k++)
                {
                    crc = (crc & 1) != 0 ? 0xEDB88320 ^ (crc >> 1) : crc >> 1;
                }
            }

            return crc ^ 0xFFFFFFFF;
        }

        /// <summary>A metric drawing saved in the folder as host.dxf and opened.</summary>
        private CadDocument Host()
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            string path = Path.Combine(_folder, "host.dxf");
            database.Save(path);
            return CadDocument.Open(path);
        }

        private static List<T> Items<T>(CadDocument document)
            where T : DisplayItem2 => document.GetDisplay(document.ModelSpace).Items.OfType<T>().ToList();

        /// <summary>The logo attached at (10, 20) by -IMAGE, scale and rotation taken as offered.</summary>
        private CadDocument WithLogo(out CadCommandSession session)
        {
            string png = Png("logo.png");
            CadDocument document = Host();
            session = Session(document);
            Run(session, "-IMAGE", "A", png, "10,20", Enter, Enter);
            return document;
        }

        [Fact]
        public void APictureAttachedByTheTypedImage_IsSizedByItsResolution_AsAutoCadSizesIt_AndDrawnWithItsFrame()
        {
            CadDocument document = WithLogo(out CadCommandSession session);

            Assert.Contains("Enter image option [?/Detach/Path/Reload/Unload/Attach] <Attach>: A", session.History);
            Assert.Contains("Base image size: Width: 10.584811, Height: 7.938608, Millimeters", session.History);
            Assert.Contains("Specify scale factor <1>:", session.History);
            Assert.Contains("Specify rotation angle <0>:", session.History);

            DxfRasterImage image = Assert.Single(All<DxfRasterImage>(document.ModelSpace));
            Assert.Equal(".\\logo.png", image.ImageDef.SourceFileName);
            Assert.Equal(40.0, image.ImageWidth);
            Assert.Equal(30.0, image.ImageHeight);
            Assert.Equal(10.584811, image.Width, 6);
            Assert.Equal(7.938608, image.Height, 6);

            DisplayImage2 picture = Assert.Single(Items<DisplayImage2>(document));
            Assert.Equal(10.0, picture.X, 9);
            Assert.Equal(20.0, picture.Y, 9);
            Assert.Equal(10.584811, picture.Ux, 6);
            Assert.Equal(7.938608, picture.Vy, 6);
            Assert.Equal(Path.Combine(_folder, "logo.png"), picture.FilePath);
            Assert.Equal(image.Handle, picture.Handle);
            Assert.Single(Items<DisplayPolyline2>(document), p => p.Handle == image.Handle);

            CadExternalReference row = Assert.Single(document.Xrefs.GetReferences());
            Assert.True(row.IsImage);
            Assert.Equal("PNG", row.TypeName);
            Assert.Equal(CadReferenceStatus.Loaded, row.Status);

            Assert.Equal("-IMAGE", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Empty(All<DxfRasterImage>(document.ModelSpace));
            AssertReopens(document);
        }

        [Fact]
        public void APictureOfNoResolution_IsOneUnitWide_AndAPictureOfANameTaken_IsRefusedInAutoCadsWords()
        {
            string plain = Png("plain.png", false);
            string other = Png("other.png");
            CadDocument document = Host();
            CadCommandSession session = Session(document);

            Run(session, "-IMAGE", "A", plain, Enter, "2", "90");

            Assert.Contains("Base image size: Width: 1, Height: 0.75, Millimeters", session.History);
            DisplayImage2 picture = Assert.Single(Items<DisplayImage2>(document));
            Assert.Equal(0.0, picture.X, 9);
            Assert.Equal(0.0, picture.Ux, 9);
            Assert.Equal(2.0, picture.Uy, 9);
            Assert.Equal(-1.5, picture.Vx, 9);

            Run(session, "-IMAGE", "A", "plain=" + other);
            Assert.Contains(
                "Warning: The image name plain is already in use. Use the syntax <imagename>=<filename>.",
                session.History
            );
            Assert.Single(All<DxfRasterImage>(document.ModelSpace));
        }

        [Fact]
        public void TheImageDialog_AttachesAtThePointAndScaleGiven_AndReadsThePicturesSize()
        {
            string png = Png("logo.png");
            CadDocument document = Host();
            CadCommandSession session = Session(document);
            session.UiRequested += (sender, e) =>
            {
                if (e.Name == "IMAGEATTACH")
                {
                    var request = (CadAttachRequest)e.Payload;
                    Assert.Equal(CadAttachKind.Image, request.Kind);
                    request.FileName = png;
                    Assert.Equal("logo", request.Name);
                    Assert.Equal(40, request.PixelWidth);
                    Assert.Equal(30, request.PixelHeight);
                    Assert.True(request.HasResolution);
                    Assert.Equal(".\\logo.png", request.SavedPath);
                    request.SpecifyPointOnScreen = false;
                    request.Point = new GeoPoint3(100, 100, 0);
                    request.Scale = new GeoVector3(2, 2, 2);
                    request.Action = CadDialogAction.Ok;
                    e.Handled = true;
                }
            };

            Run(session, "IAT");

            DisplayImage2 picture = Assert.Single(Items<DisplayImage2>(document));
            Assert.Equal(100.0, picture.X, 9);
            Assert.Equal(2 * 10.584811, picture.Ux, 5);
        }

        [Fact]
        public void TheImagesListed_AreThoseAutoCadLists_ThenUnloadedReloadedAndDetached()
        {
            CadDocument document = WithLogo(out CadCommandSession session);

            Run(session, "-IMAGE", "?", Enter);
            int at = session.History.ToList().LastIndexOf("Image Name                       Instances  Path");
            Assert.True(at > 0);
            Assert.Equal("----------------                 ---------  ----------", session.History[at + 1]);
            Assert.Equal("logo                                     1  .\\logo.png", session.History[at + 2]);
            Assert.Equal("Total image definitions: 1", session.History[at + 3]);
            Assert.Equal("Total image instances: 1", session.History[at + 4]);

            Run(session, "-IMAGE", "U", "logo");
            Assert.Null(Assert.Single(Items<DisplayImage2>(document)).FilePath);
            Assert.Equal(CadReferenceStatus.Unloaded, Assert.Single(document.Xrefs.GetReferences()).Status);
            Assert.Single(Items<DisplayPolyline2>(document));

            Run(session, "-IMAGE", "R", "logo");
            Assert.Contains("logo loaded and relinked.", session.History);
            Assert.NotNull(Assert.Single(Items<DisplayImage2>(document)).FilePath);

            Run(session, "-IMAGE", "P", "logo", "logo.png");
            Assert.Contains("Old path:   .\\logo.png", session.History);
            Assert.Equal("logo.png", Assert.Single(All<DxfRasterImage>(document.ModelSpace)).ImageDef.SourceFileName);
            Assert.NotNull(Assert.Single(Items<DisplayImage2>(document)).FilePath);

            Run(session, "-IMAGE", "D", "logo");
            Assert.Empty(All<DxfRasterImage>(document.ModelSpace));
            Assert.Empty(document.Xrefs.GetReferences());
            Assert.True(document.Undo());
            Assert.Single(All<DxfRasterImage>(document.ModelSpace));
        }

        [Fact]
        public void APictureNotFound_IsItsFrameWithItsFilesName_AsAutoCadShowsIt()
        {
            CadDocument document = WithLogo(out _);
            File.Delete(Path.Combine(_folder, "logo.png"));
            document.Regen();

            Assert.Null(Assert.Single(Items<DisplayImage2>(document)).FilePath);
            Assert.Single(Items<DisplayPolyline2>(document));
            DisplayText2 name = Assert.Single(Items<DisplayText2>(document));
            Assert.Equal(".\\logo.png", name.Text);
            Assert.Equal(CadReferenceStatus.NotFound, Assert.Single(document.Xrefs.GetReferences()).Status);
        }

        [Fact]
        public void ImageClip_ClipsByARectangleOrAPolygon_InvertsAndDeletes_EachOneUndoStep()
        {
            CadDocument document = WithLogo(out CadCommandSession session);
            string handle = Assert.Single(All<DxfRasterImage>(document.ModelSpace)).Handle;

            Run(session, "ICL", new[] { handle }, Enter, Enter, P(12, 22), P(18, 26));

            Assert.Contains("Enter image clipping option [ON/OFF/Delete/New boundary] <New>:", session.History);
            Assert.Contains("Outside mode - Objects outside boundary will be hidden.", session.History);
            DisplayImage2 picture = Assert.Single(Items<DisplayImage2>(document));
            double[] ring = Assert.Single(picture.Clip);
            Assert.Equal(12.0, ring.Where((v, i) => i % 2 == 0).Min(), 6);
            Assert.Equal(18.0, ring.Where((v, i) => i % 2 == 0).Max(), 6);
            Assert.Equal(26.0, ring.Where((v, i) => i % 2 == 1).Max(), 6);
            Assert.False(picture.IsClipInverted);
            Assert.Equal(new DisplayBox(12, 22, 18, 26).MaxX, picture.Bounds.MaxX, 6);

            Run(session, "IMAGECLIP", new[] { handle }, "N", "Y", "I", "P", P(11, 21), P(19, 21), P(15, 27), "C");
            Assert.Contains("Inside mode - Objects inside boundary will be hidden.", session.History);
            picture = Assert.Single(Items<DisplayImage2>(document));
            Assert.True(picture.IsClipInverted);
            Assert.Equal(6, Assert.Single(picture.Clip).Length);

            Run(session, "IMAGECLIP", new[] { handle }, "D");
            Assert.Equal(10.584811 + 10.0, Assert.Single(Items<DisplayImage2>(document)).Bounds.MaxX, 6);

            Assert.True(document.Undo());
            Assert.True(Assert.Single(Items<DisplayImage2>(document)).IsClipInverted);
        }

        [Fact]
        public void ImageFrameZero_HidesTheFrames_AndTransparencyOn_LetsWhatIsUnderShow()
        {
            CadDocument document = WithLogo(out CadCommandSession session);
            string handle = Assert.Single(All<DxfRasterImage>(document.ModelSpace)).Handle;

            Run(session, "IMAGEFRAME", "0");
            Assert.Contains("Enter new value for IMAGEFRAME <1>: 0", session.History);
            Assert.Single(Items<DisplayImage2>(document));
            Assert.Empty(Items<DisplayPolyline2>(document));

            Run(session, "IMAGEFRAME", "2");
            Assert.Single(Items<DisplayPolyline2>(document));

            Run(session, "TRANSPARENCY", new[] { handle }, Enter, "ON");
            Assert.True(Assert.Single(Items<DisplayImage2>(document)).IsTransparent);
            Assert.True(document.Undo());
            Assert.False(Assert.Single(Items<DisplayImage2>(document)).IsTransparent);
        }

        [Fact]
        public void APicture_IsDrawnByItsPixels_ItsTransparentPartsShowingTheBackgroundUntilTransparencyIsOn()
        {
            CadDocument document = WithLogo(out CadCommandSession session);
            string handle = Assert.Single(All<DxfRasterImage>(document.ModelSpace)).Handle;
            var view = new CadView(200, 150);
            view.ZoomTo(new DisplayBox(10, 20, 10 + 10.584811, 20 + 7.938608));

            SKColor Pixel(double x, double y, int background)
            {
                using (var bitmap = new SKBitmap(200, 150))
                using (var canvas = new SKCanvas(bitmap))
                using (var renderer = new SkiaCanvasRenderer())
                {
                    renderer.Render(
                        canvas,
                        document,
                        document.ModelSpace,
                        view,
                        new CanvasOptions { Background = background }
                    );
                    return bitmap.GetPixel(
                        (int)Math.Round(view.DrawingToScreen.X(x, y)),
                        (int)Math.Round(view.DrawingToScreen.Y(x, y))
                    );
                }
            }

            double cx = 10 + 10.584811 / 2;
            double cy = 20 + 7.938608 / 2;
            Assert.Equal(SKColors.Red, Pixel(cx, cy, 0x203040));

            // A corner of the picture, transparent in the file, shows the background behind it in either case.
            Assert.Equal(new SKColor(0x20, 0x30, 0x40), Pixel(10.6, 20.6, 0x203040));
            Run(session, "TRANSPARENCY", new[] { handle }, Enter, "ON");
            Assert.Equal(new SKColor(0x20, 0x30, 0x40), Pixel(10.6, 20.6, 0x203040));
            Assert.Equal(SKColors.Red, Pixel(cx, cy, 0x203040));
        }
    }
}
