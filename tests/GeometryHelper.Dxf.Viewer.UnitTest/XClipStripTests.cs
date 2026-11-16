using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// XCLIP's Clipdepth on a text, an MTEXT, a hatch and a solid that stand in a plane tilted out of the plan, as AutoCAD 2021
    /// draws them (PNGOUT in plan of each in a plane tilted 45 degrees about X, planes 20 and 5): what is left is the strip of
    /// the plan where the plane's height lies between the planes, glyph by glyph, here y from 5 to 20.
    /// </summary>
    public class XClipStripTests
    {
        private static readonly double Half = Math.Sqrt(0.5);

        /// <summary>The normal of a plane turned 45 degrees about X: its y axis rises as it runs along the plan, at the height of its y.</summary>
        private static readonly GeoVector3 Tilt = new GeoVector3(0, -Half, Half);

        private static string Text(double value) => value.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// A block GHC holding what the caller adds, inserted at (0, 0, 0) and clipped to the rectangle (-50, -50) to (350, 150)
        /// with the planes front 20 and back 5, seen from above.
        /// </summary>
        private static CadDocument Clipped(Action<DxfDatabase, DxfBlockTableRecord> fill, out string handle)
        {
            var database = new DxfDatabase("AC1027");
            var block = new DxfBlockTableRecord { Name = "GHC" };
            database.BlockTable.Add(block);
            fill(database, block);
            var insert = new DxfBlockReference(new GeoPoint3(0, 0, 0), block);
            database.ModelSpace.AppendEntity(insert);
            handle = insert.Handle;
            return Planes(database, handle);
        }

        private static CadDocument Planes(DxfDatabase database, string handle)
        {
            CadDocument document = CadDocument.From(database, "xclipstrip.dxf");
            CadCommandSession session = Session(document);
            Run(session, "XCLIP", new[] { handle }, Enter, "New boundary", "Rectangular", P(-50, -50), P(350, 150));
            Run(session, "XCLIP", new[] { handle }, Enter, "Clipdepth", "Distance", Text(20), "Distance", Text(5));
            return document;
        }

        private static DxfHatch Hatch(double x, double y, double width, double height, GeoVector3 normal)
        {
            var hatch = new DxfHatch();
            hatch.SetHatchPattern(DxfHatchPatternType.PreDefined, "SOLID");
            hatch.Normal = normal;
            hatch.AppendLoop(
                DxfHatchLoopTypes.External,
                new[]
                {
                    new GeoPoint2(x, y),
                    new GeoPoint2(x + width, y),
                    new GeoPoint2(x + width, y + height),
                    new GeoPoint2(x, y + height),
                },
                null
            );
            return hatch;
        }

        /// <summary>What is drawn of the reference, the frame of the clip left out.</summary>
        private static List<DisplayItem2> Drawn(CadDocument document, string handle) =>
            document.GetDisplay(document.ModelSpace).Items.Where(i => i.Handle == handle && !i.IsClipFrame).ToList();

        /// <summary>Whether the part of the plan an item is shown in holds a point of the plan.</summary>
        private static bool Shows(DisplayItem2 item, double x, double y) => item.BlockClip.Contains(x, y);

        /// <summary>The part of the plan the items are shown in is the strip from y 5 to 20, held by the boundary: nothing above or below it.</summary>
        private static void AssertStrip(IReadOnlyList<DisplayItem2> items, double from, double to)
        {
            Assert.NotEmpty(items);

            foreach (DisplayItem2 item in items)
            {
                Assert.NotNull(item.BlockClip);
                Assert.True(Shows(item, 100, 0.5 * (from + to)), "The middle of the strip is shown.");
                Assert.False(Shows(item, 100, from - 0.1), "Below the strip is not shown.");
                Assert.False(Shows(item, 100, to + 0.1), "Above the strip is not shown.");
                Assert.Equal(from, item.BlockClip.Bounds.MinY, 6);
                Assert.Equal(to, item.BlockClip.Bounds.MaxY, 6);
            }
        }

        [Fact]
        public void AHatchOfATiltedPlane_ThatCrossesThePlanes_IsKeptToTheStripBetweenThem()
        {
            // Probe (plan): a solid hatch of 100 by 40 in the plane tilted 45 degrees about X shows from y 5 to 20, the whole
            // 100 along x (the strip's pixels, 5.01 to 20.16 where the frame stands for -50 to 150).
            CadDocument document = Clipped(
                (database, block) => block.AppendEntity(Hatch(0, 0, 100, 40, Tilt)),
                out string handle
            );

            AssertStrip(Drawn(document, handle), 5.0, 20.0);
        }

        [Fact]
        public void ATextOfATiltedPlane_ThatCrossesThePlanes_IsKeptToTheStripBetweenThem()
        {
            // Probe (plan): the text HIJKL 40 high in the same plane is left glyph by glyph between y 5 and 20.
            CadDocument document = Clipped(
                (database, block) =>
                {
                    var text = new DxfDBText();
                    text.TextString = "HIJKL";
                    text.Height = 40;
                    text.Normal = Tilt;
                    text.Position = new GeoPoint3(0, 0, 0);
                    block.AppendEntity(text);
                },
                out string handle
            );

            AssertStrip(Drawn(document, handle), 5.0, 20.0);
        }

        [Fact]
        public void AnMTextOfATiltedPlane_ThatCrossesThePlanes_IsKeptToTheStripBetweenThem()
        {
            CadDocument document = Clipped(
                (database, block) =>
                {
                    var text = new DxfMText();
                    text.Contents = "MNOPQ";
                    text.TextHeight = 40;
                    text.Location = new GeoPoint3(0, 0, 0);
                    text.Normal = Tilt;
                    block.AppendEntity(text);
                },
                out string handle
            );

            AssertStrip(Drawn(document, handle), 5.0, 20.0);
        }

        [Fact]
        public void ASolidOfATiltedPlane_ThatCrossesThePlanes_IsKeptToTheStripBetweenThem()
        {
            // The solid's corners are given in the world, standing 45 degrees up from the plan: (0, 0, 0) to (80, 0, 0) and up
            // to (0, 28.28, 28.28) and (80, 28.28, 28.28), which the planes 20 and 5 leave from y 5 to 20.
            CadDocument document = Clipped(
                (database, block) =>
                    block.AppendEntity(
                        new DxfSolid(
                            new GeoPoint3(0, 0, 0),
                            new GeoPoint3(80, 0, 0),
                            new GeoPoint3(0, 40 * Half, 40 * Half),
                            new GeoPoint3(80, 40 * Half, 40 * Half)
                        )
                        {
                            Normal = Tilt,
                        }
                    ),
                out string handle
            );

            AssertStrip(Drawn(document, handle), 5.0, 20.0);
        }

        [Fact]
        public void AHatchBetweenThePlanes_IsDrawnWhole_AndOneAboveThemIsLeftOut()
        {
            // The plane's height is its y: the hatch from y 10 to 20 lies between the planes 5 and 20, the one from 30 to 40 over
            // the front plane.
            CadDocument document = Clipped(
                (database, block) =>
                {
                    block.AppendEntity(Hatch(0, 10 / Half, 100, 10 / Half, Tilt));
                    block.AppendEntity(Hatch(0, 30 / Half, 100, 10 / Half, Tilt));
                },
                out string handle
            );

            List<DisplayItem2> items = Drawn(document, handle);

            Assert.NotEmpty(items);
            Assert.All(items, item => Assert.Null(item.BlockClip.Parent));
            Assert.All(items, item =>
                Assert.True(item.Bounds.MaxY < 20.5 / 1.0, "Nothing of the hatch over the front plane.")
            );
        }

        [Fact]
        public void AHatchStandingUpright_ThatCrossesThePlanes_IsDrawnWhole()
        {
            // An upright plane is seen edge-on in the plan: the height does not change along any line of the plan it does not
            // stand on, so it is left as the display draws it.
            CadDocument document = Clipped(
                (database, block) =>
                {
                    var hatch = new DxfHatch();
                    hatch.SetHatchPattern(DxfHatchPatternType.PreDefined, "SOLID");
                    hatch.Normal = new GeoVector3(0, 1, 0);
                    hatch.AppendLoop(
                        DxfHatchLoopTypes.External,
                        new[]
                        {
                            new GeoPoint2(0, 0),
                            new GeoPoint2(100, 0),
                            new GeoPoint2(100, 40),
                            new GeoPoint2(0, 40),
                        },
                        null
                    );
                    block.AppendEntity(hatch);
                },
                out string handle
            );

            List<DisplayItem2> items = Drawn(document, handle);

            Assert.NotEmpty(items);
            Assert.All(items, item => Assert.Null(item.BlockClip.Parent));
        }

        [Fact]
        public void AHatchOfATiltedPlane_InABlockNestedInTheClippedOne_IsKeptToTheStripThroughThePlacing()
        {
            // INNER, placed at (100, 10, 0) in the block that is clipped, holds the tilted hatch: its height is z, and the
            // plan y it stands at 10 higher, so that the planes 5 and 20 leave it from y 15 to 30.
            var database = new DxfDatabase("AC1027");
            var inner = new DxfBlockTableRecord { Name = "INNER" };
            database.BlockTable.Add(inner);
            inner.AppendEntity(Hatch(0, 0, 100, 40, Tilt));
            var block = new DxfBlockTableRecord { Name = "GHC" };
            database.BlockTable.Add(block);
            block.AppendEntity(new DxfBlockReference(new GeoPoint3(100, 10, 0), inner));
            var insert = new DxfBlockReference(new GeoPoint3(0, 0, 0), block);
            database.ModelSpace.AppendEntity(insert);
            CadDocument document = Planes(database, insert.Handle);

            List<DisplayItem2> items = Drawn(document, insert.Handle);

            Assert.NotEmpty(items);

            foreach (DisplayItem2 item in items)
            {
                Assert.Equal(15.0, item.BlockClip.Bounds.MinY, 6);
                Assert.Equal(30.0, item.BlockClip.Bounds.MaxY, 6);
                Assert.True(Shows(item, 150, 20));
                Assert.False(Shows(item, 150, 12));
            }
        }

        [Fact]
        public void AHatchOfAFlatPlane_UnderATiltedClip_IsKeptToWhereTheTiltedPlanesMeetIt()
        {
            // A hatch lying flat in the plan at height 0, under a clip made in a system at (100, 50, 20) turned 30 degrees about
            // X (the rectangle (10, 10) to (90, 60) of it shows a flat block from y 73.09 to 130.83), with the planes front -30
            // and back -50: a point of the plan stands -0.5 y + 7.68 above the boundary's plane, which the planes meet at
            // y 75.36 and 115.36.
            var database = new DxfDatabase("AC1027");
            var block = new DxfBlockTableRecord { Name = "GHC" };
            database.BlockTable.Add(block);
            block.AppendEntity(Hatch(0, 0, 300, 300, new GeoVector3(0, 0, 1)));
            var insert = new DxfBlockReference(new GeoPoint3(0, 0, 0), block);
            database.ModelSpace.AppendEntity(insert);
            double cos30 = Math.Cos(Math.PI / 6.0);
            insert.ClipTo(
                new[] { new GeoPoint2(10, 10), new GeoPoint2(90, 60) },
                CadXClips.WorldToClip(
                    new CadUcs(new GeoPoint3(100, 50, 20), GeoVector3.XAxis, new GeoVector3(0.0, cos30, 0.5))
                )
            );
            insert.SpatialFilter.FrontClip = -30.0;
            insert.SpatialFilter.BackClip = -50.0;
            CadDocument document = CadDocument.From(database, "xclipstrip.dxf");

            List<DisplayItem2> items = Drawn(document, insert.Handle);

            Assert.NotEmpty(items);

            foreach (DisplayItem2 item in items)
            {
                Assert.Equal(2.0 * (25.0 - 20.0 * cos30 + 30.0), item.BlockClip.Bounds.MinY, 6);
                Assert.Equal(2.0 * (25.0 - 20.0 * cos30 + 50.0), item.BlockClip.Bounds.MaxY, 6);
            }
        }
    }
}
