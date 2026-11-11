using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The objects of a paste are drawn once and moved to the cursor, not drawn anew at each move: the preview at one cursor
    /// and then at another is what the paste makes at each, for every kind of item a drawing gives.
    /// </summary>
    public class PastePreviewMovedTests
    {
        private static readonly GeoPoint2[] Square =
        {
            new GeoPoint2(0, 0),
            new GeoPoint2(30, 0),
            new GeoPoint2(30, 20),
            new GeoPoint2(0, 20),
        };

        /// <summary>A drawing of every kind of item: lines, a circle, an arc, a polyline, a text, a block, an xline, a ray and two hatches.</summary>
        private static CadDocument Mixed()
        {
            CadDocument document = Plan();
            DxfDatabase database = document.Database;
            database.ModelSpace.AppendEntity(
                new DxfXline
                {
                    BasePoint = new GeoPoint3(0, 0, 0),
                    UnitDir = new GeoVector3(Math.Sqrt(0.5), Math.Sqrt(0.5), 0),
                }
            );
            database.ModelSpace.AppendEntity(
                new DxfRay { BasePoint = new GeoPoint3(10, 0, 0), UnitDir = new GeoVector3(0, 1, 0) }
            );
            var lines = new DxfHatch();
            lines.SetHatchPattern(DxfHatchPatternType.UserDefined, "_USER");
            lines.PatternAngle = Math.PI / 4.0;
            lines.PatternSpace = 3.0;
            lines.AppendLoop(DxfHatchLoopTypes.External, Square, null);
            database.ModelSpace.AppendEntity(lines);
            var solid = new DxfHatch();
            solid.SetHatchPattern(DxfHatchPatternType.PreDefined, "SOLID");
            solid.AppendLoop(DxfHatchLoopTypes.External, Square, null);
            database.ModelSpace.AppendEntity(solid);
            return CadDocument.From(DxfDatabase.Parse(database.ToText()), "mixed.dxf");
        }

        [Fact]
        public void ThePastePreview_AtOneCursorThenAnother_IsWhatThePasteMakesAtEach()
        {
            CadDocument source = Mixed();
            var clipboard = new CadMemoryClipboard();
            string[] handles = source.ModelSpace.Block.Select(e => e.Handle).ToArray();
            CadCommandSession copy = Session(source);
            copy.Clipboard = clipboard;
            Run(copy, "COPYCLIP", handles, Enter);

            foreach (GeoPoint2 first in new[] { P(300, 200), P(-1250.5, 0.25) })
            {
                CadCommandSession session = Session(Blank());
                session.Clipboard = clipboard;
                Run(session, "PASTECLIP");
                CadPreview preview = session.Prompt.Preview;

                // Drawn first at another cursor, so that the items asked for are moved from those drawn once.
                Assert.NotEmpty(preview.Items(P(17, -3)));
                Assert.Contains(preview.Items(first), i => i is DisplaySegments2);
                Assert.Contains(preview.Items(first), i => i is DisplayFill2);
                Assert.Contains(preview.Items(first), i => i is DisplayRay2);
                Assert.Contains(preview.Items(first), i => i is DisplayArc2);
                Assert.Contains(preview.Items(first), i => i is DisplayText2);
                PreviewTests.PreviewMakes(session, first);
            }
        }
    }
}
