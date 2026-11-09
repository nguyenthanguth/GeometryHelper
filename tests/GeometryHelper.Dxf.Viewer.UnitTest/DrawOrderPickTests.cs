using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// A pick among objects at the same distance takes the one drawn on top, as AutoCAD's does while SORTENTS holds its bit 1
    /// for object selection (its default 127 does); objects plainly nearer still come first.
    /// </summary>
    public class DrawOrderPickTests
    {
        private static readonly GeoVector3 Up = new GeoVector3(0, 0, 1);

        /// <summary>A line A level through the origin, a circle B of radius 10 about it and a line C upright through it, drawn in that order.</summary>
        private static CadDocument Three(out DxfEntity a, out DxfEntity b, out DxfEntity c)
        {
            var db = new DxfDatabase("AC1027");
            a = new DxfLine(new GeoPoint3(-20, 0, 0), new GeoPoint3(20, 0, 0));
            b = new DxfCircle(new GeoPoint3(0, 0, 0), Up, 10);
            c = new DxfLine(new GeoPoint3(0, -20, 0), new GeoPoint3(0, 20, 0));
            db.ModelSpace.AppendEntity(a);
            db.ModelSpace.AppendEntity(b);
            db.ModelSpace.AppendEntity(c);
            return CadDocument.From(db, "three.dxf");
        }

        [Fact]
        public void TwoLinesCrossingAtThePick_GiveTheOneDrawnOnTopFirst()
        {
            CadDocument document = Three(out DxfEntity a, out _, out DxfEntity c);

            Assert.Equal(new[] { c.Handle, a.Handle }, document.GetPickIndex(document.ModelSpace).Pick(0, 0, 1.0));
        }

        [Fact]
        public void DraworderBack_PutsTheObjectLastAmongThoseAtTheSameDistance()
        {
            CadDocument document = Three(out DxfEntity a, out _, out DxfEntity c);
            document.Database.ModelSpace.CreateDrawOrderTable().MoveToBottom(new[] { c });
            document.Regen();

            Assert.Equal(new[] { a.Handle, c.Handle }, document.GetPickIndex(document.ModelSpace).Pick(0, 0, 1.0));
        }

        [Fact]
        public void WithoutBitOneOfSortents_ThePickGoesByDistanceThenHandle_AsBefore()
        {
            CadDocument document = Three(out DxfEntity a, out _, out DxfEntity c);
            document.Database.SortEnts = 126;

            Assert.Equal(new[] { a.Handle, c.Handle }, document.GetPickIndex(document.ModelSpace).Pick(0, 0, 1.0));

            document.Database.SortEnts = 127;

            Assert.Equal(new[] { c.Handle, a.Handle }, document.GetPickIndex(document.ModelSpace).Pick(0, 0, 1.0));
        }

        [Fact]
        public void AnObjectPlainlyNearer_StillComesFirst_WhateverIsOnTop()
        {
            // At (0.6, 0) the line A runs under the pick and the line C on top of it lies 0.6 off, a fifth of the pickbox of 3.
            CadDocument document = Three(out DxfEntity a, out _, out DxfEntity c);

            Assert.Equal(new[] { a.Handle, c.Handle }, document.GetPickIndex(document.ModelSpace).Pick(0.6, 0, 3.0));

            // At (0.2, 0) the two lie within a tenth of the pickbox of each other: the one on top comes first.
            Assert.Equal(new[] { c.Handle, a.Handle }, document.GetPickIndex(document.ModelSpace).Pick(0.2, 0, 3.0));
        }
    }
}
