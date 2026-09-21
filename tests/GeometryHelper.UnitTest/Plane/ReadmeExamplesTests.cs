using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;
using GeometryHelper.Extension;
using GeometryHelper.Geometry;
using GeometryHelper.Spatial;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// Every code sample in docs/plane.md, compiled and run. A README that drifts from the
    /// API is worse than no README, so the samples are kept here rather than only in the document.
    /// </summary>
    public class ReadmeExamplesTests
    {
        [Fact]
        public void GeometricTypes_CurveVersusRegion()
        {
            var traced = new GeoPolyline2(
                new GeoPoint2(0, 0), new GeoPoint2(10, 0),
                new GeoPoint2(10, 10), new GeoPoint2(0, 10), new GeoPoint2(0, 0));

            // Exactly the four results quoted in the README table.
            Assert.Equal(PointLocation.OutSide, traced.Locate(new GeoPoint2(5, 5)));
            Assert.Equal(5.0, traced.DistanceTo(new GeoPoint2(5, 5)), 9);
            Assert.Equal(PointLocation.Inside, traced.ToPolygon().Locate(new GeoPoint2(5, 5)));
            Assert.Equal(0.0, traced.ToPolygon().DistanceTo(new GeoPoint2(5, 5)), 9);
        }

        [Fact]
        public void GeometricTypes_OnlyRegionsOfferContains()
        {
            // The README says Contains belongs to regions and Locate to everything. Curves reach Locate
            // and IsPointOn only, which is why the polyline lines below have no Contains counterpart.
            var poly = new GeoPolygon2(
                new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10), new GeoPoint2(0, 10));
            var rect = new GeoRectangle2(new GeoPoint2(5, 5), 10, 10);
            var circle = new GeoCircle2(new GeoPoint2(5, 5), 5);
            var polyline = new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10));
            var line = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 0));

            var centre = new GeoPoint2(5, 5);
            Assert.True(poly.Contains(centre));
            Assert.True(rect.Contains(centre));
            Assert.True(circle.Contains(centre));

            var onPath = new GeoPoint2(5, 0);
            Assert.Equal(PointLocation.OnSide, polyline.Locate(onPath));
            Assert.Equal(PointLocation.OnSide, line.Locate(onPath));
            Assert.True(polyline.IsPointOn(onPath));
            Assert.True(line.IsPointOn(onPath));
        }

        [Fact]
        public void CollisionAndIntersection_EveryPairWorksFromBothDirections()
        {
            var line = new GeoLine2(new GeoPoint2(0, 5), new GeoPoint2(10, 5));
            var otherLine = new GeoLine2(new GeoPoint2(5, 0), new GeoPoint2(5, 10));
            var rect = new GeoRectangle2(new GeoPoint2(5, 5), 8, 8);
            var otherRect = new GeoRectangle2(new GeoPoint2(6, 6), 8, 8);
            var poly = new GeoPolygon2(
                new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10), new GeoPoint2(0, 10));
            var otherPoly = new GeoPolygon2(
                new GeoPoint2(5, 5), new GeoPoint2(15, 5), new GeoPoint2(15, 15), new GeoPoint2(5, 15));
            var circle = new GeoCircle2(new GeoPoint2(5, 5), 4);
            var polyline = new GeoPolyline2(new GeoPoint2(0, 5), new GeoPoint2(10, 5), new GeoPoint2(10, 10));

            // The README claims every pair is reachable from both sides and agrees either way.
            Assert.Equal(rect.CollidesWith(line), line.CollidesWith(rect));
            Assert.Equal(rect.CollidesWith(poly), poly.CollidesWith(rect));
            Assert.Equal(circle.CollidesWith(polyline), polyline.CollidesWith(circle));
            Assert.Equal(circle.CollidesWith(line), line.CollidesWith(circle));
            Assert.Equal(poly.CollidesWith(line), line.CollidesWith(poly));
            Assert.Equal(polyline.CollidesWith(poly), poly.CollidesWith(polyline));
            Assert.Equal(polyline.CollidesWith(rect), rect.CollidesWith(polyline));

            Assert.True(rect.CollidesWith(otherRect));
            Assert.True(poly.CollidesWith(otherPoly));
            Assert.True(line.CollidesWith(otherLine));

            GeoPoint2[] points = poly.GetIntersections(line);
            Assert.Equal(2, points.Length);
        }

        [Fact]
        public void Tolerance_GlobalAppliesToOverloadsThatDoNotPassOne()
        {
            Tolerance saved = Tolerance.Global;
            try
            {
                var line = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 0));
                var nearby = new GeoPoint2(5, 0.5);

                Tolerance.Global = new Tolerance(1E-4, 1E-4);
                Assert.False(line.IsPointOn(nearby));

                Tolerance.Global = new Tolerance(1.0, 1.0);
                Assert.True(line.IsPointOn(nearby));

                // An explicit tolerance always wins over the global one.
                Assert.False(line.IsPointOn(nearby, new Tolerance(1E-4, 1E-4)));
            }
            finally
            {
                Tolerance.Global = saved;
            }
        }

        [Fact]
        public void Splitting_CuttingAtAPosition()
        {
            var line = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 0));
            var polyline = new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10));
            var point = new GeoPoint2(4, 0);

            Assert.True(Splition2.TrySplitBy(line, point, out GeoLine2 first, out GeoLine2 second));
            Assert.True(Splition2.TrySplitAtDistance(polyline, 12.5, out GeoPolyline2 head, out GeoPolyline2 tail));

            GeoLine2[] pieces = Splition2.SplitAtDistances(line, new[] { 2.0, 5.0, 8.0 });

            // The first piece holds the start point and the last holds the end point.
            Assert.True(first.StartPoint.IsEqualTo(line.StartPoint));
            Assert.True(second.EndPoint.IsEqualTo(line.EndPoint));
            Assert.True(head[0].IsEqualTo(polyline[0]));
            Assert.True(tail[tail.VertexCount - 1].IsEqualTo(polyline[polyline.VertexCount - 1]));
            Assert.Equal(4, pieces.Length);
        }

        [Fact]
        public void Splitting_CuttingWithAnotherShape()
        {
            var line = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 0));
            var polyline = new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10));
            var cutter = new GeoLine2(new GeoPoint2(6, -5), new GeoPoint2(6, 5));
            var cutterA = new GeoLine2(new GeoPoint2(3, -5), new GeoPoint2(3, 5));
            var cutterB = new GeoLine2(new GeoPoint2(7, -5), new GeoPoint2(7, 5));

            Assert.True(Splition2.TrySplitBy(line, cutter, out GeoLine2 first, out GeoLine2 second));
            Assert.True(Splition2.TrySplitBy(polyline, cutter, out GeoPolyline2[] pieces));

            Assert.True(Splition2.TrySplitBy(line, new[] { cutterA, cutterB }, out GeoLine2[] byLines));
            Assert.True(Splition2.TrySplitBy(polyline, new[] { new GeoPoint2(3, 0) }, out GeoPolyline2[] byPoints));

            Assert.True(first.EndPoint.IsEqualTo(new GeoPoint2(6, 0)));
            Assert.True(second.StartPoint.IsEqualTo(new GeoPoint2(6, 0)));
            Assert.Equal(2, pieces.Length);
            Assert.Equal(3, byLines.Length);
            Assert.Equal(2, byPoints.Length);
        }

        [Fact]
        public void Splitting_AgainstAPolygonSortsBySide()
        {
            var polygon = new GeoPolygon2(
                new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10), new GeoPoint2(0, 10));
            var polygonA = polygon;
            var polygonB = new GeoPolygon2(
                new GeoPoint2(10, 0), new GeoPoint2(20, 0), new GeoPoint2(20, 10), new GeoPoint2(10, 10));

            var line = new GeoLine2(new GeoPoint2(-5, 5), new GeoPoint2(15, 5));
            var polyline = new GeoPolyline2(new GeoPoint2(-5, 5), new GeoPoint2(5, 5), new GeoPoint2(5, 15));

            Assert.True(Splition2.TrySplitBy(line, polygon, out GeoLine2[] inside, out GeoLine2[] outside));
            Assert.True(Splition2.TrySplitBy(polyline, polygon, out GeoPolyline2[] insideRuns, out GeoPolyline2[] outsideRuns));
            Assert.True(Splition2.TrySplitBy(polyline, new[] { polygonA, polygonB }, out GeoPolyline2[] within, out GeoPolyline2[] beyond));

            Assert.Single(inside);
            Assert.Equal(2, outside.Length);

            // "keeps each run whole rather than breaking it into segments": the run that bends inside the
            // polygon arrives as one GeoPolyline2 of three vertices, not as two separate segments.
            Assert.Single(insideRuns);
            Assert.Equal(3, insideRuns[0].VertexCount);
            Assert.Equal(polyline.Length, insideRuns.Sum(p => p.Length) + outsideRuns.Sum(p => p.Length), 9);
            Assert.Equal(polyline.Length, within.Sum(p => p.Length) + beyond.Sum(p => p.Length), 9);
        }

        [Fact]
        public void Splitting_IsReachableFromTheShapeBeingCut()
        {
            var line = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 0));
            var point = new GeoPoint2(4, 0);
            var polygon = new GeoPolygon2(
                new GeoPoint2(2, -5), new GeoPoint2(8, -5), new GeoPoint2(8, 5), new GeoPoint2(2, 5));
            var polyline = new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10));
            var cutter = new GeoLine2(new GeoPoint2(6, -5), new GeoPoint2(6, 5));

            Assert.True(line.TrySplitBy(point, out GeoLine2 first, out GeoLine2 second));
            Assert.True(line.TrySplitAtDistance(4.0, out first, out second));
            Assert.True(line.TrySplitBy(polygon, out GeoLine2[] inside, out GeoLine2[] outside));
            GeoLine2[] pieces = line.SplitAtDistances(new[] { 2.0, 5.0, 8.0 });

            Assert.True(polyline.TrySplitBy(cutter, out GeoPolyline2[] parts));
            Assert.True(polyline.TrySplitBy(polygon, out GeoPolyline2[] insideRuns, out GeoPolyline2[] outsideRuns));

            Assert.Single(inside);
            Assert.Equal(2, outside.Length);
            Assert.Equal(4, pieces.Length);
            Assert.Equal(2, parts.Length);
            Assert.Equal(polyline.Length, insideRuns.Sum(p => p.Length) + outsideRuns.Sum(p => p.Length), 9);
        }

        [Fact]
        public void Splitting_WhatTheReturnValueMeans()
        {
            // "false says nothing was cut, not that the call failed. The out parameters are always
            // usable: an array form hands back the subject as a single piece, and a polygon form puts it
            // in whichever of the two arrays matches the side it lies on, leaving the other empty."
            var polyline = new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(10, 0));
            var missing = new GeoLine2(new GeoPoint2(50, -5), new GeoPoint2(50, 5));
            var farAway = new GeoPolygon2(
                new GeoPoint2(50, 50), new GeoPoint2(60, 50), new GeoPoint2(60, 60), new GeoPoint2(50, 60));

            Assert.False(polyline.TrySplitBy(missing, out GeoPolyline2[] pieces));
            Assert.Single(pieces);
            Assert.Equal(polyline.Length, pieces[0].Length, 9);

            Assert.False(polyline.TrySplitBy(farAway, out GeoPolyline2[] inside, out GeoPolyline2[] outside));
            Assert.Empty(inside);
            Assert.Single(outside);
        }

        [Fact]
        public void Splitting_WhatGetsSkipped()
        {
            var tolerance = new Tolerance(1E-4, 1E-4);
            var polyline = new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10));

            // "Cut positions outside the subject, or landing on one of its endpoints, are not splits."
            GeoPolyline2[] skipped = Splition2.SplitAtDistances(
                polyline, new[] { -5.0, 0.0, 20.0, 50.0 }, tolerance);
            Assert.Single(skipped);

            // "Positions closer together than the tolerance merge into one."
            GeoPolyline2[] merged = Splition2.SplitAtDistances(
                polyline, new[] { 5.0, 5.0 + tolerance.EqualPoint * 0.5 }, tolerance);
            Assert.Equal(2, merged.Length);

            // "A position within a tolerance of an existing vertex snaps onto it, so no piece and no
            // edge is ever shorter than the tolerance."
            GeoPolyline2[] snapped = Splition2.SplitAtDistances(
                polyline, new[] { 10.0 + tolerance.EqualPoint * 0.5 }, tolerance);
            Assert.Equal(2, snapped.Length);
            foreach (GeoPolyline2 piece in snapped)
            {
                Assert.True(piece.Length > tolerance.EqualPoint);
                for (int e = 0; e < piece.EdgeCount; e++)
                {
                    Assert.True(piece.GetEdgeAt(e).Length > tolerance.EqualPoint);
                }
            }

            // "A point that does not lie on the subject is refused rather than projected onto it."
            var offPath = new GeoPoint2(5, 3);
            Assert.False(polyline.TrySplitBy(offPath, out GeoPolyline2 _, out GeoPolyline2 _, tolerance));
            Assert.False(polyline.TrySplitBy(new[] { offPath }, out GeoPolyline2[] byPoints, tolerance));
            Assert.Single(byPoints);
        }

        [Fact]
        public void Splitting_AgainstAPolygonBoundaryCases()
        {
            var tolerance = new Tolerance(1E-4, 1E-4);
            var square = new GeoPolygon2(
                new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10), new GeoPoint2(0, 10));

            // "A part running along the boundary counts as inside, matching Contains."
            var alongEdge = new GeoLine2(new GeoPoint2(2, 0), new GeoPoint2(8, 0));
            alongEdge.TrySplitBy(square, out GeoLine2[] onEdge, out GeoLine2[] off, tolerance);
            Assert.Single(onEdge);
            Assert.Empty(off);

            // "A path that merely touches the boundary and turns back has not crossed it, so it comes
            // back whole instead of split in two at the touch."
            var graze = new GeoLine2(new GeoPoint2(5, 15), new GeoPoint2(15, 5));
            Assert.False(graze.TrySplitBy(square, out GeoLine2[] grazeIn, out GeoLine2[] grazeOut, tolerance));
            Assert.Empty(grazeIn);
            Assert.Single(grazeOut);
            Assert.Equal(graze.Length, grazeOut[0].Length, 9);
        }
        [Fact]
        public void PointChains_ThinThenChain()
        {
            var traced = new List<GeoPoint2>
            {
                new GeoPoint2(0, 0), new GeoPoint2(0.0001, 0), new GeoPoint2(5, 0), new GeoPoint2(5, 5),
            };

            // "The second point is a hair away from the first and goes." - 3 points.
            List<GeoPoint2> thinned = traced.RemoveConsecutiveNearPoints(new Tolerance(0.001, 0.001));
            Assert.Equal(3, thinned.Count);

            // "One segment per consecutive pair." - 2 segments.
            List<GeoLine2> segments = thinned.ToGeoLine2s();
            Assert.Equal(2, segments.Count);

            // "no two points of the result are coincident within the tolerance"
            var tolerance = new Tolerance(0.001, 0.001);
            for (int i = 1; i < thinned.Count; i++)
            {
                Assert.False(thinned[i - 1].IsEqualTo(thinned[i], tolerance));
            }

            // "leaves the chain open - nothing joins the last point back to the first"
            Assert.Equal(thinned.Count - 1, segments.Count);
        }

        [Fact]
        public void ExtendingAndTrimming_EverySampleHolds()
        {
            var beam = new GeoLine2(0, 0, 10, 0);

            Assert.Equal(new GeoLine2(0, 0, 15, 0), beam.Extend(5.0, LineEnd.End));
            Assert.Equal(new GeoLine2(-2, 0, 13, 0), beam.Extend(2.0, 3.0));
            Assert.Equal(new GeoLine2(0, 0, 8, 0), beam.ExtendToLength(8.0, LineEnd.End));

            var wall = new GeoLine2(20, -5, 20, 5);
            Assert.True(beam.TryExtendTo(wall, LineEnd.End, out GeoLine2 reached));
            Assert.Equal(new GeoLine2(0, 0, 20, 0), reached);
            Assert.True(new GeoLine2(0, 0, 30, 0).TryTrimTo(wall, LineEnd.End, out GeoLine2 cut));
            Assert.Equal(new GeoLine2(0, 0, 20, 0), cut);

            // "each segment keeps its longer part and ends at the corner"
            Assert.True(new GeoLine2(0, 0, 8, 0).TryTrimExtendToCorner(new GeoLine2(10, 2, 10, 10), out GeoLine2 a, out GeoLine2 b));
            Assert.Equal(new GeoLine2(0, 0, 10, 0), a);
            Assert.Equal(new GeoLine2(10, 0, 10, 10), b);

            Assert.True(new GeoLine2(0, 0, 1, 1).TryIntersectWith(new GeoLine2(10, 0, 11, -1), LineExtension.Both, out GeoPoint2 x));
            Assert.True(x.IsEqualTo(new GeoPoint2(5, 5)));

            // "fits an end to a boundary whichever side of it the end starts on"
            foreach (GeoLine2 line in new[] { new GeoLine2(0, 0, 12, 0), new GeoLine2(0, 0, 27, 0) })
            {
                Assert.True(line.TryExtendTo(wall, LineEnd.End, out GeoLine2 fit) || line.TryTrimTo(wall, LineEnd.End, out fit));
                Assert.Equal(new GeoLine2(0, 0, 20, 0), fit);
            }
        }

        [Fact]
        public void Offsetting_EverySampleHolds()
        {
            Assert.Equal(new GeoLine2(0, 2, 10, 2), new GeoLine2(0, 0, 10, 0).Offset(2.0));
            Assert.Equal(new GeoLine2(0, 7, 10, 7), new GeoLine2(0, 0, 10, 0).OffsetThrough(new GeoPoint2(3, 7)));

            var chain = new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10));
            GeoPolyline2 parallel = chain.Offset(-1.0).Single();
            Assert.Equal(3, parallel.VertexCount);
            Assert.True(parallel[0].IsEqualTo(new GeoPoint2(0, -1)));
            Assert.True(parallel[1].IsEqualTo(new GeoPoint2(11, -1)));
            Assert.True(parallel[2].IsEqualTo(new GeoPoint2(11, 10)));

            var square = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10), new GeoPoint2(0, 10));
            Assert.Equal(14.0 * 14.0, square.Offset(2.0).Single().Area, 9);
            Assert.Equal(4, square.Offset(2.0).Single().VertexCount);
            Assert.True(square.Offset(2.0, OffsetJoin.Round).Single().VertexCount > 4);
            Assert.Equal(8, square.Offset(2.0, OffsetJoin.Chamfer).Single().VertexCount);
            Assert.Empty(square.Offset(-5.0));

            // "a growing polygon that closes a gap around empty space gives that space as a hole, wound the
            // other way so that signed areas add up"
            var ring = new GeoPolygon2(
                new GeoPoint2(0, 0), new GeoPoint2(20, 0), new GeoPoint2(20, 9), new GeoPoint2(15, 9), new GeoPoint2(15, 5),
                new GeoPoint2(5, 5), new GeoPoint2(5, 15), new GeoPoint2(15, 15), new GeoPoint2(15, 11), new GeoPoint2(20, 11),
                new GeoPoint2(20, 20), new GeoPoint2(0, 20));
            GeoPolygon2[] loops = ring.Offset(1.5);
            Assert.Equal(2, loops.Length);
            Assert.True(loops[1].IsClockwise);
            Assert.Equal(529.0 - 49.0, loops.Sum(l => l.SignedArea), 9);

            // "its holes shrink as the boundary grows"
            var face = new GeoFace2(square, new[] { new GeoPolygon2(new GeoPoint2(3, 3), new GeoPoint2(7, 3), new GeoPoint2(7, 7), new GeoPoint2(3, 7)) });
            GeoFace2 grown = face.Offset(1.0).Single();
            Assert.Equal(12.0 * 12.0 - 2.0 * 2.0, grown.Area, 9);

            // "GeoCircle2 and GeoRectangle2 offer TryOffset, which keeps them what they are"
            Assert.True(new GeoCircle2(new GeoPoint2(0, 0), 5).TryOffset(2.0, out GeoCircle2 wider));
            Assert.Equal(7.0, wider.Radius, 12);
            Assert.True(new GeoRectangle2(new GeoPoint2(0, 0), 10, 4).TryOffset(1.0, out GeoRectangle2 bigger));
            Assert.Equal(12.0, bigger.Width, 12);

            // "a square offset by 2 ends exactly on 2"
            Assert.Contains(square.Offset(2.0).Single().Vertices, v => v.X == 12.0 && v.Y == 12.0);
        }

        [Fact]
        public void CombiningRegions_EverySampleHolds()
        {
            var a = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10), new GeoPoint2(0, 10));
            var b = new GeoPolygon2(new GeoPoint2(5, 5), new GeoPoint2(15, 5), new GeoPoint2(15, 15), new GeoPoint2(5, 15));

            Assert.Equal(175.0, a.Union(b).Single().Area, 9);
            Assert.Equal(25.0, a.Intersect(b).Single().Area, 9);
            Assert.Equal(75.0, a.Subtract(b).Single().Area, 9);
            Assert.Equal(2, a.Xor(b).Length);
            Assert.Equal(150.0, a.Xor(b).Sum(f => f.Area), 9);

            // "an opening inside becomes a hole, one across the edge a notch"
            var outline = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 40), new GeoPoint2(0, 40));
            var openings = new[]
            {
                new GeoPolygon2(new GeoPoint2(10, 10), new GeoPoint2(20, 10), new GeoPoint2(20, 20), new GeoPoint2(10, 20)),
                new GeoPolygon2(new GeoPoint2(95, 30), new GeoPoint2(105, 30), new GeoPoint2(105, 35), new GeoPoint2(95, 35))
            };

            GeoFace2[] slab = Boolean2.Subtract(outline, openings);
            Assert.Single(slab);
            Assert.Single(slab[0].Holes);
            Assert.Equal(4000.0 - 100.0 - 25.0, slab[0].Area, 9);

            var tiles = new[] { a, b };
            GeoFace2[] floor = Boolean2.Union(tiles);
            Assert.Equal(175.0, floor.Sum(f => f.Area), 9);

            // "largest first, with boundaries counter-clockwise and holes clockwise"
            Assert.All(slab, face => Assert.False(face.Boundary.IsClockwise));
            Assert.All(slab, face => Assert.All(face.Holes, hole => Assert.True(hole.IsClockwise)));
        }

        [Fact]
        public void SelfIntersection_IsNotCheckedOnTheWayIn()
        {
            // "A GeoPolygon2 is not checked for self-intersection when it is built ... polygon.IsSimple()
            // runs it when you want it"
            var crossed = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(2, 8), new GeoPoint2(6, -4));

            Assert.False(crossed.IsSimple());
            Assert.True(new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10), new GeoPoint2(0, 10)).IsSimple());

            // "a doubled-back lobe counts against the rest"
            var bowtie = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(10, 10), new GeoPoint2(10, 0), new GeoPoint2(0, 10));
            Assert.False(bowtie.IsSimple());
            Assert.Equal(0.0, bowtie.Area, 9);
        }

        [Fact]
        public void PackageReadme_QuickStart_Holds()
        {
            var slab = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(4000, 0),
                                       new GeoPoint2(4000, 2000), new GeoPoint2(0, 2000));
            var duct = new GeoPolygon2(new GeoPoint2(1000, 500), new GeoPoint2(1400, 500),
                                       new GeoPoint2(1400, 900), new GeoPoint2(1000, 900));

            GeoPolygon2 grown = slab.Offset(50.0)[0];
            GeoFace2 pierced = Boolean2.Subtract(grown, duct)[0];

            // "4100 x 2100"
            Assert.Equal(4100.0 * 2100.0, grown.Area, 6);

            // "one face, one hole"
            Assert.Single(Boolean2.Subtract(grown, duct));
            Assert.Single(pierced.Holes);
            Assert.Equal(4100.0 * 2100.0 - 400.0 * 400.0, pierced.Area, 6);

            var beam = new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(3000, 0, 0));
            var wall = new GeoPlane3(new GeoPoint3(2500, 0, 0), GeoVector3.XAxis);

            // "(0,0,0) -> (2500,0,0)"
            Assert.True(beam.TryTrimTo(wall, LineEnd.End, out GeoLine3 cut));
            Assert.Equal(new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(2500, 0, 0)), cut);
        }

        [Fact]
        public void AskingWhereSomethingIs_EverySampleHolds()
        {
            var cut = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 10));
            var point = new GeoPoint2(0, 5);

            // "LineSide.Left, Right or On" and "the same, named the other way round"
            Assert.Equal(LineSide.Left, point.GetSideOf(cut));
            Assert.Equal(point.GetSideOf(cut), Containment2.GetSide(cut, point));

            // "reversing the segment swaps left and right"
            Assert.Equal(LineSide.Right, point.GetSideOf(new GeoLine2(cut.EndPoint, cut.StartPoint)));

            // "a point past either end still has a side"
            Assert.Equal(LineSide.Right, new GeoPoint2(100, 0).GetSideOf(cut));

            // "parallel is not enough: the same way along the line"
            var first = new GeoVector2(1, 1);
            var second = new GeoVector2(5, 5);
            Assert.True(first.IsCodirectionalTo(second));
            Assert.False(first.IsCodirectionalTo(new GeoVector2(-5, -5)));

            // "the foot of the perpendicular, beyond the ends if need be"
            Assert.True(Projection2.ProjectToInfiniteLine(new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 0)),
                                                          new GeoPoint2(25, 4))
                                   .IsEqualTo(new GeoPoint2(25, 0)));

            // "Every region reports its measurements as properties, named alike whatever the shape"
            var polygon = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(4, 0), new GeoPoint2(4, 3), new GeoPoint2(0, 3));
            var face = new GeoFace2(polygon);
            var rectangle = new GeoRectangle2(new GeoPoint2(0, 0), 4, 3);
            var circle = new GeoCircle2(GeoPoint2.Origin, 5);

            Assert.Equal(12.0, polygon.Area, 9);
            Assert.Equal(12.0, polygon.SignedArea, 9);
            Assert.False(polygon.IsClockwise);
            Assert.True(polygon.Centroid.IsEqualTo(new GeoPoint2(2, 1.5)));
            Assert.Equal(12.0, face.Area, 9);
            Assert.Equal(14.0, face.Length, 9);
            Assert.True(face.Centroid.IsEqualTo(polygon.Centroid));
            Assert.Equal(12.0, rectangle.Area, 9);
            Assert.Equal(Math.PI * 25.0, circle.Area, 9);
            Assert.Equal(2.0 * Math.PI * 5.0, circle.Length, 9);
        }

        [Fact]
        public void MovingGeometry_EverySampleHolds()
        {
            var center = new GeoPoint2(5, 5);
            var axis = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(1, 0));
            var origin = new GeoPoint2(100, 50);
            var xAxis = new GeoVector2(0, 2);
            var polygon = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(4, 0), new GeoPoint2(4, 2), new GeoPoint2(0, 2));
            var point = new GeoPoint2(101, 50);

            GeoTransform2 move = GeoTransform2.Translation(new GeoVector2(10, -3));
            GeoTransform2 turn = GeoTransform2.Rotation(center, Math.PI / 2);
            GeoTransform2 shrink = GeoTransform2.Scaling(center, 0.5);
            GeoTransform2 flip = GeoTransform2.Mirror(axis);
            GeoTransform2 place = GeoTransform2.FromFrame(origin, xAxis);

            var tight = new Tolerance(1E-9, 1E-9);

            Assert.True(move.Transform(center).IsEqualTo(new GeoPoint2(15, 2), tight));
            Assert.True(turn.Transform(center).IsEqualTo(center, tight));
            Assert.True(shrink.Transform(new GeoPoint2(7, 5)).IsEqualTo(new GeoPoint2(6, 5), tight));
            Assert.True(flip.Transform(new GeoPoint2(3, 4)).IsEqualTo(new GeoPoint2(3, -4), tight));
            Assert.True(place.Transform(GeoPoint2.Origin).IsEqualTo(origin, tight));

            // "turn first, then move"
            GeoPolygon2 moved = polygon.TransformBy(move.Multiply(turn));
            Assert.True(moved.IsEqualTo(polygon.TransformBy(turn).TransformBy(move), tight));

            // "read a placed drawing back": the frame's X axis runs along +Y of the world, so a point one
            // unit east of the frame origin sits one unit down the frame's own Y axis.
            Assert.True(place.Inverse().Transform(point).IsEqualTo(new GeoPoint2(0, -1), tight));
            Assert.True(place.Transform(new GeoPoint2(0, -1)).IsEqualTo(point, tight));

            // "A polygon that ran counter-clockwise comes back clockwise, its SignedArea changes sign and
            //  its Area does not."
            Assert.False(polygon.IsClockwise);
            Assert.True(flip.Transform(polygon).IsClockwise);
            Assert.Equal(-polygon.SignedArea, flip.Transform(polygon).SignedArea, 9);
            Assert.Equal(polygon.Area, flip.Transform(polygon).Area, 9);

            // "GetDeterminant ... is the factor areas are multiplied by, negative when the transformation
            //  reverses winding."
            Assert.Equal(-1.0, flip.GetDeterminant(), 9);
            Assert.Equal(0.25, shrink.GetDeterminant(), 9);
            Assert.Equal(polygon.Area * 0.25, shrink.Transform(polygon).Area, 9);

            // "a drawing scaled down by a thousandth still inverts cleanly"
            Assert.True(GeoTransform2.Scaling(0.001).TryGetInverse(out _));

            // "A circle stays a circle only when every direction is stretched by the same amount."
            var circle = new GeoCircle2(center, 2);
            Assert.Throws<InvalidOperationException>(() => GeoTransform2.Scaling(2, 3).Transform(circle));
            Assert.Equal(4.0, GeoTransform2.Scaling(2.0).Transform(circle).Radius, 9);

            // "transform rectangle.ToPolygon() when that is what you want"
            var rectangle = new GeoRectangle2(center, 4, 2);
            Assert.Throws<InvalidOperationException>(() => GeoTransform2.Scaling(2, 3).Transform(rectangle));
            Assert.Equal(4 * 2 * 6.0, GeoTransform2.Scaling(2, 3).Transform(rectangle.ToPolygon()).Area, 9);
        }

        [Fact]
        public void CuttingCorners_EverySampleHolds()
        {
            var plate = new GeoPolygon2(
                new GeoPoint2(0, 0), new GeoPoint2(200, 0), new GeoPoint2(200, 120), new GeoPoint2(0, 120));
            var chain = new GeoPolyline2(
                new GeoPoint2(0, 0), new GeoPoint2(50, 0), new GeoPoint2(50, 40), new GeoPoint2(90, 40));

            // "every corner, the same distance along both edges" - a rectangle comes back an octagon
            GeoPolygon2 cut = plate.Chamfer(15.0);
            Assert.Equal(8, cut.VertexCount);
            Assert.Equal(200.0 * 120.0 - 4.0 * 0.5 * 15.0 * 15.0, cut.Area, 9);

            // "along the edge coming in, then the edge going out"
            GeoPolygon2 uneven = plate.Chamfer(30.0, 5.0);
            Assert.Contains(uneven.Vertices, v => v.IsEqualTo(new GeoPoint2(170, 0)));
            Assert.Contains(uneven.Vertices, v => v.IsEqualTo(new GeoPoint2(200, 5)));

            // "reversing a polygon swaps them"
            var reversed = new GeoPolygon2(plate.Vertices.Reverse().ToArray());
            Assert.Contains(reversed.Chamfer(30.0, 5.0).Vertices, v => v.IsEqualTo(new GeoPoint2(195, 0)));

            // "one corner, precisely"
            Assert.True(plate.TryChamferAt(1, 10.0, 20.0, out GeoPolygon2 one));
            Assert.Equal(5, one.VertexCount);

            // "a chain keeps both of its end points exactly where they were"
            GeoPolyline2 chained = chain.Chamfer(10.0);
            Assert.True(chained[0].IsEqualTo(chain[0]));
            Assert.True(chained[chained.VertexCount - 1].IsEqualTo(chain[chain.VertexCount - 1]));

            // "the result goes straight on into the region operations with nothing to convert"
            Assert.Single(Boolean2.Union(cut, plate));
        }

        [Fact]
        public void CirclesAsPolygons_EverySampleHolds()
        {
            var circle = new GeoCircle2(new GeoPoint2(10, -5), 1000.0);

            // "automatic: within 0.2 % of the radius, about 50 edges"
            GeoPolygon2 automatic = circle.ToPolygon();
            Assert.InRange(automatic.VertexCount, 45, 55);

            // "no edge strays further than 0.5 from the circle"
            GeoPolygon2 fine = circle.ToPolygonByChordTolerance(0.5);
            GeoLine2 edge = fine.GetEdgeAt(0);
            GeoPoint2 middle = new GeoPoint2((edge.StartPoint.X + edge.EndPoint.X) * 0.5,
                                             (edge.StartPoint.Y + edge.EndPoint.Y) * 0.5);
            Assert.True(circle.Radius - circle.Center.DistanceTo(middle) <= 0.5 + 1E-9);

            // "no two vertices further apart than 25 along the circumference"
            GeoPolygon2 spaced = circle.ToPolygonBySpacing(25.0);
            Assert.True(circle.Length / spaced.VertexCount <= 25.0 + 1E-9);

            // "exactly 36 edges"
            Assert.Equal(36, circle.ToPolygon(36).VertexCount);

            // "the same points as a chain, first point repeated at the end"
            GeoPolyline2 asChain = circle.ToPolyline(36);
            Assert.Equal(37, asChain.VertexCount);
            Assert.True(asChain[0].IsEqualTo(asChain[36]));

            // "inscribed and encloses slightly less than the circle does: about 0.26 % less"
            Assert.All(automatic.Vertices, v => Assert.Equal(1000.0, circle.Center.DistanceTo(v), 9));
            Assert.True(automatic.Area < circle.Area);
            Assert.True(automatic.Area > circle.Area * 0.997);
            Assert.True(automatic.Area < circle.Area * 0.998);

            // "GeoCircle3 answers all of it the same way, in its own plane"
            var inSpace = new GeoCircle3(GeoPoint3.Origin, GeoVector3.ZAxis, 1000.0);
            Assert.Equal(automatic.VertexCount, inSpace.ToPolygon().VertexCount);
        }
        [Fact]
        public void Arcs_EverySampleHolds()
        {
            var arc = new GeoArc2(new GeoPoint2(0, 0), 50.0, 0.0, Math.PI / 2);
            var back = new GeoArc2(new GeoPoint2(0, 0), 50.0, 0.0, Math.PI / 2, clockwise: true);

            Assert.Equal(78.54, arc.Length, 2);
            Assert.Equal(1.5708, arc.EndAngle, 4);
            Assert.True(arc.MidPoint.IsEqualTo(new GeoPoint2(50.0 * Math.Cos(Math.PI / 4), 50.0 * Math.Sin(Math.PI / 4))));
            Assert.True(arc.GetChord().IsEqualTo(new GeoLine2(arc.StartPoint, arc.EndPoint)));
            Assert.Equal(50.0, arc.GetCircle().Radius, 9);

            // The other way round is the three quarters left behind.
            Assert.Equal(Math.PI * 1.5 * 50.0, back.Length, 6);

            var start = new GeoPoint2(0, 0);
            var end = new GeoPoint2(100, 0);
            GeoPoint2 somewhereOnIt = new GeoPoint2(50, 50);

            GeoArc2 through = GeoArc2.FromThreePoints(start, somewhereOnIt, end);
            GeoArc2 fromCad = GeoArc2.FromBulge(start, end, 0.5);

            Assert.Equal(50.0, through.Radius, 9);
            Assert.Equal(0.0, through.DistanceTo(somewhereOnIt), 9);

            // The bulge is the tangent of a quarter of the swept angle, and it comes back unchanged.
            Assert.Equal(0.5, fromCad.Bulge, 12);
            Assert.Equal(Math.Tan(fromCad.SweptAngle / 4.0), fromCad.Bulge, 12);
            Assert.Equal(1.0, GeoArc2.FromBulge(start, end, 1.0).SweptAngle / Math.PI, 9);

            // A GeoArc3 gives its own box rather than the one round the whole circle.
            var spatial = new GeoArc3(new GeoPoint3(0, 0, 0), GeoVector3.ZAxis, 50.0, 0.0, Math.PI / 2);
            GeoAabb3 box = spatial.GetAabb();
            Assert.Equal(50.0, box.Max.X - box.Min.X, 6);
            Assert.Equal(50.0, box.Max.Y - box.Min.Y, 6);
        }

        [Fact]
        public void ChainsThatCurve_EverySampleHolds()
        {
            var straight = new GeoEdge2(new GeoPoint2(0, 0), new GeoPoint2(30, 40));
            var bulged = new GeoEdge2(new GeoPoint2(0, 0), new GeoPoint2(20, 0), 1.0);

            Assert.Equal(50.0, straight.Length, 9);
            Assert.True(bulged.IsArc);
            Assert.Equal(31.4, bulged.Length, 1);
            Assert.Equal(10.0, bulged.ToArc().Radius, 9);
            Assert.Equal(20.0, bulged.GetChord().Length, 9);
            Assert.Throws<InvalidOperationException>(() => bulged.ToLine());

            var chain = new GeoPolylineArc2(
                new[] { new GeoPoint2(0, 0), new GeoPoint2(20, 0), new GeoPoint2(40, 20) },
                new[] { 1.0, 0.0 });

            Assert.True(chain.HasArcs);
            Assert.Equal(59.7, chain.Length, 1);
            Assert.True(chain.GetEdgeAt(0).IsArc);
            Assert.Equal(-1.0, chain.Reverse().GetBulgeAt(1), 12);

            // Widening a straight chain loses nothing.
            var existingPolyline = new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10));
            Assert.True(new GeoPolylineArc2(existingPolyline).Flatten().IsEqualTo(existingPolyline));

            // A repeated first vertex says nothing a loop does not already say, but its bulge is kept.
            var loop = new GeoPolygonArc2(
                new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 0) },
                new[] { 0.0, 0.0, 0.0, 0.25 });

            Assert.Equal(3, loop.VertexCount);
            Assert.Equal(0.25, loop.GetBulgeAt(2), 12);

            // Equals is exact and starts where the loop starts; IsEqualTo does not care.
            var shifted = new GeoPolygonArc2(
                new[] { new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 0) },
                new[] { 0.0, 0.25, 0.0 });

            Assert.True(loop.IsEqualTo(shifted));
            Assert.False(loop.Equals(shifted));
        }

        [Fact]
        public void Flattening_EverySampleHolds()
        {
            var loop = new GeoPolygonArc2(
                new[] { new GeoPoint2(0, 0), new GeoPoint2(1000, 0), new GeoPoint2(1000, 1000), new GeoPoint2(0, 1000) },
                new[] { 0.0, 0.0, 1.0, 0.0 });

            GeoPolygon2 flat = loop.Flatten();
            GeoPolygon2 fine = loop.Flatten(0.05);

            Assert.True(fine.VertexCount > flat.VertexCount);

            var opening = new GeoPolygon2(
                new GeoPoint2(400, 400), new GeoPoint2(600, 400), new GeoPoint2(600, 600), new GeoPoint2(400, 600));

            GeoFace2[] pierced = Boolean2.Subtract(flat, opening);

            Assert.Single(pierced);
            Assert.Single(pierced[0].Holes);

            // The chords lie inside the arc, so a shape bulging outward encloses a little less once flat.
            Assert.True(flat.Area < loop.Area);
            Assert.True(fine.Area > flat.Area);
        }

        [Fact]
        public void RoundingCorners_EverySampleHolds()
        {
            var plate = new GeoPolygon2(
                new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100));

            GeoPolygonArc2 rounded = new GeoPolygonArc2(plate).Fillet(15.0);

            // Four sides and four quarter turns.
            Assert.Equal(8, rounded.EdgeCount);
            Assert.Equal(4, rounded.GetEdges().Count(edge => edge.IsArc));
            Assert.True(rounded.Flatten().VertexCount > 8);

            var chain = new GeoPolylineArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100) });
            GeoPolylineArc2 eased = chain.Fillet(15.0);

            Assert.True(eased[0].IsEqualTo(chain[0]));
            Assert.True(eased[eased.VertexCount - 1].IsEqualTo(chain[chain.VertexCount - 1]));

            // One corner at a time, from two segments that need not already meet.
            var first = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(80, 0));
            var second = new GeoLine2(new GeoPoint2(100, 20), new GeoPoint2(100, 100));

            Assert.True(Lengthen2.TryFilletCorner(first, second, 15.0, out GeoArc2 arc, out GeoLine2 trimmed1, out GeoLine2 trimmed2));
            Assert.Equal(15.0, arc.Radius, 9);

            // The order the two segments come in does not change the answer.
            Assert.True(Lengthen2.TryFilletCorner(second, first, 15.0, out GeoArc2 other, out _, out _));
            Assert.True(other.Center.IsEqualTo(arc.Center));
            Assert.True(trimmed1.EndPoint.IsEqualTo(new GeoPoint2(85, 0)));
            Assert.True(trimmed2.StartPoint.IsEqualTo(new GeoPoint2(100, 15)));
        }
        [Fact]
        public void OffsettingWithoutStraightening_EverySampleHolds()
        {
            var slot = new GeoPolygonArc2(
                new[] { new GeoPoint2(0, 0), new GeoPoint2(200, 0), new GeoPoint2(200, 50), new GeoPoint2(0, 50) },
                new[] { 0.0, 1.0, 0.0, 1.0 });

            GeoPolygonArc2[] grown = slot.Offset(20.0);
            Assert.Single(grown);
            Assert.True(grown[0].Area > slot.Area);

            var chain = new GeoPolylineArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0) });
            GeoPolylineArc2[] beside = chain.Offset(-10.0);
            Assert.True(beside[0][0].IsEqualTo(new GeoPoint2(0, -10)));

            Assert.Single(slot.Offset(20.0, OffsetJoin.Round));

            // A fillet of R40 offset by 10 comes back R50 exactly.
            GeoPolygonArc2 plate = new GeoPolygonArc2(new GeoPolygon2(
                new GeoPoint2(0, 0), new GeoPoint2(300, 0), new GeoPoint2(300, 200), new GeoPoint2(0, 200)))
                .Fillet(40.0);

            foreach (GeoEdge2 edge in plate.Offset(10.0)[0].GetEdges().Where(edge => edge.IsArc))
            {
                Assert.Equal(50.0, edge.ToArc().Radius, 8);
            }

            // Shrunk far enough, a shape vanishes, so the answer is an array.
            Assert.Empty(slot.Offset(-40.0));

            // The booleans do flatten, and the type says so.
            var opening = new GeoPolygon2(
                new GeoPoint2(80, 15), new GeoPoint2(120, 15), new GeoPoint2(120, 35), new GeoPoint2(80, 35));

            GeoFace2[] pierced = slot.Subtract(opening);
            Assert.Single(pierced);
            Assert.Single(Boolean2.Subtract(slot, opening, 0.05));
        }

        [Fact]
        public void AnIndexOverTheEdges_EverySampleHolds()
        {
            var cog = new GeoPolygonArc2(
                new[] { new GeoPoint2(0, 0), new GeoPoint2(200, 0), new GeoPoint2(200, 50), new GeoPoint2(0, 50) },
                new[] { 0.0, 1.0, 0.0, 1.0 });

            GeoBvh2 index = GeoBvh2.FromPolygonArc(cog);

            var point = new GeoPoint2(400, 25);
            var knife = new GeoLine2(new GeoPoint2(100, -50), new GeoPoint2(100, 100));

            Assert.True(index.GetClosestPoint(point).IsEqualTo(new GeoPoint2(225, 25)));
            Assert.Equal(175.0, index.DistanceTo(point), 8);
            Assert.Equal(2, index.GetIntersections(knife).Length);

            GeoBvh2 other = GeoBvh2.FromPolygonArc(cog.Translate(new GeoVector2(600, 0)));

            Assert.True(index.DistanceTo(other) > 0.0);
            Assert.False(index.CollidesWith(other));

            // Every way of building one is there.
            Assert.Equal(4, GeoBvh2.FromPolygon(cog.Flatten(40.0)).EdgeCount > 0 ? 4 : 0);
            Assert.True(GeoBvh2.FromPolyline(new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(1, 1))).EdgeCount == 1);
            Assert.True(GeoBvh2.FromPolylineArc(new GeoPolylineArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(1, 1) })).EdgeCount == 1);
        }
        [Fact]
        public void WorkingInALocalFrame_EverySampleHolds()
        {
            var origin = new GeoPoint2(1000, -400);
            var xAxis = new GeoVector2(3, 4);

            var frame = new GeoCoordinateSystem2(origin, xAxis);

            Assert.True(new GeoCoordinateSystem2(origin, frame.AngleRad).IsEqualTo(frame));

            var local = new GeoPoint2(50, 20);
            GeoPoint2 world = frame.ToGlobal(local);

            Assert.True(frame.ToLocal(world).IsEqualTo(local));
            Assert.True(frame.ToTransform().Transform(local).IsEqualTo(world));

            var plate = new GeoRectangle2(new GeoPoint2(100, 50), 80, 40, Math.PI / 6.0);
            var point = new GeoPoint2(120, 60);

            Assert.True(plate.CoordinateSystem.ToGlobal(plate.CoordinateSystem.ToLocal(point)).IsEqualTo(point));
            Assert.Equal(plate.Area, new GeoRectangle2(plate.CoordinateSystem, 80.0, 40.0).Area, 9);

            var square = new GeoPolygon2(
                new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10), new GeoPoint2(0, 10));

            Assert.NotEqual(square.IsClockwise, square.Reverse().IsClockwise);
        }
        [Fact]
        public void OneRadiusPerCorner_EverySampleHolds()
        {
            var plate = new GeoPolygonArc2(new GeoPolygon2(
                new GeoPoint2(0, 0), new GeoPoint2(300, 0), new GeoPoint2(300, 200), new GeoPoint2(0, 200)));

            GeoPolygonArc2 mixed = plate.Fillet(new[] { 40.0, 10.0, 0.0, 25.0 });

            Assert.Equal(3, mixed.GetEdges().Count(edge => edge.IsArc));
            Assert.Contains(mixed.Vertices, v => v.IsEqualTo(new GeoPoint2(300, 200)));

            Assert.True(plate.TryFilletAt(1, 30.0, out GeoPolygonArc2 one));
            Assert.Equal(1, one.GetEdges().Count(edge => edge.IsArc));

            // A shorter list leaves the rest alone; the greedy corner gives way to its neighbour.
            Assert.Equal(1, plate.Fillet(new[] { 20.0 }).GetEdges().Count(edge => edge.IsArc));
            Assert.Equal(1, plate.Fillet(new[] { 250.0, 20.0, 0.0, 0.0 }).GetEdges().Count(edge => edge.IsArc));
        }
    }
}
