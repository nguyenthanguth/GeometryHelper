using System;
using System.Linq;
using GeometryHelper.Export;
using Xunit;
using static GeometryHelper.UnitTest.Export.DxfModel;

namespace GeometryHelper.UnitTest.Export
{
    /// <summary>The face of a polyface mesh as a <see cref="DxfFaceRecord"/>: its corners' numbers and the signs that hide its edges.</summary>
    public class DxfFaceRecordTests
    {
        [Fact]
        public void AFaceMadeInCode_HidesAnEdgeByTheSignOfItsCorner()
        {
            var face = new DxfFaceRecord(1, 2, -3, 0);

            Assert.Equal(3, face.GetVertexAt(2));
            Assert.False(face.IsEdgeVisibleAt(2));
            Assert.Equal(0, face.GetVertexAt(3));

            face.MakeEdgeVisibleAt(2);
            face.MakeEdgeInvisibleAt(0);
            face.SetVertexAt(0, 7);

            Assert.True(face.IsEdgeVisibleAt(2));
            Assert.False(face.IsEdgeVisibleAt(0));
            Assert.Equal(7, face.GetVertexAt(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => face.GetVertexAt(4));
        }

        [Fact]
        public void AFaceOfAMesh_IsSetInItsOwnRecord()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfFaceRecord face = ByHandle<DxfPolyFaceMesh>(db, "4E").OfType<DxfFaceRecord>().First();

            face.MakeEdgeInvisibleAt(1);

            DxfFaceRecord again = ByHandle<DxfPolyFaceMesh>(DxfDatabase.Parse(db.ToText()), "4E").OfType<DxfFaceRecord>().First();
            Assert.Equal(face.Handle, again.Handle);
            Assert.False(again.IsEdgeVisibleAt(1));
            Assert.Equal(2, again.GetVertexAt(1));
            Assert.Contains("\n 72\n-2\n", Records(db.ToText()).Single(r => HandleOf(r) == face.Handle), StringComparison.Ordinal);
        }
    }
}
