using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Export;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Export.DxfLines;

namespace GeometryHelper.UnitTest.Export
{
    /// <summary>
    /// DXF written as AutoCAD writes it, by hand, read by <see cref="DxfReader"/>: each entity it maps, R12 polylines, the
    /// coordinate system of a tilted extrusion, layers, colours and the text of a file in its code page.
    /// </summary>
    public class DxfReaderEntityTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static DxfDocument Plan(string text) => DxfReader.Parse(text, new DxfReadOptions { PlanAs2D = true, Tolerance = Tol });

        private static DxfDocument Space(string text) => DxfReader.Parse(text, new DxfReadOptions { Tolerance = Tol });

        private static void Near(GeoPoint3 expected, GeoPoint3 actual, string what)
            => Assert.True(expected.DistanceTo(actual) <= 1E-12, what + ": " + expected + " read as " + actual);

        private static void Near(GeoPoint2 expected, GeoPoint2 actual, string what)
            => Assert.True(expected.DistanceTo(actual) <= 1E-12, what + ": " + expected + " read as " + actual);

        // ----- Points, lines, rays ---------------------------------------------------------------------------------------

        [Fact]
        public void APointLineRayAndXLine_InAutoCADsOwnStyle_AreThoseShapes()
        {
            string text = R2000(
                G(0, "POINT", 5, "1A", 330, "1F", 100, "AcDbEntity", 8, "0", 100, "AcDbPoint", 10, "1.5", 20, "2.5", 30, "0.0")
                + G(0, "LINE", 5, "1B", 330, "1F", 100, "AcDbEntity", 8, "0", 100, "AcDbLine", 10, "0.0", 20, "0.0", 30, "0.0", 11, "100.0", 21, "50.0", 31, "0.0")
                + G(0, "RAY", 5, "1C", 330, "1F", 100, "AcDbEntity", 8, "0", 100, "AcDbRay", 10, "5.0", 20, "5.0", 30, "0.0", 11, "0.6", 21, "0.8", 31, "0.0")
                + G(0, "XLINE", 5, "1D", 330, "1F", 100, "AcDbEntity", 8, "0", 100, "AcDbXline", 10, "0.0", 20, "0.0", 30, "7.0", 11, "0.0", 21, "0.0", 31, "1.0"));

            DxfDocument plan = Plan(text);

            Assert.Equal(new GeoPoint2(1.5, 2.5), Assert.IsType<GeoPoint2>(plan.Entities[0].Shape));
            Assert.Equal(new GeoPoint2(100, 50), Assert.IsType<GeoLine2>(plan.Entities[1].Shape).EndPoint);
            Assert.Equal(0.8, Assert.IsType<GeoRay2>(plan.Entities[2].Shape).Direction.Y);
            Assert.Equal(new GeoPoint3(0, 0, 7), Assert.IsType<GeoXLine3>(plan.Entities[3].Shape).Origin);
            Assert.Equal(new[] { "1A", "1B", "1C", "1D" }, plan.Entities.Select(e => e.Handle).ToArray());

            DxfDocument space = Space(text);
            Assert.IsType<GeoPoint3>(space.Entities[0].Shape);
            Assert.IsType<GeoLine3>(space.Entities[1].Shape);
            Assert.IsType<GeoRay3>(space.Entities[2].Shape);
        }

        // ----- Polylines -------------------------------------------------------------------------------------------------

        [Fact]
        public void AClosedLightweightPolylineWithBulges_IsALoopWithArcs_OfTheRightArea()
        {
            // A slot 80 long and 20 wide: four vertices, a bulge of 1 on the second and the fourth, closed by flag 1.
            string text = R2000(G(
                0, "LWPOLYLINE", 5, "2F", 330, "1F", 100, "AcDbEntity", 8, "0", 100, "AcDbPolyline", 90, 4, 70, 1, 43, "0.0",
                10, "0.0", 20, "0.0", 10, "60.0", 20, "0.0", 42, "1.0", 10, "60.0", 20, "20.0", 10, "0.0", 20, "20.0", 42, "1.0"));

            var loop = Assert.IsType<GeoPolygonArc2>(Assert.Single(Plan(text).Entities).Shape);
            Assert.Equal(new[] { 0.0, 1.0, 0.0, 1.0 }, loop.Bulges.ToArray());
            Assert.Equal(1200 + Math.PI * 100, loop.Area, 9);

            var inSpace = Assert.IsType<GeoPolygonArc3>(Assert.Single(Space(text).Entities).Shape);
            Assert.All(inSpace.Vertices, v => Assert.Equal(0.0, v.Z));
        }

