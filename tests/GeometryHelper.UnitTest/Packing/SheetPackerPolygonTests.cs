using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;
using GeometryHelper.Packing;
using Xunit;

namespace GeometryHelper.UnitTest.Packing
{
    /// <summary>
    /// <see cref="SheetPacker.Pack(IReadOnlyList{GeoPolygon2[]}, Sheet, PackOptions)"/>: views cut to the shape of a polygon
    /// are packed by that shape, so that an L- or U-shaped view takes the notch of another.
    /// </summary>
    public class SheetPackerPolygonTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static PackOptions Options() => new PackOptions { Tolerance = Tol };

        private static List<GeoPolygon2[]> OneEach(params GeoPolygon2[] shapes) =>
            shapes.Select(s => new[] { s }).ToList();

        private static double Overlap(GeoPolygon2 a, GeoPolygon2 b) => a.Intersect(b, Tol).Sum(f => f.Area);

        private static bool BoxesMeet(GeoRectangle2 a, GeoRectangle2 b) =>
            a.LowerLeft.X < b.UpperRight.X
            && b.LowerLeft.X < a.UpperRight.X
            && a.LowerLeft.Y < b.UpperRight.Y
            && b.LowerLeft.Y < a.UpperRight.Y;

        private static void AssertInsideUsableArea(PackResult result, PackPlacement placement)
        {
            GeoRectangle2 area = result.Sheets[placement.SheetIndex].UsableArea;
            Assert.InRange(placement.ViewBox.LowerLeft.X, area.LowerLeft.X - 1e-3, double.MaxValue);
            Assert.InRange(placement.ViewBox.LowerLeft.Y, area.LowerLeft.Y - 1e-3, double.MaxValue);
            Assert.InRange(placement.ViewBox.UpperRight.X, double.MinValue, area.UpperRight.X + 1e-3);
            Assert.InRange(placement.ViewBox.UpperRight.Y, double.MinValue, area.UpperRight.Y + 1e-3);
        }

        [Fact]
        public void TwoLShapes_NestSoTheyUseLessWidthThanTheirBoxes()
        {
            var sheet = Sheet.Custom(130.0, 130.0);
            List<GeoPolygon2[]> groups = OneEach(PolygonKit.Gamma(500, 700), PolygonKit.Gamma(-300, -80));

            PackResult result = SheetPacker.Pack(groups, sheet, Options());

            PackPlacement first = result.Placements[0][0],
                second = result.Placements[1][0];
            Assert.True(first.Placed);
            Assert.True(second.Placed);
            Assert.Equal(0, first.SheetIndex);
            Assert.Equal(0, second.SheetIndex);
            Assert.Single(result.Sheets);

            double width =
                Math.Max(first.ViewBox.UpperRight.X, second.ViewBox.UpperRight.X)
                - Math.Min(first.ViewBox.LowerLeft.X, second.ViewBox.LowerLeft.X);
            Assert.InRange(width, 129.9, 130.0 + 1e-3);
            Assert.True(BoxesMeet(first.ViewBox, second.ViewBox), "the second view stands in the first one's box");
            Assert.InRange(Overlap(first.ViewPolygon, second.ViewPolygon), 0.0, 1e-3);
            Assert.InRange(first.ViewPolygon.DistanceTo(second.ViewPolygon), 0.0, 1e-2);
        }

        [Fact]
        public void TwoLShapes_NestInOneSheetWhereTheirBoxesNeedTwo()
        {
            var sheet = Sheet.Custom(130.0, 130.0);
            var boxes = new List<GeoRectangle2[]>
            {
                new[] { new GeoRectangle2(500.0, 700.0, 100.0, 100.0) },
                new[] { new GeoRectangle2(-300.0, -80.0, 100.0, 100.0) },
            };

            Assert.Equal(2, SheetPacker.Pack(boxes, sheet, Options()).Sheets.Count);
            Assert.Single(
                SheetPacker
                    .Pack(OneEach(PolygonKit.Gamma(500, 700), PolygonKit.Gamma(-300, -80)), sheet, Options())
                    .Sheets
            );
        }

