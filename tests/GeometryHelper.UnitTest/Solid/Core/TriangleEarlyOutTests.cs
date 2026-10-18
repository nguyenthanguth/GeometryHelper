using System;
using System.Collections.Generic;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Two triangles one of which lies clear to one side of the other's plane are now answered from three
    /// distances. The full tests, copied below as they stood, must still say the same for every pair — above all
    /// for pairs lying about the tolerance from each other, where a margin too thin would show.
    /// </summary>
    public class TriangleEarlyOutTests
    {
        private static readonly Tolerance Tol = Tolerance.Global;

        #region The tests as they stood

        private static bool CollidesAsBefore(GeoTriangle3 first, GeoTriangle3 second)
        {
            if (first.IsDegenerate(Tol) || second.IsDegenerate(Tol))
            {
                return false;
            }

            for (int i = 0; i < 3; i++)
            {
                if (Intersection3.TryIntersectWith(first.GetEdgeAt(i), second, out _, Tol) ||
                    Intersection3.TryIntersectWith(second.GetEdgeAt(i), first, out _, Tol))
                {
                    return true;
                }
            }

            // Amended with fault F3 (2026-10-12): the pair is in one plane when either lies wholly in the other's
            // plane, read both ways round, and two that neither cross nor overlap in one plane still touch where they
            // come within the point tolerance. The full test before it read one corner of the second only.
            if (!Parallel3.IsParallel(first.Normal, second.Normal, Tol) ||
                (!LiesIn(first, second) && !LiesIn(second, first)))
            {
                return WithinThePointTolerance(first, second);
            }

            for (int i = 0; i < 3; i++)
            {
                if (Containment3.Contains(second, first[i], Tol) || Containment3.Contains(first, second[i], Tol))
                {
                    return true;
                }
            }

            return WithinThePointTolerance(first, second);
        }

        private static bool LiesIn(GeoTriangle3 plane, GeoTriangle3 other)
            => Math.Abs(plane.GetPlane().SignedDistanceTo(other.A)) <= Tol.EqualPlanar &&
               Math.Abs(plane.GetPlane().SignedDistanceTo(other.B)) <= Tol.EqualPlanar &&
               Math.Abs(plane.GetPlane().SignedDistanceTo(other.C)) <= Tol.EqualPlanar;

        private static bool WithinThePointTolerance(GeoTriangle3 first, GeoTriangle3 second)
        {
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    // The edges as Distance3 measures them: the clash reads the same exact gap, the same either way round, so a pair
                    // standing at the tolerance is decided alike from both sides (it used to be decided by the order of the search).
                    if (Distance3.DistanceTo(first.GetEdgeAt(i), second.GetEdgeAt(j), Tol) <= Tol.EqualPoint)
                    {
                        return true;
                    }
                }
            }

            for (int i = 0; i < 3; i++)
            {
                // The corner's distance as Distance3 measures it, a sliver where it is.
                if (Distance3.DistanceTo(second, first[i]) <= Tol.EqualPoint || Distance3.DistanceTo(first, second[i]) <= Tol.EqualPoint)
                {
                    return true;
                }
            }

            return false;
        }

        private static GeoLine3 ShortestAsBefore(GeoTriangle3 first, GeoTriangle3 second)
        {
            for (int i = 0; i < 3; i++)
            {
                if (Intersection3.TryIntersectWith(first.GetEdgeAt(i), second, out GeoPoint3 through, Tol) ||
                    Intersection3.TryIntersectWith(second.GetEdgeAt(i), first, out through, Tol))
                {
                    return new GeoLine3(through, through);
                }
            }

            GeoLine3 best = Projection3.GetShortestLineTo(first.GetEdgeAt(0), second.GetEdgeAt(0), Tol);

            void Consider(GeoLine3 candidate)
            {
                if (candidate.Length < best.Length)
                {
                    best = candidate;
                }
            }

            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    Consider(Projection3.GetShortestLineTo(first.GetEdgeAt(i), second.GetEdgeAt(j), Tol));
                }
            }

            for (int i = 0; i < 3; i++)
            {
                Consider(new GeoLine3(first[i], Projection3.ProjectToTriangle(second, first[i])));
                Consider(new GeoLine3(Projection3.ProjectToTriangle(first, second[i]), second[i]));
            }

            return best;
        }

        #endregion

        private static GeoPoint3 Point(Random rng, double scale, GeoPoint3 around)
            => new GeoPoint3(around.X + (rng.NextDouble() - 0.5) * scale, around.Y + (rng.NextDouble() - 0.5) * scale, around.Z + (rng.NextDouble() - 0.5) * scale);

        /// <summary>
        /// Pairs of every sort: far apart and crossing, and above all one triangle set off the other's plane by
        /// a few tolerances, turned a hair, touching at a corner or an edge, at the origin and far from it.
        /// </summary>
        private static IEnumerable<(GeoTriangle3, GeoTriangle3)> Pairs(int seed, int count)
        {
            var rng = new Random(seed);
            double e = Math.Max(Tol.EqualPoint, Tol.EqualPlanar);
            double[] offsets = { 0.0, 0.3 * e, 0.9 * e, e, 1.1 * e, 1.9 * e, 2.0 * e, 2.1 * e, 3.0 * e, 10.0 * e, 1.0 };

            for (int k = 0; k < count; k++)
            {
                GeoPoint3 origin = rng.NextDouble() < 0.3 ? new GeoPoint3(1E5, -2E5, 3E4) : GeoPoint3.Origin;
                double size = rng.NextDouble() < 0.5 ? 10.0 : 1000.0;
                var first = new GeoTriangle3(Point(rng, size, origin), Point(rng, size, origin), Point(rng, size, origin));

                if (first.IsDegenerate(Tol))
                {
                    continue;
                }

                GeoVector3 normal = first.Normal;
                GeoTriangle3 second;

                switch (rng.Next(4))
                {
                    case 0:
                        // Anywhere nearby.
                        second = new GeoTriangle3(Point(rng, size, origin), Point(rng, size, origin), Point(rng, size, origin));
                        break;

                    case 1:
                    {
                        // The same triangle slid along its plane and set off it, one side or the other.
                        double off = offsets[rng.Next(offsets.Length)] * (rng.NextDouble() < 0.5 ? -1 : 1);
                        GeoVector3 slide = new GeoVector3((rng.NextDouble() - 0.5) * size, (rng.NextDouble() - 0.5) * size, (rng.NextDouble() - 0.5) * size);
                        slide = slide.Subtract(normal.Multiply(slide.DotProduct(normal))).Multiply(0.5);
                        GeoVector3 move = slide.Add(normal.Multiply(off));
                        second = new GeoTriangle3(first.A.Add(move), first.B.Add(move), first.C.Add(move));
                        break;
                    }

                    case 2:
                    {
                        // A triangle standing off the plane, its nearest corner a few tolerances from a point of the first.
                        double off = offsets[rng.Next(offsets.Length)] * (rng.NextDouble() < 0.5 ? -1 : 1);
                        GeoPoint3 foot = new GeoPoint3((first.A.X + first.B.X + first.C.X) / 3, (first.A.Y + first.B.Y + first.C.Y) / 3, (first.A.Z + first.B.Z + first.C.Z) / 3);
                        GeoPoint3 corner = foot.Add(normal.Multiply(off));
                        double side = off >= 0 ? 1.0 : -1.0;
                        GeoPoint3 b = Point(rng, size, corner).Add(normal.Multiply(side * size * (0.2 + rng.NextDouble())));
                        GeoPoint3 c = Point(rng, size, corner).Add(normal.Multiply(side * size * (0.2 + rng.NextDouble())));
                        second = new GeoTriangle3(corner, b, c);
                        break;
                    }

                    default:
                    {
                        // Nearly parallel: the first turned a hair about its centre, and set off.
                        double off = offsets[rng.Next(offsets.Length)];
                        GeoPoint3 centre = first.Centroid;
                        GeoTransform3 turn = GeoTransform3.RotationAxis(centre, new GeoVector3(rng.NextDouble() - 0.5, rng.NextDouble() - 0.5, rng.NextDouble() - 0.5), (rng.NextDouble() - 0.5) * 1E-5);
                        GeoVector3 move = normal.Multiply(off);
                        second = new GeoTriangle3(first.A.TransformBy(turn).Add(move), first.B.TransformBy(turn).Add(move), first.C.TransformBy(turn).Add(move));
                        break;
                    }
                }

                yield return rng.NextDouble() < 0.5 ? (first, second) : (second, first);
            }
        }

        [Fact]
        public void EveryPairCollidesOrNotAsTheFullTestSaid()
        {
            int touching = 0, clear = 0, pairs = 0;

            foreach ((GeoTriangle3 a, GeoTriangle3 b) in Pairs(1, 60000))
            {
                bool before = CollidesAsBefore(a, b);
                Assert.True(before == Collision3.CollidesWith(a, b, Tol), $"{a} / {b}: the full test said {before}");

                pairs++;
                touching += before ? 1 : 0;
                clear += !a.IsDegenerate(Tol) && !b.IsDegenerate(Tol) && (Collision3.IsClearOf(a, b, Tol) || Collision3.IsClearOf(b, a, Tol)) ? 1 : 0;
            }

            Assert.True(touching > pairs / 10, $"only {touching} of {pairs} pairs touch");
            Assert.True(clear > pairs / 5, $"only {clear} of {pairs} pairs were settled early");
        }

        [Fact]
        public void EveryShortestSegmentIsTheOneTheFullSearchGave()
        {
            foreach ((GeoTriangle3 a, GeoTriangle3 b) in Pairs(2, 30000))
            {
                GeoLine3 before = ShortestAsBefore(a, b);
                GeoLine3 now = Projection3.GetShortestLineTo(a, b, Tol);

                Assert.True(before.StartPoint.Equals(now.StartPoint) && before.EndPoint.Equals(now.EndPoint),
                    $"{a} / {b}: {before} before, {now} now");
            }
        }

        [Fact]
        public void EveryDistanceIsTheOneTheFullTestGave()
        {
            foreach ((GeoTriangle3 a, GeoTriangle3 b) in Pairs(3, 30000))
            {
                // Rule R (item 5): the distance is the exact gap, nought only where the two cross, and the pair collides
                // exactly where it is within the point tolerance. It used to read nought for every pair that collided. The
                // gap is the least of the edges against the edges and the corners against the other triangle, as the closest-point
                // search of the projections finds them, to a billionth: a sliver is measured by the library along its own normal.
                double gap = double.MaxValue;

                for (int i = 0; i < 3; i++)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        gap = Math.Min(gap, Distance3.DistanceTo(a.GetEdgeAt(i), b.GetEdgeAt(j), Tol));
                    }
                }

                for (int i = 0; i < 3; i++)
                {
                    gap = Math.Min(gap, Distance3.DistanceTo(b, a[i]));
                    gap = Math.Min(gap, Distance3.DistanceTo(a, b[i]));
                }

                // A triangle with no area touches nothing by convention, whatever its distance; the pair is not held here.
                if (a.IsDegenerate(Tol) || b.IsDegenerate(Tol))
                {
                    continue;
                }

                double distance = Distance3.DistanceTo(a, b, Tol);
                bool collides = CollidesAsBefore(a, b);

                if (distance == 0.0 && gap != 0.0)
                {
                    // Two triangles that pass through each other have no pair of edges or corners at nought.
                    Assert.True(collides, $"{a} / {b}: crossing but not colliding");
                }
                else
                {
                    Assert.InRange(distance, gap - 1E-9 * Math.Max(1.0, gap), gap + 1E-9 * Math.Max(1.0, gap));
                }

                // A pair set off by exactly the tolerance, 1E-3 at coordinates of 1E5, is a few ten-billionths either side of it by
                // rounding, and the collision of two triangles, which the clash reads and keeps as it was, decides it by its own margin:
                // the pair is not held within 1E-8 of the tolerance.
                if (Math.Abs(distance - Tol.EqualPoint) > 1E-8)
                {
                    Assert.Equal(collides, distance <= Tol.EqualPoint);
                }
            }
        }
    }
}