        [Fact]
        public void AnOpenLightweightPolylineAtAnElevation_IsAChainInSpaceAtThatHeight_EvenWithPlanAs2D()
        {
            string text = R2000(G(0, "LWPOLYLINE", 8, "0", 100, "AcDbPolyline", 90, 3, 70, 0, 38, "12.5", 10, "0", 20, "0", 42, "0.5", 10, "10", 20, "0", 10, "10", 20, "10", 42, "0.7"));

            var chain = Assert.IsType<GeoPolylineArc3>(Assert.Single(Plan(text).Entities).Shape);
            Assert.All(chain.Vertices, v => Assert.Equal(12.5, v.Z));
            Assert.Equal(0.5, chain.Bulges[0]);
            Assert.Equal(0.0, chain.Bulges[2]);
        }

        [Fact]
        public void AnR12PolylineWithItsVerticesAndSeqend_NoHandlesNoSubclasses_IsALoopWithArcs()
        {
            // R12 writes no handles and no subclass markers; a 2D POLYLINE's own point is 0, 0 and its elevation.
            string text = R12(
                G(0, "POLYLINE", 8, "0", 66, 1, 10, "0.0", 20, "0.0", 30, "0.0", 70, 1)
                + G(0, "VERTEX", 8, "0", 10, "0.0", 20, "0.0", 30, "0.0", 42, "0.414213562373095")
                + G(0, "VERTEX", 8, "0", 10, "10.0", 20, "0.0", 30, "0.0")
                + G(0, "VERTEX", 8, "0", 10, "10.0", 20, "10.0", 30, "0.0")
                + G(0, "SEQEND", 8, "0")
                + G(0, "POLYLINE", 8, "0", 66, 1, 10, "0.0", 20, "0.0", 30, "0.0", 70, 8)
                + G(0, "VERTEX", 8, "0", 10, "0.0", 20, "0.0", 30, "0.0", 70, 32)
                + G(0, "VERTEX", 8, "0", 10, "5.0", 20, "0.0", 30, "3.0", 70, 32)
                + G(0, "VERTEX", 8, "0", 10, "5.0", 20, "5.0", 30, "6.0", 70, 32)
                + G(0, "SEQEND", 8, "0"));

            DxfDocument read = Plan(text);

            Assert.Equal("AC1009", read.Version);
            Assert.Equal(DxfUnits.Unitless, read.Units);
            Assert.Equal(2, read.Entities.Count);
            Assert.All(read.Entities, e => Assert.Null(e.Handle));

            var loop = Assert.IsType<GeoPolygonArc2>(read.Entities[0].Shape);
            Assert.Equal(3, loop.VertexCount);
            Assert.Equal(0.414213562373095, loop.Bulges[0]);

            var bar = Assert.IsType<GeoPolyline3>(read.Entities[1].Shape);
            Assert.Equal(new GeoPoint3(5, 5, 6), bar.EndPoint);
            Assert.Contains("VERTEX", read.Entities[1].Groups.Where(g => g.Code == 0).Select(g => g.Value));
            Assert.Equal("SEQEND", read.Entities[1].Groups.Last(g => g.Code == 0).Value);
        }

        [Fact]
        public void APolylineFittedWithASpline_IsTheSplineDrawn_NotTheFrame()
        {
            // Vertices flagged 16 are the frame the spline was fitted to; those flagged 8 are the curve AutoCAD draws.
            string text = R12(
                G(0, "POLYLINE", 8, "0", 66, 1, 10, "0", 20, "0", 30, "0", 70, 4)
                + G(0, "VERTEX", 8, "0", 10, "0", 20, "0", 70, 8)
                + G(0, "VERTEX", 8, "0", 10, "5", 20, "4", 70, 8)
                + G(0, "VERTEX", 8, "0", 10, "10", 20, "5", 70, 8)
                + G(0, "VERTEX", 8, "0", 10, "0", 20, "0", 70, 16)
                + G(0, "VERTEX", 8, "0", 10, "5", 20, "10", 70, 16)
                + G(0, "VERTEX", 8, "0", 10, "10", 20, "5", 70, 16)
                + G(0, "SEQEND", 8, "0"));

            var chain = Assert.IsType<GeoPolyline2>(Assert.Single(Plan(text).Entities).Shape);
            Assert.Equal(new[] { new GeoPoint2(0, 0), new GeoPoint2(5, 4), new GeoPoint2(10, 5) }, chain.Vertices.ToArray());
        }