        [Fact]
        public void ABoxSmallEnoughForTheNotchOfAnL_GoesIntoIt()
        {
            var sheet = Sheet.Custom(100.0, 100.0);
            List<GeoPolygon2[]> groups = OneEach(PolygonKit.El(900, 900), PolygonKit.Box(-50, -50, 60, 60));

            PackResult result = SheetPacker.Pack(groups, sheet, Options());

            PackPlacement box = result.Placements[1][0];
            Assert.True(box.Placed);
            Assert.Equal(0, box.SheetIndex);
            Assert.Single(result.Sheets);
            Assert.Equal(30.0, box.ViewBox.LowerLeft.X, 2);
            Assert.Equal(40.0, box.ViewBox.LowerLeft.Y, 2);
            Assert.Equal(100.0, box.ViewBox.UpperRight.Y, 2);
            Assert.InRange(Overlap(result.Placements[0][0].ViewPolygon, box.ViewPolygon), 0.0, 1e-3);
        }

        [Fact]
        public void TheSpacing_IsAClearGapBetweenTwoNestedLShapes()
        {
            var sheet = Sheet.Custom(150.0, 150.0);
            var options = new PackOptions
            {
                Spacing = 5.0,
                GroupSpacing = 5.0,
                Tolerance = Tol,
            };

            PackResult result = SheetPacker.Pack(
                OneEach(PolygonKit.Gamma(500, 700), PolygonKit.Gamma(-300, -80)),
                sheet,
                options
            );

            PackPlacement first = result.Placements[0][0],
                second = result.Placements[1][0];
            Assert.Single(result.Sheets);
            Assert.True(BoxesMeet(first.ViewBox, second.ViewBox), "still nested");
            double gap = first.ViewPolygon.DistanceTo(second.ViewPolygon);
            Assert.InRange(gap, 5.0 - 1e-3, 5.0 + 1e-2);
        }

        [Fact]
        public void TheGroupSpacing_IsTheGapBetweenGroups_AndTheSpacingBetweenTheShapesOfOne()
        {
            var sheet = Sheet.Custom(300.0, 300.0);
            var options = new PackOptions
            {
                Spacing = 5.0,
                GroupSpacing = 20.0,
                Tolerance = Tol,
            };
            var groups = new List<GeoPolygon2[]>
            {
                new[] { PolygonKit.Box(0, 0, 40, 40), PolygonKit.Box(900, 0, 40, 40) },
                new[] { PolygonKit.Box(-700, 50, 40, 40) },
            };

            PackResult result = SheetPacker.Pack(groups, sheet, options);

            GeoPolygon2 a = result.Placements[0][0].ViewPolygon,
                b = result.Placements[0][1].ViewPolygon,
                c = result.Placements[1][0].ViewPolygon;
            Assert.InRange(a.DistanceTo(b), 5.0 - 1e-3, 5.0 + 1e-2);
            Assert.InRange(a.DistanceTo(c), 20.0 - 1e-3, double.MaxValue);
            Assert.InRange(b.DistanceTo(c), 20.0 - 1e-3, double.MaxValue);
        }

        [Fact]
        public void TheShapesOfAGroup_StayNearEachOther()
        {
            var sheet = Sheet.Custom(300.0, 300.0);
            var options = new PackOptions
            {
                Spacing = 5.0,
                GroupSpacing = 20.0,
                Tolerance = Tol,
            };
            var groups = new List<GeoPolygon2[]>
            {
                Enumerable.Range(0, 4).Select(i => PolygonKit.Box(i * 1000, 0, 50, 50)).ToArray(),
                Enumerable.Range(0, 4).Select(i => PolygonKit.Box(i * 1000, 500, 50, 50)).ToArray(),
            };

            PackResult result = SheetPacker.Pack(groups, sheet, options);

            for (int g = 0; g < 2; g++)
            {
                var members = result.Placements[g].Select(p => p.ViewBox).ToList();
                double width = members.Max(m => m.UpperRight.X) - members.Min(m => m.LowerLeft.X);
                double height = members.Max(m => m.UpperRight.Y) - members.Min(m => m.LowerLeft.Y);
                Assert.InRange(width, 0.0, 105.0 + 1e-2);
                Assert.InRange(height, 0.0, 105.0 + 1e-2);
            }
        }

