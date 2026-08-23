using System.Collections.Generic;
using System.Linq;
using CommonGeometry;
using CommonGeometry.Enums;
using PlaneGeometry.Core;
using PlaneGeometry.Geometry;
using Xunit;

namespace ArrangeAlgorithms.UnitTest
{
    /// <summary>
    /// Runs the code shown in README.md and checks the numbers it quotes.
    /// <para>
    /// The README used to document an <c>IntersectsWith</c> family that never existed anywhere in the
    /// library, and it went unnoticed because nothing compiled it. Prose drifts silently; a test does not.
    /// Every snippet in the README is reproduced here, so renaming or removing an API breaks the build
    /// rather than leaving a reader with instructions that cannot work.
    /// </para>
    /// </summary>
    public class ReadmeExamplesTests
    {

        [Fact]
        public void QuickStart_RunsAndFillsInEveryLabel()
        {
            var leader = new GeoLine2(0.0, 0.0, 2000.0, 0.0);

            var arranges = new List<Arrange>
            {
                new Arrange
                {
                    GeoRectangle2 = new GeoRectangle2(new GeoPoint2(1000.0, 0.0), 2000.0, 1000.0),
                    GeoLine2      = leader,
                    BaseOffsetFromLine = 50.0,
                    BlockPolygons = new List<GeoPolygon2>(),
                    BlockLines    = new List<GeoLine2>()
                }
            };

            List<GeoVector2> moves = Arrange.Run(arranges);

            Assert.Equal(arranges.Count, moves.Count);

            for (int i = 0; i < arranges.Count; i++)
            {
                GeoVector2 move = arranges[i].TranslationVector;
                GeoPoint2 newPosition = arranges[i].GeoRectangle2.Center + move;
                bool isPlaced = arranges[i].Placed;

                // The README promises the returned vector and the property are the same value.
                Assert.True(move.IsEqualTo(moves[i]));
                Assert.True(newPosition.IsEqualTo(arranges[i].GeoRectangle2.Center + moves[i]));
                Assert.True(isPlaced);
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

            var smallTextLabel = new Arrange
            {
                GeoRectangle2 = new GeoRectangle2(new GeoPoint2(1000.0, 0.0), 2000.0, 1000.0),
                GeoLine2      = leader,
                BaseOffsetFromLine = 50.0
            };

            var largeTextLabel = new Arrange
            {
                GeoRectangle2 = new GeoRectangle2(new GeoPoint2(1000.0, 0.0), 4000.0, 2000.0),
                GeoLine2      = leader,
                BaseOffsetFromLine = 200.0
            };

            List<GeoVector2> moves = Arrange.Run(new List<Arrange> { smallTextLabel, largeTextLabel }, options);

            Assert.Equal(2, moves.Count);

            // The larger label carries the larger offset, so it has to end up further from the guide.
            double smallGap = smallTextLabel.GeoRectangle2.Translate(moves[0]).DistanceTo(leader);
            double largeGap = largeTextLabel.GeoRectangle2.Translate(moves[1]).DistanceTo(leader);
            Assert.True(largeGap > smallGap);
        }
    }
}
