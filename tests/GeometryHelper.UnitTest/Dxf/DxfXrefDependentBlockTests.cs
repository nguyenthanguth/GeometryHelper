using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The blocks a drawing attached as an external reference brings, <c>grid|TAG</c> for the block TAG of the drawing
    /// <c>grid</c>: AutoCAD 2021 (accoreconsole, <c>tblnext</c> and -BLOCK ?) lists each of them in the block table of the
    /// drawing that attaches it while the reference is loaded, its BLOCK flagged 48 (dependent, resolved), and its DXFOUT
    /// writes none of them. A block made in code is marked as a layer and a linetype are, by <c>SetDependent</c>, so that its
    /// table takes a name with a | and writes the flags.
    /// </summary>
    public class DxfXrefDependentBlockTests
    {
        private static DxfDatabase WithGrid(out DxfBlockTableRecord grid)
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            grid = db.AttachXref(".\\grid.dwg", "grid");
            return db;
        }

        [Fact]
        public void ABlockMarkedDependentAndResolved_IsAddedUnderANameWithABar_AndWrittenFlaggedFortyEight()
        {
            DxfDatabase db = WithGrid(out _);
            var tag = new DxfBlockTableRecord { Name = "grid|TAG" };
            tag.SetDependent(true, true);
            db.BlockTable.Add(tag);
            tag.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(0, 10, 0)));

            Assert.True(tag.IsDependent);
            Assert.True(tag.IsResolved);
            Assert.False(tag.IsFromExternalReference);
            Assert.Same(tag, db.BlockTable["grid|TAG"]);
            string saved = db.ToText();
            Assert.Contains("  2\ngrid|TAG\n 70\n48\n", saved, StringComparison.Ordinal);
            Assert.Single(Records(saved), r =>
                r.StartsWith("  0\nBLOCK_RECORD\n", StringComparison.Ordinal) && r.Contains("\n  2\ngrid|TAG\n")
            );

            DxfBlockTableRecord read = DxfDatabase.Parse(saved).BlockTable["grid|TAG"];
            Assert.True(read.IsDependent);
            Assert.True(read.IsResolved);
            Assert.False(read.IsFromExternalReference);
            Assert.Single(read);
        }

        [Fact]
        public void ADependentBlockOfAnUnloadedReference_IsFlaggedSixteen_AloneAndSetBackAfterItIsAdded()
        {
            DxfDatabase db = WithGrid(out _);
            var tag = new DxfBlockTableRecord { Name = "grid|TAG" };
            tag.SetDependent(true, false);
            db.BlockTable.Add(tag);

            Assert.False(tag.IsResolved);
            Assert.Contains("  2\ngrid|TAG\n 70\n16\n", db.ToText(), StringComparison.Ordinal);

            tag.SetDependent(true, true);

            Assert.True(tag.IsResolved);
            Assert.Contains("  2\ngrid|TAG\n 70\n48\n", db.ToText(), StringComparison.Ordinal);

            tag.SetDependent(true, false);

            Assert.False(tag.IsResolved);
            Assert.True(tag.IsDependent);
            Assert.Contains("  2\ngrid|TAG\n 70\n16\n", db.ToText(), StringComparison.Ordinal);
        }

        [Fact]
        public void ANameAndTheMarksThatDisagree_AreRefused_AsForALayer()
        {
            DxfDatabase db = WithGrid(out _);
            var plain = new DxfBlockTableRecord { Name = "TAG" };
            Assert.Throws<ArgumentException>(() => plain.SetDependent(true, true));
            Assert.Throws<ArgumentException>(() => plain.SetDependent(false, true));

            Assert.Throws<ArgumentException>(() => db.BlockTable.Add(new DxfBlockTableRecord { Name = "grid|TAG" }));
            var bare = new DxfBlockTableRecord { Name = "|TAG" };
            bare.SetDependent(true, true);
            Assert.Throws<ArgumentException>(() => db.BlockTable.Add(bare));
            var noOwner = new DxfBlockTableRecord { Name = "grid|" };
            noOwner.SetDependent(true, true);
            Assert.Throws<ArgumentException>(() => db.BlockTable.Add(noOwner));

            var tag = new DxfBlockTableRecord { Name = "grid|TAG" };
            tag.SetDependent(true, true);
            Assert.Throws<ArgumentException>(() => tag.SetDependent(false, false));
            db.BlockTable.Add(tag);
            Assert.Throws<ArgumentException>(() => tag.SetDependent(false, false));
            Assert.Throws<ArgumentException>(() => db.BlockTable["grid"].SetDependent(true, true));
            Assert.True(tag.IsDependent);
        }

        [Fact]
        public void ADependentBlock_IsNotRenamed_AndGoesWithItsReferenceWhenItIsDetached()
        {
            DxfDatabase db = WithGrid(out DxfBlockTableRecord grid);
            var tag = new DxfBlockTableRecord { Name = "grid|TAG" };
            tag.SetDependent(true, true);
            db.BlockTable.Add(tag);

            Assert.Throws<InvalidOperationException>(() => tag.Name = "OTHER");

            db.DetachXref(grid);

            Assert.False(db.BlockTable.Has("grid|TAG"));
            Assert.Throws<InvalidOperationException>(() => tag.SetDependent(true, true));
        }

        [Fact]
        public void ABlockOfTheDrawingsOwn_IsNoDependentOne_AndTheMarksDoNotChangeWhatItWrites()
        {
            DxfDatabase db = WithGrid(out _);
            var washer = new DxfBlockTableRecord { Name = "Washer" };
            washer.SetDependent(false, false);
            db.BlockTable.Add(washer);

            Assert.False(washer.IsDependent);
            Assert.False(washer.IsResolved);
            Assert.Contains("  2\nWasher\n 70\n0\n", db.ToText(), StringComparison.Ordinal);
        }
    }
}
