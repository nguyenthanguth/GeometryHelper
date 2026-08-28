using System;
using System.Collections.Generic;
using GeometryHelper.CommonGeometry;
using GeometryHelper.SolidGeometry.Core;
using GeometryHelper.SolidGeometry.Geometry;
using Xunit;

namespace GeometryHelper.SolidGeometry.UnitTest.Core
{
    /// <summary>
    /// Accounting checks on the boolean operations and on the merge that finishes them.
    /// <para>
    /// A body combined with another must account for every bit of what went in. The failure these guard
    /// against is silent: the result closes, reports itself closed, and is simply the wrong size, because
    /// the outline it was rebuilt from enclosed the wrong region.
    /// </para>
    /// </summary>
    public class BooleanConservationTests
    {
        private static GeoSolid3 Prism(GeoPoint3[] profile, double height)
        {
            List<GeoFace3> faces = new List<GeoFace3>();
            GeoPoint3[] top = new GeoPoint3[profile.Length];
            GeoPoint3[] bottomReversed = new GeoPoint3[profile.Length];

            for (int i = 0; i < profile.Length; i++)
            {
                top[i] = new GeoPoint3(profile[i].X, profile[i].Y, profile[i].Z + height);
                bottomReversed[i] = profile[profile.Length - 1 - i];
            }

            faces.Add(new GeoFace3(new GeoPolygon3(bottomReversed)));
            faces.Add(new GeoFace3(new GeoPolygon3(top)));

            for (int i = 0; i < profile.Length; i++)
            {
                int j = (i + 1) % profile.Length;
                faces.Add(new GeoFace3(new GeoPolygon3(profile[i], profile[j], top[j], top[i])));
            }

            return new GeoSolid3(faces);
        }

        /// <summary>Star-shaped about its centre, so it is always simple however the radii fall.</summary>
        private static GeoPoint3[] Star(Random rng, int sides)
        {
            GeoPoint3[] points = new GeoPoint3[sides];

            for (int i = 0; i < sides; i++)
            {
                double angle = 2.0 * Math.PI * i / sides;
                double radius = 2.0 + rng.NextDouble() * 6.0;

                // Rounded so that vertices land on one another and on the cutting planes, which is where
                // the awkward cases live.
                points[i] = new GeoPoint3(Math.Round(radius * Math.Cos(angle)), Math.Round(radius * Math.Sin(angle)), 0);
            }

            return points;
        }

        [Fact]
        public void UnionAndIntersectionAccountForBothBodies()
        {
            Random rng = new Random(777);
            int cases = 0;
            double worst = 0.0;

            for (int t = 0; t < 150; t++)
            {
                GeoSolid3 a = Prism(Star(rng, 4 + rng.Next(4)), 4.0);

                double dx = rng.Next(-6, 7);
                double dy = rng.Next(-6, 7);
                List<GeoPoint3> moved = new List<GeoPoint3>();
                foreach (GeoPoint3 p in Star(rng, 4 + rng.Next(4)))
                {
                    moved.Add(new GeoPoint3(p.X + dx, p.Y + dy, 1));
                }

                GeoSolid3 b;
                try { b = Prism(moved.ToArray(), 4.0); }
                catch (ArgumentException) { continue; }

                if (!Boolean3.TryUnion(a, b, out GeoSolid3 union)) { continue; }

                double shared = Boolean3.TryIntersect(a, b, out GeoSolid3 both) ? both.Volume : 0.0;
                double aOnly = Boolean3.TrySubtract(a, b, out GeoSolid3 left) ? left.Volume : 0.0;
                double bOnly = Boolean3.TrySubtract(b, a, out GeoSolid3 right) ? right.Volume : 0.0;

                cases++;

                // |A| + |B| = |A union B| + |A intersect B|, and each body is its own share plus what it
                // keeps to itself.
                worst = Math.Max(worst, Math.Abs(a.Volume + b.Volume - union.Volume - shared));
                worst = Math.Max(worst, Math.Abs(aOnly + shared - a.Volume));
                worst = Math.Max(worst, Math.Abs(bOnly + shared - b.Volume));
            }

            Assert.True(cases > 100, $"only {cases} usable cases");

            // Slivers thinner than the tolerance are dropped by design, so the accounting is allowed to
            // move at that scale and no further. Before the outline walk was taught to handle a vertex
            // several edges meet at, this reached 13 on a body of 336.
            Assert.True(worst < 1E-3, $"worst drift was {worst}");
        }

        [Fact]
        public void SubtractingFromABodyWithACavityKeepsTheCavity()
        {
            GeoSolid3 slab = Prism(new[]
            {
                new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0),
                new GeoPoint3(10, 10, 0), new GeoPoint3(0, 10, 0)
            }, 4.0);

            GeoSolid3 first = Prism(new[]
            {
                new GeoPoint3(1, 1, 1), new GeoPoint3(3, 1, 1),
                new GeoPoint3(3, 3, 1), new GeoPoint3(1, 3, 1)
            }, 2.0);

            GeoSolid3 second = Prism(new[]
            {
                new GeoPoint3(5, 5, 0.5), new GeoPoint3(7, 5, 0.5),
                new GeoPoint3(7, 7, 0.5), new GeoPoint3(5, 7, 0.5)
            }, 3.0);

            Assert.True(Boolean3.TrySubtract(slab, first, out GeoSolid3 once));
            Assert.Equal(392.0, once.Volume, 9);

            Assert.True(Boolean3.TrySubtract(once, second, out GeoSolid3 twice));
            Assert.Equal(380.0, twice.Volume, 9);
            Assert.True(twice.IsClosed());
        }

        [Fact]
        public void MergingCoplanarFacesNeverChangesTheirTotalArea()
        {
            GeoFace3 Rect(double x0, double y0, double x1, double y1) =>
                new GeoFace3(new GeoPolygon3(
                    new GeoPoint3(x0, y0, 0), new GeoPoint3(x1, y0, 0),
                    new GeoPoint3(x1, y1, 0), new GeoPoint3(x0, y1, 0)));

            GeoFace3[][ ] arrangements =
            {
                new[] { Rect(0, 0, 2, 2), Rect(2, 0, 4, 2) },                                  // shared edge
                new[] { Rect(0, 0, 2, 2), Rect(2, 0, 4, 1), Rect(2, 1, 4, 2) },                // T-junction
                new[] { Rect(0, 0, 2, 1), Rect(0, 1, 2, 2), Rect(2, 0, 4, 2) },                // T the other way
                new[] { Rect(0, 0, 2, 2), Rect(10, 10, 12, 12) },                              // two islands
                new[] { Rect(0, 0, 2, 2), Rect(2, 2, 4, 4) },                                  // one shared corner
                new[] { Rect(0, 0, 2, 2), Rect(2, 0, 4, 2), Rect(0, 2, 2, 4) },                // an L
                new[] { Rect(0, 0, 4, 1), Rect(0, 3, 4, 4), Rect(0, 1, 1, 3), Rect(3, 1, 4, 3) }, // a ring
                new[] { Rect(0, 0, 2, 2), Rect(2, 0, 4, 1), Rect(2, 1, 3, 2), Rect(3, 1, 4, 2) }  // a staircase
            };

            foreach (GeoFace3[] arrangement in arrangements)
            {
                double before = 0.0;
                foreach (GeoFace3 f in arrangement) { before += f.Area; }

                double after = 0.0;
                foreach (GeoFace3 f in Merge3.CoplanarFaces(arrangement, Tolerance.Global)) { after += f.Area; }

                Assert.Equal(before, after, 9);
            }
        }
    }
}
