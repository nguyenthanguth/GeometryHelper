using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Arranging;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Arranging
{
    /// <summary>
    /// A scene moved elsewhere is arranged as it was, moved: every label is tried at the same places, moved, and goes to
    /// the same one. The slides along a leader were stepped off one by one up to the reach of the label, so whether the
    /// last of them, at the reach itself, was tried came and went with the rounding of where the scene lay: a label on a
    /// leader 60 long had 234 places at the origin and 246 900 along X.
    /// </summary>
    public class ArrangeWhereverTheSceneLiesTests
    {
        private static readonly Tolerance TenThousandth = new Tolerance(1E-4, 1E-4, Tolerance.DefaultEqualAngleRad, 1E-4);

        /// <summary>
        /// Thirty labels on leaders 60 long, turned 0.7 further each, in rows over a region 50 by 40, the whole moved by
        /// (dx, dy).
        /// </summary>
        private static List<ArrangeItem> Crowd(double dx, double dy)
        {
            var regions = new[] { new GeoPolygon2(new GeoPoint2(40 + dx, 40 + dy), new GeoPoint2(90 + dx, 40 + dy), new GeoPoint2(90 + dx, 80 + dy), new GeoPoint2(40 + dx, 80 + dy)) };
            var items = new List<ArrangeItem>();

            for (int i = 0; i < 30; i++)
            {
                double a = i * 0.7;
                var start = new GeoPoint2(20 * (i % 6) + dx, 25 * (i / 6) + dy);
                var leader = new GeoLine2(start, new GeoPoint2(start.X + 60 * Math.Cos(a), start.Y + 60 * Math.Sin(a)));
                items.Add(new ArrangeItem
                {
                    Leader = leader,
                    Box = new GeoRectangle2(leader.MidPoint, 18.0 + (i % 4) * 3, 8.0 + (i % 3) * 2, (i % 5) * 0.2),
                    Offset = 3.0 + (i % 7),
                    BlockPolygons = regions,
                });
            }

            return items;
        }

        /// <summary>
        /// A sheet of 20 to 50 labels drawn at random over 600 by 400 among four regions, each label kept clear of every
        /// leader, the whole moved by (dx, dy), with the options drawn for it.
        /// </summary>
        private static List<ArrangeItem> Sheet(int seed, double dx, double dy, out ArrangeOptions options)
        {
            var random = new Random(seed);
            int count = 20 + random.Next(30);

            var regions = new List<GeoPolygon2>();
            for (int k = 0; k < 4; k++)
            {
                double x = random.NextDouble() * 600 + dx, y = random.NextDouble() * 400 + dy;
                double w = 10 + random.NextDouble() * 60, h = 10 + random.NextDouble() * 60;
                regions.Add(new GeoPolygon2(new GeoPoint2(x, y), new GeoPoint2(x + w, y), new GeoPoint2(x + w, y + h), new GeoPoint2(x, y + h)));
            }

            var leaders = new List<GeoLine2>();
            var angles = new List<double>();
            for (int i = 0; i < count; i++)
            {
                double sx = random.NextDouble() * 600, sy = random.NextDouble() * 400;
                double a = random.Next(4) == 0 ? random.Next(4) * Math.PI / 2 : random.NextDouble() * 2 * Math.PI;
                double length = 20 + random.NextDouble() * 150;
                leaders.Add(new GeoLine2(new GeoPoint2(sx + dx, sy + dy), new GeoPoint2(sx + length * Math.Cos(a) + dx, sy + length * Math.Sin(a) + dy)));
                angles.Add(Math.Atan2(length * Math.Sin(a), length * Math.Cos(a)));
            }

            var items = new List<ArrangeItem>();
            for (int i = 0; i < count; i++)
            {
                GeoLine2 leader = leaders[i];
                double width = 15 + random.NextDouble() * 40, height = 8 + random.NextDouble() * 12;
                GeoPoint2 centre = random.Next(3) == 0
                    ? new GeoPoint2(leader.MidPoint.X + (random.NextDouble() - 0.5) * 40, leader.MidPoint.Y + (random.NextDouble() - 0.5) * 40)
                    : leader.MidPoint;
                items.Add(new ArrangeItem
                {
                    Leader = leader,
                    Box = new GeoRectangle2(centre, width, height, random.Next(2) == 0 ? angles[i] : 0.0),
                    Offset = random.Next(5) == 0 ? 50.0 : random.NextDouble() * 30,
                    BlockPolygons = regions,
                    BlockLines = leaders,
                });
            }

            options = new ArrangeOptions
            {
                RowGap = random.NextDouble() * 20,
                PerpendicularLevels = 1 + random.Next(4),
                LookAheadCandidates = 1 + random.Next(5),
                PlaceMostConstrainedFirst = random.Next(3) != 0,
                PlaceFromInsideOut = random.Next(2) == 0,
                LongitudinalOvershootRatio = random.Next(3) == 0 ? 0.3 : 0.75,
                Tolerance = TenThousandth,
            };

            return items;
        }

        private static void AssertArrangedAlike(List<ArrangeItem> home, List<ArrangeItem> moved, ArrangeOptions homeOptions, ArrangeOptions movedOptions, double dx, double dy)
        {
            for (int i = 0; i < home.Count; i++)
            {
                List<GeoPoint2> at = home[i].GetPlacePoints(homeOptions);
                List<GeoPoint2> movedAt = moved[i].GetPlacePoints(movedOptions);

                Assert.Equal(at.Count, movedAt.Count);

                for (int k = 0; k < at.Count; k++)
                {
                    Assert.True(Math.Abs(movedAt[k].X - dx - at[k].X) <= 1E-6 && Math.Abs(movedAt[k].Y - dy - at[k].Y) <= 1E-6, $"label {i}, place {k}: {at[k]} and {movedAt[k]}");
                }
            }

            ArrangeResult[] placed = Arranger.Run(home, homeOptions);
            ArrangeResult[] movedPlaced = Arranger.Run(moved, movedOptions);

            for (int i = 0; i < placed.Length; i++)
            {
                Assert.Equal(placed[i].Placed, movedPlaced[i].Placed);
                Assert.True((placed[i].Translation - movedPlaced[i].Translation).Length <= 1E-6, $"label {i}: {placed[i].Translation} and {movedPlaced[i].Translation}");
            }
        }

        [Theory]
        [InlineData(900.0, 0.0)]
        [InlineData(900.0, 900.0)]
        [InlineData(1E5, 0.0)]
        [InlineData(1E5, 1E5)]
        [InlineData(1E6, 0.0)]
        [InlineData(1E6, 1E6)]
        public void ACrowdMovedElsewhere_IsTriedAtTheSamePlacesAndArrangedAlike(double dx, double dy)
        {
            var options = new ArrangeOptions { RowGap = 5.0, Tolerance = TenThousandth };

            AssertArrangedAlike(Crowd(0.0, 0.0), Crowd(dx, dy), options, new ArrangeOptions { RowGap = 5.0, Tolerance = TenThousandth }, dx, dy);
        }

        [Theory]
        [InlineData(900.0, 0.0)]
        [InlineData(900.0, 900.0)]
        [InlineData(1E5, 0.0)]
        [InlineData(1E5, 1E5)]
        [InlineData(1E6, 0.0)]
        [InlineData(1E6, 1E6)]
        public void ASheetMovedElsewhere_IsTriedAtTheSamePlacesAndArrangedAlike(double dx, double dy)
        {
            for (int seed = 1; seed <= 6; seed++)
            {
                List<ArrangeItem> home = Sheet(seed, 0.0, 0.0, out ArrangeOptions homeOptions);
                List<ArrangeItem> moved = Sheet(seed, dx, dy, out ArrangeOptions movedOptions);

                AssertArrangedAlike(home, moved, homeOptions, movedOptions, dx, dy);
            }
        }

        [Fact]
        public void ALabelWhoseReachIsTwo_SlidesAlikeAtTheOriginAndFarFromIt()
        {
            // A leader 2.5 long and a label 1 by 0.4 turned to it: the label reaches half the leader and three quarters of its
            // width, 2, each way, so it slides twenty steps of a twentieth each way. Moved 196 017.7 along X and 18 214.2 along
            // Y, the reach came to a hair under 2, and the label slid steps of its height, 66 places in all against 246.
            var options = new ArrangeOptions { MinimumBoxSize = 0.01, Tolerance = Tolerance.Default };

            ArrangeItem Label(double dx, double dy)
            {
                var leader = new GeoLine2(0.37 + dx, 0.11 + dy, 1.87 + dx, 2.11 + dy);
                return new ArrangeItem { Leader = leader, Box = new GeoRectangle2(leader.MidPoint, 1.0, 0.4, Math.Atan2(2.0, 1.5)), Offset = 0.2 };
            }

            List<GeoPoint2> home = Label(0.0, 0.0).GetPlacePoints(options);
            List<GeoPoint2> moved = Label(196017.7, 18214.2).GetPlacePoints(options);

            Assert.Equal(246, home.Count);
            Assert.Equal(home.Count, moved.Count);
        }
    }
}
