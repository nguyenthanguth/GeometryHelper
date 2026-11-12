using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;
using GeometryHelper.Packing;
using GeometryHelper.Packing.Algorithms;
using Xunit;

namespace GeometryHelper.UnitTest.Packing
{
    /// <summary>
    /// <see cref="PolygonJudge"/>: the check that a polygon reported placed is inside its sheet and clear of every other,
    /// the gap the spacing asks for kept. The packing never hands it a fault, so it is tried here on faults made on
    /// purpose.
    /// </summary>
    public class PolygonJudgeTests
    {
        private static readonly Sheet Paper = Sheet.Custom(200.0, 200.0);
        private static readonly Tolerance Tol = Tolerance.Default;

        private static PolygonSpot At(
            GeoPolygon2 polygon,
            int member,
            int cluster,
            bool kept = false,
            int sheet = 0,
            int group = 0
        ) => new PolygonSpot(group, member, sheet, cluster, kept, polygon);

        private static bool[] Judge(List<PolygonSpot> spots, double spacing = 0.0, double groupSpacing = 0.0) =>
            PolygonJudge.FindFaults(spots, Paper, Tol, spacing, groupSpacing);

        [Fact]
        public void ShapesApart_OrTouching_AreClear()
        {
            var spots = new List<PolygonSpot>
            {
                At(PolygonKit.Box(0, 0, 50, 50), 0, 0),
                At(PolygonKit.Box(50, 0, 50, 50), 1, 1),
                At(PolygonKit.Box(0, 50, 100, 50), 2, 2),
            };

            Assert.Equal(new[] { false, false, false }, Judge(spots));
        }

        [Fact]
        public void OverlappingShapes_AreBothFaults()
        {
            var spots = new List<PolygonSpot>
            {
                At(PolygonKit.Box(0, 0, 50, 50), 0, 0),
                At(PolygonKit.Box(40, 40, 20, 20), 1, 1),
                At(PolygonKit.Box(150, 150, 10, 10), 2, 2),
            };

            Assert.Equal(new[] { true, true, false }, Judge(spots));
        }

        [Fact]
        public void AShapeInTheNotchOfAnother_IsClear_AndOneThatPokesIntoItsArm_IsNot()
        {
            GeoPolygon2 el = PolygonKit.El(0, 0, 100, 30);

            var nested = new List<PolygonSpot> { At(el, 0, 0), At(PolygonKit.Box(30, 30, 70, 70), 1, 1) };
            var poking = new List<PolygonSpot> { At(el, 0, 0), At(PolygonKit.Box(29, 29, 70, 70), 1, 1) };

            Assert.Equal(new[] { false, false }, Judge(nested));
            Assert.Equal(new[] { true, true }, Judge(poking));
        }

        [Fact]
        public void TwoShapesTheBoxesOfWhichMeetButTheyDoNot_AreClear()
        {
            var spots = new List<PolygonSpot> { At(PolygonKit.Gamma(0, 60), 0, 0), At(PolygonKit.Gamma(30, 30), 1, 1) };

            Assert.Equal(new[] { false, false }, Judge(spots));
        }

        [Fact]
        public void AnOverlapWithinTheTolerance_IsClear_AndOneBeyondIt_IsNot()
        {
            var within = new List<PolygonSpot>
            {
                At(PolygonKit.Box(0, 0, 50, 50), 0, 0),
                At(PolygonKit.Box(49.9995, 0, 50, 50), 1, 1),
            };
            var beyond = new List<PolygonSpot>
            {
                At(PolygonKit.Box(0, 0, 50, 50), 0, 0),
                At(PolygonKit.Box(49.99, 0, 50, 50), 1, 1),
            };

            Assert.Equal(new[] { false, false }, Judge(within));
            Assert.Equal(new[] { true, true }, Judge(beyond));
        }

        [Fact]
        public void AShapeOutOfTheUsableArea_IsAFault()
        {
            var spots = new List<PolygonSpot>
            {
                At(PolygonKit.Box(-5, 0, 50, 50), 0, 0),
                At(PolygonKit.Box(100, 100, 100.0005, 50), 1, 1),
                At(PolygonKit.Box(100, 150, 100, 50), 2, 2),
            };

            Assert.Equal(new[] { true, false, false }, Judge(spots));
            Assert.Equal(new[] { true }, Judge(new List<PolygonSpot> { At(PolygonKit.Box(100, 160, 50, 50), 0, 0) }));
        }

