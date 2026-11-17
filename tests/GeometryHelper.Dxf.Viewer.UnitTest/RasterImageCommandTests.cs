using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using GeometryHelper.Dxf.Viewer.Wpf;
using GeometryHelper.Geometry;
using SkiaSharp;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The raster types AutoCAD 2021 attaches that the platform does not read (TGA, PCX, TIFF, DIB, RLE) and GIF, attached by
    /// -IMAGE and IMAGEATTACH at the size their resolution gives, drawn by their pixels, kept through a DXF and a DWG; a
    /// bitonal picture (one bit a pixel) drawn in the colour of the image and refused by IMAGEADJUST in AutoCAD's words;
    /// IMAGEQUALITY; and IMAGEFRAME 2, whose frames are shown and not plotted.
    /// </summary>
    public class RasterImageCommandTests : IDisposable
    {
        private readonly string _folder = Path.Combine(
            Path.GetTempPath(),
            "gk",
            "raster-" + Guid.NewGuid().ToString("N")
        );

        public RasterImageCommandTests()
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

        /// <summary>A picture of 40 by 30 pixels: a disc of radius 10 at its centre, the colour given, on white.</summary>
        private static byte[] Disc(byte red, byte green, byte blue)
        {
            var rgba = new byte[40 * 30 * 4];

            for (int y = 0; y < 30; y++)
            {
                for (int x = 0; x < 40; x++)
                {
                    bool inside = (x - 20) * (x - 20) + (y - 15) * (y - 15) < 100;
                    int at = (y * 40 + x) * 4;
                    rgba[at] = inside ? red : (byte)255;
                    rgba[at + 1] = inside ? green : (byte)255;
                    rgba[at + 2] = inside ? blue : (byte)255;
                    rgba[at + 3] = 255;
                }
            }

            return rgba;
        }

        private string Write(string name, byte[] bytes)
        {
            string path = Path.Combine(_folder, name);
            File.WriteAllBytes(path, bytes);
            return path;
        }

        /// <summary>The red disc as a file of one of the raster types, 254 dots an inch (0.1 mm a pixel) where the type has a resolution.</summary>
        private string Red(string name)
        {
            byte[] rgba = Disc(255, 0, 0);
            string extension = Path.GetExtension(name);

            switch (extension)
            {
                case ".tga":
                    return Write(name, RasterFormatTests.Tga(2, 24, 40, 30, rgba, false, false));
                case ".pcx":
                    return Write(
                        name,
                        RasterFormatTests.Pcx(8, 3, 40, 30, (x, y, p) => rgba[(y * 40 + x) * 4 + p], dpi: 254)
                    );
                case ".tif":
                case ".tiff":
                    return Write(
                        name,
                        RasterFormatTests.Tiff(
                            40,
                            RasterFormatTests.RgbRows(rgba, 40, 30),
                            new RasterFormatTests.TiffOptions
                            {
                                Compression = 32773,
                                Resolution = 254,
                                RowsPerStrip = 10,
                            }
                        )
                    );
                case ".dib":
                    return Write(
                        name,
                        RasterFormatTests.Dib(
                            24,
                            40,
                            30,
                            (x, y) =>
                                (rgba[(y * 40 + x) * 4] << 16)
                                | (rgba[(y * 40 + x) * 4 + 1] << 8)
                                | rgba[(y * 40 + x) * 4 + 2],
                            perMetre: 10000
                        )
                    );
                case ".rle":
                    return Write(name, Rle());
                default:
                    throw new ArgumentException(extension);
            }
        }

        /// <summary>The red disc as an RLE8 bitmap: index 0 white and 1 red, each row from the bottom as runs.</summary>
        private static byte[] Rle()
        {
            byte[] rgba = Disc(255, 0, 0);
            var data = new List<byte>();

            for (int row = 0; row < 30; row++)
            {
                int y = 29 - row;
                int x = 0;

                while (x < 40)
                {
                    int index = rgba[(y * 40 + x) * 4 + 1] == 0 ? 1 : 0;
                    int run = 1;

                    while (x + run < 40 && (rgba[(y * 40 + x + run) * 4 + 1] == 0 ? 1 : 0) == index)
                    {
                        run++;
                    }

                    data.Add((byte)run);
                    data.Add((byte)index);
                    x += run;
                }

                data.AddRange(new byte[] { 0, 0 });
            }

            data.AddRange(new byte[] { 0, 1 });
            return RasterFormatTests.Dib(
                8,
                40,
                30,
                null,
                new[] { 0xFFFFFF, 0xFF0000 },
                compression: 1,
                pixelBytes: data.ToArray(),
                fileHeader: true,
                perMetre: 10000
            );
        }

        /// <summary>The disc as a bitonal TIFF of 254 dots an inch: black (bit 0 of BlackIsZero) inside the disc, white round it.</summary>
        private string Bitonal(string name)
        {
            var rows = new byte[30][];

            for (int y = 0; y < 30; y++)
            {
                rows[y] = new byte[5];

                for (int x = 0; x < 40; x++)
                {
                    bool inside = (x - 20) * (x - 20) + (y - 15) * (y - 15) < 100;

                    if (!inside)
                    {
                        rows[y][x >> 3] |= (byte)(0x80 >> (x & 7));
                    }
                }
            }

            return Write(
                name,
                RasterFormatTests.Tiff(
                    40,
                    rows,
                    new RasterFormatTests.TiffOptions
                    {
                        Photometric = 1,
                        Samples = 1,
                        Bits = 1,
                        Compression = 32773,
                        Resolution = 254,
                        RowsPerStrip = 10,
                    }
                )
            );
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

        private static void Attach(CadCommandSession session, string path, string name, string at = "0,0")
        {
            Run(session, "-IMAGE", "A", name + "=" + path, at, Enter, Enter);
        }

        [Theory]
        [InlineData("logo.tif")]
        [InlineData("logo.tiff")]
        [InlineData("logo.pcx")]
        [InlineData("logo.dib")]
        [InlineData("logo.rle")]
        public void APictureOfAType_AutoCadAttachesButThePlatformDoesNotRead_IsSizedByItsResolution(string name)
        {
            string path = Red(name);
            CadDocument document = Host();
            CadCommandSession session = Session(document);

            Attach(session, path, "logo");

            // 40 by 30 pixels of 0.1 mm are 4 by 3 mm, as AutoCAD 2021 sized its own TIFF and PCX of 254 dots an inch.
            Assert.Contains("Base image size: Width: 4, Height: 3, Millimeters", session.History);
            DxfRasterImage image = Assert.Single(All<DxfRasterImage>(document.ModelSpace));
            Assert.Equal(40.0, image.ImageWidth);
            Assert.Equal(30.0, image.ImageHeight);
            Assert.Equal(4.0, image.Width, 6);
            Assert.Equal(3.0, image.Height, 6);
            Assert.EndsWith(name, image.ImageDef.SourceFileName);
        }

        [Fact]
        public void ATga_WhichHasNoResolution_IsOneUnitWide_AndAGifIsSizedFromItsHeader()
        {
            string tga = Red("logo.tga");
            byte[] gif = new byte[]
            {
                (byte)'G',
                (byte)'I',
                (byte)'F',
                (byte)'8',
                (byte)'9',
                (byte)'a',
                40,
                0,
                30,
                0,
                0,
                0,
                0,
                0x3B,
            };
            string gifPath = Write("logo.gif", gif);
            CadDocument document = Host();
            CadCommandSession session = Session(document);

            Attach(session, tga, "tga");
            Attach(session, gifPath, "gif", "10,0");

            Assert.Equal(2, session.History.Count(l => l == "Base image size: Width: 1, Height: 0.75, Millimeters"));
            List<DxfRasterImage> images = All<DxfRasterImage>(document.ModelSpace);
            Assert.Equal(2, images.Count);
            Assert.All(images, i => Assert.Equal(40.0, i.ImageWidth));
        }

        [Fact]
        public void APictureThatIsNoRasterType_IsRefused_AndASquareOfBytesWithAPictureNameToo()
        {
            string notes = Write("notes.txt", new byte[100]);
            string fake = Write("fake.tif", new byte[100]);
            CadDocument document = Host();
            CadCommandSession session = Session(document);

            Attach(session, notes, "notes");
            Attach(session, fake, "fake");

            Assert.Empty(All<DxfRasterImage>(document.ModelSpace));
        }

        [Theory]
        [InlineData("logo.tif")]
        [InlineData("logo.pcx")]
        [InlineData("logo.tga")]
        [InlineData("logo.dib")]
        [InlineData("logo.rle")]
        public void AnImageOfTheseTypes_IsDrawnByItsPixels_AsTheRedDiscOnWhite(string name)
        {
            string path = Red(name);
            CadDocument document = Host();
            CadCommandSession session = Session(document);
            Attach(session, path, "logo");
            double size = name.EndsWith(".tga") ? 1.0 : 4.0;
            var view = new CadView(200, 150);
            view.ZoomTo(new DisplayBox(0, 0, size, size * 0.75));

            SKColor Pixel(double x, double y)
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
                        new CanvasOptions { Background = 0x203040 }
                    );
                    return bitmap.GetPixel(
                        (int)Math.Round(view.DrawingToScreen.X(x, y)),
                        (int)Math.Round(view.DrawingToScreen.Y(x, y))
                    );
                }
            }

            Assert.Equal(SKColors.Red, Pixel(size / 2, size * 0.75 / 2));
            Assert.Equal(SKColors.White, Pixel(size * 0.05, size * 0.75 * 0.08));
        }

        [Fact]
        public void ABitonalPicture_IsDrawnInTheColourOfTheImage_ItsWhiteLettingWhatIsUnderShowOnlyWithTransparencyOn()
        {
            string path = Bitonal("scan.tif");
            CadDocument document = Host();
            CadCommandSession session = Session(document);

            // A green line under the lower edge of the picture, where the picture is white.
            document.Database.ModelSpace.AppendEntity(
                new DxfLine(new GeoPoint3(0.2, 0.3, 0), new GeoPoint3(3.8, 0.3, 0)) { ColorIndex = 3 }
            );
            Run(session, "CECOLOR", "1");
            Attach(session, path, "scan");
            string handle = Assert.Single(All<DxfRasterImage>(document.ModelSpace)).Handle;

            // The picture is known to be bitonal from its file, and to be drawn so.
            Assert.True(Assert.Single(Items<DisplayImage2>(document)).IsBitonal);

            var view = new CadView(400, 300);
            view.ZoomTo(new DisplayBox(0, 0, 4, 3));

            SKColor Pixel(double x, double y)
            {
                using (var bitmap = new SKBitmap(400, 300))
                using (var canvas = new SKCanvas(bitmap))
                using (var renderer = new SkiaCanvasRenderer())
                {
                    renderer.Render(
                        canvas,
                        document,
                        document.ModelSpace,
                        view,
                        new CanvasOptions { Background = 0x203040 }
                    );
                    return bitmap.GetPixel(
                        (int)Math.Round(view.DrawingToScreen.X(x, y)),
                        (int)Math.Round(view.DrawingToScreen.Y(x, y))
                    );
                }
            }

            // The black of the picture, inside its disc, is red as the image object is; the white round it is the background.
            Assert.Equal(SKColors.Red, Pixel(2, 1.5));
            Assert.Equal(new SKColor(0x20, 0x30, 0x40), Pixel(0.5, 2.5));

            // Where the line passes under the white: hidden by the picture until its transparency is on.
            Assert.Equal(new SKColor(0x20, 0x30, 0x40), Pixel(1, 0.3));
            Run(session, "TRANSPARENCY", new[] { handle }, Enter, "ON");
            SKColor under = Pixel(1, 0.3);
            Assert.True(under.Green > under.Red && under.Green > 0x80, "the green line shows through: " + under);
            Assert.Equal(SKColors.Red, Pixel(2, 1.5));
        }

        [Fact]
        public void ABitonalImage_CannotBeAdjusted_InAutoCadsWords_AndTheOthersOfTheSelectionStillCan()
        {
            string colour = Red("logo.tif");
            string scan = Bitonal("scan.tif");
            CadDocument document = Host();
            CadCommandSession session = Session(document);
            Attach(session, colour, "colour");
            Attach(session, scan, "scan", "10,0");
            List<DxfRasterImage> images = All<DxfRasterImage>(document.ModelSpace);
            DxfRasterImage colourImage = images.Single(i => i.ImageDef.SourceFileName.EndsWith("logo.tif"));
            DxfRasterImage scanImage = images.Single(i => i.ImageDef.SourceFileName.EndsWith("scan.tif"));

            // Only the scan: said, and the command ends with no question.
            Run(session, "-IMAGEADJUST", new[] { scanImage.Handle }, Enter);
            Assert.Contains("Bitonal images cannot be adjusted.", session.History);
            Assert.False(session.IsRunning);
            Assert.Equal(50, scanImage.Brightness);

            // Both: said once, then the option is asked of the colour image alone.
            Run(session, "-IMAGEADJUST", new[] { colourImage.Handle, scanImage.Handle }, Enter);
            Assert.Equal(2, session.History.Count(l => l == "Bitonal images cannot be adjusted."));
            Run(session, "B", "70");
            Assert.Equal(70, colourImage.Brightness);
            Assert.Equal(50, scanImage.Brightness);
            Assert.True(document.Undo());

            // The undo gives the drawing's entity back its old value; the one held before it is no longer the drawing's.
            Assert.Equal(50, Entity<DxfRasterImage>(document, colourImage.Handle).Brightness);
        }

        [Fact]
        public void ImageQuality_AsksHighOrDraft_KeepsItInTheVariablesOfImages_AndDrawsADraftImageWithSquarePixels()
        {
            string path = Red("logo.tif");
            CadDocument document = Host();
            CadCommandSession session = Session(document);
            Attach(session, path, "logo");
            Assert.False(Assert.Single(Items<DisplayImage2>(document)).IsDraft);

            Run(session, "IMAGEQUALITY");
            Assert.Equal("Enter image quality setting [High/Draft] <High>:", session.PromptText);
            Run(session, "Draft");
            Assert.False(session.IsRunning);
            var variables = (DxfRasterVariables)document.Database.NamedObjectsDictionary.GetAt("ACAD_IMAGE_VARS");
            Assert.Equal(DxfImageQuality.Draft, variables.ImageQuality);
            Assert.True(Assert.Single(Items<DisplayImage2>(document)).IsDraft);

            // The setting offered is the drawing's own; Enter keeps it.
            Run(session, "IMAGEQUALITY");
            Assert.Equal("Enter image quality setting [High/Draft] <Draft>:", session.PromptText);
            Run(session, Enter);
            Assert.Equal(DxfImageQuality.Draft, variables.ImageQuality);

            Assert.True(document.Undo());
            Assert.True(
                document.Database.NamedObjectsDictionary.Contains("ACAD_IMAGE_VARS")
                    ? (
                        (DxfRasterVariables)document.Database.NamedObjectsDictionary.GetAt("ACAD_IMAGE_VARS")
                    ).ImageQuality == DxfImageQuality.High
                    : true
            );
            Assert.False(Assert.Single(Items<DisplayImage2>(document)).IsDraft);
        }

        [Fact]
        public void ImageFrameTwo_ShowsTheFrame_ButLeavesItOutOfAPlot_AndOneIsPlotted()
        {
            string path = Red("logo.tif");
            CadDocument document = Host();
            CadCommandSession session = Session(document);
            Attach(session, path, "logo");

            Run(session, "IMAGEFRAME", "2");
            DisplayPolyline2 frame = Assert.Single(Items<DisplayPolyline2>(document));
            Assert.True(frame.IsNotPlotted);

            Run(session, "IMAGEFRAME", "1");
            Assert.False(Assert.Single(Items<DisplayPolyline2>(document)).IsNotPlotted);

            Run(session, "IMAGEFRAME", "0");
            Assert.Empty(Items<DisplayPolyline2>(document));
        }

        [Theory]
        [InlineData(".dxf")]
        [InlineData(".dwg")]
        public void EveryTypeAttached_IsKeptThroughASaveAndAnOpen_ItsDefinitionTheClipAndTheLevelsAlike(
            string extension
        )
        {
            string[] files = { Red("a.tif"), Red("b.pcx"), Red("c.tga"), Red("d.dib"), Red("e.rle"), Bitonal("f.tif") };
            CadDocument document = Host();
            CadCommandSession session = Session(document);

            for (int i = 0; i < files.Length; i++)
            {
                Attach(session, files[i], "img" + i, (i * 10) + ",0");
            }

            List<DxfRasterImage> before = All<DxfRasterImage>(document.ModelSpace);
            DxfRasterImage first = before.First();
            Run(session, "-IMAGEADJUST", new[] { first.Handle }, Enter, "Contrast", "80");
            Run(session, "TRANSPARENCY", new[] { before.Last().Handle }, Enter, "ON");
            Run(session, "IMAGEQUALITY", "Draft");
            string saved = Path.Combine(_folder, "round" + extension);

            document.SaveAs(saved);
            DxfDatabase read = extension == ".dwg" ? DwgReader.Open(saved) : DxfDatabase.Open(saved);
            List<DxfRasterImage> after = read.ModelSpace.OfType<DxfRasterImage>().ToList();

            Assert.Equal(files.Length, after.Count);
            DxfDBDictionary dictionary = DxfRasterImageDef.GetImageDictionary(read);

            for (int i = 0; i < files.Length; i++)
            {
                var definition = (DxfRasterImageDef)dictionary.GetAt("img" + i);
                Assert.EndsWith(Path.GetFileName(files[i]), definition.SourceFileName);
                Assert.Equal(40.0, definition.Size.X);
                Assert.Equal(30.0, definition.Size.Y);
            }

            Assert.Equal(80, after.First().Contrast);
            Assert.True(after.Last().ImageTransparency);
            Assert.False(after.First().ImageTransparency);
            Assert.Equal(
                DxfImageQuality.Draft,
                ((DxfRasterVariables)read.NamedObjectsDictionary.GetAt("ACAD_IMAGE_VARS")).ImageQuality
            );

            // The saved drawing, opened in the presenter, draws every picture it finds.
            CadDocument opened = CadDocument.From(read, "round" + extension);
            Assert.Equal(files.Length, Items<DisplayImage2>(opened).Count);
        }

        [Fact]
        public void TheSelectImageFileFilter_NamesAllTheTypes_AndEveryTypeOfItIsAPicture()
        {
            foreach (
                string name in new[]
                {
                    "a.bmp",
                    "a.rle",
                    "a.dib",
                    "a.gif",
                    "a.jpg",
                    "a.JPEG",
                    "a.pcx",
                    "a.png",
                    "a.tga",
                    "a.tif",
                    "a.TIFF",
                }
            )
            {
                Assert.True(CadImageFile.IsPicture(name), name);
            }

            Assert.False(CadImageFile.IsPicture("a.pdf"));
            Assert.False(CadImageFile.IsPicture("a.dwg"));
            Assert.StartsWith(
                "All image files|*.bmp;*.rle;*.dib;*.gif;*.jpg;*.jpeg;*.pcx;*.png;*.tga;*.tif;*.tiff",
                CadImageFile.OpenFilter
            );
        }
    }
}
