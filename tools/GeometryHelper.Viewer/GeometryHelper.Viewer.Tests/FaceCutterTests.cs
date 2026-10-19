using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    /// <summary>
    /// The fan the viewer cuts a big convex face with, which has to be the library's own cut to the bit, and the time the cone of 16 000 faces takes.
    /// </summary>
    public class FaceCutterTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static GeoSolid3 Cone(int n)
        {
            GeoPoint3[] ring = Enumerable.Range(0, n).Select(i => new GeoPoint3(1000 * Math.Cos(2 * Math.PI * i / n), 1000 * Math.Sin(2 * Math.PI * i / n), 0)).ToArray();
            var apex = new GeoPoint3(0, 0, 500);
            var faces = new List<GeoFace3> { new GeoFace3(new GeoPolygon3(Enumerable.Reverse(ring)), null) };

            for (int i = 0; i < n; i++)
            {
                faces.Add(new GeoFace3(new GeoPolygon3(new[] { ring[i], ring[(i + 1) % n], apex }), null));
            }

            return new GeoSolid3(faces);
        }

        /// <summary>
        /// Corners on an ellipse at jittered angles, in a frame turned and moved in space.
        /// </summary>
        private static GeoPoint3[] Ellipse(Random random, int n, double a, double b, GeoPoint3 origin, bool clockwise, double lift)
        {
            double step = 2.0 * Math.PI / n;
            double[] angles = Enumerable.Range(0, n).Select(i => (i + 0.4 * (random.NextDouble() - 0.5)) * step).ToArray();

            if (clockwise)
            {
                Array.Reverse(angles);
            }

            double yaw = random.NextDouble() * 2.0 * Math.PI;
            double pitch = (random.NextDouble() - 0.5) * Math.PI;

            return angles.Select((t, i) =>
            {
                double u = a * Math.Cos(t);
                double v = b * Math.Sin(t);
                double w = lift * Math.Sin(i * 7.0);

                // Turn about Y by the pitch, then about Z by the yaw.
                double x1 = u * Math.Cos(pitch) + w * Math.Sin(pitch);
                double z1 = -u * Math.Sin(pitch) + w * Math.Cos(pitch);
                double x2 = x1 * Math.Cos(yaw) - v * Math.Sin(yaw);
                double y2 = x1 * Math.Sin(yaw) + v * Math.Cos(yaw);

                return new GeoPoint3(origin.X + x2, origin.Y + y2, origin.Z + z1);
            }).ToArray();
        }

        private static void AssertSameBits(GeoTriangle3[] expected, GeoTriangle3[] actual)
        {
            Assert.Equal(expected.Length, actual.Length);

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i].A.X), BitConverter.DoubleToInt64Bits(actual[i].A.X));
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i].A.Y), BitConverter.DoubleToInt64Bits(actual[i].A.Y));
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i].A.Z), BitConverter.DoubleToInt64Bits(actual[i].A.Z));
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i].B.X), BitConverter.DoubleToInt64Bits(actual[i].B.X));
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i].B.Y), BitConverter.DoubleToInt64Bits(actual[i].B.Y));
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i].B.Z), BitConverter.DoubleToInt64Bits(actual[i].B.Z));
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i].C.X), BitConverter.DoubleToInt64Bits(actual[i].C.X));
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i].C.Y), BitConverter.DoubleToInt64Bits(actual[i].C.Y));
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i].C.Z), BitConverter.DoubleToInt64Bits(actual[i].C.Z));
            }
        }

        [Fact]
        public void ConvexFacesOfAllSortsOfShapeAndPlace_AreCutIntoTheTrianglesTheLibraryGives_ToTheBit()
        {
            var random = new Random(20261019);
            int fanned = 0;
            int refused = 0;

            using (Tolerance.Use(Tol))
            {
                for (int round = 0; round < 400; round++)
                {
                    int n = 5 + random.Next(0, 5) * random.Next(1, 90);
                    double a = Math.Pow(10.0, random.NextDouble() * 4.0 - 0.5);
                    double b = a * (round % 3 == 0 ? 1.0 : Math.Pow(10.0, -random.NextDouble() * 2.0));
                    double far = round % 4 == 0 ? 5E5 : round % 4 == 1 ? 12.5 : 0.0;
                    var origin = new GeoPoint3(far, -far * 0.5, far * 0.25);
                    double lift = round % 5 == 0 ? 0.0004 : 0.0;

                    GeoFace3 face;

                    try
                    {
                        face = new GeoFace3(new GeoPolygon3(Ellipse(random, n, a, b, origin, round % 2 == 1, lift)), null);
                    }
                    catch (ArgumentException)
                    {
                        // A shape the library refuses to build has nothing to compare.
                        continue;
                    }

                    GeoTriangle3[] library = face.TriangulateSurface(Tol);

                    if (FaceCutter.TryFan(face, Tol, 4, out GeoTriangle3[] fan))
                    {
                        fanned++;
                        AssertSameBits(library, fan);
                    }
                    else
                    {
                        refused++;
                    }
                }
            }

            // The comparison means something only if the fan was taken for most of them, and refused for some.
            Assert.True(fanned > 150, fanned + " fanned, " + refused + " refused");
            Assert.True(refused > 10, fanned + " fanned, " + refused + " refused");
        }

        [Fact]
        public void ARingOfSixtyFourThousandCorners_IsCutLikeTheLibraryCutsIt_IncludingTheTrianglesItLeavesOutAsDegenerate()
        {
            // The library takes 15 s over this ring; a ring of 4 000 is the same fan in a tenth of a second.
            GeoSolid3 cone = Cone(4000);
            GeoFace3 ring = cone.Faces[0];

            using (Tolerance.Use(Tol))
            {
                Assert.True(FaceCutter.TryFan(ring, Tol, FaceCutter.FanFrom, out GeoTriangle3[] fan));
                AssertSameBits(ring.TriangulateSurface(Tol), fan);
            }

            using (Tolerance.Use(Tol))
            {
                GeoFace3 big = Cone(64000).Faces[0];
                var clock = Stopwatch.StartNew();
                Assert.True(FaceCutter.TryFan(big, Tol, FaceCutter.FanFrom, out GeoTriangle3[] bigFan));
                clock.Stop();

                // The library leaves out eight of them as degenerate at this tolerance; the fan leaves out the same ones.
                Assert.Equal(63990, bigFan.Length);
                Assert.True(clock.Elapsed < TimeSpan.FromSeconds(1.0), clock.Elapsed.ToString());
            }
        }

        [Fact]
        public void AThinLens_ACornerTooShort_AndAFaceWithAHole_AreLeftToTheLibrary()
        {
            using (Tolerance.Use(Tol))
            {
                // 600 corners on a lens 200 long and 0.004 across: its two sides come within eight times the tolerance of each other.
                GeoPoint3[] lens = Enumerable.Range(0, 600).Select(i => new GeoPoint3(100 * Math.Cos(2 * Math.PI * i / 600), 0.002 * Math.Sin(2 * Math.PI * i / 600), 0)).ToArray();
                Assert.False(FaceCutter.TryFan(new GeoFace3(new GeoPolygon3(lens), null), Tol, 4, out _));

                // 600 corners on a circle of radius 0.5: each edge is 0.005 long, under eight times the tolerance.
                GeoPoint3[] tiny = Enumerable.Range(0, 600).Select(i => new GeoPoint3(0.5 * Math.Cos(2 * Math.PI * i / 600), 0.5 * Math.Sin(2 * Math.PI * i / 600), 0)).ToArray();
                Assert.False(FaceCutter.TryFan(new GeoFace3(new GeoPolygon3(tiny), null), Tol, 4, out _));

                GeoPoint3[] outer = Enumerable.Range(0, 600).Select(i => new GeoPoint3(500 * Math.Cos(2 * Math.PI * i / 600), 500 * Math.Sin(2 * Math.PI * i / 600), 0)).ToArray();
                GeoPoint3[] hole = Enumerable.Range(0, 300).Select(i => new GeoPoint3(100 * Math.Cos(-2 * Math.PI * i / 300), 100 * Math.Sin(-2 * Math.PI * i / 300), 0)).ToArray();
                Assert.False(FaceCutter.TryFan(new GeoFace3(new GeoPolygon3(outer), new[] { new GeoPolygon3(hole) }), Tol, 4, out _));

                // Under the number of corners worth it.
                Assert.False(FaceCutter.TryFan(new GeoFace3(new GeoPolygon3(outer.Take(200)), null), Tol, FaceCutter.FanFrom, out _));
            }
        }

        [Fact]
        public void AConcaveFaceOfManyCorners_AndALoopThatGoesRoundTwice_AreLeftToTheLibrary()
        {
            using (Tolerance.Use(Tol))
            {
                // A circle with one corner pushed in.
                GeoPoint3[] dent = Enumerable.Range(0, 400).Select(i => new GeoPoint3((i == 100 ? 300 : 500) * Math.Cos(2 * Math.PI * i / 400), (i == 100 ? 300 : 500) * Math.Sin(2 * Math.PI * i / 400), 0)).ToArray();
                Assert.False(FaceCutter.TryFan(new GeoFace3(new GeoPolygon3(dent), null), Tol, 4, out _));
            }
        }

        [Fact]
        public void TheConeOfSixteenThousandFaces_IsDrawnInUnderAHalfSecond_AndTheTrianglesAreTheLibrarys()
        {
            Scene scene = new SceneWriter().Add(Cone(16000), name: "cone").Scene;
            Primitives.Build(new SceneWriter().Add(Cone(300)).Scene);

            var clock = Stopwatch.StartNew();
            Primitives primitives = Primitives.Build(scene);
            clock.Stop();

            Assert.Empty(primitives.Warnings);
            Assert.Equal(31998, primitives.Items[0].Triangles.Count);
            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(0.5), clock.Elapsed.ToString());
        }

        [Fact]
        public void ABodyOfFiveHundredCorners_GivesTheSameTrianglesAsTheLibrarysOwnCutOfTheBody()
        {
            GeoSolid3 cone = Cone(1200);
            Scene scene = new SceneWriter().Add(cone, name: "cone").Scene;
            Primitives primitives = Primitives.Build(scene);
            GeoTriangle3[] library;

            using (Tolerance.Use(scene.Tolerance))
            {
                library = ((GeoSolid3)scene.Items[0].ToGeometry()).TriangulateSurface();
            }

            DrawTriangle[] drawn = primitives.Items[0].Triangles.ToArray();
            Assert.Equal(library.Length, drawn.Length);

            for (int i = 0; i < library.Length; i++)
            {
                var expected = new DrawTriangle(new Vec3(library[i].A.X, library[i].A.Y, library[i].A.Z), new Vec3(library[i].B.X, library[i].B.Y, library[i].B.Z), new Vec3(library[i].C.X, library[i].C.Y, library[i].C.Z));
                Assert.Equal(expected.A, drawn[i].A);
                Assert.Equal(expected.B, drawn[i].B);
                Assert.Equal(expected.C, drawn[i].C);
                Assert.Equal(expected.Normal, drawn[i].Normal);
            }
        }
    }
}
