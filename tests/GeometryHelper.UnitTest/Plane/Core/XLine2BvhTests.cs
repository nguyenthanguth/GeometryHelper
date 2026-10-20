using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using GeometryHelper.Spatial;
using Xunit;
using static GeometryHelper.UnitTest.Plane.Ray2Shapes;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The crossings of an infinite line with the edges a plane hierarchy holds (XL-5): the line runs both ways from its
    /// origin, so wherever the origin stands, even past the shape, the answer is the same.
    /// </summary>
    /// <remarks>
    /// The hierarchy's answers are held to two others: the shape's own crossings with the line, and the hierarchy's
    /// crossings with the segment cut from the line by hand, from the least to the greatest reach of the corners of
    /// <see cref="GeoBvh2.Bounds"/> along the line, widened by max(1, 100 times the point tolerance) at each end.
    /// </remarks>
    public class XLine2BvhTests
    {
        private static readonly Tolerance Strict = new Tolerance(1E-3, 1E-5);

        private static void AssertSamePoints(GeoPoint2[] expected, GeoPoint2[] actual, double within = 1E-9)
        {
            Assert.Equal(expected.Length, actual.Length);

            foreach (GeoPoint2 point in expected)
            {
                Assert.Contains(actual, p => p.DistanceTo(point) <= within);
            }
        }

        /// <summary>The segment cut by hand from the line against the hierarchy's box.</summary>
        private static GeoLine2 Cut(GeoXLine2 xline, GeoBvh2 index, Tolerance tolerance)
        {
            GeoVector2 unit = xline.Direction;
            double[] along = index.Bounds.GetVertices().Select(c => (c.X - xline.Origin.X) * unit.X + (c.Y - xline.Origin.Y) * unit.Y).ToArray();
            double margin = Math.Max(1.0, 100 * tolerance.EqualPoint);
            double from = along.Min() - margin;
            double to = along.Max() + margin;

            return new GeoLine2(
                new GeoPoint2(xline.Origin.X + from * unit.X, xline.Origin.Y + from * unit.Y),
                new GeoPoint2(xline.Origin.X + to * unit.X, xline.Origin.Y + to * unit.Y));
        }

        [Fact]
        public void ALineAlongTheMiddleOfTheSquare_CrossesItsTwoSidesFromAnyOriginAlongIt()
        {
            GeoBvh2 index = GeoBvh2.FromPolygon(Square());
            GeoPoint2[] expected = { new GeoPoint2(0, 50), new GeoPoint2(100, 50) };

            // From before the square, from inside it and from well past it: one line, one answer.
            foreach (double x in new[] { -50.0, 50.0, 300.0, -4000.0 })
            {
                var xline = new GeoXLine2(new GeoPoint2(x, 50), GeoVector2.XAxis);

                AssertSamePoints(expected, index.GetIntersections(xline, Strict));
                AssertSamePoints(expected, index.GetIntersections(xline.Reverse(), Strict));
            }
        }

        [Fact]
        public void ARayPointingAway_CrossesNothingButTheLineItIsOnCrossesBothSides()
        {
            GeoBvh2 index = GeoBvh2.FromPolygon(Square());
            var ray = new GeoRay2(new GeoPoint2(300, 50), GeoVector2.XAxis);

            Assert.Empty(index.GetIntersections(ray, Strict));
            Assert.Equal(2, index.GetIntersections(new GeoXLine2(ray.Origin, ray.Direction), Strict).Length);
        }

        [Fact]
        public void ALineAcrossACogFromInsideIt_CrossesWhereTheCogItselfAndTheCutSegmentSay()
        {
            // Twenty-one lines through (3, -2), inside the cog of 24 corners reaching 100, at angles that pass no corner.
            GeoPolygonArc2 cog = Cog(24, 100);
            GeoBvh2 index = GeoBvh2.FromPolygonArc(cog);

            for (int k = 0; k < 21; k++)
            {
                double angle = 0.05 + k * 0.29;
                var xline = new GeoXLine2(new GeoPoint2(3, -2), new GeoVector2(Math.Cos(angle), Math.Sin(angle)));

                GeoPoint2[] found = index.GetIntersections(xline, Strict);

                Assert.True(found.Length >= 2);
                AssertSamePoints(cog.GetIntersections(xline, Strict), found);
                AssertSamePoints(index.GetIntersections(Cut(xline, index, Strict), Strict), found);
            }
        }

        [Fact]
        public void ALineIsTheTwoRaysFromItsOrigin_TheOriginsOwnPointCountedOnce()
        {
            GeoPolygonArc2 cog = Cog(18, 100);
            GeoBvh2 index = GeoBvh2.FromPolygonArc(cog);

            for (int k = 0; k < 15; k++)
            {
                double angle = 0.11 + k * 0.41;
                var xline = new GeoXLine2(new GeoPoint2(-3, 8), new GeoVector2(Math.Cos(angle), Math.Sin(angle)));

                GeoPoint2[] forward = index.GetIntersections(xline.ToRay(), Strict);
                GeoPoint2[] backward = index.GetIntersections(xline.Reverse().ToRay(), Strict);
                GeoPoint2[] both = index.GetIntersections(xline, Strict);

                Assert.Equal(forward.Length + backward.Length, both.Length);

                foreach (GeoPoint2 point in forward.Concat(backward))
                {
                    Assert.Contains(both, p => p.DistanceTo(point) <= 1E-9);
                }
            }
        }

        [Fact]
        public void ALineThroughACogFarAlongIt_StillReachesBothOfItsCrossings_WhichSideTheOriginIsOn()
        {
            // The cog moved 5000 along X: the cut reaches past its box, so both crossings at y = 0.5 are found, with the
            // origin before the cog, inside its box, and beyond it.
            GeoPolygonArc2 cog = Cog(24, 100).Translate(new GeoVector2(5000, 0));
            GeoBvh2 index = GeoBvh2.FromPolygonArc(cog);

            foreach (double x in new[] { 0.0, 5000.0, 12000.0 })
            {
                var xline = new GeoXLine2(new GeoPoint2(x, 0.5), GeoVector2.XAxis);
                GeoPoint2[] found = index.GetIntersections(xline, Strict);

                Assert.Equal(2, found.Length);
                AssertSamePoints(cog.GetIntersections(xline, Strict), found);
            }
        }

        [Fact]
        public void TheLinesOfAPlateAndAnL_CrossThemWhereTheShapesThemselvesDo()
        {
            var across = new GeoXLine2(new GeoPoint2(-50, 50), GeoVector2.XAxis);
            var slant = new GeoXLine2(new GeoPoint2(20, -50), new GeoVector2(1, 1));

            AssertSamePoints(Plate().GetIntersections(across, Strict), GeoBvh2.FromFace(Plate()).GetIntersections(across, Strict));
            Assert.Equal(4, GeoBvh2.FromFace(Plate()).GetIntersections(across, Strict).Length);
            AssertSamePoints(Ell().GetIntersections(slant, Strict), GeoBvh2.FromPolyline(Ell()).GetIntersections(slant, Strict));
            AssertSamePoints(Hook().GetIntersections(across, Strict), GeoBvh2.FromPolylineArc(Hook()).GetIntersections(across, Strict));
        }

        [Fact]
        public void ALineAlongASide_CrossesNothingFromEitherSide_AsTheSegmentAlongItDoesNot()
        {
            // The bottom of the square, y = 0: the corners are touched, the same as the shape says.
            GeoBvh2 index = GeoBvh2.FromPolygon(Square());
            var xline = new GeoXLine2(new GeoPoint2(40, 0), GeoVector2.XAxis);

            AssertSamePoints(Square().GetIntersections(xline, Strict), index.GetIntersections(xline, Strict));
            AssertSamePoints(index.GetIntersections(Cut(xline, index, Strict), Strict), index.GetIntersections(xline, Strict));
        }

        /// <summary>The plain scan: every edge asked the line, the points of one that lie within the tolerance of a kept one dropped.</summary>
        private static GeoPoint2[] Scan(System.Collections.Generic.IEnumerable<GeoEdge2> edges, GeoXLine2 xline, Tolerance tolerance)
        {
            var found = new System.Collections.Generic.List<GeoPoint2>();

            foreach (GeoEdge2 edge in edges)
            {
                foreach (GeoPoint2 p in edge.GetIntersections(xline, tolerance))
                {
                    if (!found.Any(k => k.IsEqualTo(p, tolerance)))
                    {
                        found.Add(p);
                    }
                }
            }

            return found.ToArray();
        }

        private static void AssertSameAsScan(GeoEdge2[] edges, GeoXLine2 xline, string what)
        {
            GeoPoint2[] scanned = Scan(edges, xline, Strict);
            GeoPoint2[] found = new GeoBvh2(edges).GetIntersections(xline, Strict);

            Assert.True(
                scanned.All(p => found.Any(q => q.DistanceTo(p) <= 1E-9)) && found.All(q => scanned.Any(p => p.DistanceTo(q) <= 1E-9)),
                what + ": scan " + scanned.Length + ", index " + found.Length);
        }

        [Fact]
        public void ANearParallelEdge_IsJudgedByItsOwnLength_WhateverElseIsIndexed()
        {
            // The edge draws 0.0006 off the line along 100: parallel at the point tolerance 0.001, so no crossing, with or without a far edge.
            var edge = new GeoEdge2(new GeoPoint2(0, 50.0008), new GeoPoint2(100, 50.0002));
            var far = new GeoEdge2(new GeoPoint2(-900, -900), new GeoPoint2(-899, -900));
            var xline = new GeoXLine2(new GeoPoint2(200, 50.0007), GeoVector2.XAxis);

            Assert.Empty(edge.GetIntersections(xline, Strict));
            Assert.Empty(new GeoBvh2(new[] { edge }).GetIntersections(xline, Strict));
            Assert.Empty(new GeoBvh2(new[] { edge, far }).GetIntersections(xline, Strict));
        }

        [Fact]
        public void ManyLinesAmongNearParallelAndFarEdges_GiveTheScansAnswer_WithNoDifference()
        {
            var random = new Random(20261022);
            int differences = 0;
            int crossings = 0;

            for (int k = 0; k < 3000; k++)
            {
                double height = random.NextDouble() * 100 - 50;
                var edges = new System.Collections.Generic.List<GeoEdge2>();

                // Edges lying along the line, some a hair off it at either end, some curved, and some far from it.
                for (int e = 0; e < 1 + random.Next(12); e++)
                {
                    double x = random.NextDouble() * 400 - 200;
                    double length = random.NextDouble() * 150 + 0.5;
                    bool away = random.Next(4) == 0;

                    // Both ends within 0.0015 of the line, so that how far they draw apart along the edge is near the point tolerance.
                    double offset = away ? random.NextDouble() * 300 - 150 : (random.NextDouble() - 0.5) * 0.003;
                    double rise = away ? (random.NextDouble() - 0.5) * 100 : (random.NextDouble() - 0.5) * 0.003 - offset;
                    var from = new GeoPoint2(x, height + offset);
                    var to = new GeoPoint2(x + length, height + offset + rise);
                    edges.Add(random.Next(5) == 0 ? new GeoEdge2(from, to, random.NextDouble() * 0.02 - 0.01) : new GeoEdge2(from, to));
                }

                var xline = new GeoXLine2(new GeoPoint2(random.NextDouble() * 800 - 400, height + (random.NextDouble() - 0.5) * 0.002), new GeoVector2(1, (random.NextDouble() - 0.5) * 0.0005));

                GeoPoint2[] scanned = Scan(edges, xline, Strict);
                GeoPoint2[] found = new GeoBvh2(edges).GetIntersections(xline, Strict);

                if (!(scanned.All(p => found.Any(q => q.DistanceTo(p) <= 1E-9)) && found.All(q => scanned.Any(p => p.DistanceTo(q) <= 1E-9))))
                {
                    differences++;
                }

                crossings += scanned.Length > 0 ? 1 : 0;
            }

            Assert.Equal(0, differences);
            Assert.InRange(crossings, 300, 3000);
        }

        [Fact]
        public void ADiagonalLineThroughAGridOfEdges_GivesTheScansAnswer()
        {
            var edges = new System.Collections.Generic.List<GeoEdge2>();

            for (int i = 0; i < 30; i++)
            {
                for (int j = 0; j < 30; j++)
                {
                    edges.Add(new GeoEdge2(new GeoPoint2(i * 10, j * 10), new GeoPoint2(i * 10 + 8, j * 10 + 1)));
                    edges.Add(new GeoEdge2(new GeoPoint2(i * 10 + 8, j * 10 + 1), new GeoPoint2(i * 10 + 7, j * 10 + 8)));
                }
            }

            foreach (GeoXLine2 xline in new[] { new GeoXLine2(new GeoPoint2(0, 0), new GeoVector2(1, 1)), new GeoXLine2(new GeoPoint2(150.5, 3.3), new GeoVector2(1, 0.31)), new GeoXLine2(new GeoPoint2(3, 3), GeoVector2.YAxis) })
            {
                AssertSameAsScan(edges.ToArray(), xline, "grid");
            }
        }

        [Fact]
        public void AnEmptyHierarchy_IsCrossedByNoLine()
        {
            var empty = new GeoBvh2(new GeoEdge2[0]);
            var xline = new GeoXLine2(new GeoPoint2(0, 0), GeoVector2.XAxis);

            Assert.Empty(empty.GetIntersections(xline, Strict));
            Assert.Empty(empty.GetIntersections(xline));
        }

        [Fact]
        public void ALineMissingTheShape_CrossesNothing()
        {
            GeoBvh2 index = GeoBvh2.FromPolygon(Square());

            Assert.Empty(index.GetIntersections(new GeoXLine2(new GeoPoint2(0, 150), GeoVector2.XAxis), Strict));
            Assert.Empty(index.GetIntersections(new GeoXLine2(new GeoPoint2(-50, -50), new GeoVector2(1, -1)), Strict));
        }

        [Fact]
        public void TheHierarchysFormWithoutATolerance_AnswersAsTheGlobalToleranceDoes()
        {
            GeoBvh2 index = GeoBvh2.FromPolygonArc(Cog(24, 100));

            foreach (GeoXLine2 xline in new[] { new GeoXLine2(new GeoPoint2(3, -2), new GeoVector2(1, 2)), new GeoXLine2(new GeoPoint2(-300, 7), GeoVector2.XAxis) })
            {
                Assert.Equal(index.GetIntersections(xline, Tolerance.Global), index.GetIntersections(xline));
            }
        }
    }
}
