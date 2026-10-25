using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The three polylines cut, offset and run on as AutoCAD's curves are, and a polyline converted to a polyline of the plane and
    /// back, its place in the drawing taken with its handle.
    /// </summary>
    public class DxfPolylineEditingTests
    {
        private static readonly Tolerance Tight = new Tolerance(1E-9, 1E-9);

        /// <summary>The polyline (0, 0), (10, 0), (10, 10) with a bulge of 1 on its second vertex: a line and a half circle.</summary>
        private static DxfPolyline LineAndHalfCircle()
        {
            var polyline = new DxfPolyline();
            polyline.AddVertexAt(0, new GeoPoint2(0, 0), 0, 0, 0);
            polyline.AddVertexAt(1, new GeoPoint2(10, 0), 1, 0, 0);
            polyline.AddVertexAt(2, new GeoPoint2(10, 10), 0, 0, 0);
            return polyline;
        }

        /// <summary>The square of side 10 from the origin, counter-clockwise, or clockwise.</summary>
        private static DxfPolyline Square(bool clockwise)
        {
            var polyline = new DxfPolyline();
            var points = new[] { new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10), new GeoPoint2(0, 10) };

            foreach (GeoPoint2 point in clockwise ? points.Reverse() : points)
            {
                polyline.AddVertexAt(polyline.NumberOfVertices, point, 0, 0, 0);
            }

            polyline.Closed = true;
            return polyline;
        }

        [Fact]
        public void APolylineCut_KeepsThePartsOfItsArcs_AndItsWidths()
        {
            // Cut at 1.5: (0, 0), (10, 0), (15, 5) with a bulge of tan(pi / 8), and (15, 5), (10, 10) with the same.
            DxfPolyline polyline = LineAndHalfCircle();
            polyline.SetStartWidthAt(1, 2.0);
            polyline.SetEndWidthAt(1, 4.0);
            IReadOnlyList<DxfCurve> pieces = polyline.GetSplitCurves(new[] { 1.5 });

            Assert.Equal(2, pieces.Count);
            var first = (DxfPolyline)pieces[0];
            var second = (DxfPolyline)pieces[1];
            Assert.Equal(3, first.NumberOfVertices);
            Assert.True(first.GetPoint3dAt(2).IsEqualTo(new GeoPoint3(15, 5, 0), Tight));
            Assert.Equal(Math.Tan(Math.PI / 8), first.GetBulgeAt(1), 12);
            Assert.Equal(3.0, first.GetEndWidthAt(1), 12);
            Assert.Equal(2, second.NumberOfVertices);
            Assert.Equal(Math.Tan(Math.PI / 8), second.GetBulgeAt(0), 12);
            Assert.Equal(3.0, second.GetStartWidthAt(0), 12);
            Assert.True(second.EndPoint.IsEqualTo(new GeoPoint3(10, 10, 0), Tight));
            Assert.Equal(polyline.Length, first.Length + second.Length, 9);
            Assert.Single(polyline.GetSplitCurves(new[] { 0.0, 2.0 }));

            // A closed square cut at 0.5 and 2.5: from each to the next, the second round its first vertex.
            IReadOnlyList<DxfCurve> halves = Square(false).GetSplitCurves(new[] { 2.5, 0.5 });
            Assert.Equal(2, halves.Count);
            Assert.Equal(20.0, ((DxfPolyline)halves[0]).Length, 12);
            Assert.True(halves[1].StartPoint.IsEqualTo(new GeoPoint3(5, 10, 0), Tight));
            Assert.True(halves[1].EndPoint.IsEqualTo(new GeoPoint3(5, 0, 0), Tight));
            Assert.False(((DxfPolyline)halves[1]).Closed);
            Assert.Single(Square(false).GetSplitCurves(new[] { 1.0 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => polyline.GetSplitCurves(new[] { 3.0 }));
        }

        [Fact]
        public void APolylineOffset_GoesToTheRightOfItsWay_SoACounterClockwiseOneGrows()
        {
            var grown = (DxfPolyline)Assert.Single(Square(false).GetOffsetCurves(1.0));
            var shrunk = (DxfPolyline)Assert.Single(Square(true).GetOffsetCurves(1.0));

            Assert.Equal(144.0, grown.Area, 9);
            Assert.True(grown.Closed);
            Assert.Equal(64.0, shrunk.Area, 9);
            Assert.Equal(64.0, ((DxfPolyline)Assert.Single(Square(false).GetOffsetCurvesGivenPlaneNormal(new GeoVector3(0, 0, -1), 1.0))).Area, 9);

            // The half circle of radius 5 runs counter-clockwise: to its right it takes the radius 6, as an arc would.
            DxfPolyline open = LineAndHalfCircle();
            var right = (DxfPolyline)Assert.Single(open.GetOffsetCurves(1.0));
            Assert.Contains(Enumerable.Range(0, right.NumberOfVertices), i => right.GetSegmentType(i) == DxfSegmentType.Arc && Math.Abs(right.GetArcSegment2dAt(i).Radius - 6.0) < 1E-9);
            Assert.True(right.StartPoint.IsEqualTo(new GeoPoint3(0, -1, 0), Tight));
            Assert.Throws<ArgumentException>(() => open.GetOffsetCurvesGivenPlaneNormal(new GeoVector3(1, 0, 0), 1.0));
        }

        [Fact]
        public void APolylineRunOn_MovesAnEndAlongItsEndSegment_ALineOrAnArc()
        {
            var polyline = new DxfPolyline();
            polyline.AddVertexAt(0, new GeoPoint2(0, 0), 0, 0, 0);
            polyline.AddVertexAt(1, new GeoPoint2(10, 0), 0, 0, 0);

            polyline.Extend(1.5);
            Assert.True(polyline.EndPoint.IsEqualTo(new GeoPoint3(15, 0, 0), Tight));
            polyline.Extend(-0.5);
            Assert.True(polyline.StartPoint.IsEqualTo(new GeoPoint3(-7.5, 0, 0), Tight));
            polyline.Extend(false, new GeoPoint3(30, 0, 0));
            Assert.True(polyline.EndPoint.IsEqualTo(new GeoPoint3(30, 0, 0), Tight));
            Assert.Throws<ArgumentException>(() => polyline.Extend(false, new GeoPoint3(30, 1, 0)));
            Assert.Throws<ArgumentOutOfRangeException>(() => polyline.Extend(0.5));

            // The half circle run on by half its angle: a three-quarter circle, its end at (5, 10) round to (0, 5).
            DxfPolyline arc = LineAndHalfCircle();
            arc.Extend(2.5);
            Assert.True(arc.EndPoint.IsEqualTo(new GeoPoint3(5, 5, 0), Tight), arc.EndPoint.ToString());
            Assert.Equal(Math.Tan(3 * Math.PI / 8), arc.GetBulgeAt(1), 12);
            var further = new GeoPoint3(10 - 5 * Math.Sqrt(0.5), 5 - 5 * Math.Sqrt(0.5), 0);
            arc.Extend(false, further);
            Assert.True(arc.EndPoint.IsEqualTo(further, Tight));
            Assert.Equal(Math.Tan(7 * Math.PI / 16), arc.GetBulgeAt(1), 12);
            Assert.Throws<ArgumentException>(() => arc.Extend(false, new GeoPoint3(15, 5, 0)));
            Assert.Throws<InvalidOperationException>(() => Square(false).Extend(5.0));
        }

        [Fact]
        public void APointOnThePolylineItself_IsRefusedAsAnEndToRunOnTo_NotFoldedBack()
        {
            // (0, 0), (10, 0), (20, 0): (15, 0) is on the polyline, not past its start, and would fold the start back over it.
            var light = new DxfPolyline();
            light.AddVertexAt(0, new GeoPoint2(0, 0), 0, 0, 0);
            light.AddVertexAt(1, new GeoPoint2(10, 0), 0, 0, 0);
            light.AddVertexAt(2, new GeoPoint2(20, 0), 0, 0, 0);
            var plane = new DxfPolyline2d(DxfPoly2dType.SimplePoly, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(20, 0, 0) }, 0, false, 0, 0, null);
            var space = new DxfPolyline3d(DxfPoly3dType.SimplePoly, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(20, 0, 0) }, false);

            foreach (DxfCurve polyline in new DxfCurve[] { light, plane, space })
            {
                Assert.Throws<ArgumentException>(() => polyline.Extend(true, new GeoPoint3(15, 0, 0)));
                Assert.Throws<ArgumentException>(() => polyline.Extend(false, new GeoPoint3(15, 0, 0)));
                Assert.Throws<ArgumentException>(() => polyline.Extend(true, new GeoPoint3(5, 0, 0)));
                Assert.Throws<ArgumentException>(() => polyline.Extend(false, new GeoPoint3(20, 0, 0)));
                polyline.Extend(true, new GeoPoint3(-5, 0, 0));
                polyline.Extend(false, new GeoPoint3(25, 0, 0));
                Assert.True(polyline.StartPoint.IsEqualTo(new GeoPoint3(-5, 0, 0), Tight));
                Assert.True(polyline.EndPoint.IsEqualTo(new GeoPoint3(25, 0, 0), Tight));
            }
        }

        [Fact]
        public void AnArcAndAPolylineOfThatArc_OffsetToOppositeSides_UnderTheNormalTurnedOver()
        {
            // The half circle of radius 5 counter-clockwise about +z: about -z the arc still grows to 6, as AutoCAD offsets it by
            // its radius, and the polyline of it goes to its right seen from -z, inwards, to 4.
            var arc = new DxfArc(new GeoPoint3(10, 5, 0), new GeoVector3(0, 0, 1), 5, -Math.PI / 2, Math.PI / 2);
            var polyline = new DxfPolyline();
            polyline.AddVertexAt(0, new GeoPoint2(10, 0), 1, 0, 0);
            polyline.AddVertexAt(1, new GeoPoint2(10, 10), 0, 0, 0);
            var down = new GeoVector3(0, 0, -1);

            Assert.Equal(6.0, ((DxfArc)Assert.Single(arc.GetOffsetCurvesGivenPlaneNormal(down, 1.0))).Radius, 12);
            var inner = (DxfPolyline)Assert.Single(polyline.GetOffsetCurvesGivenPlaneNormal(down, 1.0));
            Assert.Equal(4.0, inner.GetArcSegment2dAt(0).Radius, 9);
            Assert.Equal(6.0, ((DxfArc)Assert.Single(arc.GetOffsetCurves(1.0))).Radius, 12);
            Assert.Equal(6.0, ((DxfPolyline)Assert.Single(polyline.GetOffsetCurves(1.0))).GetArcSegment2dAt(0).Radius, 9);
        }

        [Fact]
        public void APolylineRunOnInADrawing_IsItsRecordWrittenOnce_TheOtherVerticesAsTheyWere()
        {
            string text = DxfFileEzdxfDrawings.R2000;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfPolyline closed = ByHandle<DxfPolyline>(db, "6C");
            closed.Closed = false;

            closed.Extend(3.5);

            string saved = Encoding.UTF8.GetString(Saved(db));
            OnlyTheseChanged(text, saved, "6C");
            DxfPolyline again = ByHandle<DxfPolyline>(DxfDatabase.Parse(saved), "6C");
            Assert.True(again.EndPoint.IsEqualTo(new GeoPoint3(-5, 10, 0), Tight), again.EndPoint.ToString());
            Assert.Equal(-0.25, again.GetBulgeAt(1));
        }

        [Fact]
        public void APolylineOfThePlane_IsCutOffsetAndRunOn_AsALightweightOne_ItsVerticesKeepingTheirHandles()
        {
            string text = DxfFileEzdxfDrawings.R2000;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfPolyline2d triangle = ByHandle<DxfPolyline2d>(db, "49");

            IReadOnlyList<DxfCurve> pieces = triangle.GetSplitCurves(new[] { 0.5, 1.5 });
            Assert.Equal(2, pieces.Count);
            Assert.All(pieces, p => Assert.IsType<DxfPolyline2d>(p));
            Assert.True(pieces[0].StartPoint.IsEqualTo(new GeoPoint3(2.5, 0, 0), Tight));
            Assert.True(pieces[1].EndPoint.IsEqualTo(new GeoPoint3(2.5, 0, 0), Tight));
            Assert.Equal("Wand€Außen", pieces[0].Layer);

            var grown = (DxfPolyline2d)Assert.Single(triangle.GetOffsetCurves(1.0));
            Assert.True(grown.Area > triangle.Area);
            Assert.True(grown.Closed);

            triangle.Closed = false;
            string[] handles = triangle.Select(v => v.Handle).ToArray();
            triangle.Extend(2.5);
            Assert.True(triangle.EndPoint.IsEqualTo(new GeoPoint3(5, 7.5, 0), Tight));
            Assert.Equal(handles, triangle.Select(v => v.Handle).ToArray());

            string saved = Encoding.UTF8.GetString(Saved(db));
            OnlyTheseChanged(text, saved, "49", "4D");
            Assert.True(ByHandle<DxfPolyline2d>(DxfDatabase.Parse(saved), "49").EndPoint.IsEqualTo(new GeoPoint3(5, 7.5, 0), Tight));
        }

        [Fact]
        public void APolylineInSpace_IsCutRunOnAndOffsetInItsPlane()
        {
            // (0, 0, 0), (3, 4, 0), (3, 4, 12) cut at 1.5: the second piece from (3, 4, 6).
            var polyline = new DxfPolyline3d(DxfPoly3dType.SimplePoly, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(3, 4, 0), new GeoPoint3(3, 4, 12) }, false);
            IReadOnlyList<DxfCurve> pieces = polyline.GetSplitCurves(new[] { 1.5 });

            Assert.Equal(2, pieces.Count);
            Assert.True(pieces[1].StartPoint.IsEqualTo(new GeoPoint3(3, 4, 6), Tight));
            Assert.Equal(3, ((DxfPolyline3d)pieces[0]).Count());

            polyline.Extend(2.5);
            Assert.True(polyline.EndPoint.IsEqualTo(new GeoPoint3(3, 4, 18), Tight));
            polyline.Extend(true, new GeoPoint3(-3, -4, 0));
            Assert.True(polyline.StartPoint.IsEqualTo(new GeoPoint3(-3, -4, 0), Tight));
            Assert.Throws<InvalidOperationException>(() => new DxfPolyline3d(DxfPoly3dType.SimplePoly, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 1), new GeoPoint3(1, 1, 0), new GeoPoint3(0, 1, 1) }, false).GetOffsetCurves(1.0));

            // A square standing in the plane x = 2, counter-clockwise seen from +x: grown by 1 about (1, 0, 0).
            var standing = new DxfPolyline3d(DxfPoly3dType.SimplePoly, new[] { new GeoPoint3(2, 0, 0), new GeoPoint3(2, 10, 0), new GeoPoint3(2, 10, 10), new GeoPoint3(2, 0, 10) }, true);
            var grown = (DxfPolyline3d)Assert.Single(standing.GetOffsetCurves(1.0));
            Assert.Equal(4, grown.Count());
            Assert.All(grown, v => Assert.Equal(2.0, v.Position.X, 9));
            Assert.Equal(48.0, grown.Length, 9);
            var shrunk = (DxfPolyline3d)Assert.Single(standing.GetOffsetCurvesGivenPlaneNormal(new GeoVector3(-1, 0, 0), 1.0));
            Assert.Equal(32.0, shrunk.Length, 9);
        }

        [Fact]
        public void APolylineConvertedTakingThePlace_KeepsItsHandle_AndTheOtherIsErased()
        {
            foreach (string text in new[] { DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018 })
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfPolyline2d old = ByHandle<DxfPolyline2d>(db, "49");
                var light = new DxfPolyline();

                light.ConvertFrom(old, true);

                Assert.True(old.IsErased);
                Assert.Equal("49", light.Handle);
                Assert.Equal("Wand€Außen", light.Layer);
                Assert.True(light.Closed);
                Assert.Equal(3, light.NumberOfVertices);
                Assert.Same(light, ByHandle<DxfPolyline>(db, "49"));

                string saved = Encoding.UTF8.GetString(Saved(db));
                DxfPolyline again = ByHandle<DxfPolyline>(DxfDatabase.Parse(saved), "49");
                Assert.Equal(Bits(new GeoPoint3(5, 5, 0)), Bits(again.GetPoint3dAt(2)));
                Assert.Equal(12.5, again.Area);

                DxfPolyline2d back = light.ConvertTo(true);
                Assert.True(light.IsErased);
                Assert.Equal("49", back.Handle);
                Assert.Equal(3, back.Count());
                Assert.Same(back, ByHandle<DxfPolyline2d>(db, "49"));
                DxfPolyline2d twice = ByHandle<DxfPolyline2d>(DxfDatabase.Parse(db.ToText()), "49");
                Assert.Equal(Bits(new GeoPoint3(5, 5, 0)), Bits(twice.ToList()[2].Position));
                Assert.True(twice.Closed);
            }

            DxfDatabase r12 = DxfDatabase.Parse(DxfFileEzdxfDrawings.R12);
            Assert.Throws<ArgumentException>(() => new DxfPolyline().ConvertFrom(ByHandle<DxfPolyline2d>(r12, "4C"), true));
        }

        [Fact]
        public void APolylineConvertedWithoutThePlace_IsANewOne_TheOldKept()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfPolyline light = ByHandle<DxfPolyline>(db, "6C");

            DxfPolyline2d copy = light.ConvertTo(false);

            Assert.False(light.IsErased);
            Assert.Null(copy.Handle);
            Assert.Equal(4, copy.Count());
            Assert.Equal(-0.25, copy.ToList()[1].Bulge);
            Assert.Equal(light.Area, copy.Area, 12);
            Assert.Equal(light.LineWeight, copy.LineWeight);

            var made = new DxfPolyline();
            made.ConvertFrom(copy, false);
            Assert.Equal(light.Area, made.Area, 12);
            Assert.Equal(4, made.NumberOfVertices);
            Assert.Throws<ArgumentException>(() => made.ConvertFrom(light, false));
        }
    }
}
