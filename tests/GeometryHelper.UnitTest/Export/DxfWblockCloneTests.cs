using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Export;
using Xunit;
using static GeometryHelper.UnitTest.Export.DxfModel;

namespace GeometryHelper.UnitTest.Export
{
    /// <summary>
    /// Entities of <see cref="DxfHatchLeaderDrawings"/> copied together by <see cref="DxfDatabase.WblockCloneObjects"/>, as
    /// AutoCAD's <c>WblockCloneObjects</c> copies them: what points from one to another kept between their copies (a HATCH to
    /// its boundary, a LEADER to its MTEXT), a GROUP all of whose entities come along copied with them, and a set the drawing
    /// cannot hold refused whole; a LEADER copied alone pointing to nothing.
    /// </summary>
    public class DxfWblockCloneTests
    {
        [Fact]
        public void AHatchCopiedWithItsBoundary_FollowsTheBoundarysCopy_WhicheverComesFirst()
        {
            DxfDatabase source = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            DxfDatabase target = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfEntity hatch = ByHandle<DxfHatch>(source, "AC");
            DxfEntity boundary = ByHandle<DxfPolyline>(source, "AB");

            IReadOnlyList<DxfEntity> copies = source.WblockCloneObjects(new[] { hatch, boundary }, target.ModelSpace);

            var copied = (DxfHatch)copies[0];
            Assert.IsType<DxfPolyline>(copies[1]);
            Assert.True(copied.Associative);
            Assert.Same(copies[1], Assert.Single(copied.GetAssociatedObjects()));
            string saved = target.ToText();
            Assert.Contains("102\n{ACAD_REACTORS\n330\n" + copied.Handle + "\n102\n}\n", Records(saved).Single(r => HandleOf(r) == copies[1].Handle), StringComparison.Ordinal);
            Assert.Contains(" 97\n1\n330\n" + copies[1].Handle + "\n", Records(saved).Single(r => HandleOf(r) == copied.Handle), StringComparison.Ordinal);
            var read = ByHandle<DxfHatch>(DxfDatabase.Parse(saved), copied.Handle);
            Assert.True(read.Associative);
            Assert.Equal(copies[1].Handle, Assert.Single(read.GetAssociatedObjects()).Handle);

            // The originals follow each other as they did.
            Assert.Same(boundary, Assert.Single(((DxfHatch)hatch).GetAssociatedObjects()));
        }

        [Fact]
        public void AHatchCopiedWithoutItsBoundary_FollowsNone()
        {
            DxfDatabase source = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            DxfDatabase target = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);

            var copied = (DxfHatch)source.WblockCloneObjects(new DxfEntity[] { ByHandle<DxfHatch>(source, "AC") }, target.ModelSpace).Single();

            Assert.False(copied.Associative);
            Assert.Empty(copied.GetAssociatedObjects());
        }

        [Fact]
        public void ACopyIntoItsOwnDrawing_FollowsTheCopiesToo_TheOriginalsLeftFollowingEachOther()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            var hatch = ByHandle<DxfHatch>(db, "AC");
            DxfEntity boundary = ByHandle<DxfPolyline>(db, "AB");

            IReadOnlyList<DxfEntity> copies = db.WblockCloneObjects(new DxfEntity[] { boundary, hatch }, db.ModelSpace);

