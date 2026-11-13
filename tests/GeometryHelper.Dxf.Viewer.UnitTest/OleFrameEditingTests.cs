using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;
using static GeometryHelper.Dxf.Viewer.UnitTest.OleFrameDisplayTests;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// OLE frames edited as AutoCAD 2021 lets them be: four corner grips that scale the frame about the opposite corner,
    /// MOVE, COPY, SCALE, MIRROR and ROTATE that keep a frame upright (a turn moves it round the base point and leaves it
    /// along x and y, as the GUI does), and the rows of its Properties palette.
    /// </summary>
    public class OleFrameEditingTests
    {
        private static GeoAabb3 Box(CadDocument document, string handle) =>
            Entity<DxfOle2Frame>(document, handle).GeometricExtents;

        private static void AssertBox(double left, double bottom, double right, double top, GeoAabb3 box)
        {
            Assert.Equal(left, box.Min.X, 6);
            Assert.Equal(bottom, box.Min.Y, 6);
            Assert.Equal(right, box.Max.X, 6);
            Assert.Equal(top, box.Max.Y, 6);
        }

        [Fact]
        public void AFrame_HasFourCornerGrips_FromItsLowerLeftRoundToItsLowerRight_AsAutoCadGivesThem()
        {
            Drawing(out DxfOle2Frame frame);

            var grips = CadGrips.Of(frame);

            Assert.Equal(4, grips.Count);
            Assert.All(grips, g => Assert.Equal(CadGripKind.Corner, g.Kind));
            Near(new GeoPoint2(10, 20), grips[0].Point);
            Near(new GeoPoint2(10, 40), grips[1].Point);
            Near(new GeoPoint2(50, 40), grips[2].Point);
            Near(new GeoPoint2(50, 20), grips[3].Point);
        }

        [Fact]
        public void ACornerDragged_ScalesTheFrameAboutTheOppositeCorner_BothSidesByTheLargerFactorWhileTheAspectIsLocked()
        {
            CadDocument document = Drawing(out DxfOle2Frame frame);
            double before = frame.ScaleWidth;

            // The upper right corner from (50, 40) to (60, 45): 50 wide by 25 high is 1.25 by 1.25 times 40 by 20, from (10, 20).
            CadEditResult result = CadGrips.Drag(document, frame.Handle, 2, new GeoPoint2(60, 45));

            Assert.True(result.Succeeded, result.Message);
            AssertBox(10, 20, 60, 45, Box(document, frame.Handle));
            Assert.Equal(before * 1.25, Entity<DxfOle2Frame>(document, frame.Handle).ScaleWidth, 6);
        }

        [Fact]
        public void ACornerDragged_WithTheAspectLocked_TakesTheLargerOfTheTwoFactors()
        {
            CadDocument document = Drawing(out DxfOle2Frame frame);

            // To (50, 50): the width stays (1.0) and the height goes from 20 to 30 (1.5), so both grow 1.5 times: 60 by 30.
            Assert.True(CadGrips.Drag(document, frame.Handle, 2, new GeoPoint2(50, 50)).Succeeded);
            AssertBox(10, 20, 70, 50, Box(document, frame.Handle));
        }

        [Fact]
        public void ACornerDragged_WithTheAspectUnlocked_ScalesEachSideByItsOwnFactor()
        {
            CadDocument document = Drawing(out DxfOle2Frame frame);
            frame.LockAspect = false;

            Assert.True(CadGrips.Drag(document, frame.Handle, 2, new GeoPoint2(60, 45)).Succeeded);
            AssertBox(10, 20, 60, 45, Box(document, frame.Handle));
        }

        [Fact]
        public void ALowerLeftCorner_KeepsTheUpperRightCorner_WhereItIs()
        {
            CadDocument document = Drawing(out DxfOle2Frame frame);
            frame.LockAspect = false;

            Assert.True(CadGrips.Drag(document, frame.Handle, 0, new GeoPoint2(20, 25)).Succeeded);
            AssertBox(20, 25, 50, 40, Box(document, frame.Handle));
        }

        [Fact]
        public void ACornerDraggedPastTheOppositeCorner_DoesNotTurnTheFrameOver_ItsFactorsTakenWithoutTheirSigns()
        {
            CadDocument document = Drawing(out DxfOle2Frame frame);

            // To (-50, 10): 60 to the left of x = 10 is 1.5 times the 40 wide, 10 below y = 20 half the 20 high; the larger,
            // 1.5, makes the frame 60 by 30, still to the right of x = 10 and above y = 20.
            Assert.True(CadGrips.Drag(document, frame.Handle, 2, new GeoPoint2(-50, 10)).Succeeded);
            AssertBox(10, 20, 70, 50, Box(document, frame.Handle));
        }

        [Fact]
        public void ACornerDraggedOntoTheOppositeCornersLine_IsRefused_AFrameNeedsAWidthAndAHeight()
        {
            CadDocument document = Drawing(out DxfOle2Frame frame);
            frame.LockAspect = false;

            CadEditResult result = CadGrips.Drag(document, frame.Handle, 2, new GeoPoint2(10, 45));

            Assert.True(result.Refused);
            AssertBox(10, 20, 50, 40, Box(document, frame.Handle));
        }

        [Fact]
        public void ACornerDragged_IsOneUndoStep()
        {
            CadDocument document = Drawing(out DxfOle2Frame frame);

            CadGrips.Drag(document, frame.Handle, 2, new GeoPoint2(60, 45));
            Assert.True(document.Undo());

            AssertBox(10, 20, 50, 40, Box(document, frame.Handle));
        }

        [Fact]
        public void AFrameMoved_KeepsItsSize_AndAFrameScaledAboutItsCorner_GrowsFromThere()
        {
            CadDocument document = Drawing(out DxfOle2Frame frame);
            CadCommandSession session = Session(document);

            Run(session, "MOVE", new[] { frame.Handle }, Enter, P(0, 0), P(5, 5));
            AssertBox(15, 25, 55, 45, Box(document, frame.Handle));

            Run(session, "SCALE", new[] { frame.Handle }, Enter, P(15, 25), "2");
            AssertBox(15, 25, 95, 65, Box(document, frame.Handle));
        }

        [Fact]
        public void ARotatedFrame_StaysUpright_ItsCentreGoingRoundTheBasePoint_AsAutoCadsGuiMovesIt()
        {
            CadDocument document = Drawing(out DxfOle2Frame frame);

            // The centre (30, 30) turned 90 degrees about the origin goes to (-30, 30); the frame is 40 by 20 still.
            Run(Session(document), "ROTATE", new[] { frame.Handle }, Enter, P(0, 0), "90");

            DxfOle2Frame turned = Entity<DxfOle2Frame>(document, frame.Handle);
            AssertBox(-50, 20, -10, 40, turned.GeometricExtents);
            Assert.Equal(0.0, turned.Rotation, 12);
            Assert.Equal(40.0, turned.WcsWidth, 6);
            Assert.Equal(20.0, turned.WcsHeight, 6);
        }

        [Fact]
        public void AFrameRotatedThirtyDegrees_IsMovedNotTurned_ItsSizeAndItsRotationUnchanged()
        {
            CadDocument document = Drawing(out DxfOle2Frame frame);
            double cos = Math.Cos(Math.PI / 6);
            double sin = Math.Sin(Math.PI / 6);
            double x = 30 * cos - 30 * sin;
            double y = 30 * sin + 30 * cos;

            Run(Session(document), "ROTATE", new[] { frame.Handle }, Enter, P(0, 0), "30");

            DxfOle2Frame moved = Entity<DxfOle2Frame>(document, frame.Handle);
            AssertBox(x - 20, y - 10, x + 20, y + 10, moved.GeometricExtents);
            Assert.Equal(0.0, moved.Rotation, 12);
            Assert.Equal(40.0, moved.WcsWidth, 6);
        }

        [Fact]
        public void ARotateWithCopy_LeavesTheFrameWhereItIs_AndMakesAnUprightCopyRoundTheBasePoint()
        {
            CadDocument document = Drawing(out DxfOle2Frame frame);

            Run(Session(document), "ROTATE", new[] { frame.Handle }, Enter, P(0, 0), "C", "90");

            DxfOle2Frame[] frames = All<DxfOle2Frame>(document.ModelSpace).ToArray();
            Assert.Equal(2, frames.Length);
            AssertBox(10, 20, 50, 40, frames[0].GeometricExtents);
            AssertBox(-50, 20, -10, 40, frames[1].GeometricExtents);
        }

        [Fact]
        public void AMirrorAboutALineAlongTheAxis_BoxesTheMirroredCorners_AndOneAcrossItMovesTheFrameUpright()
        {
            CadDocument document = Drawing(out DxfOle2Frame frame);
            CadCommandSession session = Session(document);

            // About the line x = 0, the source kept: the copy is the box of the mirrored corners, the picture not turned over.
            Run(session, "MIRROR", new[] { frame.Handle }, Enter, P(0, 0), P(0, 10), Enter);
            DxfOle2Frame[] frames = All<DxfOle2Frame>(document.ModelSpace).ToArray();
            Assert.Equal(2, frames.Length);
            AssertBox(10, 20, 50, 40, frames[0].GeometricExtents);
            AssertBox(-50, 20, -10, 40, frames[1].GeometricExtents);
            Assert.True(document.Undo());
            Assert.Single(All<DxfOle2Frame>(document.ModelSpace));

            // About y = x + 10 (through (0, 10) and (10, 20)): the centre (30, 30) lands at (20, 40), the copy upright and as large.
            Run(session, "MIRROR", new[] { frame.Handle }, Enter, P(0, 10), P(10, 20), Enter);
            frames = All<DxfOle2Frame>(document.ModelSpace).ToArray();
            Assert.Equal(2, frames.Length);
            AssertBox(0, 30, 40, 50, frames[1].GeometricExtents);
        }

        [Fact]
        public void APropertiesPalette_ListsAFramesPositionSizeScaleLockTypeQualityAndSource()
        {
            CadDocument document = Drawing(out DxfOle2Frame frame);

            CadSelectionInfo info = CadProperties.Describe(document, null, new[] { frame.Handle });

            Assert.Equal("OLE Object (1)", info.Label);
            CadPropertyGroup group = Assert.Single(info.Groups, g => g.Title == "OLE");
            Assert.Equal(
                new[]
                {
                    "Position X",
                    "Position Y",
                    "Width",
                    "Height",
                    "Scale width",
                    "Scale height",
                    "Lock aspect",
                    "Type",
                    "Plot quality",
                    "Source application",
                },
                group.Rows.Select(r => r.Label).ToArray()
            );

            string Row(string label) => group.Rows.Single(r => r.Label == label).Value;

            Assert.Equal("10.0000", Row("Position X"));
            Assert.Equal("20.0000", Row("Position Y"));
            Assert.Equal("40.0000", Row("Width"));
            Assert.Equal("20.0000", Row("Height"));
            Assert.Equal("Yes", Row("Lock aspect"));
            Assert.Equal("Static", Row("Type"));
            Assert.StartsWith("High graphics", Row("Plot quality"));
            Assert.Equal("Picture (Device Independent Bitmap)", Row("Source application"));
            Assert.True(group.Rows.Single(r => r.Label == "Type").IsReadOnly);
            Assert.True(group.Rows.Single(r => r.Label == "Source application").IsReadOnly);
            Assert.False(group.Rows.Single(r => r.Label == "Width").IsReadOnly);
        }

        [Fact]
        public void TheScaleRows_ShowTheSizeInPercentOfTheSizeAt100Percent_AsAutoCadsPaletteDoes()
        {
            CadDocument document = Drawing(out DxfOle2Frame frame);
            double before = frame.ScaleWidth;

            Run(Session(document), "SCALE", new[] { frame.Handle }, Enter, P(10, 20), "0.5");

            DxfOle2Frame scaled = Entity<DxfOle2Frame>(document, frame.Handle);
            Assert.Equal(before, scaled.ScaleWidth * 2.0, 6);
            CadSelectionInfo info = CadProperties.Describe(document, null, new[] { frame.Handle });
            string text = info.Groups.Single(g => g.Title == "OLE").Rows.Single(r => r.Label == "Scale width").Value;
            Assert.EndsWith("%", text);
            Assert.Equal(
                scaled.ScaleWidth,
                double.Parse(text.TrimEnd('%'), System.Globalization.CultureInfo.InvariantCulture),
                2
            );
        }

        [Fact]
        public void TheWidthRow_SizesTheFrameFromItsUpperLeftCorner_TheHeightFollowingWhileTheAspectIsLocked()
        {
            CadDocument document = Drawing(out DxfOle2Frame frame);

            CadEditResult result = CadPropertyEditor.Set(document, new[] { frame.Handle }, "Width", "60");

            Assert.True(result.Succeeded, result.Message);
            AssertBox(10, 10, 70, 40, Box(document, frame.Handle));
            Assert.True(document.Undo());
            AssertBox(10, 20, 50, 40, Box(document, frame.Handle));
        }

        [Fact]
        public void ThePositionRows_MoveTheFrameByItsLowerLeftCorner()
        {
            CadDocument document = Drawing(out DxfOle2Frame frame);

            Assert.True(CadPropertyEditor.Set(document, new[] { frame.Handle }, "Position X", "30").Succeeded);
            Assert.True(CadPropertyEditor.Set(document, new[] { frame.Handle }, "Position Y", "5").Succeeded);

            AssertBox(30, 5, 70, 25, Box(document, frame.Handle));
        }

        [Fact]
        public void TheScaleWidthRow_SetsTheSizeInPercent_AndTheLockAndQualityRowsTheirOwnFlags()
        {
            CadDocument document = Drawing(out DxfOle2Frame frame);
            double natural = 40.0 / (frame.ScaleWidth / 100.0);

            Assert.True(CadPropertyEditor.Set(document, new[] { frame.Handle }, "Scale width", "50%").Succeeded);
            Assert.Equal(natural / 2.0, Entity<DxfOle2Frame>(document, frame.Handle).WcsWidth, 6);
            Assert.Equal(natural / 4.0, Entity<DxfOle2Frame>(document, frame.Handle).WcsHeight, 6);

            Assert.True(CadPropertyEditor.Set(document, new[] { frame.Handle }, "Lock aspect", "No").Succeeded);
            Assert.False(Entity<DxfOle2Frame>(document, frame.Handle).LockAspect);

            Assert.True(
                CadPropertyEditor.Set(document, new[] { frame.Handle }, "Plot quality", "Monochrome").Succeeded
            );
            Assert.Equal(0, Entity<DxfOle2Frame>(document, frame.Handle).OutputQuality);
        }

        [Fact]
        public void TheTypeAndSourceRows_AreRead_NotSet_AndAWidthOfNoWidthIsRefused()
        {
            CadDocument document = Drawing(out DxfOle2Frame frame);

            Assert.True(CadPropertyEditor.Set(document, new[] { frame.Handle }, "Type", "Linked").Refused);
            Assert.True(CadPropertyEditor.Set(document, new[] { frame.Handle }, "Source application", "x").Refused);
            Assert.True(CadPropertyEditor.Set(document, new[] { frame.Handle }, "Width", "0").Refused);
            Assert.True(CadPropertyEditor.Set(document, new[] { frame.Handle }, "Width", "abc").Refused);
            Assert.True(CadPropertyEditor.Set(document, new[] { frame.Handle }, "Lock aspect", "Maybe").Refused);
            AssertBox(10, 20, 50, 40, Box(document, frame.Handle));
        }
    }
}