        [Fact]
        public void AClosedPolylineOfTwoVertices_ACircleOfTwoHalves_IsALoopWithArcs_ItsFirstHalfCut_AtItsMiddle()
        {
            string text = R2000(G(0, "LWPOLYLINE", 8, "0", 100, "AcDbPolyline", 90, 2, 70, 1, 10, "0", 20, "0", 42, "1", 10, "20", 20, "0", 42, "1"));
            DxfDocument read = Plan(text);

            // The half circle from (0, 0) to (20, 0) bulging 1 runs under its chord; its middle is (10, -10), each quarter a bulge of tan(pi / 8).
            var loop = Assert.IsType<GeoPolygonArc2>(Assert.Single(read.Entities).Shape);
            Assert.Equal(3, loop.VertexCount);
            Assert.True(loop.Vertices[1].DistanceTo(new GeoPoint2(10, -10)) < 1E-12, loop.Vertices[1].ToString());
            Assert.Equal(new GeoPoint2(20, 0), loop.Vertices[2]);
            Assert.Equal(Math.Tan(Math.PI / 8), loop.Bulges[0], 15);
            Assert.Equal(1.0, loop.Bulges[2]);
            Assert.Equal(Math.PI * 100, loop.Area, 9);
            Assert.Equal(Math.PI * 20, loop.Length, 9);
            Assert.Empty(read.Warnings);

            var inSpace = Assert.IsType<GeoPolygonArc3>(Assert.Single(Space(text).Entities).Shape);
            Assert.Equal(Math.PI * 100, inSpace.Area, 9);
        }

        // ----- Circles, arcs, ellipses -----------------------------------------------------------------------------------

        [Fact]
        public void ACircleOnTheMirroredExtrusion_StandsWhereTheArbitraryAxisAlgorithmPutsIt()
        {
            // Of the extrusion (0, 0, -1) the x axis of the system is (-1, 0, 0) and its y axis (0, 1, 0): the centre (10, 20, 5)
            // of the system is (-10, 20, -5) of the world.
            string text = R2000(G(0, "CIRCLE", 8, "0", 100, "AcDbCircle", 10, "10", 20, "20", 30, "5", 40, "2.5", 210, "0", 220, "0", 230, "-1"));
            var circle = Assert.IsType<GeoCircle3>(Assert.Single(Plan(text).Entities).Shape);

            Assert.Equal(new GeoPoint3(-10, 20, -5), circle.Center);
            Assert.Equal(new GeoVector3(0, 0, -1), circle.Normal);
            Assert.Equal(2.5, circle.Radius);
        }

        [Fact]
        public void AnArcAndAPolylineOnTheExtrusionAlongX_AreInThePlaneXEqualsTheirElevation()
        {
            // Of the extrusion (1, 0, 0) the x axis of the system is the world's y and its y axis the world's z.
            string text = R2000(
                G(0, "ARC", 8, "0", 100, "AcDbCircle", 10, "0", 20, "0", 30, "7", 40, "10", 210, "1", 220, "0", 230, "0", 100, "AcDbArc", 50, "0", 51, "90")
                + G(0, "LWPOLYLINE", 8, "0", 100, "AcDbPolyline", 90, 3, 70, 0, 38, "7", 10, "0", 20, "0", 10, "10", 20, "0", 10, "10", 20, "5", 210, "1", 220, "0", 230, "0"));

            DxfDocument read = Plan(text);
            var arc = Assert.IsType<GeoArc3>(read.Entities[0].Shape);
            var chain = Assert.IsType<GeoPolyline3>(read.Entities[1].Shape);

            Near(new GeoPoint3(7, 0, 0), arc.Center, "the centre");
            Near(new GeoPoint3(7, 10, 0), arc.StartPoint, "the start");
            Near(new GeoPoint3(7, 0, 10), arc.EndPoint, "the end");
            Near(new GeoPoint3(7, 10 * Math.Cos(Math.PI / 4), 10 * Math.Sin(Math.PI / 4)), arc.MidPoint, "the middle");
            Assert.Equal(new[] { new GeoPoint3(7, 0, 0), new GeoPoint3(7, 10, 0), new GeoPoint3(7, 10, 5) }, chain.Vertices.ToArray());
        }

