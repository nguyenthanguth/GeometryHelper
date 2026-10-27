using System;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// External references: attached and overlaid as a block flagged 4 or 12 that names its drawing, where each stands
    /// (XrefStatus), and detached with their INSERTs and the records they brought; ezdxf 1.4.5 read the probe's attached
    /// references back, flags and paths as written.
    /// </summary>
    public class DxfXrefTests
    {
        [Fact]
        public void AnAttachedReference_IsABlockFlaggedFour_NamingItsDrawing_WithNoEntities()
        {
            string text = DxfFileEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);

            DxfBlockTableRecord column = db.AttachXref("column.dwg", "COLUMN");

            Assert.True(column.IsFromExternalReference);
            Assert.False(column.IsFromOverlayReference);
            Assert.Equal("column.dwg", column.PathName);
            Assert.Equal(0, column.Count);
            Assert.Same(column, db.BlockTable["column"]);

            string saved = db.ToText();
            string block = Records(saved).Single(r => r.StartsWith("  0\nBLOCK\n", StringComparison.Ordinal) && r.Contains("\n  2\nCOLUMN\n"));
            Assert.Contains("  2\nCOLUMN\n 70\n4\n", block, StringComparison.Ordinal);
            Assert.EndsWith("  3\nCOLUMN\n  1\ncolumn.dwg\n", block, StringComparison.Ordinal);
            Assert.Single(Records(saved), r => r.StartsWith("  0\nBLOCK_RECORD\n", StringComparison.Ordinal) && r.Contains("\n  2\nCOLUMN\n"));

            DxfBlockTableRecord read = DxfDatabase.Parse(saved).BlockTable["COLUMN"];
            Assert.True(read.IsFromExternalReference);
            Assert.Equal("column.dwg", read.PathName);
            Assert.Equal(DxfXrefStatus.Unreferenced, read.XrefStatus);
        }

        [Fact]
        public void AnOverlaidReference_IsFlaggedTwelve_AndAnR12DrawingWritesItsPathToo()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R12);

            DxfBlockTableRecord grid = db.OverlayXref("C:\\plans\\grid.dwg", "GRID");

            Assert.True(grid.IsFromExternalReference);
            Assert.True(grid.IsFromOverlayReference);
            string block = Records(db.ToText()).Single(r => r.StartsWith("  0\nBLOCK\n", StringComparison.Ordinal) && r.Contains("\n  2\nGRID\n"));
            Assert.Contains("  2\nGRID\n 70\n12\n", block, StringComparison.Ordinal);
            Assert.EndsWith("  3\nGRID\n  1\nC:\\plans\\grid.dwg\n", block, StringComparison.Ordinal);
            Assert.DoesNotContain("BLOCK_RECORD", db.ToText(), StringComparison.Ordinal);
            Assert.Equal("C:\\plans\\grid.dwg", DxfDatabase.Parse(db.ToText()).BlockTable["GRID"].PathName);
        }

        [Fact]
        public void AReferenceOfANameTaken_OrOfNoPath_IsRefused_AndNothingIsWritten()
        {
            string text = DxfFileEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);

            Assert.Throws<ArgumentException>(() => db.AttachXref("bolt.dwg", "bolt"));
            Assert.Throws<ArgumentException>(() => db.AttachXref(" ", "COLUMN"));
            Assert.Throws<ArgumentException>(() => db.OverlayXref("a\nb.dwg", "COLUMN"));
            Assert.Throws<ArgumentException>(() => db.AttachXref("column.dwg", "CO|LUMN"));
            Assert.Throws<ArgumentNullException>(() => db.AttachXref(null, "COLUMN"));
            Assert.Throws<ArgumentNullException>(() => db.OverlayXref("column.dwg", null));
            Assert.Equal(text, db.ToText());
        }

        [Fact]
        public void TheStatusOfAReference_FollowsItsFlagsItsInsertsAndItsFile()
        {
            string folder = Path.Combine(Path.GetTempPath(), "gh-xref-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);

            try
            {
                string drawing = Path.Combine(folder, "plan.dxf");
                File.WriteAllText(drawing, DxfFileEzdxfDrawings.R2018);
                File.WriteAllText(Path.Combine(folder, "column.dxf"), DxfFileEzdxfDrawings.R2018);
                DxfDatabase db = DxfDatabase.Open(drawing);

                Assert.Equal(DxfXrefStatus.NotAnXref, db.BlockTable["BOLT"].XrefStatus);
                Assert.Equal(DxfXrefStatus.NotAnXref, db.ModelSpace.XrefStatus);

                DxfBlockTableRecord column = db.AttachXref("column.dxf", "COLUMN");
                DxfBlockTableRecord lost = db.AttachXref("lost.dxf", "LOST");
                Assert.Equal(DxfXrefStatus.Unreferenced, column.XrefStatus);

                db.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(0, 0, 0), column));
                db.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(0, 0, 0), lost));
                Assert.Equal(DxfXrefStatus.Unresolved, column.XrefStatus);
                Assert.Equal(DxfXrefStatus.FileNotFound, lost.XrefStatus);

                // AutoCAD saves an external reference it resolved with flag 32 as well: 70 of 36.
                string saved = db.ToText();
                string head = Records(saved).Single(r => r.StartsWith("  0\nBLOCK\n", StringComparison.Ordinal) && r.Contains("\n  2\nCOLUMN\n"));
                string resolvedText = saved.Replace(head, head.Replace("  2\nCOLUMN\n 70\n4\n", "  2\nCOLUMN\n 70\n36\n"));
                string resolvedPath = Path.Combine(folder, "resolved.dxf");
                File.WriteAllText(resolvedPath, resolvedText);
                DxfBlockTableRecord resolved = DxfDatabase.Open(resolvedPath).BlockTable["COLUMN"];
                Assert.True(resolved.IsResolved);
                Assert.Equal(DxfXrefStatus.Resolved, resolved.XrefStatus);

                File.Delete(Path.Combine(folder, "column.dxf"));
                Assert.Equal(DxfXrefStatus.FileNotFound, resolved.XrefStatus);
                Assert.Equal(DxfXrefStatus.FileNotFound, column.XrefStatus);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Fact]
        public void AReferenceDetached_TakesItsInsertsAndTheRecordsItBroughtWithIt()
        {
            // A layer and a linetype the reference COLUMN brought, flagged as AutoCAD flags them (16 dependent, 32 resolved).
            const string LayerHead = "  2\nLAYER\n  5\n1\n330\n0\n100\nAcDbSymbolTable\n 70\n4\n";
            const string Layer = "  0\nLAYER\n  5\nF101\n330\n1\n100\nAcDbSymbolTableRecord\n100\nAcDbLayerTableRecord\n  2\nCOLUMN|steel\n 70\n48\n 62\n1\n  6\nContinuous\n";
            const string LinetypeHead = "  2\nLTYPE\n  5\n2\n330\n0\n100\nAcDbSymbolTable\n 70\n3\n";
            const string Linetype = "  0\nLTYPE\n  5\nF102\n330\n2\n100\nAcDbSymbolTableRecord\n100\nAcDbLinetypeTableRecord\n  2\nCOLUMN|DASHED\n 70\n48\n  3\n\n 72\n65\n 73\n0\n 40\n0.0\n";
            string text = DxfFileEzdxfDrawings.R2018;
            Assert.Contains(LayerHead, text, StringComparison.Ordinal);
            Assert.Contains(LinetypeHead, text, StringComparison.Ordinal);
            text = text.Replace(LayerHead, LayerHead.Replace(" 70\n4\n", " 70\n5\n") + Layer).Replace(LinetypeHead, LinetypeHead.Replace(" 70\n3\n", " 70\n4\n") + Linetype);
            DxfDatabase db = DxfDatabase.Parse(text);
            Assert.True(db.LayerTable["COLUMN|steel"].IsDependent);

            DxfBlockTableRecord column = db.AttachXref("column.dwg", "COLUMN");
            var insert = new DxfBlockReference(new GeoPoint3(10, 0, 0), column);
            db.ModelSpace.AppendEntity(insert);
            var nested = new DxfBlockReference(new GeoPoint3(0, 0, 0), column);
            db.BlockTable["FRAME"].AppendEntity(nested);
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "COLUMNS" });

            db.DetachXref(column);

            Assert.True(insert.IsErased);
            Assert.True(nested.IsErased);
            Assert.False(db.BlockTable.Has("COLUMN"));
            Assert.False(db.LayerTable.Has("COLUMN|steel"));
            Assert.False(db.LinetypeTable.Has("COLUMN|DASHED"));
            Assert.True(db.LayerTable.Has("COLUMNS"));
            // The tables' counts: the layer table one up for COLUMNS and one down for COLUMN|steel, the linetype table one down.
            string saved = db.ToText();
            Assert.Contains(LayerHead.Replace(" 70\n4\n", " 70\n5\n"), saved, StringComparison.Ordinal);
            Assert.Contains(LinetypeHead, saved, StringComparison.Ordinal);
            DxfDatabase read = DxfDatabase.Parse(saved);
            Assert.False(read.BlockTable.Has("COLUMN"));
            Assert.False(read.LayerTable.Has("COLUMN|steel"));
            Assert.Equal(DxfDatabase.Parse(text).ModelSpace.Count, read.ModelSpace.Count);
            Assert.Equal(DxfDatabase.Parse(text).BlockTable["FRAME"].Count, read.BlockTable["FRAME"].Count);
        }

        [Fact]
        public void AReferenceAnInsertPlaces_StaysAReferenceWhereTheInsertIsCopied_ItsFlagsAndPathKept()
        {
            DxfDatabase source = DxfDatabase.Parse(new DxfWriter().ToString());
            DxfBlockTableRecord column = source.AttachXref("column.dwg", "COLUMN");
            DxfBlockTableRecord grid = source.OverlayXref("C:\\plans\\grid.dwg", "GRID");
            source.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(0, 0, 0), column));
            source.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(100, 0, 0), grid));

            DxfDatabase moved = DxfDatabase.Parse(new DxfWriter().ToString());
            moved.Insert(GeoTransform3.Translation(new GeoVector3(0, 500, 0)), source, true);
            DxfDatabase asBlock = DxfDatabase.Parse(DxfFileEzdxfDrawings.R12);
            asBlock.Insert("PLATE", source, true);
            DxfDatabase cloned = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            source.WblockCloneObjects(source.ModelSpace.ToList(), cloned.ModelSpace);

            foreach (DxfDatabase db in new[] { moved, asBlock, cloned })
            {
                DxfDatabase read = DxfDatabase.Parse(db.ToText());
                DxfBlockTableRecord copied = read.BlockTable["COLUMN"];
                Assert.True(copied.IsFromExternalReference);
                Assert.False(copied.IsFromOverlayReference);
                Assert.Equal("column.dwg", copied.PathName);
                Assert.Empty(copied);
                Assert.True(read.BlockTable["GRID"].IsFromOverlayReference);
                Assert.Equal("C:\\plans\\grid.dwg", read.BlockTable["GRID"].PathName);
            }

            Assert.Contains("  2\nCOLUMN\n 70\n4\n", moved.ToText(), StringComparison.Ordinal);
            Assert.Contains("  2\nGRID\n 70\n12\n", asBlock.ToText(), StringComparison.Ordinal);
        }

        [Fact]
        public void OnlyAReferenceOfTheDrawing_IsDetached()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfDatabase other = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfBlockTableRecord theirs = other.AttachXref("column.dwg", "COLUMN");

            Assert.Throws<ArgumentException>(() => db.DetachXref(db.BlockTable["BOLT"]));
            Assert.Throws<ArgumentException>(() => db.DetachXref(theirs));
            Assert.Throws<ArgumentNullException>(() => db.DetachXref(null));

            DxfBlockTableRecord column = db.AttachXref("column.dwg", "COLUMN");
            db.DetachXref(column);
            Assert.Throws<InvalidOperationException>(() => db.DetachXref(column));
        }
    }
}
