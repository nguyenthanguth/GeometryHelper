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

            if (!Parallel3.IsParallel(first.Normal, second.Normal, Tol))
            {
                return false;
            }

            if (Math.Abs(first.GetPlane().SignedDistanceTo(second.A)) > Tol.EqualPlanar)
            {
                return false;
            }

            for (int i = 0; i < 3; i++)
            {
                if (Containment3.Contains(second, first[i], Tol) || Containment3.Contains(first, second[i], Tol))
                {
                    return true;
                }
            }

            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    if (Distance3.DistanceTo(first.GetEdgeAt(i), second.GetEdgeAt(j), Tol) <= Tol.EqualPoint)
                    {
                        return true;
                    }
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
                double before = 0.0;

                if (!CollidesAsBefore(a, b))
                {
                    before = double.MaxValue;

                    for (int i = 0; i < 3; i++)
                    {
                        for (int j = 0; j < 3; j++)
                        {
                            before = Math.Min(before, Distance3.DistanceTo(a.GetEdgeAt(i), b.GetEdgeAt(j), Tol));
                        }
                    }

                    for (int i = 0; i < 3; i++)
                    {
                        before = Math.Min(before, Distance3.DistanceTo(b, a[i]));
                        before = Math.Min(before, Distance3.DistanceTo(a, b[i]));
                    }
                }

                Assert.Equal(before, Distance3.DistanceTo(a, b, Tol));
            }
        }
    }
}
