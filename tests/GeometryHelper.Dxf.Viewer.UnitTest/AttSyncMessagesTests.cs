using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// What ATTSYNC says, as the string table of AutoCAD 2021's Battman.arx has it (BattmanRes.dll, read in place): "The
    /// selected entity is not a block.", "The selected block has no attributes.", "Block table contains no attributed block
    /// named PLAIN.", "Attributed blocks:" over the list, "This drawing contains no attributed blocks."; and the usage of its
    /// block name, "a block name or wildcard".
    /// </summary>
    public class AttSyncMessagesTests
    {
        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        /// <summary>
        /// The blocks GB1 and GB2, each of one attribute definition (tag T1 and T2), and PLAIN, a line; INSERTs of the three
        /// at (0, 0), (100, 0) and (200, 0) with no attributes; a line from (300, 0) to (400, 0).
        /// </summary>
        private static CadDocument Drawing(bool attributed = true)
        {
            var db = new DxfDatabase("AC1032");

            foreach (string name in new[] { "GB1", "GB2", "PLAIN" })
            {
                var block = new DxfBlockTableRecord { Name = name, Origin = new GeoPoint3(0, 0, 0) };
                db.BlockTable.Add(block);

                if (name == "PLAIN" || !attributed)
                {
                    block.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)));
                }
                else
                {
                    block.AppendEntity(
                        new DxfAttributeDefinition(new GeoPoint3(1, 1, 0), "v", "T" + name.Substring(2), "Prompt", null)
                    );
                }
            }

            double x = 0;

            foreach (string name in new[] { "GB1", "GB2", "PLAIN" })
            {
                db.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(x, 0, 0), db.BlockTable[name]));
                x += 100;
            }

            db.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(300, 0, 0), new GeoPoint3(400, 0, 0)));
            return CadDocument.From(db, "attsync.dxf");
        }

        [Fact]
        public void AttSyncBySelect_OfAnEntityThatIsNoBlock_SaysSo_AndAsksAgain()
        {
            CadDocument document = Drawing();

            CadCommandSession session = Start(document, "ATTSYNC", Enter, P(350, 0));

            Assert.Contains("The selected entity is not a block.", session.History);
            Assert.Equal("Select a block:", session.PromptText);
        }

        [Fact]
        public void AttSyncBySelect_OfABlockWithNoAttributes_SaysSo_BeforeAskingToSyncIt()
        {
            CadDocument document = Drawing();

            CadCommandSession session = Start(document, "ATTSYNC", Enter, P(205, 0));

            Assert.Contains("The selected block has no attributes.", session.History);
            Assert.DoesNotContain(session.History, l => l.StartsWith("ATTSYNC block", System.StringComparison.Ordinal));
            Assert.Equal("Select a block:", session.PromptText);
        }

        [Fact]
        public void AttSyncByName_OfABlockWithNoAttributesOrNone_SaysNoAttributedBlockHasThatName_AndAsksAgain()
        {
            CadDocument document = Drawing();

            CadCommandSession session = Start(document, "ATTSYNC", "N", "PLAIN");

            Assert.Contains("Block table contains no attributed block named PLAIN.", session.History);
            Assert.Equal("Enter name of block to sync or [?]:", session.PromptText);
            Run(session, "NOSUCH");
            Assert.Contains("Block table contains no attributed block named NOSUCH.", session.History);
            Assert.Equal("Enter name of block to sync or [?]:", session.PromptText);
        }

        [Fact]
        public void AttSyncByName_OfAWildcard_BringsEveryAttributedBlockItMatchesInLine_AsOneUndoStep()
        {
            CadDocument document = Drawing();
            string start = document.Database.ToText();

            Start(document, "ATTSYNC", "N", "GB*");

            DxfBlockReference[] inserts = document.Database.ModelSpace.OfType<DxfBlockReference>().ToArray();
            Assert.Equal(new[] { "T1" }, inserts[0].AttributeCollection.Select(a => a.Tag).ToArray());
            Assert.Equal(new[] { "T2" }, inserts[1].AttributeCollection.Select(a => a.Tag).ToArray());
            Assert.Empty(inserts[2].AttributeCollection);
            Assert.Equal("ATTSYNC", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal(start, document.Database.ToText());
        }

        [Fact]
        public void AttSyncList_HeadsTheNamesOfTheBlocksWithAttributes_AndOfNoneSaysThereAreNo()
        {
            CadDocument document = Drawing();

            CadCommandSession session = Start(document, "ATTSYNC", "?");

            Assert.Contains("Attributed blocks:", session.History);
            Assert.Contains("GB1", session.History);
            Assert.Contains("GB2", session.History);
            Assert.DoesNotContain("PLAIN", session.History);

            session = Start(Drawing(false), "ATTSYNC", "?");

            Assert.Contains("This drawing contains no attributed blocks.", session.History);
            Assert.DoesNotContain("Attributed blocks:", session.History);
        }
    }
}
