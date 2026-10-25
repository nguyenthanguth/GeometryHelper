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
    /// What the engine gives the classes of dimensions, tables and hatches: the handle of a DIMSTYLE read from its 105 and
    /// written as one, a DIMENSION written in the forms of R12, and an entity copied into another drawing taking a copy of the
    /// block AutoCAD names itself it draws by (*D, *U) rather than the block of that name there.
    /// </summary>
    public class DxfSharedHooksTests
    {
        [Theory]
        [InlineData("R12")]
        [InlineData("R2000")]
        [InlineData("R2018")]
        public void TheHandleOfADimstyle_IsItsGroup105(string version)
        {
            DxfDatabase db = DxfDatabase.Parse(version == "R12" ? DxfFileEzdxfDrawings.R12 : version == "R2000" ? DxfFileEzdxfDrawings.R2000 : DxfFileEzdxfDrawings.R2018);
            DxfItem standard = db.File.TableItems("DIMSTYLE").First();

            // ezdxf: DIMSTYLE Standard 105 2B, its 5 (DIMBLK) empty in the file of R12.
            Assert.Equal("Standard", db.File.ValueOf(standard, 2));
            Assert.Equal("2B", standard.Handle);
        }

        [Fact]
        public void ADimstyleAdded_IsGivenItsHandleAs105()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfItem added = db.File.AddTableRecord("DIMSTYLE", "AcDbDimStyleTableRecord", b => b.Group(2, "GH"));

            Assert.NotNull(added.Handle);
            string record = Records(db.ToText()).Single(r => r.StartsWith("  0\nDIMSTYLE\n105\n" + added.Handle + "\n", StringComparison.Ordinal));
            Assert.DoesNotContain("\n  5\n", record, StringComparison.Ordinal);
            Assert.Equal(added.Handle, DxfDatabase.Parse(db.ToText()).File.TableItems("DIMSTYLE").Last().Handle);
        }

        [Fact]
        public void ADimensionCopiedIntoAnotherDrawing_TakesACopyOfItsBlockUnderANameOfItsOwn()
        {
            DxfDatabase source = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            string text = DxfFileEzdxfDrawings.R2018;
            DxfDatabase target = DxfDatabase.Parse(text);
            DxfEntity dimension = ByHandle<DxfEntity>(source, "75");
            int entities = source.BlockTable["*D1"].Count;

            DxfEntity copy = dimension.Clone();
            target.ModelSpace.AppendEntity(copy);

            DxfBlockTableRecord made = target.BlockTable["*D2"];
            Assert.Equal("*D2", target.File.ValueOf(copy.Item, 2));
            Assert.True(made.IsAnonymous);
            Assert.Equal(entities, made.Count);
            Assert.All(made, e => Assert.Equal(made.Handle, e.OwnerHandle));
            Assert.Equal(entities, target.BlockTable["*D1"].Count);
            Assert.False(target.BlockTable.Has("*D3"));

            // Saved and read again: the dimension draws by its own block; the target's *D1 keeps its bytes.
            string saved = target.ToText();
            DxfDatabase again = DxfDatabase.Parse(saved);
            Assert.Equal(entities, again.BlockTable["*D2"].Count);
            Assert.Contains(Records(text).Single(r => HandleOf(r) == "77"), Records(saved));
        }

        [Fact]
        public void ADimensionCopiedIntoAnotherDrawing_TakesTheRecordsItNamesThatDrawingHasNot()
        {
            // The source's dimension style renamed GH_DIM in text, its block's text on the style GH_TXT, the dimension on GH_DIMS.
            string text = DxfFileEzdxfDrawings.R2018;
            DxfDatabase source = DxfDatabase.Parse(text.Replace("AcDbDimStyleTableRecord\n  2\nStandard\n", "AcDbDimStyleTableRecord\n  2\nGH_DIM\n").Replace("\n  3\nStandard\n", "\n  3\nGH_DIM\n")
                .Replace("\n 71\n5\n  1\n100\n", "\n 71\n5\n  7\nGH_TXT\n  1\n100\n"));
            source.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "GH_TXT", FileName = "isocp.shx" });
            DxfEntity dimension = ByHandle<DxfEntity>(source, "75");
            dimension.Layer = "GH_DIMS";
            source.LayerTable["GH_DIMS"].ColorIndex = 3;
            Assert.Contains(source.BlockTable["*D1"].OfType<DxfMText>(), t => t.TextStyleName == "GH_TXT");

            DxfDatabase target = DxfDatabase.Parse(text);
            target.ModelSpace.AppendEntity(dimension.Clone());

            Assert.True(target.TextStyleTable.Has("GH_TXT"));
            Assert.Equal(3, target.LayerTable["GH_DIMS"].ColorIndex);
            string saved = target.ToText();
            string style = Records(saved).Single(r => r.StartsWith("  0\nDIMSTYLE\n", StringComparison.Ordinal) && r.Contains("\n  2\nGH_DIM\n"));
            Assert.StartsWith("  0\nDIMSTYLE\n105\n", style, StringComparison.Ordinal);
            Assert.Contains("100\nAcDbDimStyleTableRecord\n  2\nGH_DIM\n", style, StringComparison.Ordinal);
            Assert.Single(Records(saved), r => r.StartsWith("  0\nDIMSTYLE\n", StringComparison.Ordinal) && r.Contains("\n  2\nStandard\n"));

            // A second copy finds them there: nothing is copied twice.
            target.ModelSpace.AppendEntity(dimension.Clone());
            Assert.Single(Records(target.ToText()), r => r.StartsWith("  0\nDIMSTYLE\n", StringComparison.Ordinal) && r.Contains("\n  2\nGH_DIM\n"));
            Assert.Single(target.TextStyleTable.Cast<DxfTextStyleTableRecord>(), s => s.Name == "GH_TXT");
        }

        [Fact]
        public void AShearOfAxesOfOneLength_IsNotTakenAsAScaleAlike_ByACircleOrAPolylineWithArcs()
        {
            // S(2, 1) R(45°): the images of x and y are both of length √2.5, at an angle that is not square.
            double s = Math.Sqrt(0.5);
            var shear = new GeoTransform3(new double[,]
            {
                { 2.0 * s, -2.0 * s, 0.0, 0.0 },
                { s, s, 0.0, 0.0 },
                { 0.0, 0.0, 1.0, 0.0 },
                { 0.0, 0.0, 0.0, 1.0 },
            });

            string text = DxfFileEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfCircle circle = db.ModelSpace.OfType<DxfCircle>().First();
            Assert.Throws<ArgumentException>(() => circle.TransformBy(shear));

            var polyline = new DxfPolyline();
            polyline.AddVertexAt(0, new GeoPoint2(0, 0), 1.0, 0.0, 0.0);
            polyline.AddVertexAt(1, new GeoPoint2(10, 0), 0.0, 0.0, 0.0);
            db.ModelSpace.AppendEntity(polyline);
            string before = db.ToText();
            Assert.Throws<ArgumentException>(() => polyline.TransformBy(shear));
            Assert.Equal(before, db.ToText());

            // The same polyline without arcs is sheared, every vertex carried.
            polyline.SetBulgeAt(0, 0.0);
            polyline.TransformBy(shear);
            Assert.Equal(10.0 * 2.0 * s, polyline.GetPoint2dAt(1).X, 12);
            Assert.Equal(10.0 * s, polyline.GetPoint2dAt(1).Y, 12);
        }

        [Fact]
        public void TheOverridesOfADimensionsStyle_NameTheRecordsOfTheSameNameInTheDrawingCopiedTo()
        {
            DxfDatabase source = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var style = new DxfTextStyleTableRecord { Name = "GH_TXT", FileName = "isocp.shx" };
            source.TextStyleTable.Add(style);
            var arrow = new DxfBlockTableRecord { Name = "GH_ARROW" };
            source.BlockTable.Add(arrow);
            arrow.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(-1, 0.2, 0)));
            DxfEntity dimension = ByHandle<DxfEntity>(source, "75");

            // DIMTXSTY (340) and DIMBLK (342) overridden, each a 1070 of its code and a 1005 of its record; DIMLTYPE (345) names
            // a record the source has not, and goes with its code.
            dimension.XData = new[]
            {
                new DxfTypedValue(1001, "ACAD"), new DxfTypedValue(1000, "DSTYLE"), new DxfTypedValue(1002, "{"),
                new DxfTypedValue(1070, "340"), new DxfTypedValue(1005, style.Handle),
                new DxfTypedValue(1070, "342"), new DxfTypedValue(1005, arrow.Handle),
                new DxfTypedValue(1070, "345"), new DxfTypedValue(1005, "FFFF"),
                new DxfTypedValue(1070, "41"), new DxfTypedValue(1040, "2.5"),
                new DxfTypedValue(1002, "}"),
            };

            DxfDatabase target = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfEntity copy = dimension.Clone();
            target.ModelSpace.AppendEntity(copy);

            DxfTextStyleTableRecord copiedStyle = target.TextStyleTable["GH_TXT"];
            DxfBlockTableRecord copiedArrow = target.BlockTable["GH_ARROW"];
            Assert.Equal(1, copiedArrow.Count);
            Assert.Equal(
                new[] { "1001 ACAD", "1000 DSTYLE", "1002 {", "1070 340", "1005 " + copiedStyle.Handle, "1070 342", "1005 " + copiedArrow.Handle, "1070 41", "1040 2.5", "1002 }" },
                copy.XData.Select(g => g.ToString()));
        }

        [Fact]
        public void ATextCopiedIntoAnotherDrawing_TakesItsStyleWithIt()
        {
            // The TEXT 64 put on the style GHSTYLE in text, the style added to the source.
            DxfDatabase source = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018.Replace("\n 50\n15.0\n 72\n1\n", "\n 50\n15.0\n  7\nGHSTYLE\n 72\n1\n"));
            source.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "GHSTYLE", FileName = "isocp.shx" });
            DxfDBText text = ByHandle<DxfDBText>(source, "64");
            Assert.Equal("GHSTYLE", text.TextStyleName);

            DxfDatabase target = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            Assert.False(target.TextStyleTable.Has("GHSTYLE"));
            var copy = (DxfDBText)text.Clone();
            target.ModelSpace.AppendEntity(copy);

            Assert.True(target.TextStyleTable.Has("GHSTYLE"));
            Assert.Equal("GHSTYLE", copy.TextStyleName);
            Assert.Equal("isocp.shx", DxfDatabase.Parse(target.ToText()).TextStyleTable["GHSTYLE"].FileName);
        }

        [Fact]
        public void AnMTextSheared_IsRefused_AndOneScaledUnlikeAlongItsAxesIsStretched()
        {
            double s = Math.Sqrt(0.5);
            var shear = new GeoTransform3(new double[,]
            {
                { 2.0 * s, -2.0 * s, 0.0, 0.0 },
                { s, s, 0.0, 0.0 },
                { 0.0, 0.0, 1.0, 0.0 },
                { 0.0, 0.0, 0.0, 1.0 },
            });
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfMText mtext = ByHandle<DxfMText>(db, "73");
            string before = db.ToText();

            Assert.Throws<ArgumentException>(() => mtext.TransformBy(shear));
            Assert.Equal(before, db.ToText());

            mtext.TransformBy(GeoTransform3.Scaling(2.0, 3.0, 1.0));
            Assert.Equal(9.0, mtext.TextHeight, 12);
            Assert.Equal(80.0, mtext.Width, 12);
        }

        [Fact]
        public void ADimensionCopiedWithinItsDrawing_KeepsSharingItsBlock()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfEntity copy = ByHandle<DxfEntity>(db, "75").Clone();

            db.ModelSpace.AppendEntity(copy);

            Assert.Equal("*D1", db.File.ValueOf(copy.Item, 2));
            Assert.False(db.BlockTable.Has("*D2"));
        }

        [Fact]
        public void ADimensionCopiedIntoADrawingOfR12_IsWrittenInTheFormsOfR12_WithItsBlock()
        {
            DxfDatabase source = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfDatabase target = DxfDatabase.Parse(DxfFileEzdxfDrawings.R12);
            DxfEntity dimension = ByHandle<DxfEntity>(source, "75");

            // An MTEXT in the block has no form of R12: the dimension is refused, nothing written.
            string before = target.ToText();
            Assert.Throws<ArgumentException>(() => target.ModelSpace.AppendEntity(dimension.Clone()));
            Assert.Equal(before, target.ToText());

            foreach (DxfEntity text in source.BlockTable["*D1"].Where(e => e.Item.Type == "MTEXT").ToList())
            {
                text.Erase();
            }

            int entities = source.BlockTable["*D1"].Count;
            DxfEntity copy = dimension.Clone();
            target.ModelSpace.AppendEntity(copy);

            string record = Records(target.ToText()).Single(r => r.StartsWith("  0\nDIMENSION\n", StringComparison.Ordinal));
            Assert.DoesNotContain("\n100\n", record, StringComparison.Ordinal);
            Assert.Contains("  2\n*D1\n  3\nStandard\n", record, StringComparison.Ordinal);
            Assert.Contains(" 13\n0.0\n", record, StringComparison.Ordinal);
            DxfDatabase again = DxfDatabase.Parse(target.ToText());
            Assert.True(again.BlockTable["*D1"].IsAnonymous);
            Assert.Equal(entities, again.BlockTable["*D1"].Count);
        }

        [Fact]
        public void AnInsertOfAnAnonymousBlockCopiedIntoAnotherDrawing_TakesACopyOfItsBlock()
        {
            // FRAME made the anonymous block *U1 of both drawings, in text.
            string text = DxfFileEzdxfDrawings.R2018.Replace("\nFRAME\n", "\n*U1\n");
            DxfDatabase source = DxfDatabase.Parse(text);
            DxfDatabase target = DxfDatabase.Parse(text);
            DxfBlockReference insert = source.ModelSpace.OfType<DxfBlockReference>().First(i => i.Name == "*U1");
            int entities = source.BlockTable["*U1"].Count;

            var copy = (DxfBlockReference)insert.Clone();
            target.ModelSpace.AppendEntity(copy);

            Assert.Equal("*U2", copy.Name);
            Assert.Equal(entities, target.BlockTable["*U2"].Count);
            Assert.Equal(entities, target.BlockTable["*U1"].Count);
            Assert.Equal(insert.GetGeometry().Count, ByHandle<DxfBlockReference>(DxfDatabase.Parse(target.ToText()), copy.Handle).GetGeometry().Count);
        }
    }
}
