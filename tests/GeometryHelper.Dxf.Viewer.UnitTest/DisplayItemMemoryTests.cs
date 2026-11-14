using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.TestDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The items of a display list are kept small: a chain of two points as its box and two flags, the style and layer of an
    /// item in a look its like share, what few texts have in one object made when they have it, the insertion points of an
    /// entity in its entry and what a run read apart from the run. Each test shows that what is read back is what was put in.
    /// </summary>
    public class DisplayItemMemoryTests
    {
        private static readonly DisplayStyle Style = new DisplayStyle(0xFF0000, 0.25, null);

        private static long Bits(double value) => BitConverter.DoubleToInt64Bits(value);

        [Theory]
        [InlineData(1.0, 2.0, 5.0, 8.0)]
        [InlineData(5.0, 8.0, 1.0, 2.0)]
        [InlineData(5.0, 2.0, 1.0, 8.0)]
        [InlineData(1.0, 8.0, 5.0, 2.0)]
        [InlineData(3.0, 4.0, 3.0, 4.0)]
        [InlineData(double.NaN, 1.0, 2.0, 3.0)]
        [InlineData(1.0, 2.0, 3.0, double.NaN)]
        [InlineData(double.PositiveInfinity, 0.0, double.NegativeInfinity, 1.0)]
        [InlineData(-1E300, 1E300, 1E-300, -1E-300)]
        public void AChainOfTwoPoints_GivesBackItsVerticesBitForBit_AndTheBoxOfTheirPoints(
            double x0,
            double y0,
            double x1,
            double y1
        ) => AssertKept(x0, y0, x1, y1);

        [Fact]
        public void AChainOfTwoPoints_OfZerosOfEitherSign_GivesBackEachSignInEachPlace()
        {
            double[] zeros = { 0.0, -0.0 };

            foreach (double x0 in zeros)
            foreach (double y0 in zeros)
            foreach (double x1 in zeros)
            foreach (double y1 in zeros)
            {
                AssertKept(x0, y0, x1, y1);
            }
        }

        private static void AssertKept(double x0, double y0, double x1, double y1)
        {
            var chain = new DisplayPolyline2(new[] { x0, y0, x1, y1 }, false, Style, "H1", "0");

            Assert.Equal(2, chain.Count);
            Assert.Equal(new[] { Bits(x0), Bits(y0), Bits(x1), Bits(y1) }, chain.Xy.Select(Bits).ToArray());

            DisplayBox expected = DisplayBox.Empty.Add(x0, y0).Add(x1, y1);
            DisplayBox box = chain.Bounds;
            Assert.Equal(
                new[] { Bits(expected.MinX), Bits(expected.MinY), Bits(expected.MaxX), Bits(expected.MaxY) },
                new[] { Bits(box.MinX), Bits(box.MinY), Bits(box.MaxX), Bits(box.MaxY) }
            );
        }

        [Fact]
        public void AChainOfTwoPoints_MovedAlongXAndY_HasItsVerticesMoved_AndItsFlagsKept()
        {
            var chain = new DisplayPolyline2(new[] { 9.0, 1.0, 2.0, 6.0 }, true, Style, "H1", "0") { IsSampled = true };

            var moved = (DisplayPolyline2)chain.Moved(10.0, -1.0);

            Assert.Equal(new[] { 19.0, 0.0, 12.0, 5.0 }, moved.Xy);
            Assert.True(moved.Closed);
            Assert.True(moved.IsSampled);
        }

        [Fact]
        public void AChainOfTwoPoints_IsAsFarFromAPoint_AsItsSegmentIs()
        {
            var chain = new DisplayPolyline2(new[] { 10.0, 0.0, 0.0, 0.0 }, false, Style, "H1", "0");

            Assert.Equal(3.0, chain.DistanceTo(4.0, 3.0), 12);
            Assert.Equal(5.0, chain.DistanceTo(-3.0, 4.0), 12);
            Assert.True(chain.Touches(new DisplayBox(4, -1, 6, 1)));
            Assert.False(chain.Touches(new DisplayBox(4, 1, 6, 2)));
        }

        [Fact]
        public void ALongerChain_KeepsItsList_AndItsBox()
        {
            double[] xy = { 0, 0, 4, 1, 2, 9 };
            var chain = new DisplayPolyline2(xy, false, Style, "H1", "0");

            Assert.Equal(3, chain.Count);
            Assert.Equal(xy, chain.Xy);
            Assert.Equal(0.0, chain.Bounds.MinX);
            Assert.Equal(9.0, chain.Bounds.MaxY);
        }

        [Fact]
        public void TwoItemsOfOneStyleAndLayer_ShareTheirLook_AndAClipSetOnOneIsNotSetOnTheOther()
        {
            var first = new DisplayArc2(0, 0, 5, 0, 1, Style, "H1", "Steel");
            var second = new DisplayArc2(9, 9, 5, 0, 1, Style, "H2", "Steel");
            var clip = new DisplayClipRegion(
                new DisplayClip2(new[] { 0.0, 0.0, 10.0, 0.0, 10.0, 10.0 }, new[] { 0.0, 0.0, 0.0 }),
                null
            );

            first.BlockClip = clip;

            Assert.Same(clip, first.BlockClip);
            Assert.Null(second.BlockClip);
            Assert.Same(Style, second.Style);
            Assert.Equal("Steel", second.Layer);

            var third = new DisplayPolyline2(new[] { 0.0, 0.0, 1.0, 1.0 }, false, Style, "H3", "Steel");
            Assert.Null(third.BlockClip);

            first.BlockClip = null;
            Assert.Null(first.BlockClip);
        }

        [Fact]
        public void TheFlagsOfAnItem_SetOnOne_AreNotSetOnAnotherOfTheSameStyleAndLayer()
        {
            var first = new DisplayPolyline2(new[] { 0.0, 0.0, 1.0, 1.0 }, false, Style, "H1", "Steel");
            var second = new DisplayPolyline2(new[] { 5.0, 5.0, 6.0, 6.0 }, false, Style, "H2", "Steel");
            var text = new DisplayText2("T", 0, 0, 2.5, 0, 1, 0, 0, Style, "H3", "Steel", null, 0.0);

            first.IsClipFrame = true;
            first.IsNotPlotted = true;
            first.IsFrameInside = true;
            text.IsFrameInside = true;

            Assert.True(first.IsClipFrame);
            Assert.True(first.IsNotPlotted);
            Assert.True(first.IsFrameInside);
            Assert.True(text.IsFrameInside);
            Assert.False(text.IsClipFrame);
            Assert.False(second.IsClipFrame);
            Assert.False(second.IsNotPlotted);
            Assert.False(second.IsFrameInside);

            var later = new DisplayPolyline2(new[] { 7.0, 7.0, 8.0, 8.0 }, false, Style, "H4", "Steel");
            Assert.False(later.IsClipFrame);
            Assert.False(later.IsNotPlotted);
            Assert.False(later.IsFrameInside);

            first.IsClipFrame = false;
            Assert.False(first.IsClipFrame);
            Assert.True(first.IsNotPlotted);
        }

        [Fact]
        public void ItemsOfOneStyleOnTwoLayers_KeepTheirOwnLayer_AndOneOnNoLayerKeepsNone()
        {
            var steel = new DisplayArc2(0, 0, 1, 0, 1, Style, "H1", "Steel");
            var notes = new DisplayArc2(0, 0, 1, 0, 1, Style, "H2", "Notes");
            var steelAgain = new DisplayArc2(0, 0, 1, 0, 1, Style, "H3", new string("Steel".ToCharArray()));
            var none = new DisplayArc2(0, 0, 1, 0, 1, Style, "H4", null);

            Assert.Equal("Steel", steel.Layer);
            Assert.Equal("Notes", notes.Layer);
            Assert.Equal("Steel", steelAgain.Layer);
            Assert.Null(none.Layer);
            Assert.Same(Style, none.Style);
        }

        [Fact]
        public void APlainText_HasNoSlantAndTheWidthFactorOfOne_AndATextOfNegativeOrNoWidthFactorHasOne()
        {
            var plain = new DisplayText2("abc", 1, 2, 2.5, 0.3, 1.0, 1, 2, Style, "H1", "0", "arial.ttf", 0.0);
            var none = new DisplayText2("abc", 1, 2, 2.5, 0.3, 0.0, 1, 2, Style, "H2", "0", null, 0.0);
            var negative = new DisplayText2("abc", 1, 2, 2.5, 0.3, -2.0, 1, 2, Style, "H3", "0", null, 0.0);

            Assert.Equal(1.0, plain.WidthFactor);
            Assert.Equal(0L, Bits(plain.Oblique));
            Assert.Equal(1, plain.Horizontal);
            Assert.Equal(2, plain.Vertical);
            Assert.Equal("arial.ttf", plain.FontName);
            Assert.Null(plain.Strokes);
            Assert.Null(plain.Parts);
            Assert.False(plain.IsBold);
            Assert.False(plain.IsItalic);
            Assert.False(plain.IsBackward);
            Assert.Equal(1.0, none.WidthFactor);
            Assert.Equal(1.0, negative.WidthFactor);
        }

        [Fact]
        public void ATextWithASlantAndAWidthFactor_GivesThemBack_AndANegativeZeroSlantStaysNegative()
        {
            var slanted = new DisplayText2("abc", 1, 2, 2.5, 0.3, 0.8, 0, 3, Style, "H1", "0", null, 0.2618, true);
            var negativeZero = new DisplayText2("abc", 1, 2, 2.5, 0.3, 1.0, 0, 0, Style, "H2", "0", null, -0.0);

            Assert.Equal(0.2618, slanted.Oblique);
            Assert.Equal(0.8, slanted.WidthFactor);
            Assert.Equal(3, slanted.Vertical);
            Assert.True(slanted.IsBackward);
            Assert.Equal(Bits(-0.0), Bits(negativeZero.Oblique));
        }

        [Fact]
        public void ABoldItalicText_MovedAlongXAndY_KeepsItsSlantWidthFactorAndFlags_AndItsBoxMoves()
        {
            var text = new DisplayText2("abc", 1, 2, 2.5, 0.0, 0.8, 0, 0, Style, "H1", "0", "arial.ttf", 0.2618, true)
            {
                IsBold = true,
                IsItalic = true,
                IsFrameInside = true,
            };

            var moved = (DisplayText2)text.Moved(10, 20);

            Assert.Equal(11.0, moved.X);
            Assert.Equal(22.0, moved.Y);
            Assert.Equal(0.2618, moved.Oblique);
            Assert.Equal(0.8, moved.WidthFactor);
            Assert.True(moved.IsBold);
            Assert.True(moved.IsItalic);
            Assert.True(moved.IsBackward);
            Assert.True(moved.IsFrameInside);
            Assert.Equal(text.Bounds.MinX + 10, moved.Bounds.MinX, 12);
            Assert.Equal(text.Bounds.MaxY + 20, moved.Bounds.MaxY, 12);
            Assert.False(text.Bounds.IsEmpty);
        }

        [Fact]
        public void TheInsertionPointsOfAnEntity_ComeBackInTheOrderTheyWereKept()
        {
            var list = new DisplayList2();
            list.AddInsertionPoint("A1", 1, 2);
            list.AddInsertionPoint("B2", 7, 8);
            list.AddInsertionPoint("A1", 3, 4);
            list.AddInsertionPoint("A1", 5, 6);

            Assert.Equal(
                new[] { new GeoPoint2(1, 2), new GeoPoint2(3, 4), new GeoPoint2(5, 6) },
                list.InsertionPointsOf("a1")
            );
            Assert.Equal(new[] { new GeoPoint2(7, 8) }, list.InsertionPointsOf("B2"));
            Assert.Empty(list.InsertionPointsOf("C3"));
            Assert.Empty(list.InsertionPointsOf(null));
        }

        [Fact]
        public void TheInsertionPointsOfAnEntity_SurviveTheCopyOfItsRun_AndOfAWholeListTrimmed()
        {
            var older = new DisplayList2();
            older.Add(new DisplayPolyline2(new[] { 0.0, 0.0, 1.0, 1.0 }, false, Style, "A1", "0"));
            older.AddInsertionPoint("A1", 1, 2);
            older.AddInsertionPoint("A1", 3, 4);
            older.AddRun("A1", 0, 1UL, null);
            older.TrimExcess();

            var patched = new DisplayList2(older);
            patched.CopyRun(older, older.Runs[0]);

            Assert.Equal(new[] { new GeoPoint2(1, 2), new GeoPoint2(3, 4) }, older.InsertionPointsOf("A1"));
            Assert.Equal(new[] { new GeoPoint2(1, 2), new GeoPoint2(3, 4) }, patched.InsertionPointsOf("A1"));
        }

        [Fact]
        public void AMoveOfOtherEntities_LeavesTheInsertionPointOfATextWhereItWas_AndAMoveOfTheTextMovesIt()
        {
            string layers = Layer("Steel", 1) + Layer("Bolts", 3);
            CadDocument document = CadDocument.From(
                Drawing(
                    layers,
                    string.Empty,
                    Line(0, 0, 100, 0, "Steel", handle: "A1"),
                    Text(10, 20, 5, "PLAN", "Bolts", handle: "A4")
                ),
                "plan.dxf"
            );
            GeoPoint2 at = Assert.Single(document.GetDisplay(document.ModelSpace).InsertionPointsOf("A4"));

            Move(document, "A1");
            Assert.Equal(new[] { at }, document.GetDisplay(document.ModelSpace).InsertionPointsOf("A4"));

            Move(document, "A4");
            GeoPoint2 moved = Assert.Single(document.GetDisplay(document.ModelSpace).InsertionPointsOf("A4"));
            Assert.Equal(at.X + 7, moved.X, 9);
            Assert.Equal(at.Y + 3, moved.Y, 9);
        }

        [Fact]
        public void WhatARunRead_ComesBackWithItsRun_AfterTheRunIsCopiedIntoAPatchedList()
        {
            var older = new DisplayList2();
            older.Add(new DisplayPolyline2(new[] { 0.0, 0.0, 1.0, 1.0 }, false, Style, "A1", "0"));
            older.AddRun("A1", 0, 1UL, null);
            older.Add(new DisplayPolyline2(new[] { 2.0, 2.0, 3.0, 3.0 }, false, Style, "A2", "0"));
            older.Add(new DisplayPolyline2(new[] { 4.0, 4.0, 5.0, 5.0 }, false, Style, "A2", "0"));
            older.AddRun("A2", 1, 2UL, new[] { "BLK1", "A9" });
            older.Add(new DisplayPolyline2(new[] { 6.0, 6.0, 7.0, 7.0 }, false, Style, "A3", "0"));
            older.AddRun("A3", 3, 4UL, new[] { "BLK2" });

            Assert.Null(older.Runs[0].Reads);
            Assert.Equal(new[] { "BLK1", "A9" }, older.Runs[1].Reads);

            var patched = new DisplayList2(older);

            for (int i = 0; i < older.Runs.Count; i++)
            {
                patched.CopyRun(older, older.Runs[i]);
            }

            Assert.Equal(3, patched.Runs.Count);
            Assert.Null(patched.Runs[0].Reads);
            Assert.Equal(new[] { "BLK1", "A9" }, patched.Runs[1].Reads);
            Assert.Equal(new[] { "BLK2" }, patched.Runs[2].Reads);
            Assert.Equal(new[] { "A1", "A2", "A3" }, patched.Runs.Select(r => r.Handle));
            Assert.Equal(new[] { 0, 1, 3 }, patched.Runs.Select(r => r.Start));
            Assert.Equal(new[] { 1, 2, 1 }, patched.Runs.Select(r => r.Count));
            Assert.Equal(new[] { 1UL, 2UL, 4UL }, patched.Runs.Select(r => r.Layers));
        }

        [Fact]
        public void AnEntityThatReadsAnotherDrawnAgain_WhenTheOtherIsEdited_KeepsWhatItReadThroughThePatch()
        {
            var older = new DisplayList2();
            older.Add(new DisplayPolyline2(new[] { 0.0, 0.0, 1.0, 1.0 }, false, Style, "A1", "0"));
            older.AddRun("A1", 0, 1UL, new[] { "BLK1" });
            var patched = new DisplayList2(older);
            patched.CopyRun(older, older.Runs[0]);
            var again = new DisplayList2(patched);
            again.CopyRun(patched, patched.Runs[0]);

            Assert.Equal(new[] { "BLK1" }, again.Runs[0].Reads);
        }

        private static void Move(CadDocument document, string handle)
        {
            GeoTransform3 move = GeoTransform3.Translation(new GeoVector3(7, 3, 0));
            document.ApplyEdit("MOVE", new[] { new CadEditRecord(CadEditRecordKind.Transformed, handle) }, s =>
                s.Document.FindEntity(handle).TransformBy(move)
            );
        }
    }
}
