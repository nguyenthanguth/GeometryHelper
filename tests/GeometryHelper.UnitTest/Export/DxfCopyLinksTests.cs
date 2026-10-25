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
    /// What a copy into another drawing brings and keeps, as AutoCAD's deep clone between databases does: the named block an
    /// INSERT places, with the blocks it inserts, where the drawing has none of the name; the links between the entities of a
    /// block copied with it, whichever comes first (a LEADER to its MTEXT, a HATCH to its boundary); and a HATCH's spline edge's
    /// count of fit points.
    /// </summary>
    public class DxfCopyLinksTests
    {
        // The spline edge of the HATCH AD of R2018 given two fit points, its count of them (97) 2, before the loop's own 97.
        private static string FittedSpline() => DxfHatchLeaderDrawings.R2018.Replace(
            " 10\n200.0\n 20\n0.0\n 97\n0\n 97\n0\n 92\n18\n",
            " 10\n200.0\n 20\n0.0\n 97\n2\n 11\n205.0\n 21\n20.0\n 11\n200.0\n 21\n0.0\n 97\n0\n 92\n18\n");

        [Fact]
        public void AnInsertCopiedIntoADrawingWithNoBlockOfItsName_BringsItsBlock_WithTheBlocksItInserts()
        {
            DxfDatabase source = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var nut = new DxfBlockTableRecord { Name = "GH_NUT" };
            source.BlockTable.Add(nut);
            nut.AppendEntity(new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 1.5));
            DxfBlockTableRecord bolt = source.BlockTable["BOLT"];
            bolt.AppendEntity(new DxfBlockReference(new GeoPoint3(0, 0, 0), nut));
            bolt.Comments = "Bolt M20";
            DxfDatabase target = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018);
            Assert.False(target.BlockTable.Has("BOLT"));
            DxfEntity copy = ByHandle<DxfBlockReference>(source, "65").Clone();

            target.ModelSpace.AppendEntity(copy);

            DxfDatabase read = DxfDatabase.Parse(target.ToText());
            Assert.Equal(bolt.Count, read.BlockTable["BOLT"].Count);
            Assert.Equal("Bolt M20", read.BlockTable["BOLT"].Comments);
            Assert.IsType<DxfCircle>(Assert.Single(read.BlockTable["GH_NUT"]));
            Assert.Equal("BOLT", ByHandle<DxfBlockReference>(read, copy.Handle).Name);
            Assert.Equal(((DxfBlockReference)ByHandle<DxfBlockReference>(source, "65")).AttributeCollection.Count, ByHandle<DxfBlockReference>(read, copy.Handle).AttributeCollection.Count);

            // A second copy, and one by WblockCloneObjects, place the block brought, not another.
            int blocks = target.BlockTable.Count();
            target.ModelSpace.AppendEntity(ByHandle<DxfBlockReference>(source, "65").Clone());
            source.WblockCloneObjects(new DxfEntity[] { ByHandle<DxfBlockReference>(source, "65") }, target.ModelSpace);
            Assert.Equal(blocks, target.BlockTable.Count());
        }

        [Fact]
        public void AnInsertCopiedIntoADrawingWithABlockOfItsName_PlacesThatDrawingsOwn()
        {
            DxfDatabase source = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfDatabase target = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018);
            var own = new DxfBlockTableRecord { Name = "BOLT" };
            target.BlockTable.Add(own);
            own.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0)));
            int blocks = target.BlockTable.Count();

            target.ModelSpace.AppendEntity(ByHandle<DxfBlockReference>(source, "65").Clone());

            Assert.Equal(blocks, target.BlockTable.Count());
            Assert.IsType<DxfLine>(Assert.Single(DxfDatabase.Parse(target.ToText()).BlockTable["BOLT"]));
        }

        [Fact]
        public void TheEntitiesOfABlockCopied_KeepTheirLinks_WhicheverComesFirst()
        {
            // The block GH_PLATE holds, in this order, the LEADER B2 before its MTEXT B1 and the HATCH AC before its boundary AB.
            DxfDatabase source = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            var plate = new DxfBlockTableRecord { Name = "GH_PLATE" };
            source.BlockTable.Add(plate);
            source.WblockCloneObjects(new DxfEntity[] { ByHandle<DxfLeader>(source, "B2"), ByHandle<DxfMText>(source, "B1"), ByHandle<DxfHatch>(source, "AC"), ByHandle<DxfPolyline>(source, "AB") }, plate);
            Assert.NotNull(plate.OfType<DxfLeader>().Single().Annotation);
            Assert.True(plate.OfType<DxfHatch>().Single().Associative);
            var insert = new DxfBlockReference(new GeoPoint3(0, 0, 0), plate);
            source.ModelSpace.AppendEntity(insert);
            DxfDatabase target = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018);

            target.ModelSpace.AppendEntity(insert.Clone());

            DxfDatabase read = DxfDatabase.Parse(target.ToText());
            DxfBlockTableRecord copied = read.BlockTable["GH_PLATE"];
            DxfLeader leader = copied.OfType<DxfLeader>().Single();
            Assert.Same(copied.OfType<DxfMText>().Single(), leader.Annotation);
            Assert.Equal(DxfAnnotationType.MText, leader.AnnoType);
            DxfHatch hatch = copied.OfType<DxfHatch>().Single();
            Assert.True(hatch.Associative);
            Assert.Same(copied.OfType<DxfPolyline>().Single(), Assert.Single(hatch.GetAssociatedObjects()));
            List<string> records = Records(target.ToText());
            Assert.Contains("102\n{ACAD_REACTORS\n330\n" + hatch.Handle + "\n102\n}\n", records.Single(r => HandleOf(r) == copied.OfType<DxfPolyline>().Single().Handle), StringComparison.Ordinal);
            Assert.Contains("102\n{ACAD_REACTORS\n330\n" + leader.Handle + "\n102\n}\n", records.Single(r => HandleOf(r) == leader.Annotation.Handle), StringComparison.Ordinal);
            Assert.DoesNotContain("\n?", target.ToText(), StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ALeaderAndItsMTextInADimensionsPicture_CopiedWithIt_KeepTheirLink_InEitherOrder(bool noteFirst)
        {
            // The picture *D of a dimension of the drawing given the LEADER B2 and its MTEXT B1, in one order or the other.
            DxfDatabase source = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            DxfEntity dimension = DxfDatabase.Parse(DxfDimensionDrawings.R2018).ModelSpace.OfType<DxfDimension>().Single(d => d.Handle == "61").Clone();
            source.ModelSpace.AppendEntity(dimension);
            DxfBlockTableRecord picture = ((DxfDimension)dimension).DimBlock;
            DxfEntity leader = ByHandle<DxfLeader>(source, "B2");
            DxfEntity note = ByHandle<DxfMText>(source, "B1");
            source.WblockCloneObjects(noteFirst ? new[] { note, leader } : new[] { leader, note }, picture);
            DxfDatabase target = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var copy = (DxfDimension)dimension.Clone();

            target.ModelSpace.AppendEntity(copy);

            DxfBlockTableRecord copied = copy.DimBlock;
            Assert.True(copied.IsAnonymous);
            DxfLeader pointing = copied.OfType<DxfLeader>().Single();
            DxfMText words = copied.OfType<DxfMText>().Single(m => m.Text == "note");
            Assert.Same(words, pointing.Annotation);
            Assert.Equal(DxfAnnotationType.MText, pointing.AnnoType);
            Assert.Contains("102\n{ACAD_REACTORS\n330\n" + pointing.Handle + "\n102\n}\n", Records(target.ToText()).Single(r => HandleOf(r) == words.Handle), StringComparison.Ordinal);
        }

        [Fact]
        public void AHatchCopied_KeepsTheCountOfFitPointsOfItsSplineEdge()
        {
            string text = FittedSpline();
            Assert.NotEqual(DxfHatchLeaderDrawings.R2018, text);
            DxfDatabase source = DxfDatabase.Parse(text);
            DxfDatabase target = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfEntity copy = ByHandle<DxfHatch>(source, "AD").Clone();

            target.ModelSpace.AppendEntity(copy);

            string record = Records(target.ToText()).Single(r => HandleOf(r) == copy.Handle);
            Assert.Contains(" 97\n2\n 11\n205.0\n 21\n20.0\n 11\n200.0\n", record, StringComparison.Ordinal);
            Assert.Equal(2, ByHandle<DxfHatch>(DxfDatabase.Parse(target.ToText()), copy.Handle).NumberOfLoops);
        }
    }
}