        [Fact]
        public void AnArcOnTheMirroredExtrusion_RunsClockwiseSeenFromAbove()
        {
            // AutoCAD writes a mirrored arc with the extrusion (0, 0, -1): from 0 to 90 degrees about down is from (-15, 0)
            // to (-10, 5) about the centre (-10, 0), clockwise seen from above.
            string text = R2000(G(0, "ARC", 8, "0", 100, "AcDbCircle", 10, "10", 20, "0", 30, "0", 40, "5", 210, "0", 220, "0", 230, "-1", 100, "AcDbArc", 50, "0", 51, "90"));
            var arc = Assert.IsType<GeoArc3>(Assert.Single(Space(text).Entities).Shape);

            // On the floor it is the arc of the plane, clockwise from 180 to 90 degrees about (-10, 0), as in space.
            var flat = Assert.IsType<GeoArc2>(Assert.Single(Plan(text).Entities).Shape);
            Assert.Equal(new GeoPoint2(-10, 0), flat.Center);
            Assert.True(flat.IsClockwise);
            Assert.True(flat.StartPoint.DistanceTo(new GeoPoint2(-15, 0)) < 1E-12 && flat.EndPoint.DistanceTo(new GeoPoint2(-10, 5)) < 1E-12, flat.StartPoint + " " + flat.EndPoint);

            Near(new GeoPoint3(-15, 0, 0), arc.StartPoint, "the start");
            Near(new GeoPoint3(-10, 5, 0), arc.EndPoint, "the end");
            Near(new GeoPoint3(-10 - 5 * Math.Cos(Math.PI / 4), 5 * Math.Sin(Math.PI / 4), 0), arc.MidPoint, "the middle");
            Assert.Equal(Math.PI / 2 * 5, arc.Length, 12);
        }

        [Fact]
        public void PartsOfAnEllipse_AreSplinesExactlyOnIt_EndingWhereItsAnglesSay()
        {
            // An ellipse 60 by 30 about (10, 20): a quarter, three quarters, and a part across angle nought, from 5.5 to 1.
            foreach ((string from, string to, double start, double end, int pieces) in new[]
            {
                ("0", "1.5707963267948966", 0.0, Math.PI / 2, 1),
                ("0", "4.71238898038469", 0.0, 3 * Math.PI / 2, 3),
                ("5.5", "1.0", 5.5, 1.0, 2),
            })
            {
                string text = R2000(G(0, "ELLIPSE", 8, "0", 100, "AcDbEllipse", 10, "10", 20, "20", 30, "0", 11, "30", 21, "0", 31, "0", 210, "0", 220, "0", 230, "1", 40, "0.5", 41, from, 42, to));
                var spline = Assert.IsType<GeoSpline2>(Assert.Single(Plan(text).Entities).Shape);

                Assert.Equal(2, spline.Degree);
                Assert.Equal(2 * pieces + 1, spline.ControlPointCount);
                Near(new GeoPoint2(10 + 30 * Math.Cos(start), 20 + 15 * Math.Sin(start)), spline.StartPoint, "the start");
                Near(new GeoPoint2(10 + 30 * Math.Cos(end), 20 + 15 * Math.Sin(end)), spline.EndPoint, "the end");

                for (int i = 0; i <= 50; i++)
                {
                    GeoPoint2 p = spline.GetPointAtParameter(spline.DomainStart + (spline.DomainEnd - spline.DomainStart) * i / 50.0);
                    double x = (p.X - 10) / 30;
                    double y = (p.Y - 20) / 15;
                    Assert.Equal(1.0, x * x + y * y, 12);
                }
            }
        }

        [Fact]
        public void APartOfAnEllipseOnTheMirroredExtrusion_RunsTheOtherWay_AndAWholeOneIsAnEllipse()
        {
            // About (0, 0, -1) the minor axis is the extrusion crossed with the major, (0, -1, 0): a quarter from angle 0 ends at (10, 5, 0).
            string part = R2000(G(0, "ELLIPSE", 8, "0", 100, "AcDbEllipse", 10, "10", 20, "20", 30, "0", 11, "30", 21, "0", 31, "0", 210, "0", 220, "0", 230, "-1", 40, "0.5", 41, "0", 42, "1.5707963267948966"));
            var spline = Assert.IsType<GeoSpline3>(Assert.Single(Space(part).Entities).Shape);
            Near(new GeoPoint3(40, 20, 0), spline.StartPoint, "the start");
            Near(new GeoPoint3(10, 5, 0), spline.EndPoint, "the end");

            var flat = Assert.IsType<GeoSpline2>(Assert.Single(Plan(part).Entities).Shape);
            Near(new GeoPoint2(40, 20), flat.StartPoint, "the start in the plane");
            Near(new GeoPoint2(10, 5), flat.EndPoint, "the end in the plane");

            string whole = R2000(G(0, "ELLIPSE", 8, "0", 100, "AcDbEllipse", 10, "10", 20, "20", 30, "0", 11, "0", 21, "30", 31, "0", 210, "0", 220, "0", 230, "1", 40, "0.5", 41, "0", 42, "6.283185307179586"));
            var ellipse = Assert.IsType<GeoEllipse2>(Assert.Single(Plan(whole).Entities).Shape);
            Assert.Equal(30, ellipse.MajorRadius);
            Assert.Equal(15, ellipse.MinorRadius);
            Assert.Equal(new GeoVector2(0, 1), ellipse.MajorAxis);
        }

