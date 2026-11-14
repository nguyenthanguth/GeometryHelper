using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// TEXTALIGN along a line of two points under Set spacing and Distribute, as AutoCAD 2021's console runs it (four TEXT 2.5
    /// high, A at (0, 0), BBB at (60, 25), CCCCCC at (30, 50) and DDDD at (45, 40), a line of slope 1/2 from (30, 50) to (130,
    /// 100); the first picked at the origin, as AutoCAD's console moves every typed point by the first object's place). The
    /// texts are put in the order their corners stand along the line, the first corner on the first point; the next stands
    /// along the line far enough on that the boxes are apart by the spacing, measured along the line, either across (the
    /// advance is the first box's width over the line's cosine, plus the spacing) or up (the next box's height over its sine,
    /// plus the spacing), whichever is the less: the corners of the Set spacing 3 run at 4.863, 8.59 and 8.59 apart under TL.
    /// Distribute puts the last corner on the second point and the same gap between every two boxes.
    /// </summary>
    public class TextAlignLineSpacingTests
    {
        private static readonly string[] Handles = { "B1", "B2", "B3", "B4" };

        private static CadDocument Four(double height = 2.5) =>
            CadDocument.From(
                TestDrawings.Drawing(
                    null,
                    null,
                    TestDrawings.Text(0, 0, height, "AA", handle: "B1"),
                    TestDrawings.Text(60, 25, height, "BBB", handle: "B2"),
                    TestDrawings.Text(30, 50, height, "CCCCCC", handle: "B3"),
                    TestDrawings.Text(45, 40, height, "DDDD", handle: "B4")
                ),
                "four.dxf"
            );

        private static DisplayBox Box(CadDocument document, string handle)
        {
            DisplayBox box = DisplayBox.Empty;

            foreach (DisplayItem2 item in document.GetDisplay(document.ModelSpace).Items)
            {
                if (item.Handle == handle)
                {
                    box = box.Union(item.Bounds);
                }
            }

            return box;
        }

        /// <summary>The corner of a box the alignment holds it by: across 0 left, 1 middle, 2 right; up 0 top, 1 middle, 2 bottom.</summary>
        private static GeoPoint2 Corner(DisplayBox box, int across, int up) =>
            new GeoPoint2(
                across == 0 ? box.MinX
                    : across == 1 ? (box.MinX + box.MaxX) / 2.0
                    : box.MaxX,
                up == 0 ? box.MaxY
                    : up == 1 ? (box.MinY + box.MaxY) / 2.0
                    : box.MinY
            );

        /// <summary>The corners of the texts in the order they stand along the line from its first point, with how far along each stands.</summary>
        private static List<KeyValuePair<string, double>> AlongTheLine(
            CadDocument document,
            GeoPoint2 from,
            GeoPoint2 to,
            int across,
            int up
        )
        {
            GeoVector2 line = to - from;
            var u = new GeoVector2(line.X / line.Length, line.Y / line.Length);
            var along = new List<KeyValuePair<string, double>>();

            foreach (string handle in Handles)
            {
                GeoPoint2 corner = Corner(Box(document, handle), across, up);
                GeoVector2 offset = corner - from;
                Assert.Equal(0.0, offset.X * -u.Y + offset.Y * u.X, 6);
                along.Add(new KeyValuePair<string, double>(handle, offset.X * u.X + offset.Y * u.Y));
            }

            return along.OrderBy(a => a.Value).ToList();
        }

        /// <summary>How far along the line the next box stands from the one before it when the gap is nought, as AutoCAD advances it: across or up, the less.</summary>
        private static double Advance(DisplayBox previous, DisplayBox next, GeoVector2 u, int across, int up)
        {
            double a = across / 2.0;
            double v = up / 2.0;
            double widthPrevious = previous.MaxX - previous.MinX;
            double widthNext = next.MaxX - next.MinX;
            double heightPrevious = previous.MaxY - previous.MinY;
            double heightNext = next.MaxY - next.MinY;
            double x = double.PositiveInfinity;
            double y = double.PositiveInfinity;

            if (Math.Abs(u.X) > 1E-9)
            {
                x =
                    (u.X > 0.0 ? (1.0 - a) * widthPrevious + a * widthNext : a * widthPrevious + (1.0 - a) * widthNext)
                    / Math.Abs(u.X);
            }

            if (Math.Abs(u.Y) > 1E-9)
            {
                y =
                    (
                        u.Y > 0.0
                            ? (1.0 - v) * heightNext + v * heightPrevious
                            : (1.0 - v) * heightPrevious + v * heightNext
                    ) / Math.Abs(u.Y);
            }

            return Math.Min(x, y);
        }

        private static void AssertSpaced(
            CadDocument document,
            GeoPoint2 from,
            GeoPoint2 to,
            int across,
            int up,
            double? spacing
        )
        {
            List<KeyValuePair<string, double>> along = AlongTheLine(document, from, to, across, up);
            GeoVector2 line = to - from;
            var u = new GeoVector2(line.X / line.Length, line.Y / line.Length);
            Assert.Equal(0.0, along[0].Value, 6);
            var gaps = new List<double>();

            for (int i = 1; i < along.Count; i++)
            {
                double advance = Advance(Box(document, along[i - 1].Key), Box(document, along[i].Key), u, across, up);
                gaps.Add(along[i].Value - along[i - 1].Value - advance);
            }

            foreach (double gap in gaps)
            {
                Assert.Equal(spacing ?? gaps[0], gap, 6);
            }

            if (spacing == null)
            {
                Assert.Equal(line.Length, along[along.Count - 1].Value, 6);
            }
        }

        [Fact]
        public void SetSpacing_UnderTL_PutsTheCornersAlongTheLineInTheOrderTheyStandOnIt_TheBoxesAGapApartAlongIt()
        {
            CadDocument document = Four();

            Run(Session(document), "TA", "I", "TL", Handles, Enter, "P", P(30, 50), "O", "S", "3", P(130, 100));

            AssertSpaced(document, P(30, 50), P(130, 100), 0, 0, 3.0);
            Assert.Equal("TEXTALIGN", document.NextUndo.Name);
            Assert.Equal(
                new[] { "B1", "B3", "B4", "B2" },
                AlongTheLine(document, P(30, 50), P(130, 100), 0, 0).Select(a => a.Key)
            );
        }

        [Fact]
        public void SetSpacing_UnderTR_TakesTheNextBoxesWidthAcross_AndItsHeightUp()
        {
            CadDocument document = Four();

            Run(Session(document), "TA", "I", "TR", Handles, Enter, "P", P(30, 50), "O", "S", "3", P(130, 100));

            AssertSpaced(document, P(30, 50), P(130, 100), 2, 0, 3.0);
        }

        [Fact]
        public void SetSpacing_UnderCenter_SpacesTheMiddles()
        {
            CadDocument document = Four();

            Run(Session(document), "TA", "I", "Center", Handles, Enter, "P", P(30, 50), "O", "S", "3", P(130, 100));

            AssertSpaced(document, P(30, 50), P(130, 100), 1, 1, 3.0);
        }

        [Fact]
        public void Distribute_PutsTheFirstCornerOnTheFirstPoint_TheLastOnTheSecond_AndTheSameGapBetweenTheBoxes()
        {
            CadDocument document = Four();

            Run(Session(document), "TA", "I", "TL", Handles, Enter, "P", P(30, 50), "O", "D", P(130, 100));

            AssertSpaced(document, P(30, 50), P(130, 100), 0, 0, null);
        }

        [Fact]
        public void Distribute_UnderMC_AndTR_FollowTheSameRule()
        {
            CadDocument document = Four();
            Run(Session(document), "TA", "I", "MC", Handles, Enter, "P", P(30, 50), "O", "D", P(130, 100));
            AssertSpaced(document, P(30, 50), P(130, 100), 1, 1, null);

            Assert.True(document.Undo());
            Run(Session(document), "TA", "I", "TR", Handles, Enter, "P", P(30, 50), "O", "D", P(130, 100));
            AssertSpaced(document, P(30, 50), P(130, 100), 2, 0, null);
        }

        [Fact]
        public void ASteepLine_PutsTheTextsInTheOrderTheyStandAlongIt_NotAlongX()
        {
            // AutoCAD 2021: along a line of slope 2 BBB (at y = 25) comes before CCCCCC (at y = 50, further left).
            CadDocument document = Four();

            Run(Session(document), "TA", "I", "TL", Handles, Enter, "P", P(30, 50), "O", "S", "3", P(80, 150));

            AssertSpaced(document, P(30, 50), P(80, 150), 0, 0, 3.0);
            Assert.Equal(
                new[] { "B1", "B2", "B4", "B3" },
                AlongTheLine(document, P(30, 50), P(80, 150), 0, 0).Select(a => a.Key)
            );
        }

        [Fact]
        public void ALineGoingLeftAndDown_StartsAtItsFirstPoint_AndTheTextsFarthestRightComeFirst()
        {
            CadDocument document = Four();

            Run(Session(document), "TA", "I", "TL", Handles, Enter, "P", P(130, 100), "O", "S", "3", P(30, 50));

            AssertSpaced(document, P(130, 100), P(30, 50), 0, 0, 3.0);
            Assert.Equal("B2", AlongTheLine(document, P(130, 100), P(30, 50), 0, 0)[0].Key);
        }

        [Fact]
        public void ALineAcross_IsNotDiscarded_UnderSetSpacingOrDistribute_TheBoxesStandInARow()
        {
            // AutoCAD 2021: along y = 50 the corners are on the line and the boxes are the spacing apart across, the same under
            // Distribute with the same gap between each two.
            CadDocument document = Four();
            CadCommandSession session = Run(
                Session(document),
                "TA",
                "I",
                "TL",
                Handles,
                Enter,
                "P",
                P(30, 50),
                "O",
                "S",
                "3",
                P(130, 50)
            );

            Assert.DoesNotContain(
                "The base line is near horizontal or vertical, aligned texts may be out of available area, alignment is discarded.",
                session.History
            );
            AssertSpaced(document, P(30, 50), P(130, 50), 0, 0, 3.0);
            Assert.True(document.Undo());
        }

        [Fact]
        public void ALineUp_PutsTheBoxesInAColumn_AGapApartUp()
        {
            CadDocument document = Four();

            Run(Session(document), "TA", "I", "TL", Handles, Enter, "P", P(30, 50), "O", "S", "3", P(30, 150));

            AssertSpaced(document, P(30, 50), P(30, 150), 0, 0, 3.0);
        }
    }
}
