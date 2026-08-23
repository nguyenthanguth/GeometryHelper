using System.Linq;
using GeometryHelper.CommonGeometry;
using GeometryHelper.PlaneGeometry.Core;
using GeometryHelper.PlaneGeometry.Geometry;
using Xunit;

namespace GeometryHelper.PlaneGeometry.UnitTest.Core
{
    /// <summary>
    /// The promises the split overloads make to each other rather than to any one caller: that siblings
    /// answering the same question answer it the same way, and that the shortcuts one overload takes are
    /// taken by all of them.
    /// <para>
    /// Each case here corresponds to a place where two overloads had drifted apart. None of them was
    /// visible from inside a single overload's own tests, because each overload was self consistent; the
    /// disagreement only showed when two were put side by side.
    /// </para>
    /// </summary>
    public class SplitionContractTests
    {
        private static readonly Tolerance Tol = new Tolerance(1E-4, 1E-4);

        private static GeoLine2 Segment() => new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 0));

        private static GeoPolyline2 Path() => new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(10, 0));

        #region A point off the subject is refused, however it is handed in

        [Fact]
        public void PointArrayForm_RefusesAPointOffTheSubject_JustAsTheSinglePointFormDoes()
        {
            var offPath = new GeoPoint2(5, 3);

            // Both forms are asked to cut at a point three units above the segment. Projecting it would
            // give a perfectly plausible cut at (5, 0) that the caller never asked for.
            Assert.False(Segment().TrySplitBy(offPath, out GeoLine2 _, out GeoLine2 _, Tol));
            Assert.False(Segment().TrySplitBy(new[] { offPath }, out GeoLine2[] pieces, Tol));
            Assert.Single(pieces);
            Assert.Equal(Segment().Length, pieces[0].Length, 9);

            Assert.False(Path().TrySplitBy(offPath, out GeoPolyline2 _, out GeoPolyline2 _, Tol));
            Assert.False(Path().TrySplitBy(new[] { offPath }, out GeoPolyline2[] pathPieces, Tol));
            Assert.Single(pathPieces);
        }

        [Fact]
        public void PointArrayForm_KeepsThePointsThatAreOnTheSubject()
        {
            var points = new[]
            {
                new GeoPoint2(3, 0),      // on the segment
                new GeoPoint2(7, 3),      // off it, dropped
                new GeoPoint2(8, 0)       // on the segment
            };

            Assert.True(Segment().TrySplitBy(points, out GeoLine2[] pieces, Tol));

            Assert.Equal(3, pieces.Length);
            Assert.True(pieces[0].EndPoint.IsEqualTo(new GeoPoint2(3, 0), Tol));
            Assert.True(pieces[1].EndPoint.IsEqualTo(new GeoPoint2(8, 0), Tol));
            Assert.Equal(Segment().Length, pieces.Sum(p => p.Length), 9);
        }

        [Fact]
        public void PointArrayForm_HonoursToleranceWhenDecidingWhatIsOnTheSubject()
        {
            var slightlyOff = new GeoPoint2(5, 0.2);

            Assert.False(Segment().TrySplitBy(new[] { slightlyOff }, out GeoLine2[] _, Tol));
            Assert.True(Segment().TrySplitBy(new[] { slightlyOff }, out GeoLine2[] pieces, new Tolerance(0.5, 0.5)));
            Assert.Equal(2, pieces.Length);
        }

        #endregion

        #region A false return still leaves the out parameters usable

        [Fact]
        public void EveryOverload_HandsBackTheSubjectWhenNothingWasCut()
        {
            var missing = new GeoLine2(new GeoPoint2(50, -5), new GeoPoint2(50, 5));
            var farAway = new GeoPolygon2(
                new GeoPoint2(50, 50), new GeoPoint2(60, 50), new GeoPoint2(60, 60), new GeoPoint2(50, 60));

            // A caller who ignores the bool and iterates the result must not hit a null array on one
            // overload and a usable one on its neighbours.
            Assert.False(Path().TrySplitBy(missing, out GeoPolyline2[] byCutter, Tol));
            Assert.False(Path().TrySplitBy(new[] { missing }, out GeoPolyline2[] byCutterArray, Tol));
            Assert.False(Path().TrySplitBy(new GeoPoint2[0], out GeoPolyline2[] byPoints, Tol));

            foreach (GeoPolyline2[] result in new[] { byCutter, byCutterArray, byPoints })
            {
                Assert.NotNull(result);
                Assert.Single(result);
                Assert.Equal(Path().Length, result[0].Length, 9);
            }

            Assert.False(Segment().TrySplitBy(new[] { missing }, out GeoLine2[] lineByCutterArray, Tol));
            Assert.NotNull(lineByCutterArray);
            Assert.Single(lineByCutterArray);

            // The polygon forms fill both sides: one holds the subject, the other is empty but not null.
            Assert.False(Path().TrySplitBy(farAway, out GeoPolyline2[] inside, out GeoPolyline2[] outside, Tol));
            Assert.NotNull(inside);
            Assert.NotNull(outside);
            Assert.Empty(inside);
            Assert.Single(outside);
        }

        #endregion

        #region The line and polyline forms agree

        [Fact]
        public void MergingAcrossAdjacentPolygons_GivesTheSameShapeInBothForms()
        {
            // Two squares meeting at x = 2. The crossing there separates nothing, so the inside run is a
            // single straight stretch from 0 to 4 and the junction is an artefact of the cut.
            var poly1 = new GeoPolygon2(new GeoPoint2(0, -1), new GeoPoint2(2, -1), new GeoPoint2(2, 1), new GeoPoint2(0, 1));
            var poly2 = new GeoPolygon2(new GeoPoint2(2, -1), new GeoPoint2(4, -1), new GeoPoint2(4, 1), new GeoPoint2(2, 1));
            var cutters = new[] { poly1, poly2 };

            var asLine = new GeoLine2(new GeoPoint2(-5, 0), new GeoPoint2(15, 0));
            var asPath = new GeoPolyline2(new GeoPoint2(-5, 0), new GeoPoint2(15, 0));

            Assert.True(asLine.TrySplitBy(cutters, out GeoLine2[] lineInside, out GeoLine2[] lineOutside, Tol));
            Assert.True(asPath.TrySplitBy(cutters, out GeoPolyline2[] pathInside, out GeoPolyline2[] pathOutside, Tol));

            Assert.Equal(lineInside.Length, pathInside.Length);
            Assert.Equal(lineOutside.Length, pathOutside.Length);

            Assert.Single(pathInside);
            Assert.Equal(2, pathInside[0].VertexCount);
            Assert.True(pathInside[0][0].IsEqualTo(new GeoPoint2(0, 0), Tol));
            Assert.True(pathInside[0][1].IsEqualTo(new GeoPoint2(4, 0), Tol));

            Assert.True(lineInside[0].StartPoint.IsEqualTo(pathInside[0][0], Tol));
            Assert.True(lineInside[0].EndPoint.IsEqualTo(pathInside[0][pathInside[0].VertexCount - 1], Tol));
        }

        [Fact]
        public void MergingKeepsARealCorner()
        {
            // The path turns at (5, 5), which sits outside the polygon along with everything else here.
            // The two pieces either side of the graze rejoin, but the corner between them is part of the
            // subject and has to survive.
            var path = new GeoPolyline2(new GeoPoint2(0, 10), new GeoPoint2(5, 5), new GeoPoint2(10, 10));
            var square = new GeoPolygon2(
                new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 5), new GeoPoint2(0, 5));

            path.TrySplitBy(new[] { square }, out GeoPolyline2[] inside, out GeoPolyline2[] outside, Tol);

            Assert.Single(outside);
            Assert.Equal(3, outside[0].VertexCount);
            Assert.True(outside[0][1].IsEqualTo(new GeoPoint2(5, 5), Tol));
            Assert.Equal(path.Length, outside.Sum(p => p.Length) + inside.Sum(p => p.Length), 9);
        }

        [Fact]
        public void MergingUsesTheSuppliedToleranceNotTheGlobalOne()
        {
            // Reassembling pieces builds a new polyline, and building one through the public constructor
            // would filter its vertices against Tolerance.Global instead of the tolerance in hand.
            // Built before the global is widened: the constructors filter their own vertices against it,
            // so building them inside the block would corrupt the fixture rather than test the split.
            //
            // The legs are deliberately shorter than the widened global. Rebuilding the merged run
            // through the filtering constructor would collapse the corner at (2, 1) into nothing, which
            // is the whole point of reassembling through the trusted one instead.
            var path = new GeoPolyline2(new GeoPoint2(0, 3), new GeoPoint2(2, 1), new GeoPoint2(4, 3));
            var square = new GeoPolygon2(
                new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 1), new GeoPoint2(0, 1));

            Tolerance saved = Tolerance.Global;
            try
            {
                // Wider than the 2.83 long legs of the path above.
                Tolerance.Global = new Tolerance(3.0, 3.0);

                path.TrySplitBy(new[] { square }, out GeoPolyline2[] inside, out GeoPolyline2[] outside, Tol);

                Assert.Single(outside);
                Assert.Equal(3, outside[0].VertexCount);
                Assert.True(outside[0][1].IsEqualTo(new GeoPoint2(2, 1), Tol));
                Assert.Equal(path.Length, outside.Sum(p => p.Length) + inside.Sum(p => p.Length), 9);
            }
            finally
            {
                Tolerance.Global = saved;
            }
        }

        #endregion

        [Fact]
        public void TheArrayForms_TreatTheBoundaryAsInside_JustAsTheSingleCutterFormDoes()
        {
            // The single cutter overloads and the array overloads reach the classification through
            // different code, so the rule has to be pinned on both. A part lying on the boundary is
            // inside, matching Containment2.Contains.
            var square = new GeoPolygon2(
                new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10), new GeoPoint2(0, 10));
            var alongEdge = new GeoLine2(new GeoPoint2(2, 0), new GeoPoint2(8, 0));
            var pathAlongEdge = new GeoPolyline2(new GeoPoint2(2, 0), new GeoPoint2(8, 0));

            alongEdge.TrySplitBy(square, out GeoLine2[] singleIn, out GeoLine2[] singleOut, Tol);
            alongEdge.TrySplitBy(new[] { square }, out GeoLine2[] arrayIn, out GeoLine2[] arrayOut, Tol);
            pathAlongEdge.TrySplitBy(new[] { square }, out GeoPolyline2[] pathIn, out GeoPolyline2[] pathOut, Tol);

            Assert.Single(singleIn);
            Assert.Empty(singleOut);

            Assert.Single(arrayIn);
            Assert.Empty(arrayOut);

            Assert.Single(pathIn);
            Assert.Empty(pathOut);
        }

    }
}
