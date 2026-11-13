using System;
using System.Collections.Generic;
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
    /// OLE frames drawn: the picture a bitmap frame holds, red on its left half and blue on its right in these tests, laid on
    /// the frame, the frame drawn over it as OLEFRAME says, the frames of a space left out by OLEHIDE, and an object whose
    /// picture cannot be drawn shown as its frame with its kind written in it.
    /// </summary>
    public class OleFrameDisplayTests
    {
        /// <summary>
        /// A bitmap file of 24 bits, 4 pixels across and 2 up: the left two pixels of each row red, the right two blue, as a
        /// bottom-up file stores them.
        /// </summary>
        internal static byte[] RedAndBlueBitmap()
        {
            const int width = 4;
            const int height = 2;
            int row = (width * 3 + 3) & ~3;
            var file = new List<byte>();
            file.AddRange(new byte[] { (byte)'B', (byte)'M' });
            file.AddRange(BitConverter.GetBytes(14 + 40 + row * height));
            file.AddRange(new byte[4]);
            file.AddRange(BitConverter.GetBytes(14 + 40));
            file.AddRange(BitConverter.GetBytes(40));
            file.AddRange(BitConverter.GetBytes(width));
            file.AddRange(BitConverter.GetBytes(height));
            file.AddRange(BitConverter.GetBytes((short)1));
            file.AddRange(BitConverter.GetBytes((short)24));
            file.AddRange(new byte[24]);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    file.AddRange(x < 2 ? new byte[] { 0, 0, 255 } : new byte[] { 255, 0, 0 });
                }

                file.AddRange(new byte[row - width * 3]);
            }

            return file.ToArray();
        }

        /// <summary>
        /// A frame of the bitmap in model space, 40 wide and 20 high with its lower left corner at (10, 20), and with a layout the
        /// same frame is copied into, where <paramref name="withLayout"/>.
        /// </summary>
        internal static CadDocument Drawing(out DxfOle2Frame frame, byte[] picture = null, bool withLayout = false)
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            frame = new DxfOle2Frame(picture ?? RedAndBlueBitmap());
            database.ModelSpace.AppendEntity(frame);
            frame.Position3d = new DxfRectangle3d(
                new GeoPoint3(10, 40, 0),
                new GeoPoint3(50, 40, 0),
                new GeoPoint3(10, 20, 0),
                new GeoPoint3(50, 20, 0)
            );

            if (withLayout)
            {
                var layout = new DxfLayout { LayoutName = "Plan" };
                layout.AddToLayoutDictionary(database, null);
                layout.Initialize();
                layout.BlockTableRecord.AppendEntity(frame.Clone());
            }

            return CadDocument.From(database, "ole.dxf");
        }

        private static List<T> Items<T>(CadDocument document, CadSpace space = null)
            where T : DisplayItem2 => document.GetDisplay(space ?? document.ModelSpace).Items.OfType<T>().ToList();

        private static SKColor Pixel(
            CadDocument document,
            CadSpace space,
            double x,
            double y,
            int background = 0x203040
        )
        {
            var view = new CadView(200, 100);
            view.ZoomTo(new DisplayBox(10, 20, 50, 40));

            using (var bitmap = new SKBitmap(200, 100))
            using (var canvas = new SKCanvas(bitmap))
            using (var renderer = new SkiaCanvasRenderer())
            {
                renderer.Render(canvas, document, space, view, new CanvasOptions { Background = background });
                return bitmap.GetPixel(
                    (int)Math.Round(view.DrawingToScreen.X(x, y)),
                    (int)Math.Round(view.DrawingToScreen.Y(x, y))
                );
            }
        }

        [Fact]
        public void ABitmapFrame_IsDrawnAsItsPicture_ItsLeftHalfRedAndItsRightHalfBlue()
        {
            CadDocument document = Drawing(out _);

            Assert.Single(Items<DisplayImage2>(document));
            Assert.Equal(SKColors.Red, Pixel(document, document.ModelSpace, 20, 30));
            Assert.Equal(SKColors.Blue, Pixel(document, document.ModelSpace, 40, 30));
        }

        [Fact]
        public void ThePictureOfAFrame_LiesWhereItsFrameIs_AsADrawingPaintsIt()
        {
            CadDocument document = Drawing(out DxfOle2Frame frame);
            DisplayImage2 picture = Assert.Single(Items<DisplayImage2>(document));

            Assert.Equal(10.0, picture.Bounds.MinX, 9);
            Assert.Equal(20.0, picture.Bounds.MinY, 9);
            Assert.Equal(50.0, picture.Bounds.MaxX, 9);
            Assert.Equal(40.0, picture.Bounds.MaxY, 9);
            Assert.Equal(frame.Handle, picture.Handle);
        }

        [Fact]
        public void TheFrame_IsDrawnOverThePicture_AsOleframe2ShowsItWithoutPlottingIt()
        {
            CadDocument document = Drawing(out _);

            DisplayPolyline2 frame = Assert.Single(Items<DisplayPolyline2>(document));
            Assert.True(frame.IsNotPlotted);
        }

        [Fact]
        public void Oleframe1_PlotsTheFrame_AndOleframe0_DrawsNoFrameAtAll()
        {
            CadDocument document = Drawing(out DxfOle2Frame frame);
            CadCommandSession session = Session(document);

            Run(session, "OLEFRAME", "1");
            Assert.False(Assert.Single(Items<DisplayPolyline2>(document)).IsNotPlotted);

            Run(session, "OLEFRAME", "0");
            Assert.Empty(Items<DisplayPolyline2>(document));
            Assert.Single(Items<DisplayImage2>(document));

            // With no frame the picture is picked by a point on it.
            Assert.Equal(0.0, Assert.Single(Items<DisplayImage2>(document)).DistanceTo(20, 30), 9);
            Assert.True(double.IsPositiveInfinity(Assert.Single(Items<DisplayImage2>(document)).DistanceTo(60, 30)));

            Run(session, "OLEFRAME", "2");
            Assert.True(Assert.Single(Items<DisplayPolyline2>(document)).IsNotPlotted);
            Assert.Equal(frame.Handle, Assert.Single(Items<DisplayPolyline2>(document)).Handle);
        }

        [Fact]
        public void Oleframe_StartsAt2_AsAutoCadsDoes_AndRefusesWhatIsNotFrom0To2()
        {
            CadDocument document = Drawing(out _);
            CadCommandSession session = Session(document);

            Run(session, "OLEFRAME", Enter);
            Assert.Contains("Enter new value for OLEFRAME <2>:", session.History);

            Run(session, "OLEFRAME", "3");
            Assert.Contains("Requires an integer between 0 and 2.", session.History);
            Run(session, Esc);
        }

        [Fact]
        public void TheDrawingsOleframe_IsOneThingOfTheDrawing_SetByOneAndKeptByNoOther()
        {
            CadDocument one = Drawing(out _);
            CadDocument two = Drawing(out _);

            Run(Session(one), "OLEFRAME", "0");

            Assert.Empty(Items<DisplayPolyline2>(one));
            Assert.Single(Items<DisplayPolyline2>(two));
        }

        [Theory]
        [InlineData(0, true, true)]
        [InlineData(1, false, true)]
        [InlineData(2, true, false)]
        [InlineData(3, false, false)]
        public void Olehide_LeavesOutTheFramesOfModelSpaceOrOfALayout_AsItsValueSays(
            int value,
            bool inModel,
            bool inLayout
        )
        {
            CadDocument document = Drawing(out _, null, true);
            CadSpace plan = document.Spaces.Single(s => s.Name == "Plan");

            Run(Session(document), "OLEHIDE", value.ToString());

            Assert.Equal(inModel, Items<DisplayImage2>(document).Count == 1);
            Assert.Equal(inLayout, Items<DisplayImage2>(document, plan).Count == 1);
            Assert.Equal(inModel, Items<DisplayPolyline2>(document).Count == 1);
            Assert.Equal(inLayout, Items<DisplayPolyline2>(document, plan).Count == 1);
        }

        [Fact]
        public void Olehide_StartsAtNought_AndOlequality_At3()
        {
            CadCommandSession session = Session(Drawing(out _));

            Run(session, "OLEHIDE", Enter);
            Assert.Contains("Enter new value for OLEHIDE <0>:", session.History);
            Run(session, "OLEQUALITY", Enter);
            Assert.Contains("Enter new value for OLEQUALITY <3>:", session.History);
        }

        [Fact]
        public void AFrameWhosePictureCannotBeRead_IsDrawnAsItsFrameWithItsKindWrittenInIt()
        {
            CadDocument good = Drawing(out DxfOle2Frame frame);
            string text = good.Database.ToText();
            string signature = "D0CF11E0A1B11AE1";
            int at = text.IndexOf(signature, StringComparison.Ordinal);
            Assert.True(at > 0, "The compound file's signature sits within one line of the binary.");
            DxfDatabase damaged = DxfDatabase.Parse(
                text.Substring(0, at) + new string('0', signature.Length) + text.Substring(at + signature.Length)
            );
            CadDocument document = CadDocument.From(damaged, "damaged.dxf");

            Assert.Empty(Items<DisplayImage2>(document));
            Assert.Single(Items<DisplayPolyline2>(document));
            DisplayText2 words = Assert.Single(Items<DisplayText2>(document));
            Assert.Equal(damaged.ModelSpace.OfType<DxfOle2Frame>().Single().UserType, words.Text);
            Assert.False(string.IsNullOrEmpty(words.Text));
        }

        [Fact]
        public void TwoDrawingsWithFramesOfOneHandle_EachShowItsOwnPicture()
        {
            CadDocument one = Drawing(out _);
            byte[] noBlue = RedAndBlueBitmap();

            // The second bitmap loses its blue: its right half goes black, its left stays red.
            for (int i = 54; i < noBlue.Length; i += 3)
            {
                noBlue[i] = 0;
            }

            CadDocument two = Drawing(out _, noBlue);

            Assert.Equal(SKColors.Blue, Pixel(one, one.ModelSpace, 40, 30));
            Assert.Equal(SKColors.Black, Pixel(two, two.ModelSpace, 40, 30));
        }
    }
}
