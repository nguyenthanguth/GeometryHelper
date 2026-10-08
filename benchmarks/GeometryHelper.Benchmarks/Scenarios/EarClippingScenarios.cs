using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;

namespace GeometryHelper.Benchmarks.Scenarios
{
    /// <summary>
    /// Faces broken into triangles for a mesh of their surface: <see cref="GeoFace3.TriangulateSurface(Tolerance)"/>,
    /// which before it clips any ear checks, by a sweep along the X of the face's own frame, that no two of its rings come
    /// within the point tolerance of each other.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A face of the plane z = 0 wound counter-clockwise seen from above has the X of its frame along the world's Y, so
    /// each X case lays its long edges along the world's Y, every one overlapping every other along the frame's X. Each Y
    /// case is the same face with X and Y swapped in every corner, which rounds nothing: it is wound the other way, its
    /// frame has X along the world's -Y, and the long edges lie along the frame's Y. The note of each case gives the pairs
    /// counted in the frame itself, so that it shows which is which.
    /// </para>
    /// <para>
    /// The slots are a plate with 300 slots in it, each 4 000 long and 2 wide, 4 apart: 1 204 edges, 602 of them running
    /// the whole length side by side, and rings for the check to tell apart. The comb is one ring of 500 teeth 4 000 long
    /// and 5 wide, 5 apart: 2 000 edges, 1 000 of them running the whole length. Measured at 36f56df, the check along X
    /// took 6 ms of the 41 the slots took, and 18 of the 57 the comb took; the rest is clipping the ears, which no
    /// choice of axis changes, so the comb shows the check best.
    /// </para>
    /// <para>
    /// The small case is 3 000 faces of 4 to 12 corners, a quarter of them with one to three small square holes near
    /// the middle, on a sloping plane.
    /// </para>
    /// </remarks>
    internal static class EarClippingScenarios
    {
        private const int Slots = 300;
        private const double SlotLength = 4000.0;
        private const int SmallCount = 3000;

        private static readonly Tolerance Within = Tolerance.Default;

        /// <summary>
        /// Every case of the surface triangulation.
        /// </summary>
        internal static IEnumerable<BenchmarkCase> All()
        {
            yield return new BenchmarkCase("EarClipping.AxisTrap.Slots.X", 4 * Slots + 4, "edges", false, () => Slotted(false));
            yield return new BenchmarkCase("EarClipping.AxisTrap.Slots.Y", 4 * Slots + 4, "edges", false, () => Slotted(true));
            yield return new BenchmarkCase("EarClipping.AxisTrap.Comb.X", 4 * Teeth, "edges", false, () => Comb(false));
            yield return new BenchmarkCase("EarClipping.AxisTrap.Comb.Y", 4 * Teeth, "edges", false, () => Comb(true));
            yield return new BenchmarkCase("EarClipping.Small", SmallCount, "faces", false, Small);
        }

        /// <summary>
        /// The slotted plate, its slots along the world's Y, or with X and Y swapped when asked.
        /// </summary>
        private static Fixture Slotted(bool swapped)
        {
            GeoPoint3 At(double x, double y) => swapped ? new GeoPoint3(y, x, 0.0) : new GeoPoint3(x, y, 0.0);

            double width = 6.0 * Slots + 20.0, height = SlotLength + 20.0;
            var boundary = new GeoPolygon3(new[] { At(0, 0), At(width, 0), At(width, height), At(0, height) }, Within);
            var holes = new List<GeoPolygon3>(Slots);

            for (int h = 0; h < Slots; h++)
            {
                double x = 10.0 + 6.0 * h;
                holes.Add(new GeoPolygon3(new[] { At(x, 10), At(x + 2, 10), At(x + 2, 10 + SlotLength), At(x, 10 + SlotLength) }, Within));
            }

            var face = new GeoFace3(boundary, holes, Within);
            GeoFace3[] faces = { face };

            return Fixture.Of(() => faces.Select(f => f.TriangulateSurface(Within)).ToArray(), Sign, Describe, Pairs(face));
        }

        private const int Teeth = 500;
        private const double ToothLength = 4000.0;

        /// <summary>
        /// The comb, its teeth along the world's Y, or with X and Y swapped when asked.
        /// </summary>
        private static Fixture Comb(bool swapped)
        {
            GeoPoint3 At(double x, double y) => swapped ? new GeoPoint3(y, x, 0.0) : new GeoPoint3(x, y, 0.0);

            // Counter-clockwise from above: along the bottom of the spine, which is 20 deep, then up and down each tooth
            // from the last, at x 10 * (Teeth - 1) to that and 5, to the first, at x 0 to 5, and down the side of that.
            var corners = new List<GeoPoint3> { At(0.0, 0.0), At(10.0 * (Teeth - 1) + 5.0, 0.0) };

            for (int i = Teeth - 1; i >= 0; i--)
            {
                double left = 10.0 * i, right = left + 5.0;
                corners.Add(At(right, ToothLength));
                corners.Add(At(left, ToothLength));

                if (i > 0)
                {
                    corners.Add(At(left, 20.0));
                    corners.Add(At(left - 5.0, 20.0));
                }
            }

            var face = new GeoFace3(new GeoPolygon3(corners, Within), null, Within);
            GeoFace3[] faces = { face };

            return Fixture.Of(() => faces.Select(f => f.TriangulateSurface(Within)).ToArray(), Sign, Describe, Pairs(face));
        }

