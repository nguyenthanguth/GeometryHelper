using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Enums;
using GeometryHelper.Export;
using GeometryHelper.Geometry;
using GeometryHelper.Meshing;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfLines;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// What the review of the first reading found: polylines at an elevation, entities lying on the floor mirrored, one
    /// entity that throws, the text of a code page, loops of no area, numbers of thousands of digits, and the rest. The
    /// expected points of the mirrored entities are those ezdxf 1.4.5 works out for the same groups.
    /// </summary>
    public class DxfReaderReviewTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static DxfDocument Plan(string text) => DxfReader.Parse(text, new DxfReadOptions { PlanAs2D = true, Tolerance = Tol });

        private static DxfDocument Space(string text) => DxfReader.Parse(text, new DxfReadOptions { Tolerance = Tol });

        private static void Near(GeoPoint3 expected, GeoPoint3 actual, string what)
            => Assert.True(expected.DistanceTo(actual) <= 1E-12, what + ": " + expected + " read as " + actual);

        private static void Near(GeoPoint2 expected, GeoPoint2 actual, string what)
            => Assert.True(expected.DistanceTo(actual) <= 1E-12, what + ": " + expected + " read as " + actual);

        // ----- Must -------------------------------------------------------------------------------------------------------

        [Fact]
        public void A2DPolylineStandsAtThePolylinesElevation_NotAtTheHeightsOfItsVertices()
        {
            // ezdxf's add_polyline2d with the elevation (0, 0, 50) writes the POLYLINE's 30 as 50 and every VERTEX's as 0; ezdxf
            // places it at z = 50, and on the extrusion (0, 0, -1) at z = -50, x turned over.
            string up = R2000(G(0, "POLYLINE", 8, "0", 100, "AcDb2dPolyline", 66, 1, 10, "0", 20, "0", 30, "50", 70, 0)
                + G(0, "VERTEX", 8, "0", 100, "AcDbVertex", 100, "AcDb2dVertex", 10, "0", 20, "0", 30, "0")
                + G(0, "VERTEX", 8, "0", 100, "AcDbVertex", 100, "AcDb2dVertex", 10, "10", 20, "0", 30, "0")
                + G(0, "VERTEX", 8, "0", 100, "AcDbVertex", 100, "AcDb2dVertex", 10, "10", 20, "10", 30, "0")
                + G(0, "SEQEND", 8, "0"));
            string down = R2000(G(0, "POLYLINE", 8, "0", 100, "AcDb2dPolyline", 66, 1, 10, "0", 20, "0", 30, "50", 70, 1, 210, "0", 220, "0", 230, "-1")
                + G(0, "VERTEX", 8, "0", 100, "AcDbVertex", 100, "AcDb2dVertex", 10, "0", 20, "0", 30, "0", 42, "0.5")
                + G(0, "VERTEX", 8, "0", 100, "AcDbVertex", 100, "AcDb2dVertex", 10, "10", 20, "0", 30, "0")
                + G(0, "VERTEX", 8, "0", 100, "AcDbVertex", 100, "AcDb2dVertex", 10, "10", 20, "10", 30, "0")
                + G(0, "SEQEND", 8, "0"));
            string noElevation = R12(G(0, "POLYLINE", 8, "0", 66, 1, 70, 0)
                + G(0, "VERTEX", 8, "0", 10, "0", 20, "0", 30, "7") + G(0, "VERTEX", 8, "0", 10, "10", 20, "0", 30, "7") + G(0, "SEQEND", 8, "0"));

            var chain = Assert.IsType<GeoPolyline3>(Assert.Single(Plan(up).Shapes).Shape);
            Assert.Equal(new[] { new GeoPoint3(0, 0, 50), new GeoPoint3(10, 0, 50), new GeoPoint3(10, 10, 50) }, chain.Vertices.ToArray());

            var loop = Assert.IsType<GeoPolygonArc3>(Assert.Single(Plan(down).Shapes).Shape);
            Assert.Equal(new[] { new GeoPoint3(0, 0, -50), new GeoPoint3(-10, 0, -50), new GeoPoint3(-10, 10, -50) }, loop.Vertices.ToArray());
            Near(new GeoPoint3(-5, -2.5, -50), loop.GetEdgeAt(0).MidPoint, "the middle of the arc");

            // ezdxf's point of that arc lies on it, to the 6E-6 its flattening of cubic pieces stands off a circle.
            GeoArc3 arc = loop.GetEdgeAt(0).ToArc();
            Assert.True(Math.Abs(arc.Radius - arc.Center.DistanceTo(new GeoPoint3(-5.184074754330292, -2.4972949873672445, -50.0))) < 1E-5);

            // Where the POLYLINE gives no elevation the first vertex's height is it.
            Assert.All(Assert.IsType<GeoPolyline3>(Assert.Single(Plan(noElevation).Shapes).Shape).Vertices, v => Assert.Equal(7.0, v.Z));
        }

        [Fact]
        public void ALineAndAPointAtHeightNought_AreOfThePlane_WhateverTheirExtrusion()
        {
            string text = R2000(
                G(0, "LINE", 8, "0", 10, "1", 20, "2", 30, "0", 11, "4", 21, "5", 31, "0", 210, "0", 220, "0", 230, "-1")
                + G(0, "POINT", 8, "0", 10, "1", 20, "2", 30, "0", 210, "1", 220, "0", 230, "0"));

            DxfDocument read = Plan(text);

            Assert.Equal(new GeoPoint2(4, 5), Assert.IsType<GeoLine2>(read.Shapes[0].Shape).EndPoint);
            Assert.Equal(new GeoPoint2(1, 2), Assert.IsType<GeoPoint2>(read.Shapes[1].Shape));
        }

        [Fact]
        public void EntitiesOnTheMirroredExtrusionAtElevationNought_AreShapesOfThePlane_WhereEzdxfPlacesThem()
        {
            string text = R2000(
                G(0, "ARC", 8, "0", 100, "AcDbCircle", 10, "0", 20, "0", 30, "0", 40, "10", 210, "0", 220, "0", 230, "-1", 100, "AcDbArc", 50, "0", 51, "90")
                + G(0, "CIRCLE", 8, "0", 100, "AcDbCircle", 10, "10", 20, "20", 30, "0", 40, "3", 210, "0", 220, "0", 230, "-1")
                + G(0, "TRACE", 8, "0", 100, "AcDbTrace", 10, "0", 20, "0", 30, "0", 11, "10", 21, "0", 31, "0", 12, "0", 22, "10", 32, "0", 13, "10", 23, "10", 33, "0", 210, "0", 220, "0", 230, "-1")
                + G(0, "LWPOLYLINE", 8, "0", 100, "AcDbPolyline", 90, 4, 70, 1, 10, "0", 20, "0", 42, "0.5", 10, "10", 20, "0", 42, "-1", 10, "10", 20, "10", 10, "0", 20, "10", 42, "0.3", 210, "0", 220, "0", 230, "-1")
                + G(0, "LWPOLYLINE", 8, "0", 100, "AcDbPolyline", 90, 4, 70, 1, 10, "0", 20, "0", 42, "0.5", 10, "10", 20, "0", 42, "-1", 10, "10", 20, "10", 10, "0", 20, "10", 42, "0.3")
                + G(0, "ELLIPSE", 8, "0", 100, "AcDbEllipse", 10, "1", 20, "2", 30, "0", 11, "20", 21, "5", 31, "0", 210, "0", 220, "0", 230, "-1", 40, "0.4", 41, "5.5", 42, "0.5")
                + G(0, "SPLINE", 8, "0", 100, "AcDbSpline", 210, "0", 220, "0", 230, "-1", 70, 8, 71, 1, 72, 4, 73, 2, 74, 0, 40, "0", 40, "0", 40, "1", 40, "1", 10, "0", 20, "0", 30, "0", 10, "5", 20, "5", 30, "0"));

            DxfDocument read = Plan(text);

            // ezdxf: the arc from (-10, 0) through (-7.0711, 7.0711) to (0, 10); read so, clockwise.
            var arc = Assert.IsType<GeoArc2>(read.Shapes[0].Shape);
            Assert.True(arc.IsClockwise);
            Near(new GeoPoint2(-10, 0), arc.StartPoint, "the start of the arc");
            Near(new GeoPoint2(0, 10), arc.EndPoint, "the end of the arc");
            Near(new GeoPoint2(-7.0710678118654755, 7.0710678118654755), arc.MidPoint, "the middle of the arc");

            Assert.Equal(new GeoPoint2(-10, 20), Assert.IsType<GeoCircle2>(read.Shapes[1].Shape).Center);

            // ezdxf: the trace's third corner (-10, 10).
            Assert.Equal(new[] { new GeoPoint2(0, 0), new GeoPoint2(-10, 0), new GeoPoint2(-10, 10), new GeoPoint2(0, 10) }, Assert.IsType<GeoPolygon2>(read.Shapes[2].Shape).Vertices.ToArray());

            // The mirrored loop is the loop on up with x turned over: its vertices, its bulges negated, its area and the middles of its edges.
            var mirrored = Assert.IsType<GeoPolygonArc2>(read.Shapes[3].Shape);
            var plain = Assert.IsType<GeoPolygonArc2>(read.Shapes[4].Shape);
            Assert.Equal(plain.Area, mirrored.Area, 12);

            for (int i = 0; i < 4; i++)
            {
                Assert.Equal(-plain.Vertices[i].X, mirrored.Vertices[i].X);
                Assert.Equal(plain.Vertices[i].Y, mirrored.Vertices[i].Y);
                Assert.Equal(-plain.Bulges[i], mirrored.Bulges[i]);
                GeoPoint2 m = plain.GetEdgeAt(i).MidPoint;
                Near(new GeoPoint2(-m.X, m.Y), mirrored.GetEdgeAt(i).MidPoint, "the middle of edge " + i);
            }

            // ezdxf: the ellipse part from (13.7623, 11.1877) to (19.5105, 2.5525), at z = 3 there and nought here.
            var part = Assert.IsType<GeoSpline2>(read.Shapes[5].Shape);
            Assert.True(part.StartPoint.DistanceTo(new GeoPoint2(13.762314834684418, 11.187671476019437)) < 1E-12, part.StartPoint.ToString());
            Assert.True(part.EndPoint.DistanceTo(new GeoPoint2(19.510502315015863, 2.552508500618243)) < 1E-12, part.EndPoint.ToString());

            Assert.IsType<GeoSpline2>(read.Shapes[6].Shape);
        }

        [Fact]
        public void AnEntityThatThrows_IsLeftOutAndCounted_AndTheOthersRead()
        {
            string text = R2000(
                G(0, "LWPOLYLINE", 8, "0", 100, "AcDbPolyline", 90, 3, 70, 1, 10, "0", 20, "0", 42, "0.5", 10, "1e200", 20, "0", 10, "1e200", 20, "1e200")
                + G(0, "SPLINE", 8, "0", 100, "AcDbSpline", 70, 8, 71, 3, 72, 0, 73, 0, 74, 4, 11, "0", 21, "0", 31, "0", 11, "1e200", 21, "0", 31, "0", 11, "2e200", 21, "1", 31, "0", 11, "3", 21, "3", 31, "0")
                + G(0, "LINE", 8, "0", 10, "0", 20, "0", 11, "1", 21, "0"));

            var log = new List<string>();
            Action<GeometryHelperLogLevel, string, Exception> before = GeometryHelperLog.Writer;
            GeometryHelperLog.Writer = (level, message, exception) => log.Add(level + " " + message);
            DxfDocument read;

            try
            {
                read = Plan(text);
            }
            finally
            {
                GeometryHelperLog.Writer = before;
            }

            // The fit points 1E200 apart make no system that can be solved: that SPLINE alone is left out. The polyline of them is read.
            Assert.Equal(new[] { "LWPOLYLINE", "LINE" }, read.Shapes.Select(e => e.Type).ToArray());
            Assert.Equal(1, read.Skipped["SPLINE"]);
            Assert.Contains(read.Warnings, w => w.StartsWith("1 SPLINE could not be read and was left out: InvalidOperationException", StringComparison.Ordinal));
            Assert.Contains("Warn DxfReader: 1 SPLINE could not be read and was left out.", log);
        }

        // ----- Should -----------------------------------------------------------------------------------------------------

        [Fact]
        public void TheTextOfWindows1252_ReadsTheSameOnEitherRuntime_AndUtf8InAnOldFileIsUtf8()
        {
            // "Wand–€Außen": the en dash is 0x96 and the euro 0x80, where Latin-1 has control characters.
            byte[] name = { (byte)'W', (byte)'a', (byte)'n', (byte)'d', 0x96, 0x80, (byte)'A', (byte)'u', 0xDF, (byte)'e', (byte)'n' };
            byte[] head = Encoding.ASCII.GetBytes(G(0, "SECTION", 2, "HEADER", 9, "$ACADVER", 1, "AC1015", 9, "$DWGCODEPAGE", 3, "ANSI_1252", 0, "ENDSEC", 0, "SECTION", 2, "ENTITIES", 0, "POINT") + "  8\n");
            byte[] tail = Encoding.ASCII.GetBytes("\n" + G(10, "0", 20, "0", 0, "ENDSEC", 0, "EOF"));

            DxfDocument western = DxfReader.Read(new MemoryStream(head.Concat(name).Concat(tail).ToArray()));
            Assert.Equal("Wand–€Außen", Assert.Single(western.Shapes).Layer);
            Assert.Empty(western.Warnings);

            DxfDocument utf8 = DxfReader.Read(new MemoryStream(head.Concat(Encoding.UTF8.GetBytes("Kết cấu")).Concat(tail).ToArray()));
            Assert.Equal("Kết cấu", Assert.Single(utf8.Shapes).Layer);
            Assert.Contains(utf8.Warnings, w => w.Contains("UTF-8"));
        }

        [Fact]
        public void AClosedPolylineThatEnclosesNoArea_IsAnOpenChainEndingOnItsFirstVertex_InThePlaneAsInSpace()
        {
            string straight = R2000(G(0, "LWPOLYLINE", 8, "0", 100, "AcDbPolyline", 90, 3, 70, 1, 10, "0", 20, "0", 10, "5", 20, "0", 10, "10", 20, "0"));
            string bulged = R2000(G(0, "LWPOLYLINE", 8, "0", 100, "AcDbPolyline", 90, 2, 70, 1, 10, "0", 20, "0", 42, "1", 10, "10", 20, "0", 42, "-1"));

            foreach (DxfDocument read in new[] { Plan(straight), Space(straight) })
            {
                object shape = Assert.Single(read.Shapes).Shape;
                IReadOnlyList<GeoPoint3> vertices = shape is GeoPolyline2 flat ? flat.Vertices.Select(p => new GeoPoint3(p.X, p.Y, 0)).ToList() : Assert.IsType<GeoPolyline3>(shape).Vertices;
                Assert.Equal(4, vertices.Count);
                Assert.Equal(vertices[0], vertices[3]);
                Assert.Contains(read.Warnings, w => w.Contains("open chain"));
            }

            // Two half circles, one there and one back the same way: no area.
            foreach (DxfDocument read in new[] { Plan(bulged), Space(bulged) })
            {
                object shape = Assert.Single(read.Shapes).Shape;
                Assert.True(shape is GeoPolylineArc2 || shape is GeoPolylineArc3, shape.GetType().Name);
                Assert.Contains(read.Warnings, w => w.Contains("open chain"));
            }
        }

        [Fact]
        public void ANumberOfThousandsOfDigits_IsReadAsTheDoubleNearestIt()
        {
            Assert.True(Numbers.TryRead("1" + new string('0', 9999) + "e-9995", out double ten));
            Assert.Equal(10000.0, ten);
            Assert.True(Numbers.TryRead("1" + new string('0', 99999) + "e-99999", out double one));
            Assert.Equal(1.0, one);
            Assert.True(Numbers.TryRead("0." + new string('3', 300000), out double third));
            Assert.Equal(1.0 / 3.0, third);
            Assert.True(Numbers.TryRead("4.8721540113315704" + new string('0', 5000) + "1", out double hair));
            Assert.Equal(4.8721540113315704, hair);
            Assert.False(Numbers.TryRead("1" + new string('0', 400), out _));
        }

        [Fact]
        public void WithoutTheGroups_TheShapesAndTheLookAreReadTheSame_AndTheGroupsAreEmpty()
        {
            string text = R2000(G(0, "LINE", 5, "2A", 8, "steel", 62, 3, 10, "0", 20, "0", 11, "10", 21, "0", 1001, "ACAD", 1000, "a note"));
            DxfDocument kept = Plan(text);
            DxfDocument dropped = DxfReader.Parse(text, new DxfReadOptions { PlanAs2D = true, KeepGroups = false, Tolerance = Tol });

            Assert.NotEmpty(kept.Shapes[0].Groups);
            Assert.Empty(dropped.Shapes[0].Groups);
            Assert.Equal(kept.Shapes[0].Color, dropped.Shapes[0].Color);
            Assert.Equal(kept.Shapes[0].Handle, dropped.Shapes[0].Handle);
            Assert.Equal(((GeoLine2)kept.Shapes[0].Shape).EndPoint, ((GeoLine2)dropped.Shapes[0].Shape).EndPoint);
        }

        // ----- Notes ------------------------------------------------------------------------------------------------------

        [Fact]
        public void TheEdgesA3DFaceHides_AreHiddenAgainWhenItIsWrittenBack()
        {
            string text = R2000(G(0, "3DFACE", 8, "0", 100, "AcDbFace", 10, "0", 20, "0", 30, "0", 11, "10", 21, "0", 31, "0", 12, "10", 22, "10", 32, "0", 13, "0", 23, "10", 33, "0", 70, 5)
                + G(0, "3DFACE", 8, "0", 100, "AcDbFace", 10, "0", 20, "0", 30, "0", 11, "10", 21, "0", 31, "0", 12, "0", 22, "10", 32, "0", 13, "0", 23, "10", 33, "0", 70, 2));

            DxfDocument read = Space(text);
            var writer = new DxfWriter();
            writer.Add(read.Shapes[0]).Add(read.Shapes[1]);
            DxfDocument again = Space(writer.ToString());

            Assert.Equal(new[] { "5", "2" }, again.Shapes.Select(e => e.Groups.Single(g => g.TypeCode == 70).Value).ToArray());
        }

        [Fact]
        public void AClosedSplineFlaggedOnlyClosed_IsClampedToo_AndASplineThatCannotBeRead_IsWarnedAbout()
        {
            string text = R2000(
                G(0, "SPLINE", 8, "0", 70, 1, 71, 2, 72, 6, 73, 3, 74, 0, 40, "0", 40, "1", 40, "2", 40, "3", 40, "4", 40, "5",
                  10, "0", 20, "0", 30, "0", 10, "10", 20, "0", 30, "0", 10, "10", 20, "10", 30, "0")
                + G(0, "SPLINE", 8, "0", 70, 8, 71, 3, 72, 3, 73, 4, 74, 0, 40, "0", 40, "0", 40, "1",
                  10, "0", 20, "0", 30, "0", 10, "10", 20, "0", 30, "0", 10, "10", 20, "10", 30, "0", 10, "0", 20, "10", 30, "0")
                + G(0, "SPLINE", 8, "0", 70, 8, 71, 3, 72, 0, 73, 0, 74, 2, 11, "0", 21, "0", 31, "0", 11, "10", 21, "5", 31, "0"));

            DxfDocument read = Plan(text);

            // The quadratic over the knots 2 to 3 starts half way along its first leg, at (5, 0), and ends half way along its second.
            var closed = Assert.IsType<GeoSpline2>(read.Shapes[0].Shape);
            Near(new GeoPoint2(5, 0), closed.StartPoint, "the start");
            Near(new GeoPoint2(10, 5), closed.EndPoint, "the end");

            // Two fit points of a cubic: the straight line through them, at degree 1.
            var straight = Assert.IsType<GeoSpline2>(read.Shapes[1].Shape);
            Assert.Equal(1, straight.Degree);
            Assert.Contains(read.Warnings, w => w.Contains("one degree less"));

            Assert.Equal(1, read.Skipped["SPLINE"]);
            Assert.Contains(read.Warnings, w => w.Contains("SPLINE was not read"));
        }

        [Fact]
        public void TheBlocksAreRead_AndAWarningSaysWhenTheFileNamesNoUnit()
        {
            string blocks = G(0, "SECTION", 2, "BLOCKS", 0, "BLOCK", 2, "B", 70, 0, 10, "0", 20, "0",
                0, "POLYLINE", 8, "0", 66, 1, 70, 8, 0, "VERTEX", 8, "0", 10, "0", 20, "0", 30, "0", 0, "VERTEX", 8, "0", 10, "1", 20, "0", 30, "0", 0, "SEQEND", 8, "0",
                0, "LINE", 8, "0", 10, "0", 20, "0", 11, "1", 21, "0", 0, "ENDBLK", 0, "ENDSEC");
            string text = G(0, "SECTION", 2, "HEADER", 9, "$ACADVER", 1, "AC1015", 0, "ENDSEC") + blocks + G(0, "SECTION", 2, "ENTITIES", 0, "POINT", 8, "0", 10, "1", 20, "1", 0, "ENDSEC", 0, "EOF");

            DxfDocument read = DxfReader.Parse(text, new DxfReadOptions { ToMillimeters = true, Tolerance = Tol });

            Assert.Equal(new[] { "POLYLINE", "LINE" }, Assert.Single(read.Blocks).Shapes.Select(e => e.Type).ToArray());
            Assert.Contains(read.Warnings, w => w.StartsWith("The file names no unit", StringComparison.Ordinal));
            Assert.Single(read.Warnings);
        }

        [Fact]
        public void AGroupMissingAPart_ReadsThatPartAsNought_TheSameForEveryEntity()
        {
            string text = R2000(
                G(0, "POINT", 8, "0", 10, "1", 30, "5")
                + G(0, "CIRCLE", 8, "0", 10, "0", 20, "0", 40, "2", 210, "1", 230, "1"));

            DxfDocument read = Space(text);

            Assert.Equal(new GeoPoint3(1, 0, 5), Assert.IsType<GeoPoint3>(read.Shapes[0].Shape));
            GeoVector3 normal = Assert.IsType<GeoCircle3>(read.Shapes[1].Shape).Normal;
            Assert.Equal(Math.Sqrt(0.5), normal.X, 15);
            Assert.Equal(0.0, normal.Y);
            Assert.Equal(Math.Sqrt(0.5), normal.Z, 15);
        }

        [Fact]
        public void AGroupOfTheLookThatCannotBeRead_IsLeftOut_AndTheEntityRead()
        {
            string text = R2000(G(0, "LINE", 8, "0", 62, "1.0", 420, "3271524352", 370, "x", 39, "thick", 10, "0", 20, "0", 11, "1", 21, "0"));
            DxfDocument read = Plan(text);
            DxfShape line = Assert.Single(read.Shapes);

            Assert.Null(line.Color);
            Assert.Null(line.TrueColor);
            Assert.Null(line.LineWeight);
            Assert.Equal(0.0, line.Thickness);
            Assert.Contains(read.Warnings, w => w.StartsWith("4 groups of the colour", StringComparison.Ordinal));
        }

        [Fact]
        public void TheSkippedCountsCannotBeChanged_ThroughTheirInterface()
        {
            DxfDocument read = Plan(R2000(G(0, "TEXT", 8, "0", 10, "0", 20, "0", 1, "a")));
            var counts = (IDictionary<string, int>)read.Skipped;

            Assert.Throws<NotSupportedException>(() => counts.Add("LINE", 1));
        }

        [Fact]
        public void EntitiesMadeInCode_AreWrittenWithTheirLook_EveryShapeOfThem()
        {
            var face = new GeoFace2(new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10), new GeoPoint2(0, 10)), new[] { new GeoPolygon2(new GeoPoint2(2, 2), new GeoPoint2(4, 2), new GeoPoint2(4, 4)) });
            GeoMesh3 mesh = new GeoFace3(new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(10, 10, 0), new GeoPoint3(0, 10, 0))).ToMesh(MeshOptions.Grid(5, 5));
            object[] shapes =
            {
                new GeoRectangle2(new GeoPoint2(5, 5), 10, 4), new GeoTriangle2(new GeoPoint2(0, 0), new GeoPoint2(4, 0), new GeoPoint2(0, 3)), face, mesh,
                new GeoAabb3(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 2, 3)), new GeoObb3(new GeoPoint3(0, 0, 0), 1, 2, 3),
            };
            int[] entities = { 1, 1, 2, mesh.FaceCount, 6, 6 };

            for (int i = 0; i < shapes.Length; i++)
            {
                var entity = new DxfShape(shapes[i], "steel", color: 1, trueColor: 0xFF8000, linetype: "DASHED", lineWeight: 35);
                DxfDocument read = Space(new DxfWriter().Add(entity).ToString());

                Assert.Equal(entities[i], read.Shapes.Count);
                Assert.All(read.Shapes, e => Assert.True(e.Layer == "steel" && e.Color == 1 && e.TrueColor == 0xFF8000 && e.Linetype == "DASHED" && e.LineWeight == 35, shapes[i].GetType().Name));
            }

            Assert.Null(new DxfShape(new GeoPoint2(1, 2)).Type);
            Assert.Equal("0", new DxfShape(new GeoPoint2(1, 2)).Layer);
            Assert.Throws<ArgumentNullException>(() => new DxfShape(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DxfShape(new GeoPoint2(1, 2), color: 256));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DxfShape(new GeoPoint2(1, 2), trueColor: 0x1000000));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DxfShape(new GeoPoint2(1, 2), lineWeight: 212));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DxfShape(new GeoPoint2(1, 2), thickness: double.NaN));
        }

        // ----- The last round -------------------------------------------------------------------------------------------

        [Fact]
        public void ANumberScaledPastTheLargestDouble_LeavesItsEntityOutAsNotFinite_AndAThicknessSoIsLeftOutAlone()
        {
            string text = File("AC1015", 6, string.Empty, G(
                0, "POINT", 8, "0", 10, "1", 20, "-1e308", 30, "0",
                0, "LINE", 8, "0", 10, "0", 20, "0", 11, "1e307", 21, "0",
                0, "LWPOLYLINE", 8, "0", 90, 2, 70, 0, 10, "0", 20, "0", 10, "1e307", 20, "0",
                0, "LINE", 8, "0", 39, "1e307", 10, "0", 20, "0", 11, "1", 21, "0"));

            DxfDocument read = DxfReader.Parse(text, new DxfReadOptions { ToMillimeters = true, Tolerance = Tol });
            DxfShape line = Assert.Single(read.Shapes);

            Assert.Equal(new GeoPoint3(1000, 0, 0), Assert.IsType<GeoLine3>(line.Shape).EndPoint);
            Assert.Equal(0.0, line.Thickness);
            Assert.Equal(1, read.Skipped["POINT"]);
            Assert.Equal(1, read.Skipped["LINE"]);
            Assert.Equal(1, read.Skipped["LWPOLYLINE"]);
            Assert.Contains(read.Warnings, w => w.StartsWith("1 group of the colour, lineweight, true colour or thickness", StringComparison.Ordinal));
            Assert.Contains("39\n1000\n", new DxfWriter().Add(new DxfShape(new GeoLine2(0, 0, 1, 0), thickness: 1000)).ToString().Replace(" ", string.Empty));
        }

        [Fact]
        public void AMirroredArcOnTheFloor_RunsClockwiseFromTheStartTheFileGives_AsTheArcReadInSpaceDoes()
        {
            string text = R2000(G(0, "ARC", 8, "0", 100, "AcDbCircle", 10, "3", 20, "4", 30, "0", 40, "7", 210, "0", 220, "0", 230, "-1", 100, "AcDbArc", 50, "30", 51, "300"));

            var flat = Assert.IsType<GeoArc2>(Assert.Single(Plan(text).Shapes).Shape);
            var space = Assert.IsType<GeoArc3>(Assert.Single(Space(text).Shapes).Shape);

            Assert.True(flat.IsClockwise);
            Near(new GeoPoint2(space.StartPoint.X, space.StartPoint.Y), flat.StartPoint, "the start");
            Near(new GeoPoint2(space.EndPoint.X, space.EndPoint.Y), flat.EndPoint, "the end");
            Near(new GeoPoint2(space.MidPoint.X, space.MidPoint.Y), flat.MidPoint, "the middle");
            Assert.Equal(space.Length, flat.Length, 12);

            // A mirrored polyline keeps the order of its vertices, the first the file gives first.
            string chain = R2000(G(0, "LWPOLYLINE", 8, "0", 100, "AcDbPolyline", 90, 2, 70, 0, 10, "1", 20, "0", 42, "0.5", 10, "5", 20, "0", 210, "0", 220, "0", 230, "-1"));
            var bent = Assert.IsType<GeoPolylineArc2>(Assert.Single(Plan(chain).Shapes).Shape);
            var bentInSpace = Assert.IsType<GeoPolylineArc3>(Assert.Single(Space(chain).Shapes).Shape);
            Assert.Equal(new GeoPoint2(-1, 0), bent.StartPoint);
            Near(new GeoPoint2(bentInSpace.GetEdgeAt(0).MidPoint.X, bentInSpace.GetEdgeAt(0).MidPoint.Y), bent.GetEdgeAt(0).MidPoint, "the middle of the arc");
        }

        [Fact]
        public void ALineweightNotAmongAutoCADs_IsLeftOutWithTheWarning_AndRefusedInCode()
        {
            DxfDocument read = Plan(R2000(G(0, "LINE", 8, "0", 370, 37, 10, "0", 20, "0", 11, "1", 21, "0", 0, "LINE", 8, "0", 370, 211, 10, "0", 20, "0", 11, "1", 21, "0")));

            Assert.Null(read.Shapes[0].LineWeight);
            Assert.Equal(211, read.Shapes[1].LineWeight);
            Assert.Contains(read.Warnings, w => w.StartsWith("1 group of the colour, lineweight", StringComparison.Ordinal));
            Assert.Contains("0, 5, 9, 13", Assert.Throws<ArgumentOutOfRangeException>(() => new DxfShape(new GeoPoint2(1, 2), lineWeight: 37)).Message);
            Assert.Equal(158, new DxfShape(new GeoPoint2(1, 2), lineWeight: 158).LineWeight);
        }
    }
}
