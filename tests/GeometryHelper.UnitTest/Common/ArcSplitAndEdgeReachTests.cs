using System;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Common
{
    /// <summary>
    /// The last two blanks the matrix showed: an arc in space could not be cut in two, though one in the plane
    /// could and a whole chain of them could; and an edge could not be lengthened or cut back, though both the
    /// shapes it reads as could.
    /// </summary>
    /// <remarks>
    /// Both are dispatch rather than arithmetic. Cutting an arc shares its sweep out and keeps its centre, its
    /// radius and its plane, so the pieces put back end to end draw what went in. An edge carries on straight or
    /// carries on round, and hands back an edge either way, which is what lets the direction be offered at all.
    /// </remarks>
    public class ArcSplitAndEdgeReachTests
    {
        private static readonly Tolerance Loose = new Tolerance(1E-7, 1E-7);

        private static GeoArc3 Quarter() => GeoArc3.FromThreePoints(
            new GeoPoint3(100, 0, 0),
            new GeoPoint3(100.0 * Math.Cos(Math.PI / 4.0), 100.0 * Math.Sin(Math.PI / 4.0), 0),
            new GeoPoint3(0, 100, 0));

        [Fact]
        public void AnArcInSpaceCutInTwoKeepsItsCentreRadiusAndPlane()
        {
            GeoArc3 quarter = Quarter();

            Assert.True(quarter.TrySplitAt(0.25, out GeoArc3[] pieces));
            Assert.Equal(2, pieces.Length);

            Assert.Equal(quarter.Length, pieces[0].Length + pieces[1].Length, 6);
            Assert.Equal(quarter.Length * 0.25, pieces[0].Length, 6);

            foreach (GeoArc3 piece in pieces)
            {
                Assert.Equal(quarter.Radius, piece.Radius, 9);
                Assert.True(piece.Center.IsEqualTo(quarter.Center, Loose));
                Assert.True(piece.Normal.IsEqualTo(quarter.Normal, Loose));
            }

            // The pieces run end to end along the arc it came from.
            Assert.True(pieces[0].StartPoint.IsEqualTo(quarter.StartPoint, Loose));
            Assert.True(pieces[0].EndPoint.IsEqualTo(pieces[1].StartPoint, Loose));
            Assert.True(pieces[1].EndPoint.IsEqualTo(quarter.EndPoint, Loose));
        }

        [Fact]
        public void AnArcInSpaceCanBeCutAtAPointAndAtADistance()
        {
            GeoArc3 quarter = Quarter();
            GeoPoint3 middle = quarter.GetPointAtParameter(0.5);

            Assert.True(quarter.TrySplitAt(middle, out GeoArc3[] halves));
            Assert.Equal(halves[0].Length, halves[1].Length, 6);

            Assert.True(quarter.TrySplitAtDistance(quarter.Length / 4.0, out GeoArc3[] quarters));
            Assert.Equal(quarter.Length / 4.0, quarters[0].Length, 6);

            // A cut at either end leaves the arc whole, and so does a cut past it.
            Assert.False(quarter.TrySplitAt(0.0, out GeoArc3[] whole));
            Assert.Single(whole);
            Assert.Equal(quarter.Length, whole[0].Length, 6);

            Assert.False(quarter.TrySplitAt(1.0, out _));
            Assert.False(quarter.TrySplitAt(double.NaN, out _));
            Assert.False(quarter.TrySplitAtDistance(0.0, out _));
            Assert.False(quarter.TrySplitAtDistance(quarter.Length, out _));
        }

        [Fact]
        public void TheArcsOfThePlaneAndOfSpaceCutTheSameWay()
        {
            GeoArc3 quarter = Quarter();
            var flat = new GeoArc2(new GeoPoint2(0, 0), 100.0, 0.0, Math.PI / 2.0);

            Assert.True(flat.TrySplitAt(0.25, out GeoArc2[] inThePlane));
            Assert.True(quarter.TrySplitAt(0.25, out GeoArc3[] inSpace));

            Assert.Equal(inThePlane.Length, inSpace.Length);
            Assert.Equal(inThePlane[0].Length, inSpace[0].Length, 6);
            Assert.Equal(inThePlane[1].Length, inSpace[1].Length, 6);
        }

        [Fact]
        public void AStraightEdgeCarriesOnStraightAndACurvedOneCarriesOnRound()
        {
            var straight = new GeoEdge3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0));

            GeoEdge3 longer = straight.Extend(50.0, LineEnd.End);

            Assert.False(longer.IsArc);
            Assert.Equal(150.0, longer.Length, 6);
            Assert.True(longer.StartPoint.IsEqualTo(straight.StartPoint, Loose));
            Assert.True(longer.EndPoint.IsEqualTo(new GeoPoint3(150, 0, 0), Loose));

            // A bend keeps its radius and gains sweep.
            var bend = new GeoEdge3(Quarter());

            Assert.True(bend.IsArc);

            GeoEdge3 grown = bend.Extend(50.0, LineEnd.End);

            Assert.True(grown.IsArc);
            Assert.Equal(bend.Length + 50.0, grown.Length, 6);
            Assert.Equal(100.0, grown.ToArc().Radius, 6);
            Assert.True(grown.ToArc().Center.IsEqualTo(bend.ToArc().Center, Loose));

            // To a length works either way.
            Assert.Equal(200.0, straight.ExtendToLength(200.0, LineEnd.Start).Length, 6);
            Assert.Equal(200.0, bend.ExtendToLength(200.0, LineEnd.Start).Length, 6);
            Assert.True(bend.ExtendToLength(200.0, LineEnd.Start).IsArc);
        }

        [Fact]
        public void AnEdgeIsCutBackToAPointOnIt()
        {
            var straight = new GeoEdge3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0));

            Assert.True(straight.TryTrimTo(new GeoPoint3(60, 0, 0), LineEnd.End, out GeoEdge3 shorter));
            Assert.Equal(60.0, shorter.Length, 6);
            Assert.True(shorter.StartPoint.IsEqualTo(straight.StartPoint, Loose));

            var bend = new GeoEdge3(Quarter());
            GeoPoint3 on = bend.ToArc().GetPointAtParameter(0.5);

            Assert.True(bend.TryTrimTo(on, LineEnd.End, out GeoEdge3 cut));
            Assert.True(cut.IsArc);
            Assert.Equal(bend.Length / 2.0, cut.Length, 6);
            Assert.Equal(100.0, cut.ToArc().Radius, 6);

            // A point off the edge cuts nothing, and the edge comes back as it was.
            Assert.False(bend.TryTrimTo(new GeoPoint3(500, 500, 500), LineEnd.End, out GeoEdge3 whole));
            Assert.Equal(bend.Length, whole.Length, 6);
        }

        [Fact]
        public void TheEdgeOfThePlaneAnswersTheSameWay()
        {
            var straight = new GeoEdge2(new GeoPoint2(0, 0), new GeoPoint2(100, 0));

            Assert.Equal(150.0, straight.Extend(50.0, LineEnd.End).Length, 6);
            Assert.Equal(200.0, straight.ExtendToLength(200.0, LineEnd.End).Length, 6);
            Assert.True(straight.TryTrimTo(new GeoPoint2(60, 0), LineEnd.End, out GeoEdge2 shorter));
            Assert.Equal(60.0, shorter.Length, 6);

            var bend = new GeoEdge2(new GeoArc2(new GeoPoint2(0, 0), 100.0, 0.0, Math.PI / 2.0));

            Assert.True(bend.IsArc);
            Assert.Equal(bend.Length + 50.0, bend.Extend(50.0, LineEnd.End).Length, 6);
            Assert.Equal(100.0, bend.Extend(50.0, LineEnd.End).ToArc().Radius, 6);

            // The two edges agree on the same shapes laid flat.
            var inSpace = new GeoEdge3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0));

            Assert.Equal(straight.Extend(50.0, LineEnd.End).Length, inSpace.Extend(50.0, LineEnd.End).Length, 6);
        }

        [Fact]
        public void EveryNewDirectionTakesAToleranceAndNothingIsAskedOfNothing()
        {
            Tolerance global = Tolerance.Global;
            GeoArc3 quarter = Quarter();
            var bend = new GeoEdge3(quarter);
            var straight = new GeoEdge2(new GeoPoint2(0, 0), new GeoPoint2(100, 0));

            Assert.Equal(
                quarter.TrySplitAt(0.5, out GeoArc3[] a),
                quarter.TrySplitAt(0.5, out GeoArc3[] b, global));
            Assert.Equal(a[0].Length, b[0].Length, 9);

            Assert.Equal(
                quarter.TrySplitAtDistance(50.0, out GeoArc3[] c),
                quarter.TrySplitAtDistance(50.0, out GeoArc3[] d, global));
            Assert.Equal(c[0].Length, d[0].Length, 9);

            Assert.Equal(bend.Extend(10.0, LineEnd.End).Length, bend.Extend(10.0, LineEnd.End, global).Length, 9);
            Assert.Equal(bend.ExtendToLength(200.0, LineEnd.End).Length, bend.ExtendToLength(200.0, LineEnd.End, global).Length, 9);
            Assert.Equal(straight.Extend(10.0, LineEnd.End).Length, straight.Extend(10.0, LineEnd.End, global).Length, 9);

            GeoPoint3 on = quarter.GetPointAtParameter(0.5);

            Assert.Equal(
                bend.TryTrimTo(on, LineEnd.End, out GeoEdge3 e),
                bend.TryTrimTo(on, LineEnd.End, out GeoEdge3 f, global));
            Assert.Equal(e.Length, f.Length, 9);

            Assert.Throws<ArgumentOutOfRangeException>(() => bend.Extend(double.NaN, LineEnd.End));
            Assert.Throws<ArgumentOutOfRangeException>(() => bend.Extend(10.0, (LineEnd)99));
        }
    }
}
