using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Export;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Export.DxfModel;

namespace GeometryHelper.UnitTest.Export
{
    /// <summary>
    /// The details of a layer: its description, its default in new viewports, whether it is used, renaming it with every
    /// reference to it and erasing it when nothing is on it; ezdxf 1.4.5 read the renamed, described and erased layers of the
    /// probe's drawings back, its recover and audit finding nothing.
    /// </summary>
    public class DxfLayerTableRecordTests
    {
        private static readonly string[] Drawings = { DxfFileEzdxfDrawings.R12, DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018 };

        [Fact]
        public void ALayersDescription_IsTheSecondTextOfAcAecLayerStandard_AsAutoCADKeepsIt()
        {
            string text = DxfFileEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfLayerTableRecord steel = db.LayerTable["steel"];

            Assert.Equal(string.Empty, steel.Description);
            steel.Description = "Columns of grid A";

            string saved = db.ToText();
            Assert.EndsWith("1001\nAcAecLayerStandard\n1000\n\n1000\nColumns of grid A\n", Records(saved).Single(r => HandleOf(r) == steel.Handle), StringComparison.Ordinal);
            DxfDatabase again = DxfDatabase.Parse(saved);
            Assert.Equal("Columns of grid A", again.LayerTable["steel"].Description);
            again.LayerTable["steel"].Description = string.Empty;
            Assert.Null(again.LayerTable["steel"].GetXDataForApplication("AcAecLayerStandard"));
            Assert.Throws<ArgumentNullException>(() => steel.Description = null);
            Assert.Throws<ArgumentException>(() => steel.Description = "two\nlines");

            var made = new DxfLayerTableRecord { Name = "GH_Grid", Description = "grid lines", ViewportVisibilityDefault = true };
            DxfDatabase r12 = DxfDatabase.Parse(DxfFileEzdxfDrawings.R12);
            r12.LayerTable.Add(made);
            DxfLayerTableRecord read = DxfDatabase.Parse(r12.ToText()).LayerTable["GH_Grid"];
            Assert.Equal("grid lines", read.Description);
            Assert.True(read.ViewportVisibilityDefault);
        }

        [Fact]
        public void ALayerFrozenInNewViewports_IsFlag2_AndEverySetterGivenItsValueChangesNothing()
        {
            string text = DxfFileEzdxfDrawings.R2000;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfLayerTableRecord steel = db.LayerTable["steel"];

            foreach (string drawing in Drawings)
            {
                DxfDatabase each = DxfDatabase.Parse(drawing);

                foreach (DxfLayerTableRecord layer in each.LayerTable)
                {
                    layer.ViewportVisibilityDefault = layer.ViewportVisibilityDefault;
                    layer.Description = layer.Description;
                    layer.Transparency = layer.Transparency;
                    layer.Name = layer.Name;
                }

                Assert.Equal(drawing, each.ToText());
            }

            Assert.False(steel.ViewportVisibilityDefault);
            steel.ViewportVisibilityDefault = true;
            string saved = db.ToText();
            OnlyTheseChanged(text, saved, steel.Handle);
            Assert.True(DxfDatabase.Parse(saved).LayerTable["steel"].ViewportVisibilityDefault);
            Assert.Contains(" 70\n2\n", Records(saved).Single(r => HandleOf(r) == steel.Handle), StringComparison.Ordinal);
        }

        [Fact]
        public void ALayer_IsUsed_WhereAnEntityIsOnIt_OrItIsZeroDefpointsOrTheCurrentOne()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var spare = new DxfLayerTableRecord { Name = "GH_Spare" };
            db.LayerTable.Add(spare);
            db.LayerTable.GenerateUsageData();

            Assert.True(db.LayerTable["0"].IsUsed);
            Assert.True(db.LayerTable["Defpoints"].IsUsed);
            Assert.True(db.LayerTable["steel"].IsUsed);
            Assert.False(spare.IsUsed);
            Assert.False(new DxfLayerTableRecord { Name = "x" }.IsUsed);

            // A vertex of a polyline is drawn as part of it: on a layer, it uses it.
            DxfEntity vertex = ((System.Collections.IEnumerable)ByHandle<DxfEntity>(db, "44")).Cast<DxfEntity>().First();
            vertex.Layer = "GH_Spare";
            Assert.True(spare.IsUsed);
            vertex.Layer = "0";
            Assert.False(spare.IsUsed);
            db.Clayer = "GH_Spare";
            Assert.True(spare.IsUsed);
        }

