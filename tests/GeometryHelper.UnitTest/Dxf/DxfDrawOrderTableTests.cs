using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The draw order of a block (SORTENTSTABLE), as AutoCAD's DrawOrderTable: made under ACAD_SORTENTS, the sort handles
    /// given out again in the new order, read back from the file, and an entity erased left out.
    /// </summary>
    public class DxfDrawOrderTableTests
    {
        private static string[] Handles(DxfDrawOrderTable table) =>
            table.GetFullDrawOrder().Select(e => e.Handle).ToArray();

        [Fact]
        public void ABlockGivenADrawOrder_ListsOnlyTheEntitiesMoved_WithTheSortHandlesTheyShare()
        {
            var db = new DxfDatabase("AC1027");
            var lines = Enumerable
                .Range(0, 3)
                .Select(i => new DxfLine(new GeoPoint3(0, i, 0), new GeoPoint3(10, i, 0)))
                .ToArray();

            foreach (DxfLine line in lines)
            {
                db.ModelSpace.AppendEntity(line);
            }

            string a = lines[0].Handle,
                b = lines[1].Handle,
                c = lines[2].Handle;
            Assert.Null(db.ModelSpace.DrawOrderTable);

            DxfDrawOrderTable table = db.ModelSpace.CreateDrawOrderTable();
            Assert.Same(db.ModelSpace, table.Block);
            Assert.Equal(new[] { a, b, c }, Handles(table));

            table.MoveToBottom(new[] { lines[2] });
            Assert.Equal(new[] { c, a, b }, Handles(table));

            // The three share their three handles: c takes a's, a takes b's, b takes c's.
            string text = db.ToText();
            Assert.Contains("331\n" + c + "\n  5\n" + a + "\n", text.Replace("\r\n", "\n"));

            table.MoveAbove(new[] { lines[0] }, lines[1]);
            Assert.Equal(new[] { c, b, a }, Handles(table));
            table.MoveBelow(new[] { lines[2] }, lines[0]);
            Assert.Equal(new[] { b, c, a }, Handles(table));
            Assert.Throws<ArgumentException>(() => table.MoveAbove(new[] { lines[0] }, lines[0]));

            DxfDatabase again = DxfDatabase.Parse(db.ToText());
            Assert.Equal(new[] { b, c, a }, Handles(again.ModelSpace.DrawOrderTable));

            lines[2].Erase();
            Assert.Equal(new[] { b, a }, Handles(db.ModelSpace.DrawOrderTable));
            Assert.Equal(new[] { b, a }, Handles(DxfDatabase.Parse(db.ToText()).ModelSpace.DrawOrderTable));
        }
    }
}
