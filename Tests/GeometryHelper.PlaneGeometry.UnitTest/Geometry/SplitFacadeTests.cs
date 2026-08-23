using System.Linq;
using GeometryHelper.CommonGeometry;
using GeometryHelper.PlaneGeometry.Core;
using GeometryHelper.PlaneGeometry.Geometry;
using Xunit;

namespace GeometryHelper.PlaneGeometry.UnitTest.Geometry
{
    /// <summary>
    /// The instance methods on GeoLine2 and GeoPolyline2 that forward to <see cref="Splition2"/>.
    /// <para>
    /// A forwarding method has its own failure mode: it compiles and returns something plausible while
    /// handing the operation the wrong arguments — the two out parameters swapped, or Tolerance.Global
    /// substituted for the tolerance the caller passed. Neither shows up in a test that only checks the
    /// result looks reasonable, so every case here pins the instance call against the static one, and
    /// every tolerance overload is given a tolerance that changes the answer.
    /// </para>
    /// </summary>
    public class SplitFacadeTests
    {
        private static GeoLine2 Segment() => new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 0));

        private static GeoPolyline2 Path() => new GeoPolyline2(
            new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10));

        private static GeoPolygon2 Square() => new GeoPolygon2(
            new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10), new GeoPoint2(0, 10));

        // Wide enough that a position 0.4 from an endpoint stops counting as a split.
        private static Tolerance Wide() => new Tolerance(0.5, 0.5);

        #region GeoLine2

        [Fact]
        public void Line_SplitByPoint_MatchesTheStaticCall()
        {
            var point = new GeoPoint2(4, 0);

            Assert.True(Segment().TrySplitBy(point, out GeoLine2 a1, out GeoLine2 a2));
            Assert.True(Splition2.TrySplitBy(Segment(), point, out GeoLine2 b1, out GeoLine2 b2));

            Assert.Equal(b1, a1);
            Assert.Equal(b2, a2);

            // The piece holding the start point comes first; swapping the out parameters would pass a
            // length check but fail this.
            Assert.True(a1.StartPoint.IsEqualTo(new GeoPoint2(0, 0)));
            Assert.True(a2.EndPoint.IsEqualTo(new GeoPoint2(10, 0)));
        }

        [Fact]
        public void Line_SplitByPoint_HonoursTheSuppliedTolerance()
        {
            var nearTheEnd = new GeoPoint2(0.4, 0);

            Assert.True(Segment().TrySplitBy(nearTheEnd, out _, out _));
            Assert.False(Segment().TrySplitBy(nearTheEnd, out _, out _, Wide()));
        }

        [Fact]
        public void Line_SplitByLine_MatchesTheStaticCall()
        {
            var cutter = new GeoLine2(new GeoPoint2(6, -5), new GeoPoint2(6, 5));

            Assert.True(Segment().TrySplitBy(cutter, out GeoLine2 a1, out GeoLine2 a2));
            Assert.True(Splition2.TrySplitBy(Segment(), cutter, out GeoLine2 b1, out GeoLine2 b2));

            Assert.Equal(b1, a1);
            Assert.Equal(b2, a2);
            Assert.True(a1.EndPoint.IsEqualTo(new GeoPoint2(6, 0)));
        }

        [Fact]
        public void Line_SplitByLine_HonoursTheSuppliedTolerance()
        {
            // Crossing 0.4 from the end: a split under the default tolerance, none under a wide one.
            var cutter = new GeoLine2(new GeoPoint2(9.6, -5), new GeoPoint2(9.6, 5));

            Assert.True(Segment().TrySplitBy(cutter, out _, out _));
            Assert.False(Segment().TrySplitBy(cutter, out _, out _, Wide()));
        }

        [Fact]
        public void Line_SplitByPolygon_MatchesTheStaticCall()
        {
            var line = new GeoLine2(new GeoPoint2(-5, 5), new GeoPoint2(15, 5));

            Assert.True(line.TrySplitBy(Square(), out GeoLine2[] aIn, out GeoLine2[] aOut));
            Assert.True(Splition2.TrySplitBy(line, Square(), out GeoLine2[] bIn, out GeoLine2[] bOut));

            Assert.Equal(bIn, aIn);
            Assert.Equal(bOut, aOut);

            // Inside and outside must not be handed over the wrong way round.
            Assert.Single(aIn);
            Assert.Equal(10.0, aIn[0].Length, 9);
            Assert.Equal(2, aOut.Length);
        }

        [Fact]
        public void Line_SplitByPolygon_HonoursTheSuppliedTolerance()
        {
            var line = new GeoLine2(new GeoPoint2(-5, 5), new GeoPoint2(15, 5));

            Assert.True(line.TrySplitBy(Square(), out GeoLine2[] _, out GeoLine2[] _, new Tolerance(1E-4, 1E-4)));

            // A tolerance wider than the whole crossing swallows both cut positions.
            Assert.False(line.TrySplitBy(Square(), out GeoLine2[] wideIn, out GeoLine2[] wideOut, new Tolerance(30.0, 30.0)));
            Assert.Equal(1, wideIn.Length + wideOut.Length);
        }

        [Fact]
        public void Line_SplitAtDistance_MatchesTheStaticCall()
        {
            Assert.True(Segment().TrySplitAtDistance(4.0, out GeoLine2 a1, out GeoLine2 a2));
            Assert.True(Splition2.TrySplitAtDistance(Segment(), 4.0, out GeoLine2 b1, out GeoLine2 b2));

            Assert.Equal(b1, a1);
            Assert.Equal(b2, a2);
            Assert.Equal(4.0, a1.Length, 9);
            Assert.Equal(6.0, a2.Length, 9);
        }

        [Fact]
        public void Line_SplitAtDistance_HonoursTheSuppliedTolerance()
        {
            Assert.True(Segment().TrySplitAtDistance(0.4, out _, out _));
            Assert.False(Segment().TrySplitAtDistance(0.4, out _, out _, Wide()));
        }

        [Fact]
        public void Line_SplitAtDistances_MatchesTheStaticCall()
        {
            var cuts = new[] { 2.0, 5.0, 8.0 };

            GeoLine2[] viaInstance = Segment().SplitAtDistances(cuts);
            GeoLine2[] viaStatic = Splition2.SplitAtDistances(Segment(), cuts);

            Assert.Equal(viaStatic, viaInstance);
            Assert.Equal(4, viaInstance.Length);
            Assert.True(viaInstance[0].StartPoint.IsEqualTo(new GeoPoint2(0, 0)));
        }

        [Fact]
        public void Line_SplitAtDistances_HonoursTheSuppliedTolerance()
        {
            var cuts = new[] { 4.0, 4.2 };

            // The default tolerance keeps both positions; a wide one merges them.
            Assert.Equal(3, Segment().SplitAtDistances(cuts).Length);
            Assert.Equal(2, Segment().SplitAtDistances(cuts, Wide()).Length);
        }

        #endregion

        #region GeoPolyline2

        [Fact]
        public void Polyline_SplitByPoint_MatchesTheStaticCall()
        {
            var point = new GeoPoint2(10, 4);

            Assert.True(Path().TrySplitBy(point, out GeoPolyline2 a1, out GeoPolyline2 a2));
            Assert.True(Splition2.TrySplitBy(Path(), point, out GeoPolyline2 b1, out GeoPolyline2 b2));

            Assert.Equal(b1, a1);
            Assert.Equal(b2, a2);
            Assert.True(a1[0].IsEqualTo(new GeoPoint2(0, 0)));
            Assert.True(a2[a2.VertexCount - 1].IsEqualTo(new GeoPoint2(10, 10)));
        }

        [Fact]
        public void Polyline_SplitByPoint_HonoursTheSuppliedTolerance()
        {
            var nearTheStart = new GeoPoint2(0.4, 0);

            Assert.True(Path().TrySplitBy(nearTheStart, out GeoPolyline2 _, out GeoPolyline2 _));
            Assert.False(Path().TrySplitBy(nearTheStart, out GeoPolyline2 _, out GeoPolyline2 _, Wide()));
        }

        [Fact]
        public void Polyline_SplitByLine_MatchesTheStaticCall()
        {
            var cutter = new GeoLine2(new GeoPoint2(4, -5), new GeoPoint2(4, 5));

            Assert.True(Path().TrySplitBy(cutter, out GeoPolyline2[] aPieces));
            Assert.True(Splition2.TrySplitBy(Path(), cutter, out GeoPolyline2[] bPieces));

            Assert.Equal(bPieces, aPieces);

            Assert.True(Path().TrySplitBy(cutter, out GeoPolyline2[] viaInstance));
            Assert.True(Splition2.TrySplitBy(Path(), cutter, out GeoPolyline2[] viaStatic));
            Assert.Equal(viaStatic, viaInstance);
            Assert.Equal(2, viaInstance.Length);
        }

        [Fact]
        public void Polyline_SplitByLine_HonoursTheSuppliedTolerance()
        {
            var cutter = new GeoLine2(new GeoPoint2(0.4, -5), new GeoPoint2(0.4, 5));

            Assert.True(Path().TrySplitBy(cutter, out GeoPolyline2[] _));
            Assert.False(Path().TrySplitBy(cutter, out GeoPolyline2[] _, Wide()));

            Assert.True(Path().TrySplitBy(cutter, out GeoPolyline2[] pieces1));
            Assert.Equal(2, pieces1.Length);
            Assert.False(Path().TrySplitBy(cutter, out GeoPolyline2[] pieces2, Wide()));
            Assert.Single(pieces2);
        }

        [Fact]
        public void Polyline_SplitByPolygon_MatchesTheStaticCall()
        {
            var path = new GeoPolyline2(new GeoPoint2(-5, 5), new GeoPoint2(5, 5), new GeoPoint2(5, 15));

            Assert.True(path.TrySplitBy(Square(), out GeoPolyline2[] aIn, out GeoPolyline2[] aOut));
            Assert.True(Splition2.TrySplitBy(path, Square(), out GeoPolyline2[] bIn, out GeoPolyline2[] bOut));

            Assert.Equal(bIn, aIn);
            Assert.Equal(bOut, aOut);
            Assert.Equal(path.Length, aIn.Sum(p => p.Length) + aOut.Sum(p => p.Length), 9);
        }

        [Fact]
        public void Polyline_SplitByPolygon_HonoursTheSuppliedTolerance()
        {
            var path = new GeoPolyline2(new GeoPoint2(-5, 5), new GeoPoint2(15, 5));

            Assert.True(path.TrySplitBy(Square(), out GeoPolyline2[] _, out GeoPolyline2[] _, new Tolerance(1E-4, 1E-4)));
            Assert.False(path.TrySplitBy(Square(), out GeoPolyline2[] wideIn, out GeoPolyline2[] wideOut, new Tolerance(30.0, 30.0)));
            Assert.Equal(1, wideIn.Length + wideOut.Length);
        }

        [Fact]
        public void Polyline_SplitAtDistance_MatchesTheStaticCall()
        {
            Assert.True(Path().TrySplitAtDistance(15.0, out GeoPolyline2 a1, out GeoPolyline2 a2));
            Assert.True(Splition2.TrySplitAtDistance(Path(), 15.0, out GeoPolyline2 b1, out GeoPolyline2 b2));

            Assert.Equal(b1, a1);
            Assert.Equal(b2, a2);
            Assert.Equal(15.0, a1.Length, 9);
            Assert.Equal(5.0, a2.Length, 9);
        }

        [Fact]
        public void Polyline_SplitAtDistance_HonoursTheSuppliedTolerance()
        {
            Assert.True(Path().TrySplitAtDistance(0.4, out GeoPolyline2 _, out GeoPolyline2 _));
            Assert.False(Path().TrySplitAtDistance(0.4, out GeoPolyline2 _, out GeoPolyline2 _, Wide()));
        }

        [Fact]
        public void Polyline_SplitAtDistances_MatchesTheStaticCall()
        {
            var cuts = new[] { 5.0, 15.0 };

            GeoPolyline2[] viaInstance = Path().SplitAtDistances(cuts);
            Assert.Equal(Splition2.SplitAtDistances(Path(), cuts), viaInstance);
            Assert.Equal(3, viaInstance.Length);
            Assert.Equal(Path().Length, viaInstance.Sum(p => p.Length), 9);
        }

        [Fact]
        public void Polyline_SplitAtDistances_HonoursTheSuppliedTolerance()
        {
            var cuts = new[] { 5.0, 5.2 };

            Assert.Equal(3, Path().SplitAtDistances(cuts).Length);
            Assert.Equal(2, Path().SplitAtDistances(cuts, Wide()).Length);
        }

        #endregion
    }
}
