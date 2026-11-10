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
    /// What reads the layout of an MTEXT besides its picture: the grips at the corners of its box and what dragging one does,
    /// as AutoCAD 2021's GetGripPoints and MoveGripPointsAt give them, the TEXTs and LINEs EXPLODE breaks it into, and the
    /// frame the MTEXT command asks. Every text is 10 high in a font 10 above its baseline, as in
    /// <see cref="MTextLayoutTests"/>: an I is a line up at 1 moving the pen 4, and a space moves it 5.
    /// </summary>
    public sealed class MTextEditTests : IDisposable
    {
        private readonly FontFolder _folder = new FontFolder();

        public void Dispose() => _folder.Dispose();

        private CadDocument Made(string contents, double width, out DxfMText mtext, Action<DxfMText> more = null, string version = "AC1027")
        {
            _folder.Write("test.shx", ShxFiles.Unifont());
            var database = new DxfDatabase(version);
            database.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "GH_SHX", FileName = "test.shx" });
            mtext = new DxfMText { Contents = contents, Location = new GeoPoint3(0, 0, 0), TextHeight = 10.0, Width = width };
            database.ModelSpace.AppendEntity(mtext);
            mtext.TextStyleName = "GH_SHX";
            more?.Invoke(mtext);
            CadDocument document = CadDocument.From(database);
            document.Fonts = new CadFonts(new[] { _folder.FullPath }, null, null, f => f == "Arial");
            return document;
        }

        private static void At(double x, double y, CadGripKind kind, CadGrip grip)
        {
            Assert.Equal(kind, grip.Kind);
            Assert.Equal(x, grip.Point.X, 9);
            Assert.Equal(y, grip.Point.Y, 9);
        }

        [Fact]
        public void AnMTextHeldAtItsTopLeft_HasTheFourCornersOfItsBoxAsGrips_ItsLocationTheFirst()
        {
            Made("II II II", 20.0, out DxfMText mtext);

            // Two lines: the box is the 20 of the width by 10 + 50 / 3 of the lines.
            IReadOnlyList<CadGrip> grips = CadGrips.Of(mtext);
            double high = 10.0 + (50.0 / 3.0);
            Assert.Equal(4, grips.Count);
            At(0.0, 0.0, CadGripKind.Insertion, grips[0]);
            At(20.0, 0.0, CadGripKind.Corner, grips[1]);
            At(0.0, -high, CadGripKind.Corner, grips[2]);
            At(20.0, -high, CadGripKind.Corner, grips[3]);
        }

        [Fact]
        public void AnMTextHeldAtItsMiddle_HasItsLocationFirst_ThenTheFourCornersAboutIt()
        {
            Made("II II II", 20.0, out DxfMText mtext, t => t.Attachment = DxfAttachmentPoint.MiddleCenter);

            IReadOnlyList<CadGrip> grips = CadGrips.Of(mtext);
            double half = (10.0 + (50.0 / 3.0)) / 2.0;
            Assert.Equal(5, grips.Count);
            At(0.0, 0.0, CadGripKind.Insertion, grips[0]);
            At(-10.0, half, CadGripKind.Corner, grips[1]);
            At(10.0, half, CadGripKind.Corner, grips[2]);
            At(-10.0, -half, CadGripKind.Corner, grips[3]);
            At(10.0, -half, CadGripKind.Corner, grips[4]);
        }

        [Fact]
        public void AnMTextHeldAtItsTopRight_HasItsLocationAsTheSecondCorner_AndADefinedHeightIsTheHeightOfItsBox()
        {
            Made("II II II", 20.0, out DxfMText mtext, t =>
            {
                t.Attachment = DxfAttachmentPoint.TopRight;
                t.Height = 40.0;
            });

            IReadOnlyList<CadGrip> grips = CadGrips.Of(mtext);
            Assert.Equal(4, grips.Count);
            At(-20.0, 0.0, CadGripKind.Corner, grips[0]);
            At(0.0, 0.0, CadGripKind.Insertion, grips[1]);
            At(-20.0, -40.0, CadGripKind.Corner, grips[2]);
            At(0.0, -40.0, CadGripKind.Corner, grips[3]);
        }

        [Fact]
        public void AnMTextTurnedAQuarter_HasItsBoxTurnedWithIt()
        {
            Made("II II II", 20.0, out DxfMText mtext, t => t.Rotation = Math.PI / 2.0);

            IReadOnlyList<CadGrip> grips = CadGrips.Of(mtext);
            double high = 10.0 + (50.0 / 3.0);
            At(0.0, 0.0, CadGripKind.Insertion, grips[0]);
            At(0.0, 20.0, CadGripKind.Corner, grips[1]);
            At(high, 0.0, CadGripKind.Corner, grips[2]);
            At(high, 20.0, CadGripKind.Corner, grips[3]);
        }

        [Fact]
        public void AnMTextOfNoWidth_OrInColumns_HasTheGripOfItsLocationAlone()
        {
            Made("II II II", 0.0, out DxfMText plain);
            At(0.0, 0.0, CadGripKind.Insertion, Assert.Single(CadGrips.Of(plain)));

            Made("II II II", 20.0, out DxfMText columns, t =>
            {
                t.SetStaticColumns(20.0, 5.0, 2);
                t.Height = 30.0;
            });
            At(0.0, 0.0, CadGripKind.Insertion, Assert.Single(CadGrips.Of(columns)));
        }

        // AutoCAD 2021 moved each grip of a two-line MTEXT 2.5 high and 60 wide by (5, -3); here four times the size.
        [Theory]
        [InlineData(1, 260.0, 10.0 + (50.0 / 3.0) - 12.0, 0.0, -12.0)]
        [InlineData(2, 220.0, 10.0 + (50.0 / 3.0) + 12.0, 20.0, 0.0)]
        [InlineData(3, 260.0, 10.0 + (50.0 / 3.0) + 12.0, 0.0, 0.0)]
        public void ACornerDragged_TakesItsTwoEdgesOfTheBoxWithIt_TheLocationStayingAtTheTopLeft(int grip, double width, double height, double x, double y)
        {
            CadDocument document = Made(@"II\PII", 240.0, out DxfMText mtext);
            CadGrip dragged = CadGrips.Of(mtext)[grip];

            CadEditResult result = CadGrips.Drag(document, mtext.Handle, grip, P(dragged.Point.X + 20.0, dragged.Point.Y - 12.0));

            Assert.True(result.Succeeded, result.Message);
            var after = (DxfMText)document.FindEntity(mtext.Handle);
            Assert.Equal(width, after.Width, 9);
            Assert.Equal(height, after.Height, 9);
            Assert.Equal(x, after.Location.X, 9);
            Assert.Equal(y, after.Location.Y, 9);

            // One undo step puts the box back.
            Assert.True(document.Undo());
            after = (DxfMText)document.FindEntity(mtext.Handle);
            Assert.Equal(240.0, after.Width, 9);
            Assert.Equal(0.0, after.Height, 9);
            Assert.Equal(0.0, after.Location.X, 9);
        }

        [Fact]
        public void ACornerOfAnMTextHeldAtItsMiddleDragged_MovesItsLocationHalfAsFar()
        {
            CadDocument document = Made(@"II\PII", 240.0, out DxfMText mtext, t => t.Attachment = DxfAttachmentPoint.MiddleCenter);
            CadGrip topLeft = CadGrips.Of(mtext)[1];

            Assert.True(CadGrips.Drag(document, mtext.Handle, 1, P(topLeft.Point.X + 20.0, topLeft.Point.Y - 12.0)).Succeeded);

            var after = (DxfMText)document.FindEntity(mtext.Handle);
            Assert.Equal(220.0, after.Width, 9);
            Assert.Equal(10.0 + (50.0 / 3.0) - 12.0, after.Height, 9);
            Assert.Equal(10.0, after.Location.X, 9);
            Assert.Equal(-6.0, after.Location.Y, 9);
        }

        [Fact]
        public void ABoxDraggedLowerThanItsLetters_StaysAsHighAsThey_AndOneDraggedToNoWidthIsRefused()
        {
            CadDocument document = Made("II", 240.0, out DxfMText mtext);

            // The top right corner taken 12 down: the box of one line, 10 high, would be -2 high.
            Assert.True(CadGrips.Drag(document, mtext.Handle, 1, P(260.0, -12.0)).Succeeded);
            var after = (DxfMText)document.FindEntity(mtext.Handle);
            Assert.Equal(10.0, after.Height, 9);
            Assert.Equal(-12.0, after.Location.Y, 9);

            CadEditResult refused = CadGrips.Drag(document, mtext.Handle, 1, P(0.0, -12.0));
            Assert.True(refused.Refused);
            Assert.Equal("An MTEXT needs a box of some width.", refused.Message);
        }

        [Fact]
        public void ACornerDraggedInADrawingOfR2004_ChangesTheWidthAlone_AsItsFileHoldsNoHeightOfTheBox()
        {
            CadDocument document = Made(@"II\PII", 240.0, out DxfMText mtext, null, "AC1018");

            Assert.True(CadGrips.Drag(document, mtext.Handle, 1, P(260.0, -12.0)).Succeeded);

            var after = (DxfMText)document.FindEntity(mtext.Handle);
            Assert.Equal(260.0, after.Width, 9);
            Assert.Equal(0.0, after.Height, 9);
            Assert.Equal(0.0, after.Location.Y, 9);
        }

        [Fact]
        public void TheGripOfTheLocation_MovesTheMTextWhole()
        {
            CadDocument document = Made("II II II", 20.0, out DxfMText mtext, t => t.Attachment = DxfAttachmentPoint.TopRight);

            Assert.True(CadGrips.Drag(document, mtext.Handle, 1, P(5.0, -3.0)).Succeeded);

            var after = (DxfMText)document.FindEntity(mtext.Handle);
            Assert.Equal(5.0, after.Location.X, 9);
            Assert.Equal(-3.0, after.Location.Y, 9);
            Assert.Equal(20.0, after.Width, 9);
        }

        [Fact]
        public void Explode_MakesATextOfEachRunWhereThePictureDrawsIt_AndALineOfTheBarOfAStack()
        {
            CadDocument document = Made(@"II II \LII\l \SI/I; II", 40.0, out DxfMText mtext, t => t.Layer = "0");
            DisplayText2 shown = document.GetDisplay(document.ModelSpace).Items.OfType<DisplayText2>().Single();
            List<double[]> before = shown.Parts.OfType<DisplayText2>().Select(p => new[] { p.X, p.Y, p.Height }).ToList();
            Assert.Equal(6, before.Count);

            CadCommandSession session = Run(Session(document), "EXPLODE", new[] { mtext.Handle }, Enter);

            Assert.False(session.IsRunning);
            Assert.Empty(All<DxfMText>(document.ModelSpace));
            List<DxfDBText> texts = All<DxfDBText>(document.ModelSpace).ToList();
            Assert.All(texts, t => Assert.Equal("GH_SHX", t.TextStyleName));
            Assert.Contains(texts, t => t.TextString == "%%UII");
            List<DxfDBText> stack = texts.Where(t => t.TextString == "I").OrderBy(t => t.Position.Y).ToList();
            Assert.Equal(2, stack.Count);

            // Each TEXT stands where its run of the MTEXT was drawn, and is as high.
            List<double[]> after = texts.Select(t => new[] { t.Position.X, t.Position.Y, t.Height }).ToList();
            Comparison<double[]> order = (a, b) => Math.Abs(a[1] - b[1]) > 1E-6 ? b[1].CompareTo(a[1]) : Math.Abs(a[0] - b[0]) > 1E-6 ? a[0].CompareTo(b[0]) : a[2].CompareTo(b[2]);
            before.Sort(order);
            after.Sort(order);

            Assert.Equal(before.Count, after.Count);
            for (int i = 0; i < before.Count; i++)
            {
                Assert.Equal(before[i][0], after[i][0], 9);
                Assert.Equal(before[i][1], after[i][1], 9);
                Assert.Equal(before[i][2], after[i][2], 9);
            }

            // The bar of the stack is a LINE, level, between the top of the lower I and the foot of the upper.
            DxfLine bar = Assert.Single(All<DxfLine>(document.ModelSpace));
            Assert.Equal(bar.StartPoint.Y, bar.EndPoint.Y, 9);
            Assert.True(bar.StartPoint.Y > stack[0].Position.Y + stack[0].Height);
            Assert.True(bar.StartPoint.Y < stack[1].Position.Y);
            Assert.Equal("0", bar.Layer);

            // One undo step brings the MTEXT back.
            Assert.True(document.Undo());
            Assert.Single(All<DxfMText>(document.ModelSpace));
            Assert.Empty(All<DxfDBText>(document.ModelSpace));
        }

        [Theory]
        [InlineData("TL", 0.0, 40.0)]
        [InlineData("MC", 60.0, 20.0)]
        [InlineData("BR", 120.0, 0.0)]
        public void TheFrameTheMTextCommandAsks_IsTheBoxTheWordsAreSetIn_ItsSidesTheSidesOfTheGrips(string justify, double x, double y)
        {
            CadDocument document = Blank();
            Run(Session(document), "MTEXT", P(0, 0), "J", justify, P(120, 40), "Plate 300 x 200");

            DxfMText text = All<DxfMText>(document.ModelSpace).Single();
            Assert.Equal(120.0, text.Width, 9);
            Assert.Equal(x, text.Location.X, 9);
            Assert.Equal(y, text.Location.Y, 9);

            // The words wrap between the two sides of the frame picked, whatever the corner or middle they hang by.
            IReadOnlyList<CadGrip> corners = CadGrips.Of(text).Where(g => !(Math.Abs(g.Point.X - x) < 1E-9 && Math.Abs(g.Point.Y - y) < 1E-9 && g.Kind == CadGripKind.Insertion) || justify != "MC").ToList();
            Assert.Equal(0.0, corners.Min(g => g.Point.X), 9);
            Assert.Equal(120.0, corners.Max(g => g.Point.X), 9);
        }
    }
}
