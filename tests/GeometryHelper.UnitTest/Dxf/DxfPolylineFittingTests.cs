using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The fitting AutoCAD does to a POLYLINE: a polyline of the plane fitted with a curve or a spline, a 3D one with a spline,
    /// a mesh smoothed into a surface, each straightened again; and the points of a mesh taken out with its counts kept true.
    /// </summary>
    public class DxfPolylineFittingTests
    {
        private static readonly Tolerance Tight = new Tolerance(1E-9, 1E-9);

        private static DxfPolyline2d Plane(DxfDatabase db, bool closed, params GeoPoint3[] points)
        {
            var polyline = new DxfPolyline2d(DxfPoly2dType.SimplePoly, points, 0, closed, 0, 0, null);
            db.ModelSpace.AppendEntity(polyline);
            return polyline;
        }

        [Fact]
        public void APolylineFittedWithASpline_IsItsFrameAndTheVerticesOfTheCurve_StraightenedBack()
        {
            // A cubic on (0, 0), (10, 10), (20, 0) is of degree 2 on three points: one span, 8 segments, (10, 5) at its middle.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfPolyline2d polyline = Plane(db, false, new GeoPoint3(0, 0, 0), new GeoPoint3(10, 10, 0), new GeoPoint3(20, 0, 0));
            string[] frame = polyline.Select(v => v.Handle).ToArray();

            polyline.SplineFit(DxfPoly2dType.CubicSplinePoly, 8);

            List<DxfVertex2d> vertices = polyline.ToList();
            Assert.Equal(DxfPoly2dType.CubicSplinePoly, polyline.PolyType);
            Assert.Equal(12, vertices.Count);
            // The vertices of the curve follow the first of the frame, as AutoCAD keeps them.
            List<DxfVertex2d> control = new[] { vertices[0] }.Concat(vertices.Skip(10)).ToList();
            Assert.Equal(frame, control.Select(v => v.Handle).ToArray());
            Assert.All(control, v => Assert.Equal(DxfVertex2dType.SplineControlVertex, v.VertexType));
            Assert.All(vertices.Skip(1).Take(9), v => Assert.Equal(DxfVertex2dType.SplineFitVertex, v.VertexType));
            Assert.Equal(8.0, polyline.EndParam);
            Assert.True(polyline.GetPointAtParameter(4).IsEqualTo(new GeoPoint3(10, 5, 0), Tight));
            Assert.True(polyline.EndPoint.IsEqualTo(new GeoPoint3(20, 0, 0), Tight));

            DxfPolyline2d again = ByHandle<DxfPolyline2d>(DxfDatabase.Parse(db.ToText()), polyline.Handle);
            Assert.Equal(DxfPoly2dType.CubicSplinePoly, again.PolyType);
            Assert.True(again.GetPointAtParameter(4).IsEqualTo(new GeoPoint3(10, 5, 0), Tight));
            Assert.True(((GeoPolyline3)Assert.Single(again.GetGeometry())).Length > 20.0);

            polyline.Straighten();
            Assert.Equal(DxfPoly2dType.SimplePoly, polyline.PolyType);
            Assert.Equal(frame, polyline.Select(v => v.Handle).ToArray());
            Assert.All(polyline, v => Assert.Equal(DxfVertex2dType.SimpleVertex, v.VertexType));
        }

        [Fact]
        public void VerticesHeldWhileAPolylineIsStraightened_AreErasedWhereTheFittingMadeThem_TheFrameStillTheirs()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfPolyline2d polyline = Plane(db, false, new GeoPoint3(0, 0, 0), new GeoPoint3(10, 10, 0), new GeoPoint3(20, 0, 0));
            polyline.SplineFit(DxfPoly2dType.CubicSplinePoly, 8);
            List<DxfVertex2d> held = polyline.ToList();

            polyline.Straighten();

            // The frame is the first vertex and the last two, the curve's 9 between them, as AutoCAD keeps them.
            List<DxfVertex2d> frame = new[] { held[0] }.Concat(held.Skip(10)).ToList();
            DxfVertex2d last = frame[2];
            last.Position = new GeoPoint3(20, -5, 0);
            polyline.AppendVertex(new DxfVertex2d(new GeoPoint3(30, 0, 0), 0, 0, 0, 0));

            Assert.All(held.Skip(1).Take(9), v => Assert.True(v.IsErased));
            Assert.All(frame, v => Assert.False(v.IsErased));
            DxfPolyline2d again = ByHandle<DxfPolyline2d>(DxfDatabase.Parse(db.ToText()), polyline.Handle);
            Assert.Equal(new[] { 0.0, 10.0, 20.0, 30.0 }, again.Select(v => v.Position.X).ToArray());
            Assert.Equal(-5.0, again.ElementAt(2).Position.Y);
            Assert.Equal(last.Handle, again.ElementAt(2).Handle);
        }

        [Fact]
        public void TheHeadersSplineVariables_SayTheSplineAndItsSegments()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            db.SetHeader("$SPLINESEGS", new DxfTypedValue(70, "4"));
            db.SetHeader("$SPLINETYPE", new DxfTypedValue(70, "5"));
            DxfPolyline2d polyline = Plane(db, false, new GeoPoint3(0, 0, 0), new GeoPoint3(10, 10, 0), new GeoPoint3(20, 0, 0), new GeoPoint3(30, 10, 0));

            polyline.SplineFit();

            // Quadratic on four points: two spans of 4 segments.
            Assert.Equal(DxfPoly2dType.QuadSplinePoly, polyline.PolyType);
            Assert.Equal(8.0, polyline.EndParam);
            Assert.True(polyline.GetPointAtParameter(4).IsEqualTo(new GeoPoint3(15, 5, 0), Tight));

            polyline.ConvertToPolyType(DxfPoly2dType.CubicSplinePoly);
            Assert.Equal(DxfPoly2dType.CubicSplinePoly, polyline.PolyType);
            Assert.Equal(4.0, polyline.EndParam);

            polyline.SplineFit(DxfPoly2dType.CubicSplinePoly, -6);
            Assert.Equal(6.0, polyline.EndParam);
            Assert.Contains(polyline.Where(v => v.VertexType == DxfVertex2dType.SplineFitVertex), v => v.Bulge != 0.0);
            Assert.Throws<ArgumentOutOfRangeException>(() => polyline.SplineFit(DxfPoly2dType.FitCurvePoly, 4));
            Assert.Throws<ArgumentOutOfRangeException>(() => polyline.SplineFit(DxfPoly2dType.CubicSplinePoly, 0));
        }

        [Fact]
        public void APolylineFittedWithACurve_PassesItsVertices_ThreeOnACircleGivingThatCircle()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfPolyline2d polyline = Plane(db, false, new GeoPoint3(10, 0, 0), new GeoPoint3(0, 10, 0), new GeoPoint3(-10, 0, 0));

            polyline.PolyType = DxfPoly2dType.FitCurvePoly;

            List<DxfVertex2d> vertices = polyline.ToList();
            Assert.Equal(DxfPoly2dType.FitCurvePoly, polyline.PolyType);
            Assert.Equal(5, vertices.Count);
            Assert.Equal(DxfVertex2dType.CurveFitVertex, vertices[1].VertexType);
            Assert.Equal(DxfVertex2dType.SimpleVertex, vertices[2].VertexType);

            for (int i = 0; i <= 40; i++)
            {
                Assert.Equal(10.0, polyline.GetPointAtParameter(polyline.EndParam * i / 40).DistanceTo(new GeoPoint3(0, 0, 0)), 9);
            }

            polyline.ConvertToPolyType(DxfPoly2dType.SimplePoly);
            Assert.Equal(3, polyline.Count());
            Assert.All(polyline, v => Assert.Equal(0.0, v.Bulge));
            Assert.Equal(20.0 * Math.Sqrt(2.0), polyline.Length, 9);
        }

        [Fact]
        public void ACurveFitOfATaperedSegment_ReadsTheWidthAtItsJointByArcLength_AsAutoCad2021()
        {
            // AutoCAD 2021's PEDIT Fit gives a segment from width 1 to 3 the width 2.01969395 at its joint, where the arcs it
            // made either side of the joint are 25.723 and 24.733 long: the first arc's share of their length, not 2 at the middle.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfPolyline2d polyline = Plane(db, false, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0), new GeoPoint3(0, 80, 0));
            DxfVertex2d tapered = polyline.ElementAt(1);
            tapered.StartWidth = 1.0;
            tapered.EndWidth = 3.0;

            polyline.CurveFit();

            List<DxfVertex2d> vertices = polyline.ToList();
            DxfVertex2d joint = vertices[3];
            Assert.Equal(DxfVertex2dType.CurveFitVertex, joint.VertexType);
            double first = ArcLength(vertices[2], joint);
            double second = ArcLength(joint, vertices[4]);
            Assert.True(Math.Abs(first - second) > 0.1, "The two arcs are of one length: " + first + ".");
            double expected = 1.0 + 2.0 * first / (first + second);
            Assert.Equal(1.0, vertices[2].StartWidth, 12);
            Assert.Equal(expected, vertices[2].EndWidth, 12);
            Assert.Equal(expected, joint.StartWidth, 12);
            Assert.Equal(3.0, joint.EndWidth, 12);
        }

        [Fact]
        public void AnOpenPolylineFittedWithACurve_IsTheArcsAutoCad2021Makes_VertexByVertex()
        {
            // PEDIT Fit in AutoCAD 2021, dumped with entnext: each row the x, y and bulge of a vertex, and 1 for one the fitting
            // added. On (0, 0), (100, 0), (100, 50), (0, 80) the tangent at (100, 0) is 64.41 degrees, not the 63.43 of the
            // circle through it and its neighbours.
            Fitted(false, new[]
            {
                new[] { 0.0, 0.0, 0.28407904384, 0 },
                new[] { 50.473479334062, -31.194325758915, 0.288680674965, 1 },
                new[] { 100.0, 0.0, 0.112125437658, 0 },
                new[] { 105.231629414823, 23.036060694724, 0.096115383771, 1 },
                new[] { 100.0, 50.0, 0.227837791583, 0 },
                new[] { 57.209372712299, 89.031242374329, 0.227837791583, 1 },
                new[] { 0.0, 80.0, 0.0, 0 },
            });

            // Five vertices, two segments of them S-shaped.
            Fitted(false, new[]
            {
                new[] { 0.0, 0.0, 0.077165813254, 0 },
                new[] { 26.254994179079, 1.139371720445, 0.080049749463, 1 },
                new[] { 50.0, 10.0, 0.206521601094, 0 },
                new[] { 70.539518451053, 37.211853489124, -0.238127307995, 1 },
                new[] { 90.0, 60.0, -0.342093110794, 0 },
                new[] { 118.161285251997, 52.424770657694, 0.282114185974, 1 },
                new[] { 150.0, 40.0, 0.153060539429, 0 },
                new[] { 182.267044124121, 56.865668857406, 0.148068739112, 1 },
                new[] { 200.0, 90.0, 0.0, 0 },
            });

            // Three vertices on a line: the segments between them stay straight, with no vertex added.
            Fitted(false, new[]
            {
                new[] { 0.0, 0.0, 0.0, 0 },
                new[] { 50.0, 0.0, 0.0, 0 },
                new[] { 100.0, 0.0, 0.135932959546, 0 },
                new[] { 120.195784467261, 5.593908456986, 0.072993423051, 1 },
                new[] { 150.0, 30.0, 0.0, 0 },
            });

            // Two vertices: one straight segment.
            Fitted(false, new[] { new[] { 0.0, 0.0, 0.0, 0 }, new[] { 100.0, 30.0, 0.0, 0 } });
        }

        [Fact]
        public void AClosedPolylineFittedWithACurve_IsTheArcsAutoCad2021Makes_VertexByVertex()
        {
            Fitted(true, new[]
            {
                new[] { 0.0, 0.0, 0.172812229613, 0 },
                new[] { 26.700146629128, -13.448730240108, 0.09476678955, 1 },
                new[] { 80.0, -10.0, 0.083800121773, 0 },
                new[] { 123.778578517857, 9.591967802102, 0.156867023795, 1 },
                new[] { 140.0, 30.0, 0.270077423561, 0 },
                new[] { 134.993810368845, 59.6671646671, 0.142395184431, 1 },
                new[] { 90.0, 90.0, 0.139713433167, 0 },
                new[] { 45.07564450748, 91.467350275556, 0.153304122779, 1 },
                new[] { 10.0, 70.0, 0.144861085964, 0 },
                new[] { -9.0822163568, 28.350185691233, 0.229888488787, 1 },
            });

            // A triangle: the tangent at each vertex halves the turn there.
            Fitted(true, new[]
            {
                new[] { 0.0, 0.0, 0.259933205007, 0 },
                new[] { 55.533722703826, -30.962078267641, 0.31385715039, 1 },
                new[] { 100.0, 0.0, 0.31385715039, 0 },
                new[] { 88.372072386924, 44.803662670615, 0.23095147812, 1 },
                new[] { 30.0, 60.0, 0.23095147812, 0 },
                new[] { -1.611170223595, 35.808795686246, 0.259933205007, 1 },
            });
        }

        [Fact]
        public void APolylineWithAVertexTwice_IsFittedAsTwoCurvesMeetingThere_AsAutoCad2021()
        {
            // Open: (0, 0) to (50, 0) is a curve of two vertices, straight, and the rest one of three, on a circle.
            Fitted(false, new[]
            {
                new[] { 0.0, 0.0, 0.0, 0 },
                new[] { 50.0, 0.0, 0.0, 0 },
                new[] { 50.0, 0.0, -0.0954145724, 0 },
                new[] { 71.148351928655, 24.814560089181, -0.0954145724, 1 },
                new[] { 100.0, 40.0, -0.07369160123, 0 },
                new[] { 125.0, 43.704698247833, -0.07369160123, 1 },
                new[] { 150.0, 40.0, 0.0, 0 },
            });

            // Closed: one open curve from the second (100, 0) round to the first.
            Fitted(true, new[]
            {
                new[] { 0.0, 0.0, 0.331569029865, 0 },
                new[] { 49.488315753212, -36.871124127313, 0.326151839543, 1 },
                new[] { 100.0, 0.0, 0.0, 0 },
                new[] { 100.0, 0.0, 0.150562063818, 0 },
                new[] { 119.676399450404, 27.524823611556, 0.157168438949, 1 },
                new[] { 120.0, 60.0, 0.353633960665, 0 },
                new[] { 76.219906898164, 89.259796985769, 0.225983379751, 1 },
                new[] { 10.0, 50.0, 0.098754143785, 0 },
                new[] { -1.084080240376, 23.358775322043, 0.122493327872, 1 },
            });
        }

        [Fact]
        public void ASegmentWhoseTangentIsAMillionthOfARadianOffItsChord_StaysStraight_AsAutoCad2021()
        {
            // (0, 0), (50, 0), (100, y), (150, 30): the circle through the first three leaves (0, 0) y / 100 radians off the
            // chord. AutoCAD 2021 keeps the first segment straight at y = 0.0001 and fits it with arcs at y = 0.000101.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfPolyline2d straight = Plane(db, false, new GeoPoint3(0, 0, 0), new GeoPoint3(50, 0, 0), new GeoPoint3(100, 0.0001, 0), new GeoPoint3(150, 30, 0));
            DxfPolyline2d bent = Plane(db, false, new GeoPoint3(0, 0, 0), new GeoPoint3(50, 0, 0), new GeoPoint3(100, 0.000101, 0), new GeoPoint3(150, 30, 0));

            straight.CurveFit();
            bent.CurveFit();

            Assert.Equal(DxfVertex2dType.SimpleVertex, straight.ElementAt(1).VertexType);
            Assert.Equal(0.0, straight.First().Bulge);
            Assert.Equal(DxfVertex2dType.CurveFitVertex, bent.ElementAt(1).VertexType);
            Assert.Equal(new[] { 0, 1, 0, 0, 1, 0 }, bent.Select(v => v.VertexType == DxfVertex2dType.CurveFitVertex ? 1 : 0).ToArray());
        }

        [Fact]
        public void AVertexGivenItsTangent_IsFittedAlongIt_TheOthersAsBefore_TheVertexAddedAfterItKeepingTheDirection()
        {
            // AutoCAD 2021, PEDIT Edit vertex Tangent 30 at (100, 0), then Fit: the vertex added after it holds group 50 too.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfPolyline2d polyline = Plane(db, false, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0), new GeoPoint3(0, 80, 0));
            DxfVertex2d given = polyline.ElementAt(1);
            given.Tangent = Math.PI / 6.0;
            given.TangentUsed = true;

            polyline.CurveFit();

            Same(polyline, new[]
            {
                new[] { 0.0, 0.0, 0.28407904384, 0 },
                new[] { 30.243146610746, -18.691292532187, 0.131652497587, 1 },
                new[] { 100.0, 0.0, 0.267949192431, 0 },
                new[] { 107.261031473914, 12.576475428175, 0.096115383771, 1 },
                new[] { 100.0, 50.0, 0.227837791583, 0 },
                new[] { 57.209372712299, 89.031242374329, 0.227837791583, 1 },
                new[] { 0.0, 80.0, 0.0, 0 },
            });
            DxfVertex2d joint = polyline.ElementAt(3);
            Assert.False(joint.TangentUsed);
            Assert.Equal(Math.PI / 6.0, joint.Tangent, 12);
            Assert.True(polyline.ElementAt(2).TangentUsed);
        }

        [Fact]
        public void TheWidthsOfACurveFit_AreAutoCad2021s_OpenAndClosed()
        {
            // PLINE with widths 1 to 3, then 3 to 0.5, then 2 (open); 1 to 3, then 0.5 to 2, then 2 (closed); PEDIT Fit. Each
            // row the start and end width of a vertex.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfPolyline2d open = Plane(db, false, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0), new GeoPoint3(0, 80, 0));
            Widths(open, 1, 3, 3, 0.5, 2, 2, 2, 2);
            DxfPolyline2d closed = Plane(db, true, new GeoPoint3(0, 0, 0), new GeoPoint3(80, -10, 0), new GeoPoint3(140, 30, 0), new GeoPoint3(90, 90, 0), new GeoPoint3(10, 70, 0));
            Widths(closed, 1, 3, 0.5, 2, 2, 2, 2, 2, 2, 2);

            open.CurveFit();
            closed.CurveFit();

            Near(new[] { 1.0, 2.006008626782, 2.006008626782, 3.0, 3.0, 1.842688178646, 1.842688178646, 0.5, 2.0, 2.0, 2.0, 2.0, 2.0, 2.0 }, open.SelectMany(v => new[] { v.StartWidth, v.EndWidth }).ToArray());
            Near(new[] { 1.0, 1.724015528075, 1.724015528075, 3.0, 0.5, 1.467840348513, 1.467840348513, 2.0 }, closed.Take(4).SelectMany(v => new[] { v.StartWidth, v.EndWidth }).ToArray());
            Assert.All(closed.Skip(4), v => Assert.Equal(new[] { 2.0, 2.0 }, new[] { v.StartWidth, v.EndWidth }));
        }

        private static void Near(double[] expected, double[] actual)
        {
            Assert.Equal(expected.Length, actual.Length);

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i], actual[i], 1E-9);
            }
        }

        private static void Widths(DxfPolyline2d polyline, params double[] widths)
        {
            int i = 0;

            foreach (DxfVertex2d vertex in polyline)
            {
                vertex.StartWidth = widths[i++];
                vertex.EndWidth = widths[i++];
            }
        }

        /// <summary>Fits the frame of the rows (those not added) with a curve and checks every vertex against them.</summary>
        private static void Fitted(bool closed, double[][] expected)
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfPolyline2d polyline = Plane(db, closed, expected.Where(r => r[3] == 0).Select(r => new GeoPoint3(r[0], r[1], 0)).ToArray());

            polyline.CurveFit();

            Same(polyline, expected);
            Same(ByHandle<DxfPolyline2d>(DxfDatabase.Parse(db.ToText()), polyline.Handle), expected);
        }

        private static void Same(DxfPolyline2d polyline, double[][] expected)
        {
            List<DxfVertex2d> vertices = polyline.ToList();
            Assert.Equal(expected.Length, vertices.Count);

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i][3] == 1 ? DxfVertex2dType.CurveFitVertex : DxfVertex2dType.SimpleVertex, vertices[i].VertexType);
                Assert.Equal(expected[i][0], vertices[i].Position.X, 1E-9);
                Assert.Equal(expected[i][1], vertices[i].Position.Y, 1E-9);
                Assert.Equal(expected[i][2], vertices[i].Bulge, 1E-9);
            }
        }

        private static double ArcLength(DxfVertex2d from, DxfVertex2d to)
        {
            double chord = from.Position.DistanceTo(to.Position);
            double sweep = 4.0 * Math.Atan(Math.Abs(from.Bulge));
            return sweep == 0.0 ? chord : chord * sweep / (2.0 * Math.Sin(sweep / 2.0));
        }

        [Fact]
        public void ASplineFit_KeepsTheVerticesOfTheCurveAfterTheFirstOfTheFrame_AsAutoCad2021Keeps()
        {
            // PEDIT Spline in AutoCAD 2021 of (0, 0), (100, 0), (100, 50), (0, 80), SPLINETYPE 6 and SPLINESEGS 8, walked with
            // entnext: the first vertex of the frame, the 9 of the curve, then the rest of the frame.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfPolyline2d polyline = Plane(db, false, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0), new GeoPoint3(0, 80, 0));
            string[] frame = polyline.Select(v => v.Handle).ToArray();

            polyline.SplineFit(DxfPoly2dType.CubicSplinePoly, 8);

            double[][] curve =
            {
                new[] { 0.0, 0.0 }, new[] { 32.8125, 2.20703125 }, new[] { 56.25, 8.28125 }, new[] { 70.3125, 17.40234375 },
                new[] { 75.0, 28.75 }, new[] { 70.3125, 41.50390625 }, new[] { 56.25, 54.84375 }, new[] { 32.8125, 67.94921875 },
                new[] { 0.0, 80.0 },
            };
            List<DxfVertex2d> vertices = polyline.ToList();
            Assert.Equal(13, vertices.Count);
            Assert.Equal(DxfVertex2dType.SplineControlVertex, vertices[0].VertexType);
            Assert.All(vertices.Skip(10), v => Assert.Equal(DxfVertex2dType.SplineControlVertex, v.VertexType));
            Assert.Equal(frame, new[] { vertices[0] }.Concat(vertices.Skip(10)).Select(v => v.Handle).ToArray());

            for (int i = 0; i < curve.Length; i++)
            {
                Assert.Equal(DxfVertex2dType.SplineFitVertex, vertices[i + 1].VertexType);
                Assert.Equal(curve[i][0], vertices[i + 1].Position.X, 8);
                Assert.Equal(curve[i][1], vertices[i + 1].Position.Y, 8);
            }

            DxfPolyline2d again = ByHandle<DxfPolyline2d>(DxfDatabase.Parse(db.ToText()), polyline.Handle);
            Assert.True(again.EndPoint.IsEqualTo(new GeoPoint3(0, 80, 0), Tight));
            Assert.True(again.GetPointAtParameter(4).IsEqualTo(new GeoPoint3(75, 28.75, 0), Tight));
        }

        [Fact]
        public void APolylineInSpaceFittedWithASpline_IsItsFrameAndTheCurve()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R12);
            var polyline = new DxfPolyline3d(DxfPoly3dType.SimplePoly, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 10, 10), new GeoPoint3(20, 0, 0), new GeoPoint3(30, 10, 10) }, false);
            db.ModelSpace.AppendEntity(polyline);

            polyline.PolyType = DxfPoly3dType.CubicSplinePoly;

            List<DxfPolylineVertex3d> vertices = polyline.ToList();
            // The first vertex of the frame, the 9 of the curve, then the rest of the frame, as AutoCAD 2021 keeps them.
            Assert.Equal(13, vertices.Count);
            Assert.All(new[] { vertices[0] }.Concat(vertices.Skip(10)), v => Assert.Equal(DxfVertex3dType.ControlVertex, v.VertexType));
            Assert.All(vertices.Skip(1).Take(9), v => Assert.Equal(DxfVertex3dType.FitVertex, v.VertexType));
            Assert.True(polyline.EndPoint.IsEqualTo(new GeoPoint3(30, 10, 10), Tight));
            Assert.True(polyline.GetPointAtParameter(4).IsEqualTo(new GeoPoint3(15, 5, 5), Tight));

            DxfPolyline3d again = ByHandle<DxfPolyline3d>(DxfDatabase.Parse(db.ToText()), polyline.Handle);
            Assert.Equal(DxfPoly3dType.CubicSplinePoly, again.PolyType);
            Assert.Equal(8.0, again.EndParam);

            again.ConvertToPolyType(DxfPoly3dType.SimplePoly);
            Assert.Equal(4, again.Count());
        }

        [Fact]
        public void APolylineInSpaceFittedWithASpline_KeepsTheCurveAfterTheFirstOfTheFrame_AsAutoCad2021Keeps()
        {
            // 3DPOLY and PEDIT Spline in AutoCAD 2021 (SPLINETYPE 6, SPLINESEGS 8), walked with entnext: the first vertex of the
            // frame, those of the curve, then the rest of the frame; open on five vertices, and closed on four.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            var open = new DxfPolyline3d(DxfPoly3dType.SimplePoly, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 10, 5), new GeoPoint3(20, 0, 10), new GeoPoint3(30, 10, 0), new GeoPoint3(40, 0, 5) }, false);
            var closed = new DxfPolyline3d(DxfPoly3dType.SimplePoly, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 10, 5), new GeoPoint3(20, 0, 10), new GeoPoint3(30, 10, 0) }, true);
            db.ModelSpace.AppendEntity(open);
            db.ModelSpace.AppendEntity(closed);
            string[] frame = open.Select(v => v.Handle).ToArray();

            open.SplineFit(DxfPoly3dType.CubicSplinePoly, 8);
            closed.SplineFit(DxfPoly3dType.CubicSplinePoly, 8);

            List<DxfPolylineVertex3d> vertices = open.ToList();
            Assert.Equal(5 + 17, vertices.Count);
            Assert.Equal(frame, new[] { vertices[0] }.Concat(vertices.Skip(18)).Select(v => v.Handle).ToArray());
            Assert.All(vertices.Skip(1).Take(17), v => Assert.Equal(DxfVertex3dType.FitVertex, v.VertexType));
            Assert.True(vertices[1].Position.IsEqualTo(new GeoPoint3(0, 0, 0), Tight));
            Assert.True(vertices[2].Position.IsEqualTo(new GeoPoint3(3.525390625, 3.0859375, 1.75537109375), Tight));
            Assert.True(vertices[8].Position.IsEqualTo(new GeoPoint3(18.115234375, 5.1953125, 6.54541015625), Tight));
            Assert.True(vertices[17].Position.IsEqualTo(new GeoPoint3(40, 0, 5), Tight));

            List<DxfPolylineVertex3d> round = closed.ToList();
            Assert.Equal(4 + 32, round.Count);
            Assert.All(new[] { round[0] }.Concat(round.Skip(33)), v => Assert.Equal(DxfVertex3dType.ControlVertex, v.VertexType));
            Assert.True(round[1].Position.IsEqualTo(new GeoPoint3(10, 20.0 / 3.0, 5), Tight));
            Assert.True(round[2].Position.IsEqualTo(new GeoPoint3(11.25, 6.5234375, 5.6201171875), Tight));
            Assert.True(round[32].Position.IsEqualTo(new GeoPoint3(8.763020833333, 6.5234375, 4.376627604167), new Tolerance(1E-9, 1E-9)));
        }

        [Fact]
        public void AMeshSmoothed_IsItsGridAndThePointsOfTheSurface_DesmoothedBack()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            var mesh = new DxfPolygonMesh(DxfPolyMeshType.SimpleMesh, 3, 4, Enumerable.Range(0, 12).Select(i => new GeoPoint3(i / 4, Math.Cos(i % 4 * Math.PI / 2), Math.Sin(i % 4 * Math.PI / 2))), false, true);
            db.ModelSpace.AppendEntity(mesh);
            string[] grid = mesh.Select(v => v.Handle).ToArray();

            mesh.SurfaceFit(DxfPolyMeshType.CubicSurfaceMesh, 6, 8);

            List<DxfPolygonMeshVertex> points = mesh.ToList();
            Assert.Equal(DxfPolyMeshType.CubicSurfaceMesh, mesh.PolyMeshType);
            // 6 spans along the open M make 7 rows, as AutoCAD's PEDIT smooths (check 14); 8 points round the closed N.
            Assert.Equal(12 + 56, points.Count);
            Assert.Equal(6, mesh.MSurfaceDensity);
            Assert.Equal(8, mesh.NSurfaceDensity);
            Assert.Equal(grid, points.Take(12).Select(v => v.Handle).ToArray());
            Assert.All(points.Take(12), v => Assert.Equal(DxfVertex3dType.ControlVertex, v.VertexType));
            Assert.All(points.Skip(12), v => Assert.Equal(DxfVertex3dType.FitVertex, v.VertexType));

            // Open along M from the first row to the last; the first and last rows of the surface are on those of the grid's ends.
            Assert.Equal(0.0, points[12].Position.X, 12);
            Assert.Equal(2.0, points[points.Count - 1].Position.X, 12);
            Assert.Equal(8, ((GeoSolid3)Assert.Single(ByHandle<DxfPolygonMesh>(DxfDatabase.Parse(db.ToText()), mesh.Handle).GetGeometry())).Faces.Count);

            mesh.ConvertToPolyMeshType(DxfPolyMeshType.SimpleMesh);
            Assert.Equal(DxfPolyMeshType.SimpleMesh, mesh.PolyMeshType);
            Assert.Equal(grid, mesh.Select(v => v.Handle).ToArray());
            Assert.All(mesh, v => Assert.Equal(DxfVertex3dType.SimpleVertex, v.VertexType));
            Assert.Equal(6, mesh.MSurfaceDensity);
        }

        [Fact]
        public void ABezierSurfaceOfFourPoints_IsTheBilinearPatch_AndTheHeadersSurfaceIsTaken()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            db.SetHeader("$SURFTYPE", new DxfTypedValue(70, "8"));
            db.SetHeader("$SURFU", new DxfTypedValue(70, "3"));
            db.SetHeader("$SURFV", new DxfTypedValue(70, "3"));
            var mesh = new DxfPolygonMesh(DxfPolyMeshType.SimpleMesh, 2, 2, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(0, 10, 0), new GeoPoint3(10, 10, 4) }, false, false);
            db.ModelSpace.AppendEntity(mesh);

            mesh.SurfaceFit();

            List<DxfPolygonMeshVertex> points = mesh.ToList();
            Assert.Equal(DxfPolyMeshType.BezierSurfaceMesh, mesh.PolyMeshType);
            // 3 spans each way, 4 rows of 4 points, as AutoCAD's PEDIT smooths (check 14): the patch at a third along each.
            Assert.Equal(4 + 16, points.Count);
            Assert.True(points[4 + 5].Position.IsEqualTo(new GeoPoint3(10.0 / 3.0, 10.0 / 3.0, 4.0 / 9.0), Tight));
            Assert.True(points[4 + 15].Position.IsEqualTo(new GeoPoint3(10, 10, 4), Tight));
            Assert.Throws<ArgumentOutOfRangeException>(() => mesh.SurfaceFit(DxfPolyMeshType.SimpleMesh, 3, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => mesh.SurfaceFit(DxfPolyMeshType.QuadSurfaceMesh, 1, 3));
            var big = new DxfPolygonMesh(DxfPolyMeshType.SimpleMesh, 12, 2, Enumerable.Range(0, 24).Select(i => new GeoPoint3(i / 2, i % 2, 0)), false, false);
            Assert.Throws<InvalidOperationException>(() => big.SurfaceFit(DxfPolyMeshType.BezierSurfaceMesh, 4, 4));
        }

        [Fact]
        public void APointOrFaceOfAPolyfaceMeshTakenOut_KeepsItsCountsAndFacesTrue()
        {
            // Four points and the faces 1, 2, 3 and 2, -3, 4: the first face taken out, the first point is no corner, and taken
            // out the other face is 1, -2, 3.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            var mesh = new DxfPolyFaceMesh();
            db.ModelSpace.AppendEntity(mesh);

            foreach (GeoPoint3 point in new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(0, 10, 0), new GeoPoint3(10, 10, 1) })
            {
                mesh.AppendVertex(new DxfPolyFaceMeshVertex(point));
            }

            mesh.AppendFaceRecord(new DxfFaceRecord(1, 2, 3, 0));
            mesh.AppendFaceRecord(new DxfFaceRecord(2, -3, 4, 0));
            Assert.Equal(4, mesh.NumVertices);
            Assert.Equal(2, mesh.NumFaces);
            DxfPolyFaceMeshVertex first = mesh.OfType<DxfPolyFaceMeshVertex>().First();

            Assert.Throws<InvalidOperationException>(() => first.Erase());
            mesh.OfType<DxfFaceRecord>().First().Erase();
            Assert.Equal(1, mesh.NumFaces);
            first.Erase();

            DxfPolyFaceMesh again = ByHandle<DxfPolyFaceMesh>(DxfDatabase.Parse(db.ToText()), mesh.Handle);
            Assert.Equal(3, again.NumVertices);
            Assert.Equal(1, again.NumFaces);
            DxfFaceRecord face = Assert.Single(again.OfType<DxfFaceRecord>());
            Assert.Equal(new short[] { 1, 2, 3 }, new[] { face.GetVertexAt(0), face.GetVertexAt(1), face.GetVertexAt(2) });
            Assert.False(face.IsEdgeVisibleAt(1));
            Assert.Equal(Bits(new GeoPoint3(10, 0, 0)), Bits(again.OfType<DxfPolyFaceMeshVertex>().First().Position));
        }
    }
}