        [Fact]
        public void WhenASheetIsFull_TheNextIsBegunBesideIt()
        {
            var sheet = Sheet.Custom(100.0, 100.0);
            sheet.SheetSpacing = 10.0;

            PackResult result = SheetPacker.Pack(
                OneEach(PolygonKit.Box(0, 0, 60, 60), PolygonKit.Box(0, 0, 60, 60), PolygonKit.Box(0, 0, 60, 60)),
                sheet,
                Options()
            );

            Assert.Equal(3, result.Sheets.Count);
            for (int i = 0; i < 3; i++)
            {
                PackPlacement placement = result.Placements[i][0];
                Assert.True(placement.Placed);
                Assert.Equal(i, placement.SheetIndex);
                AssertInsideUsableArea(result, placement);
            }

            Assert.Equal(110.0, result.Sheets[1].Bounds.LowerLeft.X, 6);
            Assert.Equal(220.0, result.Sheets[2].Bounds.LowerLeft.X, 6);
        }

        [Fact]
        public void AShapeLargerThanTheUsableArea_IsNotPlaced()
        {
            var sheet = Sheet.Custom(100.0, 100.0);
            GeoPolygon2 tooWide = PolygonKit.Gamma(10, 20, 120, 30);
            List<GeoPolygon2[]> groups = OneEach(tooWide, PolygonKit.Box(0, 0, 40, 40));

            PackResult result = SheetPacker.Pack(groups, sheet, Options());

            PackPlacement lost = result.Placements[0][0];
            Assert.False(lost.Placed);
            Assert.Equal(-1, lost.SheetIndex);
            Assert.Equal(GeoVector2.Zero, lost.Translation);
            Assert.Equal(tooWide, lost.ViewPolygon);
            Assert.True(result.Placements[1][0].Placed);
        }

        [Fact]
        public void TheOffsetsOfTheSheet_KeepEveryShapeInsideTheUsableArea()
        {
            var sheet = new Sheet(PaperSize.A4)
            {
                OffsetLeft = 20,
                OffsetRight = 10,
                OffsetTop = 10,
                OffsetBottom = 10,
            };

            PackResult result = SheetPacker.Pack(PolygonKit.Views(7, 12, 90.0), sheet, Options());

            foreach (PackPlacement placement in result.Placements.SelectMany(g => g))
            {
                Assert.True(placement.Placed);
                AssertInsideUsableArea(result, placement);
            }
        }

        [Fact]
        public void ThePlacementOfAPolygon_GivesItsMovedShapeAndTheBoxRoundIt()
        {
            var sheet = Sheet.Custom(300.0, 300.0);
            GeoPolygon2 shape = PolygonKit.U(800, -400, 120, 90, 30);

            PackPlacement placement = SheetPacker.Pack(OneEach(shape), sheet, Options()).Placements[0][0];

            Assert.Equal(shape.Translate(placement.Translation), placement.ViewPolygon);
            var bounds = PolygonKit.Bounds(placement.ViewPolygon);
            Assert.Equal(bounds.MinX, placement.ViewBox.LowerLeft.X, 9);
            Assert.Equal(bounds.MinY, placement.ViewBox.LowerLeft.Y, 9);
            Assert.Equal(bounds.MaxX, placement.ViewBox.UpperRight.X, 9);
            Assert.Equal(bounds.MaxY, placement.ViewBox.UpperRight.Y, 9);
            Assert.Equal(0.0, placement.ViewBox.AngleRad);
        }

