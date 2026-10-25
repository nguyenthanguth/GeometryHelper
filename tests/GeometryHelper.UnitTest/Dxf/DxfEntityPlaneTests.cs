using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The plane an entity lies in, and where two entities, or their boxes, meet, as AutoCAD's <c>GetPlane</c>,
    /// <c>IsPlanar</c>, <c>IntersectWith</c> and <c>BoundingBoxIntersectWith</c> give them, worked out by the library's shapes
    /// to <see cref="Tolerance.Default"/>.
    /// </summary>
    public class DxfEntityPlaneTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        [Fact]
        public void ACircleALineAndAPolyline_LieInThePlaneOfTheirExtrusion()
        {
            var circle = new DxfCircle(new GeoPoint3(10, 20, 5), new GeoVector3(0, 0, 1), 3);
            var line = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0));
            var flat = new DxfPolyline3d(DxfPoly3dType.SimplePoly, new[] { new GeoPoint3(0, 0, 2), new GeoPoint3(10, 0, 2), new GeoPoint3(0, 10, 2) }, false);

            Assert.True(circle.IsPlanar);
            Assert.True(new GeoPlane3(new GeoPoint3(0, 0, 5), new GeoVector3(0, 0, 1)).IsEqualTo(circle.GetPlane(), Tol));
            Assert.True(line.IsPlanar);
            Assert.True(line.GetPlane().Normal.IsParallelTo(new GeoVector3(0, 0, 1), Tol));
            Assert.True(flat.IsPlanar);
            Assert.True(flat.GetPlane().Normal.IsParallelTo(new GeoVector3(0, 0, 1), Tol));
            Assert.Equal(2.0, flat.GetPlane().Origin.Z, 12);

            // A line standing up along its extrusion lies in a plane through it all the same.
            var up = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(0, 0, 10));
            Assert.True(up.GetPlane().ContainsAll(new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(0, 0, 10) }, Tol));
        }

        [Fact]
        public void APolylineWithACornerOffThePlaneOfTheOthers_LiesInNone()
        {
            var bent = new DxfPolyline3d(DxfPoly3dType.SimplePoly, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(10, 10, 0), new GeoPoint3(0, 10, 0.002) }, false);
            var almost = new DxfPolyline3d(DxfPoly3dType.SimplePoly, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(10, 10, 0), new GeoPoint3(0, 10, 0.0005) }, false);

            Assert.False(bent.IsPlanar);
            Assert.Throws<InvalidOperationException>(() => bent.GetPlane());
            Assert.True(almost.IsPlanar);
            Assert.Throws<InvalidOperationException>(() => new DxfLine(new GeoPoint3(1, 1, 1), new GeoPoint3(1, 1, 1)).GetPlane());
        }

        [Fact]
        public void ALineAndACircle_MeetWhereTheirShapesMeet_AndExtendedWhereTheirLineAndCircleMeet()
        {
            var line = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0));
            var circle = new DxfCircle(new GeoPoint3(10, 0, 0), new GeoVector3(0, 0, 1), 3);

            Meet(new[] { new GeoPoint3(7, 0, 0) }, line.IntersectWith(circle, DxfIntersect.OnBothOperands));
            Meet(new[] { new GeoPoint3(7, 0, 0), new GeoPoint3(13, 0, 0) }, line.IntersectWith(circle, DxfIntersect.ExtendThis));
            Meet(new[] { new GeoPoint3(7, 0, 0) }, circle.IntersectWith(line, DxfIntersect.ExtendThis));
            Meet(new[] { new GeoPoint3(7, 0, 0), new GeoPoint3(13, 0, 0) }, circle.IntersectWith(line, DxfIntersect.ExtendArgument));

            // A quarter arc of radius 5 and a short line at y = 1 meet only when both run on: at x = -sqrt(24) and sqrt(24).
            var arc = new DxfArc(new GeoPoint3(0, 0, 0), 5, 0, Math.PI / 2);
            var shortLine = new DxfLine(new GeoPoint3(-10, 1, 0), new GeoPoint3(-6, 1, 0));
            Assert.Empty(arc.IntersectWith(shortLine, DxfIntersect.OnBothOperands));
            Assert.Empty(arc.IntersectWith(shortLine, DxfIntersect.ExtendThis));
            Meet(new[] { new GeoPoint3(-Math.Sqrt(24), 1, 0), new GeoPoint3(Math.Sqrt(24), 1, 0) }, arc.IntersectWith(shortLine, DxfIntersect.ExtendBoth));
            Assert.Throws<ArgumentNullException>(() => arc.IntersectWith(null, DxfIntersect.ExtendBoth));
            Assert.Throws<ArgumentOutOfRangeException>(() => arc.IntersectWith(line, (DxfIntersect)9));
        }

        [Fact]
        public void TheBoxesOfTwoLines_MeetWhereTheirOutlinesCross()
        {
            var a = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 10, 0));
            var b = new DxfLine(new GeoPoint3(5, 5, 0), new GeoPoint3(15, 15, 0));
            var far = new DxfLine(new GeoPoint3(50, 50, 0), new GeoPoint3(60, 60, 0));

            Meet(new[] { new GeoPoint3(5, 10, 0), new GeoPoint3(10, 5, 0) }, a.BoundingBoxIntersectWith(b, DxfIntersect.OnBothOperands));
            Assert.Empty(a.BoundingBoxIntersectWith(far, DxfIntersect.ExtendBoth));
            Assert.Throws<InvalidOperationException>(() => a.BoundingBoxIntersectWith(new DxfLine(new GeoPoint3(1, 1, 1), new GeoPoint3(1, 1, 1)), DxfIntersect.OnBothOperands));
        }

        [Fact]
        public void AClosedOutlineAndALineInItsPlane_MeetWhereTheLineCrossesItsEdges_NotInsideIt()
        {
            // A square 4 by 4 about the origin, and a line along the x axis through it: they meet at x = -2 and x = 2.
            var square = new[] { new GeoPoint3(-2, -2, 0), new GeoPoint3(2, -2, 0), new GeoPoint3(2, 2, 0), new GeoPoint3(-2, 2, 0) };
            var along = new DxfLine(new GeoPoint3(-10, 0, 0), new GeoPoint3(10, 0, 0));
            var through = new DxfLine(new GeoPoint3(0, 0, -5), new GeoPoint3(0, 0, 5));
            var light = new DxfPolyline(0);

            for (int i = 0; i < 4; i++)
            {
                light.AddVertexAt(i, new GeoPoint2(square[i].X, square[i].Y), 0, 0, 0);
            }

            light.Closed = true;
            var outlines = new DxfEntity[]
            {
                new DxfPolyline2d(DxfPoly2dType.SimplePoly, square, 0, true, 0, 0, new double[] { 0, 0, 0, 0 }),
                light,
                new DxfPolyline3d(DxfPoly3dType.SimplePoly, square, true),
                new DxfFace(square[0], square[1], square[2], square[3], true, true, true, true),
                new DxfSolid(square[0], square[1], square[3], square[2]),
            };
            var ends = new[] { new GeoPoint3(-2, 0, 0), new GeoPoint3(2, 0, 0) };

            foreach (DxfEntity outline in outlines)
            {
                Meet(ends, outline.IntersectWith(along, DxfIntersect.OnBothOperands));
                Meet(ends, along.IntersectWith(outline, DxfIntersect.OnBothOperands));

                // A line standing through the inside crosses no edge; a circle inside touches none.
                Assert.Empty(outline.IntersectWith(through, DxfIntersect.OnBothOperands));
                Assert.Empty(outline.IntersectWith(new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 1), DxfIntersect.OnBothOperands));
            }
        }

        [Fact]
        public void APointMeetsWhatItLiesOn_AnEllipseMeetsA3dPolylineByItsSegments_AndATextMeetsNothingItCanTell()
        {
            var line = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0));
            var on = new DxfDBPoint(new GeoPoint3(4, 0, 0));
            var off = new DxfDBPoint(new GeoPoint3(4, 0.01, 0));

            Meet(new[] { new GeoPoint3(4, 0, 0) }, on.IntersectWith(line, DxfIntersect.OnBothOperands));
            Meet(new[] { new GeoPoint3(4, 0, 0) }, line.IntersectWith(on, DxfIntersect.OnBothOperands));
            Assert.Empty(off.IntersectWith(line, DxfIntersect.OnBothOperands));
            Meet(new[] { new GeoPoint3(4, 0, 0) }, on.IntersectWith(new DxfDBPoint(new GeoPoint3(4, 0.0001, 0)), DxfIntersect.OnBothOperands));

            // An ellipse 10 by 5 about the origin and a 3D polyline up the y axis, then up: they meet at y = -2.5 and 2.5.
            var ellipse = new DxfEllipse(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), new GeoVector3(5, 0, 0), 0.5, 0, 2 * Math.PI);
            var bent = new DxfPolyline3d(DxfPoly3dType.SimplePoly, new[] { new GeoPoint3(0, -10, 0), new GeoPoint3(0, 10, 0), new GeoPoint3(0, 10, 5) }, false);
            Meet(new[] { new GeoPoint3(0, -2.5, 0), new GeoPoint3(0, 2.5, 0) }, ellipse.IntersectWith(bent, DxfIntersect.OnBothOperands));
            Meet(new[] { new GeoPoint3(0, -2.5, 0), new GeoPoint3(0, 2.5, 0) }, bent.IntersectWith(ellipse, DxfIntersect.OnBothOperands));

            var text = new DxfDBText { TextString = "B-12" };
            Assert.Throws<NotSupportedException>(() => text.IntersectWith(line, DxfIntersect.OnBothOperands));
            Assert.Throws<NotSupportedException>(() => line.IntersectWith(text, DxfIntersect.OnBothOperands));
        }

        /// <summary>Checks the points met are the points expected, in any order, each within <see cref="Tol"/>.</summary>
        private static void Meet(GeoPoint3[] expected, IEnumerable<GeoPoint3> actual)
        {
            List<GeoPoint3> left = actual.ToList();
            Assert.Equal(expected.Length, left.Count);

            foreach (GeoPoint3 point in expected)
            {
                int at = left.FindIndex(p => p.IsEqualTo(point, Tol));
                Assert.True(at >= 0, point + " is not among " + string.Join(" ", left));
                left.RemoveAt(at);
            }
        }
    }
}
