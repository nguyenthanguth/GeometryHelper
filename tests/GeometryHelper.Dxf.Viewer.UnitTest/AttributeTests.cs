using System.IO;
using System.Linq;
using GeometryHelper.Dxf.Viewer.Wpf;
using SkiaSharp;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    public class AttributeTests
    {
        private static readonly string Folder = TestRun.Folder("render2");

        /// <summary>
        /// A title block TB, a frame 100 by 30 with the attribute definitions TITLE at (5, 18) and DRAWN at (5, 6), 5 and 3.5
        /// high, inserted at (20, 20) with the values BEAM B-12 and NGUYEN, and an invisible attribute SECRET.
        /// </summary>
        private static CadDocument TitleBlock(string insertScale = "1")
        {
            string block = TestDrawings.Block(
                "TB",
                TestDrawings.Line(0, 0, 100, 0),
                TestDrawings.Line(100, 0, 100, 30),
                TestDrawings.Line(100, 30, 0, 30),
                TestDrawings.Line(0, 30, 0, 0),
                TestDrawings.Entity(
                    "ATTDEF",
                    "0",
                    256,
                    null,
                    "10",
                    5.0,
                    "20",
                    18.0,
                    "30",
                    0.0,
                    "40",
                    5.0,
                    "1",
                    "TITLE?",
                    "3",
                    "Title",
                    "2",
                    "TITLE",
                    "70",
                    0
                ),
                TestDrawings.Entity(
                    "ATTDEF",
                    "0",
                    256,
                    null,
                    "10",
                    5.0,
                    "20",
                    6.0,
                    "30",
                    0.0,
                    "40",
                    3.5,
                    "1",
                    "NAME?",
                    "3",
                    "Drawn",
                    "2",
                    "DRAWN",
                    "70",
                    0
                )
            );
            string insert =
                "0\nINSERT\n5\nB1\n8\n0\n66\n1\n2\nTB\n10\n20\n20\n20\n30\n0\n41\n"
                + insertScale
                + "\n42\n"
                + insertScale
                + "\n43\n1\n"
                + TestDrawings.Entity(
                    "ATTRIB",
                    "0",
                    1,
                    "B2",
                    "10",
                    25.0,
                    "20",
                    38.0,
                    "30",
                    0.0,
                    "40",
                    5.0,
                    "1",
                    "BEAM B-12",
                    "2",
                    "TITLE",
                    "70",
                    0
                )
                + TestDrawings.Entity(
                    "ATTRIB",
                    "0",
                    256,
                    "B3",
                    "10",
                    25.0,
                    "20",
                    26.0,
                    "30",
                    0.0,
                    "40",
                    3.5,
                    "1",
                    "NGUYEN",
                    "2",
                    "DRAWN",
                    "70",
                    0
                )
                + TestDrawings.Entity(
                    "ATTRIB",
                    "0",
                    256,
                    "B4",
                    "10",
                    60.0,
                    "20",
                    26.0,
                    "30",
                    0.0,
                    "40",
                    3.5,
                    "1",
                    "HIDDEN",
                    "2",
                    "SECRET",
                    "70",
                    1
                )
                + "0\nSEQEND\n5\nB5\n8\n0\n";
            return CadDocument.From(TestDrawings.Drawing(null, block, insert), "titleblock.dxf");
        }

        [Fact]
        public void TheValuesOfATitleBlockAreDrawnWhereTheirAttributesStand_PickedAsTheInsert()
        {
            CadDocument document = TitleBlock();

            DisplayText2[] texts = document.GetDisplay(document.ModelSpace).Items.OfType<DisplayText2>().ToArray();

            Assert.Equal(new[] { "BEAM B-12", "NGUYEN" }, texts.Select(t => t.Text));
            Assert.All(texts, t => Assert.Equal("B1", t.Handle));
            Assert.Equal(25, texts[0].X, 12);
            Assert.Equal(38, texts[0].Y, 12);
            Assert.Equal(5, texts[0].Height, 12);
            Assert.Equal(0xFF0000, texts[0].Style.Rgb);
            Assert.Equal(new[] { "B1" }, document.GetPickIndex(document.ModelSpace).Pick(30, 39, 1.0));
        }

        [Fact]
        public void ATitleBlockIsRendered_ItsValuesShown_ItsDefinitionsAndInvisibleAttributesNot()
        {
            CadDocument document = TitleBlock();
            var view = new CadView(480, 240);
            view.ZoomTo(new DisplayBox(15, 15, 125, 55), 0.0);
            var bitmap = new SKBitmap((int)view.Width, (int)view.Height, SKColorType.Rgba8888, SKAlphaType.Premul);

            using (var canvas = new SKCanvas(bitmap))
            using (var renderer = new SkiaCanvasRenderer())
            {
                renderer.Render(canvas, document, document.ModelSpace, view, new CanvasOptions());
            }

            Directory.CreateDirectory(Folder);

            using (SKImage image = SKImage.FromBitmap(bitmap))
            using (SKData data = image.Encode(SKEncodedImageFormat.Png, 100))
            using (FileStream file = File.Create(Path.Combine(Folder, "title-block.png")))
            {
                data.SaveTo(file);
            }

            // The red title BEAM B-12 lies between x 25 and 60, y 38 and 43: red pixels there, none where SECRET would be.
            view.DrawingToPixel(25, 43, out double x0, out double y0);
            view.DrawingToPixel(60, 38, out double x1, out double y1);
            int red = 0;

            for (int y = (int)y0; y <= (int)y1; y++)
            {
                for (int x = (int)x0; x <= (int)x1; x++)
                {
                    SKColor c = bitmap.GetPixel(x, y);
                    red += c.Red > 150 && c.Green < 80 && c.Blue < 80 ? 1 : 0;
                }
            }

            Assert.True(red > 50, "red pixels of the title: " + red);
            Assert.DoesNotContain(document.GetDisplay(document.ModelSpace).Items.OfType<DisplayText2>(), t =>
                t.Text == "HIDDEN" || t.Text == "TITLE?" || t.Text == "NAME?"
            );
        }
    }
}
