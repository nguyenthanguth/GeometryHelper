using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// What a drawing of an older version cannot hold, left out of what is copied into it, as AutoCAD's SAVEAS to that version
    /// leaves it out: the overrides of a dimension style a dimension brings that the version has no variable of, and the groups
    /// of the dimension styles and layers copied by name that the version has not.
    /// </summary>
    public class DxfOlderVersionCopyTests
    {
        [Fact]
        public void ADimensionCopiedIntoR2000_LeavesOutTheOverridesR2007Brought_AndKeepsTheRest()
        {
            DxfDatabase source = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfDatabase target = DxfDatabase.Parse(DxfDimensionDrawings.R2000);
            DxfEntity copy = Dim(source, "4D").Clone();

            target.ModelSpace.AppendEntity(copy);

            // 4D overrides DIMASZ (41), DIMLWD (371), DIMBLK (342) and DIMTXSTY (340), which R2000 has, and DIMLTYPE (345),
            // which R2007 brought.
            string record = Records(target.ToText()).Single(r => HandleOf(r) == copy.Handle);
            Assert.DoesNotContain("1070\n345\n", record, StringComparison.Ordinal);
            Assert.Contains("1070\n41\n1040\n4.5\n", record, StringComparison.Ordinal);
            Assert.Contains("1070\n371\n1070\n18\n", record, StringComparison.Ordinal);
            Assert.Contains("1070\n342\n1005\n", record, StringComparison.Ordinal);
            var read = (DxfRotatedDimension)Dim(DxfDatabase.Parse(target.ToText()), copy.Handle);
            Assert.Equal(4.5, read.Dimasz);
            Assert.Equal(DxfLineWeight.LineWeight018, read.Dimlwd);
        }

        [Fact]
        public void ADimStyleAndALayerCopiedByNameIntoR2000_KeepOnlyTheGroupsR2000Has()
        {
            // The source's layer Defpoints renamed DIMS, the layer of 4D, so that the target, which has no DIMS and no GH_DIM, is
            // given both; each of R2018 with groups R2007 brought.
            DxfDatabase source = DxfDatabase.Parse(DxfDimensionDrawings.R2018.Replace("  2\nDefpoints\n", "  2\nDIMS\n"));
            DxfDatabase target = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            Assert.False(target.LayerTable.Has("DIMS"));
            Assert.False(target.DimStyleTable.Has("GH_DIM"));

            target.ModelSpace.AppendEntity(Dim(source, "4D").Clone());

            string saved = target.ToText();
            string style = Records(saved).Single(r => r.StartsWith("  0\nDIMSTYLE\n", StringComparison.Ordinal) && r.Contains("\n  2\nGH_DIM\n"));

            foreach (string code in new[] { " 49", " 50", " 69", "290", "345", "346", "347" })
            {
                Assert.DoesNotContain("\n" + code + "\n", style, StringComparison.Ordinal);
            }

            Assert.Single(style.Split('\n'), line => line == " 70");
            Assert.Contains("\n 41\n3.5\n", style, StringComparison.Ordinal);
            Assert.Contains("\n271\n3\n", style, StringComparison.Ordinal);
            string layer = Records(saved).Single(r => r.StartsWith("  0\nLAYER\n", StringComparison.Ordinal) && r.Contains("\n  2\nDIMS\n"));
            Assert.DoesNotContain("\n347\n", layer, StringComparison.Ordinal);
            Assert.Contains("\n370\n", layer, StringComparison.Ordinal);
            Assert.Equal(3.5, DxfDatabase.Parse(saved).DimStyleTable["GH_DIM"].Dimasz);
        }

        [Fact]
        public void ADimensionMadeInCode_LeavesOutAnOverrideItsDrawingsVersionHasNot()
        {
            // R2000 has no DIMFXLEN, which R2007 brought; R12 has no DIMDEC, which R2000 brought.
            DxfDatabase r2000 = DxfDatabase.Parse(DxfDimensionDrawings.R2000);
            var made = new DxfRotatedDimension(0.0, new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(5, 5, 0), string.Empty, null) { DimBlock = r2000.BlockTable["*D2"] };
            made.Dimfxlen = 3.0;
            made.Dimasz = 1.5;
            r2000.ModelSpace.AppendEntity(made);

            string record = Records(r2000.ToText()).Single(r => HandleOf(r) == made.Handle);
            Assert.DoesNotContain("1070\n49\n", record, StringComparison.Ordinal);
            Assert.Contains("1070\n41\n1040\n1.5\n", record, StringComparison.Ordinal);

            DxfDatabase r12 = DxfDatabase.Parse(DxfDimensionDrawings.R12);
            var old = new DxfRotatedDimension(0.0, new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(5, 5, 0), string.Empty, null) { DimBlock = r12.BlockTable["*D2"] };
            old.Dimdec = 2;
            old.Dimasz = 1.5;
            r12.ModelSpace.AppendEntity(old);

            string written = Records(r12.ToText()).Last(r => r.StartsWith("  0\nDIMENSION\n", StringComparison.Ordinal));
            Assert.DoesNotContain("1070\n271\n", written, StringComparison.Ordinal);
            Assert.Contains("1070\n41\n1040\n1.5\n", written, StringComparison.Ordinal);
        }

        [Fact]
        public void ADimensionCopiedIntoR12_NamesItsArrowBlockAsR12Does_AndLeavesOutWhatR12HasNot()
        {
            DxfDatabase source = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfDimension dimension = Dim(source, "61");
            dimension.Dimblk = source.BlockTable["_DOT"];
            dimension.Dimdec = 3;
            dimension.Dimtfill = 1;
            dimension.Dimasz = 2.0;
            DxfDatabase r12 = DxfDatabase.Parse(DxfDimensionDrawings.R12);
            Assert.True(r12.BlockTable.Has("_DOT"));
            DxfEntity copy = dimension.Clone();

            r12.ModelSpace.AppendEntity(copy);

            string record = Records(r12.ToText()).Last(r => r.StartsWith("  0\nDIMENSION\n", StringComparison.Ordinal));
            Assert.Contains("1070\n5\n1000\n_DOT\n", record, StringComparison.Ordinal);
            Assert.Contains("1070\n41\n1040\n2", record, StringComparison.Ordinal);
            Assert.DoesNotContain("1070\n271\n", record, StringComparison.Ordinal);
            Assert.DoesNotContain("1070\n69\n", record, StringComparison.Ordinal);
            Assert.DoesNotContain("1005\n", record, StringComparison.Ordinal);
            DxfDimension read = DxfDatabase.Parse(r12.ToText()).ModelSpace.OfType<DxfDimension>().Last();
            Assert.Equal("_DOT", read.Dimblk.Name);
            Assert.Equal(2.0, read.Dimasz);

            // A drawing of R12 with no block of the name has the override left out, the rest kept.
            DxfDatabase bare = DxfDatabase.Parse(DxfDimensionDrawings.R12.Replace("\n_DOT\n", "\n_DOX\n"));
            DxfEntity other = dimension.Clone();
            bare.ModelSpace.AppendEntity(other);
            string written = Records(bare.ToText()).Last(r => r.StartsWith("  0\nDIMENSION\n", StringComparison.Ordinal));
            Assert.DoesNotContain("1070\n5\n", written, StringComparison.Ordinal);
            Assert.Contains("1070\n41\n1040\n2", written, StringComparison.Ordinal);
        }

        private static DxfDimension Dim(DxfDatabase db, string handle) => db.ModelSpace.OfType<DxfDimension>().Single(d => d.Handle == handle);
    }
}
