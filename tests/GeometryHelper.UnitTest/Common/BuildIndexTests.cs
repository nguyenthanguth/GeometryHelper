using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using GeometryHelper.Spatial;
using Xunit;

namespace GeometryHelper.UnitTest.Common
{
    /// <summary>
    /// <c>BuildIndex</c> on the shapes: the same tree <see cref="GeoBvh2"/> and <see cref="GeoBvh3"/> could
    /// always build, reached from the shape that is in hand instead of by naming the index first.
    /// </summary>
    /// <remarks>
    /// The whole claim is that an index answers what the shape answers, only faster, so that is what these
    /// check: every answer is compared against the answer the shape gives without one. An index that agreed
    /// with nothing would be worse than no index.
    /// </remarks>
    public class BuildIndexTests
    {
        private static readonly Tolerance Loose = new Tolerance(1E-7, 1E-7);

        private static GeoPolygon2 Plate() => new GeoPolygon2(
            new GeoPoint2(0, 0), new GeoPoint2(200, 0), new GeoPoint2(200, 200), new GeoPoint2(0, 200));

        [Fact]
        public void APlaneShapesIndexFindsWhatTheShapeFinds()
        {
            GeoPolygon2 plate = Plate();
            var probe = new GeoPoint2(-50, 100);

            GeoBvh2 index = plate.BuildIndex();

            Assert.Equal(plate.VertexCount, index.EdgeCount);
            Assert.Equal(plate.DistanceTo(probe), index.DistanceTo(probe), 6);
            Assert.True(index.GetClosestPoint(probe).IsEqualTo(plate.GetClosestPointOnBoundary(probe), Loose));

            // It is a snapshot, so it holds nothing back to the shape and is the same tree the factory builds.
            Assert.Equal(GeoBvh2.FromPolygon(plate).EdgeCount, index.EdgeCount);

            // A chain, a curved loop and a curved chain index the same way.
            var chain = new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(200, 0), new GeoPoint2(200, 200));

            Assert.Equal(chain.DistanceTo(probe), chain.BuildIndex().DistanceTo(probe), 6);

            GeoPolygonArc2 rounded = plate.Fillet(30.0);

            Assert.Equal(rounded.DistanceTo(probe), rounded.BuildIndex().DistanceTo(probe), 6);
            Assert.Contains(true, rounded.BuildIndex().Edges.Select(edge => edge.IsArc));

            GeoPolylineArc2 curvedChain = chain.Fillet(30.0);

            Assert.Equal(curvedChain.DistanceTo(probe), curvedChain.BuildIndex().DistanceTo(probe), 6);
        }

        [Fact]
        public void AFacesIndexHoldsTheRimOfEveryHole()
        {
            var hole = new GeoPolygon2(
                new GeoPoint2(80, 80), new GeoPoint2(120, 80), new GeoPoint2(120, 120), new GeoPoint2(80, 120));
            var face = new GeoFace2(Plate(), new[] { hole });

            GeoBvh2 index = face.BuildIndex();

            // Four sides of the outline and four of the rim.
            Assert.Equal(8, index.EdgeCount);

            // A point sitting in the hole is answered by the rim it sits in, not by the outline far away,
            // which is the reading the face itself keeps.
            var inHole = new GeoPoint2(100, 100);

            Assert.Equal(face.DistanceTo(inHole), index.DistanceTo(inHole), 6);
            Assert.Equal(20.0, index.DistanceTo(inHole), 6);

            // Dead centre of a square hole is the same distance from all four sides, so the two walks are
            // free to name different points of the rim. What has to hold is the distance, and that the point
            // they name is on the rim at all.
            Assert.Equal(PointLocation.OnSide, hole.Locate(index.GetClosestPoint(inHole), Loose));
            Assert.Equal(PointLocation.OnSide, hole.Locate(face.GetClosestPointOnBoundary(inHole), Loose));

            // Off centre there is no tie, and then the two agree on the place as well.
            var offCentre = new GeoPoint2(100, 95);

            Assert.True(index.GetClosestPoint(offCentre).IsEqualTo(face.GetClosestPointOnBoundary(offCentre), Loose));

            Assert.Throws<ArgumentNullException>(() => GeoBvh2.FromFace(null));
        }

        [Fact]
        public void ABodysIndexFindsWhatTheBodyFinds()
        {
            GeoSolid3 cube = new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(100, 100, 100)).ToObb().ToSolid();
            var probe = new GeoPoint3(-50, 50, 50);

            GeoBvh3 index = cube.BuildIndex();

            Assert.Equal(cube.Triangulate().Length, index.TriangleCount);
            Assert.Equal(cube.DistanceTo(probe), index.DistanceTo(probe), 6);
            Assert.True(index.GetClosestPoint(probe).IsEqualTo(cube.GetClosestPointOnBoundary(probe), Loose));

            // A ray through the body is found where the body finds it, as long as it clears the diagonals.
            var ray = new GeoRay3(new GeoPoint3(-50, 40, 30), GeoVector3.XAxis);

            Assert.Equal(2, cube.GetIntersections(ray).Length);
            Assert.Equal(cube.GetIntersections(ray).Length, index.GetIntersections(ray).Length);

            // The index is over TRIANGLES and reports one hit per triangle; the body answers about itself and
            // names each place once. A ray through the middle of a face lands on the diagonal the two
            // triangles share, and there the two counts differ on purpose.
            var throughTheMiddle = new GeoRay3(new GeoPoint3(-50, 50, 50), GeoVector3.XAxis);

            Assert.Equal(2, cube.GetIntersections(throughTheMiddle).Length);
            Assert.Equal(4, index.GetIntersections(throughTheMiddle).Length);

            // The tolerance twin is the same tree.
            Assert.Equal(index.TriangleCount, cube.BuildIndex(Tolerance.Global).TriangleCount);
        }

        [Fact]
        public void AFacesSurfaceIsIndexedFromTheSurfaceMeshAndNotTheFan()
        {
            var hole = new GeoPolygon3(
                new GeoPoint3(80, 80, 0), new GeoPoint3(120, 80, 0), new GeoPoint3(120, 120, 0), new GeoPoint3(80, 120, 0));
            var face = new GeoFace3(
                new GeoPolygon3(
                    new GeoPoint3(0, 0, 0), new GeoPoint3(200, 0, 0),
                    new GeoPoint3(200, 200, 0), new GeoPoint3(0, 200, 0)),
                new[] { hole });

            GeoBvh3 index = face.BuildIndex();

            // The mesh follows the material, so the hole is left open and the triangles add up to the area.
            Assert.Equal(face.TriangulateSurface().Length, index.TriangleCount);
            Assert.Equal(face.Area, index.Triangles.Sum(t => t.Area), 3);

            // Which is the whole point: a point over the hole is not on the face, and the index agrees.
            var overHole = new GeoPoint3(100, 100, 50);

            Assert.True(index.DistanceTo(overHole) > 50.0);

            Assert.Throws<ArgumentNullException>(() => GeoBvh3.FromFace(null));
        }

        [Fact]
        public void BuildingIsWorkAndSaysSoInItsName()
        {
            GeoPolygon2 plate = Plate();

            // Two calls give two trees, because nothing is cached: the shape stays a value and the caller
            // decides how long to keep the index.
            GeoBvh2 first = plate.BuildIndex();
            GeoBvh2 second = plate.BuildIndex();

            Assert.NotSame(first, second);
            Assert.Equal(first.EdgeCount, second.EdgeCount);
        }
    }
}
