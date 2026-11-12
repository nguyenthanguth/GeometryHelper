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
    /// The grips of an MTEXT in columns and what dragging each does, as AutoCAD 2021's GetGripPoints and MoveGripPointsAt
    /// gave them (probed on columns 12 wide with a gutter of 2 and a height of 10; here ten times the size): the location,
    /// the first column's width, the corner, the height and the last column's edge, which moves the gutter; dynamic columns of
    /// their own heights a height grip under the middle of each column and no corner. Every text is 10 high in a font 10 above
    /// its baseline, its lines 50/3 apart, so a column 100 high holds six lines.
    /// </summary>
    public sealed class MTextColumnGripTests : IDisposable
    {
        private const string EightLines = @"II\PII\PII\PII\PII\PII\PII\PII";

        private readonly FontFolder _folder = new FontFolder();

        public void Dispose() => _folder.Dispose();

        private CadDocument Made(
            string contents,
            out DxfMText mtext,
            Action<DxfMText> columns,
            DxfAttachmentPoint attachment = DxfAttachmentPoint.TopLeft
        )
        {
            _folder.Write("test.shx", ShxFiles.Unifont());
            var database = new DxfDatabase("AC1027");
            database.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "GH_SHX", FileName = "test.shx" });
            mtext = new DxfMText
            {
                Contents = contents,
                Location = new GeoPoint3(0, 0, 0),
                TextHeight = 10.0,
                Width = 120.0,
                Attachment = attachment,
            };
            database.ModelSpace.AppendEntity(mtext);
            mtext.TextStyleName = "GH_SHX";
            columns(mtext);
            CadDocument document = CadDocument.From(database);
            document.Fonts = new CadFonts(new[] { _folder.FullPath }, null, null, f => f == "Arial");
            return document;
        }

        private static void Static(DxfMText t)
        {
            t.Height = 100.0;
            t.SetStaticColumns(120.0, 20.0, 2);
        }

        private static void Dynamic(DxfMText t)
        {
            t.SetDynamicColumns(120.0, 20.0, true);
            t.Height = 100.0;
        }

        private static void Manual(DxfMText t)
        {
            t.SetDynamicColumns(120.0, 20.0, false);
            t.ColumnCount = 3;
            t.SetColumnHeight(0, 80.0);
            t.SetColumnHeight(1, 65.0);
            t.SetColumnHeight(2, 90.0);
        }

        private static void At(double x, double y, CadGripKind kind, CadGrip grip)
        {
            Assert.Equal(kind, grip.Kind);
            Assert.Equal(x, grip.Point.X, 9);
            Assert.Equal(y, grip.Point.Y, 9);
        }

        private static void Way(double x, double y, CadGrip grip)
        {
            Assert.Equal(x, grip.Direction.X, 9);
            Assert.Equal(y, grip.Direction.Y, 9);
        }

        private static DxfMText After(CadDocument document, DxfMText mtext) =>
            (DxfMText)document.FindEntity(mtext.Handle);

        /// <summary>Drags a grip by a step from where it is, as AutoCAD's MoveGripPointsAt was given an offset.</summary>
        private static void Drag(CadDocument document, DxfMText mtext, int grip, double dx, double dy)
        {
            CadGrip dragged = CadGrips.Of(document.FindEntity(mtext.Handle))[grip];
            CadEditResult result = CadGrips.Drag(
                document,
                mtext.Handle,
                grip,
                P(dragged.Point.X + dx, dragged.Point.Y + dy)
            );
            Assert.True(result.Succeeded, result.Message);
        }

        [Theory]
        [InlineData("static")]
        [InlineData("dynamic")]
        public void ColumnsOfOneHeight_HaveTheLocation_TheColumnWidth_TheCorner_TheHeight_AndTheGutterGrip(string kind)
        {
            Made(EightLines, out DxfMText mtext, kind == "static" ? (Action<DxfMText>)Static : Dynamic);

            IReadOnlyList<CadGrip> grips = CadGrips.Of(mtext);

            Assert.Equal(5, grips.Count);
            At(0.0, 0.0, CadGripKind.Insertion, grips[0]);
            At(120.0, 0.0, CadGripKind.MTextColumnWidth, grips[1]);
            At(260.0, -100.0, CadGripKind.MTextSize, grips[2]);
            At(0.0, -100.0, CadGripKind.MTextHeight, grips[3]);
            At(260.0, 0.0, CadGripKind.MTextGutter, grips[4]);
            Way(1.0, 0.0, grips[1]);
            Way(1.0, -1.0, grips[2]);
            Way(0.0, -1.0, grips[3]);
            Way(1.0, 0.0, grips[4]);
        }

        [Fact]
        public void DynamicColumnsOfTheirOwnHeights_HaveAHeightGripUnderTheMiddleOfEachColumn_AndNoCorner()
        {
            Made(EightLines + @"\PII\PII\PII\PII", out DxfMText mtext, Manual);

            IReadOnlyList<CadGrip> grips = CadGrips.Of(mtext);

            Assert.Equal(6, grips.Count);
            At(0.0, 0.0, CadGripKind.Insertion, grips[0]);
            At(120.0, 0.0, CadGripKind.MTextColumnWidth, grips[1]);
            At(60.0, -80.0, CadGripKind.MTextColumnHeight, grips[2]);
            At(200.0, -65.0, CadGripKind.MTextColumnHeight, grips[3]);
            At(340.0, -90.0, CadGripKind.MTextColumnHeight, grips[4]);
            At(400.0, 0.0, CadGripKind.MTextGutter, grips[5]);
        }

        [Fact]
        public void HeldAtTheMiddle_TheGripsSitOnTheLocationsLines_AndHeldAtTheBottomRight_TheyPointTheOtherWay()
        {
            Made(EightLines, out DxfMText middle, Static, DxfAttachmentPoint.MiddleCenter);
            IReadOnlyList<CadGrip> grips = CadGrips.Of(middle);
            At(0.0, 0.0, CadGripKind.Insertion, grips[0]);
            At(-10.0, 0.0, CadGripKind.MTextColumnWidth, grips[1]);
            At(130.0, -50.0, CadGripKind.MTextSize, grips[2]);
            At(0.0, -50.0, CadGripKind.MTextHeight, grips[3]);
            At(130.0, 0.0, CadGripKind.MTextGutter, grips[4]);

            Made(EightLines, out DxfMText corner, Static, DxfAttachmentPoint.BottomRight);
            grips = CadGrips.Of(corner);
            At(0.0, 0.0, CadGripKind.Insertion, grips[0]);
            At(-120.0, 0.0, CadGripKind.MTextColumnWidth, grips[1]);
            At(-260.0, 100.0, CadGripKind.MTextSize, grips[2]);
            At(0.0, 100.0, CadGripKind.MTextHeight, grips[3]);
            At(-260.0, 0.0, CadGripKind.MTextGutter, grips[4]);
            Way(-1.0, 0.0, grips[1]);
            Way(-1.0, 1.0, grips[2]);
            Way(0.0, 1.0, grips[3]);
        }

        // AutoCAD 2021 on two static columns 12 wide, gutter 2, height 10: the first column's grip by (-3, 2) made the column 9
        // and the gutter 8, the total kept; by (5, 0) the column 13, the gutter 0 (no wider); the last column's by (5, 0) the
        // total 31, the gutter 7; by (-3, 0) the gutter 0, the total 24 (no narrower); the corner by (5, -3) the total 31 and the
        // height 13; the height grip by (0, -4) the height 14. The location stays where it is.
        [Theory]
        [InlineData(1, -30.0, 20.0, 260.0, 90.0, 80.0, 100.0)]
        [InlineData(1, 50.0, 0.0, 260.0, 130.0, 0.0, 100.0)]
        [InlineData(4, 50.0, 0.0, 310.0, 120.0, 70.0, 100.0)]
        [InlineData(4, -30.0, 0.0, 240.0, 120.0, 0.0, 100.0)]
        [InlineData(2, 50.0, -30.0, 310.0, 120.0, 70.0, 130.0)]
        [InlineData(3, 0.0, -40.0, 260.0, 120.0, 20.0, 140.0)]
        public void AGripOfStaticColumnsDragged_ChangesWhatAutoCadsDoes_OneUndoStep(
            int grip,
            double dx,
            double dy,
            double total,
            double column,
            double gutter,
            double height
        )
        {
            CadDocument document = Made(EightLines, out DxfMText mtext, Static);

            Drag(document, mtext, grip, dx, dy);

            DxfMText after = After(document, mtext);
            Assert.Equal(DxfColumnType.StaticColumns, after.ColumnType);
            Assert.Equal(2, after.ColumnCount);
            Assert.Equal(total, after.Width, 9);
            Assert.Equal(column, after.ColumnWidth, 9);
            Assert.Equal(gutter, after.ColumnGutterWidth, 9);
            Assert.Equal(height, after.Height, 9);
            Assert.Equal(0.0, after.Location.X, 9);
            Assert.Equal(0.0, after.Location.Y, 9);

            Assert.True(document.Undo());
            after = After(document, mtext);
            Assert.Equal(120.0, after.ColumnWidth, 9);
            Assert.Equal(20.0, after.ColumnGutterWidth, 9);
            Assert.Equal(100.0, after.Height, 9);
        }

        // AutoCAD 2021 on dynamic columns 12 wide, gutter 2, auto height 6: the first column's grip by (5, -3) made them 17 wide,
        // the gutter kept; the corner by (5, -3) 17 wide and 9 high; the height grip by (0, -4) 10 high; the last column's grip
        // by (5, 0), four columns laid, the gutter 2 + 5/3.
        [Theory]
        [InlineData(1, 50.0, -30.0, 170.0, 20.0, 100.0)]
        [InlineData(2, 50.0, -30.0, 170.0, 20.0, 130.0)]
        [InlineData(3, 0.0, -40.0, 120.0, 20.0, 140.0)]
        [InlineData(4, 60.0, 0.0, 120.0, 80.0, 100.0)]
        public void AGripOfDynamicColumnsDragged_ChangesWhatAutoCadsDoes_OneUndoStep(
            int grip,
            double dx,
            double dy,
            double column,
            double gutter,
            double height
        )
        {
            CadDocument document = Made(EightLines, out DxfMText mtext, Dynamic);

            Drag(document, mtext, grip, dx, dy);

            DxfMText after = After(document, mtext);
            Assert.Equal(DxfColumnType.DynamicColumns, after.ColumnType);
            Assert.True(after.ColumnAutoHeight);
            Assert.Equal(column, after.ColumnWidth, 9);
            Assert.Equal(gutter, after.ColumnGutterWidth, 9);
            Assert.Equal(height, after.Height, 9);
            Assert.Equal(0.0, after.Location.Y, 9);

            Assert.True(document.Undo());
            after = After(document, mtext);
            Assert.Equal(120.0, after.ColumnWidth, 9);
            Assert.Equal(20.0, after.ColumnGutterWidth, 9);
            Assert.Equal(100.0, after.Height, 9);
        }

        // AutoCAD 2021 on dynamic columns of heights 8, 6, 9: the second column's grip by (5, -3) made it 9, x ignored; the last
        // column's by (5, -3) the gutter 4.5 (the total 45 over three columns).
        [Fact]
        public void AColumnsOwnHeightGrip_SetsThatColumnsHeight_AndTheLastGripTheGutter()
        {
            CadDocument document = Made(EightLines + @"\PII\PII\PII\PII", out DxfMText mtext, Manual);

            Drag(document, mtext, 3, 50.0, -30.0);
            DxfMText after = After(document, mtext);
            Assert.Equal(80.0, after.GetColumnHeight(0), 9);
            Assert.Equal(95.0, after.GetColumnHeight(1), 9);
            Assert.Equal(90.0, after.GetColumnHeight(2), 9);
            Assert.Equal(120.0, after.ColumnWidth, 9);

            Drag(document, mtext, 5, 50.0, -30.0);
            after = After(document, mtext);
            Assert.Equal(45.0, after.ColumnGutterWidth, 9);
            Assert.Equal(95.0, after.GetColumnHeight(1), 9);

            Assert.True(document.Undo());
            Assert.Equal(20.0, After(document, mtext).ColumnGutterWidth, 9);
            Assert.True(document.Undo());
            Assert.Equal(65.0, After(document, mtext).GetColumnHeight(1), 9);
        }

        // AutoCAD 2021 held at the bottom right: the first column's grip by (5, 0) made static columns 7 wide, by (-3, 2) 13.
        [Fact]
        public void HeldAtTheBottomRight_AGripDraggedAwayFromTheLocation_Grows()
        {
            CadDocument document = Made(EightLines, out DxfMText mtext, Static, DxfAttachmentPoint.BottomRight);

            Drag(document, mtext, 1, 50.0, 0.0);
            Assert.Equal(70.0, After(document, mtext).ColumnWidth, 9);

            Drag(document, mtext, 3, 0.0, 30.0);
            Assert.Equal(130.0, After(document, mtext).Height, 9);
            Assert.Equal(0.0, After(document, mtext).Location.X, 9);
        }

        [Fact]
        public void AColumnOrAHeightDraggedPastNothing_StopsAtTheTextHeight()
        {
            CadDocument document = Made(EightLines, out DxfMText mtext, Dynamic);

            Drag(document, mtext, 1, -500.0, 0.0);
            Assert.Equal(10.0, After(document, mtext).ColumnWidth, 9);

            Drag(document, mtext, 3, 0.0, 500.0);
            Assert.Equal(10.0, After(document, mtext).Height, 9);
        }

        [Fact]
        public void TheGripsOfAnMTextOfNoColumns_PointAsTheirShapes_TheTopLeftAndTheLocationSquares()
        {
            Made("II", out DxfMText mtext, t => t.Height = 30.0);

            IReadOnlyList<CadGrip> grips = CadGrips.Of(mtext);

            // The drags stay as they were: four corners, the top left the location.
            Assert.Equal(
                new[] { CadGripKind.Insertion, CadGripKind.Corner, CadGripKind.Corner, CadGripKind.Corner },
                grips.Select(g => g.Kind)
            );
            Way(0.0, 0.0, grips[0]);
            Way(1.0, 0.0, grips[1]);
            Way(0.0, -1.0, grips[2]);
            Way(1.0, -1.0, grips[3]);

            Made("II", out DxfMText middle, t =>
            {
                t.Height = 30.0;
                t.Attachment = DxfAttachmentPoint.MiddleCenter;
            });
            grips = CadGrips.Of(middle);
            Way(0.0, 0.0, grips[0]);
            Way(0.0, 0.0, grips[1]);
            Way(1.0, 0.0, grips[2]);
            Way(0.0, -1.0, grips[3]);
            Way(1.0, -1.0, grips[4]);
        }

        [Fact]
        public void TheGutterGripOfAColumnsGripEdit_SetsTheGutter_ThroughAutoCadsStretch()
        {
            CadDocument document = Made(EightLines, out DxfMText mtext, Static);
            CadCommandSession session = Session(document);

            Assert.True(session.StartGripEdit(new[] { new KeyValuePair<string, int>(mtext.Handle, 4) }));
            Run(session, P(300.0, 5.0));

            Assert.False(session.IsRunning);
            DxfMText after = After(document, mtext);
            Assert.Equal(300.0, after.Width, 9);
            Assert.Equal(60.0, after.ColumnGutterWidth, 9);
            Assert.True(document.Undo());
            Assert.Equal(20.0, After(document, mtext).ColumnGutterWidth, 9);
        }
    }
}
