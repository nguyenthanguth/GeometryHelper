using System;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Two arcs in one plane in space meet whichever is asked first, however flat one of them is.
    /// </summary>
    /// <remarks>
    /// Two coplanar arcs are crossed on their radical line, which was placed from the first arc's centre and crossed
    /// with its circle. Behind an edge 100 long of bulge 1E-7 lies a circle of radius 2.5E8, and the square of the half
    /// chord there is the difference of two squares near 6E16: asked from the edge, the edge lost its crossing with an
    /// arc of radius 40 and did not touch it. The line is now placed from, and crossed with, the smaller circle.
    /// </remarks>
    public class CollisionFaultB11NearFlatArc3Tests
    {
        private static readonly Tolerance TenThousandth = new Tolerance(1E-4, 1E-5);

        private static readonly GeoVector3 Up = new GeoVector3(0, 0, 1);

        /// <summary>
        /// The arc of radius 40 about (50, 30, 0) from 3.4 radians round to 5.0 in the plane z = 0, built from its two ends
        /// and its bulge, since angle nought in space sits wherever the plane puts it. It crosses y = 0 once, near x = 23.54.
        /// </summary>
        private static GeoArc3 Arc()
        {
            GeoPoint3 start = new GeoPoint3(50 + 40 * Math.Cos(3.4), 30 + 40 * Math.Sin(3.4), 0);
            GeoPoint3 end = new GeoPoint3(50 + 40 * Math.Cos(5.0), 30 + 40 * Math.Sin(5.0), 0);

            return new GeoEdge3(start, end, Math.Tan(1.6 / 4), Up).ToArc();
        }

        [Theory]
        [InlineData(1E-6)]
        [InlineData(1E-7)]
        public void ANearlyFlatArcInSpace_MeetsAnArcAtOnePointWhicheverIsAskedFirst(double bulge)
        {
            // The one crossing lies on the circle of radius 40 and within the edge's bow below y = 0, the same from both
            // sides, and the two collide either way round.
            GeoArc3 flat = new GeoEdge3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), bulge, Up).ToArc();
            GeoArc3 arc = Arc();

            GeoPoint3 fromFlat = Assert.Single(Arc3.GetIntersections(flat, arc, TenThousandth));
            GeoPoint3 fromArc = Assert.Single(Arc3.GetIntersections(arc, flat, TenThousandth));

            Assert.InRange(fromFlat.DistanceTo(fromArc), 0.0, 1E-12);
            double offTheCircle = Math.Abs(Math.Sqrt((fromFlat.X - 50) * (fromFlat.X - 50) + (fromFlat.Y - 30) * (fromFlat.Y - 30)) - 40);
            Assert.InRange(offTheCircle, 0.0, 1E-6);
            Assert.InRange(fromFlat.Y, -50 * bulge - 1E-6, 1E-6);
            Assert.Equal(23.5425, fromFlat.X, 4);

            Assert.True(Arc3.CollidesWith(flat, arc, TenThousandth));
            Assert.True(Arc3.CollidesWith(arc, flat, TenThousandth));
        }
    }
}
