using System;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The VPORT table: the snap and the grid of the <c>*Active</c> viewport, SNAPMODE, GRIDMODE, SNAPUNIT, GRIDUNIT, SNAPBASE,
    /// SNAPANG, SNAPSTYL, SNAPISOPAIR, GRIDDISPLAY and GRIDMAJOR as AutoCAD 2021 reads them from its groups, written in place
    /// and kept through a round trip.
    /// </summary>
    public class DxfViewportTableRecordTests
    {
        [Fact]
        public void TheActiveOfANewDrawing_GivesTheSnapAndGridAutoCad2021ReadsFromIt()
        {
            DxfViewportTableRecord active = new DxfDatabase("AC1032").ViewportTable["*Active"];

            Assert.Equal("*Active", active.Name);
            Assert.False(active.SnapEnabled);
            Assert.False(active.GridEnabled);
            Assert.Equal(new GeoVector2(10.0, 10.0), active.SnapIncrements);
            Assert.Equal(new GeoVector2(10.0, 10.0), active.GridIncrements);
            Assert.Equal(GeoPoint2.Origin, active.SnapBase);
            Assert.Equal(0.0, active.SnapAngle);
            Assert.False(active.IsometricSnapEnabled);
            Assert.Equal(0, active.SnapPair);

            // No group 60 or 61: AutoCAD 2021 read such a drawing with GRIDDISPLAY 3 and GRIDMAJOR 5.
            Assert.Equal(3, active.GridDisplay);
            Assert.Equal(5, active.GridMajor);
        }

        [Fact]
        public void SnapAndGridSet_AreWrittenInTheGroupsAutoCad2021ReadsThemFrom_AndARoundTripKeepsThemBitForBit()
        {
            var db = new DxfDatabase("AC1032");
            DxfViewportTableRecord active = db.ViewportTable["*Active"];
            double angle = Math.PI / 12.0;

            active.SnapEnabled = true;
            active.GridEnabled = true;
            active.SnapIncrements = new GeoVector2(2.5, 5.0);
            active.GridIncrements = new GeoVector2(10.0, 20.0);
            active.SnapBase = new GeoPoint2(1.0, 2.0);
            active.SnapAngle = angle;
            active.IsometricSnapEnabled = true;
            active.SnapPair = 2;
            active.GridDisplay = 5;
            active.GridMajor = 7;
            string saved = db.ToText();

            // The groups AutoCAD 2021 opened as SNAPMODE 1, GRIDMODE 1, SNAPUNIT (2.5, 5), GRIDUNIT (10, 20), SNAPBASE (1, 2),
            // SNAPANG 15 degrees, SNAPSTYL 1, SNAPISOPAIR 2, GRIDDISPLAY 5 and GRIDMAJOR 7.
            Assert.Contains("\n 13\n1\n 23\n2\n 14\n2.5\n 24\n5\n 15\n10\n 25\n20\n", saved, StringComparison.Ordinal);
            string vport = saved.Substring(saved.IndexOf("*Active", StringComparison.Ordinal), 900);
            Assert.Matches(@"\n 50\n1[45]\.?[0-9]*\n 51\n", vport);
            Assert.Contains("\n 75\n1\n 76\n1\n 77\n1\n 78\n2\n 60\n5\n 61\n7\n", vport, StringComparison.Ordinal);

            DxfDatabase again = DxfDatabase.Parse(saved);
            DxfViewportTableRecord read = again.ViewportTable["*ACTIVE"];
            Assert.True(read.SnapEnabled);
            Assert.True(read.GridEnabled);
            Assert.Equal(new GeoVector2(2.5, 5.0), read.SnapIncrements);
            Assert.Equal(new GeoVector2(10.0, 20.0), read.GridIncrements);
            Assert.Equal(new GeoPoint2(1.0, 2.0), read.SnapBase);
            Assert.Equal(
                BitConverter.DoubleToInt64Bits(active.SnapAngle),
                BitConverter.DoubleToInt64Bits(read.SnapAngle)
            );
            Assert.True(read.IsometricSnapEnabled);
            Assert.Equal(2, read.SnapPair);
            Assert.Equal(5, read.GridDisplay);
            Assert.Equal(7, read.GridMajor);
            Assert.Equal(saved, again.ToText());
        }

        [Fact]
        public void TheValuesTheViewportHas_ChangeNoByte_AndValuesAutoCadRefuses_AreRefused()
        {
            var db = new DxfDatabase("AC1032");
            DxfViewportTableRecord active = db.ViewportTable["*Active"];
            string before = db.ToText();

            active.SnapEnabled = false;
            active.GridEnabled = false;
            active.SnapIncrements = new GeoVector2(10.0, 10.0);
            active.GridIncrements = new GeoVector2(10.0, 10.0);
            active.SnapBase = GeoPoint2.Origin;
            active.SnapAngle = 0.0;
            active.SnapPair = 0;
            active.GridDisplay = 3;
            active.GridMajor = 5;
            Assert.Equal(before, db.ToText());

            Assert.Throws<ArgumentException>(() => active.SnapIncrements = new GeoVector2(0.0, 10.0));
            Assert.Throws<ArgumentException>(() => active.GridIncrements = new GeoVector2(-1.0, 10.0));
            Assert.Throws<ArgumentException>(() => active.SnapBase = new GeoPoint2(double.NaN, 0.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => active.SnapPair = 3);
            Assert.Throws<ArgumentOutOfRangeException>(() => active.GridMajor = 0);
            Assert.Equal(before, db.ToText());

            // GRIDUNIT of nought takes the snap's spacing, and is kept as such.
            active.GridIncrements = new GeoVector2(0.0, 0.0);
            Assert.Equal(
                new GeoVector2(0.0, 0.0),
                DxfDatabase.Parse(db.ToText()).ViewportTable["*Active"].GridIncrements
            );
        }

        [Fact]
        public void GridDisplayAndGridMajor_CameWithR2007_AndAreRefusedInADrawingOfR2000()
        {
            DxfViewportTableRecord active = new DxfDatabase("AC1015").ViewportTable["*Active"];

            active.GridEnabled = true;
            Assert.True(active.GridEnabled);
            Assert.Throws<InvalidOperationException>(() => active.GridDisplay = 1);
            Assert.Throws<InvalidOperationException>(() => active.GridMajor = 10);
        }
    }
}
