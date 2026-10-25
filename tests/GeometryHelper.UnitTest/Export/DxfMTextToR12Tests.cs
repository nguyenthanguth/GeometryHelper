using System;
using System.Linq;
using GeometryHelper.Export;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Export.DxfModel;

namespace GeometryHelper.UnitTest.Export
{
    /// <summary>
    /// An MTEXT written into a drawing of R12, which has none, as one TEXT for each of its lines, as AutoCAD's SAVEAS to R12
    /// writes it: appended in code, and in the picture (<c>*D</c>) of a dimension copied there from a drawing of R2018.
    /// </summary>
    public class DxfMTextToR12Tests
    {
        private const double SingleSpacing = 5.0 / 3.0;

        [Fact]
        public void AnMTextAppendedToR12_IsWrittenAsOneTextForEachLine_OnTheBaselinesOfItsLines()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R12);
            int before = db.ModelSpace.Count();
            var mtext = new DxfMText { Contents = "{\\fArial|b1;B-12}\\PM16", Location = new GeoPoint3(10, 20, 0), TextHeight = 2.5, Layer = "steel" };

            db.ModelSpace.AppendEntity(mtext);

            Assert.False(mtext.IsResident);
            DxfDBText[] texts = DxfDatabase.Parse(db.ToText()).ModelSpace.Skip(before).Cast<DxfDBText>().ToArray();
            Assert.Equal(new[] { "B-12", "M16" }, texts.Select(t => t.TextString));
            Assert.Equal(Bits(new GeoPoint3(10.0, 17.5, 0.0)), Bits(texts[0].Position));
            Assert.Equal(20.0 - 2.5 - 2.5 * SingleSpacing, texts[1].Position.Y, 12);
            Assert.All(texts, t => Assert.Equal(2.5, t.Height));
            Assert.All(texts, t => Assert.Equal(DxfTextHorizontalMode.TextLeft, t.HorizontalMode));
            Assert.All(texts, t => Assert.Equal("steel", t.Layer));
            Assert.DoesNotContain("\nMTEXT\n", db.ToText(), StringComparison.Ordinal);
        }

        [Fact]
        public void AnMTextHeldAtItsMiddle_IsWrittenAsCentredTexts_AboutItsMiddle()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R12);
            int before = db.ModelSpace.Count();
            var mtext = new DxfMText { Contents = "one\\Ptwo", Location = new GeoPoint3(0, 0, 0), TextHeight = 3.0, Attachment = DxfAttachmentPoint.MiddleCenter };

            db.ModelSpace.AppendEntity(mtext);

            DxfDBText[] texts = db.ModelSpace.Skip(before).Cast<DxfDBText>().ToArray();
            double pitch = 3.0 * SingleSpacing;
            double first = (3.0 + pitch) / 2.0 - 3.0;
            Assert.All(texts, t => Assert.Equal(DxfTextHorizontalMode.TextCenter, t.HorizontalMode));
            Assert.Equal(first, texts[0].AlignmentPoint.Y, 12);
            Assert.Equal(first - pitch, texts[1].AlignmentPoint.Y, 12);
            Assert.Equal(0.0, texts[1].AlignmentPoint.X, 12);
        }

        [Fact]
        public void AColumnBreak_BeginsALine_AndBlankParagraphsAtTheEnd_AreNoLines()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R12);
            int before = db.ModelSpace.Count();

            // As ezdxf places them, a\P\Pb\P held at its bottom left 3 high: a at 10, b at 0.
            db.ModelSpace.AppendEntity(new DxfMText { Contents = "a\\P\\Pb\\P", Location = new GeoPoint3(0, 0, 0), TextHeight = 3.0, Attachment = DxfAttachmentPoint.BottomLeft });
            db.ModelSpace.AppendEntity(new DxfMText { Contents = "c1\\Nc2", Location = new GeoPoint3(0, 50, 0), TextHeight = 1.0 });

            DxfDBText[] texts = db.ModelSpace.Skip(before).Cast<DxfDBText>().ToArray();
            Assert.Equal(new[] { "a", "b", "c1", "c2" }, texts.Select(t => t.TextString));
            Assert.Equal(10.0, texts[0].Position.Y, 12);
            Assert.Equal(0.0, texts[1].Position.Y, 12);
            Assert.Equal(49.0 - SingleSpacing, texts[3].Position.Y, 12);
        }

        [Fact]
        public void AnMTextOfAnotherDrawingWrittenIntoR12_KeepsItsTextStyle_CopiedByName()
        {
            DxfDatabase source = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var note = new DxfTextStyleTableRecord { Name = "GH_NOTE", FileName = "romans.shx" };
            source.TextStyleTable.Add(note);
            DxfMText mtext = ByHandle<DxfMText>(source, "73");
            mtext.TextStyle = note;
            DxfDatabase r12 = DxfDatabase.Parse(DxfFileEzdxfDrawings.R12);
            Assert.False(r12.TextStyleTable.Has("GH_NOTE"));
            int before = r12.ModelSpace.Count();

            r12.ModelSpace.AppendEntity(mtext.Clone());

            DxfDatabase read = DxfDatabase.Parse(r12.ToText());
            Assert.Equal("romans.shx", read.TextStyleTable["GH_NOTE"].FileName);
            Assert.All(read.ModelSpace.Skip(before).Cast<DxfDBText>(), t => Assert.Equal("GH_NOTE", t.TextStyleName));
        }

        [Fact]
        public void ADimensionCopiedIntoR12_BringsItsPictureWithItsMTextWrittenAsATurnedText()
        {
            DxfDatabase source = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfDatabase r12 = DxfDatabase.Parse(DxfDimensionDrawings.R12);
            var copy = (DxfRotatedDimension)source.ModelSpace.OfType<DxfDimension>().Single(d => d.Handle == "61").Clone();

            r12.ModelSpace.AppendEntity(copy);

            DxfDatabase read = DxfDatabase.Parse(r12.ToText());
            DxfBlockTableRecord picture = read.BlockTable[copy.DimBlock.Name];
            Assert.NotEqual("*D2", picture.Name);
            DxfDBText text = picture.OfType<DxfDBText>().Single();
            Assert.Equal("89,44", text.TextString);
            Assert.Equal(1.0, text.Height);
            Assert.Equal(DxfTextHorizontalMode.TextCenter, text.HorizontalMode);

            // Held at its middle, turned by -26.57 degrees: the baseline half its height below the MTEXT's point, square to the text.
            var along = new GeoVector3(Math.Cos(-26.56505117707799 * Math.PI / 180.0), Math.Sin(-26.56505117707799 * Math.PI / 180.0), 0.0);
            GeoPoint3 expected = new GeoPoint3(44.47213595499957, -31.05572809000084, 0.0).Add(new GeoVector3(-along.Y, along.X, 0.0).Multiply(-0.5));
            Assert.True(expected.DistanceTo(text.AlignmentPoint) <= 1E-9, text.AlignmentPoint.ToString());
            Assert.Equal(along.Y, Math.Sin(text.Rotation), 9);
            Assert.Equal(source.BlockTable["*D2"].Count(), picture.Count());
        }
    }
}
