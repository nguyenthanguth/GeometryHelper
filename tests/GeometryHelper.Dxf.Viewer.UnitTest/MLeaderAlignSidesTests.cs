using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// MLEADERALIGN and the side of the content a leader meets, as AutoCAD 2021's console runs it (probed through standard
    /// input with multileaders made by MLEADER): a content stands in a box (its words and a landing gap before and after them, a
    /// block's extents) and is held by the left of that box, whichever side its leader is on; a content whose arrowhead ends up
    /// on the far side of the middle of its box has its leader turned to meet it there, the words or block staying where they are.
    /// The drawings here measure their words in a font of their own, as AutoCAD does.
    /// </summary>
    public class MLeaderAlignSidesTests : IDisposable
    {
        private readonly FontFolder _folder = new FontFolder();

        public void Dispose() => _folder.Dispose();

        /// <summary>A blank drawing with a multileader style whose words are measured in a font of the drawing's own.</summary>
        private CadDocument Measured()
        {
            CadDocument document = BlankWithMLeaderStyle();
            _folder.Write("test.shx", ShxFiles.Unifont());
            document.Fonts = new CadFonts(new[] { _folder.FullPath }, null, null);
            document.Database.TextStyleTable["Standard"].FileName = "test.shx";
            return document;
        }

        private static DxfMLeader Make(CadDocument document, GeoPoint2 arrow, GeoPoint2 landing, string words)
        {
            int before = All<DxfMLeader>(document.ModelSpace).Count;
            Run(Session(document), "MLD", arrow, landing, words);
            List<DxfMLeader> all = All<DxfMLeader>(document.ModelSpace);
            Assert.Equal(before + 1, all.Count);
            return all[all.Count - 1];
        }

        /// <summary>The words as an MTEXT of them in the drawing's fonts, to measure.</summary>
        private DxfMText Measure(DxfMLeader like, string words)
        {
            var database = new DxfDatabase("AC1027") { FontSearchPath = _folder.FullPath };
            database.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "GH_SHX", FileName = "test.shx" });
            var probe = new DxfMText
            {
                Contents = words,
                TextHeight = like.MText.TextHeight,
                Location = new GeoPoint3(0, 0, 0),
            };
            database.ModelSpace.AppendEntity(probe);
            probe.TextStyleName = "GH_SHX";
            return probe;
        }

        /// <summary>The width the drawing's fonts give words, as an MTEXT of them measures it.</summary>
        private double Width(CadDocument document, DxfMLeader like, string words) => Measure(like, words).ActualWidth;

        private static DxfAttachmentPoint Held(DxfMLeader mleader) => mleader.MText.Attachment;

        private static double Gap(DxfMLeader mleader) => mleader.LandingGap * mleader.Scale;

        private static double Dogleg(DxfMLeader mleader) => mleader.DoglegLength * mleader.Scale;

        /// <summary>The left of the box of the words: the ink's left edge less the landing gap.</summary>
        private static double BoxLeft(DxfMLeader mleader, double ink) =>
            (
                mleader.MText.Attachment == DxfAttachmentPoint.TopRight
                    ? mleader.TextLocation.X - ink
                    : mleader.TextLocation.X
            ) - Gap(mleader);

        private static GeoPoint3 Landing(DxfMLeader mleader) =>
            mleader.GetLastVertex(mleader.GetLeaderLineIndexes(mleader.GetLeaderIndexes()[0])[0]);

        /// <summary>The left middle of the box of the first multileader, the point a direction is taken from.</summary>
        private static GeoPoint2 Anchor(DxfMLeader mleader, double ink) =>
            new GeoPoint2(BoxLeft(mleader, ink), mleader.TextLocation.Y - mleader.TextHeight * mleader.Scale / 2.0);

        [Fact]
        public void AContentWhoseArrowheadIsPastTheMiddleOfItsBox_HasItsLeaderTurnedToTheOtherSide_TheWordsStaying()
        {
            // AutoCAD 2021, words ONE with the landing at (10, 10) as the one picked, TWO from an arrowhead at (30, 0) landing at
            // (40, 25): aligned up the line x = 10.36 the box of TWO (0.72 wide) stands from 10.36 to 11.08, its arrowhead is
            // past the middle of it, and the words stay at 10.45 while the leader comes in from the right: held at their top
            // right at 10.99, the dogleg against x ending 0.09 past them, the landing 0.36 further.
            CadDocument document = Measured();
            DxfMLeader one = Make(document, P(0, 0), P(10, 10), "IOI");
            DxfMLeader two = Make(document, P(30, 0), P(40, 25), "SOD");
            double inkOne = Width(document, one, "IOI");
            double inkTwo = Width(document, two, "SOD");
            Assert.True(inkTwo > 1.0);
            GeoPoint2 anchor = Anchor(one, inkOne);
            double inkY = two.TextLocation.Y;

            Run(
                Session(document),
                "MLA",
                new[] { one.Handle, two.Handle },
                Enter,
                new[] { one.Handle },
                P(anchor.X, anchor.Y + 100)
            );

            Assert.Equal(DxfAttachmentPoint.TopRight, Held(two));
            Assert.Equal(anchor.X + Gap(two) + inkTwo, two.TextLocation.X, 6);
            Assert.Equal(inkY, two.TextLocation.Y, 6);
            Assert.Equal(-1.0, two.GetDogleg(two.GetLeaderIndexes()[0]).X, 6);
            GeoPoint3 landing = Landing(two);
            Assert.Equal(two.TextLocation.X + Gap(two) + Dogleg(two), landing.X, 6);
            Assert.Equal(two.TextLocation.Y - two.TextHeight / 2.0, landing.Y, 6);
            Assert.Equal(1, two.GetLeaderIndexes()[0]);
            Assert.Equal(DxfAttachmentPoint.TopLeft, Held(one));
            Assert.Equal("MLEADERALIGN", document.NextUndo.Name);
            Assert.True(document.Undo());
            DxfMLeader back = Entity<DxfMLeader>(document, two.Handle);
            Assert.Equal(DxfAttachmentPoint.TopLeft, Held(back));
            Assert.Equal(40.0 + Dogleg(back) + Gap(back), back.TextLocation.X, 6);
        }

        [Fact]
        public void AContentOnTheLeftWhoseArrowheadIsBeforeTheMiddleOfItsBox_HasItsLeaderTurnedToTheLeft()
        {
            // AutoCAD 2021: TWO landing at (-1, 25) from an arrowhead at (9, 0) is held at its top right; aligned to x = 10.36 its
            // arrowhead (9) is before the middle of the box, so the words stay at 10.45 held at their top left and the leader
            // meets them from the left, the dogleg ending at 10.36 and the landing 0.36 short of that.
            CadDocument document = Measured();
            DxfMLeader one = Make(document, P(0, 0), P(10, 10), "IOI");
            DxfMLeader two = Make(document, P(9, 0), P(-1, 25), "SOD");
            Assert.Equal(DxfAttachmentPoint.TopRight, Held(two));
            double inkOne = Width(document, one, "IOI");
            double inkTwo = Width(document, two, "SOD");
            GeoPoint2 anchor = Anchor(one, inkOne);
            double inkY = two.TextLocation.Y;

            Run(
                Session(document),
                "MLA",
                new[] { one.Handle, two.Handle },
                Enter,
                new[] { one.Handle },
                P(anchor.X, anchor.Y + 100)
            );

            Assert.Equal(DxfAttachmentPoint.TopLeft, Held(two));
            Assert.Equal(anchor.X + Gap(two), two.TextLocation.X, 6);
            Assert.Equal(inkY, two.TextLocation.Y, 6);
            Assert.Equal(1.0, two.GetDogleg(two.GetLeaderIndexes()[0]).X, 6);
            Assert.Equal(anchor.X - Dogleg(two), Landing(two).X, 6);
            Assert.True(inkTwo > 1.0);
        }

        [Fact]
        public void AnArrowheadBeforeTheMiddleOfTheBox_KeepsTheLeaderOnTheLeft_ItsLandingOnTheLine()
        {
            // AutoCAD 2021 control: TWO from an arrowhead at (0, 30) landing at (40, 50) is aligned up x = 10.36 and keeps its leader
            // on the left (the arrowhead at 10.71, just before the middle of the box at 10.72, does too, one at 10.73 does not).
            CadDocument document = Measured();
            DxfMLeader one = Make(document, P(0, 0), P(10, 10), "IOI");
            DxfMLeader two = Make(document, P(0, 30), P(40, 50), "SOD");
            GeoPoint2 anchor = Anchor(one, Width(document, one, "IOI"));

            Run(
                Session(document),
                "MLA",
                new[] { one.Handle, two.Handle },
                Enter,
                new[] { one.Handle },
                P(anchor.X, anchor.Y + 100)
            );

            Assert.Equal(DxfAttachmentPoint.TopLeft, Held(two));
            Assert.Equal(anchor.X + Gap(two), two.TextLocation.X, 6);
            Assert.Equal(anchor.X - Dogleg(two), Landing(two).X, 6);
            Assert.Equal(1.0, two.GetDogleg(two.GetLeaderIndexes()[0]).X, 6);
        }

        [Fact]
        public void AContentOnTheLeft_IsHeldByTheLeftOfItsBox_NotByWhereItsLeaderMeetsIt()
        {
            // AutoCAD 2021: a reference of ONE landing at (10, 10) and TWO from an arrowhead at (100, 50) landing at (90, 60), held at
            // its top right: aligned up the line x = 10.36 the left of its box lies on the line (the words 0.54 wide held at their
            // top right at 10.99), where the leader's end, at the right of the box, used to be put there.
            CadDocument document = Measured();
            DxfMLeader one = Make(document, P(0, 0), P(10, 10), "IOI");
            DxfMLeader two = Make(document, P(100, 50), P(90, 60), "SOD");
            double inkTwo = Width(document, two, "SOD");
            GeoPoint2 anchor = Anchor(one, Width(document, one, "IOI"));

            Run(
                Session(document),
                "MLA",
                new[] { one.Handle, two.Handle },
                Enter,
                new[] { one.Handle },
                P(anchor.X, anchor.Y + 100)
            );

            Assert.Equal(DxfAttachmentPoint.TopRight, Held(two));
            Assert.Equal(anchor.X, BoxLeft(two, inkTwo), 6);
            Assert.Equal(-1.0, two.GetDogleg(two.GetLeaderIndexes()[0]).X, 6);
        }

        [Fact]
        public void TheDirectionIsTakenFromTheLeftMiddleOfTheBoxOfTheOnePicked_ThoughItsLeaderIsOnTheRight()
        {
            // AutoCAD 2021: ONE from (100, 0) landing at (90, 10) has its box from 88.98 to 89.64; a direction straight up from
            // 88.98 puts the left of every box on that line.
            CadDocument document = Measured();
            DxfMLeader one = Make(document, P(100, 0), P(90, 10), "IOI");
            DxfMLeader two = Make(document, P(150, 30), P(120, 50), "SOD");
            double inkOne = Width(document, one, "IOI");
            GeoPoint2 anchor = Anchor(one, inkOne);
            Assert.Equal(one.TextLocation.X - inkOne - Gap(one), anchor.X, 6);

            Run(
                Session(document),
                "MLA",
                new[] { one.Handle, two.Handle },
                Enter,
                new[] { one.Handle },
                P(anchor.X, anchor.Y + 100)
            );

            Assert.Equal(anchor.X, BoxLeft(two, Width(document, two, "SOD")), 6);
        }

        [Fact]
        public void Distribute_PutsTheLeftOfTheFirstBoxOnTheFirstPoint_AndTheLeftOfTheLastOnTheSecond_InTheOrderOfTheirLefts()
        {
            // AutoCAD 2021: ONE, HELLO and Default_Text landing at (90, 10), (90, 40) and (90, 70) from arrowheads on their right
            // are distributed from (0, 100) to (100, 100): the one whose box starts furthest left (Default_Text) goes first, its
            // box at 0, ONE last with its box at 100, the gaps between the boxes all the same.
            CadDocument document = Measured();
            DxfMLeader one = Make(document, P(100, 0), P(90, 10), "IOI");
            DxfMLeader hello = Make(document, P(100, 30), P(90, 40), "EDOIS");
            DxfMLeader text = Make(document, P(100, 60), P(90, 70), "DIESDOFISE");
            double inkOne = Width(document, one, "IOI");
            double inkHello = Width(document, hello, "EDOIS");
            double inkText = Width(document, text, "DIESDOFISE");
            string[] handles = { one.Handle, hello.Handle, text.Handle };

            Run(Session(document), "MLA", handles, Enter, "O", "D", P(0, 100), P(100, 100));

            Assert.Equal(0.0, BoxLeft(text, inkText), 6);
            Assert.Equal(100.0, BoxLeft(one, inkOne), 6);
            double gapA = BoxLeft(hello, inkHello) - (BoxLeft(text, inkText) + inkText + 2.0 * Gap(text));
            double gapB = BoxLeft(one, inkOne) - (BoxLeft(hello, inkHello) + inkHello + 2.0 * Gap(hello));
            Assert.Equal(gapA, gapB, 6);
        }

        [Fact]
        public void WordsOfSeveralLines_AreHeldByTheMiddleOfTheirFirstLine_AndStackByTheirWholeHeight()
        {
            // AutoCAD 2021: ONE, then three lines ONE/TWO/THREE and AB, spacing 5 up the line: the boxes (the words' own heights)
            // stand 5 apart, and the words of three lines are held by the middle of their first line.
            CadDocument document = Measured();
            DxfMLeader one = Make(document, P(0, 0), P(10, 10), "IOI");
            DxfMLeader three = Make(document, P(30, 0), P(40, 25), "IOI\\PSOD\\PEDOIS");
            DxfMLeader ab = Make(document, P(60, 0), P(70, 45), "OI");
            GeoPoint2 anchor = Anchor(one, Width(document, one, "IOI"));

            Run(
                Session(document),
                "MLA",
                new[] { one.Handle, three.Handle, ab.Handle },
                Enter,
                "O",
                "S",
                "5",
                new[] { one.Handle },
                P(anchor.X, anchor.Y + 100)
            );

            double highThree = Measure(three, "IOI\\PSOD\\PEDOIS").ActualHeight;
            double highAb = Measure(ab, "OI").ActualHeight;
            Assert.True(highThree > 2.0 * Measure(one, "IOI").ActualHeight);
            Assert.Equal(one.TextLocation.Y + 5.0, three.TextLocation.Y - highThree, 6);
            Assert.Equal(three.TextLocation.Y + 5.0, ab.TextLocation.Y - highAb, 6);
        }

        [Fact]
        public void ABlockIsHeldByTheLeftOfItsExtents_AndItsLeaderTurnedWhenItsArrowheadIsPastTheMiddle()
        {
            // AutoCAD 2021: a block whose shapes fill a box from an arrowhead at (35, 50) landing at (50, 60), aligned to the left
            // of the block of the one picked, lies with its left on that line; its arrowhead is past the middle of it, so its
            // leader lands the dogleg's length past the right of the block, against x, the block staying (a rectangle block
            // 10 wide at 20.36 with its landing at 18.36 by a dogleg of 2 landed at 32.36 with its dogleg against x).
            CadDocument document = Measured();
            var tag = new DxfBlockTableRecord { Name = "TAG" };
            document.Database.BlockTable.Add(tag);
            tag.AppendEntity(new DxfCircle(GeoPoint3.Origin, new GeoVector3(0, 0, 1), 5));
            DxfMLeaderStyle style = CadMLeaderStyleEditor.Find(document.Database, "Standard");
            style.ContentType = DxfContentType.BlockContent;
            style.Block = tag;
            Run(Session(document), "MLD", P(0, 0), P(18, 10));
            Run(Session(document), "MLD", P(35, 50), P(50, 60));
            List<DxfMLeader> all = All<DxfMLeader>(document.ModelSpace);
            DxfMLeader one = all[0];
            DxfMLeader two = all[1];
            Assert.Equal(DxfContentType.BlockContent, two.ContentType);
            double radius = 5.0 * two.Scale * two.BlockScale.X;
            double left = one.BlockPosition.X - radius;
            var anchor = new GeoPoint2(left, one.BlockPosition.Y);
            Assert.Equal(1.0, two.GetDogleg(two.GetLeaderIndexes()[0]).X, 6);

            Run(
                Session(document),
                "MLA",
                new[] { one.Handle, two.Handle },
                Enter,
                new[] { one.Handle },
                P(anchor.X, anchor.Y + 100)
            );

            Assert.Equal(left + radius, two.BlockPosition.X, 6);
            Assert.Equal(-1.0, two.GetDogleg(two.GetLeaderIndexes()[0]).X, 6);
            GeoPoint3 landing = Landing(two);
            Assert.Equal(left + 2.0 * radius + Dogleg(two), landing.X, 6);
            Assert.Equal(two.BlockPosition.Y, landing.Y, 6);
        }
    }
}