        [Fact]
        public void ThePlacementOfABox_GivesTheMovedBoxAsAPolygonOfFourPoints()
        {
            var sheet = Sheet.Custom(300.0, 300.0);
            var box = new GeoRectangle2(new GeoPoint2(700.0, -50.0), 80.0, 40.0, 0.0);

            PackPlacement placement = SheetPacker
                .Pack(new List<GeoRectangle2[]> { new[] { box } }, sheet, Options())
                .Placements[0][0];

            GeoPolygon2 polygon = placement.ViewPolygon;
            Assert.Equal(4, polygon.VertexCount);
            Assert.Equal(placement.ViewBox.LowerLeft, polygon[0]);
            Assert.Equal(placement.ViewBox.LowerRight, polygon[1]);
            Assert.Equal(placement.ViewBox.UpperRight, polygon[2]);
            Assert.Equal(placement.ViewBox.UpperLeft, polygon[3]);
            Assert.Equal(3200.0, polygon.Area, 6);
            Assert.True(polygon.SignedArea > 0.0);
        }

        [Fact]
        public void ThePlacementOfABoxMadeByHand_HasThePolygonOfItsBox()
        {
            var box = new GeoRectangle2(10.0, 20.0, 30.0, 40.0);
            var placement = new PackPlacement(0, GeoVector2.Zero, box, true);

            Assert.Equal(1200.0, placement.ViewPolygon.Area, 6);
            Assert.Equal(placement, new PackPlacement(0, GeoVector2.Zero, box, true));
            Assert.Equal(placement, new PackPlacement(0, GeoVector2.Zero, box, placement.ViewPolygon, true));
        }

        [Fact]
        public void TheSameShapesAndOptions_GiveTheSameResultToTheBit()
        {
            var sheet = new Sheet(PaperSize.A3)
            {
                OffsetLeft = 10,
                OffsetRight = 10,
                OffsetTop = 10,
                OffsetBottom = 10,
            };
            var options = new PackOptions
            {
                Spacing = 3.0,
                GroupSpacing = 7.0,
                Tolerance = Tol,
            };
            List<GeoPolygon2[]> groups = PolygonKit.Views(11, 30, 90.0, 3);

            PackResult first = SheetPacker.Pack(groups, sheet, options);
            PackResult second = SheetPacker.Pack(groups, sheet, options);

            Assert.Equal(first.Utilization, second.Utilization);
            Assert.Equal(first.Sheets.Count, second.Sheets.Count);
            for (int g = 0; g < groups.Count; g++)
            {
                for (int i = 0; i < groups[g].Length; i++)
                {
                    Assert.Equal(first.Placements[g][i], second.Placements[g][i]);
                }
            }
        }

        [Fact]
        public void IdenticalShapesInAnyOrder_TakeTheSamePlaces()
        {
            var sheet = Sheet.Custom(250.0, 160.0);
            var spots = new List<(double X, double Y)> { (0, 0), (900, 40), (-600, 30), (50, -700), (3000, 3000) };
            var forward = new List<GeoPolygon2[]>();
            foreach ((double x, double y) in spots)
            {
                forward.Add(new[] { PolygonKit.Gamma(x, y) });
            }

            List<GeoPolygon2[]> backward = Enumerable.Reverse(forward).ToList();

            PackResult a = SheetPacker.Pack(forward, sheet, Options());
            PackResult b = SheetPacker.Pack(backward, sheet, Options());

            List<(int, double, double)> Places(PackResult r) =>
                r
                    .Placements.Select(g => g[0])
                    .Select(p =>
                        (p.SheetIndex, Math.Round(p.ViewBox.LowerLeft.X, 6), Math.Round(p.ViewBox.LowerLeft.Y, 6))
                    )
                    .OrderBy(t => t.SheetIndex)
                    .ThenBy(t => t.Item2)
                    .ThenBy(t => t.Item3)
                    .ToList();

            Assert.Equal(Places(a), Places(b));
        }

        [Fact]
        public void TwoShapesTheSizeOfTheSheet_TakeASheetEach()
        {
            var sheet = Sheet.Custom(100.0, 100.0);

            PackResult result = SheetPacker.Pack(
                OneEach(PolygonKit.Box(5, 5, 100, 100), PolygonKit.Box(-5, -5, 100, 100)),
                sheet,
                Options()
            );

            Assert.Equal(0, result.Placements[0][0].SheetIndex);
            Assert.Equal(1, result.Placements[1][0].SheetIndex);
        }