        [Fact]
        public void ALayerRenamed_TakesEveryReferenceToItAlong_InModelSpaceBlocksVerticesExtendedDataAndTheHeader()
        {
            string text = DxfFileEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfLine line = ByHandle<DxfLine>(db, "3E");
            DxfLine inBolt = db.BlockTable["BOLT"].OfType<DxfLine>().Single();
            inBolt.Layer = "steel";
            line.XData = new[] { new DxfTypedValue(1001, "GHPROBE"), new DxfTypedValue(1003, "STEEL") };
            db.Clayer = "steel";
            string before = db.ToText();
            DxfLayerTableRecord steel = db.LayerTable["steel"];

            steel.Name = "Steel_S355";

            Assert.Equal("Steel_S355", line.Layer);
            Assert.Equal("Steel_S355", inBolt.Layer);
            Assert.Equal("Steel_S355", ByHandle<DxfBlockReference>(db, "65").Layer);
            Assert.Equal("Steel_S355", db.Clayer);
            Assert.Equal("Steel_S355", line.XData[1].Value);
            Assert.Same(steel, db.LayerTable["steel_s355"]);
            Assert.False(db.LayerTable.Has("steel"));

            string saved = db.ToText();
            // The INSERT 65 on steel with its ATTRIB 67 and SEQEND 66, the LINE 3E and the LINE 36 of BOLT.
            OnlyTheseChanged(before, saved, steel.Handle, "3E", "36", "65", "67", "66");
            DxfDocument read = DxfReader.Parse(saved, new DxfReadOptions { Tolerance = Tolerance.Default });
            Assert.DoesNotContain(read.Shapes, s => s.Layer == "steel");
            Assert.Contains(read.Shapes, s => s.Layer == "Steel_S355");

            // A layer of the name added after, letters of either case the same, is a new one: the old name is free.
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "STEEL" });
            Assert.Equal(new[] { "0", "Defpoints", "Steel_S355", "Wand€Außen", "STEEL" }, DxfDatabase.Parse(db.ToText()).LayerTable.Select(l => l.Name));
        }

        [Fact]
        public void ALayerIsNotRenamed_ToAnotherLayersName_NorIsLayer0OrDefpoints()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            string text = db.ToText();

            Assert.Throws<ArgumentException>(() => db.LayerTable["steel"].Name = "DEFPOINTS");
            Assert.Throws<ArgumentException>(() => db.LayerTable["steel"].Name = "a:b");
            Assert.Throws<InvalidOperationException>(() => db.LayerTable["0"].Name = "zero");
            Assert.Throws<InvalidOperationException>(() => db.LayerTable["Defpoints"].Name = "points");
            Assert.Equal(text, db.ToText());

            // Letters of another case, as AutoCAD lets a layer be renamed.
            db.LayerTable["steel"].Name = "STEEL";
            Assert.Equal("STEEL", ByHandle<DxfLine>(DxfDatabase.Parse(db.ToText()), "3E").Layer);
        }

        [Fact]
        public void ALayerErased_IsItsRecordTakenOut_TheTableCountedAgain_WhereNothingIsOnIt()
        {
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                var spare = new DxfLayerTableRecord { Name = "GH_Spare" };
                db.LayerTable.Add(spare);
                string added = db.ToText();
                spare.Erase();

                Assert.True(spare.IsErased);
                Assert.False(db.LayerTable.Has("GH_Spare"));
                Assert.Throws<InvalidOperationException>(() => spare.Erase());
                Assert.Throws<InvalidOperationException>(() => spare.ColorIndex = 3);
                Assert.NotEqual(added, db.ToText());

                // Every record as it was, the layer table's count among them; the header's handle seed aside.
                OnlyTheseChanged(text, db.ToText());

                Assert.Throws<InvalidOperationException>(() => db.LayerTable["0"].Erase());
                Assert.Throws<InvalidOperationException>(() => db.LayerTable["Defpoints"].Erase());
                Assert.Throws<InvalidOperationException>(() => db.LayerTable["steel"].Erase());
                Assert.Throws<InvalidOperationException>(() => new DxfLayerTableRecord { Name = "x" }.Erase());
            }
        }

        [Fact]
        public void ALayerOfALineAppendedInCode_IsUsed_RenamedAlongWithIt_AndNotErased()
        {
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                var fresh = new DxfLayerTableRecord { Name = "GH_Fresh" };
                db.LayerTable.Add(fresh);
                Assert.False(fresh.IsUsed);

                // The line is written after the last entity of the section, before its ENDSEC.
                var line = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 1, 0)) { Layer = "GH_Fresh" };
                db.ModelSpace.AppendEntity(line);

                Assert.True(fresh.IsUsed);
                Assert.Throws<InvalidOperationException>(() => fresh.Erase());
                fresh.Name = "GH_Renamed";
                Assert.Equal("GH_Renamed", line.Layer);
                Assert.Equal("GH_Renamed", ByHandle<DxfLine>(DxfDatabase.Parse(db.ToText()), line.Handle).Layer);
                Assert.True(db.LayerTable.Has("GH_Renamed"));
            }
        }
    }
}
