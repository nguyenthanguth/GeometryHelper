using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The reader of PDF underlays: a file built by the tests (no PDF of anyone's is in the repository) read for its pages and
    /// layers and run for what each page draws: paths, colours of every space, clips, forms, pictures, layers, text of
    /// embedded and unembedded fonts, files with cross-reference streams, damaged tables and passwords.
    /// </summary>
    public class PdfReadingTests
    {
        private static PdfScene SceneOf(
            string content,
            string resources = "",
            string box = "[0 0 200 100]",
            string extra = ""
        )
        {
            PdfDocument document = PdfDocument.Open(PdfTestFile.OnePage(content, resources, box, extra));
            return document.Scene(1);
        }

        private static void Near(double expected, double actual, double tolerance = 1E-6) =>
            Assert.True(Math.Abs(expected - actual) <= tolerance, expected + " was " + actual);

        [Fact]
        public void APageIsReadForItsSize_ItsCropBoxItsTurnAndItsUserUnit()
        {
            var file = new PdfTestFile();
            int pages = file.Reserve();
            int content = file.AddStream(string.Empty, "0 0 10 10 re f");
            int a = file.Add(
                "<< /Type /Page /Parent " + pages + " 0 R /MediaBox [0 0 200 100] /Contents " + content + " 0 R >>"
            );
            int b = file.Add(
                "<< /Type /Page /Parent "
                    + pages
                    + " 0 R /MediaBox [0 0 200 100] /CropBox [10 20 110 70] /Rotate 90 /Contents "
                    + content
                    + " 0 R >>"
            );
            int c = file.Add(
                "<< /Type /Page /Parent "
                    + pages
                    + " 0 R /MediaBox [0 0 72 36] /UserUnit 2 /Contents "
                    + content
                    + " 0 R >>"
            );
            file.Set(pages, "<< /Type /Pages /Count 3 /Kids [" + a + " 0 R " + b + " 0 R " + c + " 0 R] >>");
            int root = file.Add("<< /Type /Catalog /Pages " + pages + " 0 R >>");
            PdfDocument document = PdfDocument.Open(file.Build(root));

            Assert.Equal(3, document.Pages.Count);
            Near(200, document.Pages[0].Width);
            Near(100, document.Pages[0].Height);
            Near(200.0 / 72.0, document.Pages[0].WidthInches);

            // The crop box 100 by 50, turned a quarter: 50 wide and 100 high.
            Near(50, document.Pages[1].Width);
            Near(100, document.Pages[1].Height);

            // A page of 72 by 36 points of a user unit 2 is 2 inches by 1 inch.
            Near(2.0, document.Pages[2].WidthInches);
            Near(1.0, document.Pages[2].HeightInches);
        }

        [Fact]
        public void AFilledRectangleAndAStrokedLine_AreReadInPagePointsWithTheirColours()
        {
            PdfScene scene = SceneOf("1 0 0 rg 20 20 100 50 re f 0 0 1 RG 2 w 10 10 m 190 90 l S");
            var fill = Assert.IsType<PdfFill>(scene.Items[0]);
            Assert.Equal(0xFF0000, fill.Rgb);
            Assert.Equal(new double[] { 20, 20, 120, 20, 120, 70, 20, 70 }, fill.Rings[0]);

            var stroke = Assert.IsType<PdfStroke>(scene.Items[1]);
            Assert.Equal(0x0000FF, stroke.Rgb);
            Near(2.0, stroke.Width);
            Assert.Equal(new double[] { 10, 10, 190, 90 }, stroke.Lines[0]);
            Assert.False(stroke.Closed[0]);
        }

        [Fact]
        public void TheTransformationMatrix_MovesAndScalesWhatFollows_UntilTheStateIsRestored()
        {
            PdfScene scene = SceneOf("q 2 0 0 2 10 10 cm 0 0 10 10 re f Q 0 0 10 10 re f");
            Assert.Equal(new double[] { 10, 10, 30, 10, 30, 30, 10, 30 }, ((PdfFill)scene.Items[0]).Rings[0]);
            Assert.Equal(new double[] { 0, 0, 10, 0, 10, 10, 0, 10 }, ((PdfFill)scene.Items[1]).Rings[0]);
        }

        [Fact]
        public void ACurve_IsCutIntoPieces_ThatStartAndEndWhereItDoes()
        {
            PdfScene scene = SceneOf("0 0 m 10 0 10 10 0 10 c S");
            double[] line = ((PdfStroke)scene.Items[0]).Lines[0];
            Assert.True(line.Length >= 10, line.Length + " numbers");
            Near(0, line[0]);
            Near(0, line[1]);
            Near(0, line[line.Length - 2]);
            Near(10, line[line.Length - 1]);

            // The bulge reaches x = 0.75 * 10 at the middle of a cubic of control points at x = 10.
            double widest = Enumerable.Range(0, line.Length / 2).Max(i => line[2 * i]);
            Near(7.5, widest, 0.2);
        }

        [Fact]
        public void AClip_IsKeptWithWhatItClips_AndTheBoxOfARectangleIsSaid()
        {
            PdfScene scene = SceneOf("q 10 10 50 50 re W n 0 0 100 100 re f Q 0 0 5 5 re f");
            PdfClip clip = ((PdfFill)scene.Items[0]).Clip;
            Assert.NotNull(clip);
            Assert.True(clip.IsBox);
            Near(10, clip.MinX);
            Near(60, clip.MaxY);
            Assert.Null(((PdfFill)scene.Items[1]).Clip);
        }

        [Fact]
        public void TheColourSpaces_GiveTheirRgb()
        {
            // CMYK black, a CMYK cyan, an indexed colour and a spot colour through its tint transform.
            string resources =
                "/ColorSpace << /Idx [/Indexed /DeviceRGB 1 <FF0000 00FF00>] /Spot [/Separation /Gold /DeviceRGB << /FunctionType 2 /Domain [0 1] /C0 [1 1 1] /C1 [1 0.5 0] /N 1 >>] /Icc [/ICCBased 1 0 R] >>";
            var file = new PdfTestFile();
            int icc = file.AddStream("/N 1", new byte[0]);
            Assert.Equal(1, icc);
            file.Page(
                "0 0 0 1 k 0 0 1 1 re f 1 0 0 0 k 0 0 1 1 re f /Idx cs 1 sc 0 0 1 1 re f /Spot cs 1 scn 0 0 1 1 re f /Spot cs 0 scn 0 0 1 1 re f /Icc cs 0.5 sc 0 0 1 1 re f",
                resources,
                out int root
            );
            PdfScene scene = PdfDocument.Open(file.Build(root)).Scene(1);

            Assert.Equal(0x000000, ((PdfFill)scene.Items[0]).Rgb);
            Assert.Equal(0x00FFFF, ((PdfFill)scene.Items[1]).Rgb);
            Assert.Equal(0x00FF00, ((PdfFill)scene.Items[2]).Rgb);
            Assert.Equal(0xFF8000, ((PdfFill)scene.Items[3]).Rgb);
            Assert.Equal(0xFFFFFF, ((PdfFill)scene.Items[4]).Rgb);
            Assert.Equal(0x808080, ((PdfFill)scene.Items[5]).Rgb);
        }

        [Fact]
        public void AFormPlacedByDo_IsDrawnThroughItsMatrixAndItsBox()
        {
            var file = new PdfTestFile();
            int form = file.AddStream(
                "/Type /XObject /Subtype /Form /BBox [0 0 10 10] /Matrix [2 0 0 2 100 0]",
                "0 0 1 rg 0 0 20 20 re f"
            );
            file.Page("/Fm0 Do", "/XObject << /Fm0 " + form + " 0 R >>", out int root);
            PdfScene scene = PdfDocument.Open(file.Build(root)).Scene(1);

            var fill = Assert.IsType<PdfFill>(Assert.Single(scene.Items));
            Assert.Equal(0x0000FF, fill.Rgb);
            Assert.Equal(new double[] { 100, 0, 140, 0, 140, 40, 100, 40 }, fill.Rings[0]);

            // The form's box, 10 by 10 before its matrix, is a clip 20 by 20 from (100, 0).
            Assert.True(fill.Clip.IsBox);
            Near(100, fill.Clip.MinX);
            Near(120, fill.Clip.MaxX);
        }

        [Fact]
        public void TheLayersOfAPdf_AreListedWithTheirStateAndTheContentOnThem()
        {
            var file = new PdfTestFile();
            int walls = file.Add("<< /Type /OCG /Name (Walls) >>");
            int notes = file.Add("<< /Type /OCG /Name (Notes) >>");
            int pages = file.Reserve();
            int content = file.AddStream(
                string.Empty,
                "/OC /L1 BDC 1 0 0 rg 0 0 10 10 re f EMC /OC /L2 BDC 0 0 1 rg 20 0 10 10 re f EMC 0 1 0 rg 40 0 10 10 re f"
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
                    + " 0 R] /D << /OFF ["
                    + notes
                    + " 0 R] >> >> >>"
            );
            PdfDocument document = PdfDocument.Open(file.Build(root));

            Assert.Equal(new[] { "Walls", "Notes" }, document.Layers.Select(l => l.Name));
            Assert.Equal(new[] { true, false }, document.Layers.Select(l => l.IsOn));
            PdfScene scene = document.Scene(1);
            Assert.Equal(3, scene.Items.Count);
            Assert.Equal(new[] { walls }, scene.Items[0].Oc.Groups);
            Assert.Equal(new[] { notes }, scene.Items[1].Oc.Groups);
            Assert.Null(scene.Items[2].Oc);

            // Shown by the state the PDF opens with, and by one the user changes.
            Func<int, bool> asOpened = id => document.Layers.First(l => l.Id == id).IsOn;
            Assert.True(scene.Items[0].Oc.IsShown(asOpened));
            Assert.False(scene.Items[1].Oc.IsShown(asOpened));
            Assert.True(scene.Items[1].Oc.IsShown(id => true));
            Assert.False(scene.Items[0].Oc.IsShown(id => false));
        }

        [Fact]
        public void CompressedContent_AndEveryFilterOfStreams_AreDecoded()
        {
            PdfScene flate = SceneOf("1 0 0 rg 0 0 10 10 re f");
            Assert.Single(flate.Items);

            var file = new PdfTestFile();
            file.Page("0 1 0 rg 0 0 10 10 re f 0 0 1 rg 20 0 10 10 re f", string.Empty, out int root, flate: true);
            PdfScene packed = PdfDocument.Open(file.Build(root)).Scene(1);
            Assert.Equal(2, packed.Items.Count);
            Assert.Equal(0x00FF00, ((PdfFill)packed.Items[0]).Rgb);

            // ASCIIHex then ASCII85 of the same content text, and run length.
            string text = "1 0 0 rg 0 0 10 10 re f";
            var hex = new PdfTestFile();
            int stream = hex.AddStream(
                "/Filter /ASCIIHexDecode",
                BitConverter.ToString(Encoding.ASCII.GetBytes(text)).Replace("-", string.Empty) + ">"
            );
            AssertDecodes(hex, stream, text);

            var runs = new PdfTestFile();
            var run = new List<byte> { (byte)(text.Length - 1) };
            run.AddRange(Encoding.ASCII.GetBytes(text));
            run.Add(128);
            stream = runs.AddStream("/Filter /RunLengthDecode", run.ToArray());
            AssertDecodes(runs, stream, text);

            var lzw = new PdfTestFile();
            stream = lzw.AddStream("/Filter /LZWDecode", Lzw(Encoding.ASCII.GetBytes(text)));
            AssertDecodes(lzw, stream, text);
        }

        private static void AssertDecodes(PdfTestFile file, int stream, string expected)
        {
            int root = file.Add("<< /Type /Catalog /Pages 1 0 R >>");
            PdfFile opened = PdfFile.Open(file.Build(root));
            Assert.Equal(expected, Encoding.ASCII.GetString(opened.Decode(opened.Stream(new PdfRef(stream, 0)))));
        }

        /// <summary>The bytes as PDF's LZW (early change) has them: each byte a code of 9 bits, the table never reaching 512.</summary>
        private static byte[] Lzw(byte[] data)
        {
            var bits = new List<bool>();

            void Put(int code, int width)
            {
                for (int i = width - 1; i >= 0; i--)
                {
                    bits.Add(((code >> i) & 1) != 0);
                }
            }

            Put(256, 9);

            for (int i = 0; i < data.Length; i++)
            {
                Put(data[i], 9);

                // After 200 codes the table is cleared, so that the codes stay nine bits.
                if (i % 200 == 199)
                {
                    Put(256, 9);
                }
            }

            Put(257, 9);
            var bytes = new List<byte>();

            for (int i = 0; i < bits.Count; i += 8)
            {
                int v = 0;

                for (int k = 0; k < 8; k++)
                {
                    v = (v << 1) | (i + k < bits.Count && bits[i + k] ? 1 : 0);
                }

                bytes.Add((byte)v);
            }

            return bytes.ToArray();
        }

        [Fact]
        public void AFileWithACrossReferenceStreamAndAnObjectStream_IsRead()
        {
            var file = new PdfTestFile();
            file.Page("1 0 0 rg 0 0 10 10 re f", string.Empty, out int root);
            PdfDocument document = PdfDocument.Open(file.BuildCompressed(root));
            var fill = Assert.IsType<PdfFill>(Assert.Single(document.Scene(1).Items));
            Assert.Equal(0xFF0000, fill.Rgb);
        }

        [Fact]
        public void AFileWhoseTableIsBroken_IsRebuiltByScanningItsObjects()
        {
            var file = new PdfTestFile();
            file.Page("0 0 1 rg 0 0 10 10 re f", string.Empty, out int root);
            PdfDocument document = PdfDocument.Open(file.Build(root, brokenTable: true));
            Assert.Equal(0x0000FF, ((PdfFill)Assert.Single(document.Scene(1).Items)).Rgb);

            // No table and no trailer at all: the catalog is found by its type.
            byte[] bytes = PdfTestFile.OnePage("0 1 0 rg 0 0 10 10 re f");
            string text = Encoding.GetEncoding("iso-8859-1").GetString(bytes);
            text = text.Substring(0, text.IndexOf("xref", StringComparison.Ordinal));
            document = PdfDocument.Open(Encoding.GetEncoding("iso-8859-1").GetBytes(text));
            Assert.Equal(0x00FF00, ((PdfFill)Assert.Single(document.Scene(1).Items)).Rgb);
        }

        [Fact]
        public void APdfThatIsNotOne_AnEncryptedOne_AndOneCutShort_AreRefusedOrReadAsFarAsTheyGo()
        {
            Assert.Throws<PdfException>(() =>
                PdfDocument.Open(Encoding.ASCII.GetBytes("this is not a pdf file at all, only words"))
            );
            Assert.Throws<PdfException>(() => PdfDocument.Open(new byte[0]));

            var file = new PdfTestFile();
            file.Page("0 0 10 10 re f", string.Empty, out int root);
            PdfException encrypted = Assert.Throws<PdfException>(() =>
                PdfDocument.Open(file.Build(root, "/Encrypt << /Filter /Standard >>"))
            );
            Assert.Contains("password", encrypted.Message);

            byte[] whole = PdfTestFile.OnePage("1 0 0 rg 0 0 10 10 re f");
            var cut = new byte[whole.Length / 2];
            Array.Copy(whole, cut, cut.Length);

            // Cut short, the file is read by the objects it still has, or refused with a reason: it never throws anything else.
            try
            {
                PdfDocument document = PdfDocument.Open(cut);
                Assert.NotNull(document.Scene(1));
            }
            catch (PdfException) { }
        }

        [Fact]
        public void ATextInAFontThatIsNotEmbedded_IsKeptAsWordsInTheFontOfThatName()
        {
            PdfScene scene = SceneOf(
                "0 0 1 rg BT /F1 12 Tf 30 80 Td (Hello) Tj ET",
                "/Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> >>"
            );
            var words = Assert.IsType<PdfWords>(Assert.Single(scene.Items));
            Assert.Equal("Hello", words.Text);
            Assert.Equal("Arial", words.Family);
            Assert.Equal(0x0000FF, words.Rgb);
            Near(30, words.X);
            Near(80, words.Y);
            Near(12 * 0.716, words.Height, 1E-9);
            Near(0, words.Rotation);
            Assert.Contains(scene.Dropped, d => d.StartsWith("text of fonts", StringComparison.Ordinal));
        }

        [Fact]
        public void TheTextMatrix_TurnsTheTextAndTheSpacingAndTheArrayMoveThePen()
        {
            PdfScene scene = SceneOf(
                "BT /F1 10 Tf 0 1 -1 0 50 20 Tm [(A) -1000 (B)] TJ ET",
                "/Font << /F1 << /Type /Font /Subtype /TrueType /BaseFont /Courier /FirstChar 65 /LastChar 66 /Widths [600 600] >> >>"
            );
            Assert.Equal(2, scene.Items.Count);
            var a = (PdfWords)scene.Items[0];
            Near(Math.PI / 2, a.Rotation, 1E-9);
            Assert.Equal("Courier New", a.Family);
            Assert.Equal("A", a.Text);
            Near(50, a.X);
            Near(20, a.Y);

            // The text runs up the page: "A" is 600 thousandths of 10 along, then 1000 thousandths of an em of the gap: B starts 16 up
            // in words of its own.
            var b = (PdfWords)scene.Items[1];
            Assert.Equal("B", b.Text);
            Near(50, b.X);
            Near(36, b.Y);
        }

        [Fact]
        public void APictureInline_AndAPictureXObject_AreMadeIntoPngsOfTheirPixels()
        {
            // Two by two pixels of RGB: red, green, blue, white; drawn by an inline image on a 20 by 20 square.
            string content =
                "q 20 0 0 20 10 10 cm BI /W 2 /H 2 /BPC 8 /CS /RGB ID "
                + Encoding
                    .GetEncoding("iso-8859-1")
                    .GetString(new byte[] { 255, 0, 0, 0, 255, 0, 0, 0, 255, 255, 255, 255 })
                + "\nEI Q";
            PdfScene scene = SceneOf(content);
            var picture = Assert.IsType<PdfPicture>(Assert.Single(scene.Items));
            Assert.Equal(new double[] { 20, 0, 0, 20, 10, 10 }, picture.Matrix);

            using (var bitmap = new Bitmap(new MemoryStream(picture.Bytes)))
            {
                Assert.Equal(2, bitmap.Width);
                Assert.Equal(Color.FromArgb(255, 255, 0, 0).ToArgb(), bitmap.GetPixel(0, 0).ToArgb());
                Assert.Equal(Color.FromArgb(255, 0, 255, 0).ToArgb(), bitmap.GetPixel(1, 0).ToArgb());
                Assert.Equal(Color.FromArgb(255, 0, 0, 255).ToArgb(), bitmap.GetPixel(0, 1).ToArgb());
            }

            // An XObject of grey 4-bit samples, Flate, with a soft mask that clears the second row.
            var file = new PdfTestFile();
            int mask = file.AddStream(
                "/Type /XObject /Subtype /Image /Width 2 /Height 2 /ColorSpace /DeviceGray /BitsPerComponent 8",
                new byte[] { 255, 255, 0, 0 }
            );
            int image = file.AddStream(
                "/Type /XObject /Subtype /Image /Width 2 /Height 2 /ColorSpace /DeviceGray /BitsPerComponent 4 /SMask "
                    + mask
                    + " 0 R",
                new byte[] { 0x0F, 0xF0 }
            );
            file.Page("q 10 0 0 10 0 0 cm /Im0 Do Q", "/XObject << /Im0 " + image + " 0 R >>", out int root);
            var drawn = Assert.IsType<PdfPicture>(Assert.Single(PdfDocument.Open(file.Build(root)).Scene(1).Items));

            using (var bitmap = new Bitmap(new MemoryStream(drawn.Bytes)))
            {
                Assert.Equal(0, bitmap.GetPixel(0, 0).R);
                Assert.Equal(255, bitmap.GetPixel(0, 0).A);
                Assert.Equal(255, bitmap.GetPixel(1, 0).R);
                Assert.Equal(0, bitmap.GetPixel(0, 1).A);
            }
        }

        [Fact]
        public void AJpegIsKeptAsItIs_AndWhatTheReaderLeavesOutIsSaid()
        {
            var file = new PdfTestFile();
            byte[] jpeg = { 0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4, 0xFF, 0xD9 };
            int image = file.AddStream(
                "/Type /XObject /Subtype /Image /Width 8 /Height 8 /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode",
                jpeg
            );
            int cmyk = file.AddStream(
                "/Type /XObject /Subtype /Image /Width 8 /Height 8 /ColorSpace /DeviceCMYK /BitsPerComponent 8 /Filter /DCTDecode",
                jpeg
            );
            file.Page(
                "/Im0 Do /Im1 Do /Sh0 sh",
                "/XObject << /Im0 " + image + " 0 R /Im1 " + cmyk + " 0 R >>",
                out int root
            );
            PdfScene scene = PdfDocument.Open(file.Build(root)).Scene(1);

            var kept = Assert.IsType<PdfPicture>(Assert.Single(scene.Items));
            Assert.Equal(jpeg, kept.Bytes);
            Assert.Contains("CMYK JPEG pictures", scene.Dropped);
            Assert.Contains("shadings", scene.Dropped);
        }

        [Fact]
        public void TheOutlinesOfAnEmbeddedTrueTypeFont_AreDrawnAsFilledRings_ThroughTheTextMatrix()
        {
            // A font of one letter, A: a triangle (0, 0), (600, 0), (300, 700) in an em of 1000.
            List<GeometryHelper.UnitTest.Dxf.TrueTypeFile.Letter> letters =
                GeometryHelper.UnitTest.Dxf.TrueTypeFile.Letters();
            letters.First(l => l.Number == 'A').Record = Triangle();
            byte[] font = GeometryHelper.UnitTest.Dxf.TrueTypeFile.Font(letters);
            int glyphA = letters.FindIndex(l => l.Number == 'A');

            var file = new PdfTestFile();
            int program = file.AddStream("/Length1 " + font.Length, font);
            int descriptor = file.Add(
                "<< /Type /FontDescriptor /FontName /AAAAAA+Test /Flags 32 /FontFile2 " + program + " 0 R >>"
            );
            int cid = file.Add(
                "<< /Type /Font /Subtype /CIDFontType2 /BaseFont /AAAAAA+Test /CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >> /FontDescriptor "
                    + descriptor
                    + " 0 R /DW 600 /CIDToGIDMap /Identity >>"
            );
            int type0 = file.Add(
                "<< /Type /Font /Subtype /Type0 /BaseFont /AAAAAA+Test /Encoding /Identity-H /DescendantFonts ["
                    + cid
                    + " 0 R] >>"
            );
            int simple = file.Add(
                "<< /Type /Font /Subtype /TrueType /BaseFont /AAAAAA+Test /FirstChar 65 /LastChar 65 /Widths [600] /Encoding /WinAnsiEncoding /FontDescriptor "
                    + descriptor
                    + " 0 R >>"
            );
            string glyph = glyphA.ToString("X4");
            file.Page(
                "BT /F0 10 Tf 20 30 Td <" + glyph + "> Tj ET BT /F1 10 Tf 100 30 Td (A) Tj ET",
                "/Font << /F0 " + type0 + " 0 R /F1 " + simple + " 0 R >>",
                out int root
            );
            PdfScene scene = PdfDocument.Open(file.Build(root)).Scene(1);

            Assert.Equal(2, scene.Items.Count);
            Assert.All(scene.Items, item => Assert.IsType<PdfFill>(item));

            // The em is 1000 units and the size 10: the triangle is 6 wide and 7 high, from the pen's place.
            double[] ring = ((PdfFill)scene.Items[0]).Rings[0];
            Assert.Equal(6, ring.Length);
            Near(20, ring[0]);
            Near(30, ring[1]);
            Near(26, ring[2]);
            Near(30, ring[3]);
            Near(23, ring[4]);
            Near(37, ring[5]);

            // The simple font maps the character A through its table: the same triangle at its own place.
            double[] second = ((PdfFill)scene.Items[1]).Rings[0];
            Near(100, second[0]);
            Near(30, second[1]);
            Assert.Empty(scene.Dropped);
        }

        [Fact]
        public void AFileDamagedAtRandom_IsReadOrRefusedWithAReason_AndItsPagesAreNeverAFault()
        {
            var file = new PdfTestFile();
            int image = file.AddStream(
                "/Type /XObject /Subtype /Image /Width 2 /Height 2 /ColorSpace /DeviceGray /BitsPerComponent 8",
                new byte[] { 0, 255, 255, 0 },
                true
            );
            int form = file.AddStream(
                "/Type /XObject /Subtype /Form /BBox [0 0 50 50]",
                "1 0 0 rg 0 0 20 20 re f 5 5 m 40 40 l S"
            );
            file.Page(
                "q 10 0 0 10 5 5 cm /Im0 Do Q /Fm0 Do 0 0 1 RG 4 w [3 2] 0 d 0 0 m 50 20 80 40 100 0 c S BT /F1 12 Tf 10 80 Td (Hello) Tj ET 20 20 40 40 re W n 0 1 0 rg 0 0 100 100 re f",
                "/XObject << /Im0 "
                    + image
                    + " 0 R /Fm0 "
                    + form
                    + " 0 R >> /Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> >>",
                out int root,
                flate: true
            );
            byte[] good = file.Build(root);
            var random = new Random(20261117);

            for (int round = 0; round < 400; round++)
            {
                byte[] bytes = (byte[])good.Clone();

                for (int k = 0; k < 1 + round % 6; k++)
                {
                    bytes[random.Next(bytes.Length)] = (byte)random.Next(256);
                }

                if (round % 7 == 0)
                {
                    Array.Resize(ref bytes, random.Next(bytes.Length));
                }

                try
                {
                    PdfDocument document = PdfDocument.Open(bytes);

                    for (int page = 1; page <= document.Pages.Count; page++)
                    {
                        Assert.NotNull(document.Scene(page));
                    }
                }
                catch (PdfException)
                {
                    // Refused with a reason: the file is no PDF the reader can use.
                }
            }
        }

        /// <summary>The record in <c>glyf</c> of one triangle: (0, 0), (600, 0), (300, 700), all on the curve.</summary>
        private static byte[] Triangle()
        {
            var bytes = new List<byte>();

            void Short(int v)
            {
                bytes.Add((byte)(v >> 8));
                bytes.Add((byte)v);
            }

            Short(1); // one contour
            Short(0);
            Short(0);
            Short(600);
            Short(700);
            Short(2); // its last point
            Short(0); // no instructions
            bytes.AddRange(new byte[] { 1, 1, 1 }); // on curve, 16-bit x and y
            Short(0);
            Short(600);
            Short(-300);
            Short(0);
            Short(0);
            Short(700);
            return bytes.ToArray();
        }
    }
}