        [Fact]
        public void FromTheLowerRight_TheFirstShapeSitsInThatCorner()
        {
            var sheet = new Sheet(PaperSize.A4)
            {
                OffsetLeft = 20,
                OffsetRight = 10,
                OffsetTop = 10,
                OffsetBottom = 10,
            };
            var options = new PackOptions { StartCorner = SheetCorner.LowerRight, Tolerance = Tol };

            PackResult result = SheetPacker.Pack(OneEach(PolygonKit.El(0, 0, 100, 30)), sheet, options);

            PackPlacement placement = result.Placements[0][0];
            GeoRectangle2 area = result.Sheets[0].UsableArea;
            Assert.Equal(area.LowerRight.X, placement.ViewBox.LowerRight.X, 2);
            Assert.Equal(area.LowerRight.Y, placement.ViewBox.LowerRight.Y, 2);
        }

        [Theory]
        [InlineData(SheetCorner.UpperLeft)]
        [InlineData(SheetCorner.UpperRight)]
        [InlineData(SheetCorner.LowerLeft)]
        [InlineData(SheetCorner.LowerRight)]
        public void FromAnyCorner_NoTwoShapesOverlapAndEachIsInsideTheUsableArea(SheetCorner corner)
        {
            var sheet = new Sheet(PaperSize.A3)
            {
                OffsetLeft = 10,
                OffsetRight = 10,
                OffsetTop = 10,
                OffsetBottom = 10,
            };
            var options = new PackOptions
            {
                StartCorner = corner,
                Spacing = 2.0,
                GroupSpacing = 4.0,
                Tolerance = Tol,
            };

            PackResult result = SheetPacker.Pack(PolygonKit.Views(3, 25, 80.0, 2), sheet, options);

            AssertClear(result, 2.0, 4.0);
        }

        [Fact]
        public void KeepingAGroup_MovesItsShapesAsOneBlock()
        {
            var sheet = Sheet.Custom(300.0, 300.0);
            var options = new PackOptions { GroupLayout = GroupLayout.Keep, Tolerance = Tol };
            var groups = new List<GeoPolygon2[]>
            {
                new[] { PolygonKit.El(-500, -500, 100, 30), PolygonKit.Box(-350, -480, 60, 60) },
            };

            PackResult result = SheetPacker.Pack(groups, sheet, options);

            PackPlacement lower = result.Placements[0][0],
                upper = result.Placements[0][1];
            Assert.True(lower.Placed);
            Assert.Equal(lower.Translation.X, upper.Translation.X, 9);
            Assert.Equal(lower.Translation.Y, upper.Translation.Y, 9);
        }

        [Fact]
        public void LargestGroupsFirst_PutsTheLargestInTheStartCorner()
        {
            var sheet = Sheet.Custom(200.0, 200.0);
            var options = new PackOptions { LargestGroupsFirst = true, Tolerance = Tol };

            PackResult result = SheetPacker.Pack(
                OneEach(PolygonKit.Box(0, 0, 30, 30), PolygonKit.Box(0, 0, 90, 90)),
                sheet,
                options
            );

            Assert.Equal(0.0, result.Placements[1][0].ViewBox.LowerLeft.X, 2);
            Assert.Equal(200.0, result.Placements[1][0].ViewBox.UpperRight.Y, 2);
            Assert.True(result.Placements[0][0].Placed);
        }

        [Fact]
        public void FillEarlierSheets_SendsASmallShapeBackToTheFirstSheet()
        {
            var sheet = Sheet.Custom(100.0, 100.0);
            var options = new PackOptions { FillEarlierSheets = true, Tolerance = Tol };

            PackResult result = SheetPacker.Pack(
                OneEach(PolygonKit.Box(0, 0, 70, 70), PolygonKit.Box(0, 0, 70, 70), PolygonKit.Box(0, 0, 25, 25)),
                sheet,
                options
            );

            Assert.Equal(0, result.Placements[0][0].SheetIndex);
            Assert.Equal(1, result.Placements[1][0].SheetIndex);
            Assert.Equal(0, result.Placements[2][0].SheetIndex);
        }

