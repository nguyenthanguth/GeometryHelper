using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;
using GeometryHelper.Arranging;

namespace GeometryHelper.UnitTest.Arranging
{
    /// <summary>
    /// Runs the code shown in docs/arrange.md and checks what it says.
    /// <para>
    /// The README used to document an <c>IntersectsWith</c> family that never existed anywhere in the
    /// library, and it went unnoticed because nothing compiled it. Prose drifts silently; a test does not.
    /// Every snippet in the guide is reproduced here, so renaming or removing an API breaks the build
    /// rather than leaving a reader with instructions that cannot work.
    /// </para>
    /// </summary>
    public class ReadmeExamplesTests
    {
        [Fact]
        public void QuickStart_AnswersForEveryLabelAndLeavesTheItemsAlone()
        {
            var leader = new GeoLine2(0.0, 0.0, 2000.0, 0.0);

            var items = new List<ArrangeItem>
            {
                new ArrangeItem
                {
                    Box    = new GeoRectangle2(new GeoPoint2(1000.0, 0.0), 2000.0, 1000.0),
                    Leader = leader,
                    Offset = 50.0,
                    BlockPolygons = new List<GeoPolygon2>(),
                    BlockLines    = new List<GeoLine2>()
                }
            };

            ArrangeResult[] results = Arranger.Run(items);

            Assert.Equal(items.Count, results.Length);

            for (int i = 0; i < items.Count; i++)
            {
                GeoPoint2 newCentre = items[i].Box.Center + results[i].Translation;
                bool placed = results[i].Placed;

                // The guide promises the items are left as they were given...
                Assert.True(items[i].Box.Center.IsEqualTo(new GeoPoint2(1000.0, 0.0)));

                // ...and the first row at half the label's height plus its offset from the leader.
                Assert.Equal(1000.0, newCentre.X, 9);
                Assert.Equal(500.0 + 50.0, Math.Abs(newCentre.Y), 9);
                Assert.True(placed);
            }
        }

        [Fact]
        public void QuickStart_OptionsAndPerLabelOffset()
        {
            var leader = new GeoLine2(0.0, 0.0, 2000.0, 0.0);

            var options = new ArrangeOptions
            {
                Algorithm           = ArrangeAlgorithmType.BoundedBacktracking,
                RowGap              = 20.0,
                PerpendicularLevels = 3
            };

            var smallTextLabel = new ArrangeItem
            {
                Box    = new GeoRectangle2(new GeoPoint2(1000.0, 0.0), 2000.0, 1000.0),
                Leader = leader,
                Offset = 50.0
            };

            var largeTextLabel = new ArrangeItem
            {
                Box    = new GeoRectangle2(new GeoPoint2(1000.0, 0.0), 4000.0, 2000.0),
                Leader = leader,
                Offset = 200.0
            };

            ArrangeResult[] results = Arranger.Run(new List<ArrangeItem> { smallTextLabel, largeTextLabel }, options);

            Assert.Equal(2, results.Length);

            // The larger label carries the larger offset, so it has to end up further from the leader.
            double smallGap = smallTextLabel.Box.Translate(results[0].Translation).DistanceTo(leader);
            double largeGap = largeTextLabel.Box.Translate(results[1].Translation).DistanceTo(leader);
            Assert.True(largeGap > smallGap);
        }


        [Fact]
        public void QuickStart_AGapForEachSide()
        {
            var leader = new GeoLine2(0.0, 0.0, 2000.0, 0.0);

            var dimensionText = new ArrangeItem
            {
                Box = new GeoRectangle2(new GeoPoint2(1000.0, 0.0), 2000.0, 1000.0),
                Leader = leader,
                OffsetTop = 20.0,
                OffsetBottom = 300.0
            };

            List<GeoPoint2> points = dimensionText.GetPlacePoints(new ArrangeOptions());

            // Half the label's height (500) plus the gap of each side: 520 above, 800 below.
            Assert.True(points.Where(p => p.Y > 0.0).All(p => p.Y >= 520.0 - 1e-9));
            Assert.True(points.Where(p => p.Y < 0.0).All(p => p.Y <= -800.0 + 1e-9));
            Assert.Contains(points, p => p.IsEqualTo(new GeoPoint2(1000.0, 520.0)));
            Assert.Contains(points, p => p.IsEqualTo(new GeoPoint2(1000.0, -800.0)));

            ArrangeResult result = Arranger.Run(new List<ArrangeItem> { dimensionText })[0];
            Assert.True(result.Placed);
            Assert.Equal(520.0, (dimensionText.Box.Center + result.Translation).Y, 9);
        }
    }
}
