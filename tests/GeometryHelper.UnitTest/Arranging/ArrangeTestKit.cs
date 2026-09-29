using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Arranging;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Arranging
{
    /// <summary>
    /// What the tests of label placement share: the algorithms, the small label most of them place, and the checks more
    /// than one class makes. The classes that use them derive from it, so that a theory names
    /// <see cref="AllAlgorithms"/> as its own.
    /// </summary>
    public abstract class ArrangeTestKit
    {
        /// <summary>Every algorithm, one row each, for a theory to run over.</summary>
        public static IEnumerable<object[]> AllAlgorithms()
            => Enum.GetValues(typeof(ArrangeAlgorithmType)).Cast<ArrangeAlgorithmType>().Select(algorithm => new object[] { algorithm });

        /// <summary>The options most tests run with: three rows on either side, 5 apart; the greedy algorithm unless told.</summary>
        public static ArrangeOptions OptionsFor(ArrangeAlgorithmType algorithm = ArrangeAlgorithmType.Greedy)
            => new ArrangeOptions { Algorithm = algorithm, RowGap = 5.0, PerpendicularLevels = 3 };

        /// <summary>A label 20 by 10 unless told otherwise, at the middle of its leader, 5 off either side.</summary>
        public static ArrangeItem LabelOn(GeoLine2 leader, double width = 20.0, double height = 10.0)
            => new ArrangeItem
            {
                Leader = leader,
                Box = new GeoRectangle2(leader.MidPoint, width, height),
                Offset = 5.0
            };

        /// <summary>The box of the label, moved by the translation a run gave it.</summary>
        public static GeoRectangle2 MovedBox(ArrangeItem arrange, GeoVector2 translation)
            => new GeoRectangle2(arrange.Box.Center + translation, arrange.Box.Width, arrange.Box.Height, arrange.Box.AngleRad);

        /// <summary>An axis-aligned rectangle as a region to keep clear of.</summary>
        public static GeoPolygon2 Rectangle(double x0, double y0, double x1, double y1)
            => new GeoPolygon2(new GeoPoint2(x0, y0), new GeoPoint2(x1, y0), new GeoPoint2(x1, y1), new GeoPoint2(x0, y1));

        /// <summary>
        /// Labels far apart, each on a leader of its own 100 long, its 20 by 10 box above and to the right of the
        /// middle of the leader, 5 off either side.
        /// </summary>
        public static List<ArrangeItem> LabelsApart(int count)
            => Enumerable.Range(0, count).Select(i => new ArrangeItem
            {
                Leader = new GeoLine2(200.0 * i, 0.0, 200.0 * i + 100.0, 0.0),
                Box = new GeoRectangle2(new GeoPoint2(200.0 * i + 90.0, 30.0), 20.0, 10.0),
                Offset = 5.0,
            }).ToList();

        /// <summary>
        /// Runs the labels and checks that each is placed at the candidate nearest to where its box stands, the first
        /// of them in their order where two are as near.
        /// </summary>
        public static void AssertEachGoesToItsNearestCandidate(List<ArrangeItem> items, ArrangeOptions options)
        {
            ArrangeResult[] results = Arranger.Run(items, options);

            for (int i = 0; i < items.Count; i++)
            {
                GeoPoint2 centre = items[i].Box.Center;
                GeoVector2 nearest = items[i].GetPlacePoints(options).Select(p => centre.GetVectorTo(p)).OrderBy(v => v.Length).First();

                Assert.True(results[i].Placed, $"label {i}: {results[i]}");
                Assert.True(nearest.Equals(results[i].Translation), $"label {i}: {results[i]}, not moved by {nearest}");
            }
        }
    }
}
