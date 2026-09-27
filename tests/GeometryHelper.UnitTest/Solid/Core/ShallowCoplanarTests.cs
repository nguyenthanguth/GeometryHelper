using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Two shapes lie in one plane when the whole of each does — not when their planes are parallel within the
    /// angle tolerance and one point of one lands on the other.
    /// </summary>
    /// <remarks>
    /// The angle tolerance is a whole degree. A shape turned half a degree about a line through its first
    /// corner passed the old test: a metre long, its far side stood nine millimetres off the plane, and the
    /// flat booleans projected it onto the plane without a word, contact reported a patch where two bodies
    /// met along an edge, and the solid booleans took a piece of real boundary crossing another face at a
    /// shallow angle for the inside of the body.
    /// </remarks>
    public class ShallowCoplanarTests
    {
        private const double HalfDegree = Math.PI / 360.0;

        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoAabb3(new GeoPoint3(x0, y0, z0), new GeoPoint3(x1, y1, z1)).ToObb().ToSolid();

        /// <summary>A metre square on the ground, and the same turned half a degree about its first edge.</summary>
        private static (GeoPolygon3 Flat, GeoPolygon3 Turned) Squares()
        {
            var flat = new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(1000, 0, 0), new GeoPoint3(1000, 1000, 0), new GeoPoint3(0, 1000, 0));
            GeoTransform3 turn = GeoTransform3.RotationAxis(GeoPoint3.Origin, GeoVector3.XAxis, HalfDegree);

            return (flat, new GeoPolygon3(flat.Vertices.Select(v => turn.Transform(v))));
        }

        [Fact]
        public void APolygonTurnedHalfADegreeDoesNotShareThePlane()
        {
            var (flat, turned) = Squares();

            // Its far side stands almost nine millimetres off the ground.
            Assert.Equal(1000.0 * Math.Sin(HalfDegree), turned.Vertices.Max(v => v.Z), 9);

            Assert.False(flat.SharesPlaneWith(turned.GetPlane()));
            Assert.False(turned.SharesPlaneWith(flat.GetPlane()));
            Assert.False(new GeoFace3(turned).SharesPlaneWith(flat.GetPlane()));
            Assert.True(flat.SharesPlaneWith(flat.GetPlane()));
        }

        [Fact]
        public void TheFlatBooleansRefuseAPolygonTurnedHalfADegree()
        {
            var (flat, turned) = Squares();

            Assert.Throws<ArgumentException>(() => flat.Union(turned));
            Assert.Throws<ArgumentException>(() => Boolean3.Intersect(new GeoFace3(flat), new GeoFace3(turned)));
        }

        [Fact]
        public void AnArcLoopTurnedHalfADegreeDoesNotShareThePlaneEither()
        {
            var (flat, turned) = Squares();
            var loop = new GeoPolygonArc3(flat.Vertices);
            var tilted = new GeoPolygonArc3(turned.Vertices);

            Assert.False(loop.SharesPlaneWith(tilted.GetPlane()));
            Assert.Throws<ArgumentException>(() => loop.Union(tilted));
            Assert.Throws<ArgumentException>(() => loop.Union(turned));
        }

        [Fact]
        public void ABlockTiltedOntoAnEdgeHasNoContactPatch()
        {
            // A block standing on another, tilted half a degree about one bottom edge and then about the
            // opposite one: either way the two meet along an edge, which is no patch.
            GeoSolid3 footing = Box(0, 0, 0, 1000, 1000, 500);
            GeoSolid3 block = Box(0, 0, 500, 1000, 1000, 1000);

            foreach (GeoPoint3 hinge in new[] { new GeoPoint3(0, 0, 500), new GeoPoint3(0, 1000, 500) })
            {
                double sense = hinge.Y == 0 ? 1.0 : -1.0;
                GeoSolid3 tilted = block.TransformBy(GeoTransform3.RotationAxis(hinge, GeoVector3.XAxis, sense * HalfDegree));

                Assert.False(footing.TryGetContact(tilted, out GeoFace3[] contact));
                Assert.Empty(contact);
            }
        }
    }
}