        // ----- Splines ---------------------------------------------------------------------------------------------------

        [Fact]
        public void ASplineByItsFitPointsAlone_PassesThroughThem_AndIsWarnedAbout()
        {
            string text = R2000(
                G(0, "SPLINE", 8, "0", 100, "AcDbSpline", 210, "0", 220, "0", 230, "1", 70, 8, 71, 3, 72, 0, 73, 0, 74, 4, 44, "1E-10",
                  11, "0", 21, "0", 31, "0", 11, "100", 21, "50", 31, "0", 11, "200", 21, "0", 31, "0", 11, "300", 21, "50", 31, "0")
                + G(0, "SPLINE", 8, "0", 100, "AcDbSpline", 70, 8, 71, 3, 72, 0, 73, 0, 74, 3, 12, "1", 22, "0", 32, "0", 13, "0", 23, "1", 33, "0",
                  11, "0", 21, "0", 31, "0", 11, "100", 21, "50", 31, "0", 11, "200", 21, "0", 31, "0"));

            DxfDocument read = Plan(text);
            IReadOnlyList<GeoSpline2> splines = read.OfType<GeoSpline2>();

            Assert.Equal(2, splines.Count);
            Assert.Equal(new GeoPoint2(0, 0), splines[0].StartPoint);
            Assert.Equal(new GeoPoint2(300, 50), splines[0].EndPoint);
            Assert.True(splines[0].DistanceTo(new GeoPoint2(100, 50), Tol) < 1E-9);
            Assert.True(splines[1].DistanceTo(new GeoPoint2(100, 50), Tol) < 1E-9);

            // It leaves its first point along X, as the start tangent says.
            GeoPoint2 near = splines[1].GetPointAtParameter(splines[1].DomainStart + 1E-6);
            Assert.True(near.X > 0 && Math.Abs(near.Y) < 1E-3 * near.X, near.ToString());
            Assert.Contains(read.Warnings, w => w.Contains("fit points"));
        }

        [Fact]
        public void ASplineOfControlPointsKnotsAndWeights_IsThatSpline()
        {
            // AutoCAD's quarter circle of radius 10 about the origin as a SPLINE of degree 2, rational.
            string text = R2000(G(
                0, "SPLINE", 8, "0", 100, "AcDbSpline", 210, "0", 220, "0", 230, "1", 70, 12, 71, 2, 72, 6, 73, 3, 74, 0,
                40, "0", 40, "0", 40, "0", 40, "1", 40, "1", 40, "1", 41, "1", 41, "0.7071067811865476", 41, "1",
                10, "10", 20, "0", 30, "0", 10, "10", 20, "10", 30, "0", 10, "0", 20, "10", 30, "0"));

            var spline = Assert.IsType<GeoSpline2>(Assert.Single(Plan(text).Entities).Shape);
            Assert.True(spline.IsRational);

            GeoPoint2 middle = spline.GetPointAtParameter(0.5);
            Assert.Equal(10.0, middle.DistanceTo(GeoPoint2.Origin), 12);
        }

        // ----- Meshes, faces and filled areas ----------------------------------------------------------------------------

