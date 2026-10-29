using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// What was left of the copies of blocks between drawings once a copy that would insert itself was refused: a drawing
    /// inserted and refused part way, the names AutoCAD gives blocks itself taken by one entity of a set where an INSERT
    /// copied by another gives them, a dimension of the drawing copied into drawing by the copy of another's block, and
    /// INSERTs of blocks their drawing has not copied with no word. Each drawing is a DXF of R12 built here as text, read as
    /// a drawing from a file is.
    /// </summary>
    public class DxfD10Tests
    {
        private const string Head = "  0\nSECTION\n  2\nHEADER\n  9\n$ACADVER\n  1\nAC1009\n  0\nENDSEC\n  0\nSECTION\n  2\nBLOCKS\n";
        private const string Line = "  0\nLINE\n  8\n0\n 10\n0\n 20\n0\n 30\n0\n 11\n1\n 21\n0\n 31\n0\n";

        private static string Insert(string name) => "  0\nINSERT\n  8\n0\n  2\n" + name + "\n 10\n0\n 20\n0\n 30\n0\n";

        // A block of a name AutoCAD gives itself, *U1, flagged anonymous (70 of 1).
        private static string Block(string name, string entities) => "  0\nBLOCK\n  8\n0\n  2\n" + name + "\n 70\n" + (name.StartsWith("*", StringComparison.Ordinal) ? "1" : "0") + "\n 10\n0\n 20\n0\n 30\n0\n" + entities + "  0\nENDBLK\n  8\n0\n";

        private static string Drawing(string blocks, string entities) => Head + blocks + "  0\nENDSEC\n  0\nSECTION\n  2\nENTITIES\n" + entities + "  0\nENDSEC\n  0\nEOF\n";

        private static IEnumerable<string> RoundWarnings(DxfDatabase db)
            => DxfReader.Parse(db.ToText(), new DxfReadOptions { Tolerance = Tolerance.Default }).Warnings.Where(w => w.Contains("without end"));

        /// <summary>The messages logged while an action runs, every level.</summary>
        private static List<string> Logged(Action action)
        {
            var written = new List<string>();
            Action<GeometryHelperLogLevel, string, Exception> before = GeometryHelperLog.Writer;
            bool enabled = GeometryHelperLog.Enable;

            try
            {
                GeometryHelperLog.Enable = true;
                GeometryHelperLog.Writer = (level, message, exception) => written.Add(level + " " + message);
                action();
            }
            finally
            {
                GeometryHelperLog.Writer = before;
                GeometryHelperLog.Enable = enabled;
            }

            return written;
        }

        [Fact]
        public void ADrawingInsertedThatWouldComeToInsertItselfThroughAnInsertOfABlockThisDrawingHasNot_IsRefusedBeforeAnythingIsWritten()
        {
            // This drawing's P holds an INSERT of M, which it has not; the other's A is a line and its M an INSERT of P. The
            // merge was refused at M, leaving A and an empty M; it is refused before A now.
            DxfDatabase db = DxfDatabase.Parse(Drawing(Block("P", Line + Insert("M")), string.Empty));
            DxfDatabase other = DxfDatabase.Parse(Drawing(Block("A", Line) + Block("M", Insert("P")), Insert("A")));
            string before = db.ToText();

            ArgumentException refused = Assert.Throws<ArgumentException>(() => db.Insert(GeoTransform3.Identity, other, true));

            Assert.Contains("insert itself", refused.Message, StringComparison.Ordinal);
            Assert.Equal(before, db.ToText());
            Assert.False(db.BlockTable.Has("A"));
            Assert.False(db.BlockTable.Has("M"));

            // Moved, the same.
            Assert.Throws<ArgumentException>(() => db.Insert(GeoTransform3.Translation(new GeoVector3(100, 0, 0)), other, true));
            Assert.Equal(before, db.ToText());
        }

        [Fact]
        public void ADrawingMadeABlockThatAnInsertOfThisDrawingWouldPlaceRoundToItself_IsRefusedBeforeAnythingIsWritten()
        {
            // This drawing's P holds an INSERT of NEWB, which it has not; the other's model space holds an INSERT of P, and
            // its A a line. The block NEWB would place this P, and P that NEWB: the new block was left written, empty, and A
            // merged; nothing is written now.
            DxfDatabase db = DxfDatabase.Parse(Drawing(Block("P", Line + Insert("NEWB")), string.Empty));
            DxfDatabase other = DxfDatabase.Parse(Drawing(Block("P", Line) + Block("A", Line), Insert("P")));
            string before = db.ToText();

            ArgumentException refused = Assert.Throws<ArgumentException>(() => db.Insert("NEWB", other, true));

            Assert.Contains("insert itself", refused.Message, StringComparison.Ordinal);
            Assert.Equal(before, db.ToText());
            Assert.False(db.BlockTable.Has("NEWB"));
            Assert.False(db.BlockTable.Has("A"));

            // Under a name no INSERT of this drawing gives, the same drawing is made a block, A merged with it.
            DxfBlockTableRecord made = db.Insert("PLATE", other, true);
            Assert.Equal("P", made.OfType<DxfBlockReference>().Single().Name);
            Assert.True(db.BlockTable.Has("A"));
            Assert.Empty(RoundWarnings(db));
        }

        [Fact]
        public void AnAnonymousBlockOneInsertOfASetBrings_IsNotNamedAsAnInsertInABlockAnotherBringsNames()
        {
            // This drawing has *U1 and *U2, so that the first name of the letter it has not is *U3, and P holding an INSERT
            // of Q, which it has not. The other's *U1 inserts P, and its Q holds an INSERT of *U3, a block it has not. Copied
            // together, the INSERT of *U1 brought its block as *U3, which Q's INSERT then placed: Q, *U3, P, Q without end,
            // found only once *U3 was written, and the set refused half written. *U1 comes as *U4 now, and Q's INSERT of
            // *U3 places nothing, as it placed nothing where it came from.
            DxfDatabase db = DxfDatabase.Parse(Drawing(Block("*U1", Line) + Block("*U2", Line) + Block("P", Line + Insert("Q")), string.Empty));
            DxfDatabase source = DxfDatabase.Parse(Drawing(Block("P", Line) + Block("*U1", Insert("P")) + Block("Q", Line + Insert("*U3")), Insert("*U1") + Insert("Q")));

            IReadOnlyList<DxfEntity> copies = source.WblockCloneObjects(source.ModelSpace.ToList(), db.ModelSpace);

            Assert.Equal(new[] { "*U4", "Q" }, copies.Cast<DxfBlockReference>().Select(r => r.Name).ToArray());
            DxfDatabase read = DxfDatabase.Parse(db.ToText());
            Assert.False(read.BlockTable.Has("*U3"));
            Assert.Equal("*U3", read.BlockTable["Q"].OfType<DxfBlockReference>().Single().Name);
            Assert.Equal("P", read.BlockTable["*U4"].OfType<DxfBlockReference>().Single().Name);
            Assert.Empty(RoundWarnings(db));
        }

        [Fact]
        public void AnAnonymousBlockADrawingInsertedBrings_IsNotNamedAsAnInsertInAnotherBlockItBringsNames()
        {
            // This drawing has *U1 and *U2. The other's model space holds INSERTs of its *U1, a line, and of its *U5, which
            // holds an INSERT of *U3, a block it has not. Inserted whole, *U1 came as *U3, which the copy of *U5's INSERT then
            // placed, where it placed nothing; *U1 takes another name now, and that INSERT places nothing still.
            DxfDatabase db = DxfDatabase.Parse(Drawing(Block("*U1", Line) + Block("*U2", Line), string.Empty));
            DxfDatabase source = DxfDatabase.Parse(Drawing(Block("*U1", Line) + Block("*U5", Line + Insert("*U3")), Insert("*U1") + Insert("*U5")));

            db.Insert(GeoTransform3.Identity, source, true);

            DxfDatabase read = DxfDatabase.Parse(db.ToText());
            Assert.False(read.BlockTable.Has("*U3"));
            List<DxfBlockReference> placed = read.ModelSpace.OfType<DxfBlockReference>().ToList();
            Assert.Equal(2, placed.Count);
            Assert.Equal("*U3", read.BlockTable[placed[1].Name].OfType<DxfBlockReference>().Single().Name);
            Assert.Empty(RoundWarnings(db));
        }

        [Fact]
        public void ADimensionCopiedIntoADrawingWhoseDimensionNamesABlockItHasNot_TakesAnotherNameForItsBlock()
        {
            // The drawing of R12 of DxfDimensionDrawings without its block *D1, as a file may come: its first DIMENSION names
            // *D1, which it has not, and *D2 to *D9 draw the others. The same drawing's first dimension, drawn by its *D1, copied
            // in brought its block as *D1, the first name of its letter the drawing had no block of, and this drawing's
            // DIMENSION then drew by it; it comes as *D10.
            string r12 = DxfDimensionDrawings.R12;
            int block = r12.IndexOf("  0\nBLOCK\n  5\n56\n  8\n0\n  2\n*D1\n", StringComparison.Ordinal);
            int end = r12.IndexOf("  0\n", r12.IndexOf("  0\nENDBLK\n", block, StringComparison.Ordinal) + 1, StringComparison.Ordinal);
            string without = r12.Remove(block, end - block);
            DxfDatabase db = DxfDatabase.Parse(without);
            Assert.False(db.BlockTable.Has("*D1"));
            Assert.Null(db.ModelSpace.OfType<DxfDimension>().First().DimBlock);
            DxfDatabase source = DxfDatabase.Parse(r12);
            DxfDimension dimension = source.ModelSpace.OfType<DxfDimension>().First();
            Assert.Equal("*D1", dimension.DimBlock.Name);

            var copy = (DxfDimension)source.WblockCloneObjects(new[] { dimension }, db.ModelSpace).Single();

            Assert.Equal("*D10", copy.DimBlock.Name);
            Assert.Equal(dimension.DimBlock.Count, copy.DimBlock.Count);
            DxfDatabase read = DxfDatabase.Parse(db.ToText());
            Assert.False(read.BlockTable.Has("*D1"));
            Assert.Null(read.ModelSpace.OfType<DxfDimension>().First().DimBlock);

            // Appended alone, the same.
            DxfDatabase alone = DxfDatabase.Parse(without);
            DxfEntity appended = dimension.Clone();
            alone.ModelSpace.AppendEntity(appended);
            Assert.Equal("*D10", ((DxfDimension)appended).DimBlock.Name);
        }

        [Fact]
        public void InsertsOfBlocksTheirDrawingHasNot_InTheBlocksACopyBrings_AreCopiedAsTheyAre_AndSaidInOneLine()
        {
            // The other's M holds INSERTs of Z and of P, and its N one of Z and one of K: Z and K are blocks it has not, and
            // so has this drawing not; P this drawing has. Both INSERTs copied together bring M and N with the INSERTs as
            // they were, and one Debug line says so for the whole set: three INSERTs, of Z and K, none of which places a block.
            DxfDatabase db = DxfDatabase.Parse(Drawing(Block("P", Line), string.Empty));
            DxfDatabase source = DxfDatabase.Parse(Drawing(Block("P", Line) + Block("M", Line + Insert("Z") + Insert("P")) + Block("N", Insert("Z") + Insert("K")), Insert("M") + Insert("N")));

            List<string> logged = Logged(() => source.WblockCloneObjects(source.ModelSpace.ToList(), db.ModelSpace));

            DxfDatabase read = DxfDatabase.Parse(db.ToText());
            Assert.Equal(new[] { "Z", "P" }, read.BlockTable["M"].OfType<DxfBlockReference>().Select(r => r.Name).ToArray());
            Assert.Equal(new[] { "Z", "K" }, read.BlockTable["N"].OfType<DxfBlockReference>().Select(r => r.Name).ToArray());
            string said = Assert.Single(logged, m => m.Contains("block their drawing has not"));
            Assert.StartsWith("Debug ", said, StringComparison.Ordinal);
            Assert.Contains("3 INSERTs", said, StringComparison.Ordinal);
            Assert.Contains("K, Z", said, StringComparison.Ordinal);

            // An INSERT copied alone says it once too; a copy that brings no such INSERT says nothing of it.
            DxfDatabase alone = DxfDatabase.Parse(Drawing(Block("P", Line), string.Empty));
            Assert.Single(Logged(() => alone.ModelSpace.AppendEntity(source.ModelSpace.First().Clone())), m => m.Contains("block their drawing has not"));
            DxfDatabase clean = DxfDatabase.Parse(Drawing(Block("A", Line), Insert("A")));
            Assert.DoesNotContain(Logged(() => clean.WblockCloneObjects(clean.ModelSpace.ToList(), alone.ModelSpace)), m => m.Contains("block their drawing has not"));
        }

        /// <summary>
        /// The drawing of R2018 of <see cref="DxfDimensionDrawings"/>, its table F6 given an extension dictionary (A04) holding an
        /// XRECORD (A05), as AutoCAD's ACAD_XREC_ROUNDTRIP holds a table's TABLECONTENT, and its cells (1, 0) to (2, 1) linked to
        /// the DATALINK Parts (A01) of ACAD_DATALINK (A00) as the library reads a link (the top left cell's 92 made odd, then
        /// 340, 93, 94 and 96). The link's own groups are in the order AutoCAD 2021 writes them: its status, then the count of
        /// the objects it is tied to (94), each a 330, here the table, its XRECORD and the dictionary EZDXF_META (2D), which is
        /// none of the table's; then its 360 and its custom data, <c>305 CUSTOMDATA</c>, <c>1 DATAMAP_BEGIN</c>, <c>90 0</c>,
        /// <c>309 DATAMAP_END</c>.
        /// </summary>
        private static string TiedToTheTable()
        {
            const string Mark = " 92\n0\n301\nCELL_VALUE\n 93\n6\n 90\n4\n  1\nMark\n";
            return DxfDimensionDrawings.R2018
                .Replace("$HANDSEED\n  5\n10A\n", "$HANDSEED\n  5\nA06\n")
                .Replace("  0\nACAD_TABLE\n  5\nF6\n330\n17\n", "  0\nACAD_TABLE\n  5\nF6\n102\n{ACAD_XDICTIONARY\n360\nA04\n102\n}\n330\n17\n")
                .Replace(Mark, Mark.Replace(" 92\n0\n", " 92\n1\n340\nA01\n 93\n2\n 94\n2\n 96\n0\n"))
                .Replace("  3\nEZDXF_META\n350\n2D\n", "  3\nACAD_DATALINK\n350\nA00\n  3\nEZDXF_META\n350\n2D\n")
                .Replace("  0\nENDSEC\n  0\nEOF\n", "  0\nDICTIONARY\n  5\nA00\n330\nA\n100\nAcDbDictionary\n281\n1\n  3\nParts\n350\nA01\n"
                    + "  0\nDATALINK\n  5\nA01\n102\n{ACAD_REACTORS\n330\nA00\n102\n}\n330\nA00\n100\nAcDbDataLink\n  1\nAcExcel\n300\nBill of parts\n301\n\n302\nparts.csv\n"
                    + " 90\n2\n 91\n0\n 92\n0\n170\n1900\n171\n1\n172\n0\n173\n0\n174\n0\n175\n0\n176\n0\n177\n0\n 93\n654\n304\n\n"
                    + " 94\n3\n330\nF6\n330\nA05\n330\n2D\n360\n0\n305\nCUSTOMDATA\n  1\nDATAMAP_BEGIN\n 90\n0\n309\nDATAMAP_END\n"
                    + "  0\nDICTIONARY\n  5\nA04\n330\nF6\n100\nAcDbDictionary\n280\n1\n281\n1\n  3\nACAD_XREC_ROUNDTRIP\n360\nA05\n"
                    + "  0\nXRECORD\n  5\nA05\n102\n{ACAD_REACTORS\n330\nA04\n102\n}\n330\nA04\n100\nAcDbXrecord\n280\n1\n102\nACAD_ROUNDTRIP_2008_TABLE_ENTITY\n 90\n0\n"
                    + "  0\nENDSEC\n  0\nEOF\n");
        }

        /// <summary>The groups of the DATALINK of a drawing saved, from its count of the objects it is tied to (94) to its end.</summary>
        private static string TiesOf(string saved)
        {
            int link = saved.IndexOf("\n  0\nDATALINK\n", StringComparison.Ordinal);
            int count = saved.IndexOf("\n 94\n", link, StringComparison.Ordinal);
            int end = saved.IndexOf("\n  0\n", count, StringComparison.Ordinal);
            return saved.Substring(count + 1, end - count);
        }

        [Fact]
        public void ADataLinkTakenAwayFromATable_NoLongerListsTheTableOrWhatItOwns_AsAutoCADsRemoveDataLinkLeavesIt()
        {
            // AutoCAD 2021's RemoveDataLink on its own link of the table F6 alone took its count from 2 to nought, the 330s of
            // F6 and of the table's TABLECONTENT out, the 360 and the custom data as they were (77-autocad-named-api-*.dxf of
            // the AutoCAD checks). Here the table and its XRECORD go; the dictionary EZDXF_META, none of the table's, stays.
            DxfDatabase db = DxfDatabase.Parse(TiedToTheTable());
            DxfTable table = db.ModelSpace.OfType<DxfTable>().First();
            Assert.Equal("Parts", table.GetDataLink(1, 0).Name);
            Assert.Equal(" 94\n3\n330\nF6\n330\nA05\n330\n2D\n360\n0\n305\nCUSTOMDATA\n  1\nDATAMAP_BEGIN\n 90\n0\n309\nDATAMAP_END\n", TiesOf(db.ToText()));

            table.RemoveDataLink();

            string saved = db.ToText();
            Assert.Null(table.GetDataLink(1, 0));
            Assert.Equal(" 94\n1\n330\n2D\n360\n0\n305\nCUSTOMDATA\n  1\nDATAMAP_BEGIN\n 90\n0\n309\nDATAMAP_END\n", TiesOf(saved));
            Assert.Equal("Parts", db.DataLinkDictionary.Cast<DxfDBDictionaryEntry>().Single().Key);

            // Taken away by a cell of its range, the same.
            DxfDatabase again = DxfDatabase.Parse(TiedToTheTable());
            again.ModelSpace.OfType<DxfTable>().First().RemoveDataLink(2, 1);
            Assert.StartsWith(" 94\n1\n330\n2D\n360\n", TiesOf(again.ToText()), StringComparison.Ordinal);
        }
    }
}