        [Fact]
        public void TheUtilization_IsTheAreaOfTheShapesOverTheUsableAreaOfTheSheetsTaken()
        {
            var sheet = Sheet.Custom(130.0, 130.0);

            PackResult result = SheetPacker.Pack(
                OneEach(PolygonKit.Gamma(0, 0), PolygonKit.Gamma(50, 50)),
                sheet,
                Options()
            );

            Assert.Equal(2 * 5100.0 / (130.0 * 130.0), result.Utilization, 9);
        }

        [Fact]
        public void TheScaleOfTheSheet_ScalesTheSpacingAndTheOffsets()
        {
            var sheet = Sheet.Custom(100.0, 100.0);
            sheet.Scale = 10.0;
            sheet.OffsetLeft = 5.0;
            var options = new PackOptions
            {
                Spacing = 2.0,
                GroupSpacing = 2.0,
                Tolerance = Tol,
            };

            PackResult result = SheetPacker.Pack(
                OneEach(PolygonKit.Box(0, 0, 300, 300), PolygonKit.Box(0, 0, 300, 300)),
                sheet,
                options
            );

            GeoRectangle2 area = result.Sheets[0].UsableArea;
            Assert.Equal(50.0, area.LowerLeft.X, 6);
            Assert.Equal(50.0, result.Placements[0][0].ViewBox.LowerLeft.X, 2);
            double gap = result.Placements[0][0].ViewPolygon.DistanceTo(result.Placements[1][0].ViewPolygon);
            Assert.InRange(gap, 20.0 - 1e-3, 20.0 + 1e-2);
        }

        [Fact]
        public void ARandomPacking_LeavesNoTwoShapesOverlappingAndEachInsideItsSheet()
        {
            var sheet = new Sheet(PaperSize.A1)
            {
                OffsetLeft = 20,
                OffsetRight = 10,
                OffsetTop = 10,
                OffsetBottom = 10,
            };
            var options = new PackOptions
            {
                Spacing = 5.0,
                GroupSpacing = 8.0,
                Tolerance = Tol,
            };
            List<GeoPolygon2[]> groups = PolygonKit.Views(21, 60, 160.0, 3);

            PackResult result = SheetPacker.Pack(groups, sheet, options);

            AssertClear(result, 5.0, 8.0);
        }

        [Fact]
        public void RectanglesGivenAsPolygons_TakeNoMoreSheetsThanTheRectangleOverload()
        {
            var sheet = new Sheet(PaperSize.A1)
            {
                OffsetLeft = 20,
                OffsetRight = 10,
                OffsetTop = 10,
                OffsetBottom = 10,
            };
            var random = new Random(5);
            var boxes = new List<GeoRectangle2[]>();
            var polygons = new List<GeoPolygon2[]>();
            for (int i = 0; i < 60; i++)
            {
                double width = Math.Round(30.0 + random.NextDouble() * 140.0),
                    height = Math.Round(30.0 + random.NextDouble() * 110.0);
                boxes.Add(new[] { new GeoRectangle2(i * 10.0, 0.0, width, height) });
                polygons.Add(new[] { PolygonKit.Box(i * 10.0, 0.0, width, height) });
            }

            PackResult byBoxes = SheetPacker.Pack(boxes, sheet, Options());
            PackResult byPolygons = SheetPacker.Pack(polygons, sheet, Options());

            Assert.InRange(byPolygons.Sheets.Count, 1, byBoxes.Sheets.Count);
            Assert.InRange(byPolygons.Utilization, byBoxes.Utilization * 0.9, 1.0);
        }

        [Fact]
        public void NoGroups_GiveOneEmptySheet()
        {
            PackResult result = SheetPacker.Pack(new List<GeoPolygon2[]>(), Sheet.Custom(100.0, 100.0), Options());

            Assert.Empty(result.Placements);
            Assert.Single(result.Sheets);
            Assert.Equal(0.0, result.Utilization);
        }

        [Fact]
        public void ANullGroup_IsPassedOver()
        {
            var groups = new List<GeoPolygon2[]> { null, new[] { PolygonKit.Box(0, 0, 40, 40) } };

            PackResult result = SheetPacker.Pack(groups, Sheet.Custom(100.0, 100.0), Options());

            Assert.Empty(result.Placements[0]);
            Assert.True(result.Placements[1][0].Placed);
        }

