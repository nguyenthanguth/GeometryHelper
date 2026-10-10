using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using GeometryHelper.Meshing;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// The ways of asking an ellipse in space a question, held against each other, as Ellipse2MirrorTests holds the
    /// ellipse in the plane. Every operation lives in <see cref="Ellipse3"/> (decision 16), the plane-to-space ones in
    /// <see cref="PlanarMap"/> (decision 11) and the transformation in <see cref="GeoTransform3"/>; the members hand
    /// them on, and where a tolerance is read there is a form taking one and a form reading Tolerance.Global. A member
    /// wired to the wrong static, or an overload that drops the tolerance it was handed, gives an answer that is
    /// plausible and wrong. The answers here are compared exactly: the forms are one computation, not two.
    /// </summary>
    public class Ellipse3MirrorTests
    {
        private static readonly Tolerance Global = Tolerance.Global;

        private static readonly Tolerance Loose = new Tolerance(1.0, 1E-5);

        private static readonly Tolerance Strict = new Tolerance(1E-3, 1E-5);

        [Fact]
        public void APointIsPlacedAndMeasuredTheSameWhicheverWayItIsAsked()
        {
            Ellipse3Oracle.Specimen specimen = Ellipse3Oracle.Tilted();
            GeoEllipse3 ellipse = specimen.Build();

            GeoPoint3[] points =
            {
                specimen.Center, specimen.OffRim(1.1, 0.5, 0.0), specimen.OffRim(4.0, -0.0004, 0.0), specimen.At(100, 20, 40),
                specimen.At(900, 900, -300),
            };

            foreach (GeoPoint3 point in points)
            {
                Assert.Equal(Ellipse3.Contains(ellipse, point, Global), ellipse.Contains(point, Global));
                Assert.Equal(ellipse.Contains(point, Global), ellipse.Contains(point));
                Assert.Equal(Ellipse3.Contains(ellipse, point), ellipse.Contains(point));

                Assert.Equal(Ellipse3.Locate(ellipse, point, Global), ellipse.Locate(point, Global));
                Assert.Equal(ellipse.Locate(point, Global), ellipse.Locate(point));
                Assert.Equal(Ellipse3.Locate(ellipse, point), ellipse.Locate(point));

                Assert.Equal(Ellipse3.IsPointOn(ellipse, point, Global), ellipse.IsPointOn(point, Global));
                Assert.Equal(ellipse.IsPointOn(point, Global), ellipse.IsPointOn(point));
                Assert.Equal(Ellipse3.IsPointOn(ellipse, point), ellipse.IsPointOn(point));

                Assert.Equal(Ellipse3.DistanceTo(ellipse, point), ellipse.DistanceTo(point));
                Assert.Equal(Ellipse3.DistanceTo(ellipse, point, Global), ellipse.DistanceTo(point));
                Assert.Equal(Ellipse3.GetClosestPointOnSurface(ellipse, point), ellipse.GetClosestPointOnSurface(point));
                Assert.Equal(Ellipse3.GetClosestPointOnSurface(ellipse, point, Global), ellipse.GetClosestPointOnSurface(point));
                Assert.Equal(Ellipse3.GetClosestPointOnBoundary(ellipse, point), ellipse.GetClosestPointOnBoundary(point));

                Assert.Equal(Ellipse3.GetAngleAtPoint(ellipse, point), ellipse.GetAngleAtPoint(point));
                Assert.Equal(Ellipse3.GetParameterAtPoint(ellipse, point), ellipse.GetParameterAtPoint(point));
                Assert.Equal(Ellipse3.GetParameterAtPoint(ellipse, point, Global), ellipse.GetParameterAtPoint(point, Global));
                Assert.Equal(ellipse.GetParameterAtPoint(point, Global), ellipse.GetParameterAtPoint(point));
                Assert.Equal(Ellipse3.GetDistanceAtPoint(ellipse, point), ellipse.GetDistanceAtPoint(point));
                Assert.Equal(Ellipse3.GetDistanceAtPoint(ellipse, point, Global), ellipse.GetDistanceAtPoint(point, Global));
                Assert.Equal(ellipse.GetDistanceAtPoint(point, Global), ellipse.GetDistanceAtPoint(point));
            }
        }

        [Fact]
        public void TheToleranceHandedInIsTheOneRead()
        {
            // Half a unit out: on the rim and held within a whole unit, outside within a thousandth, through the static and
            // the member alike.
            Ellipse3Oracle.Specimen specimen = Ellipse3Oracle.Tilted();
            GeoEllipse3 ellipse = specimen.Build();
            GeoPoint3 point = specimen.OffRim(1.1, 0.5, 0.0);

            Assert.True(ellipse.IsPointOn(point, Loose));
            Assert.True(Ellipse3.IsPointOn(ellipse, point, Loose));
            Assert.True(ellipse.Contains(point, Loose));
            Assert.True(Ellipse3.Contains(ellipse, point, Loose));
            Assert.Equal(Enums.PointLocation.OnSide, Ellipse3.Locate(ellipse, point, Loose));
            Assert.False(ellipse.IsPointOn(point, Strict));
            Assert.False(Ellipse3.IsPointOn(ellipse, point, Strict));
            Assert.False(ellipse.Contains(point, Strict));
            Assert.False(Ellipse3.Contains(ellipse, point, Strict));
            Assert.Equal(Enums.PointLocation.OutSide, Ellipse3.Locate(ellipse, point, Strict));

            // 300 by 299.5 is a circle within a whole unit and not within a thousandth; moved 0.5 it is the same ellipse
            // within the unit only.
            var nearlyRound = new GeoEllipse3(specimen.Center, specimen.N, specimen.U, 300, 299.5);
            Assert.True(nearlyRound.IsCircle(Loose));
            Assert.True(Ellipse3.IsCircle(nearlyRound, Loose));
            Assert.False(nearlyRound.IsCircle(Strict));
            Assert.False(Ellipse3.IsCircle(nearlyRound, Strict));
            Assert.Equal(Ellipse3.IsCircle(nearlyRound, Global), nearlyRound.IsCircle());
            Assert.Equal(Ellipse3.IsCircle(nearlyRound), nearlyRound.IsCircle());

            GeoEllipse3 moved = ellipse.Translate(specimen.U.Multiply(0.5));
            Assert.True(ellipse.IsEqualTo(moved, Loose));
            Assert.True(Ellipse3.IsEqualTo(ellipse, moved, Loose));
            Assert.False(ellipse.IsEqualTo(moved, Strict));
            Assert.False(Ellipse3.IsEqualTo(ellipse, moved, Strict));
            Assert.Equal(Ellipse3.IsEqualTo(ellipse, moved, Global), ellipse.IsEqualTo(moved));
            Assert.Equal(Ellipse3.IsEqualTo(ellipse, moved), ellipse.IsEqualTo(moved));
        }

        [Fact]
        public void TheRimIsWalkedTheSameWhicheverWayItIsAsked()
        {
            GeoEllipse3 ellipse = Ellipse3Oracle.Tilted().Build();

            Assert.Equal(Ellipse3.GetLength(ellipse), ellipse.Length);

            foreach (double x in new[] { -0.3, 0.0, 0.27, 1.6 })
            {
                Assert.Equal(Ellipse3.GetPointAtAngle(ellipse, x * 7), ellipse.GetPointAtAngle(x * 7));
                Assert.Equal(Ellipse3.GetPointAtParameter(ellipse, x), ellipse.GetPointAtParameter(x));
                Assert.Equal(Ellipse3.GetDistanceAtParameter(ellipse, x), ellipse.GetDistanceAtParameter(x));
                Assert.Equal(Ellipse3.GetPointAtDistance(ellipse, x * 1000), ellipse.GetPointAtDistance(x * 1000));
                Assert.Equal(Ellipse3.GetParameterAtDistance(ellipse, x * 1000), ellipse.GetParameterAtDistance(x * 1000));
            }
        }

        [Fact]
        public void TheEllipseIsMovedProjectedAndBoxedTheSameWhicheverWayItIsAsked()
        {
            Ellipse3Oracle.Specimen specimen = Ellipse3Oracle.Tilted();
            GeoEllipse3 ellipse = specimen.Build();
            GeoCircle3 circle = Ellipse3Oracle.CircleTiltedSixty();
            GeoTransform3 motion = GeoTransform3.Translation(new GeoVector3(3, -4, 5)) * GeoTransform3.Scaling(2, 1, 0.5);
            var plane = new GeoPlane3(new GeoPoint3(10, 20, -30), new GeoVector3(0.2, -0.3, 1));
            var frame = new GeoCoordinateSystem3(new GeoPoint3(100, 200, 300), new GeoVector3(1, 1, 0), new GeoVector3(-1, 1, 1));
            GeoEllipse2 flat = Plane.Ellipse2Oracle.Tilted();

            Assert.Equal(Ellipse3.Translate(ellipse, new GeoVector3(1, 2, 3)), ellipse.Translate(new GeoVector3(1, 2, 3)));
            Assert.Equal(Ellipse3.TransformBy(ellipse, motion), ellipse.TransformBy(motion));
            Assert.Equal(motion.Transform(ellipse), ellipse.TransformBy(motion));
            Assert.Equal(Ellipse3.GetAabb(ellipse), ellipse.GetAabb());

            Assert.Equal(Ellipse3.ProjectToPlane(ellipse, plane), ellipse.ProjectToPlane(plane));
            Assert.Equal(Ellipse3.ProjectToPlane(circle, plane), circle.ProjectToPlane(plane));
            Assert.Equal(PlanarMap.ProjectToEllipse2(frame, ellipse), ellipse.ProjectToEllipse2(frame));
            Assert.Equal(PlanarMap.ProjectToEllipse2(frame, circle), circle.ProjectToEllipse2(frame));
            Assert.Equal(PlanarMap.ToEllipse3(frame, flat), flat.ToEllipse3(frame));
        }

        [Fact]
        public void TheEllipseIsFlattenedAndMeshedTheSameWhicheverWayItIsAsked()
        {
            GeoEllipse3 ellipse = Ellipse3Oracle.Tilted().Build();

            Assert.Equal(Ellipse3.ToPolygonByChordTolerance(ellipse, 0.0).Vertices, ellipse.ToPolygon().Vertices);
            Assert.Equal(Ellipse3.ToPolygonByChordTolerance(ellipse, 0.5).Vertices, ellipse.ToPolygonByChordTolerance(0.5).Vertices);
            Assert.Equal(Ellipse3.ToPolygonBySpacing(ellipse, 25).Vertices, ellipse.ToPolygonBySpacing(25).Vertices);
            Assert.Equal(Ellipse3.ToPolygon(ellipse, 7).Vertices, ellipse.ToPolygon(7).Vertices);
            Assert.Equal(Ellipse3.ToPolylineByChordTolerance(ellipse, 0.0).Vertices, ellipse.ToPolyline().Vertices);
            Assert.Equal(Ellipse3.ToPolylineByChordTolerance(ellipse, 0.5).Vertices, ellipse.ToPolylineByChordTolerance(0.5).Vertices);
            Assert.Equal(Ellipse3.ToPolylineBySpacing(ellipse, 25).Vertices, ellipse.ToPolylineBySpacing(25).Vertices);
            Assert.Equal(Ellipse3.ToPolyline(ellipse, 7).Vertices, ellipse.ToPolyline(7).Vertices);

            MeshOptions grid = MeshOptions.Grid(40, 40);
            AssertSameMesh(Mesh3.ToMesh(ellipse, grid, MeshPlacement3.Own, Global), ellipse.ToMesh(grid, MeshPlacement3.Own, Global));
            AssertSameMesh(ellipse.ToMesh(grid, MeshPlacement3.Own, Global), ellipse.ToMesh(grid, MeshPlacement3.Own));
            AssertSameMesh(Mesh3.ToMesh(ellipse, grid, MeshPlacement3.Own), ellipse.ToMesh(grid, MeshPlacement3.Own));
            AssertSameMesh(Mesh3.ToMesh(ellipse, grid, Global), ellipse.ToMesh(grid, Global));
            AssertSameMesh(ellipse.ToMesh(grid, Global), ellipse.ToMesh(grid));
            AssertSameMesh(Mesh3.ToMesh(ellipse, grid), ellipse.ToMesh(grid));
            AssertSameMesh(Mesh3.ToMesh(ellipse, MeshOptions.Triangles), ellipse.ToMesh(MeshKind.Triangles));
        }

        private static void AssertSameMesh(GeoMesh3 expected, GeoMesh3 actual)
        {
            Assert.Equal(expected.FaceCount, actual.FaceCount);
            Assert.Equal(expected.Vertices, actual.Vertices);
            Assert.Equal(expected.Area, actual.Area);
        }
    }
}
