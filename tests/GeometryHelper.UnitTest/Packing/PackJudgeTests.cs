using System.Collections.Generic;
using GeometryHelper.Packing;
using GeometryHelper.Packing.Algorithms;
using Xunit;

namespace GeometryHelper.UnitTest.Packing
{
    /// <summary>
    /// <see cref="PackJudge"/>: the check that a box reported placed is inside its sheet and clear of every other. The
    /// packing never hands it a fault, so it is tried here on faults made on purpose.
    /// </summary>
    public class PackJudgeTests
    {
        private static readonly Sheet Paper = Sheet.Custom(100.0, 100.0);

        private static Spot At(int member, int cluster, bool kept, double x, double y, double width, double height, int sheet = 0)
            => new Spot(0, member, sheet, cluster, kept, new Footprint(x, y, width, height));

        [Fact]
        public void BoxesApart_OrTouching_AreClear()
        {
            var spots = new List<Spot> { At(0, 0, false, 0, 0, 50, 50), At(1, 1, false, 50, 0, 50, 50), At(2, 2, false, 0, 50, 100, 50) };

            Assert.Equal(new[] { false, false, false }, PackJudge.FindFaults(spots, Paper, 1e-9));
        }

        [Fact]
        public void OverlappingBoxes_AreBothFaults()
        {
            var spots = new List<Spot> { At(0, 0, false, 0, 0, 50, 50), At(1, 1, false, 49, 49, 20, 20), At(2, 2, false, 80, 80, 10, 10) };

            Assert.Equal(new[] { true, true, false }, PackJudge.FindFaults(spots, Paper, 1e-9));
        }

        [Fact]
        public void BoxesOnDifferentSheets_DoNotMeet()
        {
            var spots = new List<Spot> { At(0, 0, false, 0, 0, 50, 50), At(1, 1, false, 150, 0, 50, 50, sheet: 1) };

            Assert.Equal(new[] { false, false }, PackJudge.FindFaults(spots, Paper, 1e-9));
        }

        [Fact]
        public void TheBoxesOfAKeptBlock_MayOverlap_ButNotThoseOfAnother()
        {
            var spots = new List<Spot> { At(0, 0, true, 0, 0, 50, 50), At(1, 0, true, 25, 25, 50, 50), At(2, 1, true, 60, 60, 30, 30) };

            Assert.Equal(new[] { false, true, true }, PackJudge.FindFaults(spots, Paper, 1e-9));
        }

        [Fact]
        public void ABoxPastTheUsableArea_IsAFault_WithinTheToleranceItIsNot()
        {
            var spots = new List<Spot> { At(0, 0, false, -1, 0, 20, 20), At(1, 1, false, 80, 80, 20.0000001, 20), At(2, 2, false, 50, 0, 10, 10) };

            Assert.Equal(new[] { true, false, false }, PackJudge.FindFaults(spots, Paper, 1e-6));
        }
    }
}