        [Fact]
        public void APolyfaceMeshOfACube_IsAClosedBodyOfSixFaces()
        {
            var vertices = new StringBuilder();
            double[][] corners = { new[] { 0.0, 0, 0 }, new[] { 10.0, 0, 0 }, new[] { 10.0, 10, 0 }, new[] { 0.0, 10, 0 }, new[] { 0.0, 0, 10 }, new[] { 10.0, 0, 10 }, new[] { 10.0, 10, 10 }, new[] { 0.0, 10, 10 } };

            foreach (double[] c in corners)
            {
                vertices.Append(G(0, "VERTEX", 8, "0", 100, "AcDbVertex", 100, "AcDbPolyFaceMeshVertex", 10, c[0], 20, c[1], 30, c[2], 70, 192));
            }

            int[][] faces = { new[] { 1, -4, 3, 2 }, new[] { 5, 6, 7, 8 }, new[] { 1, 2, 6, 5 }, new[] { 2, 3, 7, 6 }, new[] { 3, 4, 8, 7 }, new[] { 4, 1, 5, 8 } };

            foreach (int[] f in faces)
            {
                vertices.Append(G(0, "VERTEX", 8, "0", 100, "AcDbVertex", 100, "AcDbFaceRecord", 10, "0", 20, "0", 30, "0", 70, 128, 71, f[0], 72, f[1], 73, f[2], 74, f[3]));
            }

            string text = R2000(G(0, "POLYLINE", 5, "40", 8, "mesh", 100, "AcDb2dPolyline", 66, 1, 10, "0", 20, "0", 30, "0", 70, 64, 71, 8, 72, 6) + vertices + G(0, "SEQEND", 8, "mesh"));

            DxfEntity entity = Assert.Single(Plan(text).Entities);
            var body = Assert.IsType<GeoSolid3>(entity.Shape);

            Assert.Equal("POLYLINE", entity.Type);
            Assert.Equal(6, body.Faces.Count);
            Assert.True(body.IsClosed(Tol));
            Assert.Equal(1000.0, body.GetVolume(Tol), 9);
        }

