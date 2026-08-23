using System;
using System.Collections.Generic;
using System.Linq;
using CommonGeometry;
using CommonGeometry.Enums;
using PlaneGeometry.Core;
using PlaneGeometry.Geometry;
using Xunit;

namespace PlaneGeometry.UnitTest
{
    /// <summary>
    /// Every code sample in PlaneGeometry/README.md, compiled and run. A README that drifts from the
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
    }
}
