using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using GeometryHelper.TeklaConvert;
using Xunit;
using TSG = Tekla.Structures.Geometry3d;

namespace GeometryHelper.TeklaConvert.UnitTest
{
    /// <summary>
    /// Turning a Tekla reinforcement into a bar.
    /// <para>
    /// Tekla sets a bar out the way a schedule does: the points it turns at, and a bending radius for each
    /// turn. The bar is not that polyline — it is that polyline with a tangent arc at every bend, so it is
    /// shorter than its set-out and does not pass through its own corners. Everything that matters about
    /// that mapping is tested here, because it works on <c>Tekla.Structures.Geometry3d</c> types and so
    /// needs no running Tekla; the overloads reaching into <c>Tekla.Structures.Model</c> cannot be
    /// exercised without the modeller and are not pretended to be.
    /// </para>
    /// </summary>
    public class RebarConvertTests
    {
        private static readonly Tolerance Loose = new Tolerance(1E-6, 1E-6);

        /// <summary>
        /// An L bar in millimetres, as Tekla holds it: two legs of three hundred meeting at a right angle.
        /// </summary>
        private static List<TSG.Point> Elbow() => new List<TSG.Point>
        {
            new TSG.Point(0, 0, 0), new TSG.Point(300, 0, 0), new TSG.Point(300, 300, 0)
        };

        /// <summary>
        /// A bar that turns twice, the second turn leaving the plane of the first.
        /// </summary>
        private static List<TSG.Point> Stirrup() => new List<TSG.Point>
        {
            new TSG.Point(0, 0, 0), new TSG.Point(300, 0, 0),
            new TSG.Point(300, 300, 0), new TSG.Point(300, 300, 300)
        };

        [Fact]
        public void ABarIsItsSetOutPointsWithATangentArcAtEveryBend()
        {
            GeoPolylineArc3 bar = RebarConvert.ToGeoPolylineArc3(Elbow(), new[] { 50.0 });

            // A straight run, a quarter turn of the bending radius, a straight run.
            Assert.Equal(3, bar.EdgeCount);
            Assert.Equal(1, bar.GetEdges().Count(edge => edge.IsArc));
            Assert.Equal(50.0, bar.GetEdges().Single(edge => edge.IsArc).ToArc().Radius, 6);

            // The bar does not pass through its own corner, and it is shorter than the set-out.
            Assert.False(bar.IsPointOn(new GeoPoint3(300, 0, 0)));
            Assert.Equal(250.0 + Math.PI * 50.0 / 2.0 + 250.0, bar.Length, 6);
            Assert.True(bar.Length < 600.0);

            // Nothing is scaled: Tekla works in millimetres and so does what comes back.
            Assert.True(bar.StartPoint.IsEqualTo(new GeoPoint3(0, 0, 0), Loose));
            Assert.True(bar.EndPoint.IsEqualTo(new GeoPoint3(300, 300, 0), Loose));
        }

        [Fact]
        public void ARadiusPerBendIsReadAgainstTheBendAndNotAgainstTheFirstPoint()
        {
            // Tekla gives one radius per bend, and a bar of four points has two. The first belongs to the
            // second point, because nothing turns at the start of a bar.
            GeoPolylineArc3 bar = RebarConvert.ToGeoPolylineArc3(Stirrup(), new[] { 40.0, 0.0 });

            Assert.Equal(1, bar.GetEdges().Count(edge => edge.IsArc));
            Assert.Equal(40.0, bar.GetEdges().Single(edge => edge.IsArc).ToArc().Radius, 6);

            // The bend that was given a radius is the first one, at (300, 0, 0).
            Assert.False(bar.IsPointOn(new GeoPoint3(300, 0, 0)));
            Assert.True(bar.IsPointOn(new GeoPoint3(300, 300, 0), Loose));

            // And the other way round.
            GeoPolylineArc3 second = RebarConvert.ToGeoPolylineArc3(Stirrup(), new[] { 0.0, 40.0 });

            Assert.True(second.IsPointOn(new GeoPoint3(300, 0, 0), Loose));
            Assert.False(second.IsPointOn(new GeoPoint3(300, 300, 0)));
        }

        [Fact]
        public void AListAsLongAsThePointsIsTakenAsItIs()
        {
            // A caller who has already built the list the way GeometryHelper reads it should not have it
            // shifted underneath them: the entries for the two ends are simply ignored.
            GeoPolylineArc3 bar = RebarConvert.ToGeoPolylineArc3(Stirrup(), new[] { 99.0, 40.0, 0.0, 99.0 });

            Assert.Equal(1, bar.GetEdges().Count(edge => edge.IsArc));
            Assert.Equal(40.0, bar.GetEdges().Single(edge => edge.IsArc).ToArc().Radius, 6);
            Assert.False(bar.IsPointOn(new GeoPoint3(300, 0, 0)));
        }

