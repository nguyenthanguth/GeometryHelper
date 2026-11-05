using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfLines;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The typed conversions of the entity classes, named as the AutoCAD bridge names its own (<c>ToGeoLine3</c>,
    /// <c>ToGeoPolygonArc2</c>): of space the shape <see cref="DxfEntity.GetGeometry"/> gives, to the bit; of the plane what
    /// <see cref="DxfReader"/> reads with <see cref="DxfReadOptions.PlanAs2D"/>, to the bit; a polyline of the kind asked,
    /// built by the reader's own chain and loop; and every refusal with its reason.
    /// </summary>
    public class DxfTypedShapeTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static readonly Tolerance Fine = new Tolerance(1E-5, 1E-5);

        private static string E(params object[] pairs) => G(pairs);

        private static string Lwpolyline(string handle, bool closed, double[] xyb, double elevation = 0.0, double[] normal = null)
        {
            var groups = new List<object> { 0, "LWPOLYLINE", 5, handle, 100, "AcDbEntity", 8, "0", 100, "AcDbPolyline", 90, xyb.Length / 3, 70, closed ? 1 : 0 };

            if (elevation != 0.0)
            {
                groups.AddRange(new object[] { 38, elevation });
            }

            for (int i = 0; i < xyb.Length; i += 3)
            {
                groups.AddRange(new object[] { 10, xyb[i], 20, xyb[i + 1] });

                if (xyb[i + 2] != 0.0)
                {
                    groups.AddRange(new object[] { 42, xyb[i + 2] });
                }
            }

            if (normal != null)
            {
                groups.AddRange(new object[] { 210, normal[0], 220, normal[1], 230, normal[2] });
            }

            return G(groups.ToArray());
        }

        private static string Polyline(string handle, int flags, string vertexMarker, int vertexFlags, params double[] xyzb)
        {
            string text = G(0, "POLYLINE", 5, handle, 100, "AcDbEntity", 8, "0", 100, (flags & 8) != 0 ? "AcDb3dPolyline" : "AcDb2dPolyline", 66, 1, 10, 0.0, 20, 0.0, 30, 0.0, 70, flags);

            for (int i = 0; i < xyzb.Length; i += 4)
            {
                text += G(0, "VERTEX", 5, handle + "V" + (i / 4).ToString(System.Globalization.CultureInfo.InvariantCulture), 100, "AcDbEntity", 8, "0", 100, "AcDbVertex", 100, vertexMarker, 10, xyzb[i], 20, xyzb[i + 1], 30, xyzb[i + 2])
                    + (xyzb[i + 3] != 0.0 ? G(42, xyzb[i + 3]) : string.Empty)
                    + G(70, vertexFlags);
            }

            return text + G(0, "SEQEND", 5, handle + "E", 100, "AcDbEntity", 8, "0");
        }

        private static readonly string Text = R2000(
            E(0, "LINE", 5, "10", 100, "AcDbEntity", 8, "0", 100, "AcDbLine", 10, 0.0, 20, 0.0, 30, 0.0, 11, 100.0, 21, 50.0, 31, 0.0)
            + E(0, "LINE", 5, "11", 100, "AcDbEntity", 8, "0", 100, "AcDbLine", 10, 0.0, 20, 0.0, 30, 0.0, 11, 100.0, 21, 50.0, 31, 5.0)
            + E(0, "LINE", 5, "12", 100, "AcDbEntity", 8, "0", 100, "AcDbLine", 10, 0.0, 20, 0.0, 30, 0.0, 11, 0.0004, 21, 0.0, 31, 0.0)
            + E(0, "CIRCLE", 5, "20", 100, "AcDbEntity", 8, "0", 100, "AcDbCircle", 10, 1.0, 20, 2.0, 30, 0.0, 40, 5.0)
            + E(0, "CIRCLE", 5, "21", 100, "AcDbEntity", 8, "0", 100, "AcDbCircle", 10, 10.0, 20, 20.0, 30, 0.0, 40, 5.0, 210, 0.0, 220, 0.0, 230, -1.0)
            + E(0, "CIRCLE", 5, "22", 100, "AcDbEntity", 8, "0", 100, "AcDbCircle", 10, 10.0, 20, 20.0, 30, 0.0, 40, 5.0, 210, 0.0, 220, 0.6, 230, 0.8)
            + E(0, "ARC", 5, "30", 100, "AcDbEntity", 8, "0", 100, "AcDbCircle", 10, 0.0, 20, 0.0, 30, 0.0, 40, 10.0, 100, "AcDbArc", 50, 0.0, 51, 90.0)
            + E(0, "ARC", 5, "31", 100, "AcDbEntity", 8, "0", 100, "AcDbCircle", 10, 0.0, 20, 0.0, 30, 0.0, 40, 10.0, 210, 0.0, 220, 0.0, 230, -1.0, 100, "AcDbArc", 50, 0.0, 51, 90.0)
            + E(0, "ARC", 5, "32", 100, "AcDbEntity", 8, "0", 100, "AcDbCircle", 10, 0.0, 20, 0.0, 30, 2.0, 40, 10.0, 100, "AcDbArc", 50, 0.0, 51, 90.0)
            + E(0, "ELLIPSE", 5, "42", 100, "AcDbEntity", 8, "0", 100, "AcDbEllipse", 10, 0.0, 20, 0.0, 30, 3.0, 11, 60.0, 21, 0.0, 31, 0.0, 210, 0.0, 220, 0.0, 230, 1.0, 40, 0.5, 41, 0.0, 42, 2 * Math.PI)
            + E(0, "LEADER", 5, "C0", 100, "AcDbEntity", 8, "0", 100, "AcDbLeader", 3, "Standard", 71, 1, 72, 0, 73, 3, 74, 1, 75, 0, 76, 3,
                10, 0.0, 20, 0.0, 30, 0.0, 10, 10.0, 20, 10.0, 30, 4.0, 10, 20.0, 20, 10.0, 30, 4.0)
            + E(0, "ELLIPSE", 5, "40", 100, "AcDbEntity", 8, "0", 100, "AcDbEllipse", 10, 0.0, 20, 0.0, 30, 0.0, 11, 60.0, 21, 0.0, 31, 0.0, 210, 0.0, 220, 0.0, 230, 1.0, 40, 0.5, 41, 0.0, 42, 2 * Math.PI)
            + E(0, "ELLIPSE", 5, "41", 100, "AcDbEntity", 8, "0", 100, "AcDbEllipse", 10, 0.0, 20, 0.0, 30, 0.0, 11, 60.0, 21, 0.0, 31, 0.0, 210, 0.0, 220, 0.0, 230, 1.0, 40, 0.5, 41, 0.0, 42, Math.PI / 2)
            + E(0, "RAY", 5, "50", 100, "AcDbEntity", 8, "0", 100, "AcDbRay", 10, 0.0, 20, 0.0, 30, 0.0, 11, 0.6, 21, 0.8, 31, 0.0)
            + E(0, "XLINE", 5, "51", 100, "AcDbEntity", 8, "0", 100, "AcDbXline", 10, 1.0, 20, 2.0, 30, 0.0, 11, 0.6, 21, 0.8, 31, 0.0)
            + E(0, "RAY", 5, "52", 100, "AcDbEntity", 8, "0", 100, "AcDbRay", 10, 0.0, 20, 0.0, 30, 0.0, 11, 0.6, 21, 0.0, 31, 0.8)
            + E(0, "SPLINE", 5, "60", 100, "AcDbEntity", 8, "0", 100, "AcDbSpline", 70, 8, 71, 3, 72, 8, 73, 4, 74, 0,
                40, 0.0, 40, 0.0, 40, 0.0, 40, 0.0, 40, 1.0, 40, 1.0, 40, 1.0, 40, 1.0,
                10, 0.0, 20, 0.0, 30, 0.0, 10, 100.0, 20, 150.0, 30, 0.0, 10, 200.0, 20, 150.0, 30, 0.0, 10, 300.0, 20, 0.0, 30, 0.0)
            + E(0, "SPLINE", 5, "61", 100, "AcDbEntity", 8, "0", 100, "AcDbSpline", 70, 0, 71, 3, 72, 8, 73, 4, 74, 0,
                40, 0.0, 40, 0.0, 40, 0.0, 40, 0.0, 40, 1.0, 40, 1.0, 40, 1.0, 40, 1.0,
                10, 0.0, 20, 0.0, 30, 0.0, 10, 100.0, 20, 150.0, 30, 40.0, 10, 200.0, 20, 150.0, 30, 0.0, 10, 300.0, 20, 0.0, 30, 0.0)
            + E(0, "POINT", 5, "70", 100, "AcDbEntity", 8, "0", 100, "AcDbPoint", 10, 3.0, 20, 4.0, 30, 0.0)
            + E(0, "POINT", 5, "71", 100, "AcDbEntity", 8, "0", 100, "AcDbPoint", 10, 3.0, 20, 4.0, 30, 5.0)
            + Lwpolyline("80", true, new[] { 0.0, 0.0, 0.0, 10.0, 0.0, -0.25, 10.0, 10.0, 0.0, 0.0, 10.0, 0.0 })
            + Lwpolyline("81", false, new[] { 0.0, 0.0, 0.0, 10.0, 0.0, 0.0, 10.0, 10.0, 0.0 })
            + Lwpolyline("82", true, new[] { 0.0, 0.0, 0.0, 10.0, 0.0, 0.0, 10.0, 10.0, 0.5, 0.0, 10.0, 0.0 }, 5.0, new[] { 0.0, 0.6, 0.8 })
            + Lwpolyline("83", true, new[] { 0.0, 0.0, 0.0, 10.0, 0.0, 0.5, 10.0, 10.0, 0.0, 0.0, 10.0, 0.0 }, 0.0, new[] { 0.0, 0.0, -1.0 })
            + Lwpolyline("84", false, new[] { 0.0, 0.0, 0.0, 5.0, 0.0, 0.0, 10.0, 0.0, 0.0 })
            + Lwpolyline("85", true, new[] { 0.0, 0.0, 1.0, 20.0, 0.0, 1.0 })
            + Lwpolyline("86", false, new[] { 0.0, 0.0, 0.0, 10.0, 0.0, 1.0, 10.0, 10.0, 0.4142 })
            + Lwpolyline("87", false, new[] { 0.0, 0.0, 0.0, 10.0, 0.0, 0.0, 10.0, 10.0, 0.0 }, 5.0)
            + Lwpolyline("88", true, new[] { 0.0, 0.0, 0.0, 10.0, 0.0, 0.0 })
            + Lwpolyline("89", false, new[] { 0.0, 0.0, 0.0, 10.0, 0.0, 0.0, 10.05, 0.0, 0.0, 20.0, 5.0, 0.0 })
            + Lwpolyline("8A", true, new[] { 0.0, 0.0, 1.0, 10.0, 0.0, -1.0 })
            + Polyline("90", 1, "AcDb2dVertex", 0, 0.0, 0.0, 0.0, 0.0, 12.0, 0.0, 0.0, 0.0, 12.0, 8.0, 0.0, 0.0)
            + Polyline("91", 9, "AcDb3dPolylineVertex", 32, 0.0, 0.0, 0.0, 0.0, 10.0, 0.0, 0.0, 0.0, 10.0, 10.0, 5.0, 0.0, 0.0, 10.0, 0.0, 0.0)
            + Polyline("92", 8, "AcDb3dPolylineVertex", 32, 0.0, 0.0, 0.0, 0.0, 3.0, 4.0, 0.0, 0.0)
            + Polyline("93", 8, "AcDb3dPolylineVertex", 32, 0.0, 0.0, 0.0, 0.0, 3.0, 4.0, 0.0, 0.0, 3.0, 4.0, 12.0, 0.0)
            + E(0, "POLYLINE", 5, "94", 100, "AcDbEntity", 8, "0", 100, "AcDb2dPolyline", 66, 1, 10, 0.0, 20, 0.0, 30, 0.0, 70, 4, 75, 6)
            + E(0, "VERTEX", 5, "94A", 100, "AcDbEntity", 8, "0", 100, "AcDbVertex", 100, "AcDb2dVertex", 10, 0.0, 20, 0.0, 30, 0.0, 70, 16)
            + E(0, "VERTEX", 5, "94B", 100, "AcDbEntity", 8, "0", 100, "AcDbVertex", 100, "AcDb2dVertex", 10, 10.0, 20, 10.0, 30, 0.0, 70, 16)
            + E(0, "VERTEX", 5, "94C", 100, "AcDbEntity", 8, "0", 100, "AcDbVertex", 100, "AcDb2dVertex", 10, 20.0, 20, 0.0, 30, 0.0, 70, 16)
            + E(0, "VERTEX", 5, "94D", 100, "AcDbEntity", 8, "0", 100, "AcDbVertex", 100, "AcDb2dVertex", 10, 0.0, 20, 0.0, 30, 0.0, 70, 8)
            + E(0, "VERTEX", 5, "94E", 100, "AcDbEntity", 8, "0", 100, "AcDbVertex", 100, "AcDb2dVertex", 10, 5.0, 20, 4.0, 30, 0.0, 70, 8)
            + E(0, "VERTEX", 5, "94F", 100, "AcDbEntity", 8, "0", 100, "AcDbVertex", 100, "AcDb2dVertex", 10, 10.0, 20, 5.0, 30, 0.0, 70, 8)
            + E(0, "VERTEX", 5, "950", 100, "AcDbEntity", 8, "0", 100, "AcDbVertex", 100, "AcDb2dVertex", 10, 15.0, 20, 4.0, 30, 0.0, 70, 8)
            + E(0, "VERTEX", 5, "951", 100, "AcDbEntity", 8, "0", 100, "AcDbVertex", 100, "AcDb2dVertex", 10, 20.0, 20, 0.0, 30, 0.0, 70, 8)
            + E(0, "SEQEND", 5, "952", 100, "AcDbEntity", 8, "0")
            + E(0, "3DFACE", 5, "A0", 100, "AcDbEntity", 8, "0", 100, "AcDbFace", 10, 0.0, 20, 0.0, 30, 0.0, 11, 10.0, 21, 0.0, 31, 0.0, 12, 10.0, 22, 8.0, 32, 0.0, 13, 0.0, 23, 8.0, 33, 0.0)
            + E(0, "3DFACE", 5, "A1", 100, "AcDbEntity", 8, "0", 100, "AcDbFace", 10, 0.0, 20, 0.0, 30, 0.0, 11, 10.0, 21, 0.0, 31, 0.0, 12, 10.0, 22, 8.0, 32, 1.0, 13, 0.0, 23, 8.0, 33, 0.0)
            + E(0, "3DFACE", 5, "A2", 100, "AcDbEntity", 8, "0", 100, "AcDbFace", 10, 0.0, 20, 0.0, 30, 2.0, 11, 10.0, 21, 0.0, 31, 2.0, 12, 10.0, 22, 8.0, 32, 2.0, 13, 10.0, 23, 8.0, 33, 2.0)
            + E(0, "3DFACE", 5, "A3", 100, "AcDbEntity", 8, "0", 100, "AcDbFace", 10, 0.0, 20, 0.0, 30, 0.0, 11, 5.0, 21, 0.0, 31, 0.0, 12, 10.0, 22, 0.0, 32, 0.0, 13, 10.0, 23, 0.0, 33, 0.0)
            + E(0, "3DFACE", 5, "A4", 100, "AcDbEntity", 8, "0", 100, "AcDbFace", 10, 0.0, 20, 0.0, 30, 0.0, 11, 10.0, 21, 10.0, 31, 0.0, 12, 10.0, 22, 0.0, 32, 0.0, 13, 0.0, 23, 10.0, 33, 0.0)
            + E(0, "SOLID", 5, "B0", 100, "AcDbEntity", 8, "0", 100, "AcDbTrace", 10, 0.0, 20, 0.0, 30, 0.0, 11, 2.0, 21, 0.0, 31, 0.0, 12, 0.0, 22, 2.0, 32, 0.0, 13, 2.0, 23, 2.0, 33, 0.0)
            + E(0, "SOLID", 5, "B1", 100, "AcDbEntity", 8, "0", 100, "AcDbTrace", 10, 0.0, 20, 0.0, 30, 3.0, 11, 2.0, 21, 0.0, 31, 3.0, 12, 0.0, 22, 2.0, 32, 3.0, 13, 2.0, 23, 2.0, 33, 3.0));

        private static DxfDatabase Drawing() => DxfDatabase.Parse(Text);

        private static object Plan(string text, string handle, Tolerance tolerance)
            => DxfReader.Parse(text, new DxfReadOptions { PlanAs2D = true, Tolerance = tolerance }).Shapes.Single(s => s.Handle == handle).Shape;

        private static object Space(string text, string handle)
            => DxfReader.Parse(text, new DxfReadOptions { Tolerance = Tol }).Shapes.Single(s => s.Handle == handle).Shape;

        private static string Refused(Action convert) => Assert.Throws<InvalidOperationException>(convert).Message;

        #region Of space, the shape GetGeometry gives

        [Fact]
        public void EveryConversionOfSpace_IsTheShapeGetGeometryGives_ToTheBit()
        {
            DxfDatabase db = Drawing();
            object Shape(string handle) => Assert.Single(ByHandle<DxfEntity>(db, handle).GetGeometry());

            Assert.Equal(Shape("10"), ByHandle<DxfLine>(db, "10").ToGeoLine3());
            Assert.Equal(Shape("11"), ByHandle<DxfLine>(db, "11").ToGeoLine3());
            Assert.Equal(Shape("20"), ByHandle<DxfCircle>(db, "20").ToGeoCircle3());
            Assert.Equal(Shape("22"), ByHandle<DxfCircle>(db, "22").ToGeoCircle3());
            Assert.Equal(Shape("30"), ByHandle<DxfArc>(db, "30").ToGeoArc3());
            Assert.Equal(Shape("31"), ByHandle<DxfArc>(db, "31").ToGeoArc3());
            Assert.Equal(Shape("40"), ByHandle<DxfEllipse>(db, "40").ToGeoEllipse3());
            Assert.Same(Shape("41"), ByHandle<DxfEllipse>(db, "41").ToGeoSpline3());
            Assert.Equal(Shape("50"), ByHandle<DxfRay>(db, "50").ToGeoRay3());
            Assert.Equal(Shape("51"), ByHandle<DxfXline>(db, "51").ToGeoXLine3());
            Assert.Same(Shape("60"), ByHandle<DxfSpline>(db, "60").ToGeoSpline3());
            Assert.Equal(Shape("71"), ByHandle<DxfDBPoint>(db, "71").ToGeoPoint3());
            Assert.Same(Shape("80"), ByHandle<DxfPolyline>(db, "80").ToGeoPolygonArc3());
            Assert.Same(Shape("81"), ByHandle<DxfPolyline>(db, "81").ToGeoPolyline3());
            Assert.Same(Shape("82"), ByHandle<DxfPolyline>(db, "82").ToGeoPolygonArc3());
            Assert.Same(Shape("90"), ByHandle<DxfPolyline2d>(db, "90").ToGeoPolygon3());
            Assert.Same(Shape("91"), ByHandle<DxfPolyline3d>(db, "91").ToGeoPolyline3());
            Assert.Same(Shape("93"), ByHandle<DxfPolyline3d>(db, "93").ToGeoPolyline3());
            Assert.Same(Shape("B0"), ByHandle<DxfSolid>(db, "B0").ToGeoPolygon3());
            Assert.Same(Assert.IsType<GeoFace3>(Shape("A0")).Boundary, ByHandle<DxfFace>(db, "A0").ToGeoPolygon3());

            // The same as a reading of the file, every shape of the reader's to the bit.
            Assert.Equal(Space(Text, "10"), ByHandle<DxfLine>(db, "10").ToGeoLine3());
            Assert.Equal(Space(Text, "80"), ByHandle<DxfPolyline>(db, "80").ToGeoPolygonArc3());
        }

        [Fact]
        public void TheLineFrom0To100And50_IsTheGeoLine3BetweenThem()
        {
            DxfDatabase db = Drawing();

            Assert.Equal(new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 50, 0)), ByHandle<DxfLine>(db, "10").ToGeoLine3());
            Assert.Equal(new GeoPoint3(3, 4, 5), ByHandle<DxfDBPoint>(db, "71").ToGeoPoint3());
        }

        #endregion

        #region Of the plane, what the reader reads with PlanAs2D

        [Fact]
        public void EveryConversionOfThePlane_IsWhatTheReaderReadsWithPlanAs2D_ToTheBit()
        {
            DxfDatabase db = Drawing();
            object Read(string handle) => Plan(Text, handle, Tol);

            Assert.Equal(Read("10"), ByHandle<DxfLine>(db, "10").ToGeoLine2());
            Assert.Equal(Read("10"), ByHandle<DxfLine>(db, "10").ToGeoLine2(Tol));
            Assert.Equal(Read("20"), ByHandle<DxfCircle>(db, "20").ToGeoCircle2());
            Assert.Equal(Read("21"), ByHandle<DxfCircle>(db, "21").ToGeoCircle2());
            Assert.Equal(Read("30"), ByHandle<DxfArc>(db, "30").ToGeoArc2());
            Assert.Equal(Read("31"), ByHandle<DxfArc>(db, "31").ToGeoArc2(Tol));
            Assert.Equal(Read("40"), ByHandle<DxfEllipse>(db, "40").ToGeoEllipse2());
            Assert.Equal(Read("41"), ByHandle<DxfEllipse>(db, "41").ToGeoSpline2());
            Assert.Equal(Read("50"), ByHandle<DxfRay>(db, "50").ToGeoRay2());
            Assert.Equal(Read("51"), ByHandle<DxfXline>(db, "51").ToGeoXLine2());
            Assert.Equal(Read("60"), ByHandle<DxfSpline>(db, "60").ToGeoSpline2());
            Assert.Equal(Read("70"), ByHandle<DxfDBPoint>(db, "70").ToGeoPoint2());
            Assert.Equal(Read("80"), ByHandle<DxfPolyline>(db, "80").ToGeoPolygonArc2());
            Assert.Equal(Read("81"), ByHandle<DxfPolyline>(db, "81").ToGeoPolyline2());
            Assert.Equal(Read("83"), ByHandle<DxfPolyline>(db, "83").ToGeoPolygonArc2());
            Assert.Equal(Read("85"), ByHandle<DxfPolyline>(db, "85").ToGeoPolygonArc2());
            Assert.Equal(Read("90"), ByHandle<DxfPolyline2d>(db, "90").ToGeoPolygon2());
            Assert.Equal(Read("92"), ByHandle<DxfPolyline3d>(db, "92").ToGeoPolyline2());
            Assert.Equal(Read("B0"), ByHandle<DxfSolid>(db, "B0").ToGeoPolygon2());

            Assert.Equal(new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(100, 50)), ByHandle<DxfLine>(db, "10").ToGeoLine2());
            Assert.Equal(new GeoCircle2(new GeoPoint2(-10, 20), 5), ByHandle<DxfCircle>(db, "21").ToGeoCircle2());
            Assert.Equal(new GeoPoint2(3, 4), ByHandle<DxfDBPoint>(db, "70").ToGeoPoint2());
        }

        [Fact]
        public void AnEntityOffTheFloor_IsRefusedInThePlane_NotFlattened()
        {
            DxfDatabase db = Drawing();

            Assert.Contains("does not lie on the floor", Refused(() => ByHandle<DxfLine>(db, "11").ToGeoLine2()));
            Assert.Contains("ToGeoLine3 gives it in space", Refused(() => ByHandle<DxfLine>(db, "11").ToGeoLine2()));
            Assert.Contains("does not lie on the floor", Refused(() => ByHandle<DxfCircle>(db, "22").ToGeoCircle2()));
            Assert.Contains("does not lie on the floor", Refused(() => ByHandle<DxfRay>(db, "52").ToGeoRay2()));
            Assert.Contains("does not lie on the floor", Refused(() => ByHandle<DxfSpline>(db, "61").ToGeoSpline2()));
            Assert.Contains("does not lie on the floor", Refused(() => ByHandle<DxfDBPoint>(db, "71").ToGeoPoint2()));
            Assert.Contains("does not lie on the floor", Refused(() => ByHandle<DxfPolyline>(db, "82").ToGeoPolygonArc2()));
            Assert.Contains("does not lie on the floor", Refused(() => ByHandle<DxfPolyline>(db, "87").ToGeoPolyline2()));
            Assert.Contains("does not lie on the floor", Refused(() => ByHandle<DxfPolyline3d>(db, "93").ToGeoPolyline2()));
            Assert.Contains("does not lie on the floor", Refused(() => ByHandle<DxfSolid>(db, "B1").ToGeoPolygon2()));
            Assert.Contains("does not lie on the floor", Refused(() => ByHandle<DxfFace>(db, "A2").ToGeoPolygon2()));
            Assert.Contains("ToGeoArc3 gives it in space", Refused(() => ByHandle<DxfArc>(db, "32").ToGeoArc2()));
            Assert.Contains("ToGeoEllipse3 gives it in space", Refused(() => ByHandle<DxfEllipse>(db, "42").ToGeoEllipse2(Tol)));
            Assert.Contains("ToGeoPolyline3 gives it in space", Refused(() => ByHandle<DxfLeader>(db, "C0").ToGeoPolyline2()));
            Assert.Equal(new GeoPoint3(20, 10, 4), ByHandle<DxfLeader>(db, "C0").ToGeoPolyline3().Vertices[2]);
            Assert.Equal(new GeoPoint3(0, 0, 2), ByHandle<DxfArc>(db, "32").ToGeoArc3().Center);
        }

        [Fact]
        public void APolylineReadAtACoarseTolerance_HasTheVerticesItMergesLeftOut_AsTheReaderReadsIt()
        {
            DxfDatabase db = Drawing();
            DxfPolyline polyline = ByHandle<DxfPolyline>(db, "89");
            var coarse = new Tolerance(0.1, 1E-5);

            Assert.Equal(4, polyline.ToGeoPolyline2().VertexCount);
            Assert.Equal(3, polyline.ToGeoPolyline2(coarse).VertexCount);
            Assert.Equal(Plan(Text, "89", coarse), polyline.ToGeoPolyline2(coarse));
            Assert.Equal(Assert.IsType<GeoPolyline2>(Plan(Text, "89", coarse)).Vertices, polyline.ToGeoPolylineArc2(coarse).Vertices);
        }

        [Fact]
        public void AnArcMirroredOnMinusZ_IsReadClockwiseInThePlane_FromWhereTheArcInSpaceStarts()
        {
            DxfDatabase db = Drawing();
            DxfArc arc = ByHandle<DxfArc>(db, "31");

            GeoArc2 plan = arc.ToGeoArc2();
            GeoArc3 space = arc.ToGeoArc3();

            Assert.True(plan.IsClockwise);
            Assert.True(plan.StartPoint.IsEqualTo(new GeoPoint2(-10, 0), Tol));
            Assert.True(plan.EndPoint.IsEqualTo(new GeoPoint2(0, 10), Tol));
            Assert.True(space.StartPoint.IsEqualTo(new GeoPoint3(-10, 0, 0), Tol));
            Assert.True(space.EndPoint.IsEqualTo(new GeoPoint3(0, 10, 0), Tol));
        }

        #endregion

        #region No shape

        [Fact]
        public void ALine0Point0004Long_HasNoShapeWithinTheDefaultTolerance_AndOneWithin1E5()
        {
            DxfDatabase db = Drawing();
            DxfLine line = ByHandle<DxfLine>(db, "12");

            Assert.Contains("of no length", Refused(() => line.ToGeoLine3()));
            Assert.Contains("of no length", Refused(() => line.ToGeoLine2()));
            Assert.Equal(Plan(Text, "12", Fine), line.ToGeoLine2(Fine));
            Assert.Equal(new GeoPoint2(0.0004, 0), line.ToGeoLine2(Fine).EndPoint);
        }

        [Fact]
        public void AnErasedEntity_GivesNoShape()
        {
            DxfDatabase db = Drawing();
            DxfLine line = ByHandle<DxfLine>(db, "10");
            line.Erase();

            Assert.Contains("is erased", Refused(() => line.ToGeoLine3()));
            Assert.Contains("is erased", Refused(() => line.ToGeoLine2()));
        }

        #endregion

        #region Ellipses, leaders, faces

        [Fact]
        public void AnEllipticalArc_IsASplineAndNoEllipse_AndAWholeEllipseTheOtherWayRound()
        {
            DxfDatabase db = Drawing();
            DxfEllipse whole = ByHandle<DxfEllipse>(db, "40");
            DxfEllipse part = ByHandle<DxfEllipse>(db, "41");

            GeoEllipse3 ellipse = whole.ToGeoEllipse3();
            Assert.Equal(60.0, ellipse.MajorRadius);
            Assert.Equal(30.0, ellipse.MinorRadius);
            Assert.Equal(3, part.ToGeoSpline3().ControlPointCount);
            Assert.Contains("ToGeoSpline3 gives it", Refused(() => part.ToGeoEllipse3()));
            Assert.Contains("ToGeoSpline2 gives it", Refused(() => part.ToGeoEllipse2()));
            Assert.Contains("ToGeoEllipse3 gives", Refused(() => whole.ToGeoSpline3()));
            Assert.Contains("ToGeoEllipse2 gives", Refused(() => whole.ToGeoSpline2()));
        }

        [Fact]
        public void AStraightLeaderIsAChain_AndASplinedOneASpline_EachRefusingTheOther()
        {
            // 89 through (0, 0, 0), (10, 10, 0) and (20, 10, 0); B2 splined.
            string text = DxfHatchLeaderDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfLeader straight = ByHandle<DxfLeader>(db, "89");
            DxfLeader splined = ByHandle<DxfLeader>(db, "B2");

            Assert.Same(Assert.Single(straight.GetGeometry()), straight.ToGeoPolyline3());
            Assert.Same(Assert.Single(splined.GetGeometry()), splined.ToGeoSpline3());
            Assert.Equal(Plan(text, "89", Tol), straight.ToGeoPolyline2());
            Assert.Equal(Plan(text, "B2", Tol), splined.ToGeoSpline2(Tol));
            Assert.Equal(3, straight.ToGeoPolyline3().VertexCount);
            Assert.Contains("is splined: ToGeoSpline3", Refused(() => splined.ToGeoPolyline3()));
            Assert.Contains("is splined: ToGeoSpline2", Refused(() => splined.ToGeoPolyline2()));
            Assert.Contains("is straight: ToGeoPolyline3", Refused(() => straight.ToGeoSpline3()));
            Assert.Contains("is straight: ToGeoPolyline2", Refused(() => straight.ToGeoSpline2()));
        }

        [Fact]
        public void AFlatFaceIsItsPolygon_AWarpedOneIsRefused_AndATriangleIsItsThreeCorners()
        {
            DxfDatabase db = Drawing();

            GeoPolygon3 flat = ByHandle<DxfFace>(db, "A0").ToGeoPolygon3();
            Assert.Equal(new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(10, 8, 0), new GeoPoint3(0, 8, 0) }, flat.Vertices);
            Assert.Equal(new[] { new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 8), new GeoPoint2(0, 8) }, ByHandle<DxfFace>(db, "A0").ToGeoPolygon2().Vertices);

            Assert.Equal(2, ByHandle<DxfFace>(db, "A1").GetGeometry().Count);
            Assert.Contains("its corners not being in one plane", Refused(() => ByHandle<DxfFace>(db, "A1").ToGeoPolygon3()));

            // The bow tie lies flat, but its edges cross: the reason says so, not that its corners are out of one plane.
            Assert.Equal(2, ByHandle<DxfFace>(db, "A4").GetGeometry().Count);
            string bowTie = Refused(() => ByHandle<DxfFace>(db, "A4").ToGeoPolygon3());
            Assert.Contains("its edges crossing", bowTie);
            Assert.DoesNotContain("not being in one plane", bowTie);
            Assert.Contains("the reader cuts it into two triangles, which GetGeometry gives", Refused(() => ByHandle<DxfFace>(db, "A1").ToGeoPolygon2()));

            var triangle = Assert.IsType<GeoTriangle3>(Assert.Single(ByHandle<DxfFace>(db, "A2").GetGeometry()));
            Assert.Equal(new[] { triangle.A, triangle.B, triangle.C }, ByHandle<DxfFace>(db, "A2").ToGeoPolygon3().Vertices);

            Assert.Contains("encloses no area", Refused(() => ByHandle<DxfFace>(db, "A3").ToGeoPolygon3()));
        }

        [Fact]
        public void ASolidWritten0And0_2And0_0And2_2And2_IsTheSquareOfItsCornersInTheOrder1243()
        {
            DxfDatabase db = Drawing();

            Assert.Equal(new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(2, 0, 0), new GeoPoint3(2, 2, 0), new GeoPoint3(0, 2, 0) }, ByHandle<DxfSolid>(db, "B0").ToGeoPolygon3().Vertices);
            Assert.Equal(new[] { new GeoPoint2(0, 0), new GeoPoint2(2, 0), new GeoPoint2(2, 2), new GeoPoint2(0, 2) }, ByHandle<DxfSolid>(db, "B0").ToGeoPolygon2().Vertices);
            Assert.Equal(3.0, ByHandle<DxfSolid>(db, "B1").ToGeoPolygon3().Vertices[0].Z);
        }

        #endregion

        #region Polylines of the kind asked

        [Fact]
        public void APolylineWithAnArc_IsRefusedByTheStraightKinds_AndGivenBulgeForBulgeByTheArcKinds()
        {
            DxfDatabase db = Drawing();
            DxfPolyline polyline = ByHandle<DxfPolyline>(db, "80");

            Assert.Contains("has arcs, which a GeoPolygon3 cannot hold: ToGeoPolygonArc3 gives them", Refused(() => polyline.ToGeoPolygon3()));
            Assert.Contains("ToGeoPolylineArc3 gives them", Refused(() => polyline.ToGeoPolyline3()));
            Assert.Contains("ToGeoPolygonArc2 gives them", Refused(() => polyline.ToGeoPolygon2()));
            Assert.Contains("ToGeoPolylineArc2 gives them", Refused(() => polyline.ToGeoPolyline2(Tol)));

            GeoPolygonArc2 loop = polyline.ToGeoPolygonArc2();
            Assert.Equal(new[] { new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10), new GeoPoint2(0, 10) }, loop.Vertices);
            Assert.Equal(new[] { 0.0, -0.25, 0.0, 0.0 }, loop.Bulges);
        }

        [Fact]
        public void AClosedPolylineAsAChain_RunsOnBackToItsFirstVertex_TheLastBulgeOnTheClosingSegment()
        {
            DxfDatabase db = Drawing();
            DxfPolyline polyline = ByHandle<DxfPolyline>(db, "80");

            GeoPolylineArc2 chain = polyline.ToGeoPolylineArc2();
            Assert.Equal(new[] { new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10), new GeoPoint2(0, 10), new GeoPoint2(0, 0) }, chain.Vertices);
            Assert.Equal(new[] { 0.0, -0.25, 0.0, 0.0, 0.0 }, chain.Bulges);

            GeoPolylineArc3 space = polyline.ToGeoPolylineArc3();
            Assert.Equal(5, space.VertexCount);
            Assert.Equal(new GeoPoint3(0, 0, 0), space.Vertices[4]);

            GeoPolyline2 straight = ByHandle<DxfPolyline2d>(db, "90").ToGeoPolyline2();
            Assert.Equal(new[] { new GeoPoint2(0, 0), new GeoPoint2(12, 0), new GeoPoint2(12, 8), new GeoPoint2(0, 0) }, straight.Vertices);
            Assert.Equal(new GeoPoint3(0, 0, 0), ByHandle<DxfPolyline3d>(db, "91").ToGeoPolyline3().Vertices.Last());
        }

        [Fact]
        public void AnOpenPolylineAsALoop_IsJoinedBackToItsFirstVertex_TheBulgeOfItsLastVertexClosingIt()
        {
            DxfDatabase db = Drawing();

            GeoPolygon2 square = ByHandle<DxfPolyline>(db, "81").ToGeoPolygon2();
            Assert.Equal(new[] { new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10) }, square.Vertices);
            Assert.Equal(50.0, square.Area, 12);

            // (0, 0), (10, 0) bulge 1, (10, 10) bulge 0.4142: the last bulge closes it, as the AutoCAD bridge reads one.
            GeoPolygonArc2 loop = ByHandle<DxfPolyline>(db, "86").ToGeoPolygonArc2();
            Assert.Equal(new[] { 0.0, 1.0, 0.4142 }, loop.Bulges);
            Assert.Equal(new[] { 0.0, 1.0, 0.0 }, ByHandle<DxfPolyline>(db, "86").ToGeoPolylineArc2().Bulges);
            Assert.Same(Assert.Single(ByHandle<DxfPolyline>(db, "86").GetGeometry()), ByHandle<DxfPolyline>(db, "86").ToGeoPolylineArc3());
        }

        [Fact]
        public void APolylineWithNoArc_AsAKindWithArcs_HasEveryBulgeNought()
        {
            DxfDatabase db = Drawing();
            DxfPolyline polyline = ByHandle<DxfPolyline>(db, "81");

            GeoPolylineArc3 chain = polyline.ToGeoPolylineArc3();
            Assert.Equal(polyline.ToGeoPolyline3().Vertices, chain.Vertices);
            Assert.All(chain.Bulges, b => Assert.Equal(0.0, b));

            GeoPolygonArc2 loop = polyline.ToGeoPolygonArc2();
            Assert.Equal(polyline.ToGeoPolygon2().Vertices, loop.Vertices);
            Assert.All(loop.Bulges, b => Assert.Equal(0.0, b));

            GeoPolygonArc3 space = ByHandle<DxfPolyline2d>(db, "90").ToGeoPolygonArc3();
            Assert.Equal(48.0, space.Area, 9);
            Assert.All(space.Bulges, b => Assert.Equal(0.0, b));
        }

        [Fact]
        public void ThreeVerticesOnALine_AreNoLoop_ButAChain()
        {
            DxfDatabase db = Drawing();
            DxfPolyline polyline = ByHandle<DxfPolyline>(db, "84");

            Assert.Contains("encloses no area", Refused(() => polyline.ToGeoPolygon3()));
            Assert.Contains("ToGeoPolyline2 gives it as a chain", Refused(() => polyline.ToGeoPolygon2()));
            Assert.Contains("ToGeoPolylineArc2 gives it as a chain", Refused(() => polyline.ToGeoPolygonArc2()));
            Assert.Equal(3, polyline.ToGeoPolyline2().VertexCount);

            // Two vertices closed with no arc are a line there and back, as the reader reads them.
            DxfPolyline two = ByHandle<DxfPolyline>(db, "88");
            Assert.Contains("encloses no area", Refused(() => two.ToGeoPolygon2()));
            Assert.Equal(new[] { new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(0, 0) }, two.ToGeoPolyline2().Vertices);
            Assert.Equal(two.ToGeoPolyline2(), Plan(Text, "88", Tol));
        }

        [Fact]
        public void ALoopWithArcsOfNoArea_AskedForAsAStraightLoop_IsRefusedForItsAreaNotItsArcs()
        {
            // (0, 0) bulge 1 to (10, 0) and bulge -1 back: the same half circle there and back, enclosing nothing.
            DxfDatabase db = Drawing();
            DxfPolyline polyline = ByHandle<DxfPolyline>(db, "8A");

            foreach (string message in new[] { Refused(() => polyline.ToGeoPolygon2()), Refused(() => polyline.ToGeoPolygon3()), Refused(() => polyline.ToGeoPolygonArc2()) })
            {
                Assert.Contains("encloses no area", message);
                Assert.DoesNotContain("has arcs", message);
            }

            Assert.Contains("ToGeoPolylineArc2 gives them", Refused(() => polyline.ToGeoPolyline2()));
        }

        [Fact]
        public void APolylineFittedWithASpline_IsReadThroughTheVerticesOfTheSplineDrawn_ItsFrameLeftOut()
        {
            DxfDatabase db = Drawing();
            DxfPolyline2d fitted = ByHandle<DxfPolyline2d>(db, "94");

            Assert.Equal(new[] { new GeoPoint2(0, 0), new GeoPoint2(5, 4), new GeoPoint2(10, 5), new GeoPoint2(15, 4), new GeoPoint2(20, 0) }, fitted.ToGeoPolyline2().Vertices);
            Assert.Same(Assert.Single(fitted.GetGeometry()), fitted.ToGeoPolyline3());
            Assert.Equal(5, fitted.ToGeoPolygon3().VertexCount);
        }

        [Fact]
        public void A3dPolylineOutOfOnePlane_IsNoLoop_ButAChainBackToItsStart()
        {
            DxfDatabase db = Drawing();
            DxfPolyline3d polyline = ByHandle<DxfPolyline3d>(db, "91");

            Assert.Contains("does not lie in one plane", Refused(() => polyline.ToGeoPolygon3()));
            Assert.Equal(5, polyline.ToGeoPolyline3().VertexCount);
        }

        [Fact]
        public void TwoHalfCirclesClosed_AreTheLoopOfArea314_WithTheFirstHalved()
        {
            DxfDatabase db = Drawing();
            DxfPolyline circle = ByHandle<DxfPolyline>(db, "85");

            GeoPolygonArc2 loop = circle.ToGeoPolygonArc2();
            Assert.Equal(3, loop.VertexCount);
            Assert.Equal(Math.PI * 100.0, loop.Area, 9);
            Assert.Equal(Math.PI * 100.0, circle.ToGeoPolygonArc3().Area, 9);
            Assert.Contains("ToGeoPolygonArc3 gives them", Refused(() => circle.ToGeoPolygon3()));

            // As a chain, the reader's loop opened: back to its first vertex.
            GeoPolylineArc2 chain = circle.ToGeoPolylineArc2();
            Assert.Equal(chain.Vertices[0], chain.Vertices[chain.VertexCount - 1]);
            Assert.Equal(Math.PI * 20.0, chain.Length, 9);
        }

        [Fact]
        public void ATiltedPolyline_IsGivenInSpaceAsTheReaderReadsIt_InThePlaneOfItsNormal()
        {
            DxfDatabase db = Drawing();
            DxfPolyline polyline = ByHandle<DxfPolyline>(db, "82");

            GeoPolygonArc3 loop = polyline.ToGeoPolygonArc3();
            Assert.Equal(Space(Text, "82"), loop);

            for (int i = 0; i < 4; i++)
            {
                Assert.Equal(Bits(polyline.GetPoint3dAt(i)), Bits(loop.Vertices[i]));
            }

            Assert.True(loop.Normal.IsParallelTo(new GeoVector3(0, 0.6, 0.8), Tol));

            GeoPolylineArc3 chain = polyline.ToGeoPolylineArc3();
            Assert.Equal(5, chain.VertexCount);
            Assert.Equal(Bits(polyline.GetPoint3dAt(0)), Bits(chain.Vertices[4]));
        }

        [Fact]
        public void APolylineMirroredOnMinusZ_HasItsXTurnedOverAndItsBulgesNegated_InThePlane()
        {
            DxfDatabase db = Drawing();
            GeoPolygonArc2 loop = ByHandle<DxfPolyline>(db, "83").ToGeoPolygonArc2();

            Assert.Equal(new[] { new GeoPoint2(0, 0), new GeoPoint2(-10, 0), new GeoPoint2(-10, 10), new GeoPoint2(0, 10) }, loop.Vertices);
            Assert.Equal(new[] { 0.0, -0.5, 0.0, 0.0 }, loop.Bulges);
        }

        #endregion

        #region The way back, read again

        [Fact]
        public void AShapeOfThePlaneSetAndSavedAndReadAgain_ComesBackFromItsConversion_ToTheBit()
        {
            DxfDatabase db = Drawing();
            var line = new GeoLine2(new GeoPoint2(0.1, 0.2), new GeoPoint2(30.3, 40.7));
            var circle = new GeoCircle2(new GeoPoint2(1.25, -3.5), 7.125);
            var ellipse = new GeoEllipse2(new GeoPoint2(5, 6), new GeoVector2(0.6, 0.8), 40, 10);
            var ray = new GeoRay2(new GeoPoint2(1, 2), new GeoVector2(0.6, 0.8));
            var xline = new GeoXLine2(new GeoPoint2(1, 2), new GeoVector2(0.8, -0.6));
            var spline = new GeoSpline2(3, new[] { new GeoPoint2(0, 0), new GeoPoint2(1, 2.5), new GeoPoint2(3, 2.5), new GeoPoint2(4, 0.1) }, new[] { 0.0, 0, 0, 0, 1, 1, 1, 1 }, null);
            var chain = new GeoPolylineArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(10.5, 0), new GeoPoint2(10.5, 7.25) }, new[] { 0.0, 0.3, 0.0 });
            var loop = new GeoPolygonArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(10.5, 0), new GeoPoint2(10.5, 7.25), new GeoPoint2(0, 7.25) }, new[] { 0.0, -0.4, 0.0, 0.2 });
            var straight = new GeoPolygon2(new GeoPoint2(1, 1), new GeoPoint2(9, 1), new GeoPoint2(5, 8));

            ByHandle<DxfLine>(db, "11").SetFromGeCurve(line);
            ByHandle<DxfCircle>(db, "22").SetFromGeCurve(circle);
            ByHandle<DxfEllipse>(db, "40").SetFromGeCurve(ellipse);
            ByHandle<DxfRay>(db, "52").SetFromGeCurve(ray);
            ByHandle<DxfXline>(db, "51").SetFromGeCurve(xline);
            ByHandle<DxfSpline>(db, "61").SetFromGeCurve(spline);
            ByHandle<DxfPolyline>(db, "82").SetFromGeCurve(chain);
            ByHandle<DxfPolyline>(db, "83").SetFromGeCurve(loop);
            ByHandle<DxfPolyline2d>(db, "90").SetFromGeCurve(loop);
            ByHandle<DxfPolyline3d>(db, "93").SetFromGeCurve(straight);

            foreach (DxfDatabase again in new[] { db, Opened(Saved(db)) })
            {
                Assert.Equal(line, ByHandle<DxfLine>(again, "11").ToGeoLine2());
                Assert.Equal(circle, ByHandle<DxfCircle>(again, "22").ToGeoCircle2());
                Assert.Equal(ellipse, ByHandle<DxfEllipse>(again, "40").ToGeoEllipse2());
                Assert.Equal(ray, ByHandle<DxfRay>(again, "52").ToGeoRay2());
                Assert.Equal(xline, ByHandle<DxfXline>(again, "51").ToGeoXLine2());
                Assert.Equal(spline, ByHandle<DxfSpline>(again, "61").ToGeoSpline2());
                Assert.Equal(chain, ByHandle<DxfPolyline>(again, "82").ToGeoPolylineArc2());
                Assert.Equal(loop, ByHandle<DxfPolyline>(again, "83").ToGeoPolygonArc2());
                Assert.Equal(loop, ByHandle<DxfPolyline2d>(again, "90").ToGeoPolygonArc2());
                Assert.Equal(straight, ByHandle<DxfPolyline3d>(again, "93").ToGeoPolygon2());
                Assert.Equal(new GeoPoint3(0.1, 0.2, 0), ByHandle<DxfLine>(again, "11").ToGeoLine3().StartPoint);
            }
        }

        [Fact]
        public void AnArcOfThePlaneSet_ComesBackOverTheSamePoints_ItsAnglesThroughDegrees()
        {
            DxfDatabase db = Drawing();
            var clockwise = new GeoArc2(new GeoPoint2(3, 4), 12.5, 1.0, 0.25, true);

            ByHandle<DxfArc>(db, "31").SetFromGeCurve(clockwise);
            GeoArc2 back = ByHandle<DxfArc>(Opened(Saved(db)), "31").ToGeoArc2();

            Assert.False(back.IsClockwise);
            Assert.True(back.StartPoint.IsEqualTo(clockwise.EndPoint, Tol));
            Assert.True(back.EndPoint.IsEqualTo(clockwise.StartPoint, Tol));
            Assert.Equal(Bits(12.5), Bits(back.Radius));
        }

        #endregion
    }
}
