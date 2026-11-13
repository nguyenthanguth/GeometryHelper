using System;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// Small follow-ups of the batch 4 commands: -RENAME VPort and the configurations the tiled viewports hold agreeing, and CAL's
    /// rad on the arc segment of a polyline.
    /// </summary>
    public class SmallFollowUpsTests
    {
        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        #region -RENAME VPort and the tiled viewports

        [Fact]
        public void RenameVPort_OfAConfigurationSavedWithTheTilesLoaded_ShowsInTheTiles_AndInTheSavedDxf()
        {
            CadDocument document = Blank();
            CadTiledViewports tiles = document.TiledViewports;
            Start(document, "-VPORTS", "2", "V", "-VPORTS", "S", "Pair");
            Assert.Equal(new[] { "Pair" }, tiles.Names);

            Start(document, "-RENAME", "VP", "Pair", "Twin");

            Assert.Equal(new[] { "Twin" }, tiles.Names);
            Assert.Equal(2, tiles.Saved("Twin").Count);
            Assert.Null(tiles.Saved("Pair"));
            string path = Path.Combine(Path.GetTempPath(), "gh-rename-vport-" + Guid.NewGuid().ToString("N") + ".dxf");

            try
            {
                document.SaveAs(path);
                DxfDatabase saved = DxfDatabase.Open(path);
                Assert.DoesNotContain(saved.ViewportTable, v => v.Name == "Pair");
                Assert.Equal(2, saved.ViewportTable.Count(v => v.Name == "Twin"));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void RenameVPort_AfterTheTilesRenamedAConfiguration_FindsItByTheNewName()
        {
            CadDocument document = Blank();
            CadTiledViewports tiles = document.TiledViewports;
            Start(document, "-VPORTS", "2", "V", "-VPORTS", "S", "Pair");
            Assert.Null(tiles.Rename("Pair", "Duo"));

            CadCommandSession session = Start(document, "-RENAME", "VP", "Pair");
            Assert.Contains("Cannot find viewport configuration \"Pair\".", session.History);
            Run(session, Esc);

            Start(document, "-RENAME", "VP", "Duo", "Trio");

            Assert.Equal(new[] { "Trio" }, tiles.Names);
        }

        [Fact]
        public void RenameVPort_ToTheNameOfAnotherConfiguration_IsRefusedAsTheTilesRefuseIt()
        {
            CadDocument document = Blank();
            CadTiledViewports tiles = document.TiledViewports;
            Start(document, "-VPORTS", "2", "V", "-VPORTS", "S", "Pair", "-VPORTS", "S", "Other");

            CadCommandSession session = Start(document, "-RENAME", "VP", "Pair", "other");

            Assert.Contains("Viewport configuration \"Other\" already exists.", session.History);
            Assert.Equal(new[] { "Other", "Pair" }, tiles.Names);
        }

        #endregion

        #region CAL rad of a polyline segment

        private static CadDocument PolylineWithAnArc()
        {
            // A line from (0, 0) to (100, 0), a half circle of radius 50 about (100, 50) to (100, 100), a line back to (0, 100).
            DxfDatabase database = TestDrawings.Drawing(
                string.Empty,
                string.Empty,
                TestDrawings.Polyline("0", "P1", false, 0, 0, 0, 100, 0, 1, 100, 100, 0, 0, 100, 0)
            );
            return CadDocument.From(database, "arc.dxf");
        }

        [Fact]
        public void Cal_Rad_OfAPolylineSegment_GivesTheRadiusOfTheArcPicked_AndRefusesAStraightOne()
        {
            CadCommandSession session = Start(PolylineWithAnArc(), "CAL", "rad");
            Assert.Equal(">> Select circle, arc or polyline segment for RAD function:", session.PromptText);

            Run(session, P(50, 0));
            Assert.Equal("Improper type of object picked.", session.History.Last());

            Run(session, P(150, 50));
            Assert.Equal("50", session.History.Last());
        }

        #endregion
    }
}
