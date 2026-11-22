using System;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>CETRANSPARENCY, the transparency new objects take, typed as a system variable the way AutoCAD 2021 takes it.</summary>
    public class CetransparencyTests
    {
        // The per cent an alpha stands for: 255 is solid, 0 per cent.
        private static int Percent(DxfTransparency transparency) =>
            (int)Math.Round((255 - transparency.Alpha) * 100.0 / 255.0);

        [Fact]
        public void CetransparencyTyped_TakesByLayerByBlockAndZeroToNinety_AndRefusesNinetyOne()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;

            Run(session, "CETRANSPARENCY");
            Assert.StartsWith("Enter new value for CETRANSPARENCY", session.PromptText, StringComparison.Ordinal);
            Assert.Equal(DxfTransparency.ByLayer, document.Database.Cetransparency);

            // 30 per cent, the alpha AutoCAD keeps for it; 0 is solid and 90 the most it allows.
            Run(session, "30");
            Assert.False(session.IsRunning);
            Assert.True(document.Database.Cetransparency.IsByAlpha);
            Assert.Equal(30, Percent(document.Database.Cetransparency));

            Run(session, "CETRANSPARENCY", "90");
            Assert.Equal(90, Percent(document.Database.Cetransparency));

            Run(session, "CETRANSPARENCY", "0");
            Assert.True(document.Database.Cetransparency.IsByAlpha);
            Assert.Equal((byte)255, document.Database.Cetransparency.Alpha);

            Run(session, "CETRANSPARENCY", "ByBlock");
            Assert.Equal(DxfTransparency.ByBlock, document.Database.Cetransparency);

            Run(session, "CETRANSPARENCY", "bylayer");
            Assert.Equal(DxfTransparency.ByLayer, document.Database.Cetransparency);

            // 91 is more than AutoCAD allows: refused, asked again, the value kept; so is a word that is not one of the two.
            Run(session, "CETRANSPARENCY", "40");
            Run(session, "CETRANSPARENCY", "91");
            Assert.True(session.IsRunning);
            Assert.StartsWith("Enter new value for CETRANSPARENCY", session.PromptText, StringComparison.Ordinal);
            Assert.Equal(40, Percent(document.Database.Cetransparency));

            Run(session, "clear");
            Assert.True(session.IsRunning);
            Assert.Equal(40, Percent(document.Database.Cetransparency));
            session.Escape();
            Assert.False(session.IsRunning);
        }
    }
}
