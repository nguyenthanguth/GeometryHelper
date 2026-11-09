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
            Assert.Equal(13, vertices.Count);
            Assert.All(vertices.Take(4), v => Assert.Equal(DxfVertex3dType.ControlVertex, v.VertexType));
            Assert.All(vertices.Skip(4), v => Assert.Equal(DxfVertex3dType.FitVertex, v.VertexType));
            Assert.True(polyline.EndPoint.IsEqualTo(new GeoPoint3(30, 10, 10), Tight));
            Assert.True(polyline.GetPointAtParameter(4).IsEqualTo(new GeoPoint3(15, 5, 5), Tight));

            DxfPolyline3d again = ByHandle<DxfPolyline3d>(DxfDatabase.Parse(db.ToText()), polyline.Handle);
            Assert.Equal(DxfPoly3dType.CubicSplinePoly, again.PolyType);
            Assert.Equal(8.0, again.EndParam);

            again.ConvertToPolyType(DxfPoly3dType.SimplePoly);
            Assert.Equal(4, again.Count());
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