        [Fact]
        public void EveryBendKeepsItsOwnPlane()
        {
            // The two bends of this bar turn about different axes, which is the whole reason a bar cannot be
            // held as a flat chain.
            GeoPolylineArc3 bar = RebarConvert.ToGeoPolylineArc3(Stirrup(), new[] { 50.0, 50.0 });

            GeoEdge3[] arcs = bar.GetEdges().Where(edge => edge.IsArc).ToArray();

            Assert.Equal(2, arcs.Length);
            Assert.False(bar.IsPlanar());
            Assert.False(arcs[0].Normal.IsParallelTo(arcs[1].Normal, Loose));
            Assert.All(arcs, edge => Assert.Equal(50.0, edge.ToArc().Radius, 6));
        }

        [Fact]
        public void ABendWithNoRoomOrNoRadiusIsLeftSquare()
        {
            // No radii at all is a bar of straight runs, and every point is still on it.
            GeoPolylineArc3 sharp = RebarConvert.ToGeoPolylineArc3(Stirrup(), null);

            Assert.Equal(0, sharp.GetEdges().Count(edge => edge.IsArc));
            Assert.True(sharp.IsPointOn(new GeoPoint3(300, 0, 0), Loose));
            Assert.Equal(900.0, sharp.Length, 6);

            // A radius of nought leaves that bend square.
            Assert.Equal(0, RebarConvert.ToGeoPolylineArc3(Stirrup(), new[] { 0.0, 0.0 }).GetEdges().Count(edge => edge.IsArc));

            // A radius with nowhere to fit is left square rather than forced.
            Assert.Equal(0, RebarConvert.ToGeoPolylineArc3(Elbow(), new[] { 5000.0 }).GetEdges().Count(edge => edge.IsArc));

            // Two bends wanting more of the leg between them than it is long: the greedy one gives way.
            GeoPolylineArc3 crowded = RebarConvert.ToGeoPolylineArc3(Stirrup(), new[] { 280.0, 40.0 });

            Assert.Equal(1, crowded.GetEdges().Count(edge => edge.IsArc));
            Assert.Equal(40.0, crowded.GetEdges().Single(edge => edge.IsArc).ToArc().Radius, 6);
        }

        [Fact]
        public void AShapeWorkedOutByTeklaIsReadTheSameWay()
        {
            var shape = new TSG.PolyLine(new ArrayList(Elbow()));

            GeoPolylineArc3 fromShape = RebarConvert.ToGeoPolylineArc3(shape, new[] { 50.0 });
            GeoPolylineArc3 fromPoints = RebarConvert.ToGeoPolylineArc3(Elbow(), new[] { 50.0 });

            Assert.True(fromShape.IsEqualTo(fromPoints, Loose));
            Assert.Equal(fromShape.Length, fromPoints.Length, 6);

            // The tolerance forms answer the same, which matters because Tekla coordinates run large.
            Assert.True(RebarConvert.ToGeoPolylineArc3(shape, new[] { 50.0 }, Tolerance.Global).IsEqualTo(fromShape, Loose));
        }

        [Fact]
        public void ABarSetOutFarFromTheOriginIsStillABar()
        {
            // Tekla coordinates run to hundreds of thousands of millimetres, which is where a tolerance
            // meant for small numbers starts to matter.
            var far = new List<TSG.Point>
            {
                new TSG.Point(452300, -118700, 39500),
                new TSG.Point(452600, -118700, 39500),
                new TSG.Point(452600, -118400, 39500)
            };

            GeoPolylineArc3 bar = RebarConvert.ToGeoPolylineArc3(far, new[] { 50.0 });

            Assert.Equal(1, bar.GetEdges().Count(edge => edge.IsArc));
            Assert.Equal(50.0, bar.GetEdges().Single(edge => edge.IsArc).ToArc().Radius, 4);
            Assert.Equal(250.0 + Math.PI * 50.0 / 2.0 + 250.0, bar.Length, 4);
        }

        [Fact]
        public void NothingIsAskedOfNothing()
        {
            Assert.Throws<ArgumentNullException>(() => RebarConvert.ToGeoPolylineArc3((IList<TSG.Point>)null, new[] { 50.0 }));
            Assert.Throws<ArgumentNullException>(() => RebarConvert.ToGeoPolylineArc3((TSG.PolyLine)null, new[] { 50.0 }));

            // A point that is not there cannot be set out through.
            var holed = new List<TSG.Point> { new TSG.Point(0, 0, 0), null, new TSG.Point(300, 300, 0) };

            Assert.Throws<ArgumentException>(() => RebarConvert.ToGeoPolylineArc3(holed, new[] { 50.0 }));

            // One point is no bar.
            Assert.Throws<ArgumentException>(() => RebarConvert.ToGeoPolylineArc3(
                new List<TSG.Point> { new TSG.Point(0, 0, 0) }, new[] { 50.0 }));
        }
    }
}
