using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Solid.Ellipse3Oracle;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Decisions 16, 18 and 23: every query of the ellipse in space is the same through the instance and through
    /// <see cref="Ellipse3"/>, the form without a tolerance is the one with <see cref="Tolerance.Global"/>, and the form
    /// with a tolerance reads the one it is given. The answers are compared bit for bit, the same call twice included.
    /// The tolerance passed, <see cref="Wide"/>, is a tenth: a shape 0.05 short of the rim touches it under that and
    /// misses it under the default, which the last test shows for one shape of each family. No test here sets
    /// <see cref="Tolerance.Global"/>; it is only read.
    /// </summary>
    public class Ellipse3QueryMirrorTests
    {
        private static readonly Tolerance Wide = new Tolerance(0.1, 1E-5, Tolerance.Default.EqualAngleRad, 0.1);

        private static GeoAabb3 Crate() => new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(100, 100, 100));

        /// <summary>80 by 30 level about the middle of the cube, out through two of its walls.</summary>
        private static Specimen Level() => new Specimen(new GeoPoint3(50, 50, 50), GeoVector3.XAxis, GeoVector3.YAxis, 80, 30);

        private static void Bits(GeoPoint3[] first, params GeoPoint3[][] others)
        {
            foreach (GeoPoint3[] other in others)
            {
                Assert.Equal(first.Select(p => (p.X, p.Y, p.Z)), other.Select(p => (p.X, p.Y, p.Z)));
            }
        }

        private static void Bools(bool first, params bool[] others)
        {
            foreach (bool other in others)
            {
                Assert.Equal(first, other);
            }
        }

        private static void Tries(bool first, GeoPoint3[] firstOut, bool second, GeoPoint3[] secondOut)
        {
            Assert.Equal(first, second);
            Bits(firstOut, secondOut);
            Assert.Equal(first, firstOut.Length > 0);
        }

        [Fact]
        public void APlaneIsAskedAlikeThroughEitherFormWithAndWithoutATolerance()
        {
            GeoEllipse3 e = Tilted().Build();
            GeoPlane3 x = new GeoPlane3(Tilted().Rim(0.7), LeanAbout(Tilted(), Tilted().Rim(0.7).GetVectorTo(Tilted().Rim(3.9)), 0.9));

            Bits(e.GetIntersections(x), Ellipse3.GetIntersections(e, x), e.GetIntersections(x, Tolerance.Global), Ellipse3.GetIntersections(e, x, Tolerance.Global));
            Bits(e.GetIntersections(x, Wide), Ellipse3.GetIntersections(e, x, Wide), e.GetIntersections(x, Wide));
            Bools(e.CollidesWith(x), Ellipse3.CollidesWith(e, x), e.CollidesWith(x, Tolerance.Global), Ellipse3.CollidesWith(e, x, Tolerance.Global));
            Bools(e.CollidesWith(x, Wide), Ellipse3.CollidesWith(e, x, Wide));
            Tries(e.TryIntersectWith(x, out GeoPoint3[] a1), a1, Ellipse3.TryIntersectWith(e, x, out GeoPoint3[] a2), a2);
            Tries(e.TryIntersectWith(x, out GeoPoint3[] a3, Tolerance.Global), a3, Ellipse3.TryIntersectWith(e, x, out GeoPoint3[] a4, Tolerance.Global), a4);
            Tries(e.TryIntersectWith(x, out GeoPoint3[] a5), a5, e.TryIntersectWith(x, out GeoPoint3[] a6, Tolerance.Global), a6);
            Tries(e.TryIntersectWith(x, out GeoPoint3[] b1, Wide), b1, Ellipse3.TryIntersectWith(e, x, out GeoPoint3[] b2, Wide), b2);
            Bits(b1, e.GetIntersections(x, Wide));
        }

        [Fact]
        public void ASegmentIsAskedAlikeThroughEitherFormWithAndWithoutATolerance()
        {
            GeoEllipse3 e = Tilted().Build();
            GeoLine3 x = new GeoLine3(Tilted().At(-400, 50, 0), Tilted().At(400, 50, 0));

            Bits(e.GetIntersections(x), Ellipse3.GetIntersections(e, x), e.GetIntersections(x, Tolerance.Global), Ellipse3.GetIntersections(e, x, Tolerance.Global));
            Bits(e.GetIntersections(x, Wide), Ellipse3.GetIntersections(e, x, Wide), e.GetIntersections(x, Wide));
            Bools(e.CollidesWith(x), Ellipse3.CollidesWith(e, x), e.CollidesWith(x, Tolerance.Global), Ellipse3.CollidesWith(e, x, Tolerance.Global));
            Bools(e.CollidesWith(x, Wide), Ellipse3.CollidesWith(e, x, Wide));
            Tries(e.TryIntersectWith(x, out GeoPoint3[] a1), a1, Ellipse3.TryIntersectWith(e, x, out GeoPoint3[] a2), a2);
            Tries(e.TryIntersectWith(x, out GeoPoint3[] a3, Tolerance.Global), a3, Ellipse3.TryIntersectWith(e, x, out GeoPoint3[] a4, Tolerance.Global), a4);
            Tries(e.TryIntersectWith(x, out GeoPoint3[] a5), a5, e.TryIntersectWith(x, out GeoPoint3[] a6, Tolerance.Global), a6);
            Tries(e.TryIntersectWith(x, out GeoPoint3[] b1, Wide), b1, Ellipse3.TryIntersectWith(e, x, out GeoPoint3[] b2, Wide), b2);
            Bits(b1, e.GetIntersections(x, Wide));
        }

        [Fact]
        public void ARayIsAskedAlikeThroughEitherFormWithAndWithoutATolerance()
        {
            GeoEllipse3 e = Tilted().Build();
            GeoRay3 x = new GeoRay3(Tilted().At(-400, 50, 0), Tilted().U);

            Bits(e.GetIntersections(x), Ellipse3.GetIntersections(e, x), e.GetIntersections(x, Tolerance.Global), Ellipse3.GetIntersections(e, x, Tolerance.Global));
            Bits(e.GetIntersections(x, Wide), Ellipse3.GetIntersections(e, x, Wide), e.GetIntersections(x, Wide));
            Bools(e.CollidesWith(x), Ellipse3.CollidesWith(e, x), e.CollidesWith(x, Tolerance.Global), Ellipse3.CollidesWith(e, x, Tolerance.Global));
            Bools(e.CollidesWith(x, Wide), Ellipse3.CollidesWith(e, x, Wide));
            Tries(e.TryIntersectWith(x, out GeoPoint3[] a1), a1, Ellipse3.TryIntersectWith(e, x, out GeoPoint3[] a2), a2);
            Tries(e.TryIntersectWith(x, out GeoPoint3[] a3, Tolerance.Global), a3, Ellipse3.TryIntersectWith(e, x, out GeoPoint3[] a4, Tolerance.Global), a4);
            Tries(e.TryIntersectWith(x, out GeoPoint3[] a5), a5, e.TryIntersectWith(x, out GeoPoint3[] a6, Tolerance.Global), a6);
            Tries(e.TryIntersectWith(x, out GeoPoint3[] b1, Wide), b1, Ellipse3.TryIntersectWith(e, x, out GeoPoint3[] b2, Wide), b2);
            Bits(b1, e.GetIntersections(x, Wide));
        }

        [Fact]
        public void AnArcIsAskedAlikeThroughEitherFormWithAndWithoutATolerance()
        {
            GeoEllipse3 e = Tilted().Build();
            GeoArc3 x = GeoArc3.FromThreePoints(Tilted().At(150, 0, 0), Tilted().At(106.066, 106.066, 0), Tilted().At(0, 150, 0));

            Bits(e.GetIntersections(x), Ellipse3.GetIntersections(e, x), e.GetIntersections(x, Tolerance.Global), Ellipse3.GetIntersections(e, x, Tolerance.Global));
            Bits(e.GetIntersections(x, Wide), Ellipse3.GetIntersections(e, x, Wide), e.GetIntersections(x, Wide));
            Bools(e.CollidesWith(x), Ellipse3.CollidesWith(e, x), e.CollidesWith(x, Tolerance.Global), Ellipse3.CollidesWith(e, x, Tolerance.Global));
            Bools(e.CollidesWith(x, Wide), Ellipse3.CollidesWith(e, x, Wide));
            Tries(e.TryIntersectWith(x, out GeoPoint3[] a1), a1, Ellipse3.TryIntersectWith(e, x, out GeoPoint3[] a2), a2);
            Tries(e.TryIntersectWith(x, out GeoPoint3[] a3, Tolerance.Global), a3, Ellipse3.TryIntersectWith(e, x, out GeoPoint3[] a4, Tolerance.Global), a4);
            Tries(e.TryIntersectWith(x, out GeoPoint3[] a5), a5, e.TryIntersectWith(x, out GeoPoint3[] a6, Tolerance.Global), a6);
            Tries(e.TryIntersectWith(x, out GeoPoint3[] b1, Wide), b1, Ellipse3.TryIntersectWith(e, x, out GeoPoint3[] b2, Wide), b2);
            Bits(b1, e.GetIntersections(x, Wide));
        }

        [Fact]
        public void ACircleIsAskedAlikeThroughEitherFormWithAndWithoutATolerance()
        {
            GeoEllipse3 e = Tilted().Build();
            GeoCircle3 x = new GeoCircle3(Tilted().Center, Tilted().N, 150);

            Bits(e.GetIntersections(x), Ellipse3.GetIntersections(e, x), e.GetIntersections(x, Tolerance.Global), Ellipse3.GetIntersections(e, x, Tolerance.Global));
            Bits(e.GetIntersections(x, Wide), Ellipse3.GetIntersections(e, x, Wide), e.GetIntersections(x, Wide));
            Bools(e.CollidesWith(x), Ellipse3.CollidesWith(e, x), e.CollidesWith(x, Tolerance.Global), Ellipse3.CollidesWith(e, x, Tolerance.Global));
            Bools(e.CollidesWith(x, Wide), Ellipse3.CollidesWith(e, x, Wide));
            Tries(e.TryIntersectWith(x, out GeoPoint3[] a1), a1, Ellipse3.TryIntersectWith(e, x, out GeoPoint3[] a2), a2);
            Tries(e.TryIntersectWith(x, out GeoPoint3[] a3, Tolerance.Global), a3, Ellipse3.TryIntersectWith(e, x, out GeoPoint3[] a4, Tolerance.Global), a4);
            Tries(e.TryIntersectWith(x, out GeoPoint3[] a5), a5, e.TryIntersectWith(x, out GeoPoint3[] a6, Tolerance.Global), a6);
            Tries(e.TryIntersectWith(x, out GeoPoint3[] b1, Wide), b1, Ellipse3.TryIntersectWith(e, x, out GeoPoint3[] b2, Wide), b2);
            Bits(b1, e.GetIntersections(x, Wide));
        }

        [Fact]
        public void AnotherEllipseIsAskedAlikeThroughEitherFormWithAndWithoutATolerance()
        {
            GeoEllipse3 e = Tilted().Build();
            GeoEllipse3 x = new Specimen(Tilted().Center, Tilted().V, Tilted().U.Negate(), 300, 100).Build();

            Bits(e.GetIntersections(x), Ellipse3.GetIntersections(e, x), e.GetIntersections(x, Tolerance.Global), Ellipse3.GetIntersections(e, x, Tolerance.Global));
            Bits(e.GetIntersections(x, Wide), Ellipse3.GetIntersections(e, x, Wide), e.GetIntersections(x, Wide));
            Bools(e.CollidesWith(x), Ellipse3.CollidesWith(e, x), e.CollidesWith(x, Tolerance.Global), Ellipse3.CollidesWith(e, x, Tolerance.Global));
            Bools(e.CollidesWith(x, Wide), Ellipse3.CollidesWith(e, x, Wide));
            Tries(e.TryIntersectWith(x, out GeoPoint3[] a1), a1, Ellipse3.TryIntersectWith(e, x, out GeoPoint3[] a2), a2);
            Tries(e.TryIntersectWith(x, out GeoPoint3[] a3, Tolerance.Global), a3, Ellipse3.TryIntersectWith(e, x, out GeoPoint3[] a4, Tolerance.Global), a4);
            Tries(e.TryIntersectWith(x, out GeoPoint3[] a5), a5, e.TryIntersectWith(x, out GeoPoint3[] a6, Tolerance.Global), a6);
            Tries(e.TryIntersectWith(x, out GeoPoint3[] b1, Wide), b1, Ellipse3.TryIntersectWith(e, x, out GeoPoint3[] b2, Wide), b2);
            Bits(b1, e.GetIntersections(x, Wide));
        }

        [Fact]
        public void ATriangleIsAskedAlikeThroughEitherFormWithAndWithoutATolerance()
        {
            GeoEllipse3 e = Tilted().Build();
            GeoTriangle3 x = new GeoTriangle3(Tilted().At(-50, -50, 0), Tilted().At(400, -50, 0), Tilted().At(-50, 150, 0));

            Bits(e.GetIntersections(x), Ellipse3.GetIntersections(e, x), e.GetIntersections(x, Tolerance.Global), Ellipse3.GetIntersections(e, x, Tolerance.Global));
            Bits(e.GetIntersections(x, Wide), Ellipse3.GetIntersections(e, x, Wide), e.GetIntersections(x, Wide));
            Bools(e.CollidesWith(x), Ellipse3.CollidesWith(e, x), e.CollidesWith(x, Tolerance.Global), Ellipse3.CollidesWith(e, x, Tolerance.Global));
            Bools(e.CollidesWith(x, Wide), Ellipse3.CollidesWith(e, x, Wide));
            Tries(e.TryIntersectWith(x, out GeoPoint3[] a1), a1, Ellipse3.TryIntersectWith(e, x, out GeoPoint3[] a2), a2);
            Tries(e.TryIntersectWith(x, out GeoPoint3[] a3, Tolerance.Global), a3, Ellipse3.TryIntersectWith(e, x, out GeoPoint3[] a4, Tolerance.Global), a4);
            Tries(e.TryIntersectWith(x, out GeoPoint3[] a5), a5, e.TryIntersectWith(x, out GeoPoint3[] a6, Tolerance.Global), a6);
            Tries(e.TryIntersectWith(x, out GeoPoint3[] b1, Wide), b1, Ellipse3.TryIntersectWith(e, x, out GeoPoint3[] b2, Wide), b2);
            Bits(b1, e.GetIntersections(x, Wide));
        }

        [Fact]
        public void APolygonIsAskedAlikeThroughEitherFormWithAndWithoutATolerance()
        {
            GeoEllipse3 e = Tilted().Build();
            GeoPolygon3 x = new GeoPolygon3(Tilted().At(-100, -200, 0), Tilted().At(100, -200, 0), Tilted().At(100, 200, 0), Tilted().At(-100, 200, 0));

            Bits(e.GetIntersections(x), Ellipse3.GetIntersections(e, x), e.GetIntersections(x, Tolerance.Global), Ellipse3.GetIntersections(e, x, Tolerance.Global));
            Bits(e.GetIntersections(x, Wide), Ellipse3.GetIntersections(e, x, Wide), e.GetIntersections(x, Wide));
            Bools(e.CollidesWith(x), Ellipse3.CollidesWith(e, x), e.CollidesWith(x, Tolerance.Global), Ellipse3.CollidesWith(e, x, Tolerance.Global));
            Bools(e.CollidesWith(x, Wide), Ellipse3.CollidesWith(e, x, Wide));
            Tries(e.TryIntersectWith(x, out GeoPoint3[] a1), a1, Ellipse3.TryIntersectWith(e, x, out GeoPoint3[] a2), a2);
            Tries(e.TryIntersectWith(x, out GeoPoint3[] a3, Tolerance.Global), a3, Ellipse3.TryIntersectWith(e, x, out GeoPoint3[] a4, Tolerance.Global), a4);
            Tries(e.TryIntersectWith(x, out GeoPoint3[] a5), a5, e.TryIntersectWith(x, out GeoPoint3[] a6, Tolerance.Global), a6);
            Tries(e.TryIntersectWith(x, out GeoPoint3[] b1, Wide), b1, Ellipse3.TryIntersectWith(e, x, out GeoPoint3[] b2, Wide), b2);
            Bits(b1, e.GetIntersections(x, Wide));
        }

        [Fact]
        public void AFaceIsAskedAlikeThroughEitherFormWithAndWithoutATolerance()
        {
            GeoEllipse3 e = Tilted().Build();
            GeoFace3 x = new GeoFace3(new GeoPolygon3(Tilted().At(-400, -200, 0), Tilted().At(400, -200, 0), Tilted().At(400, 200, 0), Tilted().At(-400, 200, 0)), new[] { new GeoPolygon3(Tilted().At(200, -50, 0), Tilted().At(350, -50, 0), Tilted().At(350, 50, 0), Tilted().At(200, 50, 0)) });

            Bits(e.GetIntersections(x), Ellipse3.GetIntersections(e, x), e.GetIntersections(x, Tolerance.Global), Ellipse3.GetIntersections(e, x, Tolerance.Global));
            Bits(e.GetIntersections(x, Wide), Ellipse3.GetIntersections(e, x, Wide), e.GetIntersections(x, Wide));
            Bools(e.CollidesWith(x), Ellipse3.CollidesWith(e, x), e.CollidesWith(x, Tolerance.Global), Ellipse3.CollidesWith(e, x, Tolerance.Global));
            Bools(e.CollidesWith(x, Wide), Ellipse3.CollidesWith(e, x, Wide));
            Tries(e.TryIntersectWith(x, out GeoPoint3[] a1), a1, Ellipse3.TryIntersectWith(e, x, out GeoPoint3[] a2), a2);
            Tries(e.TryIntersectWith(x, out GeoPoint3[] a3, Tolerance.Global), a3, Ellipse3.TryIntersectWith(e, x, out GeoPoint3[] a4, Tolerance.Global), a4);
            Tries(e.TryIntersectWith(x, out GeoPoint3[] a5), a5, e.TryIntersectWith(x, out GeoPoint3[] a6, Tolerance.Global), a6);
            Tries(e.TryIntersectWith(x, out GeoPoint3[] b1, Wide), b1, Ellipse3.TryIntersectWith(e, x, out GeoPoint3[] b2, Wide), b2);
            Bits(b1, e.GetIntersections(x, Wide));
        }

        [Fact]
        public void ASolidIsAskedAlikeThroughEitherFormWithAndWithoutATolerance()
        {
            GeoEllipse3 e = Level().Build();
            GeoSolid3 x = Crate().ToObb().ToSolid();

            Bits(e.GetIntersections(x), Ellipse3.GetIntersections(e, x), e.GetIntersections(x, Tolerance.Global), Ellipse3.GetIntersections(e, x, Tolerance.Global));
            Bits(e.GetIntersections(x, Wide), Ellipse3.GetIntersections(e, x, Wide), e.GetIntersections(x, Wide));
            Bools(e.CollidesWith(x), Ellipse3.CollidesWith(e, x), e.CollidesWith(x, Tolerance.Global), Ellipse3.CollidesWith(e, x, Tolerance.Global));
            Bools(e.CollidesWith(x, Wide), Ellipse3.CollidesWith(e, x, Wide));
            Tries(e.TryIntersectWith(x, out GeoPoint3[] a1), a1, Ellipse3.TryIntersectWith(e, x, out GeoPoint3[] a2), a2);
            Tries(e.TryIntersectWith(x, out GeoPoint3[] a3, Tolerance.Global), a3, Ellipse3.TryIntersectWith(e, x, out GeoPoint3[] a4, Tolerance.Global), a4);
            Tries(e.TryIntersectWith(x, out GeoPoint3[] a5), a5, e.TryIntersectWith(x, out GeoPoint3[] a6, Tolerance.Global), a6);
            Tries(e.TryIntersectWith(x, out GeoPoint3[] b1, Wide), b1, Ellipse3.TryIntersectWith(e, x, out GeoPoint3[] b2, Wide), b2);
            Bits(b1, e.GetIntersections(x, Wide));
        }

        [Fact]
        public void AnOrientedBoxIsAskedAlikeThroughEitherFormWithAndWithoutATolerance()
        {
            GeoEllipse3 e = Level().Build();
            GeoObb3 x = Crate().ToObb();

            Bits(e.GetIntersections(x), Ellipse3.GetIntersections(e, x), e.GetIntersections(x, Tolerance.Global), Ellipse3.GetIntersections(e, x, Tolerance.Global));
            Bits(e.GetIntersections(x, Wide), Ellipse3.GetIntersections(e, x, Wide), e.GetIntersections(x, Wide));
            Bools(e.CollidesWith(x), Ellipse3.CollidesWith(e, x), e.CollidesWith(x, Tolerance.Global), Ellipse3.CollidesWith(e, x, Tolerance.Global));
            Bools(e.CollidesWith(x, Wide), Ellipse3.CollidesWith(e, x, Wide));
            Tries(e.TryIntersectWith(x, out GeoPoint3[] a1), a1, Ellipse3.TryIntersectWith(e, x, out GeoPoint3[] a2), a2);
            Tries(e.TryIntersectWith(x, out GeoPoint3[] a3, Tolerance.Global), a3, Ellipse3.TryIntersectWith(e, x, out GeoPoint3[] a4, Tolerance.Global), a4);
            Tries(e.TryIntersectWith(x, out GeoPoint3[] a5), a5, e.TryIntersectWith(x, out GeoPoint3[] a6, Tolerance.Global), a6);
            Tries(e.TryIntersectWith(x, out GeoPoint3[] b1, Wide), b1, Ellipse3.TryIntersectWith(e, x, out GeoPoint3[] b2, Wide), b2);
            Bits(b1, e.GetIntersections(x, Wide));
        }

        [Fact]
        public void AnAxisAlignedBoxIsAskedAlikeThroughEitherFormWithAndWithoutATolerance()
        {
            GeoEllipse3 e = Level().Build();
            GeoAabb3 x = Crate();

            Bits(e.GetIntersections(x), Ellipse3.GetIntersections(e, x), e.GetIntersections(x, Tolerance.Global), Ellipse3.GetIntersections(e, x, Tolerance.Global));
            Bits(e.GetIntersections(x, Wide), Ellipse3.GetIntersections(e, x, Wide), e.GetIntersections(x, Wide));
            Bools(e.CollidesWith(x), Ellipse3.CollidesWith(e, x), e.CollidesWith(x, Tolerance.Global), Ellipse3.CollidesWith(e, x, Tolerance.Global));
            Bools(e.CollidesWith(x, Wide), Ellipse3.CollidesWith(e, x, Wide));
            Tries(e.TryIntersectWith(x, out GeoPoint3[] a1), a1, Ellipse3.TryIntersectWith(e, x, out GeoPoint3[] a2), a2);
            Tries(e.TryIntersectWith(x, out GeoPoint3[] a3, Tolerance.Global), a3, Ellipse3.TryIntersectWith(e, x, out GeoPoint3[] a4, Tolerance.Global), a4);
            Tries(e.TryIntersectWith(x, out GeoPoint3[] a5), a5, e.TryIntersectWith(x, out GeoPoint3[] a6, Tolerance.Global), a6);
            Tries(e.TryIntersectWith(x, out GeoPoint3[] b1, Wide), b1, Ellipse3.TryIntersectWith(e, x, out GeoPoint3[] b2, Wide), b2);
            Bits(b1, e.GetIntersections(x, Wide));
        }

        [Fact]
        public void AnEdgeIsAskedAlikeThroughEitherFormWithAndWithoutATolerance()
        {
            GeoEllipse3 e = Tilted().Build();
            GeoEdge3 x = new GeoEdge3(Tilted().At(-400, 50, 0), Tilted().At(400, 50, 0));

            Bits(e.GetIntersections(x), Ellipse3.GetIntersections(e, x), e.GetIntersections(x, Tolerance.Global), Ellipse3.GetIntersections(e, x, Tolerance.Global));
            Bits(e.GetIntersections(x, Wide), Ellipse3.GetIntersections(e, x, Wide), e.GetIntersections(x, Wide));
            Bools(e.CollidesWith(x), Ellipse3.CollidesWith(e, x), e.CollidesWith(x, Tolerance.Global), Ellipse3.CollidesWith(e, x, Tolerance.Global));
            Bools(e.CollidesWith(x, Wide), Ellipse3.CollidesWith(e, x, Wide));
            Tries(e.TryIntersectWith(x, out GeoPoint3[] a1), a1, Ellipse3.TryIntersectWith(e, x, out GeoPoint3[] a2), a2);
            Tries(e.TryIntersectWith(x, out GeoPoint3[] a3, Tolerance.Global), a3, Ellipse3.TryIntersectWith(e, x, out GeoPoint3[] a4, Tolerance.Global), a4);
            Tries(e.TryIntersectWith(x, out GeoPoint3[] a5), a5, e.TryIntersectWith(x, out GeoPoint3[] a6, Tolerance.Global), a6);
            Tries(e.TryIntersectWith(x, out GeoPoint3[] b1, Wide), b1, Ellipse3.TryIntersectWith(e, x, out GeoPoint3[] b2, Wide), b2);
            Bits(b1, e.GetIntersections(x, Wide));
        }

        [Fact]
        public void ACurvedChainIsAskedAlikeThroughEitherFormWithAndWithoutATolerance()
        {
            GeoEllipse3 e = Tilted().Build();
            GeoPolylineArc3 x = new GeoPolylineArc3(new[] { Tilted().At(-400, 0, 0), Tilted().At(0, 150, 0), Tilted().At(400, 0, 0) });

            Bits(e.GetIntersections(x), Ellipse3.GetIntersections(e, x), e.GetIntersections(x, Tolerance.Global), Ellipse3.GetIntersections(e, x, Tolerance.Global));
            Bits(e.GetIntersections(x, Wide), Ellipse3.GetIntersections(e, x, Wide), e.GetIntersections(x, Wide));
            Bools(e.CollidesWith(x), Ellipse3.CollidesWith(e, x), e.CollidesWith(x, Tolerance.Global), Ellipse3.CollidesWith(e, x, Tolerance.Global));
            Bools(e.CollidesWith(x, Wide), Ellipse3.CollidesWith(e, x, Wide));
        }

        [Fact]
        public void ACurvedLoopIsAskedAlikeThroughEitherFormWithAndWithoutATolerance()
        {
            GeoEllipse3 e = Tilted().Build();
            GeoPolygonArc3 x = new GeoPolygonArc3(new[] { Tilted().At(-100, -200, 0), Tilted().At(100, -200, 0), Tilted().At(100, 200, 0), Tilted().At(-100, 200, 0) });

            Bits(e.GetIntersections(x), Ellipse3.GetIntersections(e, x), e.GetIntersections(x, Tolerance.Global), Ellipse3.GetIntersections(e, x, Tolerance.Global));
            Bits(e.GetIntersections(x, Wide), Ellipse3.GetIntersections(e, x, Wide), e.GetIntersections(x, Wide));
            Bools(e.CollidesWith(x), Ellipse3.CollidesWith(e, x), e.CollidesWith(x, Tolerance.Global), Ellipse3.CollidesWith(e, x, Tolerance.Global));
            Bools(e.CollidesWith(x, Wide), Ellipse3.CollidesWith(e, x, Wide));
        }

        [Fact]
        public void TheToleranceGivenIsTheOneRead()
        {
            // Each shape comes 0.05 short of the rim of 300 by 100, or of its plane, crossing it square rather than grazing
            // it: within the tenth, beyond the default thousandth, clear of both.
            Specimen e = Tilted();
            GeoEllipse3 ellipse = e.Build();
            GeoPoint3 first = e.Rim(0.6), second = e.Rim(2.8);
            GeoVector3 chord = first.GetVectorTo(second);
            GeoVector3 along = chord.Normalize();
            GeoVector3 lean = LeanAbout(e, chord, 70 * Math.PI / 180);
            GeoVector3 up = lean.CrossProduct(along);

            // A plane parallel to the ellipse's, 0.05 off: one with it under the tenth.
            var plane = new GeoPlane3(e.At(0, 0, 0.05), e.N);

            // A segment piercing the plane 0.05 beyond the end of the major axis.
            var line = new GeoLine3(e.At(300.05, 0, -50), e.At(300.05, 0, 50));

            // An ellipse leaning 70 degrees through the two rim points lifted 0.05 off the plane: the planes meet 0.018
            // from the chord, where the first rim crosses it, 0.05 from the second rim.
            GeoEllipse3 lifted = ThroughTwo(first.Add(e.N.Multiply(0.05)), second.Add(e.N.Multiply(0.05)), lean, 0.3, 0.4, 0.25).Build();

            // A triangle standing on the chord's line whose base stops 0.05 short of either rim point.
            var triangle = new GeoTriangle3(first.Add(along.Multiply(0.05)), second.Add(along.Multiply(-0.05)), first.Add(chord.Multiply(0.5)).Add(up.Multiply(200)));

            // The cube, and an ellipse upright in the plane x = 100.05, 0.05 outside its wall, whose rim crosses the plane
            // of the top at (100.05, 42, 100) and (100.05, 58, 100).
            var box = new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(100, 100, 100));
            GeoEllipse3 outside = new Specimen(new GeoPoint3(100.05, 50, 100), GeoVector3.ZAxis, GeoVector3.YAxis.Negate(), 30, 8).Build();

            Assert.False(ellipse.CollidesWith(plane, Tolerance.Default));
            Assert.True(ellipse.CollidesWith(plane, Wide));
            Assert.Empty(ellipse.GetIntersections(line, Tolerance.Default));
            Assert.Single(ellipse.GetIntersections(line, Wide));
            Assert.Empty(ellipse.GetIntersections(lifted, Tolerance.Default));
            Assert.Equal(2, ellipse.GetIntersections(lifted, Wide).Length);
            Assert.Empty(ellipse.GetIntersections(triangle, Tolerance.Default));
            Assert.Equal(2, ellipse.GetIntersections(triangle, Wide).Length);
            Assert.False(outside.CollidesWith(box, Tolerance.Default));
            Assert.True(outside.CollidesWith(box, Wide));
        }
    }
}
