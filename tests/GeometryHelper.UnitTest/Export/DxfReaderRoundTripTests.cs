using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Export;
using GeometryHelper.Geometry;
using GeometryHelper.Meshing;
using Xunit;

namespace GeometryHelper.UnitTest.Export
{
    /// <summary>
    /// What <see cref="DxfWriter"/> writes, read back by <see cref="DxfReader"/>: every overload of Add, the shapes of the
    /// plane with PlanAs2D and those of space without, each number the file holds back to the bit.
    /// </summary>
    public class DxfReaderRoundTripTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        // Numbers whose shortest exact text has 16 or 17 digits, among them some .NET Framework's parser reads a step off.
        private const double A = 4.8721540113315704;
        private const double B = 3.3824149791069402;
        private const double C = 1894.4963821650001;
        private const double D = 0.84551240822557006;

        internal static DxfDocument Read(DxfWriter writer, bool plan)
            => DxfReader.Parse(writer.ToString(), new DxfReadOptions { PlanAs2D = plan, Tolerance = Tol });

        internal static T Only<T>(DxfDocument document)
        {
            DxfShape entity = Assert.Single(document.Shapes);
            return Assert.IsType<T>(entity.Shape);
        }

        /// <summary>The same double to the bit, a negative nought being written as nought.</summary>
        internal static void Same(double expected, double actual)
            => Assert.Equal(BitConverter.DoubleToInt64Bits(expected + 0.0), BitConverter.DoubleToInt64Bits(actual));

        internal static void Same(GeoPoint2 expected, GeoPoint2 actual)
        {
            Same(expected.X, actual.X);
            Same(expected.Y, actual.Y);
        }

        internal static void Same(GeoPoint3 expected, GeoPoint3 actual)
        {
            Same(expected.X, actual.X);
            Same(expected.Y, actual.Y);
            Same(expected.Z, actual.Z);
        }

        internal static void Same(IReadOnlyList<GeoPoint2> expected, IReadOnlyList<GeoPoint2> actual)
        {
            Assert.Equal(expected.Count, actual.Count);

            for (int i = 0; i < expected.Count; i++)
            {
                Same(expected[i], actual[i]);
            }
        }

        internal static void Same(IReadOnlyList<GeoPoint3> expected, IReadOnlyList<GeoPoint3> actual)
        {
            Assert.Equal(expected.Count, actual.Count);

            for (int i = 0; i < expected.Count; i++)
            {
                Same(expected[i], actual[i]);
            }
        }

        internal static void Same(IReadOnlyList<double> expected, IReadOnlyList<double> actual)
        {
            Assert.Equal(expected.Count, actual.Count);

            for (int i = 0; i < expected.Count; i++)
            {
                Same(expected[i], actual[i]);
            }
        }

        internal static void Near(GeoPoint3 expected, GeoPoint3 actual, double within, string what)
            => Assert.True(expected.DistanceTo(actual) <= within, what + ": " + expected + " read as " + actual + ", " + expected.DistanceTo(actual) + " apart, more than " + within);

        private static GeoPoint3[] Corners(GeoTriangle3 t) => new[] { t.A, t.B, t.C };

        private static GeoPoint3[] Corners(object shape)
            => shape is GeoTriangle3 t ? Corners(t) : ((GeoFace3)shape).Boundary.Vertices.ToArray();

        // ----- The plane -----------------------------------------------------------------------------------------------

        [Fact]
        public void APointOfThePlane_IsThatPointWithPlanAs2D_AndAPointAtHeightNoughtWithout()
        {
            // Add(GeoPoint2)
            var point = new GeoPoint2(A, -C);

            Same(point, Only<GeoPoint2>(Read(new DxfWriter().Add(point), true)));
            Same(new GeoPoint3(A, -C, 0), Only<GeoPoint3>(Read(new DxfWriter().Add(point), false)));
        }

        [Fact]
        public void ASegmentOfThePlane_ReadsBackToTheBit()
        {
            // Add(GeoLine2)
            var line = new GeoLine2(new GeoPoint2(A, B), new GeoPoint2(-C, D));
            GeoLine2 back = Only<GeoLine2>(Read(new DxfWriter().Add(line), true));

            Same(line.StartPoint, back.StartPoint);
            Same(line.EndPoint, back.EndPoint);
        }

        [Fact]
        public void AChainAndAPolygonOfThePlane_ReadBackToTheBit()
        {
            // Add(GeoPolyline2), Add(GeoPolygon2)
            var chain = new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(A, B), new GeoPoint2(C, -D), new GeoPoint2(1E-7, 1E7));
            var polygon = new GeoPolygon2(new GeoPoint2(0.1, 0.2), new GeoPoint2(100.3, 0.7), new GeoPoint2(C, A * 50), new GeoPoint2(-D, 33.3));

