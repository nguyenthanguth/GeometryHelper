using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using GeometryHelper.Meshing;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The ways of asking an ellipse a question, held against each other, as <see cref="PlaneMirrorTests"/> holds the
    /// straight shapes. Every operation lives in <see cref="Ellipse2"/>, the ellipse first, and GeoEllipse2's members
    /// hand it on; where a tolerance is read there is a form that takes one and a form reading Tolerance.Global. A
    /// member wired to the wrong static, or an overload that quietly drops the tolerance it was handed, gives an
    /// answer that is plausible and wrong, which shows up nowhere else. The answers here are compared exactly: the
    /// forms are one computation, not two.
    /// </summary>
    public class Ellipse2MirrorTests
    {
        private static readonly Tolerance Global = Tolerance.Global;

        // A point 0.5 outside the rim of the tilted 300 by 100: off it within a thousandth, on it within a whole unit.
        private static GeoPoint2 Near(GeoEllipse2 ellipse) => Ellipse2Oracle.OffRim(ellipse, 1.1, 0.5);

        private static readonly Tolerance Loose = new Tolerance(1.0, 1E-5);

        private static readonly Tolerance Strict = new Tolerance(1E-3, 1E-5);

        [Fact]
        public void APointIsPlacedTheSameWhicheverWayItIsAsked()
        {
            GeoEllipse2 ellipse = Ellipse2Oracle.Tilted();

            foreach (GeoPoint2 point in new[] { ellipse.Center, Near(ellipse), Ellipse2Oracle.OffRim(ellipse, 4.0, -0.0004), new GeoPoint2(900, 900) })
            {
                Assert.Equal(Ellipse2.Contains(ellipse, point, Global), ellipse.Contains(point, Global));
                Assert.Equal(ellipse.Contains(point, Global), ellipse.Contains(point));
                Assert.Equal(Ellipse2.Contains(ellipse, point), ellipse.Contains(point));

                Assert.Equal(Ellipse2.Locate(ellipse, point, Global), ellipse.Locate(point, Global));
                Assert.Equal(ellipse.Locate(point, Global), ellipse.Locate(point));
                Assert.Equal(Ellipse2.Locate(ellipse, point), ellipse.Locate(point));

                Assert.Equal(Ellipse2.IsPointOn(ellipse, point, Global), ellipse.IsPointOn(point, Global));
                Assert.Equal(ellipse.IsPointOn(point, Global), ellipse.IsPointOn(point));
                Assert.Equal(Ellipse2.IsPointOn(ellipse, point), ellipse.IsPointOn(point));

                Assert.Equal(Ellipse2.GetClosestPointOnBoundary(ellipse, point), ellipse.GetClosestPointOnBoundary(point));
                Assert.Equal(Ellipse2.GetAngleAtPoint(ellipse, point), ellipse.GetAngleAtPoint(point));
            }
        }

        [Fact]
        public void TheToleranceHandedInIsTheOneRead()
        {
            // Half a unit out: on the rim and contained within a whole unit, outside within a thousandth, through the
            // static and the member alike.
            GeoEllipse2 ellipse = Ellipse2Oracle.Tilted();
            GeoPoint2 point = Near(ellipse);

            Assert.True(ellipse.IsPointOn(point, Loose));
            Assert.True(Ellipse2.IsPointOn(ellipse, point, Loose));
            Assert.True(ellipse.Contains(point, Loose));
            Assert.True(Ellipse2.Contains(ellipse, point, Loose));
            Assert.False(ellipse.IsPointOn(point, Strict));
            Assert.False(Ellipse2.IsPointOn(ellipse, point, Strict));
            Assert.False(ellipse.Contains(point, Strict));
            Assert.False(Ellipse2.Contains(ellipse, point, Strict));

            // 300 by 299.5 is a circle within a whole unit and not within a thousandth; moved 0.5 it is the same ellipse
            // within the unit only.
            var nearlyRound = new GeoEllipse2(ellipse.Center, ellipse.MajorAxis, 300, 299.5);
            Assert.True(nearlyRound.IsCircle(Loose));
            Assert.True(Ellipse2.IsCircle(nearlyRound, Loose));
            Assert.False(nearlyRound.IsCircle(Strict));
            Assert.False(Ellipse2.IsCircle(nearlyRound, Strict));

            GeoEllipse2 moved = ellipse.Translate(new GeoVector2(0.5, 0));
            Assert.True(ellipse.IsEqualTo(moved, Loose));
            Assert.True(Ellipse2.IsEqualTo(ellipse, moved, Loose));
            Assert.False(ellipse.IsEqualTo(moved, Strict));
            Assert.False(Ellipse2.IsEqualTo(ellipse, moved, Strict));
        }

        [Fact]
        public void TheFormsWithoutAToleranceReadTheGlobalOne()
        {
            GeoEllipse2 ellipse = Ellipse2Oracle.Tilted();
            var nearlyRound = new GeoEllipse2(ellipse.Center, ellipse.MajorAxis, 300, 299.9995);
            GeoEllipse2 moved = ellipse.Translate(new GeoVector2(0.0004, 0));

            Assert.Equal(nearlyRound.IsCircle(Global), nearlyRound.IsCircle());
            Assert.Equal(Ellipse2.IsCircle(nearlyRound, Global), Ellipse2.IsCircle(nearlyRound));
            Assert.Equal(ellipse.IsEqualTo(moved, Global), ellipse.IsEqualTo(moved));
            Assert.Equal(Ellipse2.IsEqualTo(ellipse, moved, Global), Ellipse2.IsEqualTo(ellipse, moved));
        }

        [Fact]
        public void TheRimIsWalkedTheSameWhicheverWayItIsAsked()
        {
            GeoEllipse2 ellipse = Ellipse2Oracle.Tilted();
            GeoPoint2 point = Near(ellipse);

            foreach (double value in new[] { -0.1, 0.0, 0.3, 0.75, 1.3 })
            {
                Assert.Equal(Ellipse2.GetPointAtAngle(ellipse, value * 6), ellipse.GetPointAtAngle(value * 6));
                Assert.Equal(Ellipse2.GetPointAtParameter(ellipse, value), ellipse.GetPointAtParameter(value));
                Assert.Equal(Ellipse2.GetDistanceAtParameter(ellipse, value), ellipse.GetDistanceAtParameter(value));
                Assert.Equal(Ellipse2.GetParameterAtDistance(ellipse, value * 1000), ellipse.GetParameterAtDistance(value * 1000));
                Assert.Equal(Ellipse2.GetPointAtDistance(ellipse, value * 1000), ellipse.GetPointAtDistance(value * 1000));
            }

            Assert.Equal(Ellipse2.GetParameterAtPoint(ellipse, point), ellipse.GetParameterAtPoint(point));
            Assert.Equal(Ellipse2.GetDistanceAtPoint(ellipse, point), ellipse.GetDistanceAtPoint(point));
        }

        [Fact]
        public void AnEllipseIsTransformedAndCutTheSameWhicheverWayItIsAsked()
        {
            GeoEllipse2 ellipse = Ellipse2Oracle.Tilted();
            var shear = new GeoTransform2(new double[,] { { 1, 0.4, 5 }, { 0, 1, -3 }, { 0, 0, 1 } });

            Assert.Equal(Ellipse2.TransformBy(ellipse, shear), ellipse.TransformBy(shear));

            Assert.Equal(Ellipse2.ToPolygonByChordTolerance(ellipse, 0.05).Vertices, ellipse.ToPolygonByChordTolerance(0.05).Vertices);
            Assert.Equal(Ellipse2.ToPolygonByChordTolerance(ellipse, 0.0).Vertices, ellipse.ToPolygon().Vertices);
            Assert.Equal(Ellipse2.ToPolygonBySpacing(ellipse, 31.0).Vertices, ellipse.ToPolygonBySpacing(31.0).Vertices);
            Assert.Equal(Ellipse2.ToPolygon(ellipse, 12).Vertices, ellipse.ToPolygon(12).Vertices);

            // Asked twice, the same polygon: no state carried from one call to the next.
            Assert.Equal(ellipse.ToPolygonByChordTolerance(0.05).Vertices, ellipse.ToPolygonByChordTolerance(0.05).Vertices);
        }

        [Fact]
        public void TheSurfaceIsBrokenUpTheSameWhicheverWayItIsAsked()
        {
            GeoEllipse2 ellipse = Ellipse2Oracle.Tilted();

            Assert.Equal(Ellipse2.TriangulateSurface(ellipse, 0.05, Global), ellipse.TriangulateSurface(0.05, Global));
            Assert.Equal(ellipse.TriangulateSurface(0.05, Global), ellipse.TriangulateSurface(0.05));
            Assert.Equal(Ellipse2.TriangulateSurface(ellipse, 0.05), ellipse.TriangulateSurface(0.05));
            Assert.Equal(ellipse.TriangulateSurface(0.0, Global), ellipse.TriangulateSurface(Global));
            Assert.Equal(ellipse.TriangulateSurface(Global), ellipse.TriangulateSurface());

            var options = new MeshOptions(MeshKind.Triangles, chordTolerance: 0.05);
            GeoMesh2 fromStatic = Mesh2.ToMesh(ellipse, options, Global);

            foreach (GeoMesh2 mesh in new[] { ellipse.ToMesh(options, Global), ellipse.ToMesh(options), Mesh2.ToMesh(ellipse, options) })
            {
                Assert.Equal(fromStatic.Vertices, mesh.Vertices);
                Assert.Equal(fromStatic.Area, mesh.Area);
            }

            GeoMesh2 byKind = ellipse.ToMesh(MeshKind.Convex);
            GeoMesh2 byOptions = ellipse.ToMesh(MeshOptions.Convex, Global);
            Assert.Equal(byOptions.Vertices, byKind.Vertices);
            Assert.Equal(byOptions.FaceCount, byKind.FaceCount);
        }
    }
}
