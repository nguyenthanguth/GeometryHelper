using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The fields of texts and of the attributes of an INSERT, copied by <see cref="DxfDatabase.WblockCloneObjects"/> as
    /// AutoCAD's <c>WblockCloneObjects</c> copies them: each copy keeps a field of its own, of the same code, in the
    /// drawing it is copied into, the original's left as it was.
    /// </summary>
    public class DxfWblockCloneFieldTests
    {
        private static readonly string Slash = ((char)92).ToString();
        private static readonly string Code = "By %<" + Slash + "AcVar Login>%";

        private static DxfBlockReference InsertWithField(DxfDatabase db, out DxfAttributeReference attribute)
        {
            var block = new DxfBlockTableRecord { Name = "TB" };
            db.BlockTable.Add(block);
            var definition = new DxfAttributeDefinition(new GeoPoint3(0, 0, 0), "x", "AUTHOR", "Author", null);
            block.AppendEntity(definition);
            var insert = new DxfBlockReference(new GeoPoint3(0, 0, 0), block);
            db.ModelSpace.AppendEntity(insert);
            attribute = new DxfAttributeReference();
            attribute.SetAttributeFromBlock(definition, insert.BlockTransform);
            insert.AttributeCollection.AppendAttribute(attribute);
            attribute.SetField(new DxfField(Code, true));
            return insert;
        }

        [Fact]
        public void ATextWithAFieldCopiedIntoItsOwnDrawing_KeepsAFieldOfItsOwn()
        {
            var db = new DxfDatabase("AC1032");
            var text = new DxfDBText
            {
                TextString = "x",
                Position = new GeoPoint3(0, 0, 0),
                Height = 2.5,
            };
            db.ModelSpace.AppendEntity(text);
            DxfField original = text.SetField(new DxfField(Code, true));

            var copy = (DxfDBText)db.WblockCloneObjects(new DxfEntity[] { text }, db.ModelSpace).Single();

            DxfField field = Assert.IsType<DxfField>(copy.GetField());
            Assert.Equal("By %<" + Slash + "_FldIdx 0>%", field.GetFieldCode());
            Assert.NotEqual(original.Handle, field.Handle);
            Assert.Same(original, text.GetField());
        }

        [Fact]
        public void AnInsertWhoseAttributeHoldsAFieldCopiedIntoAnotherDrawing_KeepsTheFieldOfTheAttribute()
        {
            var source = new DxfDatabase("AC1032");
            DxfBlockReference insert = InsertWithField(source, out DxfAttributeReference attribute);
            var target = new DxfDatabase("AC1032");

            var copy = (DxfBlockReference)
                source.WblockCloneObjects(new DxfEntity[] { insert }, target.ModelSpace).Single();

            DxfField field = copy.AttributeCollection.Single().GetField();
            Assert.NotNull(field);
            Assert.Equal(attribute.GetField().GetFieldCode(), field.GetFieldCode());
            Assert.Equal(Code, copy.AttributeCollection.Single().GetTextWithFieldCodes());
            Assert.NotNull(attribute.GetField());
        }

        [Fact]
        public void AnInsertWhoseAttributeHoldsAFieldCopiedIntoItsOwnDrawing_KeepsTheFieldOfTheAttribute()
        {
            var db = new DxfDatabase("AC1032");
            DxfBlockReference insert = InsertWithField(db, out DxfAttributeReference attribute);

            var copy = (DxfBlockReference)db.WblockCloneObjects(new DxfEntity[] { insert }, db.ModelSpace).Single();

            DxfField field = copy.AttributeCollection.Single().GetField();
            Assert.NotNull(field);
            Assert.NotEqual(attribute.GetField().Handle, field.Handle);
        }

        [Fact]
        public void TheCopiedFieldsAreListedInTheListOfFields_AndReadBackFromTheSavedDrawing()
        {
            var source = new DxfDatabase("AC1032");
            DxfBlockReference insert = InsertWithField(source, out _);
            var target = new DxfDatabase("AC1032");

            var copy = (DxfBlockReference)
                source.WblockCloneObjects(new DxfEntity[] { insert }, target.ModelSpace).Single();

            DxfAttributeReference attribute = copy.AttributeCollection.Single();
            DxfField field = attribute.GetField();
            string saved = target.ToText();
            string list = Records(saved).Single(r => r.StartsWith("  0\nFIELDLIST\n", System.StringComparison.Ordinal));
            Assert.Contains("\n330\n" + field.Handle + "\n", list, System.StringComparison.Ordinal);
            Assert.Contains("\n330\n" + field.GetChildren()[0].Handle + "\n", list, System.StringComparison.Ordinal);

            DxfAttributeReference read = DxfDatabase
                .Parse(saved)
                .ModelSpace.OfType<DxfBlockReference>()
                .Single()
                .AttributeCollection.Single();
            Assert.Equal(field.GetFieldCode(), read.GetField().GetFieldCode());
        }
    }
}
