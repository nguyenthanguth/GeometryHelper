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
    /// XCLIP's Clipdepth on an ellipse, a spline and a heavy polyline, 2D and 3D, as AutoCAD 2021 draws them (PNGOUT of
    /// accoreconsole, front view): each is cut at the front and back planes where it crosses one, as a line, a circle and a
    /// lightweight polyline are.
    /// </summary>
    public class XClipDepthMoreCurvesTests
    {
        private static readonly GeoVector3 Facing = new GeoVector3(0, 1, 0);

        /// <summary>
        /// A block GHC holding the shapes the caller adds, inserted at (0, 0, 0) and clipped to the rectangle (-20, -200) to
        /// (300, 300) with the planes front 10 and back -10, which a viewer sees from above: a shape in the plane of the
        /// world's X and Z, standing on its edge, is a segment of a line of the plan.
        /// </summary>
        private static CadDocument Clipped(Action<DxfBlockTableRecord> fill, out string handle) =>
            Clipped((database, block) => fill(block), out handle);

        private static CadDocument Clipped(Action<DxfDatabase, DxfBlockTableRecord> fill, out string handle)
        {
            var database = new DxfDatabase("AC1027");
            var block = new DxfBlockTableRecord { Name = "GHC" };
            database.BlockTable.Add(block);
            fill(database, block);
            var insert = new DxfBlockReference(new GeoPoint3(0, 0, 0), block);
            database.ModelSpace.AppendEntity(insert);
            handle = insert.Handle;
            CadDocument document = CadDocument.From(database, "xclipmorecurves.dxf");
            CadCommandSession session = Session(document);
            Run(session, "XCLIP", new[] { handle }, Enter, "New boundary", "Rectangular", P(-20, -200), P(300, 300));
            Run(
                session,
                "XCLIP",
                new[] { handle },
                Enter,
                "Clipdepth",
                "Distance",
                10.0.ToString(CultureInfo.InvariantCulture),
                "Distance",
                (-10.0).ToString(CultureInfo.InvariantCulture)
            );
            return document;
        }

        /// <summary>The boxes of what is drawn of the reference, the frame of the clip left out, by their x then y.</summary>
        private static List<DisplayBox> Drawn(CadDocument document, string handle) =>
            document
                .GetDisplay(document.ModelSpace)
                .Items.Where(i => i.Handle == handle && !i.IsClipFrame)
                .Select(i => i.Bounds)
                .OrderBy(b => b.MinX)
                .ThenBy(b => b.MinY)
                .ToList();

        private static void AssertBox(DisplayBox box, double minX, double maxX, double tolerance = 3)
        {
            Assert.Equal(minX, box.MinX, tolerance);
            Assert.Equal(maxX, box.MaxX, tolerance);
        }

        [Fact]
        public void AnEllipseOnItsEdge_IsCutAtThePlanes_ToTheTwoArcsBetweenThem()
        {
            // Probe (front view): the ellipse about (50, 0) of semi-axes 30 along x and 15 along z, planes 10 and -10, left of
            // it two arcs at the ends of its width, x 20 to 27.64 and 72.36 to 80: 50 -+ 30 cos(asin(10 / 15)).
            CadDocument document = Clipped(
                block =>
                    block.AppendEntity(
                        new DxfEllipse(new GeoPoint3(50, 100, 0), Facing, new GeoVector3(30, 0, 0), 0.5, 0, 2 * Math.PI)
                    ),
                out string handle
            );

            List<DisplayBox> boxes = Drawn(document, handle);

            // The arc at x 72.36 to 80 holds the start of the ellipse (its parameter 0, x 80) and is two, one each side of it.
            Assert.Equal(3, boxes.Count);
            AssertBox(boxes[0], 20.0, 50.0 - 30.0 * Math.Sqrt(1.0 - 4.0 / 9.0));
            AssertBox(boxes[1], 50.0 + 30.0 * Math.Sqrt(1.0 - 4.0 / 9.0), 80.0);
            AssertBox(boxes[2], 50.0 + 30.0 * Math.Sqrt(1.0 - 4.0 / 9.0), 80.0);
        }

        [Fact]
        public void AnEllipticalArc_IsCutAtThePlanes_ToItsEnds()
        {
            // The upper half of the same ellipse (z from 0 to 15, the part standing over the front plane at 10): what is left
            // is the part under z 10 at either end.
            var ellipse = new DxfEllipse(new GeoPoint3(50, 100, 0), Facing, new GeoVector3(30, 0, 0), 0.5, 0, Math.PI);
            CadDocument document = Clipped(block => block.AppendEntity(ellipse), out string handle);

            List<DisplayBox> boxes = Drawn(document, handle);

            // The sweep of an ellipse is counter-clockwise from its normal, which here points along y: it runs over the
            // top or the bottom, and either way two pieces stay, at the two ends.
            Assert.Equal(2, boxes.Count);
            AssertBox(boxes[0], 20.0, 50.0 - 30.0 * Math.Sqrt(1.0 - 4.0 / 9.0));
            AssertBox(boxes[1], 50.0 + 30.0 * Math.Sqrt(1.0 - 4.0 / 9.0), 80.0);
        }

        /// <summary>The cubic Bezier of the control points (10, 0), (30, 120), (50, -120), (90, 20) in X and Z, at y 100.</summary>
        private static DxfSpline Bezier() =>
            new DxfSpline(
                3,
                false,
                false,
                false,
                new[]
                {
                    new GeoPoint3(10, 100, 0),
                    new GeoPoint3(30, 100, 120),
                    new GeoPoint3(50, 100, -120),
                    new GeoPoint3(90, 100, 20),
                },
                new[] { 0.0, 0, 0, 0, 1, 1, 1, 1 },
                null,
                0,
                0
            );

        [Fact]
        public void ASpline_IsCutWhereItCrossesAPlane()
        {
            // The Bezier is x(t) = 10 (1-t)^3 + 90 (1-t)^2 t + 150 (1-t) t^2 + 90 t^3 and z(t) = 360 (1-t)^2 t - 360 (1-t) t^2 +
            // 20 t^3; the parts of it with z between -10 and 10 are found here by walking it finely.
            var expected = new List<double[]>();
            double fromX = double.NaN;
            double lastX = 0.0;
            const int steps = 200000;

            for (int i = 0; i <= steps; i++)
            {
                double t = (double)i / steps;
                double u = 1.0 - t;
                double x = 10 * u * u * u + 90 * u * u * t + 150 * u * t * t + 90 * t * t * t;
                double z = 360 * u * u * t - 360 * u * t * t + 20 * t * t * t;
                bool inside = z >= -10.0 && z <= 10.0;

                if (inside && double.IsNaN(fromX))
                {
                    fromX = x;
                }

                if (!inside && !double.IsNaN(fromX))
                {
                    expected.Add(new[] { Math.Min(fromX, lastX), Math.Max(fromX, lastX) });
                    fromX = double.NaN;
                }

                lastX = x;
            }

            if (!double.IsNaN(fromX))
            {
                expected.Add(new[] { Math.Min(fromX, lastX), Math.Max(fromX, lastX) });
            }

            CadDocument document = Clipped(block => block.AppendEntity(Bezier()), out string handle);

            List<DisplayBox> boxes = Drawn(document, handle);

            Assert.True(expected.Count >= 3, "the curve crosses the planes several times");
            Assert.Equal(expected.Count, boxes.Count);

            for (int i = 0; i < boxes.Count; i++)
            {
                AssertBox(boxes[i], expected[i][0], expected[i][1], 1);
            }
        }

        /// <summary>A heavy polyline in the plane of the world's X and Z at y 100 (the normal (0, 1, 0), its x axis the world's -x).</summary>
        private static DxfPolyline2d Heavy(double bulge, params double[] xz)
        {
            var points = new List<GeoPoint3>();
            var bulges = new List<double>();

            for (int i = 0; i < xz.Length / 2; i++)
            {
                points.Add(new GeoPoint3(-xz[2 * i], xz[2 * i + 1], 0));
                bulges.Add(i == 0 ? bulge : 0.0);
            }

            var polyline = new DxfPolyline2d(DxfPoly2dType.SimplePoly, points, 100.0, false, 0, 0, bulges);
            polyline.Normal = Facing;
            return polyline;
        }

        [Fact]
        public void AHeavyPolyline_IsCutWhereEachSegmentCrossesAPlane()
        {
            // Probe (blue in the front view): the zigzag (10, 0), (30, 30), (50, -30), (90, 20) in X and Z, planes 10 and -10:
            // x 10 to 16.667, 36.667 to 43.333 and 66 to 82, as a lightweight polyline of it is cut.
            CadDocument document = Clipped(
                block => block.AppendEntity(Heavy(0.0, 10, 0, 30, 30, 50, -30, 90, 20)),
                out string handle
            );

            List<DisplayBox> boxes = Drawn(document, handle);

            Assert.Equal(3, boxes.Count);
            AssertBox(boxes[0], 10.0, 10.0 + 20.0 / 3.0);
            AssertBox(boxes[1], 30.0 + 20.0 / 3.0, 30.0 + 40.0 / 3.0);
            AssertBox(boxes[2], 66.0, 82.0);
        }

        [Fact]
        public void AnArcSegmentOfAHeavyPolyline_IsCutWhereItsWaveCrossesAPlane()
        {
            // The vertices (10, 0) and (50, 0) with a bulge of 1 stand on a half circle of radius 20 over the top (z to 20):
            // the planes 10 and -10 leave 30 -+ 20 cos(30 degrees) at the ends.
            CadDocument document = Clipped(block => block.AppendEntity(Heavy(1.0, 10, 0, 50, 0)), out string handle);

            List<DisplayBox> boxes = Drawn(document, handle);

            Assert.Equal(2, boxes.Count);
            AssertBox(boxes[0], 10.0, 30.0 - 20.0 * Math.Cos(Math.PI / 6.0));
            AssertBox(boxes[1], 30.0 + 20.0 * Math.Cos(Math.PI / 6.0), 50.0);
        }

        [Fact]
        public void AClosedHeavyPolyline_IsCutAtThePlanes_ToTheSidesBetweenThem()
        {
            // The parallelogram (10, -30), (60, -30), (90, 30), (40, 30): its sides at -30 and 30 are out, the other two keep
            // x 70 to 80 and x 20 to 30.
            var points = new List<GeoPoint3>
            {
                new GeoPoint3(-10, -30, 0),
                new GeoPoint3(-60, -30, 0),
                new GeoPoint3(-90, 30, 0),
                new GeoPoint3(-40, 30, 0),
            };
            var polyline = new DxfPolyline2d(DxfPoly2dType.SimplePoly, points, 100.0, true, 0, 0, null);
            polyline.Normal = Facing;
            CadDocument document = Clipped(block => block.AppendEntity(polyline), out string handle);

            List<DisplayBox> boxes = Drawn(document, handle);

            Assert.Equal(2, boxes.Count);
            AssertBox(boxes[0], 20.0, 30.0);
            AssertBox(boxes[1], 70.0, 80.0);
        }

        [Fact]
        public void A3DPolyline_IsCutWhereEachSegmentCrossesAPlane()
        {
            // Probe (magenta in the front view): the zigzag of the heavy polyline above made of vertices in space, the same
            // three pieces.
            var polyline = new DxfPolyline3d(
                DxfPoly3dType.SimplePoly,
                new[]
                {
                    new GeoPoint3(10, 100, 0),
                    new GeoPoint3(30, 100, 30),
                    new GeoPoint3(50, 100, -30),
                    new GeoPoint3(90, 100, 20),
                },
                false
            );
            CadDocument document = Clipped(block => block.AppendEntity(polyline), out string handle);

            List<DisplayBox> boxes = Drawn(document, handle);

            Assert.Equal(3, boxes.Count);
            AssertBox(boxes[0], 10.0, 10.0 + 20.0 / 3.0);
            AssertBox(boxes[1], 30.0 + 20.0 / 3.0, 30.0 + 40.0 / 3.0);
            AssertBox(boxes[2], 66.0, 82.0);
        }

        [Fact]
        public void ADimensionOfTheBlock_IsDrawnByItsPicture_CutByThePlanes()
        {
            // Probe (yellow in the front view): a vertical dimension of z -30 to 30 shows its dimension line and extension lines
            // from z -10 to 10. Here the picture is the diagonal line (0, -30) to (60, 30) in X and Z, as a dimension line
            // turned, which the planes 10 and -10 leave from x 20 to 40.
            CadDocument document = Clipped(
                (database, block) =>
                {
                    var picture = new DxfBlockTableRecord { Name = "*D" };
                    database.BlockTable.Add(picture);
                    picture.AppendEntity(new DxfLine(new GeoPoint3(0, 100, -30), new GeoPoint3(60, 100, 30)));
                    var dimension = new DxfRotatedDimension(
                        0.0,
                        new GeoPoint3(0, 100, -30),
                        new GeoPoint3(60, 100, 30),
                        new GeoPoint3(30, 100, 0),
                        string.Empty,
                        null
                    );
                    dimension.DimBlock = picture;
                    block.AppendEntity(dimension);
                },
                out string handle
            );

            List<DisplayBox> boxes = Drawn(document, handle);

            Assert.Single(boxes);
            AssertBox(boxes[0], 20.0, 40.0);
        }

        [Fact]
        public void AnEllipseBetweenThePlanes_IsDrawnWhole_AndOneAboveThemIsLeftOut()
        {
            CadDocument document = Clipped(
                block =>
                {
                    block.AppendEntity(
                        new DxfEllipse(new GeoPoint3(50, 100, 0), Facing, new GeoVector3(30, 0, 0), 0.1, 0, 2 * Math.PI)
                    );
                    block.AppendEntity(
                        new DxfEllipse(
                            new GeoPoint3(50, 100, 40),
                            Facing,
                            new GeoVector3(30, 0, 0),
                            0.1,
                            0,
                            2 * Math.PI
                        )
                    );
                },
                out string handle
            );

            List<DisplayBox> boxes = Drawn(document, handle);

            Assert.Single(boxes);
            AssertBox(boxes[0], 20.0, 80.0);
        }
    }
}
