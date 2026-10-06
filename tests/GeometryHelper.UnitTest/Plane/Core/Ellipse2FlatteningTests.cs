using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using GeometryHelper.Meshing;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// Cutting an ellipse into straight pieces, the ways GeoCircle2 offers: by chord tolerance, by spacing and by count,
    /// as a polygon and as a closed chain, and its surface into triangles and meshes. By chord tolerance the vertices are
    /// placed adaptively in t, a quadrant built and mirrored into the other three, so the polygon is symmetric about
    /// both axes with a vertex at each end of them. The sag of every chord is measured on the true rim by golden
    /// section (<see cref="Ellipse2Oracle.LargestSag"/>), not by the estimate the cutting steps by.
    /// </summary>
    public class Ellipse2FlatteningTests
    {
        private static readonly Tolerance Tol = new Tolerance(1E-3, 1E-5);

        private static readonly Tolerance Tight = new Tolerance(1E-9, 1E-9);

        private static int CircleCount(double radius, double chordTolerance) =>
            new GeoCircle2(new GeoPoint2(0, 0), radius).ToPolygonByChordTolerance(chordTolerance).VertexCount;

        [Theory]
        [InlineData(0.0)]
        [InlineData(0.05)]
        [InlineData(0.01)]
        public void ThePolygonIsSymmetricAboutBothAxesWithAVertexAtEachEndOfThem(double chordTolerance)
        {
            // 300 by 100, at the automatic 0.2, at 0.05 and at 0.01. The count is a multiple of four; vertex 0 is the end of the major
            // axis and every quarter of the way round is the next end; vertex i and vertex n/2 - i are mirror images in
            // the minor axis, and i and n - i in the major. Every vertex is on the rim, they run counter-clockwise with
            // t rising, and the first is not repeated at the end.
            GeoEllipse2 ellipse = Ellipse2Oracle.Tilted();
            GeoPolygon2 polygon = ellipse.ToPolygonByChordTolerance(chordTolerance);
            int n = polygon.VertexCount;

            Assert.Equal(0, n % 4);
            Assert.True(polygon[0].IsEqualTo(Ellipse2Oracle.FromFrame(ellipse, 300, 0), Tight));
            Assert.True(polygon[n / 4].IsEqualTo(Ellipse2Oracle.FromFrame(ellipse, 0, 100), Tight));
            Assert.True(polygon[n / 2].IsEqualTo(Ellipse2Oracle.FromFrame(ellipse, -300, 0), Tight));
            Assert.True(polygon[3 * n / 4].IsEqualTo(Ellipse2Oracle.FromFrame(ellipse, 0, -100), Tight));

            for (int i = 1; i < n / 4; i++)
            {
                Ellipse2Oracle.ToFrame(ellipse, polygon[i], out double x, out double y);

                Assert.True(polygon[n / 2 - i].IsEqualTo(Ellipse2Oracle.FromFrame(ellipse, -x, y), Tight));
                Assert.True(polygon[n - i].IsEqualTo(Ellipse2Oracle.FromFrame(ellipse, x, -y), Tight));
            }

            for (int i = 0; i < n; i++)
            {
                Assert.InRange(Ellipse2Oracle.GapToRim(ellipse, polygon[i]), 0.0, 1E-9);
            }

            // From vertex 1 on: vertex 0 lies on the major axis, where rounding may read its angle as a hair under 2 pi.
            for (int i = 2; i < n; i++)
            {
                Assert.True(Ellipse2Oracle.AngleOf(ellipse, polygon[i]) > Ellipse2Oracle.AngleOf(ellipse, polygon[i - 1]));
            }

            Assert.True(polygon.SignedArea > 0.0);
            Assert.False(polygon[n - 1].IsEqualTo(polygon[0], Tight));
        }

        [Theory]
        [InlineData(300.0, 100.0, 0.2)]
        [InlineData(300.0, 100.0, 0.01)]
        [InlineData(1000.0, 1.0, 0.0)]
        [InlineData(1000.0, 1.0, 0.0005)]
        [InlineData(1000.0, 10.0, 1.0)]
        [InlineData(250.0, 250.0, 0.5)]
        public void NoChordStraysFurtherFromTheRimThanTheTolerance(double majorRadius, double minorRadius, double chordTolerance)
        {
            // Nought is the automatic tolerance, 0.002 of the minor radius: 0.002 on the 1 000 by 1, whose ends curve
            // with a radius of a thousandth, half the tolerance.
            var ellipse = new GeoEllipse2(new GeoPoint2(-35, 60), new GeoVector2(-3, 1), majorRadius, minorRadius);
            double allowed = chordTolerance > 0.0 ? chordTolerance : 0.002 * minorRadius;

            GeoPolygon2 polygon = ellipse.ToPolygonByChordTolerance(chordTolerance);

            Assert.InRange(Ellipse2Oracle.LargestSag(ellipse, polygon), 0.0, allowed * (1 + 1E-9));
        }

        [Fact]
        public void AThinEllipseNeedsFarFewerEdgesThanAnEvenStepWould()
        {
            // 1 000 by 1 at the automatic 0.002: the lead measured about 83 edges placed adaptively, against 1 571 at an
            // even step in t, which the ends of the major axis would force everywhere. Under 200 leaves room for any
            // reasonable stepping and none for an even one.
            var ellipse = new GeoEllipse2(new GeoPoint2(0, 0), GeoVector2.XAxis, 1000, 1);
            GeoPolygon2 polygon = ellipse.ToPolygon();

            Assert.InRange(polygon.VertexCount, 8, 200);
            Assert.InRange(Ellipse2Oracle.LargestSag(ellipse, polygon), 0.0, 0.002 * (1 + 1E-9));
        }

        [Theory]
        [InlineData(250.0, 0.0)]
        [InlineData(250.0, 1.0)]
        [InlineData(250.0, 0.1)]
        [InlineData(250.0, 5.0)]
        [InlineData(1000.0, 0.0)]
        public void ACircleIsCutIntoAsManyEdgesAsGeoCircle2OrTheNextMultipleOfFour(double radius, double chordTolerance)
        {
            // GeoCircle2 cuts 250 into 50 edges at the automatic 0.5, 36 at 1.0, 112 at 0.1 and 16 at 5.0. The ellipse
            // builds a quadrant and mirrors it, so it may round 50 up to 52 and no further.
            var ellipse = new GeoEllipse2(new GeoPoint2(12, -7), new GeoVector2(1, 1), radius, radius);
            int circle = CircleCount(radius, chordTolerance);
            int roundedUp = (circle + 3) / 4 * 4;

            Assert.Contains(ellipse.ToPolygonByChordTolerance(chordTolerance).VertexCount, new[] { circle, roundedUp });
        }

        [Fact]
        public void ATinyToleranceIsCappedAtFourThousandAndNinetySixVertices()
        {
            // 1E-12 asks for millions of edges. A circle of 1 000 is capped at 4 096, as GeoCircle2 is; the 1 000 by 1
            // at no more, a multiple of four, still with its vertex at the end of the minor axis.
            var circle = new GeoEllipse2(new GeoPoint2(0, 0), GeoVector2.XAxis, 1000, 1000);
            var thin = new GeoEllipse2(new GeoPoint2(0, 0), GeoVector2.XAxis, 1000, 1);

            Assert.Equal(4096, CircleCount(1000, 1E-12));
            Assert.Equal(4096, circle.ToPolygonByChordTolerance(1E-12).VertexCount);

            GeoPolygon2 polygon = thin.ToPolygonByChordTolerance(1E-12);
            Assert.InRange(polygon.VertexCount, 4, 4096);
            Assert.Equal(0, polygon.VertexCount % 4);
            Assert.True(polygon[polygon.VertexCount / 4].IsEqualTo(new GeoPoint2(0, 1), Tight));

            // On 1 000 000 by 100 000 the coordinates round at about 1E-10, so no sag ever measures under 1E-12: the
            // cutting has to stop at the cap rather than shrink its step for ever.
            var large = new GeoEllipse2(new GeoPoint2(0, 0), GeoVector2.XAxis, 1E6, 1E5);
            GeoPolygon2 capped = large.ToPolygonByChordTolerance(1E-12);
            Assert.InRange(capped.VertexCount, 4, 4096);
            Assert.Equal(0, capped.VertexCount % 4);
        }

        [Fact]
        public void TheArgumentsAreCheckedAsGeoCircle2ChecksThem()
        {
            GeoEllipse2 ellipse = Ellipse2Oracle.Tilted();

            Assert.Throws<ArgumentOutOfRangeException>(() => ellipse.ToPolygonByChordTolerance(-1.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ellipse.ToPolygonByChordTolerance(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => ellipse.ToPolygonBySpacing(0.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ellipse.ToPolygonBySpacing(-5.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ellipse.ToPolygonBySpacing(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => ellipse.ToPolygon(2));
            Assert.Throws<ArgumentOutOfRangeException>(() => ellipse.ToPolylineByChordTolerance(-1.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ellipse.ToPolylineBySpacing(0.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ellipse.ToPolyline(2));
        }

        [Fact]
        public void ACutByCountIsEvenInTheAngle()
        {
            // Twelve vertices, a twelfth of a turn of t apart from the end of the major axis; three is the fewest.
            GeoEllipse2 ellipse = Ellipse2Oracle.Tilted();
            GeoPolygon2 polygon = ellipse.ToPolygon(12);

            Assert.Equal(12, polygon.VertexCount);
            for (int i = 0; i < 12; i++)
            {
                Assert.True(polygon[i].IsEqualTo(Ellipse2Oracle.Rim(ellipse, 2 * Math.PI * i / 12), Tight));
            }

            Assert.Equal(3, ellipse.ToPolygon(3).VertexCount);
        }

        [Theory]
        [InlineData(100.0, 14)]
        [InlineData(31.0, 44)]
        public void ACutBySpacingSpreadsTheVerticesEvenlyAlongTheRim(double spacing, int count)
        {
            // The rim of 300 by 100 is 1 336.489 long: 13.4 spacings of 100 and 43.1 of 31, so 14 and 44 vertices, as
            // GeoCircle2 rounds up. From the end of the major axis, each is the same length along the rim from the last,
            // by quadrature, and none further than the spacing.
            GeoEllipse2 ellipse = Ellipse2Oracle.Tilted();
            double length = Ellipse2Oracle.Perimeter(300, 100);
            GeoPolygon2 polygon = ellipse.ToPolygonBySpacing(spacing);

            Assert.Equal(count, polygon.VertexCount);
            Assert.True(polygon[0].IsEqualTo(Ellipse2Oracle.FromFrame(ellipse, 300, 0), Tight));
            Assert.True(length / count <= spacing);

            for (int i = 0; i < count; i++)
            {
                // Read round the turn: the angle of vertex 0 may come out as nought or a hair under 2 pi.
                double from = Ellipse2Oracle.AngleOf(ellipse, polygon[i]);
                double to = Ellipse2Oracle.AngleOf(ellipse, polygon[(i + 1) % count]);
                if (to <= from)
                {
                    to += 2 * Math.PI;
                }

                Assert.Equal(length / count, Ellipse2Oracle.ArcLength(300, 100, from, to), 1E-9 * length);
            }
        }

        [Fact]
        public void ACircleIsCutBySpacingIntoAsManyEdgesAsGeoCircle2()
        {
            var circle = new GeoCircle2(new GeoPoint2(12, -7), 250);
            var ellipse = new GeoEllipse2(circle.Center, new GeoVector2(1, 1), 250, 250);

            Assert.Equal(circle.ToPolygonBySpacing(40.0).VertexCount, ellipse.ToPolygonBySpacing(40.0).VertexCount);
            Assert.Equal(circle.ToPolygonBySpacing(100.0).VertexCount, ellipse.ToPolygonBySpacing(100.0).VertexCount);
        }

        [Fact]
        public void EachChainIsItsPolygonWithTheFirstPointRepeatedAtTheEnd()
        {
            GeoEllipse2 ellipse = Ellipse2Oracle.Tilted();

            var pairs = new[]
            {
                (ellipse.ToPolygon(), ellipse.ToPolyline()),
                (ellipse.ToPolygonByChordTolerance(0.05), ellipse.ToPolylineByChordTolerance(0.05)),
                (ellipse.ToPolygonBySpacing(31.0), ellipse.ToPolylineBySpacing(31.0)),
                (ellipse.ToPolygon(12), ellipse.ToPolyline(12)),
            };

            foreach ((GeoPolygon2 polygon, GeoPolyline2 chain) in pairs)
            {
                Assert.Equal(polygon.VertexCount + 1, chain.VertexCount);
                for (int i = 0; i < polygon.VertexCount; i++)
                {
                    Assert.True(chain[i].IsEqualTo(polygon[i], Tight));
                }

                Assert.True(chain[polygon.VertexCount].IsEqualTo(polygon[0], Tight));
            }
        }

        [Fact]
        public void TheSurfaceIsFannedFromTheCentreOverThePolygon()
        {
            // As many triangles as the polygon cut at the same chord tolerance has edges, every one counter-clockwise
            // with the centre as a corner, covering the polygon's area. That is a little under the ellipse's 94 248: each
            // chord cuts off no more than two thirds of its sag times its length, so 0.05 takes off at most 45 all round.
            GeoEllipse2 ellipse = Ellipse2Oracle.Tilted();
            GeoPolygon2 polygon = ellipse.ToPolygonByChordTolerance(0.05);
            GeoTriangle2[] fan = ellipse.TriangulateSurface(0.05, Tol);

            Assert.Equal(polygon.VertexCount, fan.Length);
            Assert.All(fan, triangle => Assert.False(triangle.IsClockwise));
            Assert.All(fan, triangle => Assert.Contains(triangle.GetVertices(), corner => corner.IsEqualTo(ellipse.Center, Tight)));
            Assert.Equal(polygon.Area, fan.Sum(triangle => triangle.Area), 1E-9 * polygon.Area);
            Assert.InRange(polygon.Area, ellipse.Area - 45, ellipse.Area);

            // Without a chord tolerance, the automatic one.
            Assert.Equal(ellipse.ToPolygon().VertexCount, ellipse.TriangulateSurface(Tol).Length);
        }

        [Fact]
        public void TheTriangleMeshIsTheFanAndTheConvexMeshIsOneFace()
        {
            GeoEllipse2 ellipse = Ellipse2Oracle.Tilted();
            GeoTriangle2[] fan = ellipse.TriangulateSurface(0.05, Tol);
            GeoMesh2 triangles = ellipse.ToMesh(new MeshOptions(MeshKind.Triangles, chordTolerance: 0.05), Tol);

            Assert.Equal(fan.Length, triangles.FaceCount);
            Assert.Equal(fan.Length + 1, triangles.VertexCount);
            Assert.Contains(triangles.Vertices, vertex => vertex.IsEqualTo(ellipse.Center, Tight));
            Assert.Equal(fan.Sum(triangle => triangle.Area), triangles.Area, 1E-9 * ellipse.Area);

            // An ellipse is convex, and so is the polygon inscribed in it.
            GeoMesh2 convex = ellipse.ToMesh(new MeshOptions(MeshKind.Convex, chordTolerance: 0.05), Tol);
            Assert.Equal(1, convex.FaceCount);
            Assert.Equal(ellipse.ToPolygonByChordTolerance(0.05).Area, convex.Area, 1E-9 * ellipse.Area);
        }

        [Fact]
        public void AnEllipseNoThickerThanThePointToleranceMeshesToNothing()
        {
            // The circle meshes to nothing when its radius is within EqualPoint; the ellipse when its minor radius is.
            // 50 by 0.0005 is half the thousandth thick; 50 by 0.003 three times it.
            var thin = new GeoEllipse2(new GeoPoint2(0, 0), GeoVector2.XAxis, 50, 0.0005);
            var thicker = new GeoEllipse2(new GeoPoint2(0, 0), GeoVector2.XAxis, 50, 0.003);

            foreach (MeshKind kind in new[] { MeshKind.Triangles, MeshKind.Convex, MeshKind.Strips })
            {
                Assert.Equal(0, thin.ToMesh(new MeshOptions(kind), Tol).FaceCount);
            }

            Assert.True(thicker.ToMesh(MeshOptions.Triangles, Tol).FaceCount > 0);
        }
    }
}
