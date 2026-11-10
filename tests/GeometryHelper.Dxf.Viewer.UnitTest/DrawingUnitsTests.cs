using System;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// Lengths and angles in a drawing's units, as -UNITS's examples write them and AutoCAD reads them typed; and UNITS's
    /// dialog request, written to the header on OK as one undo step.
    /// </summary>
    public class DrawingUnitsTests
    {
        [Theory]
        [InlineData(1, 2, "1.55E+01")]
        [InlineData(2, 2, "15.50")]
        [InlineData(3, 2, "1'-3.50\"")]
        [InlineData(4, 1, "1'-3 1/2\"")]
        [InlineData(5, 1, "15 1/2")]
        public void Length_WritesFifteenAndAHalfAsTheExamplesOfUnits(int lunits, int luprec, string expected)
        {
            Assert.Equal(expected, CadUnits.Length(15.5, lunits, luprec));
        }

        [Theory]
        [InlineData(0, 4, "45.0000")]
        [InlineData(1, 4, "45d0'0\"")]
        [InlineData(2, 4, "50.0000g")]
        [InlineData(3, 4, "0.7854r")]
        [InlineData(4, 4, "N 45d0'0\" E")]
        public void Angle_WritesFortyFiveDegreesAsTheExamplesOfUnits(int aunits, int auprec, string expected)
        {
            Assert.Equal(expected, CadUnits.Angle(Math.PI / 4.0, aunits, auprec));
        }

        [Fact]
        public void TypedInput_IsReadInTheDrawingsUnits_FeetAndInchesAndGrads()
        {
            CadDocument document = Blank();
            document.Database.Lunits = 4;
            document.Database.Aunits = 2;
            CadCommandSession session = Run(Session(document), "LINE", "0,0", "1'3-1/2\"<100", Enter);
            Assert.False(session.IsRunning);
            DxfLine line = Assert.Single(All<DxfLine>(document.ModelSpace));
            Assert.Equal(0.0, line.EndPoint.X, 9);
            Assert.Equal(15.5, line.EndPoint.Y, 9);

            Assert.True(CadUnits.TryAngle("N 45d E", 0, out double bearing));
            Assert.Equal(Math.PI / 4.0, bearing, 12);
            Assert.True(CadUnits.TryLength("2'", 3, out double feet));
            Assert.Equal(24.0, feet, 12);
            Assert.False(CadUnits.TryLength("2'", 2, out _));
        }

        [Fact]
        public void Units_WithAWindow_WritesTheHeaderOnOkAsOneUndoStep_AndCancelLeavesIt()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            CadDialogAction answer = CadDialogAction.Cancel;
            session.UiRequested += (sender, e) =>
            {
                if (e.Name == "UNITS")
                {
                    var request = (CadUnitsRequest)e.Payload;
                    request.LengthUnits = 3;
                    request.AnglePrecision = 2;
                    Assert.Equal("0'-1.5000\",0'-2.0000\",0'-0.0000\"", request.SampleLength);
                    request.Action = answer;
                    e.Handled = true;
                }
            };

            Run(session, "UN");
            Assert.Equal(2, document.Database.Lunits);
            Assert.Null(document.NextUndo);

            answer = CadDialogAction.Ok;
            Run(session, "UNITS");
            Assert.Equal(3, document.Database.Lunits);
            Assert.Equal(2, document.Database.Auprec);
            Assert.Equal("UNITS", document.NextUndo.Name);
            AssertReopens(document);
        }
    }
}
