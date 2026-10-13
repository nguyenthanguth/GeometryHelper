using System;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Plane.OffsetFaultShapes;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A round join keeps within <c>OffsetOptions.ArcTolerance</c> of the true arc, however fine that is asked.
    /// </summary>
    /// <remarks>
    /// Round joins were drawn in at most 1 024 chords a turn. A chord turning 2 pi / 1 024 on a radius of 80 sags
    /// 80 (1 - cos(pi / 1 024)) = 3.76E-4 below the arc, so the square of side 100 grown by 80 with an arc tolerance of
    /// 1E-4 missed it by almost four times: keeping within 1E-4 at that radius takes chords of at most
    /// 2 acos(1 - 1E-4 / 80) = 0.00316 radians, 1 987 a turn. At an arc tolerance of 1E-3 it takes 629, under the cap.
    /// </remarks>
    public class OffsetFaultS8RoundJoinTests
    {
        /// <summary>The worst gap between the grown square and the true offset by <paramref name="distance"/>.</summary>
        private static double WorstGap(GeoPolygon2 grown, GeoPolygon2 square, double distance)
            => WorstOffDistance(PiecesOf(grown), PiecesOf(square), distance);

        [Fact]
        public void ASquareGrownByEightyWithAFineArcTolerance_KeepsItsCornersWithinIt()
        {
            var square = new GeoPolygon2(P(0, 0), P(100, 0), P(100, 100), P(0, 100));
            var options = new OffsetOptions(OffsetJoin.Round, OffsetOptions.DefaultMiterLimit, 1E-4);

            GeoPolygon2 grown = Assert.Single(Offset2.Offset(square, 80.0, options, new Tolerance(1E-6, 1E-9)));

            Assert.True(WorstGap(grown, square, 80.0) <= 1E-4 + 1E-9, "the round corners stand " + WorstGap(grown, square, 80.0) + " off the arc");
        }

        [Fact]
        public void ASquareGrownByEightyWithACoarserArcTolerance_KeptWithinItAlready()
        {
            // Guard: 1E-3 needs 629 chords a turn, under the old cap of 1 024.
            var square = new GeoPolygon2(P(0, 0), P(100, 0), P(100, 100), P(0, 100));
            var options = new OffsetOptions(OffsetJoin.Round, OffsetOptions.DefaultMiterLimit, 1E-3);

            GeoPolygon2 grown = Assert.Single(Offset2.Offset(square, 80.0, options, new Tolerance(1E-6, 1E-9)));

            Assert.True(WorstGap(grown, square, 80.0) <= 1E-3 + 1E-9, "the round corners stand " + WorstGap(grown, square, 80.0) + " off the arc");
        }
    }
}
