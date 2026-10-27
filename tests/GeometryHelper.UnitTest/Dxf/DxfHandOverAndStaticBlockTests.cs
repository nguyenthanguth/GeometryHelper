using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// An entity made in code put in another's place by HandOverTo, the other's handle, owner and reactors kept, its extended
    /// data and extension dictionary kept or not; and the INSERT of a dynamic block made to place a static copy of what it
    /// draws by ConvertToStaticBlock; ezdxf 1.4.5 read the probe's drawings back, its recover and audit finding nothing.
    /// </summary>
    public class DxfHandOverAndStaticBlockTests
    {
        [Fact]
        public void AnEntityHandedOver_TakesTheOthersHandleOwnerAndReactors_InItsPlace()
        {
            string text = DxfFileEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfLine line = ByHandle<DxfLine>(db, "3E");
            var group = (DxfGroup)db.GroupDictionary.Cast<DxfDBDictionaryEntry>().Single().Value;
            int place = db.ModelSpace.ToList().IndexOf(line);
            var circle = new DxfCircle(new GeoPoint3(10, 20, 0), new GeoVector3(0, 0, 1), 5) { Layer = "holes", ColorIndex = 3 };

            line.HandOverTo(circle, true, true);

            Assert.Equal("3E", circle.Handle);
            Assert.Equal("17", circle.OwnerHandle);
            Assert.Same(circle, db.GetObject("3E"));
            Assert.Same(circle, db.ModelSpace.ToList()[place]);
            Assert.Contains(circle, group.GetAllEntityIds());
            Assert.Equal("holes", circle.Layer);
            Assert.Equal(3, circle.ColorIndex);
            Assert.Equal("92", circle.ExtensionDictionary.Handle);
            Assert.Equal("GHPROBE", circle.XData[0].Value);

            // The old line is in no file, a copy of what it was, and can be appended again.
            Assert.False(line.IsResident);
            Assert.False(line.IsErased);
            Assert.Null(line.Handle);
            Assert.Equal(new GeoPoint3(100.125, 50.5, 3.0), line.EndPoint);
            Assert.Equal("GHPROBE", line.XData[0].Value);

            string saved = db.ToText();
            Assert.StartsWith("  0\nCIRCLE\n  5\n3E\n102\n{ACAD_XDICTIONARY\n360\n92\n102\n}\n102\n{ACAD_REACTORS\n330\n90\n102\n}\n330\n17\n100\nAcDbEntity\n  8\nholes\n",
                Records(saved).Single(r => HandleOf(r) == "3E"), StringComparison.Ordinal);
            Assert.EndsWith("1001\nGHPROBE\n1000\nnote é\n1040\n3.25\n1070\n7\n", Records(saved).Single(r => HandleOf(r) == "3E"), StringComparison.Ordinal);
            OnlyTheseChanged(text, saved, "3E", db.LayerTable["holes"].Handle, db.LayerTable.Handle);

            DxfDatabase read = DxfDatabase.Parse(saved);
            Assert.Equal(new GeoPoint3(10, 20, 0), ByHandle<DxfCircle>(read, "3E").Center);
            db.ModelSpace.AppendEntity(line);
            Assert.NotEqual("3E", line.Handle);
        }

        [Fact]
        public void AnEntityHandedOverWithoutTheOthersData_KeepsItsOwn_AndTheOthersDictionaryGoes()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfLine line = ByHandle<DxfLine>(db, "3E");
            DxfDBDictionary dictionary = line.ExtensionDictionary;
            var circle = new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 2)
            {
                XData = new[] { new DxfTypedValue(1001, "GH_OWN"), new DxfTypedValue(1000, "mine") },
            };

            line.HandOverTo(circle, false, false);

            Assert.Equal(new[] { "GH_OWN", "mine" }, circle.XData.Select(g => g.Value));
            Assert.Null(circle.ExtensionDictionary);
            Assert.True(dictionary.IsErased);

            DxfDatabase read = DxfDatabase.Parse(db.ToText());
            DxfCircle back = ByHandle<DxfCircle>(read, "3E");
            Assert.Null(back.ExtensionDictionary);
            Assert.Null(back.GetXDataForApplication("GHPROBE"));
            Assert.Equal("mine", back.GetXDataForApplication("GH_OWN")[1].Value);
            Assert.False(read.TryGetObject("92", out _));
        }

        [Fact]
        public void ThePlaceOfAPolyline_IsTakenWithItsVertices_AndAPolylineGivesItsVerticesTheHandleItTakes()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfPolyline2d polyline = db.ModelSpace.OfType<DxfPolyline2d>().First();
            string handle = polyline.Handle;
            List<string> vertices = polyline.Select(v => v.Handle).ToList();
            var line = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0));

            polyline.HandOverTo(line, true, true);

            string saved = db.ToText();
            Assert.All(vertices, v => Assert.DoesNotContain(Records(saved), r => HandleOf(r) == v));
            Assert.Equal("LINE", Records(saved).Single(r => HandleOf(r) == handle).Split('\n')[1]);

            var made = new DxfPolyline2d();
            made.AppendVertex(new DxfVertex2d(new GeoPoint3(0, 0, 0), 0, 0, 0, 0));
            made.AppendVertex(new DxfVertex2d(new GeoPoint3(5, 0, 0), 0, 0, 0, 0));
            line.HandOverTo(made, true, true);

            DxfPolyline2d read = ByHandle<DxfPolyline2d>(DxfDatabase.Parse(db.ToText()), handle);
            Assert.Equal(2, read.Count());
            Assert.All(read, v => Assert.Equal(handle, v.OwnerHandle));
            Assert.Equal(new GeoPoint3(5, 0, 0), read.Last().Position);
        }

        [Fact]
        public void AnEntityIsHandedOverOnlyToOneInNoFile_AndOnlyAnEntityHandsItsPlaceOver()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            string text = db.ToText();
            DxfLine line = ByHandle<DxfLine>(db, "3E");
            DxfCircle resident = ByHandle<DxfCircle>(db, "3F");

            Assert.Throws<ArgumentNullException>(() => line.HandOverTo(null, true, true));
            Assert.Throws<ArgumentException>(() => line.HandOverTo(line, true, true));
            Assert.Throws<ArgumentException>(() => line.HandOverTo(resident, true, true));
            Assert.Throws<NotSupportedException>(() => line.HandOverTo(new DxfLayerTableRecord { Name = "x" }, true, true));
            Assert.Throws<NotSupportedException>(() => db.LayerTable["steel"].HandOverTo(new DxfLine(), true, true));
            Assert.Throws<InvalidOperationException>(() => new DxfLine().HandOverTo(new DxfLine(), true, true));
            Assert.Throws<ArgumentException>(() => ByHandle<DxfLine>(DxfDatabase.Parse(DxfFileEzdxfDrawings.R12), "41").HandOverTo(new DxfMText { Contents = "B-12" }, true, true));
            Assert.Equal(text, db.ToText());

            line.Erase();
            Assert.Throws<InvalidOperationException>(() => line.HandOverTo(new DxfLine(), true, true));
        }

        /// <summary>The R2018 drawing with BOLT made a dynamic block and *D1 the anonymous block AutoCAD made of it, with an INSERT of *D1 tied to it.</summary>
        private static DxfDatabase Dynamic(out DxfBlockReference insert)
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfBlockTableRecord bolt = db.BlockTable["BOLT"];
            bolt.XData = new[] { new DxfTypedValue(1001, "AcDbDynamicBlockGUID"), new DxfTypedValue(1000, "{3C8E1F3B-7D44-4C93-9F53-5E1C3C2B1A10}") };
            DxfBlockTableRecord anonymous = db.BlockTable["*D1"];
            anonymous.XData = new[] { new DxfTypedValue(1001, "AcDbBlockRepBTag"), new DxfTypedValue(1070, "1"), new DxfTypedValue(1005, bolt.Handle) };
            insert = new DxfBlockReference(new GeoPoint3(500, 0, 0), anonymous);
            db.ModelSpace.AppendEntity(insert);
            insert.CreateExtensionDictionary();
            insert.ExtensionDictionary.SetAt("AcDbBlockRepresentation", new DxfDBDictionary());
            insert.ExtensionDictionary.SetAt("GH_KEEP", new DxfDBDictionary());
            return db;
        }

        [Fact]
        public void AnInsertOfADynamicBlockMadeStatic_PlacesANamedCopyOfWhatItDraws_NoLongerTiedToTheDynamicBlock()
        {
            DxfDatabase db = Dynamic(out DxfBlockReference insert);
            DxfBlockTableRecord anonymous = db.BlockTable["*D1"];
            Assert.True(insert.IsDynamicBlock);

            insert.ConvertToStaticBlock("BOLT_500");

            DxfBlockTableRecord made = db.BlockTable["BOLT_500"];
            Assert.Same(made, insert.BlockTableRecord);
            Assert.Equal("BOLT_500", insert.Name);
            Assert.False(insert.IsDynamicBlock);
            Assert.False(insert.ExtensionDictionary.Contains("AcDbBlockRepresentation"));
            Assert.True(insert.ExtensionDictionary.Contains("GH_KEEP"));
            Assert.False(made.IsAnonymous);
            Assert.Equal(anonymous.Origin, made.Origin);
            Assert.Equal(anonymous.Select(e => e.Item.Type), made.Select(e => e.Item.Type));
            Assert.True(db.BlockTable.Has("*D1"));

            DxfDatabase read = DxfDatabase.Parse(db.ToText());
            DxfBlockReference back = ByHandle<DxfBlockReference>(read, insert.Handle);
            Assert.Equal("BOLT_500", back.Name);
            Assert.False(back.IsDynamicBlock);
            Assert.Equal(anonymous.Count, read.BlockTable["BOLT_500"].Count);
        }

        [Fact]
        public void AnInsertMadeStaticWithNoName_PlacesAnAnonymousCopy()
        {
            DxfDatabase db = Dynamic(out DxfBlockReference insert);

            insert.ConvertToStaticBlock();

            Assert.Equal("*U1", insert.Name);
            Assert.True(insert.BlockTableRecord.IsAnonymous);
            Assert.False(insert.IsDynamicBlock);
            DxfDatabase read = DxfDatabase.Parse(db.ToText());
            Assert.True(read.BlockTable["*U1"].IsAnonymous);
            Assert.Equal(db.BlockTable["*D1"].Count, read.BlockTable["*U1"].Count);

            // An INSERT that places the dynamic block itself takes a copy of that block.
            DxfBlockReference direct = new DxfBlockReference(new GeoPoint3(0, 0, 0), db.BlockTable["BOLT"]);
            db.ModelSpace.AppendEntity(direct);
            Assert.True(direct.IsDynamicBlock);
            direct.ConvertToStaticBlock();
            Assert.Equal("*U2", direct.Name);
            Assert.Equal(db.BlockTable["BOLT"].Count, direct.BlockTableRecord.Count);
            Assert.Equal(db.BlockTable["BOLT"].HasAttributeDefinitions, direct.BlockTableRecord.HasAttributeDefinitions);
        }

        [Fact]
        public void OnlyAnInsertOfADynamicBlock_IsMadeStatic_UnderANameNoBlockHas()
        {
            DxfDatabase db = Dynamic(out DxfBlockReference insert);
            string text = db.ToText();

            Assert.Throws<InvalidOperationException>(() => ByHandle<DxfBlockReference>(DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018), "65").ConvertToStaticBlock("X"));
            Assert.Throws<ArgumentException>(() => insert.ConvertToStaticBlock("frame"));
            Assert.Throws<ArgumentException>(() => insert.ConvertToStaticBlock("A|B"));
            Assert.Throws<ArgumentNullException>(() => insert.ConvertToStaticBlock(null));
            Assert.Throws<InvalidOperationException>(() => new DxfBlockReference(new GeoPoint3(0, 0, 0), db.BlockTable["*D1"]).ConvertToStaticBlock());
            Assert.Equal(text, db.ToText());
        }
    }
}
