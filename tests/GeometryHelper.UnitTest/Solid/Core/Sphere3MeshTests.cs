using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// The ball meshed: an icosahedron cut finely enough that no facet strays further than a chord tolerance from the sphere, every
    /// corner on the sphere, the body closed and wound outwards, a tolerance that needs more than 1 048 576 triangles refused.
    /// </summary>
    /// <remarks>
    /// The checks share no code with the library: the corners are measured from the centre with plain arithmetic, closure is a count
    /// of directed edges by the exact bits of their corners, the gap of a facet is read off a grid of points on it, and the winding is the
    /// sign of the triple product of its corners about the centre.
    /// </remarks>
    public class Sphere3MeshTests
    {
        private static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

        private static double Dist(GeoPoint3 a, GeoPoint3 b)
        {
            double x = a.X - b.X;
            double y = a.Y - b.Y;
            double z = a.Z - b.Z;

            return Math.Sqrt(x * x + y * y + z * z);
        }

        /// <summary>The largest distance inside the sphere of a point of a facet, read off a grid of 24 by 24 points on it.</summary>
        private static double Sag(GeoTriangle3 t, GeoPoint3 center, double radius)
        {
            double worst = 0.0;
            const int N = 24;

            for (int i = 0; i <= N; i++)
            {
                for (int j = 0; j <= N - i; j++)
                {
                    double u = (double)i / N;
                    double v = (double)j / N;
                    double w = 1.0 - u - v;
                    var p = P(w * t.A.X + u * t.B.X + v * t.C.X, w * t.A.Y + u * t.B.Y + v * t.C.Y, w * t.A.Z + u * t.B.Z + v * t.C.Z);

                    worst = Math.Max(worst, radius - Dist(p, center));
                }
            }

            return worst;
        }

        private static string Key(GeoPoint3 p) => BitConverter.DoubleToInt64Bits(p.X) + "," + BitConverter.DoubleToInt64Bits(p.Y) + "," + BitConverter.DoubleToInt64Bits(p.Z);

        private static void AssertClosedAndOutward(GeoTriangle3[] mesh, GeoPoint3 center)
        {
            var directed = new Dictionary<string, int>();
            var corners = new HashSet<string>();

            foreach (GeoTriangle3 t in mesh)
            {
                GeoPoint3[] c = { t.A, t.B, t.C };

                for (int i = 0; i < 3; i++)
                {
                    corners.Add(Key(c[i]));
                    string edge = Key(c[i]) + ">" + Key(c[(i + 1) % 3]);

                    directed.TryGetValue(edge, out int seen);
                    directed[edge] = seen + 1;
                }

                // Counter-clockwise seen from outside: the corners wind about the outward direction, whose triple product with
                // the corners from the centre is positive.
                double ax = t.A.X - center.X, ay = t.A.Y - center.Y, az = t.A.Z - center.Z;
                double bx = t.B.X - center.X, by = t.B.Y - center.Y, bz = t.B.Z - center.Z;
                double cx = t.C.X - center.X, cy = t.C.Y - center.Y, cz = t.C.Z - center.Z;
                double triple = ax * (by * cz - bz * cy) + ay * (bz * cx - bx * cz) + az * (bx * cy - by * cx);

                Assert.True(triple > 0.0, "a facet faces inwards or has no area");
            }

            // Each directed edge once and its reverse once: a closed surface wound one way, no edge shared by three facets.
            foreach (KeyValuePair<string, int> pair in directed)
            {
                Assert.Equal(1, pair.Value);

                string[] ends = pair.Key.Split('>');

                Assert.True(directed.ContainsKey(ends[1] + ">" + ends[0]), "an edge has no mate");
            }

            // Euler's count for a sphere: V - E + F = 2, with E = 3F / 2.
            Assert.Equal(2, corners.Count - 3 * mesh.Length / 2 + mesh.Length);
        }

        public static TheoryData<double, double> Cases()
        {
            return new TheoryData<double, double>
            {
                { 50.0, 0.1 },
                { 50.0, 0.01 },
                { 1.0, 0.05 },
                { 1.0, 0.3 },
                { 1000.0, 5.0 },
                { 0.01, 0.0001 },
                { 7.3, 0.0 }
            };
        }

        [Theory]
        [MemberData(nameof(Cases))]
        public void ABallIsMeshedWithEveryCornerOnTheSphere_TheSurfaceClosed_AndAllWoundOutwards(double radius, double chord)
        {
            var center = P(12.5, -7.25, 3.0);
            var ball = new GeoSphere3(center, radius);
            GeoTriangle3[] mesh = ball.TriangulateSurface(chord);

            Assert.True(mesh.Length % 20 == 0);
            Assert.Equal(0.0, Math.Sqrt(mesh.Length / 20.0) % 1.0, 12);

            foreach (GeoTriangle3 t in mesh)
            {
                foreach (GeoPoint3 corner in new[] { t.A, t.B, t.C })
                {
                    Assert.Equal(radius, Dist(corner, center), 9);
                    Assert.True(Math.Abs(Dist(corner, center) - radius) <= 4E-15 * (radius + 20.0), "a corner is off the sphere");
                }
            }

            AssertClosedAndOutward(mesh, center);
        }

        [Theory]
        [MemberData(nameof(Cases))]
        public void NoFacetOfTheMeshStraysFurtherThanTheChordTolerance_AndTheCutIsNotFinerThanItNeeds(double radius, double chord)
        {
            double tolerance = chord > 0.0 ? chord : 0.002 * radius;
            var center = P(0, 0, 0);
            GeoTriangle3[] mesh = new GeoSphere3(center, radius).TriangulateSurface(chord);
            double worst = mesh.Max(t => Sag(t, center, radius));

            Assert.True(worst <= tolerance * (1.0 + 1E-9), "a facet sags " + worst + " against " + tolerance);

            // The least cut that keeps the promise is used, so the worst facet is not far inside it unless the cut is the coarsest.
            if (mesh.Length > 20)
            {
                int n = (int)Math.Round(Math.Sqrt(mesh.Length / 20.0));
                double finer = Math.Pow((double)(n - 1) / n, 2.0);

                Assert.True(worst >= tolerance * finer * 0.5, "the cut is finer than it needs to be: " + worst + " against " + tolerance);
            }
        }

        [Fact]
        public void TheBodyOfABall_IsClosed_WoundOutwards_AndHoldsJustLessThanTheBall()
        {
            var ball = new GeoSphere3(P(0, 0, 0), 50);
            GeoSolid3 solid = ball.ToSolid(0.01);

            Assert.Equal(30420, solid.Faces.Count);
            Assert.True(solid.IsClosed(new Tolerance(1E-3, 1E-5)));
            Assert.Same(solid, solid.TurnOutwards());
            Assert.True(solid.GetVolume() < ball.Volume);
            Assert.True(solid.GetVolume() > ball.Volume * (1.0 - 0.0005));
            Assert.True(solid.GetSurfaceArea() < ball.SurfaceArea);
            Assert.True(solid.GetSurfaceArea() > ball.SurfaceArea * (1.0 - 0.0005));
        }

        [Fact]
        public void TheBodyOfABall_HasTheTrianglesOfTheSurfaceMesh_AsItsFaces()
        {
            var ball = new GeoSphere3(P(3, 4, 5), 20);
            GeoTriangle3[] mesh = ball.TriangulateSurface(0.05);
            GeoSolid3 solid = ball.ToSolid(0.05);

            Assert.Equal(mesh.Length, solid.Faces.Count);

            for (int i = 0; i < mesh.Length; i++)
            {
                Assert.Equal(new[] { mesh[i].A, mesh[i].B, mesh[i].C }, solid.Faces[i].Boundary.Vertices.ToArray());
            }
        }

        [Fact]
        public void ABallOfRadiusFiftyWithin0Point1_IsTwentyByThirteenByThirteen_AndWithin0Point01TwentyByThirtyNineByThirtyNine()
        {
            var ball = new GeoSphere3(P(0, 0, 0), 50);

            Assert.Equal(20 * 13 * 13, ball.TriangulateSurface(0.1).Length);
            Assert.Equal(20 * 39 * 39, ball.TriangulateSurface(0.01).Length);
        }

        [Fact]
        public void AFinerToleranceNeverGivesACoarserMesh()
        {
            var ball = new GeoSphere3(P(0, 0, 0), 50);
            int previous = 0;

            foreach (double tolerance in new[] { 5.0, 2.0, 1.0, 0.5, 0.2, 0.1, 0.05, 0.02, 0.01 })
            {
                int count = ball.TriangulateSurface(tolerance).Length;

                Assert.True(count >= previous, "a finer tolerance gave fewer triangles at " + tolerance);
                previous = count;
            }
        }

        [Fact]
        public void AToleranceOfNought_PicksAFifthOfAPercentOfTheRadius()
        {
            var ball = new GeoSphere3(P(0, 0, 0), 50);

            Assert.Equal(ball.TriangulateSurface(0.1), ball.TriangulateSurface(0.0));
            Assert.Equal(ball.TriangulateSurface(0.1).Length, new GeoSphere3(P(0, 0, 0), 1).TriangulateSurface(0.002).Length);
        }

        [Fact]
        public void ABallMeshedTwice_GivesTheSameMeshToTheBit_AndAMeshIsTheSameWhereverTheBallStands()
        {
            var ball = new GeoSphere3(P(1, 2, 3), 10);

            Assert.Equal(ball.TriangulateSurface(0.02), ball.TriangulateSurface(0.02));
            Assert.Equal(ball.TriangulateSurface(0.02).Length, new GeoSphere3(P(1E6, -3E6, 5E5), 10).TriangulateSurface(0.02).Length);
        }

        [Fact]
        public void ABallFarFromTheOrigin_KeepsTheChordPromise_ToTheRoundingOfItsCoordinates()
        {
            var center = P(1E6, -3E6, 5E5);
            var ball = new GeoSphere3(center, 10);
            GeoTriangle3[] mesh = ball.TriangulateSurface(0.02);

            Assert.True(mesh.Max(t => Sag(t, center, 10)) <= 0.02 * (1.0 + 1E-6));
            AssertClosedAndOutward(mesh, center);
        }

        [Theory]
        [InlineData(-0.01)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(double.NegativeInfinity)]
        public void AToleranceThatIsNegativeOrNotANumber_IsRefused(double chord)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoSphere3(P(0, 0, 0), 50).TriangulateSurface(chord));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoSphere3(P(0, 0, 0), 50).ToSolid(chord));
        }

        [Fact]
        public void AToleranceThatNeedsMoreThanAMillionTriangles_IsRefused_AndOneThatNeedsLessIsKept()
        {
            var unit = new GeoSphere3(P(0, 0, 0), 1);

            Assert.Throws<ArgumentOutOfRangeException>(() => unit.TriangulateSurface(5E-6));
            Assert.Throws<ArgumentOutOfRangeException>(() => unit.ToSolid(5E-6));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoSphere3(P(0, 0, 0), 50).TriangulateSurface(1E-4));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoSphere3(P(0, 0, 0), 50).TriangulateSurface(1E-12));

            GeoTriangle3[] fine = unit.TriangulateSurface(5.7E-6);

            Assert.True(fine.Length <= 1048576);
            Assert.True(fine.Length > 1000000);
        }

        [Fact]
        public void ADefaultBall_OrOneAtANotANumber_IsRefused_NotMeshedIntoNotANumber()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => default(GeoSphere3).TriangulateSurface(0.1));
            Assert.Throws<ArgumentOutOfRangeException>(() => default(GeoSphere3).ToSolid(0.1));
            Assert.Throws<ArgumentException>(() => new GeoSphere3(P(double.NaN, 0, 0), 5).TriangulateSurface(0.1));
            Assert.Throws<ArgumentException>(() => new GeoSphere3(P(0, double.PositiveInfinity, 0), 5).ToSolid(0.1));
        }

        [Fact]
        public void TheRefusalNamesTheToleranceAskedFor()
        {
            ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(() => new GeoSphere3(P(0, 0, 0), 50).TriangulateSurface(1E-4));

            Assert.Equal("chordTolerance", e.ParamName);
            Assert.Contains("1048576", e.Message);
        }

        [Fact]
        public void ATinyBallStillMeshes_ItsTrianglesNotMergedAwayByTheTolerance()
        {
            var ball = new GeoSphere3(P(0, 0, 0), 0.0005);
            GeoSolid3 solid = ball.ToSolid(0.0);

            Assert.Equal(20 * 13 * 13, solid.Faces.Count);
        }
    }
}
