using GeometryHelper.Dwg;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// A DWG keeps angles in radians and DXF in degrees, so a drawing read and written again goes through both. The two
    /// conversions are one pair: the radians of a degree text are the ones AutoCAD 2021 makes of it, and the degrees read
    /// from AutoCAD's radians give the same radians back (236 ARC objects of AutoCAD's own drawings were one bit off).
    /// </summary>
    public class DwgAngleTests
    {
        /// <summary>
        /// Whole degrees as a DXF with them was read by AutoCAD 2021 (<c>DxfIn</c> into a side database, then the
        /// angle of the ARC or the rotation of the TEXT at 17 digits). The earlier <c>degrees * pi / 180</c> was one bit off
        /// on these.
        /// </summary>
        [Theory]
        [InlineData(294.0, 5.1312680008633285)]
        [InlineData(77.0, 1.3439035240356336)]
        [InlineData(337.0, 5.8817595792208905)]
        [InlineData(76.0, 1.3264502315156905)]
        [InlineData(219.0, 3.8222710618675815)]
        [InlineData(314.0, 5.480333851262195)]
        [InlineData(208.0, 3.630284844148205)]
        [InlineData(164.0, 2.8623399732707004)]
        public void DegreesAsAutoCadReadsThem_AreItsRadiansToTheBit(double degrees, double radians)
        {
            Assert.Equal(radians, DwgObjectWriter.Radians(degrees));
        }

        /// <summary>The angles of ARC objects in AutoCAD's own R2018 drawings, which came back from our reader one bit off.</summary>
        [Theory]
        [InlineData(3.9749053409422417)]
        [InlineData(5.863225927893963)]
        [InlineData(4.707152992628708)]
        [InlineData(2.321588450849652)]
        [InlineData(1.4288993132785055)]
        [InlineData(0.8200045518059916)]
        [InlineData(1.7126933403112858)]
        [InlineData(3.6315502078559017)]
        public void RadiansAutoCadWrote_ReadAsDegreesAndWrittenAgain_AreTheSameBits(double radians)
        {
            Assert.Equal(radians, DwgObjectWriter.Radians(DwgObjectReader.Degrees(radians)));
        }
    }
}
