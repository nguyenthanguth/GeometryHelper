using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;

namespace GeometryHelper.UnitTest.Packing
{
    /// <summary>
    /// The views of a drawing as VPCLIP leaves them, built in code for the polygon packing tests: boxes, L- and
    /// U-shaped clips and convex cuts, all in millimetres and all far from the sheets, as views are before they are
    /// packed.
    /// </summary>
    internal static class PolygonKit
    {
        /// <summary>A box, by its lower left corner and its size.</summary>
        internal static GeoPolygon2 Box(double x, double y, double width, double height) =>
            new GeoPolygon2(
                new GeoPoint2(x, y),
                new GeoPoint2(x + width, y),
                new GeoPoint2(x + width, y + height),
                new GeoPoint2(x, y + height)
            );

        /// <summary>
        /// An upside-down L: a column on the left, a bar across the top, the lower right of its box left open. At a size
        /// of 100 with an arm of 30, a second one fits the open corner of the first when it stands 30 to the right and
        /// 30 lower, so that two of them take 130 by 130 rather than 200 by 100.
        /// </summary>
        internal static GeoPolygon2 Gamma(double x, double y, double size = 100.0, double arm = 30.0) =>
            new GeoPolygon2(
                new GeoPoint2(x, y),
                new GeoPoint2(x + arm, y),
                new GeoPoint2(x + arm, y + size - arm),
                new GeoPoint2(x + size, y + size - arm),
                new GeoPoint2(x + size, y + size),
                new GeoPoint2(x, y + size)
            );

        /// <summary>An L: a column on the left and a bar along the bottom, the upper right of its box left open.</summary>
        internal static GeoPolygon2 El(double x, double y, double size = 100.0, double arm = 30.0) =>
            new GeoPolygon2(
                new GeoPoint2(x, y),
                new GeoPoint2(x + size, y),
                new GeoPoint2(x + size, y + arm),
                new GeoPoint2(x + arm, y + arm),
                new GeoPoint2(x + arm, y + size),
                new GeoPoint2(x, y + size)
            );

        /// <summary>A U: two columns joined along the bottom, the top of its box left open between them.</summary>
        internal static GeoPolygon2 U(double x, double y, double width, double height, double arm) =>
            new GeoPolygon2(
                new GeoPoint2(x, y),
                new GeoPoint2(x + width, y),
                new GeoPoint2(x + width, y + height),
                new GeoPoint2(x + width - arm, y + height),
                new GeoPoint2(x + width - arm, y + arm),
                new GeoPoint2(x + arm, y + arm),
                new GeoPoint2(x + arm, y + height),
                new GeoPoint2(x, y + height)
            );

        /// <summary>A hexagon with its two left and right corners pulled in: convex, and not a box.</summary>
        internal static GeoPolygon2 Hexagon(double x, double y, double width, double height) =>
            new GeoPolygon2(
                new GeoPoint2(x + width * 0.25, y),
                new GeoPoint2(x + width * 0.75, y),
                new GeoPoint2(x + width, y + height * 0.5),
                new GeoPoint2(x + width * 0.75, y + height),
                new GeoPoint2(x + width * 0.25, y + height),
                new GeoPoint2(x, y + height * 0.5)
            );

        /// <summary>The least corner and the size of the upright box round a polygon.</summary>
        internal static (double MinX, double MinY, double MaxX, double MaxY) Bounds(GeoPolygon2 polygon)
        {
            double minX = polygon.Vertices.Min(v => v.X),
                minY = polygon.Vertices.Min(v => v.Y);
            double maxX = polygon.Vertices.Max(v => v.X),
                maxY = polygon.Vertices.Max(v => v.Y);
            return (minX, minY, maxX, maxY);
        }

        /// <summary>
        /// Views of the kinds a drawing has, drawn from a seed: boxes, L-, U- and Γ-shaped clips and convex cuts up to a
        /// side of <paramref name="side"/>, one to a group or up to <paramref name="groupSize"/>.
        /// </summary>
        internal static List<GeoPolygon2[]> Views(int seed, int count, double side, int groupSize = 1)
        {
            var random = new Random(seed);
            var groups = new List<GeoPolygon2[]>();
            int made = 0;
            while (made < count)
            {
                int members = Math.Min(count - made, 1 + random.Next(groupSize));
                var group = new GeoPolygon2[members];
                for (int m = 0; m < members; m++)
                {
                    double width = Math.Round(side * (0.25 + 0.75 * random.NextDouble()));
                    double height = Math.Round(side * (0.25 + 0.75 * random.NextDouble()));
                    double arm = Math.Round(Math.Min(width, height) * (0.25 + 0.2 * random.NextDouble()));
                    double x = Math.Round(random.NextDouble() * 4000.0 - 2000.0);
                    double y = Math.Round(random.NextDouble() * 4000.0 - 2000.0);
                    switch (random.Next(5))
                    {
                        case 0:
                            group[m] = Box(x, y, width, height);
                            break;
                        case 1:
                            group[m] = El(x, y, Math.Min(width, height), arm);
                            break;
                        case 2:
                            group[m] = U(x, y, width, height, arm);
                            break;
                        case 3:
                            group[m] = Gamma(x, y, Math.Min(width, height), arm);
                            break;
                        default:
                            group[m] = Hexagon(x, y, width, height);
                            break;
                    }
                }

                groups.Add(group);
                made += members;
            }

            return groups;
        }
    }
}