            Assert.Same(copies[0], Assert.Single(((DxfHatch)copies[1]).GetAssociatedObjects()));
            Assert.Same(boundary, Assert.Single(hatch.GetAssociatedObjects()));
        }

        [Fact]
        public void ALeaderCopiedWithItsMText_PointsToItsCopy_AndAloneToNothing()
        {
            DxfDatabase source = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            DxfDatabase target = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var leader = ByHandle<DxfLeader>(source, "B2");
            var note = ByHandle<DxfMText>(source, "B1");

            IReadOnlyList<DxfEntity> copies = source.WblockCloneObjects(new DxfEntity[] { leader, note }, target.ModelSpace);

            var copied = (DxfLeader)copies[0];
            Assert.Same(copies[1], copied.Annotation);
            Assert.Equal(DxfAnnotationType.MText, copied.AnnoType);
            Assert.Contains("102\n{ACAD_REACTORS\n330\n" + copied.Handle + "\n102\n}\n", Records(target.ToText()).Single(r => HandleOf(r) == copies[1].Handle), StringComparison.Ordinal);

            // Alone, by WblockCloneObjects or by Clone, it points to nothing.
            var alone = (DxfLeader)source.WblockCloneObjects(new DxfEntity[] { leader }, target.ModelSpace).Single();
            Assert.Null(alone.Annotation);
            Assert.Equal(DxfAnnotationType.NoAnnotation, alone.AnnoType);
            var cloned = (DxfLeader)leader.Clone();
            target.ModelSpace.AppendEntity(cloned);
            Assert.Equal(DxfAnnotationType.NoAnnotation, cloned.AnnoType);
            Assert.DoesNotContain("\n340\n", Records(target.ToText()).Single(r => HandleOf(r) == cloned.Handle), StringComparison.Ordinal);
        }

        [Fact]
        public void AGroupAllOfWhoseEntitiesAreCopied_IsCopied_UnderANameOfItsOwnThere()
        {
            DxfDatabase source = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            DxfDatabase target = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfEntity hatch = ByHandle<DxfHatch>(source, "AC");
            DxfEntity boundary = ByHandle<DxfPolyline>(source, "AB");
            var plate = new DxfGroup("Plate P1", false);
            source.GroupDictionary.SetAt("PLATE", plate);
            plate.Append(boundary);
            plate.Append(hatch);
            var mixed = new DxfGroup("Plate and leader", true);
            source.GroupDictionary.SetAt("MIXED", mixed);
            mixed.Append(boundary);
            mixed.Append(ByHandle<DxfLeader>(source, "B3"));
            var loose = new DxfGroup();
            loose.SetAnonymous();
            source.GroupDictionary.SetAt("*", loose);
            loose.Append(hatch);
            var theirs = new DxfGroup();
            target.GroupDictionary.SetAt("PLATE", theirs);
            theirs.Append(target.ModelSpace.First());

            IReadOnlyList<DxfEntity> copies = source.WblockCloneObjects(new[] { hatch, boundary }, target.ModelSpace);

            var copied = (DxfGroup)target.GroupDictionary.GetAt("PLATE_1");
            Assert.Equal("Plate P1", copied.Description);
            Assert.False(copied.Selectable);
            Assert.Equal(new[] { copies[1], copies[0] }, copied.GetAllEntityIds());
            Assert.False(target.GroupDictionary.Contains("MIXED"));
            DxfGroup anonymous = target.GroupDictionary.Select(e => e.Value).OfType<DxfGroup>().Single(g => g.IsAnonymous);
            Assert.Equal(new[] { copies[0] }, anonymous.GetAllEntityIds());
            DxfDatabase read = DxfDatabase.Parse(target.ToText());
            Assert.Equal(2, ((DxfGroup)read.GroupDictionary.GetAt("PLATE_1")).NumEntities);
        }

        [Fact]
        public void EntitiesCopiedIntoABlock_BringNoGroup_WhichHoldsNoEntityOfABlock()
        {
            DxfDatabase source = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            DxfDatabase target = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfEntity line = source.ModelSpace.OfType<DxfLine>().First();
            var one = new DxfGroup("one", true);
            source.GroupDictionary.SetAt("ONE", one);
            one.Append(line);
            string[] before = target.GroupDictionary.Select(e => e.Key).ToArray();

            DxfEntity copy = source.WblockCloneObjects(new[] { line }, target.BlockTable["BOLT"]).Single();

            Assert.Equal(before, target.GroupDictionary.Select(e => e.Key).ToArray());
            Assert.Same(target.BlockTable["BOLT"], copy.Owner);
            source.WblockCloneObjects(new[] { line }, target.ModelSpace);
            Assert.Contains("ONE", target.GroupDictionary.Select(e => e.Key));
        }

        [Fact]
        public void ASetTheDrawingCannotHold_IsRefusedWhole_AndTheDrawingIsLeftAsItWas()
        {
            DxfDatabase source = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            string text = DxfFileEzdxfDrawings.R12;
            DxfDatabase target = DxfDatabase.Parse(text);
            DxfEntity line = source.ModelSpace.OfType<DxfLine>().First();

            ArgumentException refused = Assert.Throws<ArgumentException>(() => source.WblockCloneObjects(new[] { line, ByHandle<DxfHatch>(source, "AC") }, target.ModelSpace));
            Assert.Contains("HATCH", refused.Message, StringComparison.Ordinal);
            Assert.Equal(text, target.ToText());
            Assert.Throws<ArgumentException>(() => source.WblockCloneObjects(new[] { line, line }, target.ModelSpace));
            Assert.Throws<ArgumentException>(() => source.WblockCloneObjects(new[] { target.ModelSpace.First() }, target.ModelSpace));
            Assert.Throws<ArgumentNullException>(() => source.WblockCloneObjects(null, target.ModelSpace));
            Assert.Equal(text, target.ToText());
        }
    }
}
