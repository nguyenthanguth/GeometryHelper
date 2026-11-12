using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The clip boundary of a block reference, the SPATIAL_FILTER of its extension dictionary that AutoCAD 2021's XCLIP writes
    /// (probes of accoreconsole: the numbers below are those it wrote): made by <see cref="DxfBlockReference.ClipTo"/>, read
    /// back, kept against the block when the reference is moved, switched on and off, given planes, copied and taken off.
    /// </summary>
    public class DxfSpatialFilterTests
    {
        /// <summary>A drawing of R2018 with a block GHB, a square 100 by 100 and a circle, its origin (10, 20), inserted once.</summary>
        private static DxfBlockReference Inserted(
            out DxfDatabase db,
            GeoPoint3 at,
            double scale = 1.0,
            double degrees = 0.0
        )
        {
            db = new DxfDatabase("AC1032");
            var block = new DxfBlockTableRecord { Name = "GHB", Origin = new GeoPoint3(10, 20, 0) };
            db.BlockTable.Add(block);
            var square = new DxfPolyline();
            square.AddVertexAt(0, new GeoPoint2(0, 0), 0, 0, 0);
            square.AddVertexAt(1, new GeoPoint2(100, 0), 0, 0, 0);
            square.AddVertexAt(2, new GeoPoint2(100, 100), 0, 0, 0);
            square.AddVertexAt(3, new GeoPoint2(0, 100), 0, 0, 0);
            square.Closed = true;
            block.AppendEntity(square);
            block.AppendEntity(new DxfCircle(new GeoPoint3(50, 50, 0), new GeoVector3(0, 0, 1), 20.0));
            var insert = new DxfBlockReference(at, block)
            {
                ScaleFactors = new GeoVector3(scale, scale, scale),
                Rotation = degrees * Math.PI / 180.0,
            };
            db.ModelSpace.AppendEntity(insert);
            return insert;
        }

        private static GeoPoint2[] Rectangle(double x0, double y0, double x1, double y1) =>
            new[] { new GeoPoint2(x0, y0), new GeoPoint2(x1, y1) };

        private static void Near(IReadOnlyList<double> expected, IReadOnlyList<double> actual)
        {
            Assert.Equal(expected.Count, actual.Count);

            for (int i = 0; i < expected.Count; i++)
            {
                Assert.Equal(expected[i], actual[i], 4);
            }
        }

        [Fact]
        public void ARectangleOnAnUnturnedReference_IsWrittenAsAutoCadWritesIt()
        {
            // AutoCAD 2021's XCLIP New, Rectangular, (1010, 10) to (1090, 90) on GHB at (1000, 0): two points, the normal Z, the
            // origin 0, on, no planes, the inverse of the placing (the shift by -1000) and the identity.
            DxfBlockReference insert = Inserted(out _, new GeoPoint3(1000, 0, 0));

            insert.ClipTo(Rectangle(1010, 10, 1090, 90), GeoTransform3.Identity);

            DxfSpatialFilter filter = insert.SpatialFilter;
            Assert.NotNull(filter);
            Assert.Equal(2, filter.Points.Count);
            Assert.Equal(1010.0, filter.Points[0].X);
            Assert.Equal(90.0, filter.Points[1].Y);
            Assert.Equal(1.0, filter.Normal.Z, 9);
            Assert.Equal(0.0, filter.Origin.X, 9);
            Assert.True(filter.IsEnabled);
            Assert.Null(filter.FrontClip);
            Assert.Null(filter.BackClip);
            Near(new[] { 1.0, 0, 0, -1000, 0, 1, 0, 0, 0, 0, 1, 0 }, filter.InverseBlockTransform);
            Near(new[] { 1.0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0 }, filter.WorldToClip);
        }

        [Fact]
        public void AClipOfAScaledTurnedReference_UnderATurnedUcs_KeepsAutoCadsMatrices()
        {
            // GHB (origin 10, 20) at (1000, 500), scaled by 2 and turned by 30 degrees, clipped under a UCS turned by 20 degrees
            // about its origin (26.4634, 73.4826): AutoCAD 2021 wrote the inverse of the placing, 0.433013, 0.25 and
            // -558.0127 in its first row, the world into the UCS, 0.9397, 0.342 and -50, and the UCS origin as the origin.
            DxfBlockReference insert = Inserted(out _, new GeoPoint3(1000, 500, 0), 2.0, 30.0);
            double turn = 20.0 * Math.PI / 180.0;
            GeoTransform3 ucsToWorld =
                GeoTransform3.Translation(new GeoVector3(26.4634224397553, 73.48256441343794, 0))
                * GeoTransform3.RotationZ(turn);

            insert.ClipTo(Rectangle(10, 10, 90, 90), ucsToWorld.Inverse());

            DxfSpatialFilter filter = insert.SpatialFilter;
            Near(
                new[] { 0.433013, 0.25, 0, -558.0127, -0.25, 0.433013, 0, 33.4936, 0, 0, 0.5, 0 },
                filter.InverseBlockTransform
            );
            Near(new[] { 0.9396926, 0.3420201, 0, -50, -0.3420201, 0.9396926, 0, -60, 0, 0, 1, 0 }, filter.WorldToClip);
            Assert.Equal(26.4634, filter.Origin.X, 3);
            Assert.Equal(73.4826, filter.Origin.Y, 3);
            Assert.Equal(1.0, filter.Normal.Z, 9);
        }

        [Fact]
        public void TheObjectsAreMadeAsAutoCadMakesThem_AndTheClassIsListedAndCounted()
        {
            // The extension dictionary holds ACAD_FILTER, which holds SPATIAL; each owned by the one above (330), both
            // dictionaries hard owners (280 of 1), the filter's class SPATIAL_FILTER listed once, counted 1.
            DxfBlockReference insert = Inserted(out DxfDatabase db, new GeoPoint3(1000, 0, 0));

            insert.ClipTo(Rectangle(1010, 10, 1090, 90), GeoTransform3.Identity);

            DxfDBDictionary extension = insert.ExtensionDictionary;
            Assert.Equal(insert.Handle, extension.OwnerHandle);
            var filters = Assert.IsType<DxfDBDictionary>(extension.GetAt("ACAD_FILTER"));
            Assert.True(extension.TreatElementsAsHard);
            Assert.True(filters.TreatElementsAsHard);
            Assert.Equal(extension.Handle, filters.OwnerHandle);
            DxfSpatialFilter filter = Assert.IsType<DxfSpatialFilter>(filters.GetAt("SPATIAL"));
            Assert.Equal(filters.Handle, filter.OwnerHandle);

            string text = db.ToText();
            // Once as the object and once as its class.
            Assert.Equal(2, text.Split(new[] { "SPATIAL_FILTER" }, StringSplitOptions.None).Length - 1);
            Assert.Contains("AcDbSpatialFilter", text);
        }

        [Fact]
        public void ASavedAndReadDrawing_GivesTheSameClip()
        {
            DxfBlockReference insert = Inserted(out DxfDatabase db, new GeoPoint3(1000, 0, 0));
            insert.ClipTo(
                new[] { new GeoPoint2(1010, 10), new GeoPoint2(1090, 10), new GeoPoint2(1050, 90) },
                GeoTransform3.Identity
            );

            DxfDatabase read = DxfDatabase.Parse(db.ToText());

            DxfBlockReference again = read.ModelSpace.OfType<DxfBlockReference>().Single();
            DxfSpatialFilter filter = again.SpatialFilter;
            Assert.NotNull(filter);
            Assert.Equal(3, filter.Points.Count);
            Assert.Equal(1050.0, filter.Points[2].X);
            Assert.Equal(90.0, filter.Points[2].Y);
            Near(new[] { 1.0, 0, 0, -1000, 0, 1, 0, 0, 0, 0, 1, 0 }, filter.InverseBlockTransform);
        }

        [Fact]
        public void TheOutline_IsInTheBlocksOwnCoordinates_AndStaysWhereverTheReferenceIsMoved()
        {
            // GHB (origin 10, 20) at (1000, 500): the rectangle (1000, 500) to (1040, 530) covers its lower left corner 40 by 30,
            // which is (10, 20) to (50, 50) in the block. Moved by (7, 11) and turned, the clip goes with the content.
            DxfBlockReference insert = Inserted(out _, new GeoPoint3(1000, 500, 0));
            insert.ClipTo(Rectangle(1000, 500, 1040, 530), GeoTransform3.Identity);

            void Is(IReadOnlyList<GeoPoint2> ring)
            {
                Assert.Equal(4, ring.Count);
                double[][] want =
                {
                    new[] { 10.0, 20.0 },
                    new[] { 50.0, 20.0 },
                    new[] { 50.0, 50.0 },
                    new[] { 10.0, 50.0 },
                };

                for (int i = 0; i < 4; i++)
                {
                    Assert.Equal(want[i][0], ring[i].X, 6);
                    Assert.Equal(want[i][1], ring[i].Y, 6);
                }
            }

            Is(insert.ClipOutline);
            insert.TransformBy(GeoTransform3.Translation(new GeoVector3(7, 11, 0)));
            Is(insert.ClipOutline);
            insert.TransformBy(GeoTransform3.RotationZ(0.7));
            Is(insert.ClipOutline);
        }

        [Fact]
        public void APolygonOutline_IsItsPointsInTheBlock()
        {
            DxfBlockReference insert = Inserted(out _, new GeoPoint3(1000, 500, 0), 2.0, 0.0);
            insert.ClipTo(
                new[] { new GeoPoint2(1000, 500), new GeoPoint2(1100, 500), new GeoPoint2(1050, 560) },
                GeoTransform3.Identity
            );

            IReadOnlyList<GeoPoint2> ring = insert.ClipOutline;

            // Scaled by 2 about the insertion point, which is the block's origin (10, 20): the points are 0, 50 and 25 along.
            Assert.Equal(3, ring.Count);
            Assert.Equal(10.0, ring[0].X, 6);
            Assert.Equal(60.0, ring[1].X, 6);
            Assert.Equal(35.0, ring[2].X, 6);
            Assert.Equal(50.0, ring[2].Y, 6);
        }

        [Fact]
        public void ClippingAgain_ReplacesTheBoundary()
        {
            DxfBlockReference insert = Inserted(out _, new GeoPoint3(1000, 0, 0));
            insert.ClipTo(Rectangle(1010, 10, 1090, 90), GeoTransform3.Identity);
            string first = insert.SpatialFilter.Handle;

            insert.ClipTo(Rectangle(1020, 20, 1080, 80), GeoTransform3.Identity);

            Assert.NotEqual(first, insert.SpatialFilter.Handle);
            Assert.Equal(1020.0, insert.SpatialFilter.Points[0].X);
            Assert.Single(insert.ExtensionDictionary);
        }

        [Fact]
        public void TheClipTakenOff_LeavesTheExtensionDictionaryEmpty_AsAutoCadDoes()
        {
            DxfBlockReference insert = Inserted(out _, new GeoPoint3(1000, 0, 0));
            insert.ClipTo(Rectangle(1010, 10, 1090, 90), GeoTransform3.Identity);
            DxfSpatialFilter filter = insert.SpatialFilter;

            insert.RemoveClip();

            Assert.Null(insert.SpatialFilter);
            Assert.True(filter.IsErased);
            Assert.NotNull(insert.ExtensionDictionary);
            Assert.Equal(0, insert.ExtensionDictionary.Count);
            insert.RemoveClip();
        }

        [Fact]
        public void TheClipSwitchedOffAndOn_ChangesGroup71Alone()
        {
            DxfBlockReference insert = Inserted(out _, new GeoPoint3(1000, 0, 0));
            insert.ClipTo(Rectangle(1010, 10, 1090, 90), GeoTransform3.Identity);
            DxfSpatialFilter filter = insert.SpatialFilter;
            double[] inverse = filter.InverseBlockTransform.ToArray();

            filter.IsEnabled = false;
            Assert.False(filter.IsEnabled);
            Assert.Equal(2, filter.Points.Count);
            Near(inverse, filter.InverseBlockTransform);

            filter.IsEnabled = true;
            Assert.True(filter.IsEnabled);
        }

        [Fact]
        public void TheClippingPlanes_AreWrittenAsAutoCadWritesThem_AndTakenAway()
        {
            // AutoCAD 2021 wrote a front plane at 5 as 72 of 1 then 40 of 5, and a back plane at -5 as 73 of 1 then 41 of -5.
            DxfBlockReference insert = Inserted(out DxfDatabase db, new GeoPoint3(1000, 0, 0));
            insert.ClipTo(Rectangle(1010, 10, 1090, 90), GeoTransform3.Identity);
            DxfSpatialFilter filter = insert.SpatialFilter;

            filter.FrontClip = 5.0;
            filter.BackClip = -5.0;

            Assert.Equal(5.0, filter.FrontClip);
            Assert.Equal(-5.0, filter.BackClip);
            Assert.Equal(2, filter.Points.Count);
            Near(new[] { 1.0, 0, 0, -1000, 0, 1, 0, 0, 0, 0, 1, 0 }, filter.InverseBlockTransform);
            DxfSpatialFilter again = DxfDatabase
                .Parse(db.ToText())
                .ModelSpace.OfType<DxfBlockReference>()
                .Single()
                .SpatialFilter;
            Assert.Equal(5.0, again.FrontClip);
            Assert.Equal(-5.0, again.BackClip);

            filter.FrontClip = null;
            filter.BackClip = null;
            Assert.Null(filter.FrontClip);
            Assert.Null(filter.BackClip);
            Near(new[] { 1.0, 0, 0, -1000, 0, 1, 0, 0, 0, 0, 1, 0 }, filter.InverseBlockTransform);
        }

        [Fact]
        public void ABoundaryOfOnePoint_NoPoints_ANullTransform_OrAFlatOne_IsRefused()
        {
            DxfBlockReference insert = Inserted(out _, new GeoPoint3(1000, 0, 0));

            Assert.Throws<ArgumentNullException>(() => insert.ClipTo(null, GeoTransform3.Identity));
            Assert.Throws<ArgumentNullException>(() => insert.ClipTo(Rectangle(0, 0, 1, 1), null));
            Assert.Throws<ArgumentException>(() =>
                insert.ClipTo(new[] { new GeoPoint2(0, 0) }, GeoTransform3.Identity)
            );
            Assert.Throws<ArgumentException>(() =>
                insert.ClipTo(Rectangle(0, 0, double.NaN, 1), GeoTransform3.Identity)
            );
            Assert.Throws<ArgumentException>(() =>
                insert.ClipTo(Rectangle(0, 0, 1, 1), GeoTransform3.Scaling(1, 1, 0))
            );
            Assert.Null(insert.SpatialFilter);
        }

        [Fact]
        public void AReferenceNotClipped_HasNoFilterAndNoOutline()
        {
            DxfBlockReference insert = Inserted(out _, new GeoPoint3(1000, 0, 0));

            Assert.Null(insert.SpatialFilter);
            Assert.Null(insert.ClipOutline);
        }
    }
}
