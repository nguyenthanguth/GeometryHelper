using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Arranging;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Arranging
{
    /// <summary>
    /// A scene moved elsewhere is arranged as it was, moved, also where it stands exactly on an edge: a label as high as
    /// the smallest one arranged, two labels as far from the middle of them all, two labels that touch. Each of these
    /// was settled by the last digits of the coordinates, which come out differently wherever the scene lies.
    /// </summary>
    public class ArrangeTiesWhereverTheSceneLiesTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        /// <summary>The places the scene is moved to: a hundred, none of them a round number, out to 1.2 million.</summary>
        private static IEnumerable<GeoVector2> Moves()
            => Enumerable.Range(1, 100).Select(k => new GeoVector2(k * 12345.6789 * (k % 2 == 0 ? 1 : -1), k * 9876.5432));

        private static ArrangeItem Moved(ArrangeItem item, GeoVector2 move)
            => new ArrangeItem
            {
                Leader = new GeoLine2(item.Leader.StartPoint + move, item.Leader.EndPoint + move),
                Box = new GeoRectangle2(item.Box.Center + move, item.Box.Width, item.Box.Height, item.Box.AngleRad),
                OffsetTop = item.OffsetTop,
                OffsetBottom = item.OffsetBottom,
                Side = item.Side,
            };

        private static void AssertArrangedAlike(ArrangeResult[] home, ArrangeResult[] moved, string where)
        {
            for (int i = 0; i < home.Length; i++)
            {
                Assert.True(home[i].Placed == moved[i].Placed && (home[i].Translation - moved[i].Translation).Length <= 1E-6,
                    $"label {i} {where}: {home[i]} and {moved[i]}");
            }
        }

        [Fact]
        public void ALabelAsHighAsTheSmallestArranged_HasItsPlacesWhereverItLies()
        {
            // A label 20 by 10 turned along a leader 60 long, the default minimum 10: measured from its rounded corners,
            // its height came to a hair under 10 at some places, and it was left where it stood, with no places at all.
            var options = new ArrangeOptions { Tolerance = Tol };
            var leader = new GeoLine2(0.0, 0.0, 60.0 * Math.Cos(0.3), 60.0 * Math.Sin(0.3));
            var label = new ArrangeItem { Leader = leader, Box = new GeoRectangle2(leader.MidPoint, 20.0, 10.0, 0.3), Offset = 5.0 };

            int home = label.GetPlacePoints(options).Count;

            Assert.Equal(246, home);
            foreach (GeoVector2 move in Moves())
            {
                Assert.True(home == Moved(label, move).GetPlacePoints(options).Count, $"moved by {move}");
            }
        }

        [Fact]
        public void TwoLabelsAsFarFromTheMiddle_ArePlacedInTheOrderGivenWhereverTheyLie()
        {
            // Two labels 24 by 10 on leaders 10.6 apart, each kept to the side facing the other, 0.3 off: their first
            // places are one and the same, centred at (15, 5.3), so the one placed second slides along. Both stand 5.3
            // from the middle of the two, and the first given goes first; far from the origin the last digits of the
            // middle put either first, a third of the time the second.
            var options = new ArrangeOptions { Tolerance = Tol };
            var lower = new GeoLine2(0.0, 0.0, 30.0, 0.0);
            var upper = new GeoLine2(0.0, 10.6, 30.0, 10.6);
            var labels = new[]
            {
                new ArrangeItem { Leader = lower, Box = new GeoRectangle2(lower.MidPoint, 24.0, 10.0), Offset = 0.3, Side = ArrangeSide.Top },
                new ArrangeItem { Leader = upper, Box = new GeoRectangle2(upper.MidPoint, 24.0, 10.0), Offset = 0.3, Side = ArrangeSide.Bottom },
            };

            ArrangeResult[] home = Arranger.Run(labels, options);

            Assert.Equal(new GeoVector2(0.0, 5.3), home[0].Translation);
            Assert.Equal(-26.4, home[1].Translation.X, 9);
            foreach (GeoVector2 move in Moves())
            {
                AssertArrangedAlike(home, Arranger.Run(labels.Select(label => Moved(label, move)).ToArray(), options), $"moved by {move}");
            }
        }

        [Fact]
        public void ADenseSheetMovedAlongX_IsArrangedAlike()
        {
            // 400 labels 24 by 10 on leaders 30 long, 28 apart, sliding in steps of 1.65. At its 58th place one of them
            // touches a label already placed, the boxes around the two 2.8E-14 apart at the origin and meeting once moved
            // 1 000 along X: there the place was free, and the label took it, and here it was not, and the label slid
            // one step further.
            ArrangeResult[] home = Arranger.Run(Dense(0.0), new ArrangeOptions { Tolerance = Tol });

            foreach (double dx in new[] { 1000.0, 1E5, 1E6 })
            {
                AssertArrangedAlike(home, Arranger.Run(Dense(dx), new ArrangeOptions { Tolerance = Tol }), $"moved by {dx} along X");
            }
        }

        [Fact]
        public void ALabelLeftAHairFromOneWithNowhereToGo_IsNotPlaced()
        {
            // Labels 20 by 10, placed left to right, each tried at its first place only. The left one stands free above
            // its leader. The right one, below a leader 30.0005 up, has a region on its place, and stays there, its box
            // 0.0005 from the box of the left one: half the tolerance, a collision. The final layout judged the left one
            // only against labels whose boxes overlap its own, and called it placed, so that two labels touching were
            // judged one way or the other as the rounding of where the scene lay left their boxes.
            var options = new ArrangeOptions { MaximumCandidates = 1, PlaceMostConstrainedFirst = false, PlaceFromInsideOut = false, Tolerance = Tol };
            var lower = new GeoLine2(0.0, 0.0, 100.0, 0.0);
            var upper = new GeoLine2(0.1, 30.0005, 100.1, 30.0005);
            var region = new[] { new GeoPolygon2(new GeoPoint2(45.0, 20.0), new GeoPoint2(55.0, 20.0), new GeoPoint2(55.0, 40.0), new GeoPoint2(45.0, 40.0)) };
            var labels = new[]
            {
                new ArrangeItem { Leader = lower, Box = new GeoRectangle2(lower.MidPoint, 20.0, 10.0), Offset = 5.0, Side = ArrangeSide.Top },
                new ArrangeItem { Leader = upper, Box = new GeoRectangle2(upper.MidPoint, 20.0, 10.0), Offset = 5.0, Side = ArrangeSide.Bottom, BlockPolygons = region },
            };

            ArrangeResult[] results = Arranger.Run(labels, options);

            Assert.Equal(new GeoVector2(0.0, 10.0), results[0].Translation);
            Assert.Equal(-10.0, results[1].Translation.Y, 9);
            Assert.True(labels[0].Box.Translate(results[0].Translation).CollidesWith(labels[1].Box.Translate(results[1].Translation), Tol));
            Assert.False(results[0].Placed);
            Assert.False(results[1].Placed);
        }

        /// <summary>
        /// The dense sheet of the benchmarks, moved by dx along X: every leader a line every label keeps clear of, and six
        /// regions among them.
        /// </summary>
        private static List<ArrangeItem> Dense(double dx)
        {
            var random = new Random(400);
            var leaders = new List<GeoLine2>();
            for (int i = 0; i < 20; i++)
            {
                for (int j = 0; j < 20; j++)
                {
                    double x = 28.0 * i + random.Next(10) + dx, y = 28.0 * j + random.Next(10);
                    leaders.Add(random.Next(2) == 0 ? new GeoLine2(x, y, x + 30, y) : new GeoLine2(x, y, x, y + 30));
                }
            }

            var regions = new List<GeoPolygon2>();
            for (int k = 0; k < 6; k++)
            {
                double x = random.Next(530) + dx, y = random.Next(530);
                double w = 20 + random.Next(30), h = 20 + random.Next(30);
                regions.Add(new GeoPolygon2(new GeoPoint2(x, y), new GeoPoint2(x + w, y), new GeoPoint2(x + w, y + h), new GeoPoint2(x, y + h)));
            }

            return leaders.Select(leader => new ArrangeItem
            {
                Leader = leader,
                Box = new GeoRectangle2(leader.MidPoint, 24.0, 10.0),
                Offset = 3.0,
                BlockPolygons = regions,
                BlockLines = leaders,
            }).ToList();
        }
    }
}
