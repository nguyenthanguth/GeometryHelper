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
    /// The way back from the library's shapes: <see cref="DxfCurve.SetFromGeCurve"/> given a shape of the plane, as
    /// <see cref="DxfWriter"/> writes it at z = 0 on the normal (0, 0, 1), and a polyline given a chain or loop, as the writer
    /// writes one as that polyline's type; each read back to the bit, its handle and layer kept. With
    /// <see cref="DxfCurve.CreateFromGeCurve"/> of the same shapes, and AutoCAD's <c>GetLineSegment2dAt</c> and
    /// <c>GetArcSegment2dAt</c> of a tilted and a mirrored polyline.
    /// </summary>
    public class DxfSetFromGeCurveOfThePlaneTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static readonly string Text = R2000(
            G(0, "LINE", 5, "10", 102, "{ACAD_REACTORS", 330, "2F", 102, "}", 100, "AcDbEntity", 8, "steel", 100, "AcDbLine", 39, 2.0, 10, 0.0, 20, 0.0, 30, 5.0, 11, 100.0, 21, 50.0, 31, 5.0, 210, 0.0, 220, 0.6, 230, 0.8, 1001, "ACAD", 1000, "kept")
            + G(0, "CIRCLE", 5, "20", 100, "AcDbEntity", 8, "steel", 100, "AcDbCircle", 10, 10.0, 20, 20.0, 30, 0.0, 40, 5.0, 210, 0.0, 220, 0.6, 230, 0.8)
            + G(0, "ARC", 5, "30", 100, "AcDbEntity", 8, "steel", 100, "AcDbCircle", 10, 0.0, 20, 0.0, 30, 0.0, 40, 10.0, 210, 0.0, 220, 0.0, 230, -1.0, 100, "AcDbArc", 50, 0.0, 51, 90.0)
            + G(0, "ELLIPSE", 5, "40", 100, "AcDbEntity", 8, "steel", 100, "AcDbEllipse", 10, 0.0, 20, 0.0, 30, 3.0, 11, 60.0, 21, 0.0, 31, 0.0, 210, 0.0, 220, 0.0, 230, 1.0, 40, 0.5, 41, 0.0, 42, 2 * Math.PI)
            + G(0, "RAY", 5, "50", 100, "AcDbEntity", 8, "steel", 100, "AcDbRay", 10, 0.0, 20, 0.0, 30, 7.0, 11, 0.0, 21, 0.0, 31, 1.0)
            + G(0, "XLINE", 5, "51", 100, "AcDbEntity", 8, "steel", 100, "AcDbXline", 10, 0.0, 20, 0.0, 30, 7.0, 11, 0.0, 21, 0.0, 31, 1.0)
            + G(0, "SPLINE", 5, "60", 100, "AcDbEntity", 8, "steel", 100, "AcDbSpline", 70, 0, 71, 3, 72, 8, 73, 4, 74, 0,
                40, 0.0, 40, 0.0, 40, 0.0, 40, 0.0, 40, 1.0, 40, 1.0, 40, 1.0, 40, 1.0,
                10, 0.0, 20, 0.0, 30, 0.0, 10, 100.0, 20, 150.0, 30, 40.0, 10, 200.0, 20, 150.0, 30, 0.0, 10, 300.0, 20, 0.0, 30, 0.0)
            + G(0, "LWPOLYLINE", 5, "80", 102, "{ACAD_REACTORS", 330, "2F", 102, "}", 100, "AcDbEntity", 8, "steel", 100, "AcDbPolyline", 90, 3, 70, 128, 43, 0.5, 38, 4.0,
                10, 0.0, 20, 0.0, 10, 10.0, 20, 0.0, 10, 10.0, 20, 10.0, 210, 0.0, 220, 0.6, 230, 0.8, 1001, "ACAD", 1000, "kept")
            + G(0, "LWPOLYLINE", 5, "81", 100, "AcDbEntity", 8, "0", 100, "AcDbPolyline", 90, 4, 70, 1, 43, 0.25,
                10, 0.0, 20, 0.0, 10, 10.0, 20, 0.0, 42, 0.5, 10, 10.0, 20, 10.0, 10, 0.0, 20, 10.0, 42, -0.3, 210, 0.0, 220, 0.6, 230, 0.8)
            + G(0, "LWPOLYLINE", 5, "82", 100, "AcDbEntity", 8, "0", 100, "AcDbPolyline", 90, 3, 70, 0,
                10, 0.0, 20, 0.0, 10, 10.0, 20, 0.0, 42, 0.5, 10, 10.0, 20, 10.0, 210, 0.0, 220, 0.0, 230, -1.0)
            + G(0, "LWPOLYLINE", 5, "83", 100, "AcDbEntity", 8, "steel", 100, "AcDbPolyline", 90, 2, 70, 0, 38, 5.0, 10, 0.0, 20, 0.0, 10, 10.0, 20, 0.0)
            + G(0, "LWPOLYLINE", 5, "84", 100, "AcDbEntity", 8, "0", 100, "AcDbPolyline", 90, 2, 70, 0, 38, 5.0, 10, 0.0, 20, 0.0, 10, 10.0, 20, 0.0, 210, 0.0, 220, 0.6, 230, 0.8)
            + G(0, "LWPOLYLINE", 5, "85", 100, "AcDbEntity", 8, "0", 100, "AcDbPolyline", 90, 2, 70, 0, 38, 5.0, 10, 0.0, 20, 0.0, 10, 10.0, 20, 0.0, 210, 0.0, 220, 0.0, 230, -1.0)
            + G(0, "LWPOLYLINE", 5, "86", 100, "AcDbEntity", 8, "0", 100, "AcDbPolyline", 90, 4, 70, 1, 38, 5.0,
                10, 0.0, 20, 0.0, 10, 10.0, 20, 0.0, 42, 0.5, 10, 10.0, 20, 10.0, 10, 0.0, 20, 10.0, 210, 0.0, 220, 0.0, 230, -1.0)
            + G(0, "LWPOLYLINE", 5, "87", 100, "AcDbEntity", 8, "0", 100, "AcDbPolyline", 90, 4, 70, 1, 43, 0.5, 38, 5.0,
                10, 0.0, 20, 0.0, 10, 10.0, 20, 0.0, 42, 0.5, 10, 10.0, 20, 10.0, 10, 0.0, 20, 10.0, 210, 0.0, 220, 0.0, 230, -1.0)
            + G(0, "POLYLINE", 5, "90", 100, "AcDbEntity", 8, "steel", 100, "AcDb2dPolyline", 66, 1, 10, 0.0, 20, 0.0, 30, 2.0, 70, 0, 40, 1.0, 41, 1.0)
            + G(0, "VERTEX", 5, "91", 100, "AcDbEntity", 8, "steel", 100, "AcDbVertex", 100, "AcDb2dVertex", 10, 0.0, 20, 0.0, 30, 2.0, 70, 0)
            + G(0, "VERTEX", 5, "92", 100, "AcDbEntity", 8, "steel", 100, "AcDbVertex", 100, "AcDb2dVertex", 10, 5.0, 20, 0.0, 30, 2.0, 70, 0)
            + G(0, "SEQEND", 5, "93", 100, "AcDbEntity", 8, "steel")
            + G(0, "POLYLINE", 5, "A0", 100, "AcDbEntity", 8, "steel", 100, "AcDb3dPolyline", 66, 1, 10, 0.0, 20, 0.0, 30, 0.0, 70, 8)
            + G(0, "VERTEX", 5, "A1", 100, "AcDbEntity", 8, "steel", 100, "AcDbVertex", 100, "AcDb3dPolylineVertex", 10, 0.0, 20, 0.0, 30, 2.0, 70, 32)
            + G(0, "VERTEX", 5, "A2", 100, "AcDbEntity", 8, "steel", 100, "AcDbVertex", 100, "AcDb3dPolylineVertex", 10, 5.0, 20, 0.0, 30, 9.0, 70, 32)
            + G(0, "SEQEND", 5, "A3", 100, "AcDbEntity", 8, "steel"));

        private static object Plan(DxfDatabase db, string handle)
            => DxfReader.Parse(db.ToText(), new DxfReadOptions { PlanAs2D = true, Tolerance = Tol }).Shapes.Single(s => s.Handle == handle).Shape;

        private static object Space(DxfDatabase db, string handle)
            => DxfReader.Parse(db.ToText(), new DxfReadOptions { Tolerance = Tol }).Shapes.Single(s => s.Handle == handle).Shape;

        /// <summary>The x and y axes of the system of a normal, by the arbitrary axis algorithm of the DXF reference.</summary>
        private static void Axes(GeoVector3 n, out GeoVector3 u, out GeoVector3 v)
        {
            u = (Math.Abs(n.X) < 1.0 / 64.0 && Math.Abs(n.Y) < 1.0 / 64.0 ? new GeoVector3(0, 1, 0).CrossProduct(n) : new GeoVector3(0, 0, 1).CrossProduct(n)).Normalize();
            v = n.CrossProduct(u).Normalize();
        }

        #region Curves given a shape of the plane

        [Fact]
        public void ACurveGivenAShapeOfThePlane_IsWrittenAtZNoughtOnTheNormalUp_ItsHandleLayerAndThicknessKept()
        {
            DxfDatabase db = DxfDatabase.Parse(Text);
            var line = new GeoLine2(new GeoPoint2(0.1, 0.2), new GeoPoint2(30.3, 40.7));
            var circle = new GeoCircle2(new GeoPoint2(1.25, -3.5), 7.125);
            var ellipse = new GeoEllipse2(new GeoPoint2(5, 6), new GeoVector2(0.6, 0.8), 40, 10);
            var ray = new GeoRay2(new GeoPoint2(1, 2), new GeoVector2(0.6, 0.8));
            var xline = new GeoXLine2(new GeoPoint2(1, 2), new GeoVector2(0.8, -0.6));
            var spline = new GeoSpline2(3, new[] { new GeoPoint2(0, 0), new GeoPoint2(1, 2.5), new GeoPoint2(3, 2.5), new GeoPoint2(4, 0.1) }, new[] { 0.0, 0, 0, 0, 1, 1, 1, 1 }, null);

            ByHandle<DxfLine>(db, "10").SetFromGeCurve(line);
            ByHandle<DxfCircle>(db, "20").SetFromGeCurve(circle);
            ByHandle<DxfEllipse>(db, "40").SetFromGeCurve(ellipse);
            ByHandle<DxfRay>(db, "50").SetFromGeCurve(ray);
            ByHandle<DxfXline>(db, "51").SetFromGeCurve(xline);
            ByHandle<DxfSpline>(db, "60").SetFromGeCurve(spline);

            DxfDatabase again = Opened(Saved(db));
            Assert.Equal(line, Plan(again, "10"));
            Assert.Equal(circle, Plan(again, "20"));
            Assert.Equal(ellipse, Plan(again, "40"));
            Assert.Equal(ray, Plan(again, "50"));
            Assert.Equal(xline, Plan(again, "51"));
            Assert.Equal(spline, Plan(again, "60"));

            DxfLine written = ByHandle<DxfLine>(again, "10");
            Assert.Equal("steel", written.Layer);
            Assert.Equal(2.0, written.Thickness);
            Assert.Equal(new GeoVector3(0, 0, 1), written.Normal);
            Assert.Equal(new GeoPoint3(0.1, 0.2, 0), written.StartPoint);
            Assert.Equal(new GeoVector3(0, 0, 1), ByHandle<DxfCircle>(again, "20").Normal);
            Assert.All(new[] { "10", "20", "40", "50", "51", "60" }, h => Assert.Equal("steel", ByHandle<DxfEntity>(again, h).Layer));
        }

        [Fact]
        public void AnArcGivenAClockwiseArcOfThePlane_ComesBackCounterClockwiseOverTheSamePoints()
        {
            DxfDatabase db = DxfDatabase.Parse(Text);
            var clockwise = new GeoArc2(new GeoPoint2(3, 4), 12.5, 1.0, 0.25, true);

            ByHandle<DxfArc>(db, "30").SetFromGeCurve(clockwise);
            var back = Assert.IsType<GeoArc2>(Plan(db, "30"));

            Assert.False(back.IsClockwise);
            Assert.True(back.StartPoint.IsEqualTo(clockwise.EndPoint, Tol));
            Assert.True(back.EndPoint.IsEqualTo(clockwise.StartPoint, Tol));
            Assert.Equal(new GeoVector3(0, 0, 1), ByHandle<DxfArc>(db, "30").Normal);
            Assert.Throws<ArgumentException>(() => ByHandle<DxfArc>(db, "30").SetFromGeCurve(new GeoArc2(new GeoPoint2(0, 0), 5, 0, 2 * Math.PI)));
        }

        [Fact]
        public void ACurveGivenAShapeOfSpace_IsSetAsBefore_ALineKeepingItsNormal()
        {
            DxfDatabase db = DxfDatabase.Parse(Text);
            DxfLine line = ByHandle<DxfLine>(db, "10");

            line.SetFromGeCurve(new GeoLine3(new GeoPoint3(1, 2, 3), new GeoPoint3(4, 5, 6)));

            Assert.Equal(new GeoVector3(0, 0.6, 0.8), line.Normal);
            Assert.Equal(new GeoPoint3(4, 5, 6), line.EndPoint);
            Assert.Throws<ArgumentException>(() => line.SetFromGeCurve(new GeoCircle2(new GeoPoint2(0, 0), 1)));
            Assert.Throws<ArgumentException>(() => line.SetFromGeCurve(null));
        }

        #endregion

        #region Polylines given a chain or a loop

        [Fact]
        public void ALightweightPolylineGivenAChainOrLoopOfThePlane_ReadsBackToTheBit_ItsHandleAndLayerKept_ItsWidthsNot()
        {
            DxfDatabase db = DxfDatabase.Parse(Text);
            DxfPolyline polyline = ByHandle<DxfPolyline>(db, "80");
            var loop = new GeoPolygonArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(10.5, 0), new GeoPoint2(10.5, 7.25), new GeoPoint2(0, 7.25) }, new[] { 0.0, -0.4, 0.0, 0.2 });
            var chain = new GeoPolyline2(new GeoPoint2(1, 1), new GeoPoint2(9, 1), new GeoPoint2(5, 8));

            polyline.SetFromGeCurve(loop);

            Assert.Same(polyline, ByHandle<DxfPolyline>(db, "80"));
            Assert.True(polyline.Closed);
            Assert.Equal(4, polyline.NumberOfVertices);
            Assert.Equal(0.0, polyline.ConstantWidth);
            Assert.False(polyline.Plinegen);
            Assert.Equal(0.0, polyline.Elevation);
            Assert.Equal(new GeoVector3(0, 0, 1), polyline.Normal);
            Assert.Equal("steel", polyline.Layer);
            Assert.Equal(loop, Plan(Opened(Saved(db)), "80"));

            polyline.SetFromGeCurve(chain);

            Assert.False(polyline.Closed);
            Assert.Equal(chain, Plan(Opened(Saved(db)), "80"));
        }

        [Fact]
        public void ALightweightPolylineGivenALoopOfATiltedPlane_IsWrittenInThatPlane()
        {
            DxfDatabase db = DxfDatabase.Parse(Text);
            var tilted = Assert.IsType<GeoPolygonArc3>(Space(db, "81"));
            DxfPolyline polyline = ByHandle<DxfPolyline>(db, "80");

            polyline.SetFromGeCurve(tilted);

            var back = Assert.IsType<GeoPolygonArc3>(Space(db, "80"));
            Assert.True(polyline.Closed);
            Assert.True(polyline.Normal.IsEqualTo(new GeoVector3(0, 0.6, 0.8), Tol));
            Assert.Equal(tilted.VertexCount, back.VertexCount);

            for (int i = 0; i < tilted.VertexCount; i++)
            {
                Assert.True(tilted.Vertices[i].DistanceTo(back.Vertices[i]) <= 1E-12, i + ": " + back.Vertices[i]);
                Assert.Equal(tilted.Bulges[i], back.Bulges[i], 12);
            }

            // A loop of straight edges in that plane, as a polygon of space gives it.
            var square = new GeoPolygon3(tilted.Vertices, Tol);
            polyline.SetFromGeCurve(square);
            Assert.True(Assert.IsType<GeoPolygon3>(Space(db, "80")).Vertices.Zip(square.Vertices, (a, b) => a.DistanceTo(b)).Max() <= 1E-12);
        }

        [Fact]
        public void ALightweightPolylineGivenAChainOutOfOnePlane_OrAFace_IsRefused_AndLeftAsItWas()
        {
            DxfDatabase db = DxfDatabase.Parse(Text);
            DxfPolyline polyline = ByHandle<DxfPolyline>(db, "80");
            byte[] before = Saved(db);
            var bent = new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(10, 10, 5), new GeoPoint3(0, 10, -3));
            var square = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(4, 0), new GeoPoint2(4, 4), new GeoPoint2(0, 4));
            var hole = new GeoPolygon2(new GeoPoint2(1, 1), new GeoPoint2(2, 1), new GeoPoint2(2, 2), new GeoPoint2(1, 2));

            Assert.Contains("does not lie in one plane", Assert.Throws<ArgumentException>(() => polyline.SetFromGeCurve(bent)).Message);
            Assert.Throws<ArgumentException>(() => polyline.SetFromGeCurve(new GeoFace2(square, new[] { hole })));
            Assert.Throws<ArgumentException>(() => polyline.SetFromGeCurve(new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(1, 0))));
            Assert.Throws<ArgumentException>(() => polyline.SetFromGeCurve(null));
            Assert.Equal(before, Saved(db));
        }

        [Fact]
        public void APolylineOfThePlaneGivenALoop_StaysAPolylineOfThePlane_ReadBackToTheBit()
        {
            DxfDatabase db = DxfDatabase.Parse(Text);
            DxfPolyline2d polyline = ByHandle<DxfPolyline2d>(db, "90");
            var loop = new GeoPolygonArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(10.5, 0), new GeoPoint2(10.5, 7.25), new GeoPoint2(0, 7.25) }, new[] { 0.0, -0.4, 0.0, 0.2 });

            polyline.SetFromGeCurve(loop);

            DxfDatabase again = Opened(Saved(db));
            DxfPolyline2d back = ByHandle<DxfPolyline2d>(again, "90");
            Assert.True(back.Closed);
            Assert.Equal(4, back.Count());
            Assert.Equal(0.0, back.Elevation);
            Assert.Equal("steel", back.Layer);
            Assert.Equal(loop, Plan(again, "90"));
            Assert.Equal("POLYLINE", DxfReader.Parse(again.ToText(), new DxfReadOptions { Tolerance = Tol }).Shapes.Single(s => s.Handle == "90").Type);
        }

        [Fact]
        public void APolylineOfThePlaneInAFileOfR12GivenALoop_IsWrittenWithoutMarkers_ReadBackToTheBit()
        {
            string text = R12(
                G(0, "POLYLINE", 8, "steel", 66, 1, 10, 0.0, 20, 0.0, 30, 0.0, 70, 0)
                + G(0, "VERTEX", 8, "steel", 10, 0.0, 20, 0.0, 30, 0.0)
                + G(0, "VERTEX", 8, "steel", 10, 5.0, 20, 0.0, 30, 0.0)
                + G(0, "SEQEND", 8, "steel"));
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfPolyline2d polyline = db.ModelSpace.OfType<DxfPolyline2d>().Single();
            var loop = new GeoPolygonArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(10.5, 0), new GeoPoint2(10.5, 7.25) }, new[] { 0.0, 0.3, 0.0 });

            polyline.SetFromGeCurve(loop);

            string saved = db.ToText();
            Assert.DoesNotContain("AcDb", saved);
            Assert.Equal(loop, DxfReader.Parse(saved, new DxfReadOptions { PlanAs2D = true, Tolerance = Tol }).Shapes.Single().Shape);
            Assert.Equal("steel", db.ModelSpace.OfType<DxfPolyline2d>().Single().Layer);
        }

        [Fact]
        public void A3dPolylineGivenAChainOfThePlane_IsWrittenAtZNought_AndAPolygonClosed()
        {
            DxfDatabase db = DxfDatabase.Parse(Text);
            DxfPolyline3d polyline = ByHandle<DxfPolyline3d>(db, "A0");
            var chain = new GeoPolyline2(new GeoPoint2(1, 1), new GeoPoint2(9, 1), new GeoPoint2(5, 8));
            var triangle = new GeoPolygon2(new GeoPoint2(1, 1), new GeoPoint2(9, 1), new GeoPoint2(5, 8));
            var space = new GeoPolygon3(new[] { new GeoPoint3(0, 0, 1), new GeoPoint3(5, 0, 2), new GeoPoint3(5, 5, 3) }, Tol);

            polyline.SetFromGeCurve(chain);
            Assert.False(polyline.Closed);
            Assert.Equal(chain, Plan(Opened(Saved(db)), "A0"));
            Assert.Equal(new GeoPoint3(9, 1, 0), Assert.IsType<GeoPolyline3>(Space(db, "A0")).Vertices[1]);

            polyline.SetFromGeCurve(triangle);
            Assert.True(polyline.Closed);
            Assert.Equal(triangle, Plan(Opened(Saved(db)), "A0"));

            polyline.SetFromGeCurve(space);
            Assert.Equal(space.Vertices, Assert.IsType<GeoPolygon3>(Space(db, "A0")).Vertices);
            Assert.Throws<ArgumentException>(() => polyline.SetFromGeCurve(new GeoPolylineArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(1, 0) }, new[] { 0.5, 0.0 })));
        }

        #endregion

        #region Chains on one line, an unchanged writing, what is kept

        [Fact]
        public void ALightweightPolylineOfOneSegment_TakesTheStraightChainItGives_OnItsOwnNormal_ToTheBit()
        {
            DxfDatabase db = DxfDatabase.Parse(Text);
            DxfPolyline one = ByHandle<DxfPolyline>(db, "83");
            GeoPolyline3 chain = one.ToGeoPolyline3();
            var copy = new GeoPolyline3(chain.Vertices[0], chain.Vertices[1]);
            var longer = new GeoPolyline3(new GeoPoint3(0, 0, 5), new GeoPoint3(20, 0, 5));

            one.SetFromGeCurve(copy);
            Assert.Equal(copy, one.ToGeoPolyline3());

            one.SetFromGeCurve(longer);
            DxfPolyline back = ByHandle<DxfPolyline>(Opened(Saved(db)), "83");
            Assert.Equal(longer, back.ToGeoPolyline3());
            Assert.Equal("83", back.Handle);
            Assert.Equal("steel", back.Layer);
            Assert.Equal(5.0, back.Elevation);
            Assert.Equal(new GeoVector3(0, 0, 1), back.Normal);
        }

        [Fact]
        public void ATiltedPolylineGivenThreeVerticesOnALineInItsPlane_KeepsItsNormal()
        {
            DxfDatabase db = DxfDatabase.Parse(Text);
            DxfPolyline tilted = ByHandle<DxfPolyline>(db, "84");
            GeoPoint3 start = tilted.GetPoint3dAt(0);
            GeoVector3 along = start.GetVectorTo(tilted.GetPoint3dAt(1));
            var chain = new GeoPolyline3(start, start.Add(along.Multiply(0.5)), start.Add(along.Multiply(1.5)));

            tilted.SetFromGeCurve(chain);

            GeoPolyline3 back = tilted.ToGeoPolyline3();
            Assert.Equal(new GeoVector3(0, 0.6, 0.8), tilted.Normal);
            Assert.Equal(3, back.VertexCount);
            Assert.All(Enumerable.Range(0, 3), i => Assert.True(back.Vertices[i].DistanceTo(chain.Vertices[i]) <= 1E-12, i + ": " + back.Vertices[i]));
        }

        [Fact]
        public void APolylineMirroredOnMinusZGivenAChainOfTwoVertices_StaysMirrored_ToTheBit()
        {
            DxfDatabase db = DxfDatabase.Parse(Text);
            DxfPolyline mirrored = ByHandle<DxfPolyline>(db, "85");
            var chain = new GeoPolyline3(new GeoPoint3(1, 2, -5), new GeoPoint3(7, 2, -5));

            mirrored.SetFromGeCurve(chain);

            Assert.Equal(new GeoVector3(0, 0, -1), mirrored.Normal);
            Assert.Equal(chain, ByHandle<DxfPolyline>(Opened(Saved(db)), "85").ToGeoPolyline3());
        }

        [Fact]
        public void AChainOnALineOffThePolylinesPlane_IsRefused_AndTheDrawingLeftAsItWas()
        {
            DxfDatabase db = DxfDatabase.Parse(Text);
            byte[] before = Saved(db);
            var upright = new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(0, 0, 10));

            string lightweight = Assert.Throws<ArgumentException>(() => ByHandle<DxfPolyline>(db, "83").SetFromGeCurve(upright)).Message;
            string plane = Assert.Throws<ArgumentException>(() => ByHandle<DxfPolyline2d>(db, "90").SetFromGeCurve(upright)).Message;

            Assert.Contains("its vertices are on one line, not in the plane of the polyline's normal", lightweight);
            Assert.Contains("its vertices are on one line, not in the plane of the polyline's normal", plane);
            Assert.DoesNotContain("DxfPolyline3d", lightweight);
            Assert.Equal(before, Saved(db));
        }

        [Fact]
        public void AChainOf10MetresRisingATenthOfAMillimetre_IsNotFlattenedOntoThePolylinesPlane_ButRefused()
        {
            DxfDatabase db = DxfDatabase.Parse(Text);
            byte[] before = Saved(db);
            var rising = new GeoPolyline3(new GeoPoint3(0, 0, 5), new GeoPoint3(10000, 0, 5.1));

            Assert.Contains("its vertices are on one line", Assert.Throws<ArgumentException>(() => ByHandle<DxfPolyline>(db, "83").SetFromGeCurve(rising)).Message);
            Assert.Equal(before, Saved(db));
        }

        [Fact]
        public void AChainOf10MillimetresRisingHalfAThousandth_IsTakenOnThePolylinesNormal_WithinThePointTolerance()
        {
            DxfDatabase db = DxfDatabase.Parse(Text);
            DxfPolyline polyline = ByHandle<DxfPolyline>(db, "83");
            var rising = new GeoPolyline3(new GeoPoint3(0, 0, 5), new GeoPoint3(10, 0, 5.0005));

            polyline.SetFromGeCurve(rising);

            GeoPolyline3 back = polyline.ToGeoPolyline3();
            Assert.Equal(new GeoVector3(0, 0, 1), polyline.Normal);
            Assert.Equal(5.0, polyline.Elevation);
            Assert.Equal(rising.Vertices[0], back.Vertices[0]);
            Assert.True(back.Vertices[1].DistanceTo(rising.Vertices[1]) <= Tol.EqualPoint);
        }

        [Fact]
        public void ThreeVerticesAHairOffALine_StayOnThePolylinesNormalUp()
        {
            DxfDatabase db = DxfDatabase.Parse(Text);
            DxfPolyline polyline = ByHandle<DxfPolyline>(db, "83");
            var chain = new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(50, 0, 0.0009), new GeoPoint3(100, 0, 0));

            polyline.SetFromGeCurve(chain);

            GeoPolyline3 back = polyline.ToGeoPolyline3();
            Assert.Equal(new GeoVector3(0, 0, 1), polyline.Normal);
            Assert.Equal(0.0, polyline.Elevation);
            Assert.Equal(3, back.VertexCount);
            Assert.All(Enumerable.Range(0, 3), i => Assert.True(back.Vertices[i].DistanceTo(chain.Vertices[i]) <= Tol.EqualPoint, i + ": " + back.Vertices[i]));
        }

        [Fact]
        public void APolylineMirroredOnMinusZGivenItsLoopEditedInItsPlane_StaysMirrored()
        {
            DxfDatabase db = DxfDatabase.Parse(Text);
            DxfPolyline mirrored = ByHandle<DxfPolyline>(db, "86");
            GeoPolygonArc3 loop = mirrored.ToGeoPolygonArc3();
            var edited = new GeoPolygonArc3(loop.Vertices.Select(v => v.Add(new GeoVector3(1, 2, 0))), loop.Bulges, loop.Normals, Tol);

            mirrored.SetFromGeCurve(edited);

            GeoPolygonArc3 back = ByHandle<DxfPolyline>(Opened(Saved(db)), "86").ToGeoPolygonArc3();
            Assert.Equal(new GeoVector3(0, 0, -1), mirrored.Normal);
            Assert.Equal(5.0, mirrored.Elevation);
            Assert.Equal(edited.VertexCount, back.VertexCount);
            Assert.All(Enumerable.Range(0, edited.VertexCount), i => Assert.True(back.Vertices[i].DistanceTo(edited.Vertices[i]) <= Tol.EqualPoint, i + ": " + back.Vertices[i]));
            Assert.Equal(edited.Area, back.Area, 9);
        }

        [Theory]
        [InlineData(0.001)]
        [InlineData(0.004)]
        [InlineData(1E-7)]
        public void AChainWhoseHalfCircleIsTurnedOffThePolylinesPlane_IsFlattenedOnlyWhereItStandsOffItByLessThanTheTolerance(double turn)
        {
            // (0, 0, 0) to (200, 0, 0), a half circle of radius 100 about (0, -sin a, cos a), then on to (300, 0, 0): turned by a,
            // the half circle stands 100 sin a off the floor, 0.1 at a = 0.001 and 1E-5 at a = 1E-7.
            DxfDatabase db = DxfDatabase.Parse(Text);
            DxfPolyline polyline = ByHandle<DxfPolyline>(db, "83");
            var about = new GeoVector3(0, -Math.Sin(turn), Math.Cos(turn));
            var chain = new GeoPolylineArc3(new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(200, 0, 0), new GeoPoint3(300, 0, 0) }, new[] { 1.0, 0.0, 0.0 }, new[] { about, default(GeoVector3), default(GeoVector3) }, Tol);

            polyline.SetFromGeCurve(chain);

            GeoPolylineArc3 back = ByHandle<DxfPolyline>(Opened(Saved(db)), "83").ToGeoPolylineArc3();
            GeoEdge3[] given = chain.GetEdges().ToArray();
            GeoEdge3[] read = back.GetEdges().ToArray();
            Assert.Equal(given.Length, read.Length);

            for (int i = 0; i < given.Length; i++)
            {
                Assert.True(read[i].StartPoint.DistanceTo(given[i].StartPoint) <= Tol.EqualPoint, i + " start " + read[i].StartPoint);
                Assert.True(read[i].MidPoint.DistanceTo(given[i].MidPoint) <= Tol.EqualPoint, i + " middle " + read[i].MidPoint);
                Assert.True(read[i].EndPoint.DistanceTo(given[i].EndPoint) <= Tol.EqualPoint, i + " end " + read[i].EndPoint);
            }

            // Within the tolerance it is taken on the polyline's own normal; beyond it, written in its own plane.
            Assert.Equal(turn < 1E-5, polyline.Normal.Equals(new GeoVector3(0, 0, 1)));
        }

        [Fact]
        public void AMirroredPolylineWithAWidthGivenACopyOfItsOwnLoop_IsLeftAsItIs_ToTheByte()
        {
            DxfDatabase db = DxfDatabase.Parse(Text);
            DxfPolyline polyline = ByHandle<DxfPolyline>(db, "87");
            GeoPolygonArc3 own = polyline.ToGeoPolygonArc3();
            byte[] before = Saved(db);

            polyline.SetFromGeCurve(own);
            polyline.SetFromGeCurve(new GeoPolygonArc3(own.Vertices, own.Bulges, own.Normals, Tol));

            Assert.Equal(before, Saved(db));
            Assert.Equal(0.5, polyline.ConstantWidth);
            Assert.Equal(new GeoVector3(0, 0, -1), polyline.Normal);
        }

        [Fact]
        public void APolylineGivenTheShapeItHas_IsLeftAsItIs_WidthsAndAll_AndGivenAnotherLosesItsWidths()
        {
            DxfDatabase db = DxfDatabase.Parse(Text);
            DxfPolyline polyline = ByHandle<DxfPolyline>(db, "81");
            GeoPolygonArc3 loop = polyline.ToGeoPolygonArc3();
            byte[] before = Saved(db);

            polyline.SetFromGeCurve(loop);
            polyline.SetFromGeCurve(new GeoPolygonArc3(loop.Vertices, loop.Bulges, loop.Normals, Tol));
            Assert.Equal(before, Saved(db));
            Assert.Equal(0.25, polyline.ConstantWidth);

            polyline.SetFromGeCurve(new GeoPolygonArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10) }, new[] { 0.0, 0.5, 0.0 }));
            Assert.Equal(0.0, polyline.ConstantWidth);
            Assert.Equal(0.0, polyline.GetStartWidthAt(0));
        }

        [Fact]
        public void ALineAndALightweightPolylineGivenAShapeOfThePlane_KeepTheirExtendedDataAndReactors()
        {
            DxfDatabase db = DxfDatabase.Parse(Text);

            ByHandle<DxfLine>(db, "10").SetFromGeCurve(new GeoLine2(new GeoPoint2(1, 2), new GeoPoint2(3, 4)));
            ByHandle<DxfPolyline>(db, "80").SetFromGeCurve(new GeoPolygonArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10) }, new[] { 0.0, 0.5, 0.0 }));
            DxfDatabase again = Opened(Saved(db));

            foreach (string handle in new[] { "10", "80" })
            {
                Assert.Contains(ByHandle<DxfEntity>(again, handle).XData, v => v.TypeCode == 1000 && v.Value == "kept");
                string record = Records(again.ToText()).Single(r => HandleOf(r) == handle);
                Assert.Contains("{ACAD_REACTORS", record);
                Assert.Contains("\n2F\n", record);
            }
        }

        [Fact]
        public void APolylineOfThePlaneGivenALoopOfATiltedPlane_IsWrittenInThatPlane()
        {
            DxfDatabase db = DxfDatabase.Parse(Text);
            var tilted = Assert.IsType<GeoPolygonArc3>(Space(db, "81"));
            DxfPolyline2d polyline = ByHandle<DxfPolyline2d>(db, "90");

            polyline.SetFromGeCurve(tilted);

            var back = Assert.IsType<GeoPolygonArc3>(Space(db, "90"));
            Assert.Same(polyline, ByHandle<DxfPolyline2d>(db, "90"));
            Assert.True(polyline.Normal.IsEqualTo(new GeoVector3(0, 0.6, 0.8), Tol));
            Assert.Equal(tilted.VertexCount, back.VertexCount);
            Assert.All(Enumerable.Range(0, tilted.VertexCount), i => Assert.True(tilted.Vertices[i].DistanceTo(back.Vertices[i]) <= 1E-12, i + ": " + back.Vertices[i]));
        }

        #endregion

        #region Curves made of a shape

        [Fact]
        public void CreateFromGeCurve_MakesTheCurveOfEachShapeOfThePlane_ReadBackToTheBit()
        {
            var shapes = new object[]
            {
                new GeoLine2(new GeoPoint2(0.1, 0.2), new GeoPoint2(30.3, 40.7)),
                new GeoCircle2(new GeoPoint2(1.25, -3.5), 7.125),
                new GeoEllipse2(new GeoPoint2(5, 6), new GeoVector2(0.6, 0.8), 40, 10),
                new GeoRay2(new GeoPoint2(1, 2), new GeoVector2(0.6, 0.8)),
                new GeoXLine2(new GeoPoint2(1, 2), new GeoVector2(0.8, -0.6)),
                new GeoSpline2(3, new[] { new GeoPoint2(0, 0), new GeoPoint2(1, 2.5), new GeoPoint2(3, 2.5), new GeoPoint2(4, 0.1) }, new[] { 0.0, 0, 0, 0, 1, 1, 1, 1 }, null),
                new GeoPolyline2(new GeoPoint2(1, 1), new GeoPoint2(9, 1), new GeoPoint2(5, 8)),
                new GeoPolygonArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(10.5, 0), new GeoPoint2(10.5, 7.25) }, new[] { 0.0, -0.4, 0.2 }),
            };
            var classes = new[] { typeof(DxfLine), typeof(DxfCircle), typeof(DxfEllipse), typeof(DxfRay), typeof(DxfXline), typeof(DxfSpline), typeof(DxfPolyline), typeof(DxfPolyline) };

            for (int i = 0; i < shapes.Length; i++)
            {
                DxfCurve curve = DxfCurve.CreateFromGeCurve(shapes[i]);
                Assert.IsType(classes[i], curve);

                var db = DxfDatabase.Parse(Text);
                db.ModelSpace.AppendEntity(curve);
                Assert.Equal(shapes[i], DxfReader.Parse(db.ToText(), new DxfReadOptions { PlanAs2D = true, Tolerance = Tol }).Shapes.Last().Shape);
            }

            var arc = Assert.IsType<DxfArc>(DxfCurve.CreateFromGeCurve(new GeoArc2(new GeoPoint2(3, 4), 12.5, 0.25, 1.0)));
            Assert.Equal(12.5, arc.Radius);
        }

        #endregion

        #region The segments of the plane

        [Fact]
        public void TheSegmentsOfThePlaneOfATiltedAndAMirroredPolyline_AreThoseOfSpace_InTheSystemOfItsNormal()
        {
            DxfDatabase db = DxfDatabase.Parse(Text);

            foreach (string handle in new[] { "81", "82" })
            {
                DxfPolyline polyline = ByHandle<DxfPolyline>(db, handle);
                GeoVector3 n = polyline.Normal;
                Axes(n, out GeoVector3 u, out GeoVector3 v);
                GeoPoint3 Lift(GeoPoint2 p) => new GeoPoint3(0, 0, 0).Add(u.Multiply(p.X)).Add(v.Multiply(p.Y)).Add(n.Multiply(polyline.Elevation));

                GeoLine2 line = polyline.GetLineSegment2dAt(0);
                GeoLine3 line3 = polyline.GetLineSegmentAt(0);
                Assert.Equal(polyline.GetPoint2dAt(0), line.StartPoint);
                Assert.Equal(polyline.GetPoint2dAt(1), line.EndPoint);
                Assert.True(Lift(line.EndPoint).DistanceTo(line3.EndPoint) <= 1E-12);

                GeoArc2 arc = polyline.GetArcSegment2dAt(1);
                GeoArc3 arc3 = polyline.GetArcSegmentAt(1);
                Assert.Equal(arc.Bulge, polyline.GetBulgeAt(1), 12);
                Assert.True(Lift(arc.StartPoint).DistanceTo(arc3.StartPoint) <= 1E-9);
                Assert.True(Lift(arc.MidPoint).DistanceTo(arc3.MidPoint) <= 1E-9);
                Assert.True(Lift(arc.EndPoint).DistanceTo(arc3.EndPoint) <= 1E-9);
                Assert.Throws<InvalidOperationException>(() => polyline.GetArcSegment2dAt(0));
                Assert.Throws<InvalidOperationException>(() => polyline.GetLineSegment2dAt(1));
            }

            // Mirrored, the arc of bulge 0.5 runs counter-clockwise in the system of its normal, as AutoCAD gives it there.
            Assert.False(ByHandle<DxfPolyline>(db, "82").GetArcSegment2dAt(1).IsClockwise);
            Assert.Equal(new GeoPoint2(10, 0), ByHandle<DxfPolyline>(db, "82").GetArcSegment2dAt(1).StartPoint);
        }

        #endregion
    }
}
