using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// REVCLOUD Modify against AutoCAD 2021's console run through standard input (16 November): a first point nearer the start
    /// than a little over an arc ends the command with nothing said and nothing changed.
    /// </summary>
    public class RevcloudModifyTinyPathTests
    {
        private static CadCommandSession Cloud(out CadDocument document, double length, bool varies = false)
        {
            document = Blank();
            CadCommandSession session = Session(document);
            Run(
                session,
                "REVCLOUDARCVARIANCE",
                varies ? "1" : "0",
                "REVCLOUDAPPROXARCLEN",
                length.ToString("R", CultureInfo.InvariantCulture),
                "REVCLOUD",
                P(0, 0),
                P(500, 300)
            );
            return session;
        }

        [Theory]
        [InlineData(20.0, 23.5, false)]
        [InlineData(20.0, 23.6, false)]
        [InlineData(20.0, 23.7, true)]
        [InlineData(20.0, 23.9, true)]
        [InlineData(100.0, 118.0, false)]
        [InlineData(100.0, 118.5, true)]
        [InlineData(50.0, 59.0, false)]
        [InlineData(50.0, 59.5, true)]
        [InlineData(7.0, 8.2, false)]
        [InlineData(7.0, 8.3, true)]
        public void AFirstPointNearerThanALittleOverAnArc_EndsModify_AsAutoCad2021(
            double length,
            double distance,
            bool goesOn
        )
        {
            // AutoCAD 2021, a rectangle 500 by 300 picked at its first vertex, the next point straight out from it: at arcs of 20
            // the command ended at 23.6 and went on at 23.7; at 100, ended at 118 and on at 118.5; at 50, 59 and 59.5; at 7,
            // 8.2 and 8.3.
            CadCommandSession session = Cloud(out CadDocument document, length);
            DxfPolyline cloud = Assert.Single(All<DxfPolyline>(document.ModelSpace));
            string handle = cloud.Handle;
            int vertices = cloud.NumberOfVertices;

            Run(session, "REVCLOUD", "M", P(0, 0), P(-distance, 0));

            Assert.Equal(goesOn, session.IsRunning);

            if (goesOn)
            {
                Assert.Equal("Specify next point or [Undo]:", session.PromptText);
            }

            DxfPolyline kept = Assert.Single(All<DxfPolyline>(document.ModelSpace));
            Assert.Equal(handle, kept.Handle);
            Assert.Equal(vertices, kept.NumberOfVertices);
        }

        [Fact]
        public void ATinyFirstPointAfterFirstPoint_EndsModifyToo()
        {
            CadCommandSession session = Cloud(out CadDocument document, 20.0);

            Run(session, "REVCLOUD", "M", P(0, 0), "F", P(0, 0), P(-5, 0));

            Assert.False(session.IsRunning);
            Assert.Single(All<DxfPolyline>(document.ModelSpace));
        }

        [Fact]
        public void ALaterPointCloseToThePointBefore_GoesOn()
        {
            // AutoCAD 2021: the first segment is the one that counts; the next ones of 2, 5, 15 and 21 went on at arcs of 20.
            CadCommandSession session = Cloud(out _, 20.0);

            Run(session, "REVCLOUD", "M", P(0, 0), P(-30, 0), P(-32, 0));

            Assert.True(session.IsRunning);
            Assert.Equal("Specify next point or [Undo]:", session.PromptText);
        }
    }
}