        /// <summary>
        /// Star-shaped faces about points scattered over the plane z = x / 4 + y / 2, from a fixed seed, their corners in
        /// the order of the way round a square, each at its own distance out; a quarter of them with holes 5 across within
        /// 10 of the middle, where every face is solid.
        /// </summary>
        private static Fixture Small()
        {
            var random = new Random(2001);
            var faces = new GeoFace3[SmallCount];

            GeoPoint3 On(double x, double y) => new GeoPoint3(x, y, 0.25 * x + 0.5 * y);

            for (int k = 0; k < faces.Length; k++)
            {
                double cx = random.Next(100000), cy = random.Next(100000);
                int count = 4 + random.Next(9);
                var corners = new GeoPoint3[count];

                for (int i = 0; i < count; i++)
                {
                    double s = (i + 0.2 + 0.6 * random.NextDouble()) * 8.0 / count;
                    double r = 60.0 + 40.0 * random.NextDouble();
                    GeoPoint2 d = RoundTheSquare(s);
                    corners[i] = On(cx + r * d.X, cy + r * d.Y);
                }

                var holes = new List<GeoPolygon3>();

                if (random.Next(4) == 0)
                {
                    int holeCount = 1 + random.Next(3);

                    for (int h = 0; h < holeCount; h++)
                    {
                        // Side by side along x, 7 apart, so that they keep clear of each other.
                        double x = cx - 10.0 + 7.0 * h, y = cy - 2.5;
                        holes.Add(new GeoPolygon3(new[] { On(x, y), On(x + 5, y), On(x + 5, y + 5), On(x, y + 5) }, Within));
                    }
                }

                faces[k] = new GeoFace3(new GeoPolygon3(corners, Within), holes, Within);
            }

            return Fixture.Of(() => faces.Select(f => f.TriangulateSurface(Within)).ToArray(), Sign, Describe, null);
        }

        private static GeoPoint2 RoundTheSquare(double s)
        {
            if (s < 2.0)
            {
                return new GeoPoint2(1.0, s - 1.0);
            }

            if (s < 4.0)
            {
                return new GeoPoint2(3.0 - s, 1.0);
            }

            if (s < 6.0)
            {
                return new GeoPoint2(-1.0, 5.0 - s);
            }

            return new GeoPoint2(s - 7.0, -1.0);
        }

        /// <summary>
        /// How many pairs of ring edges a sweep along each axis of the face's own frame looks at, as the check that the
        /// rings stand apart reaches: the frame the triangulation reads the face in, built the same way from its plane.
        /// </summary>
        private static string Pairs(GeoFace3 face)
        {
            var frame = new GeoCoordinateSystem3(face.GetPlane());
            var lowX = new List<double>();
            var highX = new List<double>();
            var lowY = new List<double>();
            var highY = new List<double>();

            foreach (GeoPolygon3 ring in new[] { face.Boundary }.Concat(face.Holes))
            {
                IReadOnlyList<GeoPoint3> corners = ring.Vertices;

                for (int i = 0; i < corners.Count; i++)
                {
                    GeoPoint3 a = frame.ToLocal(corners[i]), b = frame.ToLocal(corners[(i + 1) % corners.Count]);
                    lowX.Add(Math.Min(a.X, b.X));
                    highX.Add(Math.Max(a.X, b.X));
                    lowY.Add(Math.Min(a.Y, b.Y));
                    highY.Add(Math.Max(a.Y, b.Y));
                }
            }

            return SweepPairs.Describe(SweepPairs.Count(lowX, highX, Within.EqualPoint), SweepPairs.Count(lowY, highY, Within.EqualPoint))
                + " (in the face's frame)";
        }

        /// <summary>
        /// Signs the triangles: for each face, how many, then each in order, every corner to the last bit.
        /// </summary>
        private static void Sign(ResultSignature signature, GeoTriangle3[][] results)
        {
            signature.Add(results.Length);

            foreach (GeoTriangle3[] triangles in results)
            {
                signature.Add(triangles.Length);

                foreach (GeoTriangle3 triangle in triangles)
                {
                    signature.Add(triangle);
                }
            }
        }

        private static string Describe(GeoTriangle3[][] results)
            => Report.Count(results.Sum(t => (long)t.Length)) + " triangles from " + Report.Count(results.Length) + " faces";
    }
}
