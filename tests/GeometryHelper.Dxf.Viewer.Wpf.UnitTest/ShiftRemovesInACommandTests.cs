using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>Shift held at "Select objects:", as AutoCAD's: a click, a box or a lasso takes out what it picks.</summary>
    public class ShiftRemovesInACommandTests
    {
        /// <summary>Two lines 10 long, A at y = 0 and B at y = 20, and ERASE asking "Select objects:".</summary>
        private static CadCommandSession Erasing(out CadDocument document, out string a, out string b)
        {
            var database = new DxfDatabase("AC1027");
            var first = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0));
            var second = new DxfLine(new GeoPoint3(0, 20, 0), new GeoPoint3(10, 20, 0));
            database.ModelSpace.AppendEntity(first);
            database.ModelSpace.AppendEntity(second);
            a = first.Handle;
            b = second.Handle;
            document = CadDocument.From(database, "two.dxf");
            var session = new CadCommandSession(document) { PickAperture = 1.0 };
            session.Start("ERASE");
            return session;
        }

        [Fact]
        public void AShiftBoxTakesOutWhatItHolds_AndAShiftClickWhatItPicks()
        {
            CadCommandSession session = Erasing(out CadDocument document, out string a, out string b);
            CadPicking.BoxDone(document, document.ModelSpace, session, new DisplayBox(-1, -1, 11, 21), false, false);
            Assert.Equal(new[] { a, b }, session.Gathering);

            CadPicking.BoxDone(document, document.ModelSpace, session, new DisplayBox(-1, -1, 11, 1), false, true);
            Assert.Equal(new[] { b }, session.Gathering);
            Assert.Equal("1 found, 1 removed, 1 total", session.History[session.History.Count - 1]);

            CadPicking.Click(document, document.ModelSpace, session, null, new GeoPoint2(5, 20), 1.0, true);
            Assert.Empty(session.Gathering);
        }

        [Fact]
        public void AShiftLassoTakesOutWhatItTakes()
        {
            CadCommandSession session = Erasing(out CadDocument document, out string a, out string b);
            session.FeedSelection(new[] { a, b });

            // Clockwise round line A: a window lasso.
            var ring = new[] { new GeoPoint2(-1, -1), new GeoPoint2(-1, 1), new GeoPoint2(11, 1), new GeoPoint2(11, -1) };
            CadPicking.LassoDone(document, document.ModelSpace, session, ring, true);

            Assert.Equal(new[] { b }, session.Gathering);
        }
    }
}