        [Fact]
        public void TwoShapesCloserThanTheSpacing_AreFaults_AndAtIt_AreNot()
        {
            GeoPolygon2 left = PolygonKit.Box(0, 0, 50, 50);

            var near = new List<PolygonSpot> { At(left, 0, 0), At(PolygonKit.Box(53, 0, 50, 50), 1, 1) };
            var at = new List<PolygonSpot> { At(left, 0, 0), At(PolygonKit.Box(55, 0, 50, 50), 1, 1) };

            Assert.Equal(new[] { true, true }, Judge(near, groupSpacing: 5.0));
            Assert.Equal(new[] { false, false }, Judge(at, groupSpacing: 5.0));
        }

        [Fact]
        public void TheSpacingBetweenShapesOfOneBlock_IsNotTheGroupSpacing()
        {
            GeoPolygon2 left = PolygonKit.Box(0, 0, 50, 50),
                right = PolygonKit.Box(56, 0, 50, 50);

            var oneBlock = new List<PolygonSpot> { At(left, 0, 0), At(right, 1, 0) };
            var twoBlocks = new List<PolygonSpot> { At(left, 0, 0), At(right, 1, 1) };

            Assert.Equal(new[] { false, false }, Judge(oneBlock, spacing: 5.0, groupSpacing: 10.0));
            Assert.Equal(new[] { true, true }, Judge(twoBlocks, spacing: 5.0, groupSpacing: 10.0));
        }

        [Fact]
        public void TheShapesOfAKeptBlock_MayOverlap_ButNotAnotherBlock()
        {
            var spots = new List<PolygonSpot>
            {
                At(PolygonKit.Box(0, 0, 50, 50), 0, 0, kept: true),
                At(PolygonKit.Box(25, 25, 50, 50), 1, 0, kept: true),
                At(PolygonKit.Box(60, 60, 50, 50), 2, 1),
            };

            Assert.Equal(new[] { false, true, true }, Judge(spots));
        }

        [Fact]
        public void ShapesOnDifferentSheets_DoNotMeet()
        {
            var spots = new List<PolygonSpot>
            {
                At(PolygonKit.Box(0, 0, 50, 50), 0, 0),
                At(PolygonKit.Box(200, 0, 50, 50), 1, 1, sheet: 1),
            };

            Assert.Equal(new[] { false, false }, Judge(spots));
        }

        [Fact]
        public void ManyShapesInARow_AreJudgedAsOneByOne()
        {
            var spots = new List<PolygonSpot>();
            for (int i = 0; i < 40; i++)
            {
                spots.Add(At(PolygonKit.Box(i * 5.0, 0, 5, 150), i, i));
            }

            spots.Add(At(PolygonKit.Box(97, 100, 6, 6), 40, 40));

            bool[] faults = Judge(spots);

            Assert.True(faults[40]);
            Assert.True(faults[19]);
            Assert.True(faults[20]);
            Assert.False(faults[0]);
            Assert.False(faults[39]);
        }

        [Fact]
        public void ManyShapesInAColumn_AreJudgedAsOneByOne_WithoutComparingEveryPairOfThem()
        {
            // Each box is 150 wide and 0.05 high, a column of 3000 on a sheet 200 by 200: along X every box meets every
            // other, so a sweep along X would compare 4.5 million pairs; along Y each meets two. The sweep takes the axis
            // with the fewest.
            var spots = new List<PolygonSpot>();
            for (int i = 0; i < 3000; i++)
            {
                spots.Add(At(PolygonKit.Box(10, i * 0.06, 150, 0.05), i, i));
            }

            spots.Add(At(PolygonKit.Box(80, 90, 4, 0.2), 3000, 3000));

            var clock = System.Diagnostics.Stopwatch.StartNew();
            bool[] faults = Judge(spots);
            clock.Stop();

            Assert.True(faults[3000]);
            Assert.True(faults[1500]);
            Assert.True(faults[1501]);
            Assert.False(faults[0]);
            Assert.False(faults[2999]);
            Assert.InRange(faults.Count(f => f), 3, 5);
            Assert.InRange(clock.ElapsedMilliseconds, 0, 5000);
        }
    }
}