            Same(chain.Vertices, Only<GeoPolyline2>(Read(new DxfWriter().Add(chain), true)).Vertices);
            Same(polygon.Vertices, Only<GeoPolygon2>(Read(new DxfWriter().Add(polygon), true)).Vertices);
        }

        [Fact]
        public void ARectangleAndATriangle_ArePolygonsOfTheirCorners_ToTheBit()
        {
            // Add(GeoRectangle2), Add(GeoTriangle2)
            var rectangle = new GeoRectangle2(new GeoPoint2(A, B), 30.7, 20.3, 0.7);
            var triangle = new GeoTriangle2(new GeoPoint2(0, 0), new GeoPoint2(C, D), new GeoPoint2(-A, 12.5));

            Same(rectangle.GetVertices(), Only<GeoPolygon2>(Read(new DxfWriter().Add(rectangle), true)).Vertices);
            Same(new[] { triangle.A, triangle.B, triangle.C }, Only<GeoPolygon2>(Read(new DxfWriter().Add(triangle), true)).Vertices);
        }

        [Fact]
        public void AChainWithArcs_KeepsItsVerticesAndTheBulgesOfItsEdges_ToTheBit()
        {
            // Add(GeoPolylineArc2): the bulge on the last vertex leaves no edge, so none is written, and it reads back nought.
            var chain = new GeoPolylineArc2(
                new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(A * 30, B * 40), new GeoPoint2(C, -D) },
                new[] { 0.41421356237309503, 0.0, -D, 0.5 });

            GeoPolylineArc2 back = Only<GeoPolylineArc2>(Read(new DxfWriter().Add(chain), true));

            Same(chain.Vertices, back.Vertices);
            Same(chain.Bulges.Take(3).ToList(), back.Bulges.Take(3).ToList());
            Same(0.0, back.Bulges[3]);
        }

        [Fact]
        public void ALoopWithArcs_KeepsEveryBulge_TheClosingEdgesToo_ToTheBit()
        {
            // Add(GeoPolygonArc2)
            var loop = new GeoPolygonArc2(
                new[] { new GeoPoint2(0, 0), new GeoPoint2(60, 0), new GeoPoint2(60, 20), new GeoPoint2(0, 20) },
                new[] { 0.0, 1.0, 0.0, A / 10.0 });

            GeoPolygonArc2 back = Only<GeoPolygonArc2>(Read(new DxfWriter().Add(loop), true));

            Same(loop.Vertices, back.Vertices);
            Same(loop.Bulges, back.Bulges);
        }

        [Fact]
        public void AChainWithArcsAllStraight_IsAPlainChain()
        {
            // A chain whose every bulge is nought writes no bulge, and a LWPOLYLINE with none is a GeoPolyline2.
            var chain = new GeoPolylineArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(A, B), new GeoPoint2(C, D) });

            Same(chain.Vertices, Only<GeoPolyline2>(Read(new DxfWriter().Add(chain), true)).Vertices);
        }

        [Fact]
        public void AFace_IsAPolygonForItsBoundaryAndOneForEachHole_InOrder_ToTheBit()
        {
            // Add(GeoFace2): DXF has no polygon with holes, so the face is its rings.
            var boundary = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 80), new GeoPoint2(0, 80));
            var slot = new GeoPolygon2(new GeoPoint2(10, 10), new GeoPoint2(10, 20), new GeoPoint2(A * 10, 20), new GeoPoint2(A * 10, 10));
            var hole = new GeoPolygon2(new GeoPoint2(60, 50), new GeoPoint2(60, 70), new GeoPoint2(C / 25, 70));

            DxfDocument read = Read(new DxfWriter().Add(new GeoFace2(boundary, new[] { slot, hole }), "plate"), true);
            IReadOnlyList<GeoPolygon2> rings = read.OfType<GeoPolygon2>();

            Assert.Equal(3, read.Shapes.Count);
            Assert.All(read.Shapes, e => Assert.Equal("plate", e.Layer));

            var face = new GeoFace2(boundary, new[] { slot, hole });
            Same(face.Boundary.Vertices, rings[0].Vertices);
            Same(face.Holes[0].Vertices, rings[1].Vertices);
            Same(face.Holes[1].Vertices, rings[2].Vertices);
        }

        [Fact]
        public void ACircleOfThePlane_ReadsBackToTheBit()
        {
            // Add(GeoCircle2)
            var circle = new GeoCircle2(new GeoPoint2(A, -B), C);
            GeoCircle2 back = Only<GeoCircle2>(Read(new DxfWriter().Add(circle), true));

            Same(circle.Center, back.Center);
            Same(circle.Radius, back.Radius);
        }

        [Fact]
        public void AnArcOfThePlane_KeepsItsCentreAndRadiusToTheBit_AndItsAnglesToTheLastPlaceOrTwo()
        {
            // Add(GeoArc2): the angles are degrees in the file, so they come back to rounding; a clockwise arc is written
            // from its end to its start, and comes back counter-clockwise over the same points; a whole turn is a CIRCLE.
            var counter = new GeoArc2(new GeoPoint2(123.456789, -98.7654321), 77.123456789, 0.7, 2.9);
            var clockwise = new GeoArc2(new GeoPoint2(A, B), C, 2.9, 0.7, true);
            var whole = new GeoArc2(new GeoPoint2(1.5, 2.5), 10.0, 0.5, 0.5);

            GeoArc2 first = Only<GeoArc2>(Read(new DxfWriter().Add(counter), true));
            GeoArc2 second = Only<GeoArc2>(Read(new DxfWriter().Add(clockwise), true));
            GeoCircle2 third = Only<GeoCircle2>(Read(new DxfWriter().Add(whole), true));

            Same(counter.Center, first.Center);
            Same(counter.Radius, first.Radius);
            Assert.Equal(counter.StartAngle, first.StartAngle, 14);
            Assert.Equal(counter.SweptAngle, first.SweptAngle, 14);

            Same(clockwise.Center, second.Center);
            Assert.False(second.IsClockwise);
            Assert.Equal(clockwise.EndAngle, second.StartAngle, 14);
            Assert.Equal(-clockwise.SweptAngle, second.SweptAngle, 14);

            Same(whole.Center, third.Center);
            Same(10.0, third.Radius);
        }

        [Fact]
        public void AnEllipseOfThePlane_KeepsItsCentreToTheBit_AndItsAxisAndRadiiToTheLastPlace()
        {
            // Add(GeoEllipse2): the file keeps the end of the major axis and the ratio of the radii.
            var ellipse = new GeoEllipse2(new GeoPoint2(A, B), new GeoVector2(Math.Cos(0.7), Math.Sin(0.7)), 300.25, 100.125);
            GeoEllipse2 back = Only<GeoEllipse2>(Read(new DxfWriter().Add(ellipse), true));

            Same(ellipse.Center, back.Center);
            Assert.Equal(ellipse.MajorRadius, back.MajorRadius, 12);
            Assert.Equal(ellipse.MinorRadius, back.MinorRadius, 12);
            Assert.Equal(ellipse.MajorAxis.X, back.MajorAxis.X, 15);
            Assert.Equal(ellipse.MajorAxis.Y, back.MajorAxis.Y, 15);
        }

        [Fact]
        public void ARayAndALineWithoutEndsOfThePlane_KeepTheirOriginsAndDirectionsToTheBit()
        {
            // Add(GeoRay2), Add(GeoXLine2)
            var ray = new GeoRay2(new GeoPoint2(A, B), new GeoVector2(C, -D));
            var xline = new GeoXLine2(new GeoPoint2(-C, D), new GeoVector2(1, 3));

            GeoRay2 ray2 = Only<GeoRay2>(Read(new DxfWriter().Add(ray), true));
            GeoXLine2 xline2 = Only<GeoXLine2>(Read(new DxfWriter().Add(xline), true));

            Same(ray.Origin, ray2.Origin);
            Same(ray.Direction.X, ray2.Direction.X);
            Same(ray.Direction.Y, ray2.Direction.Y);
            Same(xline.Origin, xline2.Origin);
            Same(xline.Direction.X, xline2.Direction.X);
            Same(xline.Direction.Y, xline2.Direction.Y);
        }

        [Fact]
        public void ASplineOfThePlane_KeepsItsDegreeKnotsWeightsAndControlPoints_ToTheBit()
        {
            // Add(GeoSpline2), plain and rational.
            var cubic = new GeoSpline2(3, new[] { new GeoPoint2(0, 0), new GeoPoint2(A * 20, 150), new GeoPoint2(200, B * 40), new GeoPoint2(C / 6, 0), new GeoPoint2(400, D) }, new[] { 0, 0, 0, 0, 0.3, 1, 1, 1, 1.0 });
            GeoSpline2 circle = GeoSpline2.FromCircle(new GeoCircle2(new GeoPoint2(A, B), 100));

            foreach (GeoSpline2 spline in new[] { cubic, circle })
            {
                GeoSpline2 back = Only<GeoSpline2>(Read(new DxfWriter().Add(spline), true));

                Assert.Equal(spline.Degree, back.Degree);
                Assert.Equal(spline.IsRational, back.IsRational);
                Same(spline.ControlPoints, back.ControlPoints);
                Same(spline.Knots, back.Knots);
                Same(spline.Weights, back.Weights);
            }
        }

        // ----- Space ---------------------------------------------------------------------------------------------------

        [Fact]
        public void APointAndASegmentInSpace_ReadBackToTheBit()
        {
            // Add(GeoPoint3), Add(GeoLine3)
            var point = new GeoPoint3(A, B, -C);
            var line = new GeoLine3(new GeoPoint3(D, A, B), new GeoPoint3(-C, 1E-9, 7.25));

            Same(point, Only<GeoPoint3>(Read(new DxfWriter().Add(point), false)));

            GeoLine3 back = Only<GeoLine3>(Read(new DxfWriter().Add(line), false));
            Same(line.StartPoint, back.StartPoint);
            Same(line.EndPoint, back.EndPoint);
        }

        [Fact]
        public void AChainAndAPolygonInSpace_Are3DPolylines_ReadBackToTheBit()
        {
            // Add(GeoPolyline3), Add(GeoPolygon3)
            var chain = new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(A, B, C), new GeoPoint3(-C, D, 3), new GeoPoint3(1, 2, 3));
            var polygon = new GeoPolygon3(new GeoPoint3(0, 0, 5), new GeoPoint3(A * 10, 0, 5 + A), new GeoPoint3(A * 10, B * 10, 5 + A), new GeoPoint3(0, B * 10, 5));

            Same(chain.Vertices, Only<GeoPolyline3>(Read(new DxfWriter().Add(chain), false)).Vertices);
            Same(polygon.Vertices, Only<GeoPolygon3>(Read(new DxfWriter().Add(polygon), false)).Vertices);
        }

        [Fact]
        public void ARayAndALineWithoutEndsInSpace_KeepTheirOriginsAndDirectionsToTheBit()
        {
            // Add(GeoRay3), Add(GeoXLine3)
            var ray = new GeoRay3(new GeoPoint3(A, B, C), new GeoVector3(1, 2, 3));
            var xline = new GeoXLine3(new GeoPoint3(-C, D, 0.1), new GeoVector3(-D, A, -B));

            GeoRay3 ray3 = Only<GeoRay3>(Read(new DxfWriter().Add(ray), false));
            GeoXLine3 xline3 = Only<GeoXLine3>(Read(new DxfWriter().Add(xline), false));

            Same(ray.Origin, ray3.Origin);
            Same(new GeoPoint3(ray.Direction.X, ray.Direction.Y, ray.Direction.Z), new GeoPoint3(ray3.Direction.X, ray3.Direction.Y, ray3.Direction.Z));
            Same(xline.Origin, xline3.Origin);
            Same(new GeoPoint3(xline.Direction.X, xline.Direction.Y, xline.Direction.Z), new GeoPoint3(xline3.Direction.X, xline3.Direction.Y, xline3.Direction.Z));
        }

        [Fact]
        public void ASplineInSpace_FlatOrNot_KeepsEveryNumberToTheBit()
        {
            // Add(GeoSpline3): a circle tilted onto the normal (1, 1, 0), written planar, and a cubic out of any one plane.
            GeoSpline3 tilted = GeoSpline3.FromCircle(new GeoCircle3(new GeoPoint3(10, 20, 30), new GeoVector3(1, 1, 0), 25));
            var bent = new GeoSpline3(3, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(100, A, 0), new GeoPoint3(100, 100, B), new GeoPoint3(C, 100, 100) }, new[] { 0, 0, 0, 0, 1, 1, 1, 1.0 });

            foreach (GeoSpline3 spline in new[] { tilted, bent })
            {
                GeoSpline3 back = Only<GeoSpline3>(Read(new DxfWriter().Add(spline), false));

                Assert.Equal(spline.Degree, back.Degree);
                Same(spline.ControlPoints, back.ControlPoints);
                Same(spline.Knots, back.Knots);
                Same(spline.Weights, back.Weights);
            }
        }

        [Fact]
        public void AnEllipseInSpace_KeepsItsCentreToTheBit_AndItsPlaneAxisAndRadiiToTheLastPlace()
        {
            // Add(GeoEllipse3)
            var ellipse = new GeoEllipse3(new GeoPoint3(A, B, C), new GeoVector3(1, 2, 3), new GeoVector3(3, 0, -1), 250.5, 99.75);
            GeoEllipse3 back = Only<GeoEllipse3>(Read(new DxfWriter().Add(ellipse), false));

            Same(ellipse.Center, back.Center);
            Assert.Equal(ellipse.MajorRadius, back.MajorRadius, 12);
            Assert.Equal(ellipse.MinorRadius, back.MinorRadius, 12);
            Assert.Equal(1.0, back.Normal.DotProduct(ellipse.Normal), 15);
            Assert.Equal(1.0, back.MajorAxis.DotProduct(ellipse.MajorAxis), 15);

            foreach (double angle in new[] { 0.0, 1.0, 2.5, 4.0, 5.5 })
            {
                Near(ellipse.GetPointAtAngle(angle), back.GetPointAtAngle(angle), 1E-12, "the point at " + angle);
            }
        }

        [Fact]
        public void ATriangleInSpace_IsThatTriangle_ToTheBit()
        {
            // Add(GeoTriangle3)
            var triangle = new GeoTriangle3(new GeoPoint3(A, B, C), new GeoPoint3(-D, 10, 0), new GeoPoint3(5, -C, 1));
            GeoTriangle3 back = Only<GeoTriangle3>(Read(new DxfWriter().Add(triangle), false));

            Same(Corners(triangle), Corners(back));
        }

        [Fact]
        public void AFaceInSpace_IsTheTrianglesOfItsSurface_InOrder_ToTheBit()
        {
            // Add(GeoFace3), at the global tolerance, and Add(GeoFace3, Tolerance).
            var boundary = new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 10), new GeoPoint3(100, 80, 10), new GeoPoint3(0, 80, 0));
            var hole = new GeoPolygon3(new GeoPoint3(20, 20, 2), new GeoPoint3(20, 40, 2), new GeoPoint3(40, 40, 4), new GeoPoint3(40, 20, 4));
            var face = new GeoFace3(boundary, new[] { hole }, Tol);

            foreach (DxfWriter writer in new[] { new DxfWriter().Add(face), new DxfWriter().Add(face, Tol) })
            {
                GeoTriangle3[] expected = face.TriangulateSurface(Tol);
                IReadOnlyList<GeoTriangle3> read = Read(writer, false).OfType<GeoTriangle3>();

                Assert.Equal(expected.Length, read.Count);

                for (int i = 0; i < expected.Length; i++)
                {
                    Same(Corners(expected[i]), Corners(read[i]));
                }
            }
        }

        [Fact]
        public void ABodyWithAnOpening_IsTheTrianglesWhereItsMaterialEnds_InOrder_ToTheBit()
        {
            // Add(GeoSolid3), at the global tolerance, and Add(GeoSolid3, Tolerance): 64 triangles for a box 100 by 100 by
            // 20 with a hole 20 by 20 through it.
            GeoSolid3 plate = Box(0, 0, 0, 100, 100, 20).WithOpenings(new[] { Box(40, 40, -1, 60, 60, 21) });

            foreach (DxfWriter writer in new[] { new DxfWriter().Add(plate, "plate"), new DxfWriter().Add(plate, Tol, "plate") })
            {
                GeoTriangle3[] expected = plate.TriangulateSurface(Tol);
                DxfDocument read = Read(writer, false);
                IReadOnlyList<GeoTriangle3> triangles = read.OfType<GeoTriangle3>();

                Assert.Equal(64, triangles.Count);
                Assert.Equal(expected.Length, triangles.Count);
                Assert.All(read.Shapes, e => Assert.Equal("3DFACE", e.Type));

                for (int i = 0; i < expected.Length; i++)
                {
                    Same(Corners(expected[i]), Corners(triangles[i]));
                }
            }
        }

        [Fact]
        public void ABall_IsTheTrianglesOfItsSurface_ToTheBit()
        {
            // Add(GeoSphere3, chord tolerance): a ball of radius 50 within 0.1 is 3 380 triangles.
            var ball = new GeoSphere3(new GeoPoint3(10.5, -20.25, 7.125), 50.0);
            GeoTriangle3[] expected = ball.TriangulateSurface(0.1);
            IReadOnlyList<GeoTriangle3> read = Read(new DxfWriter().Add(ball, 0.1), false).OfType<GeoTriangle3>();

            Assert.Equal(3380, read.Count);

            for (int i = 0; i < expected.Length; i++)
            {
                Same(Corners(expected[i]), Corners(read[i]));
            }
        }

        [Fact]
        public void Triangles_AreEachThatTriangle_ToTheBit()
        {
            // Add(IEnumerable<GeoTriangle3>)
            GeoTriangle3[] triangles = Box(0.1, 0.2, 0.3, A, B * 3, C / 100).TriangulateSurface(Tol);
            IReadOnlyList<GeoTriangle3> read = Read(new DxfWriter().Add(triangles, "tris"), false).OfType<GeoTriangle3>();

            Assert.Equal(triangles.Length, read.Count);

            for (int i = 0; i < triangles.Length; i++)
            {
                Same(Corners(triangles[i]), Corners(read[i]));
            }
        }

        [Fact]
        public void AMesh_IsAFaceForEachCellOfFourCorners_ATriangleForOneOfThree_AndTheTrianglesOfAnyOther()
        {
            // Add(GeoMesh3): a grid of a tilted rectangle and the convex pieces of a dart.
            var rectangle = new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 30), new GeoPoint3(100, 70, 30), new GeoPoint3(0, 70, 0));
            var dart = new GeoPolygon3(new GeoPoint3(0, 0, 1), new GeoPoint3(100, 50, 1), new GeoPoint3(0, 100, 1), new GeoPoint3(30, 50, 1));

            foreach (GeoMesh3 mesh in new[] { new GeoFace3(rectangle).ToMesh(MeshOptions.Grid(20, 20)), new GeoFace3(dart).ToMesh(MeshKind.Convex), new GeoFace3(dart).ToMesh(MeshKind.Triangles) })
            {
                var expected = new List<GeoPoint3[]>();

                for (int f = 0; f < mesh.FaceCount; f++)
                {
                    int[] corners = mesh.GetFaceIndices(f);

                    if (corners.Length <= 4 && mesh.IsConvex(f))
                    {
                        expected.Add(corners.Select(i => mesh.Vertices[i]).ToArray());
                    }
                    else
                    {
                        expected.AddRange(mesh.GetFaceTriangles(f).Select(Corners));
                    }
                }

                DxfDocument read = Read(new DxfWriter().Add(mesh), false);
                Assert.Equal(expected.Count, read.Shapes.Count);

                for (int i = 0; i < expected.Count; i++)
                {
                    Same(expected[i], Corners(read.Shapes[i].Shape));
                }
            }
        }

        [Fact]
        public void ABox_IsSixFacesOfFourCorners_InTheOrderTheyAreWritten_ToTheBit()
        {
            // Add(GeoAabb3), Add(GeoObb3)
            var aabb = new GeoAabb3(new GeoPoint3(0.1, 0.2, 0.3), new GeoPoint3(30.1, A * 5, C));
            var obb = new GeoObb3(new GeoPoint3(5, 6, 7), 30, 40, 70, new GeoVector3(1, 1, 0), new GeoVector3(-1, 1, 0.5));

            foreach ((DxfWriter writer, GeoPoint3[] c) in new[] { (new DxfWriter().Add(aabb), aabb.GetCorners()), (new DxfWriter().Add(obb), obb.GetCorners()) })
            {
                IReadOnlyList<GeoFace3> faces = Read(writer, false).OfType<GeoFace3>();
                GeoPoint3[][] expected =
                {
                    new[] { c[0], c[3], c[2], c[1] },
                    new[] { c[4], c[5], c[6], c[7] },
                    new[] { c[0], c[1], c[5], c[4] },
                    new[] { c[2], c[3], c[7], c[6] },
                    new[] { c[1], c[2], c[6], c[5] },
                    new[] { c[3], c[0], c[4], c[7] },
                };

                Assert.Equal(6, faces.Count);

                for (int i = 0; i < 6; i++)
                {
                    Same(expected[i], faces[i].Boundary.Vertices);
                }
            }
        }

        [Fact]
        public void ArcsAndCirclesInSpaceWithAChordTolerance_AreTheirChords_ToTheBit()
        {
            // Add(GeoArc3, chord tolerance), Add(GeoCircle3, chord tolerance)
            var arc = new GeoArc3(new GeoPoint3(10, -20, 30), new GeoVector3(1, 2, 3), 40, 0.4, 2.5);
            var circle = new GeoCircle3(new GeoPoint3(A, B, C), new GeoVector3(0, 1, 1), 25);

            Same(Chords.Of(arc, 0.1), Only<GeoPolyline3>(Read(new DxfWriter().Add(arc, 0.1), false)).Vertices);

            List<GeoPoint3> round = Chords.Of(circle, 0.1);
            round.RemoveAt(round.Count - 1);
            Same(round, Only<GeoPolygon3>(Read(new DxfWriter().Add(circle, 0.1), false)).Vertices);
        }

        [Fact]
        public void ChainsAndLoopsWithArcsInSpaceWithAChordTolerance_AreChordsThatKeepTheirCorners()
        {
            // Add(GeoPolylineArc3, chord tolerance), Add(GeoPolygonArc3, chord tolerance)
            var up = new GeoVector3(0, 0, 1);
            var chain = new GeoPolylineArc3(new[] { new GeoPoint3(0, 0, 5), new GeoPoint3(100, 0, 5), new GeoPoint3(200, 40, 5) }, new[] { 0.4, 0.0, 0.0 }, new[] { up, up, up });
            var loop = new GeoPolygonArc3(new[] { new GeoPoint3(0, 0, 5), new GeoPoint3(100, 0, 5), new GeoPoint3(100, 100, 5), new GeoPoint3(0, 100, 5) }, new[] { 0.0, 0.5, 0.0, 0.0 }, new[] { up, up, up, up });

            GeoPolyline3 open = Only<GeoPolyline3>(Read(new DxfWriter().Add(chain, 0.05), false));
            GeoPolygon3 closed = Only<GeoPolygon3>(Read(new DxfWriter().Add(loop, 0.05), false));

            Same(new GeoPoint3(0, 0, 5), open.Vertices[0]);
            Assert.Contains(open.Vertices, p => p.Equals(new GeoPoint3(100, 0, 5)));
            Same(new GeoPoint3(200, 40, 5), open.Vertices[open.VertexCount - 1]);
            Assert.True(open.VertexCount > 10);
            Assert.All(new[] { 0.0, 100.0, 0.0, 100.0 }.Zip(new[] { 0.0, 0.0, 100.0, 100.0 }, (x, y) => new GeoPoint3(x, y, 5)), p => Assert.Contains(closed.Vertices, v => v.Equals(p)));
            Assert.True(closed.VertexCount > 10);
        }

        [Fact]
        public void AnArcOnTheFloorInSpace_ReadsBackItsCentreAndRadiusToTheBit_AndItsEndsToRounding()
        {
            // Add(GeoArc3) with its normal up: the ARC of the plane, no extrusion written.
            var arc = new GeoArc3(new GeoPoint3(A, B, 5), new GeoVector3(0, 0, 1), C, 0.4, 2.5);
            GeoArc3 back = Only<GeoArc3>(Read(new DxfWriter().Add(arc), false));

            Same(arc.Center, back.Center);
            Same(arc.Radius, back.Radius);
            Near(arc.StartPoint, back.StartPoint, 1E-12 * C, "the start");
            Near(arc.MidPoint, back.MidPoint, 1E-12 * C, "the middle");
            Near(arc.EndPoint, back.EndPoint, 1E-12 * C, "the end");
        }

        [Fact]
        public void AnArcInATiltedPlane_ComesBackTheSameArc_EitherWayRound()
        {
            // Add(GeoArc3): an arc 120 degrees round a circle of radius 40 standing on the normal (1, 2, 3), centred at
            // (10, -20, 30). It is read counter-clockwise about its extrusion, so the arc that turns the other way comes
            // back from its end to its start.
            var counter = new GeoArc3(new GeoPoint3(10, -20, 30), new GeoVector3(1, 2, 3), 40, 0.4, 0.4 + 2.0 * Math.PI / 3.0);

            foreach (GeoArc3 arc in new[] { counter, counter.Reverse() })
            {
                GeoArc3 back = Only<GeoArc3>(Read(new DxfWriter().Add(arc, "bars"), false));
                double within = 1E-13 * (arc.Radius + 40);

                Same(arc.Radius, back.Radius);
                Assert.Equal(1.0, Math.Abs(back.Normal.DotProduct(arc.Normal)), 15);
                Near(arc.Center, back.Center, within, "the centre");
                Near(arc.MidPoint, back.MidPoint, within, "the middle");
                Near(arc.IsClockwise ? arc.EndPoint : arc.StartPoint, back.StartPoint, within, "the first end");
                Near(arc.IsClockwise ? arc.StartPoint : arc.EndPoint, back.EndPoint, within, "the last end");
            }
        }

        [Fact]
        public void ACircleInSpace_OnTheFloorToTheBit_AndInATiltedPlaneToRounding()
        {
            // Add(GeoCircle3)
            var floor = new GeoCircle3(new GeoPoint3(A, B, C), new GeoVector3(0, 0, 1), 25.125);
            var tilted = new GeoCircle3(new GeoPoint3(10, 20, 30), new GeoVector3(1, 1, 0), 25);

            GeoCircle3 first = Only<GeoCircle3>(Read(new DxfWriter().Add(floor), false));
            Same(floor.Center, first.Center);
            Same(floor.Radius, first.Radius);
            Same(new GeoPoint3(0, 0, 1), new GeoPoint3(first.Normal.X, first.Normal.Y, first.Normal.Z));

            GeoCircle3 second = Only<GeoCircle3>(Read(new DxfWriter().Add(tilted), false));
            Same(tilted.Radius, second.Radius);
            Near(tilted.Center, second.Center, 1E-13 * 60, "the centre");
            Assert.Equal(1.0, second.Normal.DotProduct(tilted.Normal), 15);
        }

        [Fact]
        public void AChainWithArcsOnTheFloorInSpace_ReadsBackToTheBit_AtItsHeight()
        {
            // Add(GeoPolylineArc3), at the global tolerance, and Add(GeoPolylineArc3, Tolerance): a chain at z = 5 with its
            // arcs about up is a LWPOLYLINE at the elevation 5, read back to the bit.
            var up = new GeoVector3(0, 0, 1);
            var chain = new GeoPolylineArc3(new[] { new GeoPoint3(0, 0, 5), new GeoPoint3(A * 20, 0, 5), new GeoPoint3(200, B * 10, 5), new GeoPoint3(C, 0, 5) }, new[] { 0.4, 0.0, -D, 0.0 }, new[] { up, up, up, up });

            foreach (DxfWriter writer in new[] { new DxfWriter().Add(chain), new DxfWriter().Add(chain, Tol) })
            {
                GeoPolylineArc3 back = Only<GeoPolylineArc3>(Read(writer, false));

                Same(chain.Vertices, back.Vertices);
                Same(chain.Bulges.Take(3).ToList(), back.Bulges.Take(3).ToList());
            }
        }

        [Fact]
        public void AChainWithArcsInATiltedPlane_ComesBackItsEdgesToRounding()
        {
            // Add(GeoPolylineArc3, Tolerance): a chain on the plane tilted 30 degrees about X keeps its vertices and arcs.
            double sin = Math.Sin(Math.PI / 6.0);
            double cos = Math.Cos(Math.PI / 6.0);
            var normal = new GeoVector3(0, -sin, cos);
            GeoPoint3 At(double x, double y) => new GeoPoint3(5 + x, y * cos, 7 + y * sin);

            var chain = new GeoPolylineArc3(new[] { At(0, 0), At(60, 0), At(60, 20), At(0, 25) }, new[] { 0.3, -1.0, 0.0, 0.0 }, new[] { normal, normal, default(GeoVector3), default(GeoVector3) }, Tol);
            GeoPolylineArc3 back = Only<GeoPolylineArc3>(Read(new DxfWriter().Add(chain, Tol), false));

            Assert.Equal(chain.VertexCount, back.VertexCount);

            for (int i = 0; i < chain.EdgeCount; i++)
            {
                GeoEdge3 was = chain.GetEdgeAt(i);
                GeoEdge3 now = back.GetEdgeAt(i);

                Assert.Equal(was.IsArc, now.IsArc);
                Near(was.StartPoint, now.StartPoint, 1E-12, "vertex " + i);
                Near(was.MidPoint, now.MidPoint, 1E-12, "the middle of edge " + i);
            }
        }

        [Fact]
        public void AChainBentOutOfOnePlane_IsItsLinesAndArcs_InOrder()
        {
            // Add(GeoPolylineArc3): a bar bent in two planes is a LINE or an ARC for each edge.
            GeoArc3 first = GeoArc3.FromThreePoints(new GeoPoint3(100, 0, 0), new GeoPoint3(100 + 20 * Math.Sin(Math.PI / 4), 0, 20 - 20 * Math.Cos(Math.PI / 4)), new GeoPoint3(120, 0, 20), Tol);
            GeoArc3 second = GeoArc3.FromThreePoints(new GeoPoint3(120, 0, 100), new GeoPoint3(120 + 20 * (1 - Math.Cos(Math.PI / 4)), 20 * Math.Sin(Math.PI / 4), 100), new GeoPoint3(140, 20, 100), Tol);
            var bar = new GeoPolylineArc3(new[]
            {
                new GeoEdge3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)),
                new GeoEdge3(first),
                new GeoEdge3(first.EndPoint, new GeoPoint3(120, 0, 100)),
                new GeoEdge3(second),
                new GeoEdge3(second.EndPoint, new GeoPoint3(140, 200, 100)),
            });

            DxfDocument read = Read(new DxfWriter().Add(bar, Tol, "bars"), false);

            Assert.Equal(new[] { "LINE", "ARC", "LINE", "ARC", "LINE" }, read.Shapes.Select(e => e.Type).ToArray());

            for (int i = 0; i < bar.EdgeCount; i++)
            {
                GeoEdge3 edge = bar.GetEdgeAt(i);
                object shape = read.Shapes[i].Shape;
                GeoPoint3 start = shape is GeoLine3 line ? line.StartPoint : ((GeoArc3)shape).StartPoint;
                GeoPoint3 end = shape is GeoLine3 line2 ? line2.EndPoint : ((GeoArc3)shape).EndPoint;
                GeoPoint3 middle = shape is GeoLine3 line3 ? line3.MidPoint : ((GeoArc3)shape).MidPoint;

                // An arc is read counter-clockwise about its extrusion, so it may come back from its end to its start.
                bool turned = start.DistanceTo(edge.StartPoint) > 1E-9;
                Near(turned ? edge.EndPoint : edge.StartPoint, start, 1E-11, "the first end of edge " + i);
                Near(turned ? edge.StartPoint : edge.EndPoint, end, 1E-11, "the last end of edge " + i);
                Near(edge.MidPoint, middle, 1E-11, "the middle of edge " + i);
            }
        }

        [Fact]
        public void ALoopWithArcsInSpace_OnTheFloorToTheBit_AndInATiltedPlaneToRounding()
        {
            // Add(GeoPolygonArc3): a slot 80 long and 20 wide with a half circle at each end.
            var up = new GeoVector3(0, 0, 1);
            var floor = new GeoPolygonArc3(new[] { new GeoPoint3(0, 0, 2.5), new GeoPoint3(60, 0, 2.5), new GeoPoint3(60, 20, 2.5), new GeoPoint3(0, 20, 2.5) }, new[] { 0.0, 1.0, 0.0, 1.0 }, new[] { up, up, up, up });

            GeoPolygonArc3 flat = Only<GeoPolygonArc3>(Read(new DxfWriter().Add(floor), false));
            Same(floor.Vertices, flat.Vertices);
            Same(floor.Bulges, flat.Bulges);

            double sin = Math.Sin(Math.PI / 6.0);
            double cos = Math.Cos(Math.PI / 6.0);
            var normal = new GeoVector3(0, -sin, cos);
            GeoPoint3 At(double x, double y) => new GeoPoint3(5 + x, y * cos, 7 + y * sin);
            var tilted = new GeoPolygonArc3(new[] { At(0, 0), At(60, 0), At(60, 20), At(0, 20) }, new[] { 0.0, 1.0, 0.0, 1.0 }, new[] { default(GeoVector3), normal, default(GeoVector3), normal }, Tol);

            GeoPolygonArc3 back = Only<GeoPolygonArc3>(Read(new DxfWriter().Add(tilted), false));
            Assert.Equal(4, back.VertexCount);

            for (int i = 0; i < 4; i++)
            {
                Near(tilted.GetEdgeAt(i).StartPoint, back.GetEdgeAt(i).StartPoint, 1E-12, "vertex " + i);
                Near(tilted.GetEdgeAt(i).MidPoint, back.GetEdgeAt(i).MidPoint, 1E-12, "the middle of edge " + i);
            }

            Assert.Equal(tilted.Area, back.Area, 9);
        }

        // ----- The file ------------------------------------------------------------------------------------------------

        [Fact]
        public void TheFileSaysItsVersionUnitAndLayers_AndEachEntityItsLayerAndHandle_WithNothingLeftOut()
        {
            DxfDocument read = Read(new DxfWriter().Add(new GeoLine2(0, 0, 10, 0), "steel").Add(new GeoCircle2(new GeoPoint2(5, 5), 2), "Café").Add(new GeoPoint3(1, 2, 3), "STEEL"), true);

            Assert.Equal("AC1015", read.Version);
            Assert.Equal(DxfUnits.Millimeters, read.Units);
            Assert.Equal(new[] { "0", "steel", "Café" }, read.Layers.ToArray());
            Assert.Equal(new[] { "steel", "Café", "steel" }, read.Shapes.Select(e => e.Layer).ToArray());
            Assert.Equal(new[] { "LINE", "CIRCLE", "POINT" }, read.Shapes.Select(e => e.Type).ToArray());
            Assert.All(read.Shapes, e => Assert.Null(e.Color));
            Assert.All(read.Shapes, e => Assert.Empty(e.BlockPath));
            Assert.Equal(3, read.Shapes.Select(e => e.Handle).Distinct().Count());
            Assert.All(read.Shapes, e => Assert.Matches("^[0-9A-F]+$", e.Handle));
            Assert.Empty(read.Skipped);
            Assert.Empty(read.Warnings);
        }

        [Fact]
        public void EveryOverloadOfAdd_WrittenIntoOneFile_ReadsBackEntityForEntity()
        {
            // One file of one of each, as a drawing would be: nothing is skipped or warned about, and the counts are those
            // of the entities the writer wrote.
            DxfWriter writer = OneOfEach();

            string text = writer.ToString();
            int written = EntitiesWritten(text);

            foreach (bool plan in new[] { false, true })
            {
                DxfDocument read = DxfReader.Parse(text, new DxfReadOptions { PlanAs2D = plan, Tolerance = Tol });

                Assert.Empty(read.Skipped);
                Assert.Empty(read.Warnings);
                Assert.Equal(written, read.Shapes.Count);
                Assert.Equal(written, read.Shapes.Select(e => e.Handle).Distinct().Count());
            }
        }

        /// <summary>The entities of the ENTITIES section of a file, a VERTEX or SEQEND belonging to its POLYLINE.</summary>
        internal static int EntitiesWritten(string text)
        {
            string[] lines = text.Split('\n');
            int count = 0;
            bool inside = false;

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                if (lines[i].Trim() != "0")
                {
                    if (lines[i].Trim() == "2" && lines[i + 1] == "ENTITIES")
                    {
                        inside = true;
                    }

                    continue;
                }

                if (lines[i + 1] == "ENDSEC")
                {
                    inside = false;
                }
                else if (inside && lines[i + 1] != "VERTEX" && lines[i + 1] != "SEQEND")
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>One shape of each overload of DxfWriter.Add, in one file, on layer 0.</summary>
        internal static DxfWriter OneOfEach()
        {
            var up = new GeoVector3(0, 0, 1);
            return new DxfWriter()
                .Add(new GeoPoint2(1, 2)).Add(new GeoLine2(0, 0, 1, 1)).Add(new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(1, 0)))
                .Add(new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(1, 0), new GeoPoint2(0, 1))).Add(new GeoRectangle2(0, 0, 3, 4))
                .Add(new GeoTriangle2(new GeoPoint2(0, 0), new GeoPoint2(4, 0), new GeoPoint2(0, 3)))
                .Add(new GeoPolylineArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(10, 0) }, new[] { 1.0, 0.0 }))
                .Add(new GeoPolygonArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10) }, new[] { 0.0, 0.5, 0.0 }))
                .Add(new GeoFace2(new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(0, 10))))
                .Add(new GeoCircle2(new GeoPoint2(0, 0), 5)).Add(new GeoArc2(new GeoPoint2(0, 0), 5, 0, 1))
                .Add(new GeoEllipse2(new GeoPoint2(0, 0), new GeoVector2(1, 0), 5, 2)).Add(new GeoRay2(new GeoPoint2(0, 0), new GeoVector2(1, 0)))
                .Add(new GeoXLine2(new GeoPoint2(0, 0), new GeoVector2(0, 1))).Add(GeoSpline2.FromCircle(new GeoCircle2(new GeoPoint2(0, 0), 3)))
                .Add(new GeoPoint3(1, 2, 3)).Add(new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 1, 1)))
                .Add(new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 1))).Add(new GeoPolygon3(new GeoPoint3(0, 0, 1), new GeoPoint3(1, 0, 1), new GeoPoint3(0, 1, 1)))
                .Add(new GeoRay3(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1))).Add(new GeoXLine3(new GeoPoint3(0, 0, 0), new GeoVector3(1, 1, 1)))
                .Add(GeoSpline3.FromCircle(new GeoCircle3(new GeoPoint3(0, 0, 0), new GeoVector3(1, 0, 0), 3)))
                .Add(new GeoEllipse3(new GeoPoint3(0, 0, 0), new GeoVector3(0, 1, 0), new GeoVector3(1, 0, 0), 5, 2))
                .Add(new GeoTriangle3(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0), new GeoPoint3(0, 1, 0)))
                .Add(new GeoFace3(new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0), new GeoPoint3(1, 1, 0), new GeoPoint3(0, 1, 0))), Tol)
                .Add(Box(0, 0, 0, 1, 1, 1), Tol).Add(new GeoSphere3(new GeoPoint3(0, 0, 0), 5), 1.0)
                .Add(new[] { new GeoTriangle3(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0), new GeoPoint3(0, 1, 0)) })
                .Add(new GeoFace3(new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(10, 10, 0), new GeoPoint3(0, 10, 0))).ToMesh(MeshOptions.Grid(5, 5)))
                .Add(new GeoAabb3(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 2, 3))).Add(new GeoObb3(new GeoPoint3(0, 0, 0), 1, 2, 3))
                .Add(new GeoArc3(new GeoPoint3(0, 0, 0), up, 5, 0, 1), 0.1).Add(new GeoCircle3(new GeoPoint3(0, 0, 0), up, 5), 0.1)
                .Add(new GeoPolylineArc3(new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0) }, new[] { 1.0, 0.0 }, new[] { up, up }), 0.1)
                .Add(new GeoPolygonArc3(new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(10, 10, 0) }, new[] { 0.0, 0.5, 0.0 }, new[] { up, up, up }), 0.1)
                .Add(new GeoArc3(new GeoPoint3(0, 0, 0), new GeoVector3(1, 0, 0), 5, 0, 1)).Add(new GeoCircle3(new GeoPoint3(0, 0, 0), new GeoVector3(1, 0, 0), 5))
                .Add(new GeoPolylineArc3(new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0) }, new[] { 1.0, 0.0 }, new[] { up, up }))
                .Add(new GeoPolylineArc3(new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0) }, new[] { 1.0, 0.0 }, new[] { up, up }), Tol)
                .Add(new GeoPolygonArc3(new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(10, 10, 0) }, new[] { 0.0, 0.5, 0.0 }, new[] { up, up, up }));
        }

        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoAabb3(new GeoPoint3(x0, y0, z0), new GeoPoint3(x1, y1, z1)).ToObb().ToSolid();
    }
}
