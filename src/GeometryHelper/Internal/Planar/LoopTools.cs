using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Internal.Planar
{
    /// <summary>
    /// An outer boundary loop together with the holes directly inside it.
    /// </summary>
    internal sealed class LoopGroup
    {
        public LoopGroup(List<GeoPoint2> outer, double area)
        {
            Outer = outer;
            Area = area;
        }

        /// <summary>The outer boundary, counter-clockwise.</summary>
        public List<GeoPoint2> Outer { get; }

        /// <summary>The holes, clockwise.</summary>
        public List<List<GeoPoint2>> Holes { get; } = new List<List<GeoPoint2>>();

        /// <summary>The area inside the outer boundary, holes not taken off.</summary>
        public double Area { get; }
    }

    /// <summary>
    /// Measuring, cleaning and sorting the closed loops the planar algorithms take in and hand back.
    /// </summary>
    internal static class LoopTools
    {
        /// <summary>
        /// The area of a loop, positive when it runs counter-clockwise, measured from its first point so that
        /// coordinates far from the origin cost no precision.
        /// </summary>
        public static double SignedArea(IReadOnlyList<GeoPoint2> loop)
        {
            double sum = 0.0;
            GeoPoint2 reference = loop[0];

            for (int i = 1; i + 1 < loop.Count; i++)
            {
                sum += (loop[i] - reference).Cross(loop[i + 1] - reference);
            }

            return sum * 0.5;
        }

        public static double Perimeter(IReadOnlyList<GeoPoint2> loop)
        {
            double sum = 0.0;

            for (int i = 0; i < loop.Count; i++)
            {
                sum += loop[i].DistanceTo(loop[(i + 1) % loop.Count]);
            }

            return sum;
        }

        /// <summary>
        /// Removes from a closed loop every point within <paramref name="merge"/> of the one before it, and every
        /// point lying within <paramref name="straight"/> of the line through its neighbours, which also takes
        /// out a spike doubling back on itself. Returns null when fewer than three points are left.
        /// </summary>
        public static List<GeoPoint2> Clean(IReadOnlyList<GeoPoint2> loop, double merge, double straight)
        {
            List<GeoPoint2> points = new List<GeoPoint2>(loop.Count);

            foreach (GeoPoint2 point in loop)
            {
                if (points.Count == 0 || points[points.Count - 1].DistanceTo(point) > merge)
                {
                    points.Add(point);
                }
            }

            while (points.Count > 1 && points[points.Count - 1].DistanceTo(points[0]) <= merge)
            {
                points.RemoveAt(points.Count - 1);
            }

            // Removing a point can bring its neighbours into line, so the pass repeats until nothing changes.
            bool changed = true;

            while (changed && points.Count >= 3)
            {
                changed = false;

                for (int i = 0; i < points.Count && points.Count >= 3; i++)
                {
                    GeoPoint2 previous = points[(i - 1 + points.Count) % points.Count];
                    GeoPoint2 current = points[i];
                    GeoPoint2 next = points[(i + 1) % points.Count];
                    GeoVector2 chord = next - previous;
                    double chordLength = chord.Length;

                    bool drop = chordLength <= merge
                        ? true
                        : Math.Abs(chord.Cross(current - previous)) / chordLength <= straight;

                    if (drop)
                    {
                        points.RemoveAt(i);
                        changed = true;
                        i--;
                    }
                }
            }

            return points.Count >= 3 ? points : null;
        }

        /// <summary>
        /// Removes from an open chain every point within <paramref name="merge"/> of the one before it, and every
        /// inner point lying within <paramref name="straight"/> of the segment joining its neighbours. A point
        /// where the chain turns back on itself is kept, since a chain, unlike a loop, can mean that.
        /// </summary>
        public static List<GeoPoint2> CleanChain(IReadOnlyList<GeoPoint2> chain, double merge, double straight)
        {
            List<GeoPoint2> points = new List<GeoPoint2>(chain.Count);

            foreach (GeoPoint2 point in chain)
            {
                if (points.Count == 0 || points[points.Count - 1].DistanceTo(point) > merge)
                {
                    points.Add(point);
                }
            }

            bool changed = true;

            while (changed && points.Count >= 3)
            {
                changed = false;

                for (int i = 1; i + 1 < points.Count; i++)
                {
                    GeoPoint2 previous = points[i - 1];
                    GeoPoint2 current = points[i];
                    GeoPoint2 next = points[i + 1];
                    GeoVector2 chord = next - previous;
                    double chordLength = chord.Length;

                    if (chordLength <= merge)
                    {
                        continue;
                    }

                    // Straight through only: the point must also lie between its neighbours.
                    double along = (current - previous).Dot(chord) / chordLength;

                    if (along > 0.0 && along < chordLength && Math.Abs(chord.Cross(current - previous)) / chordLength <= straight)
                    {
                        points.RemoveAt(i);
                        changed = true;
                        i--;
                    }
                }
            }

            return points;
        }

        /// <summary>
        /// Checks whether a point lies inside a loop by the even-odd rule. A point on the loop may go either way.
        /// </summary>
        public static bool Contains(IReadOnlyList<GeoPoint2> loop, GeoPoint2 point)
        {
            bool inside = false;

            for (int i = 0, j = loop.Count - 1; i < loop.Count; j = i++)
            {
                GeoPoint2 a = loop[i];
                GeoPoint2 b = loop[j];

                if ((a.Y > point.Y) != (b.Y > point.Y) &&
                    point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        /// <summary>
        /// Sorts resolved loops into outer boundaries, largest first, each with the holes directly inside it.
        /// Loops are expected with their region on the left, so an outer boundary has a positive area and a hole
        /// a negative one.
        /// </summary>
        public static List<LoopGroup> Group(IEnumerable<List<GeoPoint2>> loops)
        {
            List<LoopGroup> groups = new List<LoopGroup>();
            List<List<GeoPoint2>> holes = new List<List<GeoPoint2>>();

            foreach (List<GeoPoint2> loop in loops)
            {
                double area = SignedArea(loop);

                if (area > 0.0)
                {
                    groups.Add(new LoopGroup(loop, area));
                }
                else if (area < 0.0)
                {
                    holes.Add(loop);
                }
            }

            groups.Sort((a, b) => b.Area.CompareTo(a.Area));

            foreach (List<GeoPoint2> hole in holes)
            {
                LoopGroup owner = null;

                // A hole belongs to the smallest outer boundary around it. Outer and hole can only touch at
                // points, never along an edge, so the middle of a hole edge lies strictly inside its owner.
                for (int g = groups.Count - 1; g >= 0 && owner == null; g--)
                {
                    for (int e = 0; e < hole.Count; e++)
                    {
                        GeoPoint2 sample = PlanarMath.Midpoint(hole[e], hole[(e + 1) % hole.Count]);

                        if (Contains(groups[g].Outer, sample))
                        {
                            owner = groups[g];
                            break;
                        }
                    }
                }

                owner?.Holes.Add(hole);
            }

            return groups;
        }
    }
}
