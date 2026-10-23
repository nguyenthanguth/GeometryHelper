using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Arranging;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Arranging
{
    /// <summary>
    /// A run weighs how open each free place is at the tolerance of its options, not at the global one: clearances up to
    /// the options' point tolerance count as nought, and only those. The global tolerance this runs at is a thousandth;
    /// the options give a ten-thousandth.
    /// </summary>
    public class ArrangeOptionsToleranceTests
    {
        private static readonly Tolerance TenThousandth = new Tolerance(1E-4, 1E-5);

        /// <summary>
        /// A label 40 by 10 on a leader 60 long along X, 5 off it on either side, so that the first row above spans y = 5
        /// to 15 and the first below y = -15 to -5. A line kept clear of runs 0.0004 over the row above and another 0.0008
        /// under the row below.
        /// </summary>
        private static ArrangeItem Label()
        {
            var leader = new GeoLine2(0, 0, 60, 0);
            return new ArrangeItem
            {
                Leader = leader,
                Box = new GeoRectangle2(leader.MidPoint, 40, 10),
                Offset = 5,
                BlockLines = new List<GeoLine2>
                {
                    new GeoLine2(-20, 15.0004, 80, 15.0004),
                    new GeoLine2(-20, -15.0008, 80, -15.0008),
                },
            };
        }

        [Fact]
        public void TwoPlacesClearByMoreThanTheOptionsTolerance_TheMoreOpenWins()
        {
            // Above is tried first and is 0.0004 clear, below 0.0008: at a ten-thousandth both are free and below is the
            // more open. Read at the global thousandth, both counted as nought and above won.
            ArrangeResult result = Arranger.Run(new[] { Label() }, new ArrangeOptions { Tolerance = TenThousandth }).Single();

            Assert.True(result.Placed);
            Assert.Equal(0.0, result.Translation.X, 9);
            Assert.Equal(-10.0, result.Translation.Y, 9);
        }
    }
}