        [Fact]
        public void NullArguments_AreRefused()
        {
            var groups = new List<GeoPolygon2[]>();
            Sheet sheet = Sheet.Custom(100.0, 100.0);

            Assert.Throws<ArgumentNullException>(() => SheetPacker.Pack((List<GeoPolygon2[]>)null, sheet));
            Assert.Throws<ArgumentNullException>(() => SheetPacker.Pack(groups, null));
            Assert.Throws<ArgumentNullException>(() => SheetPacker.Pack(groups, sheet, null));
        }

        [Fact]
        public void ASheetTheOffsetsLeaveNoRoom_IsRefused()
        {
            var sheet = new Sheet(PaperSize.A4) { OffsetLeft = 150, OffsetRight = 150 };

            var error = Assert.Throws<ArgumentException>(() =>
                SheetPacker.Pack(OneEach(PolygonKit.Box(0, 0, 10, 10)), sheet, Options())
            );
            Assert.Equal("sheet", error.ParamName);
        }

        public static IEnumerable<object[]> BadShapes()
        {
            yield return new object[] { null };
            yield return new object[]
            {
                new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(double.NaN, 10)),
            };
            yield return new object[]
            {
                new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(double.PositiveInfinity, 10)),
            };
            yield return new object[]
            {
                new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(20, 0)),
            };
            yield return new object[]
            {
                new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(10, 10), new GeoPoint2(10, 0), new GeoPoint2(0, 10)),
            };
        }

        [Theory]
        [MemberData(nameof(BadShapes))]
        public void ANullNotFiniteFlatOrSelfCrossingPolygon_IsRefusedNamingTheGroup(GeoPolygon2 bad)
        {
            var groups = new List<GeoPolygon2[]> { new[] { PolygonKit.Box(0, 0, 40, 40) }, new[] { bad } };

            var error = Assert.Throws<ArgumentException>(() =>
                SheetPacker.Pack(groups, Sheet.Custom(100.0, 100.0), Options())
            );

            Assert.Equal("groups", error.ParamName);
            Assert.Contains("group 1", error.Message);
        }

        private static void AssertClear(PackResult result, double spacing, double groupSpacing)
        {
            var placed = new List<(int Group, int Member, PackPlacement Placement)>();
            for (int g = 0; g < result.Placements.Count; g++)
            {
                for (int i = 0; i < result.Placements[g].Count; i++)
                {
                    PackPlacement placement = result.Placements[g][i];
                    Assert.True(placement.Placed, $"the shape {i} of group {g} is placed");
                    AssertInsideUsableArea(result, placement);
                    placed.Add((g, i, placement));
                }
            }

            double reach = Math.Max(spacing, groupSpacing) + 1.0;
            for (int a = 0; a < placed.Count; a++)
            {
                for (int b = a + 1; b < placed.Count; b++)
                {
                    PackPlacement p = placed[a].Placement,
                        q = placed[b].Placement;
                    if (
                        p.SheetIndex != q.SheetIndex
                        || p.ViewBox.LowerLeft.X > q.ViewBox.UpperRight.X + reach
                        || q.ViewBox.LowerLeft.X > p.ViewBox.UpperRight.X + reach
                        || p.ViewBox.LowerLeft.Y > q.ViewBox.UpperRight.Y + reach
                        || q.ViewBox.LowerLeft.Y > p.ViewBox.UpperRight.Y + reach
                    )
                    {
                        continue;
                    }

                    // A group of several shapes laid out together keeps the spacing between them; the rest keep the
                    // group spacing, but for a group split over sheets, whose blocks stand at the group spacing too.
                    double wanted = placed[a].Group == placed[b].Group ? spacing : groupSpacing;
                    double gap = p.ViewPolygon.DistanceTo(q.ViewPolygon);
                    Assert.True(
                        gap >= wanted - 2e-3,
                        $"shapes {placed[a].Group}.{placed[a].Member} and {placed[b].Group}.{placed[b].Member} stand {gap} apart, {wanted} wanted"
                    );
                }
            }
        }
    }
}
