using System;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Dxf.Viewer.Wpf;
using GeometryHelper.Geometry;
using SkiaSharp;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// Drawings drawn by <see cref="SkiaCanvasRenderer"/> into a bitmap with no window, their pixels read back; each scene is
    /// also written as a PNG to the folder render of the run (<see cref="TestRun"/>), deleted when the run ends.
    /// </summary>
    public class RenderTests
    {
        private static readonly string Folder = TestRun.Folder("render");

        /// <summary>Draws a space of a document into a bitmap and writes it as a PNG of the scene's name.</summary>
        private static SKBitmap Render(
            string scene,
            CadDocument document,
            CadSpace space,
            CadView view,
            CanvasOptions options = null
        )
        {
            var bitmap = new SKBitmap((int)view.Width, (int)view.Height, SKColorType.Rgba8888, SKAlphaType.Premul);

            using (var canvas = new SKCanvas(bitmap))
            using (var renderer = new SkiaCanvasRenderer())
            {
                renderer.Render(canvas, document, space, view, options ?? new CanvasOptions());
            }

            Directory.CreateDirectory(Folder);

            using (SKImage image = SKImage.FromBitmap(bitmap))
            using (SKData data = image.Encode(SKEncodedImageFormat.Png, 100))
            using (FileStream file = File.Create(Path.Combine(Folder, scene + ".png")))
            {
                data.SaveTo(file);
            }

            return bitmap;
        }

        /// <summary>A view of a canvas 200 by 150 at one pixel to the unit, the drawing's (0, 0) at its lower left corner.</summary>
        private static CadView PixelView(double width = 200, double height = 150)
        {
            var view = new CadView(width, height);
            view.ZoomTo(new DisplayBox(0, 0, width, height), 0.0);
            return view;
        }

        private static bool IsRed(SKColor c) => c.Red > 200 && c.Green < 60 && c.Blue < 60;

        private static bool IsBlack(SKColor c) => c.Red < 20 && c.Green < 20 && c.Blue < 20;

        private static bool IsLit(SKColor c) => !IsBlack(c);

        private static int Count(SKBitmap bitmap, int x0, int y0, int x1, int y1, Func<SKColor, bool> test)
        {
            int count = 0;

            for (int y = Math.Max(0, y0); y <= Math.Min(bitmap.Height - 1, y1); y++)
            {
                for (int x = Math.Max(0, x0); x <= Math.Min(bitmap.Width - 1, x1); x++)
                {
                    if (test(bitmap.GetPixel(x, y)))
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        [Fact]
        public void ARedLineLeavesRedPixelsOnItsPathAndBlackElsewhere()
        {
            // The line runs along y = 74.5, the middle of the pixel row 75 on the screen.
            CadDocument document = CadDocument.From(
                TestDrawings.Drawing(null, null, TestDrawings.Line(20, 74.5, 180, 74.5, color: 1, handle: "A1"))
            );

            using (SKBitmap bitmap = Render("red-line", document, document.ModelSpace, PixelView()))
            {
                for (int x = 22; x <= 178; x++)
                {
                    Assert.True(IsRed(bitmap.GetPixel(x, 75)), "pixel " + x);
                }

                Assert.Equal(0, Count(bitmap, 0, 0, 199, 73, IsLit));
                Assert.Equal(0, Count(bitmap, 0, 77, 199, 149, IsLit));
                Assert.Equal(0, Count(bitmap, 0, 0, 17, 149, IsLit));
            }
        }

        [Fact]
        public void ACircleLeavesItsColourOnItsRimAndNothingInsideIt()
        {
            CadDocument document = CadDocument.From(
                TestDrawings.Drawing(null, null, TestDrawings.Circle(100, 75, 50, color: 3, handle: "A1"))
            );

            using (SKBitmap bitmap = Render("circle", document, document.ModelSpace, PixelView()))
            {
                Func<SKColor, bool> green = c => c.Green > 100 && c.Red < 60 && c.Blue < 60;

                // The rim at its four quadrants, (150, 75), (50, 75), (100, 125) and (100, 25) of the drawing.
                Assert.True(Count(bitmap, 148, 73, 151, 76, green) > 0);
                Assert.True(Count(bitmap, 48, 73, 51, 76, green) > 0);
                Assert.True(Count(bitmap, 98, 23, 101, 26, green) > 0);
                Assert.True(Count(bitmap, 98, 123, 101, 126, green) > 0);
                Assert.Equal(0, Count(bitmap, 70, 45, 130, 105, IsLit));

                // On a diagonal, 50 / sqrt(2) out each way.
                Assert.True(Count(bitmap, 133, 38, 137, 42, green) > 0);
            }
        }

        [Fact]
        public void ASelectedEntityIsDrawnInTheSelectionColour()
        {
            CadDocument document = CadDocument.From(
                TestDrawings.Drawing(null, null, TestDrawings.Line(20, 74.5, 180, 74.5, color: 1, handle: "A1"))
            );
            var options = new CanvasOptions { Selected = new[] { "A1" } };

            using (SKBitmap bitmap = Render("selected", document, document.ModelSpace, PixelView(), options))
            {
                Func<SKColor, bool> selection = c =>
                    Math.Abs(c.Red - 0x3D) < 30 && Math.Abs(c.Green - 0x9B) < 30 && Math.Abs(c.Blue - 0xFF) < 30;

                Assert.True(Count(bitmap, 20, 73, 180, 77, selection) > 60);
                Assert.Equal(0, Count(bitmap, 0, 0, 199, 149, IsRed));
            }
        }

        [Fact]
        public void ALayerFrozenInAViewportDrawsNothingInsideItButStillDrawsInTheOther()
        {
            CadDocument document = LayoutTests.TwoViewports();

            // A red upright line on Notes through the middle of both views: x 500 of the model, from y -2000 to 2000.
            var line = new DxfLine(new GeoPoint3(500, -2000, 0), new GeoPoint3(500, 2000, 0));
            document.Database.ModelSpace.AppendEntity(line);
            line.Layer = "Notes";
            line.Color = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1);
            CadSpace plan = document.Spaces.Single(s => s.Name == "Plan");
            var view = new CadView(840, 594);
            view.ZoomTo(plan.PaperBounds, 0.05);

            using (SKBitmap bitmap = Render("viewports", document, plan, view))
            {
                // The first viewport, 150 by 100 about (100, 100); the second, 100 by 100 about (300, 150), with Notes frozen.
                PixelBox(view, 25, 50, 175, 150, out int ax0, out int ay0, out int ax1, out int ay1);
                PixelBox(view, 250, 100, 350, 200, out int bx0, out int by0, out int bx1, out int by1);
                Assert.True(Count(bitmap, ax0 + 2, ay0 + 2, ax1 - 2, ay1 - 2, IsRed) > 50);
                Assert.Equal(0, Count(bitmap, bx0 + 2, by0 + 2, bx1 - 2, by1 - 2, IsRed));

                // Nothing red outside the first viewport: the line is clipped to its frame.
                Assert.Equal(0, Count(bitmap, ax0 - 40, 0, ax1 + 40, ay0 - 3, IsRed));
            }
        }

        private static void PixelBox(
            CadView view,
            double x0,
            double y0,
            double x1,
            double y1,
            out int px0,
            out int py0,
            out int px1,
            out int py1
        )
        {
            view.DrawingToPixel(x0, y1, out double left, out double top);
            view.DrawingToPixel(x1, y0, out double right, out double bottom);
            px0 = (int)Math.Ceiling(left);
            py0 = (int)Math.Ceiling(top);
            px1 = (int)Math.Floor(right);
            py1 = (int)Math.Floor(bottom);
        }

        [Fact]
        public void TextLeavesPixelsFromItsBaselineUpToItsHeightAndNoneBelow()
        {
            // HELLO 20 high from (20, 60): capitals from y 60 to 80 of the drawing, rows 90 to 70 on the screen.
            CadDocument document = CadDocument.From(
                TestDrawings.Drawing(null, null, TestDrawings.Text(20, 60, 20, "HELLO", handle: "T1"))
            );

            using (SKBitmap bitmap = Render("text", document, document.ModelSpace, PixelView()))
            {
                Assert.True(Count(bitmap, 20, 70, 140, 90, IsLit) > 200);
                Assert.Equal(0, Count(bitmap, 0, 92, 199, 149, IsLit));
                Assert.Equal(0, Count(bitmap, 0, 0, 199, 67, IsLit));
                Assert.Equal(0, Count(bitmap, 0, 0, 18, 149, IsLit));

                // Its top row is the height of a capital above the baseline, give or take a pixel and a half.
                int top = Enumerable.Range(0, 150).First(y => Count(bitmap, 0, y, 199, y, IsLit) > 0);
                Assert.InRange(top, 68, 72);
            }
        }

        [Fact]
        public void ZoomingAboutThePixelOfALineKeepsTheLineOnThatPixel()
        {
            CadDocument document = CadDocument.From(
                TestDrawings.Drawing(
                    null,
                    null,
                    TestDrawings.Line(60.5, 0, 60.5, 150, color: 1, handle: "A1"),
                    TestDrawings.Circle(120, 70, 30, color: 3)
                )
            );
            CadView view = PixelView();
            view.DrawingToPixel(60.5, 100, out double px, out double py);

            using (SKBitmap before = Render("zoom-before", document, document.ModelSpace, view))
            {
                Assert.True(IsRed(before.GetPixel((int)px, (int)py)));
            }

            view.ZoomAt(px, py, 1.2 * 1.2 * 1.2);

            using (SKBitmap after = Render("zoom-after", document, document.ModelSpace, view))
            {
                Assert.True(IsRed(after.GetPixel((int)px, (int)py)));
                Assert.True(IsRed(after.GetPixel((int)px, (int)py + 20)));
            }
        }

        [Fact]
        public void APatternHatchIsDrawnByItsLinesInsideItsBoundary()
        {
            var database = new DxfDatabase("AC1018");
            var hatch = new DxfHatch();
            hatch.SetHatchPattern(DxfHatchPatternType.UserDefined, "_USER");
            hatch.PatternAngle = 0.0;
            hatch.PatternSpace = 10.0;
            hatch.AppendLoop(
                DxfHatchLoopTypes.External,
                new[]
                {
                    new GeoPoint2(20, 20.5),
                    new GeoPoint2(180, 20.5),
                    new GeoPoint2(180, 130.5),
                    new GeoPoint2(20, 130.5),
                },
                null
            );
            database.ModelSpace.AppendEntity(hatch);
            hatch.ColorIndex = 1;
            CadDocument document = CadDocument.From(database);

            using (SKBitmap bitmap = Render("hatch", document, document.ModelSpace, PixelView()))
            {
                // Lines at y 30, 40 and on from the pattern's origin, pixel rows 120, 110 and on; nothing between them, nor
                // along the boundary's upright sides at x 20 and 180.
                Func<SKColor, bool> reddish = c => c.Red > 100 && c.Green < 40 && c.Blue < 40;
                Assert.True(Count(bitmap, 100, 119, 100, 120, reddish) > 0);
                Assert.True(Count(bitmap, 100, 109, 100, 110, reddish) > 0);
                Assert.Equal(0, Count(bitmap, 10, 112, 190, 118, IsLit));
                Assert.Equal(0, Count(bitmap, 0, 0, 18, 149, IsLit));
            }
        }

        /// <summary>
        /// A scene of every kind of item at once, for the eye: arcs and a bulge, a dashed line, text held by its nine points,
        /// a filled solid and a hatch. It checks only that the parts are drawn where they are.
        /// </summary>
        [Fact]
        public void AShowcaseOfArcsDashesTextAndFillsDrawsEachWhereItIs()
        {
            DxfDatabase database = TestDrawings.DrawingWithLinetypes(
                TestDrawings.Linetype("DASHED", 12, -6),
                TestDrawings.Layer("Hidden", 4, linetype: "DASHED"),
                null,
                TestDrawings.Arc(60, 60, 40, 0, 135, handle: "A1"),
                TestDrawings.Circle(60, 60, 10, color: 1, handle: "A2"),
                TestDrawings.Line(10, 189.5, 390, 189.5, "Hidden", handle: "A3"),
                TestDrawings.Polyline("0", "A4", true, 140, 20, 0, 220, 20, 1, 220, 100, 0, 140, 100, 0),
                TestDrawings.Solid(260, 20, 380, 20, 260, 100, 380, 100, color: 5, handle: "A5"),
                TestDrawings.AlignedText(100, 150, 12, "MIDDLE", 1, 2, color: 2, handle: "T1"),
                TestDrawings.AlignedText(300, 150, 12, "RIGHT", 2, 0, color: 3, handle: "T2"),
                TestDrawings.Line(90, 150, 110, 150, color: 1),
                TestDrawings.Line(100, 140, 100, 160, color: 1),
                TestDrawings.Line(290, 150, 310, 150, color: 1),
                TestDrawings.Line(300, 140, 300, 160, color: 1)
            );
            CadDocument document = CadDocument.From(database);
            CadView view = PixelView(400, 200);

            using (SKBitmap bitmap = Render("showcase", document, document.ModelSpace, view))
            {
                // The dashed line has gaps along it; the solid is filled blue through; the right-aligned text ends at x 300.
                int dashes = Count(bitmap, 10, 10, 390, 10, IsLit);
                Assert.InRange(dashes, 200, 300);
                Assert.True(Count(bitmap, 270, 110, 370, 170, c => c.Blue > 200 && c.Red < 60) > 5000);
                Assert.Equal(0, Count(bitmap, 312, 30, 399, 60, c => c.Green > 200 && c.Red < 60));
                Assert.True(Count(bitmap, 240, 38, 298, 60, c => c.Green > 200 && c.Red < 60) > 30);
            }
        }

        [Fact]
        public void ASelectedLineIsDrawnSolidInTheSelectionColourWithNoGapsAlongIt()
        {
            CadDocument document = CadDocument.From(
                TestDrawings.Drawing(null, null, TestDrawings.Line(20, 74.5, 180, 74.5, color: 1, handle: "A1"))
            );
            var options = new CanvasOptions { Selected = new[] { "A1" } };

            using (SKBitmap bitmap = Render("selected-solid", document, document.ModelSpace, PixelView(), options))
            {
                Func<SKColor, bool> selection = c =>
                    Math.Abs(c.Red - 0x3D) < 30 && Math.Abs(c.Green - 0x9B) < 30 && Math.Abs(c.Blue - 0xFF) < 30;

                // Every pixel of the row along the line, not three in five as a dash of 6 and a gap of 4 would leave.
                for (int x = 22; x <= 178; x++)
                {
                    Assert.True(selection(bitmap.GetPixel(x, 75)), "pixel " + x);
                }
            }
        }

        [Fact]
        public void AHoveredLineIsDrawnTwoPixelsWideInItsColourTakenHalfwayToTheSelectionColour()
        {
            // The line along y = 75, the border of the rows 74 and 75 on the screen: 2 pixels wide, it fills both.
            CadDocument document = CadDocument.From(
                TestDrawings.Drawing(null, null, TestDrawings.Line(20, 75, 180, 75, color: 1, handle: "A1"))
            );
            var options = new CanvasOptions { Hover = "A1" };

            using (SKBitmap bitmap = Render("hovered", document, document.ModelSpace, PixelView(), options))
            {
                // Red (255, 0, 0) halfway to #3D9BFF is (158, 77, 127), at full strength, not red faded.
                foreach (int row in new[] { 74, 75 })
                {
                    SKColor c = bitmap.GetPixel(100, row);
                    Assert.InRange(c.Red, 155, 161);
                    Assert.InRange(c.Green, 74, 80);
                    Assert.InRange(c.Blue, 124, 130);
                }
            }
        }

        [Fact]
        public void AHatchWhoseLinesComeCloserThanTwoPixelsIsFilledWithAnEvenTintInsideItsBoundary()
        {
            // Lines 1.5 apart at a pixel to the unit: drawn one by one they would leave rows of two strengths.
            var database = new DxfDatabase("AC1018");
            var hatch = new DxfHatch();
            hatch.SetHatchPattern(DxfHatchPatternType.UserDefined, "_USER");
            hatch.PatternAngle = 0.0;
            hatch.PatternSpace = 1.5;
            hatch.AppendLoop(
                DxfHatchLoopTypes.External,
                new[]
                {
                    new GeoPoint2(20, 20),
                    new GeoPoint2(180, 20),
                    new GeoPoint2(180, 130),
                    new GeoPoint2(20, 130),
                },
                null
            );
            database.ModelSpace.AppendEntity(hatch);
            hatch.ColorIndex = 1;
            CadDocument document = CadDocument.From(database);

            using (SKBitmap bitmap = Render("hatch-tint", document, document.ModelSpace, PixelView()))
            {
                // Inside, every pixel the same tint of red, 1 / 1.5 of full; outside, nothing.
                SKColor tint = bitmap.GetPixel(100, 75);
                Assert.InRange(tint.Red, 160, 180);
                Assert.Equal(0, Count(bitmap, 25, 25, 175, 125, c => c != tint));
                Assert.Equal(0, Count(bitmap, 0, 0, 17, 149, IsLit));
                Assert.Equal(0, Count(bitmap, 183, 0, 199, 149, IsLit));
            }
        }

        [Fact]
        public void ACircleUnderHalfAPixelIsOnePixelOfItsColourAndATextUnderThreePixelsIsLeftOut()
        {
            // A circle 0.2 across at (100.25, 75.75), inside the pixel (100, 74); a text 2.8 high at (20, 40).
            CadDocument document = CadDocument.From(
                TestDrawings.Drawing(
                    null,
                    null,
                    TestDrawings.Circle(100.25, 75.75, 0.1, color: 3, handle: "C1"),
                    TestDrawings.Text(20, 40, 2.8, "SMALL", handle: "T1")
                )
            );

            using (SKBitmap bitmap = Render("tiny", document, document.ModelSpace, PixelView()))
            {
                Assert.Equal(new SKColor(0, 255, 0), bitmap.GetPixel(100, 74));
                Assert.Equal(1, Count(bitmap, 0, 0, 199, 149, IsLit));
            }
        }
    }
}
