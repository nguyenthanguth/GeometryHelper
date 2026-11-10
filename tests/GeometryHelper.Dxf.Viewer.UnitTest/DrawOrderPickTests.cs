using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// A pick takes the object drawn on top among all those within the pickbox, as AutoCAD's does while SORTENTS holds its bit
    /// 1 for object selection (its default 127 does), and the one drawn first while it does not, however near the others lie.
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
        public void WithoutBitOneOfSortents_ThePickGoesByTheOrderTheObjectsWereDrawnIn()
        {
            CadDocument document = Three(out DxfEntity a, out _, out DxfEntity c);
            document.Database.SortEnts = 126;

            Assert.Equal(new[] { a.Handle, c.Handle }, document.GetPickIndex(document.ModelSpace).Pick(0, 0, 1.0));

            document.Database.SortEnts = 127;

            Assert.Equal(new[] { c.Handle, a.Handle }, document.GetPickIndex(document.ModelSpace).Pick(0, 0, 1.0));
        }

        [Fact]
        public void TheObjectOnTop_ComesFirstAnywhereInThePickbox_AsAutoCad()
        {
            // AutoCAD 2021, (ssget pt) on two level lines, the one drawn later 0.05 to 0.9 of PICKBOX above the point and the
            // other through it: the later one, on top, is picked every time; 1.1 of PICKBOX above, outside the box, the other.
            // At (2.7, 0) the line A runs under the pick and the line C on top of it lies 2.7 off, within the pickbox of 3.
            CadDocument document = Three(out DxfEntity a, out _, out DxfEntity c);

            Assert.Equal(new[] { c.Handle, a.Handle }, document.GetPickIndex(document.ModelSpace).Pick(2.7, 0, 3.0));
            Assert.Equal(new[] { a.Handle }, document.GetPickIndex(document.ModelSpace).Pick(3.3, 0, 3.0));
        }

        [Fact]
        public void WithoutBitOneOfSortents_TheObjectDrawnFirst_ComesFirstAnywhereInThePickbox_AsAutoCad()
        {
            // AutoCAD 2021 under SORTENTS 0, (ssget pt) on two level lines, the one drawn later through the point and the one
            // drawn first 0.1 or 0.5 of PICKBOX above it: the one drawn first is picked. At (0, 2.7) the line C runs under the
            // pick and the line A, drawn first, lies 2.7 off.
            CadDocument document = Three(out DxfEntity a, out _, out DxfEntity c);
            document.Database.SortEnts = 126;

            Assert.Equal(new[] { a.Handle, c.Handle }, document.GetPickIndex(document.ModelSpace).Pick(0, 2.7, 3.0));
        }
    }
}