        [Fact]
        public void APolygonMeshOfThreeByThree_IsABodyOfItsFourCells_OpenAsItIs()
        {
            var vertices = new StringBuilder();

            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    vertices.Append(G(0, "VERTEX", 8, "0", 10, i * 10.0, 20, j * 10.0, 30, i == 2 && j == 2 ? 4.0 : 0.0, 70, 64));
                }
            }

            string text = R12(G(0, "POLYLINE", 8, "0", 66, 1, 10, "0", 20, "0", 30, "0", 70, 16, 71, 3, 72, 3) + vertices + G(0, "SEQEND", 8, "0"));
            var body = Assert.IsType<GeoSolid3>(Assert.Single(Plan(text).Entities).Shape);

            // The cell standing out of flat, its corner (20, 20) raised 4, is its two triangles.
            Assert.Equal(5, body.Faces.Count);
            Assert.False(body.IsClosed(Tol));
        }

        [Fact]
        public void ThreeDFaces_AreATriangleAFaceOrTwoTriangles_AsTheirCornersLie()
        {
            string text = R2000(
                G(0, "3DFACE", 8, "0", 100, "AcDbFace", 10, "0", 20, "0", 30, "0", 11, "10", 21, "0", 31, "0", 12, "0", 22, "10", 32, "0", 13, "0", 23, "10", 33, "0")
                + G(0, "3DFACE", 8, "0", 100, "AcDbFace", 10, "0", 20, "0", 30, "5", 11, "10", 21, "0", 31, "5", 12, "10", 22, "10", 32, "5", 13, "0", 23, "10", 33, "5")
                + G(0, "3DFACE", 5, "9A", 8, "0", 100, "AcDbFace", 10, "0", 20, "0", 30, "0", 11, "10", 21, "0", 31, "0", 12, "10", 22, "12", 32, "3", 13, "0", 23, "10", 33, "0")
                + G(0, "3DFACE", 8, "0", 100, "AcDbFace", 10, "0", 20, "0", 30, "0", 11, "10", 21, "0", 31, "0", 12, "10", 22, "0", 32, "0", 13, "0", 23, "10", 33, "0"));

            DxfDocument read = Plan(text);

            Assert.Equal(5, read.Entities.Count);
            Assert.IsType<GeoTriangle3>(read.Entities[0].Shape);
            Assert.Equal(4, Assert.IsType<GeoFace3>(read.Entities[1].Shape).Boundary.VertexCount);

            // Out of flat, cut along the shorter diagonal, from (10, 0, 0) to (0, 10, 0); both triangles carry the handle.
            var first = Assert.IsType<GeoTriangle3>(read.Entities[2].Shape);
            var second = Assert.IsType<GeoTriangle3>(read.Entities[3].Shape);
            Assert.Equal(new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(0, 10, 0) }, new[] { first.A, first.B, first.C });
            Assert.Equal(new[] { new GeoPoint3(10, 0, 0), new GeoPoint3(10, 12, 3), new GeoPoint3(0, 10, 0) }, new[] { second.A, second.B, second.C });
            Assert.Equal("9A", read.Entities[2].Handle);
            Assert.Equal("9A", read.Entities[3].Handle);

            // A quad folded onto itself, its second and third corners the same, is a triangle.
            Assert.IsType<GeoTriangle3>(read.Entities[4].Shape);
        }

        [Fact]
        public void ASolidAndATrace_ArePolygonsOfTheirCorners_InTheOrderOneTwoFourThree()
        {
            string text = R2000(
                G(0, "SOLID", 8, "0", 100, "AcDbTrace", 10, "0", 20, "0", 30, "0", 11, "10", 21, "0", 31, "0", 12, "0", 22, "10", 32, "0", 13, "10", 23, "10", 33, "0")
                + G(0, "TRACE", 8, "0", 100, "AcDbTrace", 10, "0", 20, "0", 30, "0", 11, "10", 21, "0", 31, "0", 12, "5", 22, "8", 32, "0", 13, "5", 23, "8", 33, "0")
                + G(0, "SOLID", 8, "0", 100, "AcDbTrace", 10, "0", 20, "0", 30, "2", 11, "10", 21, "0", 12, "0", 22, "10", 13, "10", 23, "10"));

            DxfDocument read = Plan(text);

            Assert.Equal(new[] { new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10), new GeoPoint2(0, 10) }, Assert.IsType<GeoPolygon2>(read.Entities[0].Shape).Vertices.ToArray());
            Assert.Equal(3, Assert.IsType<GeoPolygon2>(read.Entities[1].Shape).VertexCount);

            // The corners after the first stand at its height where they give none.
            Assert.All(Assert.IsType<GeoPolygon3>(read.Entities[2].Shape).Vertices, v => Assert.Equal(2.0, v.Z));
        }

        // ----- Layers, colours, what is left out -------------------------------------------------------------------------

        [Fact]
        public void LayersAreThoseOfTheTableThenThoseFirstMet_OneLayerWhateverTheCase()
        {
            string text = File("AC1015", 4, Layers("Steel", "Caf\\U+00E9"), G(
                0, "LINE", 8, "STEEL", 62, 1, 10, "0", 20, "0", 11, "1", 21, "0",
                0, "LINE", 8, "Caf\\U+00E9", 62, 256, 10, "0", 20, "0", 11, "1", 21, "0",
                0, "LINE", 8, "extra", 62, 0, 10, "0", 20, "0", 11, "1", 21, "0",
                0, "LINE", 10, "0", 20, "0", 11, "1", 21, "0"));

            DxfDocument read = Plan(text);

            Assert.Equal(new[] { "Steel", "Café", "extra", "0" }, read.Layers.ToArray());
            Assert.Equal(new[] { "Steel", "Café", "extra", "0" }, read.Entities.Select(e => e.Layer).ToArray());
            Assert.Equal(new int?[] { 1, null, null, null }, read.Entities.Select(e => e.Colour).ToArray());
            Assert.Single(read.OnLayer("steel"));
            Assert.Empty(read.OnLayer("nothing"));
        }

        [Fact]
        public void TypesThatAreNotRead_AreCountedByName_AndTheEntitiesBetweenThemRead()
        {
            string text = File("AC1015", 4, G(0, "SECTION", 2, "BLOCKS", 0, "BLOCK", 2, "B", 70, 0, 10, "0", 20, "0", 30, "0", 0, "LINE", 8, "0", 10, "0", 20, "0", 11, "5", 21, "0", 0, "CIRCLE", 8, "0", 10, "0", 20, "0", 40, "1", 0, "ENDBLK", 0, "ENDSEC"), G(
                0, "TEXT", 8, "0", 10, "0", 20, "0", 40, "2.5", 1, "Hello",
                0, "LINE", 8, "0", 10, "0", 20, "0", 11, "1", 21, "0",
                0, "MTEXT", 8, "0", 10, "0", 20, "0", 40, "2.5", 1, "A\\Pb",
                0, "INSERT", 8, "0", 66, 1, 2, "B", 10, "0", 20, "0",
                0, "ATTRIB", 8, "0", 10, "0", 20, "0", 1, "v", 2, "TAG",
                0, "SEQEND", 8, "0",
                0, "HATCH", 8, "0", 10, "0", 20, "0",
                0, "TEXT", 8, "0", 10, "0", 20, "0", 40, "2.5", 1, "Again",
                0, "LINE", 8, "0", 67, 1, 10, "0", 20, "0", 11, "1", 21, "0",
                0, "CIRCLE", 8, "0", 10, "0", 20, "0", 40, "1"));

            DxfDocument read = Plan(text);

            // TEXT and MTEXT are read since the reader reads texts; ATTRIB and HATCH are still counted.
            Assert.Equal(new[] { "TEXT", "LINE", "MTEXT", "LINE", "CIRCLE", "TEXT", "CIRCLE" }, read.Entities.Select(e => e.Type).ToArray());
            Assert.Equal(new[] { "B" }, read.Entities[3].BlockPath.ToArray());
            Assert.Equal(new[] { "ATTRIB", "HATCH" }, read.Skipped.Keys.ToArray());
            Assert.Equal(1, read.Skipped["ATTRIB"]);
            Assert.Contains(read.Warnings, w => w.StartsWith("1 entity of paper space", StringComparison.Ordinal));
        }

        [Fact]
        public void LineEndsOfWindowsAndComments_ReadTheSame()
        {
            string text = R2000(G(0, "LINE", 8, "0", 999, "a comment", 10, "1.25", 20, "2", 11, "3", 21, "4"));
            DxfDocument plain = Plan(text);
            DxfDocument windows = Plan(text.Replace("\n", "\r\n"));

            Assert.Equal(Assert.IsType<GeoLine2>(Assert.Single(plain.Entities).Shape).StartPoint, Assert.IsType<GeoLine2>(Assert.Single(windows.Entities).Shape).StartPoint);
            Assert.DoesNotContain(plain.Entities[0].Groups, g => g.Code == 999);
        }

        [Fact]
        public void TheTextOfAFileIsReadInItsCodePage_OrAsUtf8FromR2007()
        {
            byte[] Bytes(string version, string layer, Encoding encoding)
            {
                string text = G(0, "SECTION", 2, "HEADER", 9, "$ACADVER", 1, version, 9, "$DWGCODEPAGE", 3, "ANSI_1252", 0, "ENDSEC")
                    + G(0, "SECTION", 2, "ENTITIES", 0, "POINT", 8, layer, 10, "0", 20, "0", 0, "ENDSEC", 0, "EOF");
                return encoding.GetBytes(text);
            }

            // The e acute is the one byte 0xE9 in the code page of R2000, and two bytes in the UTF-8 of R2007.
            DxfDocument western = DxfReader.Read(new MemoryStream(Bytes("AC1015", "Café", Encoding.GetEncoding(1252))), new DxfReadOptions { Tolerance = Tol });
            DxfDocument utf8 = DxfReader.Read(new MemoryStream(Bytes("AC1021", "Kết cấu", new UTF8Encoding(true))), new DxfReadOptions { Tolerance = Tol });

            Assert.Equal("Café", Assert.Single(western.Entities).Layer);
            Assert.Equal("Kết cấu", Assert.Single(utf8.Entities).Layer);
            Assert.Empty(western.Warnings);
            Assert.Empty(utf8.Warnings);
        }

        [Fact]
        public void TheAttributesOfAnEntity_AndEveryGroupOfIt_AreKept()
        {
            string text = R2000(G(
                0, "LINE", 5, "2A", 330, "1F", 100, "AcDbEntity", 8, "0", 6, "DASHED", 62, 1, 370, 35, 420, 16744448, 100, "AcDbLine", 39, "50",
                10, "0", 20, "0", 30, "0", 11, "10", 21, "0", 31, "0", 1001, "ACAD", 1000, "a note", 1040, "2.5",
                0, "LINE", 8, "0", 6, "ByLayer", 370, -1, 10, "0", 20, "0", 11, "10", 21, "0"));

            DxfDocument read = Plan(text);
            DxfEntity first = read.Entities[0];
            DxfEntity second = read.Entities[1];

            Assert.Equal("DASHED", first.Linetype);
            Assert.Equal(35, first.Lineweight);
            Assert.Equal(0xFF8000, first.TrueColour);
            Assert.Equal(50.0, first.Thickness);
            Assert.Equal(1, first.Colour);
            Assert.Equal(new[] { 5, 330, 100, 8, 6, 62, 370, 420, 100, 39, 10, 20, 30, 11, 21, 31, 1001, 1000, 1040 }, first.Groups.Select(g => g.Code).ToArray());
            Assert.Equal("a note", first.Groups.Single(g => g.Code == 1000).Value);

            Assert.Null(second.Linetype);
            Assert.Null(second.Lineweight);
            Assert.Null(second.TrueColour);
            Assert.Equal(0.0, second.Thickness);
        }
    }
}
