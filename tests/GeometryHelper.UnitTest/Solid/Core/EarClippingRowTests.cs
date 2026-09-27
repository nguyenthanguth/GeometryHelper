using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A face with a row of points along a straight edge is meshed, not handed back as a fan.
    /// </summary>
    /// <remarks>
    /// Ear clipping takes the first ear round the loop, so a face whose boundary carries a row of points in
    /// line — which merging coplanar faces leaves wherever a neighbour had a corner — ends with that row and
    /// nothing across from it. No corner of a straight row turns, the clipping gave up, and the face fell back
    /// to the fan of its outline, which covers its holes over. A plate with its openings cut in came out meshed
    /// across every hole: a ray down a bolt hole hit it, and a body standing in a hole touched it.
    /// </remarks>
    public class EarClippingRowTests
    {
        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoAabb3(new GeoPoint3(x0, y0, z0), new GeoPoint3(x1, y1, z1)).ToObb().ToSolid();

        private static GeoSolid3 Plate()
            => Box(0, 0, 0, 200, 100, 20).WithOpenings(new[]
            {
                Box(40, 40, -1, 60, 60, 21),
                Box(120, 30, -1, 150, 70, 21).TransformBy(GeoTransform3.RotationAxis(new GeoPoint3(135, 50, 10), GeoVector3.ZAxis, 0.4)),
                Box(180, -5, -1, 205, 30, 21),
            });

        [Fact]
        public void AFaceWithARowOfPointsAlongOneEdgeIsMeshed()
        {
            GeoPoint3 P(double y, double z) => new GeoPoint3(200, y, z);
            var face = new GeoFace3(new GeoPolygon3(
                P(30, 0), P(40, 0), P(55.767, 0), P(60, 0), P(99.196, 0), P(100, 0),
                P(100, 20), P(99.196, 20), P(60, 20), P(55.767, 20), P(40, 20), P(30, 20)));

            Assert.True(EarClipping.TryTriangulate(face, Tolerance.Global, out GeoTriangle3[] triangles));
            Assert.Equal(face.Area, triangles.Sum(t => t.Area), 9);
        }

        [Fact]
        public void APlateWithItsOpeningsCutInIsMeshedRoundTheHoles()
        {
            Assert.True(Plate().TryCutOpenings(out GeoSolid3 material));

            Assert.Equal(material.Faces.Sum(f => f.Area), Plate().TriangulateSurface().Sum(t => t.Area), 6);

            foreach (GeoFace3 face in material.Faces)
            {
                Assert.True(EarClipping.TryTriangulate(face, Tolerance.Global, out _));
            }
        }

        [Fact]
        public void ARayDownAHoleMissesTheMeshOfThePlate()
        {
            var down = new GeoRay3(new GeoPoint3(135, 50, 100), new GeoVector3(0, 0, -1));

            Assert.Empty(Plate().BuildIndex().GetIntersections(down));
            Assert.Empty(Plate().GetIntersections(down));
        }
    }
}
