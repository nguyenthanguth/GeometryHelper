using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The clip boundary of a reference to an external reference, as AutoCAD 2021 keeps it (accoreconsole: a drawing whose base
    /// point is (70, 0, 0), attached at (100, 0, 0) and clipped to the rectangle (130, 10) to (190, 40) of the world wrote the
    /// inverse of its placing as a shift by -30): in the drawing's own coordinates, base point and all, so that the boundary is
    /// the drawing's x 100 to 160 and goes with the content when the base point changes.
    /// </summary>
    public class DxfSpatialFilterXrefTests
    {
        private static DxfBlockReference Attached(GeoPoint3 origin, GeoPoint3 at, out DxfBlockTableRecord block)
        {
            var db = new DxfDatabase("AC1032");
            block = db.AttachXref(".\\grid.dxf", "GRID");
            block.Origin = origin;
            var reference = new DxfBlockReference(at, block);
            db.ModelSpace.AppendEntity(reference);
            return reference;
        }

        private static void Near(IReadOnlyList<double> expected, IReadOnlyList<double> actual)
        {
            Assert.Equal(expected.Count, actual.Count);

            for (int i = 0; i < expected.Count; i++)
            {
                Assert.Equal(expected[i], actual[i], 4);
            }
        }

        [Fact]
        public void AnExternalReferenceClip_IsKeptInTheDrawingsOwnCoordinates_AsAutoCadWritesIt()
        {
            DxfBlockReference reference = Attached(new GeoPoint3(70, 0, 0), new GeoPoint3(100, 0, 0), out _);

            reference.ClipTo(new[] { new GeoPoint2(130, 10), new GeoPoint2(190, 40) }, GeoTransform3.Identity);

            Near(new[] { 1.0, 0, 0, -30, 0, 1, 0, 0, 0, 0, 1, 0 }, reference.SpatialFilter.InverseBlockTransform);
            IReadOnlyList<GeoPoint2> ring = reference.ClipOutline;
            Assert.Equal(4, ring.Count);
            Assert.Equal(100.0, ring[0].X, 9);
            Assert.Equal(10.0, ring[0].Y, 9);
            Assert.Equal(160.0, ring[2].X, 9);
            Assert.Equal(40.0, ring[2].Y, 9);
        }

        [Fact]
        public void TheBoundaryOfAnExternalReference_StandsWhereItWasPickedInTheWorld()
        {
            DxfBlockReference reference = Attached(new GeoPoint3(70, 0, 0), new GeoPoint3(100, 0, 0), out _);
            reference.ClipTo(new[] { new GeoPoint2(130, 10), new GeoPoint2(190, 40) }, GeoTransform3.Identity);

            GeoPoint3 corner = reference.ClipPointInWorld(reference.SpatialFilter, new GeoPoint2(190, 40));

            Assert.Equal(190.0, corner.X, 9);
            Assert.Equal(40.0, corner.Y, 9);
            Assert.True(reference.TryClipPointOfWorld(reference.SpatialFilter, corner, out GeoPoint2 back));
            Assert.Equal(190.0, back.X, 9);
            Assert.Equal(40.0, back.Y, 9);
        }

        [Fact]
        public void ABlockThatIsNoExternalReference_KeepsItsClipFromItsOrigin()
        {
            var db = new DxfDatabase("AC1032");
            var block = new DxfBlockTableRecord { Name = "GHB", Origin = new GeoPoint3(70, 0, 0) };
            db.BlockTable.Add(block);
            var reference = new DxfBlockReference(new GeoPoint3(100, 0, 0), block);
            db.ModelSpace.AppendEntity(reference);

            reference.ClipTo(new[] { new GeoPoint2(130, 10), new GeoPoint2(190, 40) }, GeoTransform3.Identity);

            Near(new[] { 1.0, 0, 0, -100, 0, 1, 0, 0, 0, 0, 1, 0 }, reference.SpatialFilter.InverseBlockTransform);
            Assert.Equal(100.0, reference.ClipOutline[0].X, 9);
        }
    }
}
